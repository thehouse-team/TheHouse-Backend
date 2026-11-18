using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

// NV-22: `roommate_profiles` (módulo Roommates). Único `{ userId }`.
public interface IRoommateProfileRepository
{
    // Crea o reemplaza el perfil del usuario; devuelve si lo creó.
    Task<(RoommateProfile Profile, bool Created)> UpsertAsync(RoommateProfile profile, CancellationToken cancellationToken);

    Task<RoommateProfile?> GetByUserAsync(string userId, CancellationToken cancellationToken);

    Task<RoommateProfile?> GetAsync(string id, CancellationToken cancellationToken);

    Task DeleteByUserAsync(string userId, CancellationToken cancellationToken);

    // Perfiles visibles y vigentes de la universidad, sin el del usuario; hasta `limit`.
    Task<IReadOnlyList<RoommateProfile>> ListListedAsync(string universityId, string excludeUserId, DateTimeOffset now, int limit, CancellationToken cancellationToken);
}
