using System.Net.Http.Headers;
using System.Text.Json;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Auth;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Http;

public static class LoginEndpoint
{
    public const string Path =
        ApiConventions.Prefix + "/auth/login";

    public const int MaximumBodyBytes = 8192;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            MaxDepth = 8
        };

    public static void MapLogin(this WebApplication app) =>
        app.MapPost(Path, LoginAsync)
            .WithName("Login")
            .WithTags("Authentication")
            .WithSummary(
                "Authenticate using email and password")
            .Accepts<LoginRequest>("application/json")
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

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        LoginService login,
        IRateLimitStore rateLimitStore,
        RateLimitOptions rateLimits)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";

        try
        {
            var ipAddress =
                NormalizeAddress(
                    context.Connection.RemoteIpAddress);

            // Toda solicitud cuenta al menos contra el límite IP.
            await EnforceLimit(
                rateLimitStore,
                RateLimitKeys.LoginIp(ipAddress),
                rateLimits.LoginIpPerWindow,
                rateLimits,
                context);

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

            if (context.Request.ContentLength >
                MaximumBodyBytes)
            {
                return Error(context, 413);
            }

            using var body = new MemoryStream();
            var buffer = new byte[1024];

            while (true)
            {
                var read = await context.Request.Body.ReadAsync(
                    buffer,
                    context.RequestAborted);

                if (read == 0)
                {
                    break;
                }

                if (body.Length + read >
                    MaximumBodyBytes)
                {
                    return Error(context, 413);
                }

                await body.WriteAsync(
                    buffer.AsMemory(0, read),
                    context.RequestAborted);
            }

            var request =
                JsonSerializer.Deserialize<LoginRequest>(
                    body.GetBuffer().AsSpan(
                        0,
                        (int)body.Length),
                    JsonOptions);

            if (request is null)
            {
                return Error(context, 400);
            }

            var normalizedEmail =
                EmailNormalizer.Normalize(request.Email);

            // Incluye intentos válidos e inválidos cuando existe
            // un payload que permite obtener la clave de email.
            await EnforceLimit(
                rateLimitStore,
                RateLimitKeys.LoginEmail(
                    ipAddress,
                    normalizedEmail),
                rateLimits.LoginEmailPerWindow,
                rateLimits,
                context);

            var response =
                await login.LoginAsync(
                    request,
                    context.RequestAborted);

            return Results.Json(
                response,
                statusCode:
                    StatusCodes.Status200OK);
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
                    System.Globalization
                        .CultureInfo.InvariantCulture);

            return Error(context, 429);
        }
        catch (DependencyUnavailableException)
        {
            return Error(context, 503);
        }
    }

    private static async Task EnforceLimit(
        IRateLimitStore store,
        string key,
        int limit,
        RateLimitOptions options,
        HttpContext context)
    {
        var decision =
            await store.ConsumeAsync(
                key,
                limit,
                options.Window,
                context.RequestAborted);

        if (!decision.IsAllowed)
        {
            throw new RateLimitExceededException(
                options.RetryAfterSeconds);
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
