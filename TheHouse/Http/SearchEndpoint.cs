using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Search;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Http;

// NV-14: búsqueda pública de cuartos y colonias (rate limit por IP y caché
// pública con ETag) y detalle del cuarto con sesión (RF-24).
public static class SearchEndpoint
{
    public const string RoomsPath = ApiConventions.Prefix + "/rooms";

    public const string PlacesPath = ApiConventions.Prefix + "/places";

    public static void MapSearch(this WebApplication app)
    {
        Public(app.MapGet(RoomsPath + "/search", SearchAsync), "SearchRooms", "Search published rooms near a university", typeof(RoomSearchResponse));
        Public(app.MapGet(RoomsPath + "/map", MapAsync), "MapRooms", "Room markers inside a bounding box", typeof(RoomMapResponse));
        Public(app.MapGet(RoomsPath + "/count", CountAsync), "CountRooms", "Count rooms within the budget", typeof(RoomCountResponse));
        Public(app.MapGet(PlacesPath + "/search", PlacesAsync), "SearchPlaces", "Neighborhoods and towns near a university", typeof(PlaceSearchResponse));

        app.MapGet(RoomsPath + "/{id}", DetailAsync)
            .WithName("GetRoom")
            .WithTags("Search")
            .WithSummary("Published room details (requires session)")
            .Produces<RoomDetailResponse>(200)
            .ProducesValidationProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(404)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .RequireTokenNotRevoked()
            .DisableRateLimiting();
    }

    public static bool IsSearch(HttpContext context) =>
        context.Request.Path.StartsWithSegments(RoomsPath, StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.StartsWithSegments(PlacesPath, StringComparison.OrdinalIgnoreCase);

    public static void Normalize(ProblemDetails problem, int status)
    {
        problem.Title = ProblemResponses.TitleFor(status);
        problem.Detail = status switch
        {
            400 => "Los filtros de la búsqueda no son válidos.",
            401 => "Inicia sesión para ver el detalle del cuarto.",
            404 => "El cuarto o la universidad no existe.",
            429 => "Se superó el límite de búsquedas.",
            503 => "La búsqueda no está disponible temporalmente.",
            504 => "La búsqueda no respondió a tiempo.",
            _ => "No fue posible completar la búsqueda.",
        };
    }

    private static void Public(RouteHandlerBuilder builder, string name, string summary, Type response)
    {
        builder.WithName(name).WithTags("Search").WithSummary(summary)
            .Produces(200, response)
            .Produces(304)
            .ProducesValidationProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .DisableRateLimiting();
    }

    private static Task<IResult> SearchAsync(HttpContext context, SearchService search, IRateLimitStore store, UserRateLimitOptions limits) =>
        PublicRun(context, store, limits, async () =>
        {
            var page = UserEndpointSupport.QueryInteger(context, "page", PropertyQueryValidation.DefaultPage);
            var pageSize = UserEndpointSupport.QueryInteger(context, "pageSize", PropertyQueryValidation.DefaultPageSize);
            return await search.SearchAsync(Criteria(context), page, pageSize, context.RequestAborted);
        });

    private static Task<IResult> MapAsync(HttpContext context, SearchService search, IRateLimitStore store, UserRateLimitOptions limits) =>
        PublicRun(context, store, limits, async () => await search.MapAsync(Criteria(context), context.RequestAborted));

    private static Task<IResult> CountAsync(HttpContext context, SearchService search, IRateLimitStore store, UserRateLimitOptions limits) =>
        PublicRun(context, store, limits, async () => await search.CountAsync(Criteria(context), context.RequestAborted));

    private static Task<IResult> PlacesAsync(HttpContext context, SearchService search, IRateLimitStore store, UserRateLimitOptions limits) =>
        PublicRun(context, store, limits, async () =>
            await search.PlacesAsync(UserEndpointSupport.QueryText(context, "q"), UserEndpointSupport.QueryText(context, "universityId"), context.RequestAborted));

    private static Task<IResult> DetailAsync(string id, HttpContext context, SearchService search, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await UserEndpointSupport.EnforceAsync(context, store, RateLimitKeys.RoomDetailUser(userId), limits.RoomDetailPerWindow, limits.Window, limits.RetryAfterSeconds);
            return Results.Json(await search.DetailAsync(id, context.RequestAborted), statusCode: 200);
        });

    private static SearchCriteria Criteria(HttpContext context) =>
        new()
        {
            UniversityId = UserEndpointSupport.QueryText(context, "universityId"),
            MinPrice = UserEndpointSupport.QueryText(context, "minPrice"),
            MaxPrice = UserEndpointSupport.QueryText(context, "maxPrice"),
            RoomType = UserEndpointSupport.QueryText(context, "roomType"),
            Bathroom = UserEndpointSupport.QueryText(context, "bathroom"),
            BuildingType = UserEndpointSupport.QueryText(context, "buildingType"),
            AllowedTenants = UserEndpointSupport.QueryText(context, "allowedTenants"),
            Services = UserEndpointSupport.QueryText(context, "services"),
            MaxDistanceKm = UserEndpointSupport.QueryText(context, "maxDistanceKm"),
            Sort = UserEndpointSupport.QueryText(context, "sort"),
            Bbox = UserEndpointSupport.QueryText(context, "bbox"),
        };

    private static async Task<IResult> PublicRun<T>(HttpContext context, IRateLimitStore store, UserRateLimitOptions limits, Func<Task<T>> action)
    {
        try
        {
            var address = context.Connection.RemoteIpAddress;
            if (address?.IsIPv4MappedToIPv6 == true)
            {
                address = address.MapToIPv4();
            }

            var decision = await store.ConsumeAsync(RateLimitKeys.SearchIp(address?.ToString() ?? "unknown"), limits.SearchPerWindow, limits.Window, context.RequestAborted);
            if (!decision.IsAllowed)
            {
                throw new RateLimitExceededException(limits.RetryAfterSeconds);
            }

            return HttpCaching.PublicJson(context, await action());
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
            context.Response.Headers.RetryAfter = exception.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            return Error(context, 429);
        }
        catch (DependencyUnavailableException)
        {
            return Error(context, 503);
        }
    }

    private static IResult Error(HttpContext context, int status)
    {
        context.Response.Headers.CacheControl = "no-store";
        return Results.Problem(ProblemResponses.Create(context, status, validation: status == 400));
    }
}
