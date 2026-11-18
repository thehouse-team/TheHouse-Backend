using MongoDB.Driver;
using StackExchange.Redis;
using TheHouse.Application.Abstractions.Analytics;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Infrastructure.Configuration;
using TheHouse.Infrastructure.Persistence.Mongo;
using TheHouse.Infrastructure.Security;

namespace TheHouse.Configuration;

// NV-25: estadísticas del arrendador y registro de vistas. Requiere los
// módulos de listados, favoritos y visitas, mensajes, rentas, reseñas y
// moderación registrados antes.
public static class AnalyticsConfiguration
{
    public static void AddAnalytics(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<LandlordStatsService>();
        builder.Services.AddSingleton<RoomViewService>();
        builder.Services.AddSingleton<IRoomViewSink, LoggingRoomViewSink>();

        builder.Services.AddSingleton<IRoomViewRepository>(provider =>
        {
            var database = provider.GetService<IMongoDatabase>();
            return database is null
                ? new UnavailableRoomViewRepository()
                : new MongoRoomViewRepository(database);
        });

        var redis = builder.Configuration.GetSection("Redis").Get<RedisOptions>() ?? new RedisOptions();
        ConfigurationOptions? configuration = redis.Enabled ? RedisAccess.Build(redis) : null;
        builder.Services.AddSingleton<IViewSaltStore>(_ => new RedisViewSaltStore(configuration));
    }
}

// El detalle del cuarto nunca falla por las estadísticas: si el registro de
// la vista falla, se deja una advertencia sin datos del usuario.
public sealed class LoggingRoomViewSink(RoomViewService views, ILogger<LoggingRoomViewSink> logger) : IRoomViewSink
{
    public async Task RecordAsync(Room room, string viewerId, CancellationToken cancellationToken)
    {
        try
        {
            await views.RecordAsync(room, viewerId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning("NV-25: no se registró una vista de cuarto ({Reason}).", exception.GetType().Name);
        }
    }
}
