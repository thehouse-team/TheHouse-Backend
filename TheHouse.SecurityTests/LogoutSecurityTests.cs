using System.Security.Claims;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;

namespace TheHouse.SecurityTests;

// NV-06 T09 (seguridad estática): blindaje de la clave de revocación
// y semántica de `jti` canónico. Los hallazgos HTTP (no-store,
// rate limit, logs sin secretos) se evidencian en integración.
public sealed class LogoutSecurityTests
{
    [Fact]
    public void Canonical_jti_rejects_non_hex_identities()
    {
        // Compacta: evita claves manipuladas por `jti` no canónicos.
        Assert.False(RevocationKeys.IsCanonicalJti(null));
        Assert.False(RevocationKeys.IsCanonicalJti(""));
        Assert.False(RevocationKeys.IsCanonicalJti("abc"));
        Assert.False(RevocationKeys.IsCanonicalJti(
            "0123456789abcdef0123456789abcde:"));
        // Mayúsculas no son canónicas frente a la emisión NV-05.
        Assert.False(RevocationKeys.IsCanonicalJti(
            "0123456789ABCDEF0123456789ABCDEF"));
        Assert.False(RevocationKeys.IsCanonicalJti(
            "0123456789abcdef0123456789abcdef0"));
        Assert.True(RevocationKeys.IsCanonicalJti(
            "0123456789abcdef0123456789abcdef"));
    }

    [Fact]
    public async Task Non_canonical_jti_is_rejected_as_401()
    {
        var service = new LogoutService(
            new StubJwt("0123456789ABCDEF0123456789ABCDEF"),
            new NoopStore(),
            TimeProvider.System);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.RevokeAsync("any", default));
    }

    [Fact]
    public void Revocation_key_prefix_matches_specification()
    {
        Assert.Equal(
            "auth:revoked:jti:0123456789abcdef0123456789abcdef",
            RevocationKeys.ForJti(
                "0123456789abcdef0123456789abcdef"));
    }

    private sealed class StubJwt(string jti) : IJwtTokenService
    {
        public AccessToken CreateAccessToken(
            Domain.Entities.User user) =>
            throw new InvalidOperationException();

        public ClaimsPrincipal ValidateAccessToken(string token)
        {
            var identity =
                new ClaimsIdentity("Test");

            identity.AddClaim(
                new Claim(
                    RevocationKeys.ExpClaim,
                    "1900000000"));

            identity.AddClaim(
                new Claim(
                    RevocationKeys.JtiClaim,
                    jti));

            return new ClaimsPrincipal(identity);
        }
    }

    private sealed class NoopStore : ITokenRevocationStore
    {
        public Task RevokeAsync(
            string jti,
            TimeSpan ttl,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<bool> IsRevokedAsync(
            string jti,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }
}
