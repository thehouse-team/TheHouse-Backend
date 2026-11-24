using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;

namespace TheHouse.TestDoubles;

// NV-23 1.2.0: colecciones de Smart Match en memoria con las mismas llaves
// únicas que MongoDB (`{userId, roomId}`) y contadores diarios con suma.
public sealed class InMemorySmartMatchRepository : ISmartMatchRepository
{
    private readonly object gate = new();

    public List<(string UserId, string RoomId, bool Useful, string ModelVersion, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)> Feedback { get; } = [];

    public Dictionary<(string UserId, string RoomId), DateTimeOffset> Dismissals { get; } = [];

    public Dictionary<(string Day, string ModelVersion), SmartMatchDailyStats> Stats { get; } = [];

    public bool Unavailable { get; set; }

    // Simula que el `$inc` de las métricas falla sin afectar lo demás.
    public bool StatsUnavailable { get; set; }

    public Task SaveFeedbackAsync(string userId, string roomId, bool useful, string modelVersion, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var index = Feedback.FindIndex(item => item.UserId == userId && item.RoomId == roomId);
            var createdAt = index >= 0 ? Feedback[index].CreatedAt : now;
            var entry = (userId, roomId, useful, modelVersion, createdAt, now);
            if (index >= 0)
            {
                Feedback[index] = entry;
            }
            else
            {
                Feedback.Add(entry);
            }
        }

        return Task.CompletedTask;
    }

    public Task AddDismissalAsync(string userId, string roomId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            Dismissals[(userId, roomId)] = now;
        }

        return Task.CompletedTask;
    }

    public Task RemoveDismissalAsync(string userId, string roomId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            Dismissals.Remove((userId, roomId));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlySet<string>> ListDismissedRoomIdsAsync(string userId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult<IReadOnlySet<string>>(Dismissals
                .Where(item => item.Key.UserId == userId && item.Value > since)
                .Select(item => item.Key.RoomId)
                .ToHashSet(StringComparer.Ordinal));
        }
    }

    public Task<long> CountActiveDismissalsAsync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult((long)Dismissals.Count(item => item.Value > since));
        }
    }

    public Task<IReadOnlyList<SmartMatchFeedbackCount>> CountFeedbackAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<SmartMatchFeedbackCount>>([.. Feedback
                .Where(item => item.UpdatedAt >= from && item.UpdatedAt < to)
                .GroupBy(item => (item.ModelVersion, item.Useful))
                .Select(group => new SmartMatchFeedbackCount(group.Key.ModelVersion, group.Key.Useful, group.LongCount()))]);
        }
    }

    public Task IncrementStatsAsync(SmartMatchDailyStats delta, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        if (StatsUnavailable)
        {
            throw new DependencyUnavailableException();
        }

        lock (gate)
        {
            var key = (delta.Day, delta.ModelVersion);
            Stats[key] = Stats.TryGetValue(key, out var current)
                ? current with
                {
                    ScoreRequests = current.ScoreRequests + delta.ScoreRequests,
                    RecommendationRequests = current.RecommendationRequests + delta.RecommendationRequests,
                    GuestRequests = current.GuestRequests + delta.GuestRequests,
                    RoomsScored = current.RoomsScored + delta.RoomsScored,
                    EligibleRooms = current.EligibleRooms + delta.EligibleRooms,
                    ScoreSum = current.ScoreSum + delta.ScoreSum,
                    Bucket0To39 = current.Bucket0To39 + delta.Bucket0To39,
                    Bucket40To59 = current.Bucket40To59 + delta.Bucket40To59,
                    Bucket60To79 = current.Bucket60To79 + delta.Bucket60To79,
                    Bucket80To100 = current.Bucket80To100 + delta.Bucket80To100,
                }
                : delta;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SmartMatchDailyStats>> ListStatsAsync(string fromDay, string toDay, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<SmartMatchDailyStats>>([.. Stats.Values
                .Where(item => string.CompareOrdinal(item.Day, fromDay) >= 0 && string.CompareOrdinal(item.Day, toDay) <= 0)]);
        }
    }

    public Task DeleteUserAsync(string userId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            Feedback.RemoveAll(item => item.UserId == userId);
            foreach (var key in Dismissals.Keys.Where(key => key.UserId == userId).ToArray())
            {
                Dismissals.Remove(key);
            }
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

// NV-24 1.2.1: caché de explicaciones en memoria (misma llave y vigencia de 24 h).
public sealed class InMemorySmartMatchExplanationCache : TheHouse.Application.Abstractions.SmartMatch.ISmartMatchExplanationCache
{
    private readonly object gate = new();

    public Dictionary<string, (TheHouse.Application.Abstractions.SmartMatch.CachedExplanation Value, DateTimeOffset CreatedAt)> Items { get; } = [];

    public bool Unavailable { get; set; }

    public Task<TheHouse.Application.Abstractions.SmartMatch.CachedExplanation?> GetAsync(string key, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Items.TryGetValue(key, out var item) && item.CreatedAt > now - SmartMatchExplanationRules.CacheDuration ? item.Value : null);
        }
    }

    public Task SetAsync(string key, TheHouse.Application.Abstractions.SmartMatch.CachedExplanation explanation, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            Items[key] = (explanation, now);
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

// TheHouse-GenAI falso: devuelve el resultado configurado y guarda los hechos
// enviados para comprobar la minimización (sin red).
public sealed class FakeGenAiExplanationClient : TheHouse.Application.Abstractions.SmartMatch.IGenAiExplanationClient
{
    public bool Enabled { get; set; }

    public List<(GenAiFacts Facts, string? RequestId)> Calls { get; } = [];

    public Func<GenAiFacts, TheHouse.Application.Abstractions.SmartMatch.GenAiExplanationResult> Respond { get; set; } =
        facts => new(TheHouse.Application.Abstractions.SmartMatch.GenAiOutcome.Generated, $"Te queda a {facts.DistanceKm} km de la {facts.University} y cuesta ${facts.Price} MXN al mes.", "explain_match_v3");

    public Task<TheHouse.Application.Abstractions.SmartMatch.GenAiExplanationResult> ExplainAsync(GenAiFacts facts, string? requestId, CancellationToken cancellationToken)
    {
        lock (Calls)
        {
            Calls.Add((facts, requestId));
        }

        return Task.FromResult(Respond(facts));
    }
}
