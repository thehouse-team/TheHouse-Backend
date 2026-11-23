using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
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
    }
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
