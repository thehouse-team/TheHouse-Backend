using MongoDB.Driver;
using StackExchange.Redis;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Configuration;
using TheHouse.Infrastructure.Persistence.Mongo;
using TheHouse.Infrastructure.Security;

namespace TheHouse.Configuration;

// NV-15/NV-17: favoritos y visitas. Requiere AddRegistration (IMongoDatabase,
// TimeProvider) y AddLogin (IRateLimitStore) registrados antes.
public static class FavoriteVisitConfiguration
{
    public static void AddFavoritesAndVisits(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(new UserRateLimitOptions());
        builder.Services.AddSingleton<FavoriteService>();
        builder.Services.AddSingleton<VisitService>();
        builder.Services.AddSingleton<LandlordVisitService>();

        builder.Services.AddSingleton<IFavoriteRepository>(provider =>
        {
            var database = provider.GetService<IMongoDatabase>();
            return database is null
                ? new UnavailableFavoriteRepository()
                : new MongoFavoriteRepository(database);
        });

        builder.Services.AddSingleton<IVisitRepository>(provider =>
        {
            var database = provider.GetService<IMongoDatabase>();
            return database is null
                ? new UnavailableVisitRepository()
                : new MongoVisitRepository(database);
        });

        var redis =
            builder.Configuration.GetSection("Redis").Get<RedisOptions>() ??
            new RedisOptions();

        // La validez de la sección Redis ya la comprueban AddLogin/AddLogout.
        ConfigurationOptions? configuration = redis.Enabled ? RedisAccess.Build(redis) : null;
        builder.Services.AddSingleton<IUserLock>(_ => new RedisUserLock(configuration));
    }
}
