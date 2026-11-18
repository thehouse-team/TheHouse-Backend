using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Users;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Http;

// NV-10 1.2.0: eliminar la cuenta propia y aviso legal. La cuenta sale del
// token; no existe forma de eliminar otra cuenta por estas rutas.
public static class AccountEndpoint
{
    public const string DeletionPath = ApiConventions.Prefix + "/users/me/deletion";
    public const string AcceptLegalPath = ApiConventions.Prefix + "/users/me/legal";
    public const string LegalPath = ApiConventions.Prefix + "/legal";

    public const string BlockersItem = "thehouse.problem.blockers";

    public static bool IsAccount(HttpContext context) =>
        context.Request.Path.StartsWithSegments(DeletionPath, StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.StartsWithSegments(AcceptLegalPath, StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.StartsWithSegments(LegalPath, StringComparison.OrdinalIgnoreCase);

    public static void MapAccount(this WebApplication app)
    {
        Private(app.MapGet(DeletionPath, PreviewAsync), "PreviewAccountDeletion", "What deleting my account would erase, cancel or block")
            .Produces<DeletionPreviewResponse>(200);
        Private(app.MapPost(DeletionPath, DeleteAsync), "DeleteAccount", "Delete my account with my password and the word ELIMINAR")
            .Produces(204).ProducesProblem(403).ProducesProblem(409);
        Private(app.MapPut(AcceptLegalPath, AcceptLegalAsync), "AcceptLegal", "Accept the current terms and privacy notice")
            .Produces<LegalAcceptanceResponse>(200).ProducesProblem(409);
        app.MapGet(LegalPath, LegalAsync)
            .WithName("GetLegal").WithTags("Account").WithSummary("Current terms, privacy notice and account deletion URLs")
            .Produces<LegalResponse>(200).ProducesProblem(429).ProducesProblem(503).ProducesProblem(500)
            .AllowAnonymous().DisableRateLimiting();
    }

    private static RouteHandlerBuilder Private(RouteHandlerBuilder builder, string name, string summary) =>
        builder.WithName(name).WithTags("Account").WithSummary(summary)
            .ProducesValidationProblem(400)
            .ProducesProblem(401).ProducesProblem(429).ProducesProblem(503).ProducesProblem(500)
            .AllowAnonymous().RequireTokenNotRevoked().DisableRateLimiting();

    private static Task<IResult> PreviewAsync(HttpContext context, AccountDeletionService deletion, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await UserEndpointSupport.EnforceAsync(context, store, RateLimitKeys.AccountUser(userId), limits.AccountPerWindow, limits.Window, limits.RetryAfterSeconds);
            try
            {
                return Results.Json(await deletion.PreviewAsync(userId, context.RequestAborted));
            }
            catch (InvalidCredentialsException)
            {
                return UserEndpointSupport.Error(context, 401);
            }
        });

    private static Task<IResult> DeleteAsync(HttpContext context, AccountDeletionService deletion, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            // 5 intentos por hora: también frena adivinar la contraseña.
            await UserEndpointSupport.EnforceAsync(
                context, store, RateLimitKeys.AccountDeletionUser(userId), limits.AccountDeletionsPerHour, TimeSpan.FromHours(1), limits.CreateRetryAfterSeconds);
            var body = await UserEndpointSupport.ReadJsonAsync<AccountDeletionRequest>(context, optional: false);
            try
            {
                await deletion.DeleteAsync(userId, body, context.RequestAborted);
                return Results.NoContent();
            }
            catch (AccountDeletionBlockedException exception)
            {
                context.Items[BlockersItem] = exception.Blockers
                    .Select(blocker => new DeletionBlockerResponse(blocker.Code, blocker.Count))
                    .ToArray();
                return UserEndpointSupport.Error(context, 409, "deletion_blocked");
            }
            catch (AccountDeletionIncompleteException)
            {
                return UserEndpointSupport.Error(context, 503, "deletion_incomplete");
            }
            catch (InvalidCredentialsException)
            {
                return UserEndpointSupport.Error(context, 401);
            }
        });

    private static Task<IResult> AcceptLegalAsync(HttpContext context, AccountService accounts, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await UserEndpointSupport.EnforceAsync(context, store, RateLimitKeys.AccountUser(userId), limits.AccountPerWindow, limits.Window, limits.RetryAfterSeconds);
            var body = await UserEndpointSupport.ReadJsonAsync<LegalAcceptanceRequest>(context, optional: false);
            try
            {
                return Results.Json(await accounts.AcceptLegalAsync(userId, body, context.RequestAborted));
            }
            catch (InvalidCredentialsException)
            {
                return UserEndpointSupport.Error(context, 401);
            }
        });

    // Público y cacheable: no depende del usuario.
    private static async Task<IResult> LegalAsync(HttpContext context, AccountService accounts, IRateLimitStore store, UserRateLimitOptions limits)
    {
        try
        {
            var address = context.Connection.RemoteIpAddress;
            if (address?.IsIPv4MappedToIPv6 == true)
            {
                address = address.MapToIPv4();
            }

            await UserEndpointSupport.EnforceAsync(
                context, store, RateLimitKeys.LegalIp(address?.ToString() ?? "unknown"), limits.LegalPerWindow, limits.Window, limits.RetryAfterSeconds);
        }
        catch (RateLimitExceededException exception)
        {
            context.Response.Headers.RetryAfter = exception.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return UserEndpointSupport.Error(context, 429);
        }
        catch (DependencyUnavailableException)
        {
            return UserEndpointSupport.Error(context, 503);
        }

        context.Response.Headers.CacheControl = "public, max-age=3600";
        return Results.Json(accounts.Legal());
    }
}
