using TheHouse.Application.Common;
using TheHouse.Application.UseCases;

namespace TheHouse.Http;

// NV-06: guardia central y reutilizable para endpoints autenticados.
// Por cada solicitud en un endpoint con RevocationGuard:
//  - valida el Bearer (firma, claims, `jti`);
//  - consulta ITokenRevocationStore (sin caché ni fallback local);
//  - token revocado/inválido/sin `jti` → 401; Redis caído → 503.
// Nunca registra el JWT, el `jti` ni el header Authorization.
public sealed class TokenRevocationMiddleware(
    RequestDelegate next,
    LogoutService logout,
    ILogger<TokenRevocationMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata
                .GetMetadata<RevocationGuard>() is null)
        {
            await next(context);
            return;
        }

        var authorization =
            context.Request.Headers.Authorization.ToString();

        if (authorization.Length == 0 ||
            !authorization.StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase) ||
            authorization.Length == 7)
        {
            await Reject(context, 401);
            return;
        }

        var token = authorization[7..];

        if (token.AsSpan().IndexOfAny([' ', '\t']) >= 0)
        {
            await Reject(context, 401);
            return;
        }

        try
        {
            // NV-10: el principal validado (incluye `sub` y `sid`) queda
            // disponible para el endpoint protegido.
            context.User = await logout.ValidateActiveAsync(
                token,
                context.RequestAborted);
        }
        catch (InvalidCredentialsException)
        {
            await Reject(context, 401);
            return;
        }
        catch (DependencyUnavailableException)
        {
            await Reject(context, 503);
            return;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            // Sin token, `jti` ni encabezados de autenticación.
            logger.LogInformation(
                "Authenticated request passed revocation guard.");
        }

        await next(context);
    }

    private static async Task Reject(
        HttpContext context,
        int status)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";

        await ProblemResponses.WriteAsync(context, status);
    }
}
