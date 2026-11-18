using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Analytics;
using TheHouse.Application.UseCases;

namespace TheHouse.Http;

// NV-25 (RF-86): estadísticas del arrendador. Solo agregados de sus cuartos;
// el dueño sale del token y un edificio ajeno responde 404.
public static class LandlordStatsEndpoint
{
    public const string SummaryPath = ApiConventions.Prefix + "/landlord/stats";

    public const string RoomsPath = SummaryPath + "/rooms";

    public const string RecommendationsPath = SummaryPath + "/recommendations";

    public static bool IsStats(HttpContext context) =>
        context.Request.Path.StartsWithSegments(SummaryPath, StringComparison.OrdinalIgnoreCase);

    public static void MapLandlordStats(this WebApplication app)
    {
        Read(app.MapGet(SummaryPath, SummaryAsync), "GetLandlordStats", "Totals, previous period and daily series of my rooms")
            .Produces<LandlordStatsResponse>(200);
        Read(app.MapGet(RoomsPath, RoomsAsync), "GetLandlordRoomStats", "Stats per room, paginated")
            .Produces<RoomStatsListResponse>(200);
        Read(app.MapGet(RecommendationsPath, RecommendationsAsync), "GetLandlordRecommendations", "Rule-based tips to improve my listings")
            .Produces<StatsRecommendationsResponse>(200);
    }

    private static RouteHandlerBuilder Read(RouteHandlerBuilder builder, string name, string summary) =>
        builder.WithName(name).WithTags("LandlordStats").WithSummary(summary)
            .ProducesValidationProblem(400)
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(429).ProducesProblem(503).ProducesProblem(500)
            .AllowAnonymous().RequireTokenNotRevoked().DisableRateLimiting();

    private static Task<IResult> SummaryAsync(HttpContext context, LandlordStatsService stats, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await stats.SummaryAsync(
                userId,
                UserEndpointSupport.QueryText(context, "from"),
                UserEndpointSupport.QueryText(context, "to"),
                UserEndpointSupport.QueryText(context, "buildingId"),
                context.RequestAborted));
        });

    private static Task<IResult> RoomsAsync(HttpContext context, LandlordStatsService stats, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await stats.RoomsAsync(
                userId,
                UserEndpointSupport.QueryText(context, "from"),
                UserEndpointSupport.QueryText(context, "to"),
                UserEndpointSupport.QueryText(context, "buildingId"),
                UserEndpointSupport.QueryInteger(context, "page", PropertyQueryValidation.DefaultPage),
                UserEndpointSupport.QueryInteger(context, "pageSize", PropertyQueryValidation.DefaultPageSize),
                context.RequestAborted));
        });

    private static Task<IResult> RecommendationsAsync(HttpContext context, LandlordStatsService stats, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await stats.RecommendationsAsync(userId, UserEndpointSupport.QueryText(context, "buildingId"), context.RequestAborted));
        });

    private static Task Enforce(HttpContext context, IRateLimitStore store, UserRateLimitOptions limits, string userId) =>
        UserEndpointSupport.EnforceAsync(
            context, store, RateLimitKeys.LandlordStatsUser(userId), limits.LandlordStatsPerWindow, limits.Window, limits.RetryAfterSeconds);
}
