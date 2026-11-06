using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using TheHouse.Infrastructure.Configuration;

namespace TheHouse.Infrastructure.Security;

public sealed class JwtTokenService(
    JwtOptions options,
    TimeProvider timeProvider) : IJwtTokenService
{
    private readonly SymmetricSecurityKey signingKey =
        new(Encoding.UTF8.GetBytes(
            options.SigningKey ??
            throw new InvalidOperationException(
                "JWT signing key is not configured.")));

    public AccessToken CreateAccessToken(User user, string sessionId)
    {
        if (!SessionTokens.IsCanonicalSessionId(sessionId))
        {
            throw new ArgumentException(
                "Invalid session identifier.",
                nameof(sessionId));
        }

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddSeconds(options.ExpirationSeconds);

        var jti = Convert
            .ToHexString(RandomNumberGenerator.GetBytes(16))
            .ToLowerInvariant();

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            // NV-10: identificador de sesión (aditivo; no es obligatorio
            // al validar para aceptar tokens emitidos antes de NV-10).
            new Claim(SessionTokens.SessionIdClaim, sessionId),
            new Claim(
                JwtRegisteredClaimNames.Iat,
                now.ToUnixTimeSeconds().ToString(
                    CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
        };

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            options.ExpirationSeconds,
            expiresAt,
            jti);
    }

    public ClaimsPrincipal ValidateAccessToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new SecurityTokenException("Invalid access token.");
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,

            ValidateIssuer = true,
            ValidIssuer = options.Issuer,

            ValidateAudience = true,
            ValidAudience = options.Audience,

            ValidateLifetime = true,
            ClockSkew =
                TimeSpan.FromSeconds(options.ClockSkewSeconds),

            RequireExpirationTime = true,
            RequireSignedTokens = true,

            ValidAlgorithms =
                [SecurityAlgorithms.HmacSha256]
        };

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };

        var principal = handler.ValidateToken(
            token,
            parameters,
            out var validatedToken);

        if (validatedToken is not JwtSecurityToken jwt ||
            !string.Equals(
                jwt.Header.Alg,
                SecurityAlgorithms.HmacSha256,
                StringComparison.Ordinal))
        {
            throw new SecurityTokenException(
                "Invalid access token.");
        }

        var requiredClaims = new[]
        {
            JwtRegisteredClaimNames.Sub,
            JwtRegisteredClaimNames.Email,
            JwtRegisteredClaimNames.Jti,
            JwtRegisteredClaimNames.Iat
        };

        if (requiredClaims.Any(required =>
                !jwt.Claims.Any(claim =>
                    claim.Type == required &&
                    !string.IsNullOrWhiteSpace(claim.Value))) ||
            !jwt.Payload.NotBefore.HasValue ||
            !jwt.Payload.Expiration.HasValue)
        {
            throw new SecurityTokenException(
                "Invalid access token.");
        }

        return principal;
    }
}
