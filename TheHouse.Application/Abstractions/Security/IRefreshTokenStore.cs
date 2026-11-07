using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Security;

// NV-10: persistencia de refresh tokens (solo hashes). Las
// implementaciones fallan cerradas con DependencyUnavailableException.
public interface IRefreshTokenStore
{
    Task CreateAsync(
        RefreshToken token,
        CancellationToken cancellationToken);

    // Marca el token como rotado de forma atómica solo si sigue activo
    // (no rotado, no revocado y sin vencer). Devuelve el token rotado o
    // null si no se pudo rotar; de dos llamadas simultáneas con el mismo
    // hash, como máximo una obtiene un resultado no nulo.
    Task<RefreshToken?> TryRotateAsync(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<RefreshToken?> FindAsync(
        string tokenHash,
        CancellationToken cancellationToken);

    // NV-21: sesiones con algún token sin revocar y aún vigentes del usuario.
    Task<IReadOnlyList<string>> ListActiveSessionIdsAsync(
        string userId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    // Revoca todos los tokens aún no revocados de la sesión.
    Task RevokeSessionAsync(
        string sessionId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
