using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Search;
using TheHouse.Domain.Entities;

namespace TheHouse.Application.UseCases;

// NV-15/NV-17 1.2.0: un cuarto con su edificio.
public sealed record CatalogRoom(Room Room, Building Building)
{
    public bool IsPublished => Room.Status == RoomStatus.Available && Building.Status == BuildingStatus.Verified;

    // Propiedad de NV-07 de la que salió el cuarto `demo` (transición).
    public string? LegacyPropertyId => Building.DemoSourceId;

    public string Title => $"{Building.Name} · Cuarto {Room.Number}";

    public ApproxLocation ApproxLocation
    {
        get
        {
            var (lat, lng) = ApproximateLocation.Cell(Building.Latitude, Building.Longitude);
            return new ApproxLocation(lat, lng);
        }
    }
}

// Resuelve ids de cuarto y, durante la transición, ids de las 12 propiedades
// de NV-07 (mapeo `demoSourceId` del seed de demostración de NV-14).
public sealed class RoomCatalog(IRoomRepository rooms, IBuildingRepository buildings)
{
    public async Task<CatalogRoom?> FindAsync(string id, CancellationToken cancellationToken)
    {
        if (await rooms.GetAsync(id, cancellationToken) is { } room)
        {
            var building = await buildings.GetAsync(room.BuildingId, cancellationToken);
            return building is null ? null : new CatalogRoom(room, building);
        }

        return await FromLegacyAsync(id, cancellationToken);
    }

    public async Task<CatalogRoom?> FromLegacyAsync(string propertyId, CancellationToken cancellationToken)
    {
        var demo = (await buildings.ListDemoAsync(cancellationToken)).FirstOrDefault(building => building.DemoSourceId == propertyId);
        if (demo is null)
        {
            return null;
        }

        var room = (await rooms.ListAllByBuildingAsync(demo.Id, cancellationToken)).OrderBy(item => item.Number).FirstOrDefault();
        return room is null ? null : new CatalogRoom(room, demo);
    }

    // Mapeo propiedad → cuarto de todos los cuartos `demo`.
    public async Task<IReadOnlyDictionary<string, string>> LegacyMapAsync(CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var building in await buildings.ListDemoAsync(cancellationToken))
        {
            var room = (await rooms.ListAllByBuildingAsync(building.Id, cancellationToken)).OrderBy(item => item.Number).FirstOrDefault();
            if (building.DemoSourceId is { } source && room is not null)
            {
                map[source] = room.Id;
            }
        }

        return map;
    }

    public async Task<IReadOnlyDictionary<string, CatalogRoom>> LoadAsync(IEnumerable<string> roomIds, CancellationToken cancellationToken)
    {
        var ids = roomIds.Where(id => id.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, CatalogRoom>(StringComparer.Ordinal);
        }

        var loadedRooms = await rooms.GetManyAsync(ids, cancellationToken);
        var loadedBuildings = (await buildings.GetManyAsync(loadedRooms.Select(room => room.BuildingId).Distinct(StringComparer.Ordinal).ToArray(), cancellationToken))
            .ToDictionary(building => building.Id, StringComparer.Ordinal);

        return loadedRooms
            .Where(room => loadedBuildings.ContainsKey(room.BuildingId))
            .ToDictionary(room => room.Id, room => new CatalogRoom(room, loadedBuildings[room.BuildingId]), StringComparer.Ordinal);
    }
}
