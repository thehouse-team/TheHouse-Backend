using System.Net.Http.Headers;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Http;

// NV-06: POST /api/v1/auth/logout. Bearer obligatorio, sin cuerpo.
// Primer logout válido: 204 sin contenido. Reutilización/expirado:
// 401. Authorization mal formado o cuerpo inesperado: 400. Redis
// caído: 503. Rate limit distribuido 30/60 s por IP: 429.
public static class LogoutEndpoint
{
    public const string Path =
        ApiConventions.Prefix + "/auth/logout";

    public static void MapLogout(this WebApplication app) =>
        app.MapPost(Path, LogoutAsync)
            .WithName("Logout")
            .WithTags("Authentication")
            .WithSummary(
                "Revoke the current access token")
            .Produces(
                StatusCodes.Status204NoContent)
            .ProducesValidationProblem(400)
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .DisableRateLimiting();

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        LogoutService logout,
        IRateLimitStore rateLimitStore,
        LogoutRateLimitOptions rateLimits)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";

        try
        {
            var ipAddress =
                NormalizeAddress(
                    context.Connection.RemoteIpAddress);

            // Toda solicitud cuenta contra el límite IP compartido.
            var decision =
                await rateLimitStore.ConsumeAsync(
                    RateLimitKeys.LogoutIp(ipAddress),
                    rateLimits.LogoutIpPerWindow,
                    rateLimits.Window,
                    context.RequestAborted);

            if (!decision.IsAllowed)
            {
                throw new RateLimitExceededException(
                    rateLimits.RetryAfterSeconds);
            }

            // Sin cuerpo: cualquier contenido unexpected es 400.
            if (context.Request.ContentLength > 0 ||
                await ContentPresent(context))
            {
                return Error(context, 400);
            }

            var authorization =
                context.Request.Headers.Authorization
                    .ToString();

            // Token ausente: 401. Header mal formado: 400.
            if (authorization.Length == 0)
            {
                return Error(context, 401);
            }

            if (!authorization.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase) ||
                authorization.Length == 7 ||
                authorization.AsSpan(7)
                    .IndexOfAny([' ', '\t']) >= 0)
            {
                return Error(context, 400);
            }

            var token = authorization[7..];

            await logout.RevokeAsync(
                token,
                context.RequestAborted);

            return Results.NoContent();
        }
        catch (InvalidCredentialsException)
        {
            return Error(context, 401);
        }
        catch (RateLimitExceededException exception)
        {
            context.Response.Headers.RetryAfter =
                exception.RetryAfterSeconds.ToString(
                    System.Globalization
                        .CultureInfo.InvariantCulture);

            return Error(context, 429);
        }
        catch (DependencyUnavailableException)
        {
            return Error(context, 503);
        }
    }

    private static async Task<bool> ContentPresent(
        HttpContext context)
    {
        if (context.Request.ContentLength.HasValue)
        {
            return false;
        }

        var buffer = new byte[1];
        var read = await context.Request.Body.ReadAsync(
            buffer,
            context.RequestAborted);

        return read > 0;
    }

    private static string NormalizeAddress(
        System.Net.IPAddress? address)
    {
        if (address?.IsIPv4MappedToIPv6 == true)
        {
            address = address.MapToIPv4();
        }

        return address?.ToString() ?? "unknown";
    }

    private static IResult Error(
        HttpContext context,
        int status) =>
        Results.Problem(
            ProblemResponses.Create(
                context,
                status,
                validation: status == 400));
}
