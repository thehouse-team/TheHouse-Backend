using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.Configuration;

public static class PropertyConfiguration
{
    public static void AddProperties(
        this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(
            new PropertyRateLimitOptions());

        builder.Services.AddSingleton<PropertyQueryService>();

        builder.Services.AddSingleton<IPropertyRepository>(
            provider =>
            {
                var database =
                    provider.GetService<IMongoDatabase>();

                return database is null
                    ? new UnavailablePropertyRepository()
                    : new MongoPropertyRepository(database);
            });
    }
}
