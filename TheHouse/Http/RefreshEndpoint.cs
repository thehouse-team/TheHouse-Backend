using System.Text.Json;
using System.Net.Http.Headers;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Auth;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Http;

// NV-10: POST /auth/refresh. El refresh token es la credencial; no se
// requiere access token. Rate limit por IP en Redis (fallo cerrado).
public static class RefreshEndpoint
{
    public const string Path =
        ApiConventions.Prefix + "/auth/refresh";

    public const int MaximumBodyBytes = 1024;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            MaxDepth = 4
        };

    public static void MapRefresh(this WebApplication app) =>
        app.MapPost(Path, RefreshAsync)
            .WithName("RefreshSession")
            .WithTags("Authentication")
            .WithSummary(
                "Rotate the refresh token and issue a new access token")
            .Accepts<RefreshRequest>("application/json")
            .Produces<LoginResponse>(200)
            .ProducesValidationProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(413)
            .ProducesProblem(415)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .DisableRateLimiting();

    private static async Task<IResult> RefreshAsync(
        HttpContext context,
        SessionService sessions,
        IRateLimitStore rateLimitStore,
        RefreshTokenOptions options)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";

        try
        {
            var decision = await rateLimitStore.ConsumeAsync(
                RateLimitKeys.RefreshIp(
                    NormalizeAddress(context.Connection.RemoteIpAddress)),
                options.RefreshIpPerWindow,
                options.Window,
                context.RequestAborted);

            if (!decision.IsAllowed)
            {
                throw new RateLimitExceededException(
                    options.RetryAfterSeconds);
            }

            if (!MediaTypeHeaderValue.TryParse(
                    context.Request.ContentType,
                    out var contentType) ||
                !string.Equals(
                    contentType.MediaType,
                    "application/json",
                    StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(contentType.CharSet) &&
                 !string.Equals(
                     contentType.CharSet.Trim('"'),
                     "utf-8",
                     StringComparison.OrdinalIgnoreCase)))
            {
                return Error(context, 415);
            }

            if (context.Request.ContentLength > MaximumBodyBytes)
            {
                return Error(context, 413);
            }

            using var body = new MemoryStream();
            var buffer = new byte[512];

            while (true)
            {
                var read = await context.Request.Body.ReadAsync(
                    buffer,
                    context.RequestAborted);

                if (read == 0)
                {
                    break;
                }

                if (body.Length + read > MaximumBodyBytes)
                {
                    return Error(context, 413);
                }

                await body.WriteAsync(
                    buffer.AsMemory(0, read),
                    context.RequestAborted);
            }

            var request = JsonSerializer.Deserialize<RefreshRequest>(
                body.GetBuffer().AsSpan(0, (int)body.Length),
                JsonOptions);

            if (request is null)
            {
                return Error(context, 400);
            }

            var response = await sessions.RefreshAsync(
                request.RefreshToken,
                context.RequestAborted);

            return Results.Json(
                response,
                statusCode: StatusCodes.Status200OK);
        }
        catch (JsonException)
        {
            return Error(context, 400);
        }
        catch (RequestValidationException)
        {
            return Error(context, 400);
        }
        catch (InvalidCredentialsException)
        {
            return Error(context, 401);
        }
        catch (TheHouse.Domain.Exceptions.ForbiddenException exception)
        {
            // NV-21: cuenta suspendida.
            context.Items[UserResourceProblems.CodeItem] = exception.Reason;
            return Error(context, 403);
        }
        catch (RateLimitExceededException exception)
        {
            context.Response.Headers.RetryAfter =
                exception.RetryAfterSeconds.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            return Error(context, 429);
        }
        catch (DependencyUnavailableException)
        {
            return Error(context, 503);
        }
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
