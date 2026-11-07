using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text.Json;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Http;

// NV-15/NV-17: piezas comunes de los endpoints del usuario autenticado.
// El usuario sale siempre del `sub` validado por TokenRevocationMiddleware;
// nunca de la ruta, la query ni el cuerpo.
public static class UserEndpointSupport
{
    public const int MaximumBodyBytes = 2048;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            MaxDepth = 4,
        };

    public static async Task<IResult> RunAsync(
        HttpContext context,
        Func<string, Task<IResult>> action)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";

        var userId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Error(context, 401);
        }

        try
        {
            return await action(userId);
        }
        catch (Exception exception) when (exception is RequestValidationException or JsonException)
        {
            return Error(context, 400);
        }
        catch (UnsupportedBodyException exception)
        {
            return Error(context, exception.Status);
        }
        catch (NotFoundException)
        {
            return Error(context, 404);
        }
        catch (ConflictException exception)
        {
            return Error(context, 409, exception.Reason);
        }
        catch (BusinessRuleException exception)
        {
            return Error(context, 422, exception.Rule);
        }
        catch (RateLimitExceededException exception)
        {
            context.Response.Headers.RetryAfter =
                exception.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            return Error(context, 429);
        }
        catch (DependencyUnavailableException)
        {
            return Error(context, 503);
        }
    }

    public static async Task EnforceAsync(
        HttpContext context,
        IRateLimitStore store,
        string key,
        int limit,
        TimeSpan window,
        int retryAfterSeconds)
    {
        var decision = await store.ConsumeAsync(key, limit, window, context.RequestAborted);
        if (!decision.IsAllowed)
        {
            throw new RateLimitExceededException(retryAfterSeconds);
        }
    }

    public static int QueryInteger(HttpContext context, string name, int defaultValue)
    {
        if (!context.Request.Query.TryGetValue(name, out var values))
        {
            return defaultValue;
        }

        if (values.Count != 1 ||
            string.IsNullOrWhiteSpace(values[0]) ||
            !int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new RequestValidationException(name, $"{name} must be an integer.");
        }

        return value;
    }

    public static string? QueryText(HttpContext context, string name)
    {
        if (!context.Request.Query.TryGetValue(name, out var values))
        {
            return null;
        }

        if (values.Count != 1)
        {
            throw new RequestValidationException(name, $"{name} must appear once.");
        }

        return values[0];
    }

    // Lee un cuerpo JSON acotado. Con `optional`, un cuerpo vacío (sin
    // Content-Type) devuelve null; si hay cuerpo debe ser application/json.
    public static async Task<T?> ReadJsonAsync<T>(HttpContext context, bool optional)
        where T : class
    {
        var hasBody = context.Request.ContentLength is > 0 ||
            (context.Request.ContentLength is null && context.Request.Headers.TransferEncoding.Count > 0);

        if (!hasBody && optional && string.IsNullOrEmpty(context.Request.ContentType))
        {
            return null;
        }

        if (!MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var contentType) ||
            !string.Equals(contentType.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(contentType.CharSet) &&
             !string.Equals(contentType.CharSet.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase)))
        {
            throw new UnsupportedBodyException(415);
        }

        if (context.Request.ContentLength > MaximumBodyBytes)
        {
            throw new UnsupportedBodyException(413);
        }

        using var body = new MemoryStream();
        var buffer = new byte[512];
        while (true)
        {
            var read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted);
            if (read == 0)
            {
                break;
            }

            if (body.Length + read > MaximumBodyBytes)
            {
                throw new UnsupportedBodyException(413);
            }

            await body.WriteAsync(buffer.AsMemory(0, read), context.RequestAborted);
        }

        if (body.Length == 0)
        {
            return optional ? null : throw new RequestValidationException("body", "A JSON body is required.");
        }

        return JsonSerializer.Deserialize<T>(body.GetBuffer().AsSpan(0, (int)body.Length), JsonOptions)
            ?? throw new RequestValidationException("body", "A JSON object is required.");
    }

    public static IResult Error(HttpContext context, int status, string? code = null)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (code is not null)
        {
            // Código estable para que Mobile distinga el motivo del 409/422;
            // ProblemResponses.Normalize lo aplica (también al re-normalizar).
            context.Items[UserResourceProblems.CodeItem] = code;
        }

        return Results.Problem(ProblemResponses.Create(context, status, validation: status == 400));
    }

    public sealed class UnsupportedBodyException(int status) : Exception
    {
        public int Status { get; } = status;
    }
}
