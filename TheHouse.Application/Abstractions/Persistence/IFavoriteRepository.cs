using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

// NV-15: favoritos por usuario. Agregar y quitar son idempotentes.
public interface IFavoriteRepository
{
    // Devuelve false si ya existía (índice único {userId, propertyId}).
    Task<bool> AddAsync(Favorite favorite, CancellationToken cancellationToken);

    Task RemoveAsync(string userId, string propertyId, CancellationToken cancellationToken);

    Task RemoveManyAsync(string userId, IReadOnlyCollection<string> propertyIds, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string userId, string propertyId, CancellationToken cancellationToken);

    Task<long> CountAsync(string userId, CancellationToken cancellationToken);

    // Orden: createdAt desc, propertyId desc.
    Task<(IReadOnlyList<Favorite> Items, long TotalItems)> ListAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListPropertyIdsAsync(
        string userId,
        int limit,
        CancellationToken cancellationToken);
}
