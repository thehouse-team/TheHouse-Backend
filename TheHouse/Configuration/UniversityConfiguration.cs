using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.Configuration;

public static class UniversityConfiguration
{
    public static void AddUniversities(
        this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(
            new UniversityRateLimitOptions());

        var seedOptions =
            builder.Configuration
                .GetSection("UniversitySeed")
                .Get<UniversitySeedOptions>()
            ?? new UniversitySeedOptions();

        builder.Services.AddSingleton(seedOptions);

        builder.Services.AddSingleton<UniversityQueryService>();
        builder.Services.AddSingleton<UniversitySeedService>();

        builder.Services.AddSingleton<IUniversityRepository>(
            provider =>
            {
                var database =
                    provider.GetService<IMongoDatabase>();

                return database is null
                    ? new UnavailableUniversityRepository()
                    : new MongoUniversityRepository(database);
            });
    }
}
