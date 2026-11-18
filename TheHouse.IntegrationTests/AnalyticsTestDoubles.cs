using System.Security.Cryptography;
using TheHouse.Application.Abstractions.Analytics;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.TestDoubles;

// NV-25: `room_views` en memoria con la misma llave única que MongoDB.
public sealed class InMemoryRoomViewRepository : IRoomViewRepository
{
    private readonly object gate = new();

    public List<RoomView> Items { get; } = [];

    public bool Unavailable { get; set; }

    public Task<bool> AddAsync(RoomView view, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            if (Items.Any(item => item.RoomId == view.RoomId && item.BuildingId == view.BuildingId && item.Day == view.Day && item.ActorHash == view.ActorHash))
            {
                return Task.FromResult(false);
            }

            Items.Add(view);
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<RoomViewCount>> CountAsync(string ownerId, string fromDay, string toDay, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<RoomViewCount>>(Items
                .Where(item => item.OwnerId == ownerId && string.CompareOrdinal(item.Day, fromDay) >= 0 && string.CompareOrdinal(item.Day, toDay) <= 0)
                .GroupBy(item => (item.RoomId, item.BuildingId, item.Day))
                .Select(group => new RoomViewCount(group.Key.RoomId, group.Key.BuildingId, group.Key.Day, group.Count()))
                .ToList());
        }
    }

    // Agrega vistas de prueba sin pasar por el detalle (p. ej. días pasados).
    public void Seed(Room room, string day, int count)
    {
        lock (gate)
        {
            for (var index = 0; index < count; index++)
            {
                Items.Add(new RoomView
                {
                    RoomId = room.Id,
                    BuildingId = room.BuildingId,
                    OwnerId = room.OwnerId,
                    Day = day,
                    ActorHash = Guid.NewGuid().ToString("N"),
                    CreatedAt = DateTimeOffset.UtcNow,
                });
            }
        }
    }

    private void Guard(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }
    }
}

// NV-25: sal del día en memoria (como Redis `SET NX`).
public sealed class InMemoryViewSaltStore : IViewSaltStore
{
    private readonly Dictionary<string, byte[]> salts = new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public IReadOnlyCollection<string> Days
    {
        get
        {
            lock (salts)
            {
                return salts.Keys.ToArray();
            }
        }
    }

    public Task<byte[]> GetDailySaltAsync(string day, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }

        lock (salts)
        {
            if (!salts.TryGetValue(day, out var salt))
            {
                salt = RandomNumberGenerator.GetBytes(32);
                salts[day] = salt;
            }

            return Task.FromResult(salt);
        }
    }
}
