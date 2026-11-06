using System.IdentityModel.Tokens.Jwt;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Users;
using TheHouse.Application.UseCases;

namespace TheHouse.Http;

// NV-10: GET /users/me. TokenRevocationMiddleware valida el Bearer
// (firma, `jti`, `sid` no revocados) y deja el principal en
// HttpContext.User; aquí solo se usa su `sub`.
public static class UsersMeEndpoint
{
    public const string Path =
        ApiConventions.Prefix + "/users/me";

    public static void MapUsersMe(this WebApplication app) =>
        app.MapGet(Path, GetMeAsync)
            .WithName("GetCurrentUser")
            .WithTags("Users")
            .WithSummary(
                "Return the authenticated user's account")
            .Produces<MeResponse>(200)
            .ProducesProblem(401)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .RequireTokenNotRevoked();

    private static async Task<IResult> GetMeAsync(
        HttpContext context,
        AccountService accounts)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";

        var userId = context.User
            .FindFirst(JwtRegisteredClaimNames.Sub)
            ?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Error(context, 401);
        }

        try
        {
            var me = await accounts.GetMeAsync(
                userId,
                context.RequestAborted);

            return Results.Json(
                me,
                statusCode: StatusCodes.Status200OK);
        }
        catch (InvalidCredentialsException)
        {
            return Error(context, 401);
        }
        catch (DependencyUnavailableException)
        {
            return Error(context, 503);
        }
    }

    private static IResult Error(
        HttpContext context,
        int status) =>
        Results.Problem(
            ProblemResponses.Create(
                context,
                status));
}
