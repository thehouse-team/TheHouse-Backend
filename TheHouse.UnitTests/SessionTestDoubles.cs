using System.Collections.Concurrent;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.UnitTests;

// NV-10: dobles en memoria con la misma semántica que MongoDB/Redis:
// la rotación es atómica (candado) y las fallas son de dependencia.
internal sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly object gate = new();

    public Dictionary<string, RefreshToken> Tokens { get; } = new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public Task CreateAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            Tokens.Add(token.TokenHash, token);
        }

        return Task.CompletedTask;
    }

    public Task<RefreshToken?> TryRotateAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            if (!Tokens.TryGetValue(tokenHash, out var token) || !token.IsActive(now))
            {
                return Task.FromResult<RefreshToken?>(null);
            }

            var rotated = Copy(token, rotatedAt: now);
            Tokens[tokenHash] = rotated;
            return Task.FromResult<RefreshToken?>(rotated);
        }
    }

    public Task<RefreshToken?> FindAsync(string tokenHash, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Tokens.GetValueOrDefault(tokenHash));
        }
    }

    public Task RevokeSessionAsync(string sessionId, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            foreach (var token in Tokens.Values.Where(t => t.SessionId == sessionId && t.RevokedAt is null).ToList())
            {
                Tokens[token.TokenHash] = Copy(token, revokedAt: now, reason: reason);
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

    private static RefreshToken Copy(
        RefreshToken token,
        DateTimeOffset? rotatedAt = null,
        DateTimeOffset? revokedAt = null,
        string? reason = null) =>
        new()
        {
            TokenHash = token.TokenHash,
            SessionId = token.SessionId,
            UserId = token.UserId,
            CreatedAt = token.CreatedAt,
            ExpiresAt = token.ExpiresAt,
            SessionExpiresAt = token.SessionExpiresAt,
            RotatedAt = rotatedAt ?? token.RotatedAt,
            RevokedAt = revokedAt ?? token.RevokedAt,
            RevokedReason = reason ?? token.RevokedReason,
        };
}

internal sealed class InMemorySessionRevocationStore : ISessionRevocationStore
{
    public ConcurrentDictionary<string, TimeSpan> Revoked { get; } = new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public Task RevokeSessionAsync(string sessionId, TimeSpan ttl, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        Revoked.TryAdd(sessionId, ttl);
        return Task.CompletedTask;
    }

    public Task<bool> IsSessionRevokedAsync(string sessionId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        return Task.FromResult(Revoked.ContainsKey(sessionId));
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

internal sealed class AdjustableClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset value = now;

    public void Advance(TimeSpan delta) => value = value.Add(delta);

    public override DateTimeOffset GetUtcNow() => value;
}
