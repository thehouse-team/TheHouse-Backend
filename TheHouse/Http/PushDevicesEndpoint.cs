using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Notifications;
using TheHouse.Application.UseCases;

namespace TheHouse.Http;

// NV-20 1.2.0: dispositivos que reciben push. El usuario sale del token; una
// instalación ajena o inexistente responde 404 (BOLA). El token de FCM nunca
// se devuelve.
public static class PushDevicesEndpoint
{
    public const string BasePath = ApiConventions.Prefix + "/users/me/devices";
    public const string ItemPath = BasePath + "/{installationId}";

    public static bool IsDevices(HttpContext context) =>
        context.Request.Path.StartsWithSegments(BasePath, StringComparison.OrdinalIgnoreCase);

    public static void MapPushDevices(this WebApplication app)
    {
        Route(app.MapGet(BasePath, ListAsync), "ListPushDevices", "My devices registered for push (without tokens)")
            .Produces<PushDeviceListResponse>(200);
        Route(app.MapPut(ItemPath, RegisterAsync), "RegisterPushDevice", "Register or update the FCM token of this installation")
            .Accepts<PushDeviceRequest>("application/json")
            .Produces<PushDeviceResponse>(200).Produces<PushDeviceResponse>(201)
            .ProducesProblem(409).ProducesProblem(413).ProducesProblem(415);
        Route(app.MapDelete(ItemPath, DeleteAsync), "DeletePushDevice", "Remove this installation before logging out")
            .Produces(204).ProducesProblem(404);
    }

    private static RouteHandlerBuilder Route(RouteHandlerBuilder builder, string name, string summary) =>
        builder.WithName(name).WithTags("Notifications").WithSummary(summary)
            .ProducesValidationProblem(400)
            .ProducesProblem(401).ProducesProblem(429).ProducesProblem(503).ProducesProblem(500)
            .AllowAnonymous().RequireTokenNotRevoked().DisableRateLimiting();

    private static Task<IResult> ListAsync(HttpContext context, PushDeviceService devices, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await devices.ListAsync(userId, context.RequestAborted));
        });

    private static Task<IResult> RegisterAsync(string installationId, HttpContext context, PushDeviceService devices, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            // 30 registros por hora: la app lo llama al abrir y en `onNewToken`.
            await UserEndpointSupport.EnforceAsync(
                context, store, RateLimitKeys.PushDevicesUser(userId), limits.PushDeviceRegistrationsPerHour, TimeSpan.FromHours(1), limits.CreateRetryAfterSeconds);
            var body = await UserEndpointSupport.ReadJsonAsync<PushDeviceRequest>(context, optional: false, maximumBytes: 6144);
            var (device, created) = await devices.RegisterAsync(userId, installationId, body, context.RequestAborted);
            return Results.Json(device, statusCode: created ? StatusCodes.Status201Created : StatusCodes.Status200OK);
        });

    private static Task<IResult> DeleteAsync(string installationId, HttpContext context, PushDeviceService devices, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            await devices.DeleteAsync(userId, installationId, context.RequestAborted);
            return Results.NoContent();
        });

    private static Task Enforce(HttpContext context, IRateLimitStore store, UserRateLimitOptions limits, string userId) =>
        UserEndpointSupport.EnforceAsync(
            context, store, RateLimitKeys.NotificationsUser(userId), limits.NotificationsPerWindow, limits.Window, limits.RetryAfterSeconds);
}
