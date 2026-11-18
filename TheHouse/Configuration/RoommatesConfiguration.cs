using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.Configuration;

// NV-22: perfil de compañero. Requiere AddProfile, AddMessaging y
// AddModeration registrados antes.
public static class RoommatesConfiguration
{
    public static void AddRoommates(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<RoommateService>();

        builder.Services.AddSingleton<IRoommateProfileRepository>(provider =>
        {
            var database = provider.GetService<IMongoDatabase>();
            return database is null
                ? new UnavailableRoommateProfileRepository()
                : new MongoRoommateProfileRepository(database);
        });
    }
}
