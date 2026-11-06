using System.Globalization;
using System.Security.Claims;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;

namespace TheHouse.Application.UseCases;

// NV-06: logout stateless. Valida el JWT de NV-05, exige `jti`
// canónico, consulta la revocación compartida y escribe el marcador
// con TTL hasta `exp`. No hay caché local ni fallback en memoria:
// Redis caído produce DependencyUnavailableException (503).
public sealed class LogoutService(
    IJwtTokenService jwtTokenService,
    ITokenRevocationStore revocationStore,
    TimeProvider timeProvider,
    SessionService? sessions = null)
{
    // Primer logout con token válido: revoca y devuelve 204 en el
    // endpoint. Reutilización: 401. Este método es usado tanto por el
    // endpoint como por solicitudes autenticadas futuras.
    public async Task RevokeAsync(
        string token,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var principal = ValidateRequiredPrincipal(token);

        await EnsureNotRevokedCoreAsync(principal, cancellationToken);

        var jti = CanonicalJti(principal);

        // TTL hasta `exp`, sin exceder la vida restante del token.
        var expirationSeconds = ExpirationSeconds(principal);
        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var remaining = expirationSeconds - now;

        var ttl = remaining > 0
            ? TimeSpan.FromSeconds(remaining)
            : TimeSpan.FromSeconds(1);

        await revocationStore.RevokeAsync(
            jti,
            ttl,
            cancellationToken);

        // NV-10: si el token pertenece a una sesión, se revocan también
        // su refresh token y los demás access tokens de la sesión.
        var sessionId = SessionId(principal);
        if (sessionId is not null && sessions is not null)
        {
            await sessions.RevokeSessionAsync(
                sessionId,
                SessionService.ReasonLogout,
                cancellationToken);
        }
    }

    // NV-10: igual que EnsureNotRevokedAsync, pero devuelve el principal
    // validado para que el endpoint use `sub` sin volver a validar.
    public async Task<ClaimsPrincipal> ValidateActiveAsync(
        string token,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var principal = ValidateRequiredPrincipal(token);
        await EnsureNotRevokedCoreAsync(principal, cancellationToken);
        return principal;
    }

    // Consulta de revocación para solicitudes autenticadas (T03):
    // valida el token y rechaza 401 si su `jti` figura revocado.
    public async Task EnsureNotRevokedAsync(
        string token,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await EnsureNotRevokedCoreAsync(
            ValidateRequiredPrincipal(token),
            cancellationToken);
    }

    private async Task EnsureNotRevokedCoreAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var jti = CanonicalJti(principal);

        if (await revocationStore.IsRevokedAsync(
                jti,
                cancellationToken))
        {
            throw new InvalidCredentialsException();
        }

        // NV-10: un access token de una sesión revocada (logout o reuso
        // de refresh token) se rechaza de inmediato en cualquier réplica.
        var sessionId = SessionId(principal);
        if (sessionId is not null &&
            sessions is not null &&
            await sessions.IsSessionRevokedAsync(
                sessionId,
                cancellationToken))
        {
            throw new InvalidCredentialsException();
        }
    }

    // `sid` es opcional (tokens emitidos antes de NV-10 no lo traen),
    // pero si existe debe tener el formato canónico.
    private static string? SessionId(ClaimsPrincipal principal)
    {
        var value = principal
            .FindFirst(SessionTokens.SessionIdClaim)
            ?.Value;

        if (value is null)
        {
            return null;
        }

        if (!SessionTokens.IsCanonicalSessionId(value))
        {
            throw new InvalidCredentialsException();
        }

        return value;
    }

    private ClaimsPrincipal ValidateRequiredPrincipal(
        string token)
    {
        ClaimsPrincipal principal;

        try
        {
            principal = jwtTokenService.ValidateAccessToken(token);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException and
                not DependencyUnavailableException)
        {
            // Cualquier fallo de validación (firma, algoritmo, issuer,
            // audience, exp, nbf) es credencial inválida: 401 genérico.
            throw new InvalidCredentialsException();
        }

        if (principal.Identity?.IsAuthenticated != true)
        {
            throw new InvalidCredentialsException();
        }

        return principal;
    }

    private static string CanonicalJti(ClaimsPrincipal principal)
    {
        var jti = principal
            .FindFirst(RevocationKeys.JtiClaim)
            ?.Value ?? string.Empty;

        if (!RevocationKeys.IsCanonicalJti(jti))
        {
            // Un token sin `jti` usable no puede revocarse: 401.
            throw new InvalidCredentialsException();
        }

        return jti;
    }

    private static long ExpirationSeconds(ClaimsPrincipal principal)
    {
        var value = principal
            .FindFirst(RevocationKeys.ExpClaim)
            ?.Value;

        if (!long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var expirationSeconds) ||
            expirationSeconds <= 0)
        {
            throw new InvalidCredentialsException();
        }

        return expirationSeconds;
    }
}
