using System.Globalization;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Http;

public static class PropertyEndpoint
{
    public const string ListPath =
        ApiConventions.Prefix + "/properties";

    public const string DetailPath =
        ApiConventions.Prefix + "/properties/{id}";

    public static void MapProperties(
        this WebApplication app)
    {
        app.MapGet(
                ListPath,
                ListAsync)
            .WithName("ListProperties")
            .WithTags("Properties")
            .WithSummary(
                "List public properties")
            .Produces(
                StatusCodes.Status200OK)
            .Produces(
                StatusCodes.Status304NotModified)
            .ProducesValidationProblem(
                StatusCodes.Status400BadRequest)
            .ProducesProblem(
                StatusCodes.Status429TooManyRequests)
            .ProducesProblem(
                StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(
                StatusCodes.Status500InternalServerError)
            .AllowAnonymous()
            .DisableRateLimiting();

        app.MapGet(
                DetailPath,
                DetailAsync)
            .WithName("GetProperty")
            .WithTags("Properties")
            .WithSummary(
                "Get public property details")
            .Produces(
                StatusCodes.Status200OK)
            .Produces(
                StatusCodes.Status304NotModified)
            .ProducesValidationProblem(
                StatusCodes.Status400BadRequest)
            .ProducesProblem(
                StatusCodes.Status404NotFound)
            .ProducesProblem(
                StatusCodes.Status429TooManyRequests)
            .ProducesProblem(
                StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(
                StatusCodes.Status500InternalServerError)
            .AllowAnonymous()
            .DisableRateLimiting();
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        PropertyQueryService properties,
        IRateLimitStore rateLimitStore,
        PropertyRateLimitOptions rateLimits)
    {
        Deprecate(context);
        try
        {
            await EnforceRateLimit(
                context,
                rateLimitStore,
                rateLimits);

            var page = ParseQueryInteger(
                context,
                "page",
                PropertyQueryValidation.DefaultPage);

            var pageSize = ParseQueryInteger(
                context,
                "pageSize",
                PropertyQueryValidation.DefaultPageSize);

            var result =
                await properties.ListAsync(
                    page,
                    pageSize,
                    context.RequestAborted);

            return HttpCaching.PublicJson(context, result);
        }
        catch (RequestValidationException)
        {
            return Error(context, 400);
        }
        catch (RateLimitExceededException exception)
        {
            context.Response.Headers.RetryAfter =
                exception.RetryAfterSeconds.ToString(
                    CultureInfo.InvariantCulture);

            return Error(context, 429);
        }
        catch (DependencyUnavailableException)
        {
            return Error(context, 503);
        }
    }

    private static async Task<IResult> DetailAsync(
        string id,
        HttpContext context,
        PropertyQueryService properties,
        IRateLimitStore rateLimitStore,
        PropertyRateLimitOptions rateLimits)
    {
        Deprecate(context);
        try
        {
            await EnforceRateLimit(
                context,
                rateLimitStore,
                rateLimits);

            var result =
                await properties.GetByIdAsync(
                    id,
                    context.RequestAborted);

            return HttpCaching.PublicJson(context, result);
        }
        catch (RequestValidationException)
        {
            return Error(context, 400);
        }
        catch (NotFoundException)
        {
            return Error(context, 404);
        }
        catch (RateLimitExceededException exception)
        {
            context.Response.Headers.RetryAfter =
                exception.RetryAfterSeconds.ToString(
                    CultureInfo.InvariantCulture);

            return Error(context, 429);
        }
        catch (DependencyUnavailableException)
        {
            return Error(context, 503);
        }
    }

    // NV-15/NV-17 1.2.0: el catálogo de NV-07 queda obsoleto; su sucesor es
    // la búsqueda de cuartos de NV-14. Se retira en 1.3.0 de acuerdo con Mobile.
    private static void Deprecate(HttpContext context)
    {
        context.Response.Headers["Deprecation"] = "true";
        context.Response.Headers.Link = "</api/v1/rooms/search>; rel=\"successor-version\"";
    }

    private static int ParseQueryInteger(
        HttpContext context,
        string name,
        int defaultValue)
    {
        if (!context.Request.Query.TryGetValue(
                name,
                out var values))
        {
            return defaultValue;
        }

        if (values.Count != 1 ||
            string.IsNullOrWhiteSpace(values[0]) ||
            !int.TryParse(
                values[0],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new RequestValidationException(
                name,
                $"{name} must be an integer.");
        }

        return value;
    }

    private static async Task EnforceRateLimit(
        HttpContext context,
        IRateLimitStore store,
        PropertyRateLimitOptions options)
    {
        var ipAddress =
            NormalizeAddress(
                context.Connection.RemoteIpAddress);

        var decision =
            await store.ConsumeAsync(
                RateLimitKeys.PropertyIp(ipAddress),
                options.RequestsPerWindow,
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
        int status)
    {
        // Los errores nunca se cachean (NV-07-R08).
        context.Response.Headers.CacheControl = "no-store";

        return Results.Problem(
            ProblemResponses.Create(
                context,
                status,
                validation: status == 400));
    }
}
