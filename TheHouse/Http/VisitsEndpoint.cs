using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Visits;
using TheHouse.Application.UseCases;

namespace TheHouse.Http;

// NV-17: visitas v1 del estudiante. Crear exige `Idempotency-Key`; una
// visita ajena responde 404 (BOLA). Rate limit por usuario en Redis.
public static class VisitsEndpoint
{
    public const string BasePath = ApiConventions.Prefix + "/visits";

    public const string DetailPath = BasePath + "/{id}";

    public const string CancelPath = BasePath + "/{id}/cancel";

    public const string MinePath = ApiConventions.Prefix + "/users/me/visits";

    // NV-17 1.3.0: calendario y acciones del arrendador.
    public const string LandlordPath = ApiConventions.Prefix + "/landlord/visits";

    public const string LandlordDetailPath = LandlordPath + "/{id}";

    public const string LandlordConfirmPath = LandlordPath + "/{id}/confirm";

    public const string LandlordReschedulePath = LandlordPath + "/{id}/reschedule";

    public const string LandlordCancelPath = LandlordPath + "/{id}/cancel";

    public const string IdempotencyHeader = "Idempotency-Key";

    public const string ReplayedHeader = "Idempotent-Replayed";

    public static void MapVisits(this WebApplication app)
    {
        app.MapPost(BasePath, CreateAsync)
            .WithName("RequestVisit")
            .WithTags("Visits")
            .WithSummary("Request a visit to a property (Idempotency-Key required)")
            .Accepts<CreateVisitRequest>("application/json")
            .Produces<VisitResponse>(201)
            .Produces<VisitResponse>(200)
            .ProducesValidationProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(413)
            .ProducesProblem(415)
            .ProducesProblem(422)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .RequireTokenNotRevoked()
            .DisableRateLimiting();

        app.MapGet(MinePath, ListAsync)
            .WithName("ListMyVisits")
            .WithTags("Visits")
            .WithSummary("List the user's visits ordered by scheduled time")
            .Produces<VisitListResponse>(200)
            .ProducesValidationProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .RequireTokenNotRevoked()
            .DisableRateLimiting();

        app.MapGet(DetailPath, GetAsync)
            .WithName("GetVisit")
            .WithTags("Visits")
            .WithSummary("Get one of the user's visits")
            .Produces<VisitResponse>(200)
            .ProducesValidationProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(404)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .RequireTokenNotRevoked()
            .DisableRateLimiting();

        app.MapPost(CancelPath, CancelAsync)
            .WithName("CancelVisit")
            .WithTags("Visits")
            .WithSummary("Cancel a requested future visit (idempotent)")
            .Accepts<CancelVisitRequest>("application/json")
            .Produces<VisitResponse>(200)
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

    public static bool IsLandlordVisits(HttpContext context) =>
        context.Request.Path.StartsWithSegments(LandlordPath, StringComparison.OrdinalIgnoreCase);

    public static void MapLandlordVisits(this WebApplication app)
    {
        Landlord(app.MapGet(LandlordPath, CalendarAsync), "ListLandlordVisits", "Visits to my rooms by date range, status and room")
            .Produces<LandlordVisitListResponse>(200);
        Landlord(app.MapGet(LandlordDetailPath, LandlordGetAsync), "GetLandlordVisit", "One visit to one of my rooms")
            .Produces<LandlordVisitResponse>(200).ProducesProblem(404);
        Landlord(app.MapPost(LandlordConfirmPath, ConfirmAsync), "ConfirmVisit", "Confirm a requested visit")
            .Produces<LandlordVisitResponse>(200).ProducesProblem(404).ProducesProblem(409);
        Landlord(app.MapPost(LandlordReschedulePath, RescheduleAsync), "RescheduleVisit", "Move a visit to another time (it stays confirmed)")
            .Accepts<RescheduleVisitRequest>("application/json")
            .Produces<LandlordVisitResponse>(200).ProducesProblem(404).ProducesProblem(409).ProducesProblem(413).ProducesProblem(415).ProducesProblem(422);
        Landlord(app.MapPost(LandlordCancelPath, LandlordCancelAsync), "CancelVisitAsLandlord", "Cancel a visit with a reason")
            .Accepts<LandlordCancelVisitRequest>("application/json")
            .Produces<LandlordVisitResponse>(200).ProducesProblem(404).ProducesProblem(409).ProducesProblem(413).ProducesProblem(415);
    }

    private static RouteHandlerBuilder Landlord(RouteHandlerBuilder builder, string name, string summary) =>
        builder.WithName(name).WithTags("Visits").WithSummary(summary)
            .ProducesValidationProblem(400)
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(429).ProducesProblem(503).ProducesProblem(500)
            .AllowAnonymous().RequireTokenNotRevoked().DisableRateLimiting();

    private static Task<IResult> CalendarAsync(HttpContext context, LandlordVisitService visits, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var page = UserEndpointSupport.QueryInteger(context, "page", PropertyQueryValidation.DefaultPage);
            var pageSize = UserEndpointSupport.QueryInteger(context, "pageSize", PropertyQueryValidation.DefaultPageSize);
            return Results.Json(await visits.CalendarAsync(
                userId,
                UserEndpointSupport.QueryText(context, "from"),
                UserEndpointSupport.QueryText(context, "to"),
                UserEndpointSupport.QueryText(context, "status"),
                UserEndpointSupport.QueryText(context, "roomId"),
                page,
                pageSize,
                context.RequestAborted));
        });

    private static Task<IResult> LandlordGetAsync(string id, HttpContext context, LandlordVisitService visits, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await visits.GetAsync(userId, id, context.RequestAborted));
        });

    private static Task<IResult> ConfirmAsync(string id, HttpContext context, LandlordVisitService visits, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await visits.ConfirmAsync(userId, id, context.RequestAborted));
        });

    private static Task<IResult> RescheduleAsync(string id, HttpContext context, LandlordVisitService visits, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var body = await UserEndpointSupport.ReadJsonAsync<RescheduleVisitRequest>(context, optional: false);
            return Results.Json(await visits.RescheduleAsync(userId, id, body!, context.RequestAborted));
        });

    private static Task<IResult> LandlordCancelAsync(string id, HttpContext context, LandlordVisitService visits, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var body = await UserEndpointSupport.ReadJsonAsync<LandlordCancelVisitRequest>(context, optional: false);
            return Results.Json(await visits.CancelAsync(userId, id, body!, context.RequestAborted));
        });

    private static Task<IResult> CreateAsync(
        HttpContext context,
        VisitService visits,
        IRateLimitStore store,
        UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            await UserEndpointSupport.EnforceAsync(
                context,
                store,
                RateLimitKeys.VisitCreatesUser(userId),
                limits.VisitCreatesPerDay,
                limits.CreateWindow,
                limits.CreateRetryAfterSeconds);

            var keys = context.Request.Headers[IdempotencyHeader];
            if (keys.Count != 1)
            {
                throw new RequestValidationException(IdempotencyHeader, "Exactly one Idempotency-Key is required.");
            }

            var request = await UserEndpointSupport.ReadJsonAsync<CreateVisitRequest>(context, optional: false);
            var result = await visits.CreateAsync(userId, keys[0], request!, context.RequestAborted);

            if (result.Replayed)
            {
                context.Response.Headers[ReplayedHeader] = "true";
                return Results.Json(result.Visit, statusCode: 200);
            }

            return Results.Json(result.Visit, statusCode: 201);
        });

    private static Task<IResult> ListAsync(
        HttpContext context,
        VisitService visits,
        IRateLimitStore store,
        UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var status = UserEndpointSupport.QueryText(context, "status");
            var page = UserEndpointSupport.QueryInteger(context, "page", PropertyQueryValidation.DefaultPage);
            var pageSize = UserEndpointSupport.QueryInteger(context, "pageSize", PropertyQueryValidation.DefaultPageSize);
            var result = await visits.ListAsync(userId, status, page, pageSize, context.RequestAborted);
            return Results.Json(result, statusCode: 200);
        });

    private static Task<IResult> GetAsync(
        string id,
        HttpContext context,
        VisitService visits,
        IRateLimitStore store,
        UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var result = await visits.GetAsync(userId, id, context.RequestAborted);
            return Results.Json(result, statusCode: 200);
        });

    private static Task<IResult> CancelAsync(
        string id,
        HttpContext context,
        VisitService visits,
        IRateLimitStore store,
        UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var request = await UserEndpointSupport.ReadJsonAsync<CancelVisitRequest>(context, optional: true);
            var result = await visits.CancelAsync(userId, id, request?.Reason, context.RequestAborted);
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
            RateLimitKeys.VisitsUser(userId),
            limits.VisitsPerWindow,
            limits.Window,
            limits.RetryAfterSeconds);
}
