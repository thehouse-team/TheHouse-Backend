using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-10: sin MongoDB habilitado, las sesiones fallan cerradas (503).
public sealed class UnavailableRefreshTokenStore : IRefreshTokenStore
{
    public Task CreateAsync(RefreshToken token, CancellationToken cancellationToken) =>
        Fail<object?>(cancellationToken);

    public Task<RefreshToken?> TryRotateAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        Fail<RefreshToken?>(cancellationToken);

    public Task<RefreshToken?> FindAsync(string tokenHash, CancellationToken cancellationToken) =>
        Fail<RefreshToken?>(cancellationToken);

    public Task<IReadOnlyList<string>> ListActiveSessionIdsAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException();

    public Task RevokeSessionAsync(string sessionId, string reason, DateTimeOffset now, CancellationToken cancellationToken) =>
        Fail<object?>(cancellationToken);

    private static Task<T> Fail<T>(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new DependencyUnavailableException();
    }
}
