using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Roommates;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Http;

// NV-22: perfil de compañero, lista recíproca de la misma universidad y
// contacto por el chat de NV-16. Todo con sesión; nunca ids de usuario.
public static class RoommatesEndpoint
{
    public const string MinePath = ApiConventions.Prefix + "/users/me/roommate-profile";

    public const string ListPath = ApiConventions.Prefix + "/roommates";

    public const string DetailPath = ListPath + "/{id}";

    public const string ContactPath = DetailPath + "/contact";

    public static bool IsRoommates(HttpContext context) =>
        context.Request.Path.StartsWithSegments(MinePath, StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.StartsWithSegments(ListPath, StringComparison.OrdinalIgnoreCase);

    public static void MapRoommates(this WebApplication app)
    {
        Write(app.MapPut(MinePath, UpsertAsync), "UpsertRoommateProfile", "Create or update my roommate profile")
            .Accepts<RoommateProfileInput>("application/json")
            .Produces<MyRoommateProfileResponse>(200).Produces<MyRoommateProfileResponse>(201).ProducesProblem(409);
        Read(app.MapGet(MinePath, GetMineAsync), "GetRoommateProfile", "My roommate profile")
            .Produces<MyRoommateProfileResponse>(200).ProducesProblem(404);
        Write(app.MapDelete(MinePath, DeleteMineAsync), "DeleteRoommateProfile", "Remove my roommate profile")
            .Produces(204);
        Read(app.MapGet(ListPath, ListAsync), "ListRoommates", "Students of my university looking for a roommate, by matches")
            .Produces<RoommateListResponse>(200).ProducesProblem(403);
        Read(app.MapGet(DetailPath, GetAsync), "GetRoommate", "A roommate card")
            .Produces<RoommateCard>(200).ProducesProblem(403).ProducesProblem(404);
        Write(app.MapPost(ContactPath, ContactAsync), "ContactRoommate", "Open or reuse the roommate conversation with a message")
            .Accepts<ContactRoommateRequest>("application/json")
            .Produces<ContactRoommateResponse>(201).Produces<ContactRoommateResponse>(200).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
    }

    private static RouteHandlerBuilder Read(RouteHandlerBuilder builder, string name, string summary) =>
        builder.WithName(name).WithTags("Roommates").WithSummary(summary)
            .ProducesValidationProblem(400)
            .ProducesProblem(401).ProducesProblem(429).ProducesProblem(503).ProducesProblem(500)
            .AllowAnonymous().RequireTokenNotRevoked().DisableRateLimiting();

    private static RouteHandlerBuilder Write(RouteHandlerBuilder builder, string name, string summary) =>
        Read(builder, name, summary).ProducesProblem(413).ProducesProblem(415);

    private static Task<IResult> UpsertAsync(HttpContext context, RoommateService roommates, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var body = await UserEndpointSupport.ReadJsonAsync<RoommateProfileInput>(context, optional: false);
            var (profile, created) = await roommates.UpsertAsync(userId, body!, context.RequestAborted);
            return Results.Json(profile, statusCode: created ? 201 : 200);
        });

    private static Task<IResult> GetMineAsync(HttpContext context, RoommateService roommates, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await roommates.GetMineAsync(userId, context.RequestAborted));
        });

    private static Task<IResult> DeleteMineAsync(HttpContext context, RoommateService roommates, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            await roommates.DeleteMineAsync(userId, context.RequestAborted);
            return Results.NoContent();
        });

    private static Task<IResult> ListAsync(HttpContext context, RoommateService roommates, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var budget = UserEndpointSupport.QueryText(context, "budgetMax");
            var hasPlace = UserEndpointSupport.QueryText(context, "hasPlace");
            var filter = new RoommateFilter(
                budget is null ? null : UserEndpointSupport.QueryInteger(context, "budgetMax", 0),
                UserEndpointSupport.QueryText(context, "zone"),
                UserEndpointSupport.QueryText(context, "moveInFrom"),
                UserEndpointSupport.QueryText(context, "moveInTo"),
                hasPlace switch
                {
                    null => null,
                    "true" => true,
                    "false" => false,
                    _ => throw new RequestValidationException("hasPlace", "hasPlace must be true or false."),
                });
            return Results.Json(await roommates.ListAsync(
                userId,
                filter,
                UserEndpointSupport.QueryInteger(context, "page", PropertyQueryValidation.DefaultPage),
                UserEndpointSupport.QueryInteger(context, "pageSize", PropertyQueryValidation.DefaultPageSize),
                context.RequestAborted));
        });

    private static Task<IResult> GetAsync(string id, HttpContext context, RoommateService roommates, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await roommates.GetAsync(userId, id, context.RequestAborted));
        });

    private static Task<IResult> ContactAsync(string id, HttpContext context, RoommateService roommates, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var body = await UserEndpointSupport.ReadJsonAsync<ContactRoommateRequest>(context, optional: false, maximumBytes: 4096);
            await UserEndpointSupport.EnforceAsync(
                context, store, RateLimitKeys.RoommateContactsUser(userId), limits.RoommateContactsPerDay, limits.CreateWindow, limits.CreateRetryAfterSeconds);
            var (response, created) = await roommates.ContactAsync(userId, id, body!, context.RequestAborted);
            return Results.Json(response, statusCode: created ? 201 : 200);
        });

    private static Task Enforce(HttpContext context, IRateLimitStore store, UserRateLimitOptions limits, string userId) =>
        UserEndpointSupport.EnforceAsync(
            context, store, RateLimitKeys.RoommatesUser(userId), limits.RoommatesPerWindow, limits.Window, limits.RetryAfterSeconds);
}
