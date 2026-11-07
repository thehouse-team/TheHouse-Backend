using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Profile;
using TheHouse.Application.UseCases;

namespace TheHouse.Http;

// NV-11: perfil, universidad y preferencias del usuario autenticado. El
// usuario sale del `sub`; rate limit por usuario en Redis (fallo cerrado).
public static class ProfileEndpoint
{
    public const string ProfilePath = ApiConventions.Prefix + "/users/me/profile";

    public const string UniversityPath = ApiConventions.Prefix + "/users/me/university";

    public const string PreferencesPath = ApiConventions.Prefix + "/users/me/preferences";

    public static void MapProfile(this WebApplication app)
    {
        app.MapGet(ProfilePath, GetProfileAsync)
            .WithName("GetMyProfile")
            .WithTags("Profile")
            .WithSummary("Return the user's profile and chosen university")
            .Produces<ProfileResponse>(200)
            .ProducesProblem(401)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .RequireTokenNotRevoked()
            .DisableRateLimiting();

        app.MapPatch(ProfilePath, UpdateProfileAsync)
            .WithName("UpdateMyProfile")
            .WithTags("Profile")
            .WithSummary("Update the user's name")
            .Accepts<UpdateProfileRequest>("application/json")
            .Produces<ProfileResponse>(200)
            .ProducesValidationProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(413)
            .ProducesProblem(415)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .RequireTokenNotRevoked()
            .DisableRateLimiting();

        app.MapPut(UniversityPath, SetUniversityAsync)
            .WithName("SetMyUniversity")
            .WithTags("Profile")
            .WithSummary("Save the user's university (idempotent)")
            .Accepts<SetUniversityRequest>("application/json")
            .Produces(204)
            .ProducesValidationProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(404)
            .ProducesProblem(413)
            .ProducesProblem(415)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .RequireTokenNotRevoked()
            .DisableRateLimiting();

        app.MapGet(PreferencesPath, GetPreferencesAsync)
            .WithName("GetMyPreferences")
            .WithTags("Profile")
            .WithSummary("Return the user's search preferences or the defaults")
            .Produces<PreferencesResponse>(200)
            .ProducesProblem(401)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .RequireTokenNotRevoked()
            .DisableRateLimiting();

        app.MapPut(PreferencesPath, ReplacePreferencesAsync)
            .WithName("ReplaceMyPreferences")
            .WithTags("Profile")
            .WithSummary("Replace the user's search preferences")
            .Accepts<PreferencesRequest>("application/json")
            .Produces<PreferencesResponse>(200)
            .ProducesValidationProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(413)
            .ProducesProblem(415)
            .ProducesProblem(429)
            .ProducesProblem(503)
            .ProducesProblem(500)
            .AllowAnonymous()
            .RequireTokenNotRevoked()
            .DisableRateLimiting();
    }

    private static Task<IResult> GetProfileAsync(
        HttpContext context,
        ProfileService profiles,
        IRateLimitStore store,
        UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await profiles.GetAsync(userId, context.RequestAborted), statusCode: 200);
        });

    private static Task<IResult> UpdateProfileAsync(
        HttpContext context,
        ProfileService profiles,
        IRateLimitStore store,
        UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var request = await UserEndpointSupport.ReadJsonAsync<UpdateProfileRequest>(context, optional: false);
            return Results.Json(await profiles.UpdateNameAsync(userId, request!, context.RequestAborted), statusCode: 200);
        });

    private static Task<IResult> SetUniversityAsync(
        HttpContext context,
        ProfileService profiles,
        IRateLimitStore store,
        UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var request = await UserEndpointSupport.ReadJsonAsync<SetUniversityRequest>(context, optional: false);
            await profiles.SetUniversityAsync(userId, request!, context.RequestAborted);
            return Results.NoContent();
        });

    private static Task<IResult> GetPreferencesAsync(
        HttpContext context,
        PreferencesService preferences,
        IRateLimitStore store,
        UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            return Results.Json(await preferences.GetAsync(userId, context.RequestAborted), statusCode: 200);
        });

    private static Task<IResult> ReplacePreferencesAsync(
        HttpContext context,
        PreferencesService preferences,
        IRateLimitStore store,
        UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await Enforce(context, store, limits, userId);
            var request = await UserEndpointSupport.ReadJsonAsync<PreferencesRequest>(context, optional: false);
            return Results.Json(await preferences.ReplaceAsync(userId, request!, context.RequestAborted), statusCode: 200);
        });

    private static Task Enforce(
        HttpContext context,
        IRateLimitStore store,
        UserRateLimitOptions limits,
        string userId) =>
        UserEndpointSupport.EnforceAsync(
            context,
            store,
            RateLimitKeys.ProfileUser(userId),
            limits.ProfilePerWindow,
            limits.Window,
            limits.RetryAfterSeconds);
}
