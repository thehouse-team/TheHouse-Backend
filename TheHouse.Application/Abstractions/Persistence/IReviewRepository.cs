using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

// NV-19: `reviews` (módulo Reviews). Único `{ authorId, roomId }`.
public interface IReviewRepository
{
    // Devuelve null si el autor ya reseñó ese cuarto.
    Task<Review?> CreateAsync(Review review, CancellationToken cancellationToken);

    Task<Review?> GetAsync(string id, CancellationToken cancellationToken);

    Task<Review?> FindAsync(string authorId, string roomId, CancellationToken cancellationToken);

    // Reemplaza solo si nadie la cambió desde `expectedUpdatedAt`.
    Task<bool> ReplaceIfUnchangedAsync(Review review, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken);

    // Publicadas del cuarto; orden createdAt desc, id desc.
    Task<(IReadOnlyList<Review> Items, long TotalItems)> ListPublishedByRoomAsync(string roomId, int page, int pageSize, CancellationToken cancellationToken);

    // Todas las del autor; orden createdAt desc, id desc.
    Task<(IReadOnlyList<Review> Items, long TotalItems)> ListByAuthorAsync(string authorId, int page, int pageSize, CancellationToken cancellationToken);

    // Promedios por criterio de las publicadas del cuarto o del arrendador.
    Task<ReviewAverages> AveragesByRoomAsync(string roomId, CancellationToken cancellationToken);

    Task<ReviewAverages> AveragesByOwnerAsync(string ownerId, CancellationToken cancellationToken);
}
