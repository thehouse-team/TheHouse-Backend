using System.Security.Claims;
using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Security;

public interface IJwtTokenService
{
    AccessToken CreateAccessToken(User user);

    ClaimsPrincipal ValidateAccessToken(string token);
}

public sealed record AccessToken(
    string Token,
    string TokenType,
    int ExpiresIn,
    DateTimeOffset ExpiresAt,
    string Jti);
