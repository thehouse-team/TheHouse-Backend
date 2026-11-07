using System.Collections.Concurrent;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.TestDoubles;

// NV-15/NV-17: dobles en memoria con la semántica de MongoDB/Redis
// (unicidad {userId, propertyId}, orden de listados, lock con NX y fallas
// de dependencia). Se comparten entre pruebas unitarias e integración.
public sealed class InMemoryFavoriteRepository : IFavoriteRepository
{
    private readonly object gate = new();

    public List<Favorite> Items { get; } = [];

    public bool Unavailable { get; set; }

    public Task<bool> AddAsync(Favorite favorite, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            if (Items.Any(item => item.UserId == favorite.UserId && item.PropertyId == favorite.PropertyId))
            {
                return Task.FromResult(false);
            }

            Items.Add(favorite);
            return Task.FromResult(true);
        }
    }

    public Task RemoveAsync(string userId, string propertyId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            Items.RemoveAll(item => item.UserId == userId && item.PropertyId == propertyId);
        }

        return Task.CompletedTask;
    }

    public Task RemoveManyAsync(string userId, IReadOnlyCollection<string> propertyIds, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            Items.RemoveAll(item => item.UserId == userId && propertyIds.Contains(item.PropertyId));
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string userId, string propertyId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Items.Any(item => item.UserId == userId && item.PropertyId == propertyId));
        }
    }

    public Task<long> CountAsync(string userId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult((long)Items.Count(item => item.UserId == userId));
        }
    }

    public Task<(IReadOnlyList<Favorite> Items, long TotalItems)> ListAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var mine = Ordered(userId).ToList();
            IReadOnlyList<Favorite> slice = mine.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult((slice, (long)mine.Count));
        }
    }

    public Task<IReadOnlyList<string>> ListPropertyIdsAsync(string userId, int limit, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            IReadOnlyList<string> ids = Ordered(userId).Take(limit).Select(item => item.PropertyId).ToList();
            return Task.FromResult(ids);
        }
    }

    private IEnumerable<Favorite> Ordered(string userId) =>
        Items
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.PropertyId, StringComparer.Ordinal);

    private void Guard(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }
    }
}

public sealed class InMemoryVisitRepository : IVisitRepository
{
    private readonly object gate = new();

    private int sequence;

    public Dictionary<string, Visit> Visits { get; } = new(StringComparer.Ordinal);

    public Dictionary<(string UserId, string Key), VisitIdempotencyRecord> Keys { get; } = [];

    public bool Unavailable { get; set; }

    public Task<Visit> CreateAsync(Visit visit, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var id = $"7a7a7a7a7a7a7a7a{++sequence:x8}";
            var created = Copy(visit, id);
            Visits.Add(id, created);
            return Task.FromResult(created);
        }
    }

    public Task<Visit?> GetAsync(string id, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Visits.GetValueOrDefault(id));
        }
    }

    public Task UpdateAsync(Visit visit, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            Visits[visit.Id] = visit;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Visit>> ListActiveAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            IReadOnlyList<Visit> active = Visits.Values.Where(visit => visit.UserId == userId && visit.IsActive(now)).ToList();
            return Task.FromResult(active);
        }
    }

    public Task<(IReadOnlyList<Visit> Items, long TotalItems)> ListAsync(
        string userId,
        VisitStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var mine = Visits.Values
                .Where(visit => visit.UserId == userId && (status is null || visit.Status == status))
                .OrderBy(visit => visit.ScheduledAt)
                .ThenBy(visit => visit.Id, StringComparer.Ordinal)
                .ToList();
            IReadOnlyList<Visit> slice = mine.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult((slice, (long)mine.Count));
        }
    }

    public Task<VisitIdempotencyRecord?> FindIdempotencyAsync(string userId, string key, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Keys.GetValueOrDefault((userId, key)));
        }
    }

    public Task SaveIdempotencyAsync(VisitIdempotencyRecord record, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            Keys.Add((record.UserId, record.Key), record);
        }

        return Task.CompletedTask;
    }

    private static Visit Copy(Visit visit, string id) =>
        new()
        {
            Id = id,
            UserId = visit.UserId,
            PropertyId = visit.PropertyId,
            ScheduledAt = visit.ScheduledAt,
            Status = visit.Status,
            Message = visit.Message,
            CancelReason = visit.CancelReason,
            CreatedAt = visit.CreatedAt,
            UpdatedAt = visit.UpdatedAt,
            CancelledAt = visit.CancelledAt,
            History = visit.History,
        };

    private void Guard(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }
    }
}

// NV-11: perfil y preferencias en memoria (un documento por usuario;
// `Version` se incrementa en cada reemplazo, como el $inc de Mongo).
public sealed class InMemoryProfileRepository : IProfileRepository
{
    private readonly object gate = new();

    public Dictionary<string, UserProfile> Profiles { get; } = new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public Task<UserProfile?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Profiles.GetValueOrDefault(userId));
        }
    }

    public Task SaveNameAsync(string userId, string name, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var current = Profiles.GetValueOrDefault(userId);
            Profiles[userId] = new UserProfile { UserId = userId, Name = name, UniversityId = current?.UniversityId, UpdatedAt = now };
        }

        return Task.CompletedTask;
    }

    public Task SaveUniversityAsync(string userId, string universityId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var current = Profiles.GetValueOrDefault(userId);
            Profiles[userId] = new UserProfile { UserId = userId, Name = current?.Name, UniversityId = universityId, UpdatedAt = now };
        }

        return Task.CompletedTask;
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

public sealed class InMemoryPreferencesRepository : IPreferencesRepository
{
    private readonly object gate = new();

    public Dictionary<string, UserPreferences> Preferences { get; } = new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public Task<UserPreferences?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Preferences.GetValueOrDefault(userId));
        }
    }

    public Task<UserPreferences> ReplaceAsync(UserPreferences preferences, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var version = (Preferences.GetValueOrDefault(preferences.UserId)?.Version ?? 0) + 1;
            var stored = new UserPreferences
            {
                UserId = preferences.UserId,
                BudgetMin = preferences.BudgetMin,
                BudgetMax = preferences.BudgetMax,
                Currency = preferences.Currency,
                MaxDistanceKm = preferences.MaxDistanceKm,
                EssentialServices = preferences.EssentialServices,
                LivingWith = preferences.LivingWith,
                PlaceType = preferences.PlaceType,
                Pets = preferences.Pets,
                Transport = preferences.Transport,
                PropertyTypes = preferences.PropertyTypes,
                QuickSetupCompleted = preferences.QuickSetupCompleted,
                Version = version,
                UpdatedAt = preferences.UpdatedAt,
            };
            Preferences[preferences.UserId] = stored;
            return Task.FromResult(stored);
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

// Lock con semántica `SET NX`: compartir la instancia simula réplicas.
public sealed class InMemoryUserLock : IUserLock
{
    private readonly ConcurrentDictionary<string, byte> held = new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    // Retardo opcional mientras se sostiene el lock, para forzar carreras.
    public TimeSpan HoldDelay { get; set; }

    public async Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }

        if (!held.TryAdd(key, 0))
        {
            return null;
        }

        if (HoldDelay > TimeSpan.Zero)
        {
            await Task.Delay(HoldDelay, cancellationToken);
        }

        return new Handle(() => held.TryRemove(key, out _));
    }

    private sealed class Handle(Action release) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            release();
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
