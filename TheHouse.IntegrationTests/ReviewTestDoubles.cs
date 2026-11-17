using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.TestDoubles;

// NV-19: doble en memoria de `reviews` con la semántica de MongoDB (único por
// autor y cuarto, reemplazo condicional, promedios de las publicadas).
public sealed class InMemoryReviewRepository : IReviewRepository
{
    private readonly object gate = new();

    public Dictionary<string, Review> Reviews { get; } = new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public Task<Review?> CreateAsync(Review review, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            if (Reviews.Values.Any(item => item.AuthorId == review.AuthorId && item.RoomId == review.RoomId))
            {
                return Task.FromResult<Review?>(null);
            }

            Reviews[review.Id] = review;
            return Task.FromResult<Review?>(review);
        }
    }

    public Task<Review?> GetAsync(string id, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Reviews.GetValueOrDefault(id));
        }
    }

    public Task<Review?> FindAsync(string authorId, string roomId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Reviews.Values.FirstOrDefault(item => item.AuthorId == authorId && item.RoomId == roomId));
        }
    }

    public Task<bool> ReplaceIfUnchangedAsync(Review review, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            if (!Reviews.TryGetValue(review.Id, out var current) || current.UpdatedAt != expectedUpdatedAt)
            {
                return Task.FromResult(false);
            }

            Reviews[review.Id] = review;
            return Task.FromResult(true);
        }
    }

    public Task<(IReadOnlyList<Review> Items, long TotalItems)> ListPublishedByRoomAsync(string roomId, int page, int pageSize, CancellationToken cancellationToken) =>
        List(item => item.RoomId == roomId && item.Status == ReviewStatus.Published, page, pageSize, cancellationToken);

    public Task<(IReadOnlyList<Review> Items, long TotalItems)> ListByAuthorAsync(string authorId, int page, int pageSize, CancellationToken cancellationToken) =>
        List(item => item.AuthorId == authorId, page, pageSize, cancellationToken);

    public Task<ReviewAverages> AveragesByRoomAsync(string roomId, CancellationToken cancellationToken) =>
        Averages(item => item.RoomId == roomId && item.Status == ReviewStatus.Published, cancellationToken);

    public Task<ReviewAverages> AveragesByOwnerAsync(string ownerId, CancellationToken cancellationToken) =>
        Averages(item => item.OwnerId == ownerId && item.Status == ReviewStatus.Published, cancellationToken);

    private Task<(IReadOnlyList<Review> Items, long TotalItems)> List(Func<Review, bool> predicate, int page, int pageSize, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var all = Reviews.Values.Where(predicate)
                .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult<(IReadOnlyList<Review>, long)>((all.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), all.Length));
        }
    }

    private Task<ReviewAverages> Averages(Func<Review, bool> predicate, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var items = Reviews.Values.Where(predicate).ToArray();
            double? Of(Func<Review, int> score) => items.Length == 0 ? null : items.Average(score);
            return Task.FromResult(new ReviewAverages(
                items.Length,
                Of(item => item.Landlord.Communication),
                Of(item => item.Landlord.Attention),
                Of(item => item.Landlord.Compliance),
                Of(item => item.Landlord.Respect),
                Of(item => item.Property.Cleanliness),
                Of(item => item.Property.Location),
                Of(item => item.Property.Value),
                Of(item => item.Property.Condition),
                Of(item => item.Property.Services)));
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
