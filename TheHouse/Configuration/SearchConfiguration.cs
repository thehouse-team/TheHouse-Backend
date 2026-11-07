using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.Configuration;

// NV-14: búsqueda, colonias y seeds autorizados. Requiere AddUniversities y
// AddLandlord registrados antes.
public static class SearchConfiguration
{
    public const string SeedPlaces = "--seed-places";

    public const string SeedDemoRooms = "--seed-demo-rooms";

    public static void AddSearch(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<SearchService>();
        builder.Services.AddSingleton<PlaceSeedService>();
        builder.Services.AddSingleton<DemoRoomSeedService>();
        builder.Services.AddSingleton(builder.Configuration.GetSection("PlaceSeed").Get<PlaceSeedOptions>() ?? new PlaceSeedOptions());
        builder.Services.AddSingleton(builder.Configuration.GetSection("DemoRoomSeed").Get<DemoRoomSeedOptions>() ?? new DemoRoomSeedOptions());

        builder.Services.AddSingleton<IPlaceRepository>(provider =>
            provider.GetService<IMongoDatabase>() is { } database
                ? new MongoPlaceRepository(database)
                : new UnavailablePlaceRepository());
    }

    // Mismas reglas que el seed de NV-07/NV-08 (M-BE-043): habilitado por
    // configuración y, en Production, solo con `--allow-production-seed`.
    public static string? Reject(IReadOnlyCollection<string> args, bool placesEnabled, bool demoEnabled, bool isProduction)
    {
        if (args.Contains(SeedPlaces, StringComparer.Ordinal) && !placesEnabled)
        {
            return "Place seed is disabled. Set PlaceSeed__Enabled=true only in an authorized environment.";
        }

        if (args.Contains(SeedDemoRooms, StringComparer.Ordinal) && !demoEnabled)
        {
            return "Demo room seed is disabled. Set DemoRoomSeed__Enabled=true only in an authorized environment.";
        }

        return isProduction && !args.Contains(SeedCommandPolicy.AllowProductionSeed, StringComparer.Ordinal)
            ? $"Seed commands are not allowed in Production without {SeedCommandPolicy.AllowProductionSeed}."
            : null;
    }
}
