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
    TimeProvider timeProvider)
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
