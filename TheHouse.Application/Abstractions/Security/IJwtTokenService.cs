using System.Security.Claims;
using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Security;

public interface IJwtTokenService
{
    // NV-10: el access token incluye `sid`, el identificador de la
    // sesión a la que pertenece su refresh token.
    AccessToken CreateAccessToken(User user, string sessionId);

    ClaimsPrincipal ValidateAccessToken(string token);
}

public sealed record AccessToken(
    string Token,
    string TokenType,
    int ExpiresIn,
    DateTimeOffset ExpiresAt,
    string Jti);
