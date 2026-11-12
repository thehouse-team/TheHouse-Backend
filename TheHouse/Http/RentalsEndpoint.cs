using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Rentals;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Http;

// NV-18: cierre de trato con QR, Mi renta y fin de renta. Todo con sesión;
// lo ajeno responde 404 (BOLA). El código de cierre tiene límites propios por
// usuario, por IP y por cuenta en 15 minutos (RNF-17).
public static class RentalsEndpoint
{
    public const string LandlordPath = ApiConventions.Prefix + "/landlord/agreements";

    public const string LandlordDetailPath = LandlordPath + "/{id}";

    public const string LandlordCodePath = LandlordPath + "/{id}/code";

    public const string LandlordCancelPath = LandlordPath + "/{id}/cancel";

    public const string LandlordEndPath = LandlordPath + "/{id}/end";

    public const string StudentPath = ApiConventions.Prefix + "/agreements";

    public const string PreviewPath = StudentPath + "/preview";

    public const string AcceptPath = StudentPath + "/accept";

    public const string RentalPath = ApiConventions.Prefix + "/users/me/rental";

    public static bool IsRentals(HttpContext context)
    {
        var path = context.Request.Path;
        return path.StartsWithSegments(LandlordPath, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments(StudentPath, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments(RentalPath, StringComparison.OrdinalIgnoreCase);
    }

    public static void MapRentals(this WebApplication app)
    {
        Write(app.MapPost(LandlordPath, CreateAsync), "CreateAgreement", "Create a pending agreement from an accepted request and get its closing code")
            .Accepts<CreateAgreementRequest>("application/json")
            .Produces<LandlordAgreementResponse>(201).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        Read(app.MapGet(LandlordPath, ListAsync), "ListAgreements", "List my agreements by status and room")
            .Produces<LandlordAgreementListResponse>(200).ProducesProblem(403);
        Read(app.MapGet(LandlordDetailPath, GetAsync), "GetAgreement", "Get one of my agreements")
            .Produces<LandlordAgreementResponse>(200).ProducesProblem(403).ProducesProblem(404);
        Write(app.MapPost(LandlordCodePath, IssueCodeAsync), "IssueClosingCode", "Issue a new closing code for a pending agreement")
            .Produces<LandlordAgreementResponse>(200).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        Write(app.MapPost(LandlordCancelPath, CancelAsync), "CancelAgreement", "Cancel a pending agreement")
            .Produces<LandlordAgreementResponse>(200).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        Write(app.MapPost(LandlordEndPath, EndAsync), "EndAgreement", "End an active rental; the room becomes available")
            .Accepts<EndAgreementRequest>("application/json")
            .Produces<LandlordAgreementResponse>(200).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        Write(app.MapPost(PreviewPath, PreviewAsync), "PreviewAgreement", "Review an agreement with its closing code without using it")
            .Accepts<ClosingCodeRequest>("application/json")
            .Produces<AgreementPreviewResponse>(200).ProducesProblem(404).ProducesProblem(410);
        Write(app.MapPost(AcceptPath, AcceptAsync), "AcceptAgreement", "Accept an agreement with its closing code; the room becomes rented")
            .Accepts<AcceptAgreementRequest>("application/json")
            .Produces<RentalResponse>(200).ProducesProblem(404).ProducesProblem(409).ProducesProblem(410);
        Read(app.MapGet(RentalPath, GetRentalAsync), "GetMyRental", "My active rental with the exact address")
            .Produces<RentalResponse>(200).ProducesProblem(404);
    }

    private static RouteHandlerBuilder Read(RouteHandlerBuilder builder, string name, string summary) =>
        builder.WithName(name).WithTags("Rentals").WithSummary(summary)
            .ProducesValidationProblem(400)
            .ProducesProblem(401).ProducesProblem(429).ProducesProblem(503).ProducesProblem(500)
            .AllowAnonymous().RequireTokenNotRevoked().DisableRateLimiting();

    private static RouteHandlerBuilder Write(RouteHandlerBuilder builder, string name, string summary) =>
        Read(builder, name, summary).ProducesProblem(413).ProducesProblem(415);

    private static Task<IResult> CreateAsync(HttpContext context, AgreementService agreements, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var body = await UserEndpointSupport.ReadJsonAsync<CreateAgreementRequest>(context, optional: false);
            return Results.Json(await agreements.CreateAsync(userId, body!, context.RequestAborted), statusCode: 201);
        });

    private static Task<IResult> ListAsync(HttpContext context, AgreementService agreements, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await agreements.ListAsync(
                userId,
                UserEndpointSupport.QueryText(context, "status"),
                UserEndpointSupport.QueryText(context, "roomId"),
                UserEndpointSupport.QueryInteger(context, "page", PropertyQueryValidation.DefaultPage),
                UserEndpointSupport.QueryInteger(context, "pageSize", PropertyQueryValidation.DefaultPageSize),
                context.RequestAborted));
        });

    private static Task<IResult> GetAsync(string id, HttpContext context, AgreementService agreements, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await agreements.GetAsync(userId, id, context.RequestAborted));
        });

    private static Task<IResult> IssueCodeAsync(string id, HttpContext context, AgreementService agreements, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await agreements.IssueCodeAsync(userId, id, context.RequestAborted));
        });

    private static Task<IResult> CancelAsync(string id, HttpContext context, AgreementService agreements, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await agreements.CancelAsync(userId, id, context.RequestAborted));
        });

    private static Task<IResult> EndAsync(string id, HttpContext context, AgreementService agreements, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var body = await UserEndpointSupport.ReadJsonAsync<EndAgreementRequest>(context, optional: true);
            return Results.Json(await agreements.EndAsync(userId, id, body, context.RequestAborted));
        });

    private static Task<IResult> PreviewAsync(HttpContext context, AgreementService agreements, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await EnforceClosingCode(context, store, limits, userId);
            var body = await UserEndpointSupport.ReadJsonAsync<ClosingCodeRequest>(context, optional: false);
            return Results.Json(await agreements.PreviewAsync(userId, body!, context.RequestAborted));
        });

    private static Task<IResult> AcceptAsync(
        HttpContext context,
        AgreementService agreements,
        IRateLimitStore store,
        UserRateLimitOptions limits,
        ILoggerFactory loggers) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await EnforceClosingCode(context, store, limits, userId);
            var body = await UserEndpointSupport.ReadJsonAsync<AcceptAgreementRequest>(context, optional: false);
            var (rental, effectsPending) = await agreements.AcceptAsync(userId, body!, context.RequestAborted);
            if (effectsPending)
            {
                // Sin datos personales: solo el id del convenio.
                loggers.CreateLogger("TheHouse.Rentals").LogWarning(
                    "Agreement {AgreementId} is active but closing other requests or visits of the room is pending.",
                    rental.AgreementId);
            }

            return Results.Json(rental);
        });

    private static Task<IResult> GetRentalAsync(HttpContext context, AgreementService agreements, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await agreements.GetMineAsync(userId, context.RequestAborted));
        });

    private static Task Enforce(HttpContext context, IRateLimitStore store, UserRateLimitOptions limits, string userId) =>
        UserEndpointSupport.EnforceAsync(
            context, store, RateLimitKeys.RentalsUser(userId), limits.RentalsPerWindow, limits.Window, limits.RetryAfterSeconds);

    // Revisar y aceptar: 10/min por usuario, 30/min por IP y 5 intentos con
    // código por cuenta en 15 minutos (cuenta cada intento, no solo los
    // fallidos, porque el contador no se puede consultar sin sumar).
    private static async Task EnforceClosingCode(HttpContext context, IRateLimitStore store, UserRateLimitOptions limits, string userId)
    {
        await UserEndpointSupport.EnforceAsync(
            context, store, RateLimitKeys.ClosingCodeUser(userId), limits.ClosingCodePerWindow, limits.Window, limits.RetryAfterSeconds);
        await UserEndpointSupport.EnforceAsync(
            context,
            store,
            RateLimitKeys.ClosingCodeIp(context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            limits.ClosingCodeIpPerWindow,
            limits.Window,
            limits.RetryAfterSeconds);
        var attempts = await store.ConsumeAsync(
            RateLimitKeys.ClosingCodeAttempts(userId), limits.ClosingCodeAttempts, limits.ClosingCodeAttemptsWindow, context.RequestAborted);
        if (!attempts.IsAllowed)
        {
            throw new RateLimitExceededException(limits.ClosingCodeAttemptsRetryAfterSeconds, "too_many_attempts");
        }
    }
}
