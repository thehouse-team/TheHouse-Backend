using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Claims;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;

namespace TheHouse.UnitTests;

// NV-10 T06, T07: logout integrado con la sesión y rechazo de access
// tokens de sesiones revocadas.
public sealed class LogoutSessionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    private const string Jti = "0123456789abcdef0123456789abcdef";

    private readonly InMemoryRefreshTokenStore refreshTokens = new();
    private readonly InMemorySessionRevocationStore sessionRevocation = new();
    private readonly JtiStore jtiStore = new();

    private LogoutService Logout(ClaimsPrincipal principal, SessionService? sessions) =>
        new(new PrincipalJwt(principal), jtiStore, new AdjustableClock(Now), sessions);

    private SessionService Sessions() =>
        new(refreshTokens, sessionRevocation, new NoUsers(), new PrincipalJwt(new ClaimsPrincipal()), new RefreshTokenOptions(), new AdjustableClock(Now));

    [Fact]
    public async Task Logout_with_sid_revokes_jti_and_session()
    {
        var sessionId = SessionTokens.NewSessionId();
        refreshTokens.Tokens.Add("hash", new RefreshToken
        {
            TokenHash = "hash",
            SessionId = sessionId,
            UserId = "u1",
            CreatedAt = Now,
            ExpiresAt = Now.AddDays(30),
            SessionExpiresAt = Now.AddDays(90),
        });

        await Logout(Principal(sessionId), Sessions()).RevokeAsync("token", CancellationToken.None);

        Assert.True(jtiStore.Revoked.ContainsKey(Jti));
        Assert.True(sessionRevocation.Revoked.ContainsKey(sessionId));
        Assert.Equal(SessionService.ReasonLogout, refreshTokens.Tokens["hash"].RevokedReason);
    }

    [Fact]
    public async Task Logout_without_sid_behaves_as_before()
    {
        await Logout(Principal(sessionId: null), Sessions()).RevokeAsync("token", CancellationToken.None);

        Assert.True(jtiStore.Revoked.ContainsKey(Jti));
        Assert.Empty(sessionRevocation.Revoked);
    }

    [Fact]
    public async Task Access_token_of_revoked_session_is_rejected()
    {
        var sessionId = SessionTokens.NewSessionId();
        sessionRevocation.Revoked[sessionId] = TimeSpan.FromSeconds(930);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            Logout(Principal(sessionId), Sessions()).ValidateActiveAsync("token", CancellationToken.None));
    }

    [Fact]
    public async Task Active_session_returns_validated_principal()
    {
        var sessionId = SessionTokens.NewSessionId();

        var principal = await Logout(Principal(sessionId), Sessions())
            .ValidateActiveAsync("token", CancellationToken.None);

        Assert.Equal(sessionId, principal.FindFirst(SessionTokens.SessionIdClaim)?.Value);
    }

    [Fact]
    public async Task Malformed_sid_is_rejected()
    {
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            Logout(Principal("not-a-session"), Sessions()).ValidateActiveAsync("token", CancellationToken.None));
    }

    [Fact]
    public async Task Session_store_failure_is_fail_closed()
    {
        sessionRevocation.Unavailable = true;

        await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            Logout(Principal(SessionTokens.NewSessionId()), Sessions()).ValidateActiveAsync("token", CancellationToken.None));
    }

    private static ClaimsPrincipal Principal(string? sessionId)
    {
        var claims = new List<Claim>
        {
            new("sub", "665f1c2a9b1e4a0012345678"),
            new(RevocationKeys.JtiClaim, Jti),
            new(RevocationKeys.ExpClaim, Now.AddMinutes(15).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
        };

        if (sessionId is not null)
        {
            claims.Add(new Claim(SessionTokens.SessionIdClaim, sessionId));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private sealed class PrincipalJwt(ClaimsPrincipal principal) : IJwtTokenService
    {
        public AccessToken CreateAccessToken(User user, string sessionId) =>
            throw new NotSupportedException();

        public ClaimsPrincipal ValidateAccessToken(string token) => principal;
    }

    private sealed class JtiStore : ITokenRevocationStore
    {
        public ConcurrentDictionary<string, TimeSpan> Revoked { get; } = new(StringComparer.Ordinal);

        public Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken)
        {
            Revoked.TryAdd(jti, ttl);
            return Task.CompletedTask;
        }

        public Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken) =>
            Task.FromResult(Revoked.ContainsKey(jti));
    }

    private sealed class NoUsers : IUserRepository
    {
        public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(null);

        public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(null);

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
