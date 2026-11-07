using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

// NV-15 1.2.0: favoritos por cuarto. `legacyPropertyId` alcanza también los
// favoritos de 1.1.0 que aún no se migran.
public interface IFavoriteRepository
{
    // false si ya existía (índice único {userId, roomId}).
    Task<bool> AddAsync(Favorite favorite, CancellationToken cancellationToken);

    Task RemoveAsync(string userId, string roomId, string? legacyPropertyId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string userId, string roomId, string? legacyPropertyId, CancellationToken cancellationToken);

    Task<long> CountAsync(string userId, CancellationToken cancellationToken);

    // Todos los favoritos del usuario (máximo 200), más recientes primero.
    Task<IReadOnlyList<Favorite>> ListAllAsync(string userId, CancellationToken cancellationToken);

    // Migración: asigna `roomId` a los favoritos que solo tienen `propertyId`.
    Task<MigrationCounts> MigrateLegacyAsync(IReadOnlyDictionary<string, string> roomByProperty, CancellationToken cancellationToken);
}

public sealed record MigrationCounts(int Migrated, int DuplicatesRemoved, int Unmapped);
