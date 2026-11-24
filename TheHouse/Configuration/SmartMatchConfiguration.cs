using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.SmartMatch;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.GenAi;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.Configuration;

// NV-23 1.2.0: Smart Match con el modelo base `baseline-1.0.0`. Requiere los
// módulos de perfil, listados, búsqueda y moderación registrados.
public static class SmartMatchConfiguration
{
    public static void AddSmartMatch(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ICompatibilityModel, BaselineCompatibilityModel>();
        builder.Services.AddSingleton<SmartMatchService>();
        builder.Services.AddSingleton<ISmartMatchStatsSink, LoggingSmartMatchStatsSink>();
        builder.Services.AddSingleton<ISmartMatchRepository>(provider =>
        {
            var database = provider.GetService<IMongoDatabase>();
            return database is null
                ? new UnavailableSmartMatchRepository()
                : new MongoSmartMatchRepository(database);
        });

        // NV-24 1.2.1: explicaciones; GenAI solo con URL https y clave (si no, plantilla).
        builder.Services.AddSingleton<SmartMatchExplanationService>();
        builder.Services.AddSingleton<ISmartMatchExplanationEvents, LoggingExplanationEvents>();
        builder.Services.AddSingleton<ISmartMatchExplanationCache>(provider =>
        {
            var database = provider.GetService<IMongoDatabase>();
            return database is null
                ? new UnavailableSmartMatchExplanationCache()
                : new MongoSmartMatchExplanationCache(database);
        });

        var genAi = builder.Configuration.GetSection("GenAI").Get<GenAiOptions>() ?? new GenAiOptions();
        if (genAi.IsUsable(out _))
        {
            var http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15) })
            {
                BaseAddress = new Uri(genAi.BaseUrl!.TrimEnd('/') + "/"),
                Timeout = Timeout.InfiniteTimeSpan,
            };
            builder.Services.AddSingleton<IGenAiExplanationClient>(provider => new GenAiExplanationClient(
                http,
                genAi,
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<ILogger<GenAiExplanationClient>>()));
        }
        else
        {
            builder.Services.AddSingleton<IGenAiExplanationClient, DisabledGenAiExplanationClient>();
        }

        builder.Services.AddHostedService(provider => new GenAiStartupNotice(genAi, provider.GetRequiredService<ILogger<GenAiStartupNotice>>()));
    }
}

// Sin texto ni datos del usuario: solo el motivo del rechazo.
public sealed class LoggingExplanationEvents(ILogger<LoggingExplanationEvents> logger) : ISmartMatchExplanationEvents
{
    public void Rejected(string reason) =>
        logger.LogWarning("NV-24: resumen de TheHouse-GenAI rechazado por la validación ({Reason}); se usa la plantilla.", reason);
}

// Aviso al iniciar: si GenAI no está activo, las explicaciones usan plantilla.
public sealed class GenAiStartupNotice(GenAiOptions options, ILogger<GenAiStartupNotice> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.IsUsable(out var problem))
        {
            logger.LogInformation("NV-24: TheHouse-GenAI activo para las explicaciones.");
        }
        else
        {
            logger.LogInformation("NV-24: explicaciones con plantilla; TheHouse-GenAI {Problem}.", problem);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

// Las métricas (RF-66) nunca hacen fallar la respuesta al usuario: si el
// contador no se actualiza, se deja una advertencia sin datos del usuario.
public sealed class LoggingSmartMatchStatsSink(ISmartMatchRepository repository, ILogger<LoggingSmartMatchStatsSink> logger) : ISmartMatchStatsSink
{
    public async Task RecordAsync(SmartMatchDailyStats delta, CancellationToken cancellationToken)
    {
        try
        {
            await repository.IncrementStatsAsync(delta, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning("NV-23: no se actualizaron las métricas de Smart Match ({Reason}).", exception.GetType().Name);
        }
    }
}
