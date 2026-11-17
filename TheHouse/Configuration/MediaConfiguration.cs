using MongoDB.Driver;
using TheHouse.Application.Abstractions.Media;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Media;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.Configuration;

// NV-13: fotos y documentos. La llave maestra llega por el secreto de
// Container Apps `Media__DocumentMasterKey` (M-BE-038); nunca se registra.
// Requiere AddModeration (StaffService) y AddFavoritesAndVisits (IUserLock).
public static class MediaConfiguration
{
    public static void AddMedia(this WebApplicationBuilder builder)
    {
        var options = new MediaOptions
        {
            RequirePublicationFiles = builder.Configuration.GetValue("Media:RequirePublicationFiles", true),
        };
        var masterKey = AesGcmDocumentCipher.ParseMasterKey(builder.Configuration["Media:DocumentMasterKey"]);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IImageProcessor, SkiaImageProcessor>();
        builder.Services.AddSingleton<IDocumentCipher>(new AesGcmDocumentCipher(masterKey));
        builder.Services.AddSingleton<MediaService>();
        builder.Services.AddSingleton<MediaPort>();
        builder.Services.AddSingleton<IMediaPort>(provider => provider.GetRequiredService<MediaPort>());
        builder.Services.AddHostedService<MediaRetentionWorker>();

        builder.Services.AddSingleton<IMediaRepository>(provider =>
        {
            var database = provider.GetService<IMongoDatabase>();
            return database is null
                ? new UnavailableMediaRepository()
                : new MongoMediaRepository(database);
        });
    }
}

// NV-13: borra cada 6 horas los documentos con retención vencida, con un
// candado de Redis para que solo una réplica lo haga.
public sealed class MediaRetentionWorker(MediaPort media, IUserLock userLock, ILogger<MediaRetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var held = await userLock.TryAcquireAsync(RateLimitKeys.MediaRetentionLock(), TimeSpan.FromMinutes(10), stoppingToken);
                if (held is null)
                {
                    continue;
                }

                var deleted = await media.PurgeExpiredDocumentsAsync(stoppingToken);
                if (deleted > 0)
                {
                    logger.LogInformation("Deleted {Count} verification documents past their retention.", deleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is DependencyUnavailableException or TimeoutException)
            {
                logger.LogWarning("Document retention cleanup skipped ({ErrorType}).", exception.GetType().Name);
            }
        }
    }
}
