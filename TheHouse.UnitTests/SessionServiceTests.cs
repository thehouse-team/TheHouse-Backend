using System.Security.Claims;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;

namespace TheHouse.UnitTests;

// NV-10 T01, T02, T04, T05: sesión con refresh token rotativo.
public sealed class SessionServiceTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryRefreshTokenStore store = new();
    private readonly InMemorySessionRevocationStore revocation = new();
    private readonly SingleUserRepository users = new();
    private readonly RecordingJwt jwt = new();
    private readonly AdjustableClock clock = new(Start);

    private SessionService Service(RefreshTokenOptions? options = null) =>
        new(store, revocation, users, jwt, options ?? new RefreshTokenOptions(), clock);

    [Fact]
    public async Task Start_issues_refresh_token_and_access_token_with_same_session()
    {
        var response = await Service().StartAsync(users.User, CancellationToken.None);

        Assert.True(SessionTokens.IsWellFormedRefreshToken(response.RefreshToken));
        var stored = Assert.Single(store.Tokens.Values);
        Assert.Equal(SessionTokens.Hash(response.RefreshToken), stored.TokenHash);
        Assert.DoesNotContain(store.Tokens.Keys, key => key == response.RefreshToken);
        Assert.Equal(stored.SessionId, jwt.LastSessionId);
        Assert.True(SessionTokens.IsCanonicalSessionId(stored.SessionId));
        Assert.Equal(Start.AddDays(30), stored.ExpiresAt);
        Assert.Equal(Start.AddDays(90), stored.SessionExpiresAt);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal(900, response.ExpiresIn);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_keeps_session()
    {
        var service = Service();
        var login = await service.StartAsync(users.User, CancellationToken.None);
        var sessionId = jwt.LastSessionId;

        var refreshed = await service.RefreshAsync(login.RefreshToken, CancellationToken.None);

        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(sessionId, jwt.LastSessionId);
        Assert.NotNull(store.Tokens[SessionTokens.Hash(login.RefreshToken)].RotatedAt);
        Assert.True(store.Tokens[SessionTokens.Hash(refreshed.RefreshToken)].IsActive(Start));
    }

    [Fact]
    public async Task Reusing_rotated_token_revokes_whole_session()
    {
        var service = Service();
        var login = await service.StartAsync(users.User, CancellationToken.None);
        var sessionId = jwt.LastSessionId!;
        var refreshed = await service.RefreshAsync(login.RefreshToken, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.RefreshAsync(login.RefreshToken, CancellationToken.None));

        Assert.True(revocation.Revoked.ContainsKey(sessionId));
        Assert.All(store.Tokens.Values, token => Assert.Equal(SessionService.ReasonReuse, token.RevokedReason));
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.RefreshAsync(refreshed.RefreshToken, CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_refresh_with_same_token_issues_at_most_one_pair()
    {
        var service = Service();
        var login = await service.StartAsync(users.User, CancellationToken.None);

        var attempts = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(async () =>
            {
                try
                {
                    await service.RefreshAsync(login.RefreshToken, CancellationToken.None);
                    return true;
                }
                catch (InvalidCredentialsException)
                {
                    return false;
                }
            }))
            .ToArray();

        var results = await Task.WhenAll(attempts);

        Assert.True(results.Count(success => success) <= 1);
    }

    [Fact]
    public async Task Expired_refresh_token_is_rejected()
    {
        var service = Service();
        var login = await service.StartAsync(users.User, CancellationToken.None);
        clock.Advance(TimeSpan.FromDays(30) + TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.RefreshAsync(login.RefreshToken, CancellationToken.None));
    }

    [Fact]
    public async Task Session_cannot_be_extended_beyond_absolute_lifetime()
    {
        var service = Service();
        var token = (await service.StartAsync(users.User, CancellationToken.None)).RefreshToken;

        for (var day = 0; day < 3; day++)
        {
            clock.Advance(TimeSpan.FromDays(29));
            token = (await service.RefreshAsync(token, CancellationToken.None)).RefreshToken;
        }

        // Día 87: el token nuevo vence con la sesión (día 90), no a los 30 días.
        Assert.Equal(Start.AddDays(90), store.Tokens[SessionTokens.Hash(token)].ExpiresAt);

        clock.Advance(TimeSpan.FromDays(3) + TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.RefreshAsync(token, CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_and_malformed_tokens_are_uniform_401()
    {
        var service = Service();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.RefreshAsync(SessionTokens.NewRefreshToken(), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.RefreshAsync("short", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.RefreshAsync(new string('*', SessionTokens.RefreshTokenLength), CancellationToken.None));
    }

    [Fact]
    public async Task Missing_refresh_token_is_validation_error()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            Service().RefreshAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task Refresh_for_deleted_user_revokes_session()
    {
        var service = Service();
        var login = await service.StartAsync(users.User, CancellationToken.None);
        var sessionId = jwt.LastSessionId!;
        users.Deleted = true;

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.RefreshAsync(login.RefreshToken, CancellationToken.None));

        Assert.True(revocation.Revoked.ContainsKey(sessionId));
    }

    [Fact]
    public async Task Revoked_session_rejects_refresh()
    {
        var service = Service();
        var login = await service.StartAsync(users.User, CancellationToken.None);

        await service.RevokeSessionAsync(jwt.LastSessionId!, SessionService.ReasonLogout, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.RefreshAsync(login.RefreshToken, CancellationToken.None));
        Assert.Equal(TimeSpan.FromSeconds(930), Assert.Single(revocation.Revoked.Values));
    }

    [Fact]
    public async Task Store_failure_is_fail_closed()
    {
        store.Unavailable = true;

        await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            Service().StartAsync(users.User, CancellationToken.None));
    }

    [Fact]
    public async Task Redis_failure_during_refresh_is_fail_closed()
    {
        var service = Service();
        var login = await service.StartAsync(users.User, CancellationToken.None);
        revocation.Unavailable = true;

        await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            service.RefreshAsync(login.RefreshToken, CancellationToken.None));
    }

    [Fact]
    public void Generated_values_have_expected_format()
    {
        var token = SessionTokens.NewRefreshToken();
        var session = SessionTokens.NewSessionId();

        Assert.Equal(43, token.Length);
        Assert.True(SessionTokens.IsWellFormedRefreshToken(token));
        Assert.True(SessionTokens.IsCanonicalSessionId(session));
        Assert.Equal(64, SessionTokens.Hash(token).Length);
        Assert.NotEqual(token, SessionTokens.NewRefreshToken());
        Assert.False(SessionTokens.IsCanonicalSessionId("ABCDEF0123456789ABCDEF0123456789"));
    }

    [Theory]
    [InlineData(0, 90, 930, false)]
    [InlineData(31, 90, 930, false)]
    [InlineData(30, 91, 930, false)]
    [InlineData(30, 20, 930, false)]
    [InlineData(30, 90, 600, false)]
    [InlineData(30, 90, 930, true)]
    public void Options_are_validated(int tokenDays, int sessionDays, int revokedTtl, bool valid)
    {
        var options = new RefreshTokenOptions
        {
            TokenLifetimeDays = tokenDays,
            SessionLifetimeDays = sessionDays,
            RevokedSessionTtlSeconds = revokedTtl,
        };

        Assert.Equal(valid, options.IsValid());
    }

    [Fact]
    public async Task Account_returns_only_real_fields_for_subject()
    {
        var me = await new AccountService(users).GetMeAsync(users.User.Id, CancellationToken.None);

        Assert.Equal(users.User.Id, me.Id);
        Assert.Equal(users.User.Email, me.Email);
        Assert.Equal(users.User.CreatedAt, me.CreatedAt);
        Assert.Equal(3, typeof(Application.Contracts.Users.MeResponse).GetProperties().Length);
    }

    [Fact]
    public async Task Account_for_missing_user_is_401()
    {
        users.Deleted = true;

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            new AccountService(users).GetMeAsync(users.User.Id, CancellationToken.None));
    }

    private sealed class SingleUserRepository : IUserRepository
    {
        public User User { get; } = new()
        {
            Id = "665f1c2a9b1e4a0012345678",
            Email = "student@example.com",
            PasswordHash = "not-used",
            CreatedAt = Start.AddDays(-10),
            UpdatedAt = Start.AddDays(-10),
        };

        public bool Deleted { get; set; }

        public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult<User?>(null);

        public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(!Deleted && id == User.Id ? User : null);

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingJwt : IJwtTokenService
    {
        public string? LastSessionId { get; private set; }

        public AccessToken CreateAccessToken(User user, string sessionId)
        {
            LastSessionId = sessionId;
            return new AccessToken("access-" + sessionId, "Bearer", 900, DateTimeOffset.UtcNow.AddMinutes(15), "jti");
        }

        public ClaimsPrincipal ValidateAccessToken(string token) =>
            throw new NotSupportedException();
    }
}
