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

    [Fact]
    public void Access_token_uses_expected_algorithm_and_required_claims()
    {
        var service = Create();

        var token = service.CreateAccessToken(User());

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

        var token = untrusted.CreateAccessToken(User());

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
