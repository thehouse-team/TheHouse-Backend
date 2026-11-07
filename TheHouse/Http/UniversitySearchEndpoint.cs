using System.Globalization;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Profile;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Http;

// NV-11 / RF-07: GET /universities/search, público. Rate limit por IP en
// Redis (fallo cerrado). La ruta literal `search` tiene prioridad sobre
// `/universities/{id}`. Las coordenadas no se registran ni se guardan; la
// respuesta por cercanía es no-store para que ningún caché compartido
// conserve la URL con la ubicación del usuario.
public static class UniversitySearchEndpoint
{
    public const string Path = ApiConventions.Prefix + "/universities/search";

    public static void MapUniversitySearch(this WebApplication app) =>
        app.MapGet(Path, SearchAsync)
            .WithName("SearchUniversities")
            .WithTags("Universities")
            .WithSummary("Search universities by acronym, name or city, or by proximity")
            .Produces<UniversitySearchResponse>(200)
            .Produces(304)
            .ProducesValidationProblem(400)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .DisableRateLimiting();

    private static async Task<IResult> SearchAsync(
        HttpContext context,
        UniversitySearchService search,
        IRateLimitStore store,
        UserRateLimitOptions limits)
    {
        try
        {
            var address = context.Connection.RemoteIpAddress;
            if (address?.IsIPv4MappedToIPv6 == true)
            {
                address = address.MapToIPv4();
            }

            var decision = await store.ConsumeAsync(
                RateLimitKeys.UniversitySearchIp(address?.ToString() ?? "unknown"),
                limits.UniversitySearchPerWindow,
                limits.Window,
                context.RequestAborted);

            if (!decision.IsAllowed)
            {
                throw new RateLimitExceededException(limits.RetryAfterSeconds);
            }

            var query = Single(context, "q");
            var latitude = Single(context, "lat");
            var longitude = Single(context, "lng");

            var result = await search.SearchAsync(query, latitude, longitude, context.RequestAborted);

            if (query is not null)
            {
                return HttpCaching.PublicJson(context, result);
            }

            context.Response.Headers.CacheControl = "no-store";
            return Results.Json(result, statusCode: 200);
        }
        catch (RequestValidationException)
        {
            return Error(context, 400);
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

    private static string? Single(HttpContext context, string name)
    {
        if (!context.Request.Query.TryGetValue(name, out var values))
        {
            return null;
        }

        return values.Count == 1
            ? values[0] ?? string.Empty
            : throw new RequestValidationException(name, $"{name} must appear once.");
    }

    private static IResult Error(HttpContext context, int status)
    {
        context.Response.Headers.CacheControl = "no-store";
        return Results.Problem(ProblemResponses.Create(context, status, validation: status == 400));
    }
}
