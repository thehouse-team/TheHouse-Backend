using System.Net.Http.Headers;
using System.Text.Json;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Auth;
using TheHouse.Application.UseCases;

namespace TheHouse.Http;

public static class RegistrationEndpoint
{
    public const string Path = ApiConventions.Prefix + "/auth/register";
    public const int MaximumBodyBytes = 8192;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { MaxDepth = 8 };

    public static void MapRegistration(this WebApplication app) => app.MapPost(Path, RegisterAsync)
        .WithName("Register").WithTags("Registration").WithSummary("Create an account using email and password")
        .Accepts<RegisterRequest>("application/json").Produces<RegisterResponse>(201)
        .ProducesValidationProblem(400).ProducesProblem(409).ProducesProblem(413).ProducesProblem(415)
        .ProducesProblem(429).ProducesProblem(503).ProducesProblem(500)
        .AllowAnonymous().DisableRateLimiting(); // La cuota Redis es autoritativa, no la base local NV-01.

    private static async Task<IResult> RegisterAsync(HttpContext context, RegistrationService registration,
        IRegistrationRateLimiter rateLimiter)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            var address = context.Connection.RemoteIpAddress;
            if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();
            if (!await rateLimiter.AllowAsync(address?.ToString() ?? "unknown", context.RequestAborted))
            {
                context.Response.Headers.RetryAfter = "60";
                return Error(context, 429);
            }

            if (!MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var contentType) ||
                !string.Equals(contentType.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(contentType.CharSet) &&
                 !string.Equals(contentType.CharSet.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase)))
                return Error(context, 415);

            if (context.Request.ContentLength > MaximumBodyBytes) return Error(context, 413);
            using var body = new MemoryStream();
            var buffer = new byte[1024];
            while (true)
            {
                var read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted);
                if (read == 0) break;
                if (body.Length + read > MaximumBodyBytes) return Error(context, 413);
                await body.WriteAsync(buffer.AsMemory(0, read), context.RequestAborted);
            }

            // DTO explícito, nunca se deserializa User ni UserDocument.
            var request = JsonSerializer.Deserialize<RegisterRequest>(body.GetBuffer().AsSpan(0, (int)body.Length), JsonOptions);
            if (request is null) return Error(context, 400);
            var response = await registration.RegisterAsync(request, context.RequestAborted);
            return Results.Json(response, statusCode: StatusCodes.Status201Created);
        }
        catch (Exception exception) when (exception is JsonException or RequestValidationException)
        {
            return Error(context, 400);
        }
        catch (EmailConflictException) { return Error(context, 409); }
        catch (DependencyUnavailableException) { return Error(context, 503); }
    }

    private static IResult Error(HttpContext context, int status) =>
        Results.Problem(ProblemResponses.Create(context, status, validation: status == 400));
}
