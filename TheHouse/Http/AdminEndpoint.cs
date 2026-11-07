using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Moderation;
using TheHouse.Application.UseCases;

namespace TheHouse.Http;

// NV-21: panel interno `/admin/*` (revisor o admin según la acción) y
// reportes de usuarios. El rol se comprueba en cada solicitud; sin rol →
// 403 `role_required` (OWASP API5). Rate limit por usuario en Redis.
public static class AdminEndpoint
{
    public const string BasePath = ApiConventions.Prefix + "/admin";

    public const string ReportsPath = ApiConventions.Prefix + "/reports";

    public static void MapAdmin(this WebApplication app)
    {
        Route(app.MapGet(BasePath + "/buildings", QueueAsync), "ListBuildingsForReview", "Review queue by status", 200);
        Route(app.MapGet(BasePath + "/buildings/{id}", DetailAsync), "GetBuildingForReview", "Building, rooms and landlord for review", 200);
        Route(app.MapPost(BasePath + "/buildings/{id}/approve", (string id, HttpContext c, ReviewService r, IRateLimitStore s, UserRateLimitOptions l) => DecideAsync(id, "approved", c, r, s, l)), "ApproveBuilding", "Approve a building in review", 200);
        Route(app.MapPost(BasePath + "/buildings/{id}/request-changes", (string id, HttpContext c, ReviewService r, IRateLimitStore s, UserRateLimitOptions l) => DecideAsync(id, "changes_requested", c, r, s, l)), "RequestBuildingChanges", "Send a building back to draft with a reason", 200);
        Route(app.MapPost(BasePath + "/buildings/{id}/reject", (string id, HttpContext c, ReviewService r, IRateLimitStore s, UserRateLimitOptions l) => DecideAsync(id, "rejected", c, r, s, l)), "RejectBuilding", "Reject a building with a reason", 200);
        Route(app.MapPost(BasePath + "/buildings/{id}/suspend", (string id, HttpContext c, ModerationService m, IRateLimitStore s, UserRateLimitOptions l) => BuildingStateAsync(id, true, c, m, s, l)), "SuspendBuilding", "Suspend a verified building", 200);
        Route(app.MapPost(BasePath + "/buildings/{id}/unsuspend", (string id, HttpContext c, ModerationService m, IRateLimitStore s, UserRateLimitOptions l) => BuildingStateAsync(id, false, c, m, s, l)), "UnsuspendBuilding", "Reactivate a suspended building", 200);
        Route(app.MapPost(BasePath + "/users/{id}/suspend", (string id, HttpContext c, ModerationService m, IRateLimitStore s, UserRateLimitOptions l) => UserStateAsync(id, true, c, m, s, l)), "SuspendUser", "Suspend an account", 200);
        Route(app.MapPost(BasePath + "/users/{id}/unsuspend", (string id, HttpContext c, ModerationService m, IRateLimitStore s, UserRateLimitOptions l) => UserStateAsync(id, false, c, m, s, l)), "UnsuspendUser", "Reactivate an account", 200);
        Route(app.MapGet(BasePath + "/reports", ReportsAsync), "ListReports", "Reports by status", 200);
        Route(app.MapPost(BasePath + "/reports/{id}/resolve", ResolveAsync), "ResolveReport", "Resolve a report with an action", 200);
        Route(app.MapGet(BasePath + "/audit", AuditAsync), "ListAuditEvents", "Audit log", 200);

        app.MapPost(ReportsPath, CreateReportAsync)
            .WithName("CreateReport")
            .WithTags("Reports")
            .WithSummary("Report a building, room or account")
            .Accepts<CreateReportRequest>("application/json")
            .Produces<ReportCreatedResponse>(201)
            .ProducesValidationProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(413)
            .ProducesProblem(415)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .RequireTokenNotRevoked()
            .DisableRateLimiting();
    }

    private static void Route(RouteHandlerBuilder builder, string name, string summary, int success)
    {
        builder.WithName(name).WithTags("Admin").WithSummary(summary).Produces(success).ProducesValidationProblem(400);
        foreach (var status in new[] { 401, 403, 404, 409, 413, 415, 429, 503, 500 })
        {
            builder.ProducesProblem(status);
        }

        builder.AllowAnonymous().RequireTokenNotRevoked().DisableRateLimiting();
    }

    private static Task<IResult> QueueAsync(HttpContext context, ReviewService reviews, IRateLimitStore store, UserRateLimitOptions limits) =>
        Admin(context, store, limits, async userId =>
        {
            var page = UserEndpointSupport.QueryInteger(context, "page", PropertyQueryValidation.DefaultPage);
            var pageSize = UserEndpointSupport.QueryInteger(context, "pageSize", PropertyQueryValidation.DefaultPageSize);
            var status = UserEndpointSupport.QueryText(context, "status");
            return Results.Json(await reviews.QueueAsync(userId, status, page, pageSize, context.RequestAborted), statusCode: 200);
        });

    private static Task<IResult> DetailAsync(string id, HttpContext context, ReviewService reviews, IRateLimitStore store, UserRateLimitOptions limits) =>
        Admin(context, store, limits, async userId =>
            Results.Json(await reviews.DetailAsync(userId, id, context.RequestAborted), statusCode: 200));

    private static Task<IResult> DecideAsync(string id, string decision, HttpContext context, ReviewService reviews, IRateLimitStore store, UserRateLimitOptions limits) =>
        Admin(context, store, limits, async userId =>
        {
            var request = (await UserEndpointSupport.ReadJsonAsync<DecisionRequest>(context, optional: false))!;
            return Results.Json(await reviews.DecideAsync(userId, id, decision, request, context.RequestAborted), statusCode: 200);
        });

    private static Task<IResult> BuildingStateAsync(string id, bool suspend, HttpContext context, ModerationService moderation, IRateLimitStore store, UserRateLimitOptions limits) =>
        Admin(context, store, limits, async userId =>
        {
            var request = (await UserEndpointSupport.ReadJsonAsync<ReasonRequest>(context, optional: false))!;
            var status = suspend
                ? await moderation.SuspendBuildingAsync(userId, id, request, context.RequestAborted)
                : await moderation.UnsuspendBuildingAsync(userId, id, request, context.RequestAborted);
            return Results.Json(new { id, status }, statusCode: 200);
        });

    private static Task<IResult> UserStateAsync(string id, bool suspend, HttpContext context, ModerationService moderation, IRateLimitStore store, UserRateLimitOptions limits) =>
        Admin(context, store, limits, async userId =>
        {
            var request = (await UserEndpointSupport.ReadJsonAsync<ReasonRequest>(context, optional: false))!;
            var result = suspend
                ? await moderation.SuspendUserAsync(userId, id, request, context.RequestAborted)
                : await moderation.UnsuspendUserAsync(userId, id, request, context.RequestAborted);
            return Results.Json(result, statusCode: 200);
        });

    private static Task<IResult> ReportsAsync(HttpContext context, ReportService reports, IRateLimitStore store, UserRateLimitOptions limits) =>
        Admin(context, store, limits, async userId =>
        {
            var page = UserEndpointSupport.QueryInteger(context, "page", PropertyQueryValidation.DefaultPage);
            var pageSize = UserEndpointSupport.QueryInteger(context, "pageSize", PropertyQueryValidation.DefaultPageSize);
            var status = UserEndpointSupport.QueryText(context, "status");
            return Results.Json(await reports.ListAsync(userId, status, page, pageSize, context.RequestAborted), statusCode: 200);
        });

    private static Task<IResult> ResolveAsync(string id, HttpContext context, ReportService reports, IRateLimitStore store, UserRateLimitOptions limits) =>
        Admin(context, store, limits, async userId =>
        {
            var request = (await UserEndpointSupport.ReadJsonAsync<ResolveReportRequest>(context, optional: false))!;
            return Results.Json(await reports.ResolveAsync(userId, id, request, context.RequestAborted), statusCode: 200);
        });

    private static Task<IResult> AuditAsync(HttpContext context, StaffService staff, IRateLimitStore store, UserRateLimitOptions limits) =>
        Admin(context, store, limits, async userId =>
        {
            var page = UserEndpointSupport.QueryInteger(context, "page", PropertyQueryValidation.DefaultPage);
            var pageSize = UserEndpointSupport.QueryInteger(context, "pageSize", PropertyQueryValidation.DefaultPageSize);
            return Results.Json(await staff.AuditLogAsync(userId, page, pageSize, context.RequestAborted), statusCode: 200);
        });

    private static Task<IResult> CreateReportAsync(HttpContext context, ReportService reports, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await UserEndpointSupport.EnforceAsync(context, store, RateLimitKeys.ReportsUser(userId), limits.ReportsPerWindow, limits.Window, limits.RetryAfterSeconds);
            await UserEndpointSupport.EnforceAsync(context, store, RateLimitKeys.ReportCreatesUser(userId), limits.ReportCreatesPerDay, limits.CreateWindow, limits.CreateRetryAfterSeconds);
            var request = (await UserEndpointSupport.ReadJsonAsync<CreateReportRequest>(context, optional: false))!;
            return Results.Json(await reports.CreateAsync(userId, request, context.RequestAborted), statusCode: 201);
        });

    private static Task<IResult> Admin(HttpContext context, IRateLimitStore store, UserRateLimitOptions limits, Func<string, Task<IResult>> action) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await UserEndpointSupport.EnforceAsync(context, store, RateLimitKeys.AdminUser(userId), limits.AdminPerWindow, limits.Window, limits.RetryAfterSeconds);
            return await action(userId);
        });
}
