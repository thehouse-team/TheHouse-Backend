using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Moderation;
using TheHouse.Application.Contracts.Reviews;
using TheHouse.Application.UseCases;

namespace TheHouse.Http;

// NV-19: reseñas verificadas. Todo con sesión (RF-24); lo ajeno responde 404;
// ocultar y mostrar solo con rol `admin` (NV-21).
public static class ReviewsEndpoint
{
    public const string RoomReviewsPath = ApiConventions.Prefix + "/rooms/{roomId}/reviews";

    public const string EligibilityPath = RoomReviewsPath + "/eligibility";

    public const string ReviewsPath = ApiConventions.Prefix + "/reviews";

    public const string ReviewPath = ReviewsPath + "/{id}";

    public const string MinePath = ApiConventions.Prefix + "/users/me/reviews";

    public const string HidePath = AdminEndpoint.BasePath + "/reviews/{id}/hide";

    public const string UnhidePath = AdminEndpoint.BasePath + "/reviews/{id}/unhide";

    public static bool IsReviews(HttpContext context)
    {
        var path = context.Request.Path;
        return path.StartsWithSegments(ReviewsPath, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments(MinePath, StringComparison.OrdinalIgnoreCase) ||
               (path.StartsWithSegments(ApiConventions.Prefix + "/rooms", StringComparison.OrdinalIgnoreCase) &&
                path.Value?.Contains("/reviews", StringComparison.OrdinalIgnoreCase) == true);
    }

    public static void MapReviews(this WebApplication app)
    {
        Read(app.MapGet(EligibilityPath, EligibilityAsync), "GetReviewEligibility", "Can I review this room?")
            .Produces<ReviewEligibilityResponse>(200).ProducesProblem(404);
        Write(app.MapPost(RoomReviewsPath, CreateAsync), "CreateReview", "Review a room I visited or rented")
            .Accepts<ReviewInput>("application/json")
            .Produces<ReviewResponse>(201).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        Read(app.MapGet(RoomReviewsPath, ListAsync), "ListRoomReviews", "Published reviews of a room with averages")
            .Produces<RoomReviewListResponse>(200).ProducesProblem(404);
        Write(app.MapPatch(ReviewPath, UpdateAsync), "UpdateReview", "Edit my review within 30 days")
            .Accepts<ReviewInput>("application/json")
            .Produces<ReviewResponse>(200).ProducesProblem(404).ProducesProblem(409);
        Read(app.MapGet(MinePath, ListMineAsync), "ListMyReviews", "My reviews with their status")
            .Produces<MyReviewListResponse>(200);
        Write(app.MapPost(HidePath, (string id, HttpContext c, ReviewModerationService m, IRateLimitStore s, UserRateLimitOptions l) => ModerateAsync(id, hide: true, c, m, s, l)), "HideReview", "Hide a review (admin)")
            .Accepts<ReasonRequest>("application/json")
            .Produces<ReviewStatusResponse>(200).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        Write(app.MapPost(UnhidePath, (string id, HttpContext c, ReviewModerationService m, IRateLimitStore s, UserRateLimitOptions l) => ModerateAsync(id, hide: false, c, m, s, l)), "UnhideReview", "Show a hidden review again (admin)")
            .Accepts<ReasonRequest>("application/json")
            .Produces<ReviewStatusResponse>(200).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
    }

    private static RouteHandlerBuilder Read(RouteHandlerBuilder builder, string name, string summary) =>
        builder.WithName(name).WithTags("Reviews").WithSummary(summary)
            .ProducesValidationProblem(400)
            .ProducesProblem(401).ProducesProblem(429).ProducesProblem(503).ProducesProblem(500)
            .AllowAnonymous().RequireTokenNotRevoked().DisableRateLimiting();

    private static RouteHandlerBuilder Write(RouteHandlerBuilder builder, string name, string summary) =>
        Read(builder, name, summary).ProducesProblem(413).ProducesProblem(415);

    private static Task<IResult> EligibilityAsync(string roomId, HttpContext context, RoomReviewService reviews, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await reviews.EligibilityAsync(userId, roomId, context.RequestAborted));
        });

    private static Task<IResult> CreateAsync(string roomId, HttpContext context, RoomReviewService reviews, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var body = await UserEndpointSupport.ReadJsonAsync<ReviewInput>(context, optional: false, maximumBytes: 8192);
            await UserEndpointSupport.EnforceAsync(
                context, store, RateLimitKeys.ReviewCreatesUser(userId), limits.ReviewCreatesPerDay, limits.CreateWindow, limits.CreateRetryAfterSeconds);
            return Results.Json(await reviews.CreateAsync(userId, roomId, body!, context.RequestAborted), statusCode: 201);
        });

    private static Task<IResult> ListAsync(string roomId, HttpContext context, RoomReviewService reviews, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await reviews.ListAsync(
                roomId,
                UserEndpointSupport.QueryInteger(context, "page", PropertyQueryValidation.DefaultPage),
                UserEndpointSupport.QueryInteger(context, "pageSize", PropertyQueryValidation.DefaultPageSize),
                context.RequestAborted));
        });

    private static Task<IResult> UpdateAsync(string id, HttpContext context, RoomReviewService reviews, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var body = await UserEndpointSupport.ReadJsonAsync<ReviewInput>(context, optional: false, maximumBytes: 8192);
            return Results.Json(await reviews.UpdateAsync(userId, id, body!, context.RequestAborted));
        });

    private static Task<IResult> ListMineAsync(HttpContext context, RoomReviewService reviews, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await reviews.ListMineAsync(
                userId,
                UserEndpointSupport.QueryInteger(context, "page", PropertyQueryValidation.DefaultPage),
                UserEndpointSupport.QueryInteger(context, "pageSize", PropertyQueryValidation.DefaultPageSize),
                context.RequestAborted));
        });

    private static Task<IResult> ModerateAsync(string id, bool hide, HttpContext context, ReviewModerationService moderation, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await UserEndpointSupport.EnforceAsync(context, store, RateLimitKeys.AdminUser(userId), limits.AdminPerWindow, limits.Window, limits.RetryAfterSeconds);
            var body = (await UserEndpointSupport.ReadJsonAsync<ReasonRequest>(context, optional: false))!;
            return Results.Json(hide
                ? await moderation.HideAsync(userId, id, body, context.RequestAborted)
                : await moderation.UnhideAsync(userId, id, body, context.RequestAborted));
        });

    private static Task Enforce(HttpContext context, IRateLimitStore store, UserRateLimitOptions limits, string userId) =>
        UserEndpointSupport.EnforceAsync(
            context, store, RateLimitKeys.ReviewsUser(userId), limits.ReviewsPerWindow, limits.Window, limits.RetryAfterSeconds);
}
