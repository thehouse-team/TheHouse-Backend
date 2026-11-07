using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.TestDoubles;

// NV-12: arrendador, edificios y cuartos en memoria con la semántica de
// Mongo: perfil único, reemplazo condicionado a `Version` y número de
// cuarto único por edificio. Se comparten con las pruebas unitarias.
public sealed class InMemoryLandlordProfileRepository : ILandlordProfileRepository
{
    private readonly object gate = new();

    public Dictionary<string, LandlordProfile> Profiles { get; } = new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public Task<LandlordProfile?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Profiles.GetValueOrDefault(userId));
        }
    }

    public Task<bool> CreateAsync(LandlordProfile profile, CancellationToken cancellationToken)
    {
        Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Profiles.TryAdd(profile.UserId, profile));
        }
    }

    internal static void Guard(bool unavailable, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (unavailable)
        {
            throw new DependencyUnavailableException();
        }
    }
}

public sealed class InMemoryBuildingRepository : IBuildingRepository
{
    private readonly object gate = new();

    private int sequence;

    public Dictionary<string, Building> Buildings { get; } = new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public Task<Building> CreateAsync(Building building, CancellationToken cancellationToken)
    {
        InMemoryLandlordProfileRepository.Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            var created = building with { Id = $"b0b0b0b0b0b0b0b0{++sequence:x8}" };
            Buildings.Add(created.Id, created);
            return Task.FromResult(created);
        }
    }

    public Task<Building?> GetAsync(string id, CancellationToken cancellationToken)
    {
        InMemoryLandlordProfileRepository.Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Buildings.GetValueOrDefault(id));
        }
    }

    public Task<(IReadOnlyList<Building> Items, long TotalItems)> ListByOwnerAsync(string ownerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        InMemoryLandlordProfileRepository.Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            var mine = Buildings.Values
                .Where(building => building.OwnerId == ownerId)
                .OrderByDescending(building => building.CreatedAt)
                .ThenByDescending(building => building.Id, StringComparer.Ordinal)
                .ToList();
            IReadOnlyList<Building> slice = mine.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult((slice, (long)mine.Count));
        }
    }

    public Task<IReadOnlyList<Building>> ListAllByOwnerAsync(string ownerId, CancellationToken cancellationToken)
    {
        InMemoryLandlordProfileRepository.Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<Building>>(Buildings.Values.Where(building => building.OwnerId == ownerId).ToList());
        }
    }

    public Task<(IReadOnlyList<Building> Items, long TotalItems)> ListByStatusAsync(BuildingStatus status, int page, int pageSize, CancellationToken cancellationToken)
    {
        InMemoryLandlordProfileRepository.Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            var matching = Buildings.Values
                .Where(building => building.Status == status)
                .OrderBy(building => building.UpdatedAt)
                .ThenBy(building => building.Id, StringComparer.Ordinal)
                .ToList();
            IReadOnlyList<Building> slice = matching.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult((slice, (long)matching.Count));
        }
    }

    public Task<long> CountByOwnerAsync(string ownerId, CancellationToken cancellationToken)
    {
        InMemoryLandlordProfileRepository.Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            return Task.FromResult((long)Buildings.Values.Count(building => building.OwnerId == ownerId));
        }
    }

    public Task<bool> ReplaceAsync(Building building, long expectedVersion, CancellationToken cancellationToken)
    {
        InMemoryLandlordProfileRepository.Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            if (!Buildings.TryGetValue(building.Id, out var current) || current.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            Buildings[building.Id] = building;
            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteAsync(string id, long expectedVersion, CancellationToken cancellationToken)
    {
        InMemoryLandlordProfileRepository.Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Buildings.TryGetValue(id, out var current) && current.Version == expectedVersion && Buildings.Remove(id));
        }
    }
}

public sealed class InMemoryRoomRepository : IRoomRepository
{
    private readonly object gate = new();

    private int sequence;

    public Dictionary<string, Room> Rooms { get; } = new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public Task<IReadOnlyList<Room>> CreateManyAsync(IReadOnlyList<Room> rooms, CancellationToken cancellationToken)
    {
        InMemoryLandlordProfileRepository.Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            if (rooms.Any(room => Rooms.Values.Any(existing => existing.BuildingId == room.BuildingId && existing.Number == room.Number)) ||
                rooms.GroupBy(room => (room.BuildingId, room.Number)).Any(group => group.Count() > 1))
            {
                throw new ConflictException("room_number_taken");
            }

            var created = rooms.Select(room => room with { Id = $"c0c0c0c0c0c0c0c0{++sequence:x8}" }).ToList();
            foreach (var room in created)
            {
                Rooms.Add(room.Id, room);
            }

            return Task.FromResult<IReadOnlyList<Room>>(created);
        }
    }

    public Task<Room?> GetAsync(string id, CancellationToken cancellationToken)
    {
        InMemoryLandlordProfileRepository.Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Rooms.GetValueOrDefault(id));
        }
    }

    public Task<(IReadOnlyList<Room> Items, long TotalItems)> ListByBuildingAsync(string buildingId, int page, int pageSize, CancellationToken cancellationToken)
    {
        InMemoryLandlordProfileRepository.Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            var mine = Rooms.Values.Where(room => room.BuildingId == buildingId).OrderBy(room => room.Floor).ThenBy(room => room.Number).ToList();
            IReadOnlyList<Room> slice = mine.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult((slice, (long)mine.Count));
        }
    }

    public Task<IReadOnlyList<Room>> ListAllByBuildingAsync(string buildingId, CancellationToken cancellationToken)
    {
        InMemoryLandlordProfileRepository.Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<Room>>(Rooms.Values.Where(room => room.BuildingId == buildingId).ToList());
        }
    }

    public Task<IReadOnlyList<Room>> ListAllByOwnerAsync(string ownerId, CancellationToken cancellationToken)
    {
        InMemoryLandlordProfileRepository.Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<Room>>(Rooms.Values.Where(room => room.OwnerId == ownerId).ToList());
        }
    }

    public Task<bool> ReplaceAsync(Room room, long expectedVersion, CancellationToken cancellationToken)
    {
        InMemoryLandlordProfileRepository.Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            if (!Rooms.TryGetValue(room.Id, out var current) || current.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            if (Rooms.Values.Any(other => other.Id != room.Id && other.BuildingId == room.BuildingId && other.Number == room.Number))
            {
                throw new ConflictException("room_number_taken");
            }

            Rooms[room.Id] = room;
            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteAsync(string id, long expectedVersion, CancellationToken cancellationToken)
    {
        InMemoryLandlordProfileRepository.Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Rooms.TryGetValue(id, out var current) && current.Version == expectedVersion && Rooms.Remove(id));
        }
    }

    public Task DeleteByBuildingAsync(string buildingId, CancellationToken cancellationToken)
    {
        InMemoryLandlordProfileRepository.Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            foreach (var id in Rooms.Values.Where(room => room.BuildingId == buildingId).Select(room => room.Id).ToList())
            {
                Rooms.Remove(id);
            }
        }

        return Task.CompletedTask;
    }

    public Task SetStatusByBuildingAsync(string buildingId, RoomStatus from, RoomStatus to, DateTimeOffset now, CancellationToken cancellationToken)
    {
        InMemoryLandlordProfileRepository.Guard(Unavailable, cancellationToken);
        lock (gate)
        {
            foreach (var room in Rooms.Values.Where(room => room.BuildingId == buildingId && room.Status == from).ToList())
            {
                Rooms[room.Id] = room with { Status = to, UpdatedAt = now, Version = room.Version + 1 };
            }
        }

        return Task.CompletedTask;
    }
}
