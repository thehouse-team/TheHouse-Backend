using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Auth;
using TheHouse.Domain.Entities;

using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-10: ciclo de vida de la sesión (refresh token rotativo).
//  - Inicio: emite refresh token y access token con `sid`.
//  - Refresh: rotación atómica; reuso de un token rotado revoca la sesión.
//  - Revocación: refresh tokens en MongoDB y `sid` en Redis (todas las réplicas).
// Toda falla de credencial es InvalidCredentialsException (401 uniforme);
// las dependencias caídas producen DependencyUnavailableException (503).
public sealed class SessionService(
    IRefreshTokenStore refreshTokens,
    ISessionRevocationStore sessionRevocation,
    IUserRepository users,
    IJwtTokenService jwtTokenService,
    RefreshTokenOptions options,
    TimeProvider timeProvider,
    IAccountSuspensionQuery? suspensions = null)
{
    public const string ReasonLogout = "logout";

    public const string ReasonReuse = "reuse";

    public const string ReasonUserMissing = "user_missing";

    public async Task<LoginResponse> StartAsync(
        User user,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // NV-21: solo después de verificar la contraseña, para no revelar
        // el estado de una cuenta a quien no conoce sus credenciales.
        await EnsureNotSuspendedAsync(user.Id, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var sessionId = SessionTokens.NewSessionId();
        var sessionExpiresAt = now.Add(options.SessionLifetime);

        var refreshToken = await IssueRefreshTokenAsync(
            user.Id,
            sessionId,
            now,
            sessionExpiresAt,
            cancellationToken);

        var access = jwtTokenService.CreateAccessToken(user, sessionId);

        return new LoginResponse(
            access.Token,
            access.TokenType,
            access.ExpiresIn,
            refreshToken);
    }

    public async Task<LoginResponse> RefreshAsync(
        string? refreshToken,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (refreshToken is null)
        {
            throw new RequestValidationException(
                "refreshToken",
                "refreshToken is required.");
        }

        // Un valor con formato inválido no puede existir: mismo 401
        // que un token desconocido, sin consultar la base.
        if (!SessionTokens.IsWellFormedRefreshToken(refreshToken))
        {
            throw new InvalidCredentialsException();
        }

        var now = timeProvider.GetUtcNow();
        var hash = SessionTokens.Hash(refreshToken);

        var rotated = await refreshTokens.TryRotateAsync(
            hash,
            now,
            cancellationToken);

        if (rotated is null)
        {
            var existing = await refreshTokens.FindAsync(
                hash,
                cancellationToken);

            if (existing?.IsReuse == true)
            {
                await RevokeSessionAsync(
                    existing.SessionId,
                    ReasonReuse,
                    cancellationToken);
            }

            throw new InvalidCredentialsException();
        }

        if (await sessionRevocation.IsSessionRevokedAsync(
                rotated.SessionId,
                cancellationToken))
        {
            throw new InvalidCredentialsException();
        }

        var user = await users.GetByIdAsync(
            rotated.UserId,
            cancellationToken);

        if (user is null)
        {
            await RevokeSessionAsync(
                rotated.SessionId,
                ReasonUserMissing,
                cancellationToken);

            throw new InvalidCredentialsException();
        }

        if (suspensions is not null && await suspensions.IsSuspendedAsync(user.Id, cancellationToken))
        {
            await RevokeSessionAsync(rotated.SessionId, "suspended", cancellationToken);
            throw new ForbiddenException("account_suspended");
        }

        var newRefreshToken = await IssueRefreshTokenAsync(
            user.Id,
            rotated.SessionId,
            now,
            rotated.SessionExpiresAt,
            cancellationToken);

        var access = jwtTokenService.CreateAccessToken(
            user,
            rotated.SessionId);

        return new LoginResponse(
            access.Token,
            access.TokenType,
            access.ExpiresIn,
            newRefreshToken);
    }

    public async Task RevokeSessionAsync(
        string sessionId,
        string reason,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Redis primero: rechaza de inmediato los access tokens de la
        // sesión en todas las réplicas; después los refresh tokens.
        await sessionRevocation.RevokeSessionAsync(
            sessionId,
            options.RevokedSessionTtl,
            cancellationToken);

        await refreshTokens.RevokeSessionAsync(
            sessionId,
            reason,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private async Task EnsureNotSuspendedAsync(string userId, CancellationToken cancellationToken)
    {
        if (suspensions is not null && await suspensions.IsSuspendedAsync(userId, cancellationToken))
        {
            throw new ForbiddenException("account_suspended");
        }
    }

    public Task<bool> IsSessionRevokedAsync(
        string sessionId,
        CancellationToken cancellationToken) =>
        sessionRevocation.IsSessionRevokedAsync(
            sessionId,
            cancellationToken);

    private async Task<string> IssueRefreshTokenAsync(
        string userId,
        string sessionId,
        DateTimeOffset now,
        DateTimeOffset sessionExpiresAt,
        CancellationToken cancellationToken)
    {
        var value = SessionTokens.NewRefreshToken();
        var tokenExpiresAt = now.Add(options.TokenLifetime);

        await refreshTokens.CreateAsync(
            new RefreshToken
            {
                TokenHash = SessionTokens.Hash(value),
                SessionId = sessionId,
                UserId = userId,
                CreatedAt = now,
                ExpiresAt = tokenExpiresAt < sessionExpiresAt
                    ? tokenExpiresAt
                    : sessionExpiresAt,
                SessionExpiresAt = sessionExpiresAt,
            },
            cancellationToken);

        return value;
    }
}
