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
