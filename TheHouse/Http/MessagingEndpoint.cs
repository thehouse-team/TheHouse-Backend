using Microsoft.Net.Http.Headers;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Messaging;
using TheHouse.Application.UseCases;

namespace TheHouse.Http;

// NV-16: solicitudes de interés, bandejas y chat por consulta incremental.
// Todo con sesión; lo ajeno responde 404 (BOLA); rate limit por usuario en Redis.
public static class MessagingEndpoint
{
    public const string RoomRequestsPath = ApiConventions.Prefix + "/rooms/{roomId}/requests";

    public const string MinePath = ApiConventions.Prefix + "/users/me/requests";

    public const string MineDetailPath = MinePath + "/{id}";

    public const string MineCancelPath = MinePath + "/{id}/cancel";

    public const string LandlordPath = ApiConventions.Prefix + "/landlord/requests";

    public const string LandlordDetailPath = LandlordPath + "/{id}";

    public const string LandlordAcceptPath = LandlordPath + "/{id}/accept";

    public const string LandlordRejectPath = LandlordPath + "/{id}/reject";

    public const string ConversationsPath = ApiConventions.Prefix + "/conversations";

    public const string MessagesPath = ConversationsPath + "/{id}/messages";

    public const string ReadPath = ConversationsPath + "/{id}/read";

    // Mensaje de hasta 1000 caracteres (hasta 4 bytes cada uno) y su envoltura.
    private const int MaximumBodyBytes = 8192;

    public static bool IsMessaging(HttpContext context)
    {
        var path = context.Request.Path;
        return path.StartsWithSegments(MinePath, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments(LandlordPath, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments(ConversationsPath, StringComparison.OrdinalIgnoreCase) ||
               (path.StartsWithSegments(ApiConventions.Prefix + "/rooms", StringComparison.OrdinalIgnoreCase) &&
                path.Value?.EndsWith("/requests", StringComparison.OrdinalIgnoreCase) == true);
    }

    public static void MapMessaging(this WebApplication app)
    {
        Write(app.MapPost(RoomRequestsPath, CreateAsync), "CreateRentalRequest", "Send an interest request for a published room")
            .Accepts<CreateRentalRequest>("application/json")
            .Produces<RentalRequestResponse>(201)
            .ProducesProblem(404).ProducesProblem(409).ProducesProblem(422);
        Read(app.MapGet(MinePath, ListMineAsync), "ListMyRentalRequests", "List the requests I sent")
            .Produces<RentalRequestListResponse>(200);
        Read(app.MapGet(MineDetailPath, GetMineAsync), "GetMyRentalRequest", "Get one of the requests I sent")
            .Produces<RentalRequestResponse>(200).ProducesProblem(404);
        Write(app.MapPost(MineCancelPath, CancelAsync), "CancelMyRentalRequest", "Cancel a pending or accepted request")
            .Produces<RentalRequestResponse>(200).ProducesProblem(404).ProducesProblem(409);
        Read(app.MapGet(LandlordPath, ListReceivedAsync), "ListReceivedRentalRequests", "List the requests for my rooms")
            .Produces<RentalRequestListResponse>(200).ProducesProblem(403);
        Read(app.MapGet(LandlordDetailPath, GetReceivedAsync), "GetReceivedRentalRequest", "Get a request for one of my rooms")
            .Produces<RentalRequestResponse>(200).ProducesProblem(403).ProducesProblem(404);
        Write(app.MapPost(LandlordAcceptPath, AcceptAsync), "AcceptRentalRequest", "Accept a pending request")
            .Produces<RentalRequestResponse>(200).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        Write(app.MapPost(LandlordRejectPath, RejectAsync), "RejectRentalRequest", "Reject a pending request with a reason")
            .Accepts<RejectRentalRequest>("application/json")
            .Produces<RentalRequestResponse>(200).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        Read(app.MapGet(ConversationsPath, ListConversationsAsync), "ListConversations", "List my conversations with unread counts")
            .Produces<ConversationListResponse>(200);
        Read(app.MapGet(MessagesPath, ListMessagesAsync), "ListMessages", "Messages after a cursor (304 when nothing is new)")
            .Produces<MessageListResponse>(200).Produces(304).ProducesProblem(404);
        Write(app.MapPost(MessagesPath, SendAsync), "SendMessage", "Send a message (clientMessageId makes it idempotent)")
            .Accepts<SendMessageRequest>("application/json")
            .Produces<MessageResponse>(201).Produces<MessageResponse>(200).ProducesProblem(404).ProducesProblem(409);
        Write(app.MapPost(ReadPath, MarkReadAsync), "MarkConversationRead", "Mark messages as read up to one message")
            .Accepts<MarkReadRequest>("application/json")
            .Produces(204).ProducesProblem(404);
    }

    private static RouteHandlerBuilder Read(RouteHandlerBuilder builder, string name, string summary) =>
        builder.WithName(name).WithTags("Messaging").WithSummary(summary)
            .ProducesValidationProblem(400)
            .ProducesProblem(401).ProducesProblem(429).ProducesProblem(503).ProducesProblem(500)
            .AllowAnonymous().RequireTokenNotRevoked().DisableRateLimiting();

    private static RouteHandlerBuilder Write(RouteHandlerBuilder builder, string name, string summary) =>
        Read(builder, name, summary).ProducesProblem(413).ProducesProblem(415);

    private static Task<IResult> CreateAsync(
        string roomId,
        HttpContext context,
        RentalRequestService requests,
        IRateLimitStore store,
        UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var body = await UserEndpointSupport.ReadJsonAsync<CreateRentalRequest>(context, optional: false, MaximumBodyBytes);
            await UserEndpointSupport.EnforceAsync(
                context, store, RateLimitKeys.RequestCreatesUser(userId), limits.RequestCreatesPerDay, limits.CreateWindow, limits.CreateRetryAfterSeconds);
            var result = await requests.CreateAsync(userId, roomId, body!, context.RequestAborted);
            return Results.Json(result, statusCode: 201);
        });

    private static Task<IResult> ListMineAsync(HttpContext context, RentalRequestService requests, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var (page, pageSize) = Paging(context);
            return Results.Json(await requests.ListMineAsync(userId, UserEndpointSupport.QueryText(context, "status"), page, pageSize, context.RequestAborted));
        });

    private static Task<IResult> GetMineAsync(string id, HttpContext context, RentalRequestService requests, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await requests.GetMineAsync(userId, id, context.RequestAborted));
        });

    private static Task<IResult> CancelAsync(string id, HttpContext context, RentalRequestService requests, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await requests.CancelAsync(userId, id, context.RequestAborted));
        });

    private static Task<IResult> ListReceivedAsync(HttpContext context, RentalRequestService requests, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var (page, pageSize) = Paging(context);
            return Results.Json(await requests.ListReceivedAsync(
                userId,
                UserEndpointSupport.QueryText(context, "status"),
                UserEndpointSupport.QueryText(context, "roomId"),
                page,
                pageSize,
                context.RequestAborted));
        });

    private static Task<IResult> GetReceivedAsync(string id, HttpContext context, RentalRequestService requests, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await requests.GetReceivedAsync(userId, id, context.RequestAborted));
        });

    private static Task<IResult> AcceptAsync(string id, HttpContext context, RentalRequestService requests, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await requests.AcceptAsync(userId, id, context.RequestAborted));
        });

    private static Task<IResult> RejectAsync(string id, HttpContext context, RentalRequestService requests, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var body = await UserEndpointSupport.ReadJsonAsync<RejectRentalRequest>(context, optional: false);
            return Results.Json(await requests.RejectAsync(userId, id, body!, context.RequestAborted));
        });

    private static Task<IResult> ListConversationsAsync(HttpContext context, ConversationService conversations, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var (page, pageSize) = Paging(context);
            return Results.Json(await conversations.ListAsync(userId, page, pageSize, context.RequestAborted));
        });

    // Consulta incremental: 12 por minuto por conversación. Con `If-None-Match`
    // igual al ETag y el cursor ya en el último mensaje responde 304.
    private static Task<IResult> ListMessagesAsync(string id, HttpContext context, ConversationService conversations, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await UserEndpointSupport.EnforceAsync(
                context, store, RateLimitKeys.MessagePolls(userId, id), limits.MessagePollsPerWindow, limits.Window, limits.RetryAfterSeconds);
            var after = UserEndpointSupport.QueryText(context, "after");
            var limit = UserEndpointSupport.QueryInteger(context, "limit", MessagingRules.MaximumMessagesPerPage);
            var (conversation, tag) = await conversations.GetForParticipantAsync(userId, id, context.RequestAborted);
            var etag = new EntityTagHeaderValue(tag, isWeak: true);
            context.Response.GetTypedHeaders().ETag = etag;

            if (HttpCaching.Matches(context.Request, etag) && after == conversation.LastMessageId)
            {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }

            return Results.Json(await conversations.ListMessagesAsync(userId, conversation, after, limit, context.RequestAborted));
        });

    private static Task<IResult> SendAsync(string id, HttpContext context, ConversationService conversations, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await UserEndpointSupport.EnforceAsync(
                context, store, RateLimitKeys.MessageSendsUser(userId), limits.MessageSendsPerWindow, limits.Window, limits.RetryAfterSeconds);
            var body = await UserEndpointSupport.ReadJsonAsync<SendMessageRequest>(context, optional: false, MaximumBodyBytes);
            var (message, created) = await conversations.SendAsync(userId, id, body!, context.RequestAborted);
            return Results.Json(message, statusCode: created ? 201 : 200);
        });

    private static Task<IResult> MarkReadAsync(string id, HttpContext context, ConversationService conversations, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var body = await UserEndpointSupport.ReadJsonAsync<MarkReadRequest>(context, optional: false);
            await conversations.MarkReadAsync(userId, id, body!, context.RequestAborted);
            return Results.NoContent();
        });

    private static (int Page, int PageSize) Paging(HttpContext context) =>
        (UserEndpointSupport.QueryInteger(context, "page", PropertyQueryValidation.DefaultPage),
         UserEndpointSupport.QueryInteger(context, "pageSize", PropertyQueryValidation.DefaultPageSize));

    private static Task Enforce(HttpContext context, IRateLimitStore store, UserRateLimitOptions limits, string userId) =>
        UserEndpointSupport.EnforceAsync(
            context, store, RateLimitKeys.MessagingUser(userId), limits.MessagingPerWindow, limits.Window, limits.RetryAfterSeconds);
}
