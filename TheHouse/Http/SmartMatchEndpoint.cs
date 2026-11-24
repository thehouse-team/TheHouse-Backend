using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.SmartMatch;
using TheHouse.Application.UseCases;

namespace TheHouse.Http;

// NV-23 1.2.0: Smart Match con el modelo base de TheHouse-ML. Puntajes con o
// sin sesión, recomendaciones, retroalimentación, descartes y métricas para el
// admin (RF-66). Respuestas `no-store`; rate limit en Redis (fallo cerrado).
public static class SmartMatchEndpoint
{
    public const string BasePath = ApiConventions.Prefix + "/smart-match";

    public const string ScoresPath = BasePath + "/scores";

    public const string RecommendationsPath = BasePath + "/recommendations";

    public const string QueryPath = RecommendationsPath + "/query";

    public const string FeedbackPath = BasePath + "/feedback";

    public const string DismissalsPath = BasePath + "/dismissals";

    public const string DismissalPath = DismissalsPath + "/{roomId}";

    public const string MetricsPath = AdminEndpoint.BasePath + "/smart-match/metrics";

    // NV-24 1.2.1: explicaciones del puntaje.
    public const string ExplanationsPath = BasePath + "/explanations";

    public const string ExplanationPath = ExplanationsPath + "/{roomId}";

    public const string ExplanationQueryPath = ExplanationsPath + "/query";

    // 50 ids y las preferencias de un invitado caben con holgura.
    private const int MaximumBodyBytes = 8192;

    public static bool IsSmartMatch(HttpContext context) =>
        context.Request.Path.StartsWithSegments(BasePath, StringComparison.OrdinalIgnoreCase);

    public static void MapSmartMatch(this WebApplication app)
    {
        Route(app.MapPost(ScoresPath, ScoresAsync), "ScoreRooms", "Compatibility of up to 50 published rooms (session optional)", optionalToken: true)
            .Accepts<SmartMatchScoresRequest>("application/json")
            .Produces<SmartMatchScoresResponse>(200);
        Route(app.MapGet(RecommendationsPath, RecommendationsAsync), "ListRecommendations", "Published rooms ordered by compatibility with the saved preferences")
            .Produces<SmartMatchRecommendationsResponse>(200);
        Route(app.MapPost(QueryPath, QueryAsync), "QueryRecommendations", "Recommendations for a guest with preferences in the body", optionalToken: true)
            .Accepts<SmartMatchQueryRequest>("application/json")
            .Produces<SmartMatchRecommendationsResponse>(200);
        Route(app.MapPost(FeedbackPath, FeedbackAsync), "SendSmartMatchFeedback", "Mark a recommendation as useful or not useful")
            .Accepts<SmartMatchFeedbackRequest>("application/json")
            .Produces(204);
        Route(app.MapPost(DismissalsPath, DismissAsync), "DismissRoom", "Move a room to the end of the recommendations for 30 days")
            .Accepts<SmartMatchDismissalRequest>("application/json")
            .Produces(204);
        Route(app.MapDelete(DismissalPath, UndismissAsync), "UndoDismissal", "Undo a dismissal (idempotent)")
            .Produces(204);

        Route(app.MapGet(ExplanationPath, ExplainAsync), "ExplainRoom", "Why a room has its compatibility, with the saved preferences")
            .Produces<SmartMatchExplanationResponse>(200);
        Route(app.MapPost(ExplanationQueryPath, ExplainQueryAsync), "ExplainRoomForGuest", "Explanation for a guest with preferences in the body", optionalToken: true)
            .Accepts<SmartMatchExplanationQueryRequest>("application/json")
            .Produces<SmartMatchExplanationResponse>(200);

        app.MapGet(MetricsPath, MetricsAsync)
            .WithName("GetSmartMatchMetrics")
            .WithTags("Admin")
            .WithSummary("Basic Smart Match metrics for admins (RF-66)")
            .Produces<SmartMatchMetricsResponse>(200)
            .ProducesValidationProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .RequireTokenNotRevoked()
            .DisableRateLimiting();
    }

    private static RouteHandlerBuilder Route(RouteHandlerBuilder builder, string name, string summary, bool optionalToken = false)
    {
        builder.WithName(name).WithTags("SmartMatch").WithSummary(summary).ProducesValidationProblem(400);
        foreach (var status in new[] { 401, 404, 413, 415, 422, 429, 503, 500 })
        {
            builder.ProducesProblem(status);
        }

        builder.AllowAnonymous().DisableRateLimiting();
        return optionalToken ? builder.AllowOptionalToken() : builder.RequireTokenNotRevoked();
    }

    private static Task<IResult> ScoresAsync(HttpContext context, SmartMatchService smartMatch, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunOptionalAsync(context, async userId =>
        {
            await EnforceAsync(context, store, limits, userId);
            var request = (await UserEndpointSupport.ReadJsonAsync<SmartMatchScoresRequest>(context, optional: false, MaximumBodyBytes))!;
            return Results.Json(await smartMatch.ScoresAsync(userId, request, context.RequestAborted), statusCode: 200);
        });

    private static Task<IResult> RecommendationsAsync(HttpContext context, SmartMatchService smartMatch, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await EnforceAsync(context, store, limits, userId);
            var (page, pageSize) = Page(context);
            return Results.Json(await smartMatch.RecommendationsAsync(userId, page, pageSize, context.RequestAborted), statusCode: 200);
        });

    // Público: siempre con el límite por IP, aunque llegue un token.
    private static Task<IResult> QueryAsync(HttpContext context, SmartMatchService smartMatch, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunOptionalAsync(context, async _ =>
        {
            await EnforceAsync(context, store, limits, null);
            var (page, pageSize) = Page(context);
            var request = (await UserEndpointSupport.ReadJsonAsync<SmartMatchQueryRequest>(context, optional: false, MaximumBodyBytes))!;
            return Results.Json(await smartMatch.QueryAsync(request, page, pageSize, context.RequestAborted), statusCode: 200);
        });

    private static Task<IResult> FeedbackAsync(HttpContext context, SmartMatchService smartMatch, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await EnforceAsync(context, store, limits, userId);
            var request = (await UserEndpointSupport.ReadJsonAsync<SmartMatchFeedbackRequest>(context, optional: false))!;
            await smartMatch.FeedbackAsync(userId, request, context.RequestAborted);
            return Results.NoContent();
        });

    private static Task<IResult> DismissAsync(HttpContext context, SmartMatchService smartMatch, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await EnforceAsync(context, store, limits, userId);
            var request = (await UserEndpointSupport.ReadJsonAsync<SmartMatchDismissalRequest>(context, optional: false))!;
            await smartMatch.DismissAsync(userId, request, context.RequestAborted);
            return Results.NoContent();
        });

    private static Task<IResult> UndismissAsync(string roomId, HttpContext context, SmartMatchService smartMatch, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await EnforceAsync(context, store, limits, userId);
            await smartMatch.UndismissAsync(userId, roomId, context.RequestAborted);
            return Results.NoContent();
        });

    private static Task<IResult> MetricsAsync(HttpContext context, SmartMatchService smartMatch, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await UserEndpointSupport.EnforceAsync(context, store, RateLimitKeys.SmartMatchMetricsUser(userId), limits.SmartMatchMetricsPerWindow, limits.Window, limits.RetryAfterSeconds);
            var metrics = await smartMatch.MetricsAsync(
                userId,
                UserEndpointSupport.QueryText(context, "from"),
                UserEndpointSupport.QueryText(context, "to"),
                context.RequestAborted);
            return Results.Json(metrics, statusCode: 200);
        });

    private static Task<IResult> ExplainAsync(string roomId, HttpContext context, SmartMatchExplanationService explanations, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await UserEndpointSupport.EnforceAsync(context, store, RateLimitKeys.ExplanationsUser(userId), limits.ExplanationsPerWindow, limits.Window, limits.RetryAfterSeconds);
            return Results.Json(await explanations.ExplainAsync(userId, roomId, context.TraceIdentifier, context.RequestAborted), statusCode: 200);
        });

    // Público: siempre con el límite por IP, aunque llegue un token.
    private static Task<IResult> ExplainQueryAsync(HttpContext context, SmartMatchExplanationService explanations, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunOptionalAsync(context, async _ =>
        {
            await UserEndpointSupport.EnforceAsync(context, store, RateLimitKeys.ExplanationsIp(ClientIp(context)), limits.ExplanationsGuestPerWindow, limits.Window, limits.RetryAfterSeconds);
            var request = (await UserEndpointSupport.ReadJsonAsync<SmartMatchExplanationQueryRequest>(context, optional: false, MaximumBodyBytes))!;
            return Results.Json(await explanations.QueryAsync(request, context.TraceIdentifier, context.RequestAborted), statusCode: 200);
        });

    private static string ClientIp(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address?.IsIPv4MappedToIPv6 == true)
        {
            address = address.MapToIPv4();
        }

        return address?.ToString() ?? "unknown";
    }

    private static (int Page, int PageSize) Page(HttpContext context) =>
        (UserEndpointSupport.QueryInteger(context, "page", PropertyQueryValidation.DefaultPage),
         UserEndpointSupport.QueryInteger(context, "pageSize", PropertyQueryValidation.DefaultPageSize));

    // Con sesión: 60/min por usuario; sin sesión: 30/min por IP.
    private static Task EnforceAsync(HttpContext context, IRateLimitStore store, UserRateLimitOptions limits, string? userId)
    {
        if (userId is not null)
        {
            return UserEndpointSupport.EnforceAsync(context, store, RateLimitKeys.SmartMatchUser(userId), limits.SmartMatchPerWindow, limits.Window, limits.RetryAfterSeconds);
        }

        var address = context.Connection.RemoteIpAddress;
        if (address?.IsIPv4MappedToIPv6 == true)
        {
            address = address.MapToIPv4();
        }

        return UserEndpointSupport.EnforceAsync(context, store, RateLimitKeys.SmartMatchIp(address?.ToString() ?? "unknown"), limits.SmartMatchGuestPerWindow, limits.Window, limits.RetryAfterSeconds);
    }
}
