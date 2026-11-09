using Microsoft.Extensions.DependencyInjection;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;

namespace TheHouse.IntegrationTests;

// NV-15/NV-17 1.2.0: fábrica con los 12 cuartos de demostración de NV-14 y
// su mapeo a las propiedades de NV-07 (`Building.DemoSourceId`).
internal static class RoomTestFixtures
{
    public const string MissingId = "ffffffffffffffffffffffff";

    public static async Task<UserResourceFactory> SeededAsync(UserRateLimitOptions? limits = null)
    {
        var factory = new UserResourceFactory(limits);
        await factory.Services.GetRequiredService<DemoRoomSeedService>().SeedAsync(CancellationToken.None);
        return factory;
    }

    // Cuartos ordenados por la propiedad de la que provienen (…001, …002, …).
    public static IReadOnlyList<string> RoomIds(UserResourceFactory factory) =>
        factory.Rooms.Rooms.Values
            .OrderBy(room => LegacyOf(factory, room.Id), StringComparer.Ordinal)
            .Select(room => room.Id)
            .ToArray();

    public static string LegacyOf(UserResourceFactory factory, string roomId) =>
        factory.Buildings.Buildings[factory.Rooms.Rooms[roomId].BuildingId].DemoSourceId!;
}
