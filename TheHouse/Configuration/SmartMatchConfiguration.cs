using Microsoft.ML.OnnxRuntime;
using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.SmartMatch;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.GenAi;
using TheHouse.Infrastructure.Persistence.Mongo;
using TheHouse.Infrastructure.SmartMatch;

namespace TheHouse.Configuration;

// NV-23 1.2.0: Smart Match con el modelo base `baseline-1.0.0`. Requiere los
// módulos de perfil, listados, búsqueda y moderación registrados.
public static class SmartMatchConfiguration
{
    public static void AddSmartMatch(this WebApplicationBuilder builder)
    {
        // NV-23 1.3.0: ONNX `logreg-1.0.0` si está activado y pasa todas las
        // verificaciones; si no, el modelo base (sin error para el usuario).
        var modelOptions = builder.Configuration.GetSection("SmartMatch:Model").Get<SmartMatchModelOptions>() ?? new SmartMatchModelOptions();
        builder.Services.AddSingleton<BaselineCompatibilityModel>();
        builder.Services.AddSingleton<SmartMatchModelSelection>(provider => SmartMatchModelSelection.Create(
            modelOptions,
            provider.GetRequiredService<BaselineCompatibilityModel>(),
            provider.GetRequiredService<ILoggerFactory>().CreateLogger("TheHouse.SmartMatch")));
        builder.Services.AddSingleton<ICompatibilityModel>(provider => provider.GetRequiredService<SmartMatchModelSelection>().Model);
        builder.Services.AddHostedService(provider => new SmartMatchModelNotice(
            provider.GetRequiredService<SmartMatchModelSelection>(),
            provider.GetRequiredService<ILogger<SmartMatchModelNotice>>()));
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

public sealed class SmartMatchModelOptions
{
    public bool Enabled { get; init; }

    public string Path { get; init; } = "SmartMatch/model-v1.0.0/model.onnx";

    public string Sha256 { get; init; } = string.Empty;

    public string ModelVersion { get; init; } = string.Empty;

    public string FeatureSchemaVersion { get; init; } = SmartMatchRules.FeatureSchemaVersion;
}

// Modelo elegido al arrancar y el motivo, para el aviso y las pruebas.
public sealed class SmartMatchModelSelection(ICompatibilityModel model, string reason)
{
    // Caso dorado `perfect_100` de `golden_cases.json` (logreg-1.0.0): 93.
    public static readonly SmartMatchFeatures StartupCase = new(0, 0, 0.75, 1, 1, 1, 1, 0, 0, 1);

    public const int StartupExpectedScore = 93;

    public ICompatibilityModel Model { get; } = model;

    public string Reason { get; } = reason;

    public static SmartMatchModelSelection Create(SmartMatchModelOptions options, BaselineCompatibilityModel baseline, ILogger logger)
    {
        if (!options.Enabled)
        {
            return new(baseline, "disabled");
        }

        try
        {
            var path = System.IO.Path.IsPathRooted(options.Path) ? options.Path : System.IO.Path.Combine(AppContext.BaseDirectory, options.Path);
            var onnx = new OnnxCompatibilityModel(
                File.ReadAllBytes(path),
                options.Sha256,
                options.ModelVersion,
                options.FeatureSchemaVersion,
                baseline.Labels,
                baseline,
                reason => logger.LogError("NV-23: vector de features inválido ({Reason}); ese cuarto usa el modelo base.", reason));
            if (onnx.Score(StartupCase).MatchScore != StartupExpectedScore)
            {
                onnx.Dispose();
                return Fallback(baseline, logger, "startup-case");
            }

            return new(onnx, "onnx");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or OnnxRuntimeException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            // Sin la ruta ni el contenido del archivo en el registro.
            return Fallback(baseline, logger, exception.GetType().Name);
        }
    }

    private static SmartMatchModelSelection Fallback(BaselineCompatibilityModel baseline, ILogger logger, string reason)
    {
        logger.LogWarning("NV-23: el modelo ONNX no pasó las verificaciones ({Reason}); se usa el modelo base.", reason);
        return new(baseline, reason);
    }
}

public sealed class SmartMatchModelNotice(SmartMatchModelSelection selection, ILogger<SmartMatchModelNotice> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("NV-23: Smart Match con {ModelVersion} ({Reason}).", selection.Model.ModelVersion, selection.Reason);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
