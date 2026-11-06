using System.Security.Claims;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.Infrastructure.Security;

public sealed class UnavailableJwtTokenService : IJwtTokenService
{
    public AccessToken CreateAccessToken(User user, string sessionId) =>
        throw new DependencyUnavailableException();

    public ClaimsPrincipal ValidateAccessToken(string token) =>
        throw new DependencyUnavailableException();
}
