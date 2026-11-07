using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Favorites;
using TheHouse.Application.UseCases;

namespace TheHouse.Http;

// NV-15 1.2.0: favoritos del usuario autenticado sobre cuartos publicados. Agregar y quitar son
// idempotentes (204); rate limit por usuario en Redis (fallo cerrado).
public static class FavoritesEndpoint
{
    public const string BasePath = ApiConventions.Prefix + "/users/me/favorites";

    public const string ItemPath = BasePath + "/{roomId}";

    public const string IdsPath = BasePath + "/ids";

    public static void MapFavorites(this WebApplication app)
    {
        app.MapPut(ItemPath, AddAsync)
            .WithName("AddFavorite")
            .WithTags("Favorites")
            .WithSummary("Add a property to the user's favorites (idempotent)")
            .Produces(204)
            .ProducesValidationProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .RequireTokenNotRevoked()
            .DisableRateLimiting();

        app.MapDelete(ItemPath, RemoveAsync)
            .WithName("RemoveFavorite")
            .WithTags("Favorites")
            .WithSummary("Remove a property from the user's favorites (idempotent)")
            .Produces(204)
            .ProducesValidationProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .RequireTokenNotRevoked()
            .DisableRateLimiting();

        app.MapGet(IdsPath, IdsAsync)
            .WithName("ListFavoriteIds")
            .WithTags("Favorites")
            .WithSummary("List the ids of the user's favorite properties")
            .Produces<FavoriteIdsResponse>(200)
            .ProducesProblem(401)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .RequireTokenNotRevoked()
            .DisableRateLimiting();

        app.MapGet(BasePath, ListAsync)
            .WithName("ListFavorites")
            .WithTags("Favorites")
            .WithSummary("List the user's favorite properties, newest first")
            .Produces<FavoriteListResponse>(200)
            .ProducesValidationProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .RequireTokenNotRevoked()
            .DisableRateLimiting();
    }

    private static Task<IResult> AddAsync(
        string roomId,
        HttpContext context,
        FavoriteService favorites,
        IRateLimitStore store,
        UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            await favorites.AddAsync(userId, roomId, context.RequestAborted);
            return Results.NoContent();
        });

    private static Task<IResult> RemoveAsync(
        string roomId,
        HttpContext context,
        FavoriteService favorites,
        IRateLimitStore store,
        UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            await favorites.RemoveAsync(userId, roomId, context.RequestAborted);
            return Results.NoContent();
        });

    private static Task<IResult> ListAsync(
        HttpContext context,
        FavoriteService favorites,
        IRateLimitStore store,
        UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var page = UserEndpointSupport.QueryInteger(context, "page", PropertyQueryValidation.DefaultPage);
            var pageSize = UserEndpointSupport.QueryInteger(context, "pageSize", PropertyQueryValidation.DefaultPageSize);
            var result = await favorites.ListAsync(userId, page, pageSize, context.RequestAborted);
            return Results.Json(result, statusCode: 200);
        });

    private static Task<IResult> IdsAsync(
        HttpContext context,
        FavoriteService favorites,
        IRateLimitStore store,
        UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var result = await favorites.ListIdsAsync(userId, context.RequestAborted);
            return Results.Json(result, statusCode: 200);
        });

    private static Task Enforce(
        HttpContext context,
        IRateLimitStore store,
        UserRateLimitOptions limits,
        string userId) =>
        UserEndpointSupport.EnforceAsync(
            context,
            store,
            RateLimitKeys.FavoritesUser(userId),
            limits.FavoritesPerWindow,
            limits.Window,
            limits.RetryAfterSeconds);
}
