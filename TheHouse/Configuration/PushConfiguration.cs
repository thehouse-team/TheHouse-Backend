using System.Threading.Channels;
using MongoDB.Driver;
using TheHouse.Application.Abstractions.Notifications;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Notifications;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.Configuration;

// NV-20 1.2.0: push con FCM. Sin el secreto `FIREBASE_SERVICE_ACCOUNT` se
// registran dispositivos pero no se envía nada (aviso en el log al iniciar).
public static class PushConfiguration
{
    public const string SecretName = "FIREBASE_SERVICE_ACCOUNT";

    public static void AddPush(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<PushDeviceService>();
        builder.Services.AddSingleton<IPushDeviceRepository>(provider =>
        {
            var database = provider.GetService<IMongoDatabase>();
            return database is null
                ? new UnavailablePushDeviceRepository()
                : new MongoPushDeviceRepository(database);
        });

        var account = FirebaseServiceAccount.Parse(builder.Configuration[SecretName]);
        if (account is null)
        {
            builder.Services.AddSingleton<IPushSender, DisabledPushSender>();
        }
        else
        {
            var http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15) })
            {
                Timeout = TimeSpan.FromSeconds(10),
            };
            builder.Services.AddSingleton<IPushSender>(provider => new FcmPushSender(http, account, provider.GetRequiredService<TimeProvider>()));
        }

        builder.Services.AddSingleton(provider => new PushDispatcher(
            provider.GetRequiredService<IPushDeviceRepository>(),
            provider.GetRequiredService<IPushSender>()));
        builder.Services.AddSingleton<ChannelPushQueue>();
        builder.Services.AddSingleton<IPushQueue>(provider => provider.GetRequiredService<ChannelPushQueue>());
        builder.Services.AddHostedService<PushWorker>();
    }
}

// Cola acotada en memoria: si se llena, el push se descarta (la bandeja
// conserva la notificación). La envía la réplica que la guardó.
public sealed class ChannelPushQueue
    : IPushQueue
{
    private readonly Channel<PushRequest> channel = Channel.CreateBounded<PushRequest>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public ChannelReader<PushRequest> Reader => channel.Reader;

    public bool TryEnqueue(PushRequest request) => channel.Writer.TryWrite(request);
}

public sealed class PushWorker(ChannelPushQueue queue, PushDispatcher dispatcher, IPushSender sender, ILogger<PushWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!sender.Enabled)
        {
            logger.LogWarning("Push deshabilitado: falta o no es válido el secreto {Secret}; la bandeja sigue funcionando.", PushConfiguration.SecretName);
        }

        await foreach (var request in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await dispatcher.DispatchAsync(request, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // Sin tokens, ids ni textos: solo el tipo de notificación y el error.
                logger.LogWarning("Push {NotificationType} no enviado ({ErrorType}).", request.Message.Type, exception.GetType().Name);
            }
        }
    }
}
