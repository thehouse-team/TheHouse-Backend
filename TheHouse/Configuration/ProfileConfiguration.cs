using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.Configuration;

// NV-11: perfil, universidad, preferencias y búsqueda de universidades.
// Requiere AddRegistration (IMongoDatabase, TimeProvider), AddUniversities
// (IUniversityRepository) y AddFavoritesAndVisits (UserRateLimitOptions).
public static class ProfileConfiguration
{
    public static void AddProfile(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ProfileService>();
        builder.Services.AddSingleton<PreferencesService>();
        builder.Services.AddSingleton<UniversitySearchService>();

        builder.Services.AddSingleton<IProfileRepository>(provider =>
        {
            var database = provider.GetService<IMongoDatabase>();
            return database is null
                ? new UnavailableProfileRepository()
                : new MongoProfileRepository(database);
        });

        builder.Services.AddSingleton<IPreferencesRepository>(provider =>
        {
            var database = provider.GetService<IMongoDatabase>();
            return database is null
                ? new UnavailablePreferencesRepository()
                : new MongoPreferencesRepository(database);
        });
    }
}
