using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.Configuration;

// NV-12: arrendador, edificios y cuartos. Requiere AddRegistration
// (IMongoDatabase, TimeProvider), AddUniversities y AddFavoritesAndVisits
// (UserRateLimitOptions).
public static class LandlordConfiguration
{
    public static void AddLandlord(this WebApplicationBuilder builder)
    {
        var termsVersion = builder.Configuration["LandlordTerms:Version"];
        builder.Services.AddSingleton(
            string.IsNullOrWhiteSpace(termsVersion)
                ? new LandlordOptions()
                : new LandlordOptions { TermsVersion = termsVersion.Trim() });

        builder.Services.AddSingleton<LandlordService>();
        builder.Services.AddSingleton<BuildingService>();
        builder.Services.AddSingleton<RoomService>();

        builder.Services.AddSingleton<ILandlordProfileRepository>(provider =>
            provider.GetService<IMongoDatabase>() is { } database
                ? new MongoLandlordProfileRepository(database)
                : new UnavailableLandlordProfileRepository());

        builder.Services.AddSingleton<IBuildingRepository>(provider =>
            provider.GetService<IMongoDatabase>() is { } database
                ? new MongoBuildingRepository(database)
                : new UnavailableBuildingRepository());

        builder.Services.AddSingleton<IRoomRepository>(provider =>
            provider.GetService<IMongoDatabase>() is { } database
                ? new MongoRoomRepository(database)
                : new UnavailableRoomRepository());
    }
}
