using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TheHouse.Domain.Entities;
using TheHouse.Infrastructure.Configuration;
using TheHouse.Infrastructure.Security;

namespace TheHouse.SecurityTests;

public sealed class JwtSecurityTests
{
    private const string Key =
        "0123456789abcdef0123456789abcdef";

    private const string SessionId =
        "fedcba9876543210fedcba9876543210";

    // NV-10: el access token lleva `sid` y los tokens sin `sid` (emitidos
    // antes de NV-10) siguen siendo válidos hasta su expiración.
    [Fact]
    public void Access_token_includes_session_id_claim()
    {
        var service = Create();

        var token = service.CreateAccessToken(User(), SessionId);
        var principal = service.ValidateAccessToken(token.Token);

        Assert.Equal(SessionId, principal.FindFirst("sid")?.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("FEDCBA9876543210FEDCBA9876543210")]
    public void Non_canonical_session_id_is_refused(string sessionId)
    {
        Assert.Throws<ArgumentException>(() =>
            Create().CreateAccessToken(User(), sessionId));
    }

    [Fact]
    public void Legacy_token_without_session_id_is_still_valid()
    {
        var now = DateTimeOffset.UtcNow;
        var legacy = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "thehouse-api",
            audience: "thehouse-clients",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, "665f1c2a9b1e4a0012345678"),
                new Claim(JwtRegisteredClaimNames.Email, "user@example.com"),
                new Claim(JwtRegisteredClaimNames.Jti, "0123456789abcdef0123456789abcdef"),
                new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            ],
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(15).UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
                SecurityAlgorithms.HmacSha256)));

        var principal = Create().ValidateAccessToken(legacy);

        Assert.Null(principal.FindFirst("sid"));
    }

    [Fact]
    public void Access_token_uses_expected_algorithm_and_required_claims()
    {
        var service = Create();

        var token = service.CreateAccessToken(User(), SessionId);

        Assert.Equal("Bearer", token.TokenType);
        Assert.Equal(900, token.ExpiresIn);
        Assert.Equal(32, token.Jti.Length);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.Token);

        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Header.Alg);
        Assert.Equal("thehouse-api", jwt.Issuer);
        Assert.Contains("thehouse-clients", jwt.Audiences);

        Assert.NotNull(jwt.Claims.SingleOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub));
        Assert.NotNull(jwt.Claims.SingleOrDefault(c => c.Type == JwtRegisteredClaimNames.Email));
        Assert.NotNull(jwt.Claims.SingleOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti));
        Assert.NotNull(jwt.Claims.SingleOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat));

        var principal = service.ValidateAccessToken(token.Token);

        Assert.Equal(
            "665f1c2a9b1e4a0012345678",
            principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
    }

    [Fact]
    public void Token_signed_with_different_key_is_rejected()
    {
        var trusted = Create();

        var untrusted = new JwtTokenService(
            Options("abcdefghijklmnopqrstuvwxyzABCDEF"),
            TimeProvider.System);

        var token = untrusted.CreateAccessToken(User(), SessionId);

        Assert.ThrowsAny<SecurityTokenException>(() =>
            trusted.ValidateAccessToken(token.Token));
    }

    [Fact]
    public void Alg_none_is_rejected()
    {
        var service = Create();

        var token = new JwtSecurityToken(
            issuer: "thehouse-api",
            audience: "thehouse-clients",
            claims: Claims(includeJti: true),
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: null);

        var encoded = new JwtSecurityTokenHandler()
            .WriteToken(token);

        Assert.ThrowsAny<SecurityTokenException>(() =>
            service.ValidateAccessToken(encoded));
    }

    [Fact]
    public void Expired_token_is_rejected()
    {
        var service = Create();

        var token = SignedToken(
            Claims(includeJti: true),
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddMinutes(-2));

        Assert.ThrowsAny<SecurityTokenException>(() =>
            service.ValidateAccessToken(token));
    }

    [Fact]
    public void Token_without_jti_is_rejected()
    {
        var service = Create();

        var token = SignedToken(
            Claims(includeJti: false),
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(10));

        Assert.ThrowsAny<SecurityTokenException>(() =>
            service.ValidateAccessToken(token));
    }

    private static JwtTokenService Create() =>
        new(Options(Key), TimeProvider.System);

    private static JwtOptions Options(string key) =>
        new()
        {
            Enabled = true,
            SigningKey = key,
            Issuer = "thehouse-api",
            Audience = "thehouse-clients",
            ExpirationSeconds = 900,
            ClockSkewSeconds = 30
        };

    private static User User() =>
        new()
        {
            Id = "665f1c2a9b1e4a0012345678",
            Email = "test@example.com",
            PasswordHash = "not-used",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static IEnumerable<Claim> Claims(bool includeJti)
    {
        yield return new Claim(
            JwtRegisteredClaimNames.Sub,
            "665f1c2a9b1e4a0012345678");

        yield return new Claim(
            JwtRegisteredClaimNames.Email,
            "test@example.com");

        yield return new Claim(
            JwtRegisteredClaimNames.Iat,
            DateTimeOffset.UtcNow
                .ToUnixTimeSeconds()
                .ToString(CultureInfo.InvariantCulture),
            ClaimValueTypes.Integer64);

        if (includeJti)
        {
            yield return new Claim(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString("N"));
        }
    }

    private static string SignedToken(
        IEnumerable<Claim> claims,
        DateTime notBefore,
        DateTime expires)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "thehouse-api",
            audience: "thehouse-clients",
            claims: claims,
            notBefore: notBefore,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}
