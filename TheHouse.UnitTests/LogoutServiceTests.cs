using System.Security.Claims;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;

namespace TheHouse.UnitTests;

// NV-06 T07: clave/TTL, token sin `jti`, expirado, semÃ¡ntica
// 204/401 del caso de uso y fallo cerrado del almacÃ©n. La emisiÃ³n y
// validaciÃ³n criptogrÃ¡fica REAL del JWT se evidencia en
// SecurityTests y pruebas de integraciÃ³n; aquÃ­ el `jti`, el `exp` y
// la validez se controlan con stubs deterministas.
public sealed class LogoutServiceTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Revocation_key_uses_specified_prefix()
    {
        Assert.Equal(
            "thehouse:u2:auth:revoked:jti:v1:0123456789abcdef0123456789abcdef",
            RevocationKeys.ForJti(
                "0123456789abcdef0123456789abcdef"));
    }

    [Fact]
    public void Canonical_jti_length_matches_specification()
    {
        Assert.Equal(32, RevocationKeys.CanonicalJtiLength);
    }

    [Fact]
    public async Task Valid_token_is_revoked_with_ttl_until_exp()
    {
        var store = new RecordingStore();
        var time = new FixedTimeProvider(Start);
        var jti = "0123456789abcdef0123456789abcdef";
        var service = Create(store, time, jti);

        // exp = Start + 900 s (igual que la emisiÃ³n NV-05).
        var token = Token(jti, 900);
        time.Advance(TimeSpan.FromSeconds(100));

        await service.RevokeAsync(token, default);

        var entry = Assert.Single(store.Revocations);
        Assert.Equal(jti, entry.Jti);
        Assert.Equal(800, entry.Ttl.TotalSeconds);
    }

    [Fact]
    public async Task Near_expiry_ttl_is_one_second_minimum()
    {
        var store = new RecordingStore();
        var time = new FixedTimeProvider(Start);
        var jti = "0123456789abcdef0123456789abcdef";
        var service = Create(store, time, jti);

        time.Advance(TimeSpan.FromSeconds(899));

        await service.RevokeAsync(
            Token(jti, 900),
            default);

        var entry = Assert.Single(store.Revocations);
        Assert.Equal(1, entry.Ttl.TotalSeconds);
    }

    [Fact]
    public async Task Already_revoked_token_is_rejected_as_401()
    {
        var store = new RecordingStore();
        var time = new FixedTimeProvider(Start);
        var jti = "0123456789abcdef0123456789abcdef";
        var service = Create(store, time, jti);

        store.MarkRevoked(jti);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.RevokeAsync(
                Token(jti, 900),
                default));
    }

    [Fact]
    public async Task Store_failure_during_check_is_fail_closed()
    {
        var store = new RecordingStore { Unavailable = true };
        var service = Create(
            store,
            new FixedTimeProvider(Start),
            "0123456789abcdef0123456789abcdef");

        await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => service.RevokeAsync(
                Token("0123456789abcdef0123456789abcdef", 900),
                default));

        Assert.Empty(store.Revocations);
    }

    [Fact]
    public async Task Store_failure_during_revoke_is_fail_closed()
    {
        var store = new RecordingStore
        {
            UnavailableOnRevoke = true
        };

        var service = Create(
            store,
            new FixedTimeProvider(Start),
            "0123456789abcdef0123456789abcdef");

        await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => service.RevokeAsync(
                Token("0123456789abcdef0123456789abcdef", 900),
                default));
    }

    [Fact]
    public async Task Token_without_usable_jti_is_rejected()
    {
        var service = new LogoutService(
            new StubJwt(throwOnValidate: false),
            new RecordingStore(),
            new FixedTimeProvider(Start));

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.RevokeAsync("any-token", default));
    }

    [Fact]
    public async Task Invalid_token_is_rejected_as_401()
    {
        var service = new LogoutService(
            new StubJwt(throwOnValidate: true),
            new RecordingStore(),
            new FixedTimeProvider(Start));

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.RevokeAsync("not-a-jwt", default));
    }

    [Fact]
    public async Task Unauthenticated_principal_is_rejected()
    {
        var service = new LogoutService(
            new AnonymousJwt(),
            new RecordingStore(),
            new FixedTimeProvider(Start));

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.RevokeAsync("any-token", default));
    }

    [Fact]
    public async Task Missing_exp_claim_is_rejected()
    {
        var service = new LogoutService(
            new StubJwt(throwOnValidate: false, includeExp: false),
            new RecordingStore(),
            new FixedTimeProvider(Start));

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.RevokeAsync("any-token", default));
    }

    [Fact]
    public async Task EnsureNotRevoked_accepts_valid_token()
    {
        var store = new RecordingStore();
        var service = Create(
            store,
            new FixedTimeProvider(Start),
            "0123456789abcdef0123456789abcdef");

        await service.EnsureNotRevokedAsync(
            Token("0123456789abcdef0123456789abcdef", 900),
            default);
    }

    [Fact]
    public async Task EnsureNotRevoked_rejects_revoked_token()
    {
        var store = new RecordingStore();
        var time = new FixedTimeProvider(Start);
        var jti = "0123456789abcdef0123456789abcdef";
        var service = Create(store, time, jti);

        store.MarkRevoked(jti);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.EnsureNotRevokedAsync(
                Token(jti, 900),
                default));
    }

    // --- Soportes -------------------------------------------------

    private static LogoutService Create(
        RecordingStore store,
        FixedTimeProvider time,
        string jti) =>
        new(
            new StubJwt(
                throwOnValidate: false,
                jti: jti,
                expSeconds:
                    time.GetUtcNow()
                        .ToUnixTimeSeconds() + 900),
            store,
            time);

    private static string Token(string jti, int ttl) => jti;

    private sealed class FixedTimeProvider(DateTimeOffset now)
        : TimeProvider
    {
        private DateTimeOffset value = now;

        public void Advance(TimeSpan delta) =>
            value = value.Add(delta);

        public override DateTimeOffset GetUtcNow() => value;
    }

    // Stub que emula la validaciÃ³n de NV-05: identidad autenticada
    // con `jti` (32 hex antiguo) y `exp` configurables.
    private sealed class StubJwt(
        bool throwOnValidate,
        string? jti = null,
        long? expSeconds = null,
        bool includeExp = true) : IJwtTokenService
    {
        public AccessToken CreateAccessToken(User user) =>
            throw new InvalidOperationException();

        public ClaimsPrincipal ValidateAccessToken(string token)
        {
            if (throwOnValidate)
            {
                throw new InvalidOperationException(
                    "Invalid token (simulated).");
            }

            var identity = new ClaimsIdentity("Bearer");

            identity.AddClaim(new Claim("sub", "665f1c2a9b1e4a0012345678"));
            identity.AddClaim(new Claim("email", "user@example.com"));

            if (jti is not null)
            {
                identity.AddClaim(new Claim("jti", jti));
            }

            if (includeExp && expSeconds.HasValue)
            {
                identity.AddClaim(new Claim(
                    "exp",
                    expSeconds.Value.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)));
            }

            return new ClaimsPrincipal(identity);
        }
    }

    // Stub sin identidad autenticada: el principal no vale.
    private sealed class AnonymousJwt : IJwtTokenService
    {
        public AccessToken CreateAccessToken(User user) =>
            throw new InvalidOperationException();

        public ClaimsPrincipal ValidateAccessToken(string token) =>
            new(new ClaimsIdentity());
    }

    private sealed class RecordingStore : ITokenRevocationStore
    {
        public readonly List<(string Jti, TimeSpan Ttl)>
            Revocations = [];

        public bool Unavailable { get; set; }

        public bool UnavailableOnRevoke { get; set; }

        public void MarkRevoked(string jti) =>
            Revocations.Add((jti, TimeSpan.Zero));

        public Task RevokeAsync(
            string jti,
            TimeSpan ttl,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Unavailable || UnavailableOnRevoke)
            {
                throw new DependencyUnavailableException();
            }

            Revocations.Add((jti, ttl));

            return Task.CompletedTask;
        }

        public Task<bool> IsRevokedAsync(
            string jti,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Unavailable)
            {
                throw new DependencyUnavailableException();
            }

            return Task.FromResult(
                Revocations.Any(entry => entry.Jti == jti));
        }
    }
}
