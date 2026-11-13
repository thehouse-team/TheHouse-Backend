using Microsoft.Net.Http.Headers;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Notifications;
using TheHouse.Application.UseCases;

namespace TheHouse.Http;

// NV-20: bandeja de notificaciones del usuario autenticado. Lo ajeno responde
// 404 (BOLA); el contador admite `If-None-Match` y responde 304.
public static class NotificationsEndpoint
{
    public const string BasePath = ApiConventions.Prefix + "/users/me/notifications";

    public const string CountPath = BasePath + "/unread-count";

    public const string ReadPath = BasePath + "/{id}/read";

    public const string ReadAllPath = BasePath + "/read-all";

    public static bool IsNotifications(HttpContext context) =>
        context.Request.Path.StartsWithSegments(BasePath, StringComparison.OrdinalIgnoreCase);

    public static void MapNotifications(this WebApplication app)
    {
        Read(app.MapGet(BasePath, ListAsync), "ListNotifications", "My notifications, optionally unread only or by category")
            .Produces<NotificationListResponse>(200);
        Read(app.MapGet(CountPath, CountAsync), "CountUnreadNotifications", "Unread notifications in total and by category (ETag and 304)")
            .Produces<UnreadCountResponse>(200).Produces(304);
        Write(app.MapPost(ReadPath, MarkReadAsync), "MarkNotificationRead", "Mark one notification as read")
            .Produces(204).ProducesProblem(404);
        Write(app.MapPost(ReadAllPath, MarkAllReadAsync), "MarkAllNotificationsRead", "Mark all notifications, or those of a category, as read")
            .Accepts<ReadAllNotificationsRequest>("application/json")
            .Produces(204);
    }

    private static RouteHandlerBuilder Read(RouteHandlerBuilder builder, string name, string summary) =>
        builder.WithName(name).WithTags("Notifications").WithSummary(summary)
            .ProducesValidationProblem(400)
            .ProducesProblem(401).ProducesProblem(429).ProducesProblem(503).ProducesProblem(500)
            .AllowAnonymous().RequireTokenNotRevoked().DisableRateLimiting();

    private static RouteHandlerBuilder Write(RouteHandlerBuilder builder, string name, string summary) =>
        Read(builder, name, summary).ProducesProblem(413).ProducesProblem(415);

    private static Task<IResult> ListAsync(HttpContext context, NotificationService notifications, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await notifications.ListAsync(
                userId,
                UserEndpointSupport.QueryText(context, "unread"),
                UserEndpointSupport.QueryText(context, "category"),
                UserEndpointSupport.QueryInteger(context, "page", PropertyQueryValidation.DefaultPage),
                UserEndpointSupport.QueryInteger(context, "pageSize", PropertyQueryValidation.DefaultPageSize),
                context.RequestAborted));
        });

    // `private, no-cache`: el cliente revalida con el ETag y ninguna caché
    // compartida guarda el contador (constitution §23).
    private static Task<IResult> CountAsync(HttpContext context, NotificationService notifications, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await UserEndpointSupport.EnforceAsync(
                context, store, RateLimitKeys.NotificationCountUser(userId), limits.NotificationCountPerWindow, limits.Window, limits.RetryAfterSeconds);
            var counts = await notifications.CountAsync(userId, context.RequestAborted);
            var tag = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"\"{counts.Unread}-{counts.Requests}-{counts.Visits}-{counts.System}\"");
            var etag = new EntityTagHeaderValue(tag, isWeak: true);
            context.Response.Headers.CacheControl = "private, no-cache";
            context.Response.Headers.Remove(HeaderNames.Pragma);
            context.Response.GetTypedHeaders().ETag = etag;
            return HttpCaching.Matches(context.Request, etag)
                ? Results.StatusCode(StatusCodes.Status304NotModified)
                : Results.Json(counts);
        });

    private static Task<IResult> MarkReadAsync(string id, HttpContext context, NotificationService notifications, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            await notifications.MarkReadAsync(userId, id, context.RequestAborted);
            return Results.NoContent();
        });

    private static Task<IResult> MarkAllReadAsync(HttpContext context, NotificationService notifications, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var body = await UserEndpointSupport.ReadJsonAsync<ReadAllNotificationsRequest>(context, optional: true);
            await notifications.MarkAllReadAsync(userId, body, context.RequestAborted);
            return Results.NoContent();
        });

    private static Task Enforce(HttpContext context, IRateLimitStore store, UserRateLimitOptions limits, string userId) =>
        UserEndpointSupport.EnforceAsync(
            context, store, RateLimitKeys.NotificationsUser(userId), limits.NotificationsPerWindow, limits.Window, limits.RetryAfterSeconds);
}
