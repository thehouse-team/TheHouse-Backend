using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Landlord;
using TheHouse.Application.UseCases;

namespace TheHouse.Http;

// NV-12: modo arrendador, edificios y cuartos. Usuario solo del `sub`;
// recurso ajeno → 404; sin perfil de arrendador → 403. Rate limit por
// usuario en Redis: 60 lecturas y 30 escrituras por minuto; generar
// cuartos 10 por hora.
public static class LandlordEndpoint
{
    public const string BasePath = ApiConventions.Prefix + "/landlord";

    public const string ProfilePath = BasePath + "/profile";

    public const string BuildingsPath = BasePath + "/buildings";

    public const string BuildingPath = BuildingsPath + "/{id}";

    public const string GeneratePath = BuildingPath + "/rooms/generate";

    public const string BuildingRoomsPath = BuildingPath + "/rooms";

    public const string PublicationPath = BuildingPath + "/publication";

    public const string PublishPath = BuildingPath + "/publish";

    public const string RoomPath = BasePath + "/rooms/{id}";

    public const string DuplicatePath = RoomPath + "/duplicate";

    public const string CopyFromPath = RoomPath + "/copy-from";

    public const string PausePath = RoomPath + "/pause";

    public const string ResumePath = RoomPath + "/resume";

    public const int MaximumBodyBytes = 8 * 1024;

    public static void MapLandlord(this WebApplication app)
    {
        Write(app.MapPost(ProfilePath, CreateProfileAsync), "CreateLandlordProfile", "Activate landlord mode", 201, landlord: false);
        Read(app.MapGet(ProfilePath, GetProfileAsync), "GetLandlordProfile", "Return the landlord profile", 404);
        Write(app.MapPost(BuildingsPath, CreateBuildingAsync), "CreateBuilding", "Create a building in draft", 201);
        Read(app.MapGet(BuildingsPath, ListBuildingsAsync), "ListMyBuildings", "List my buildings with totals", 400);
        Read(app.MapGet(BuildingPath, GetBuildingAsync), "GetMyBuilding", "Get one of my buildings", 400, 404);
        Write(app.MapPatch(BuildingPath, UpdateBuildingAsync), "UpdateMyBuilding", "Update one of my buildings (version required)", 200);
        Write(app.MapDelete(BuildingPath, DeleteBuildingAsync), "DeleteMyBuilding", "Delete one of my buildings (version required)", 204);
        Write(app.MapPost(GeneratePath, GenerateRoomsAsync), "GenerateRooms", "Generate rooms by floor", 201);
        Read(app.MapGet(BuildingRoomsPath, ListRoomsAsync), "ListBuildingRooms", "List the rooms of one of my buildings", 400, 404);
        Write(app.MapPatch(RoomPath, UpdateRoomAsync), "UpdateMyRoom", "Update one of my rooms (version required)", 200);
        Write(app.MapDelete(RoomPath, DeleteRoomAsync), "DeleteMyRoom", "Delete one of my rooms (version required)", 204);
        Write(app.MapPost(DuplicatePath, DuplicateRoomAsync), "DuplicateRoom", "Duplicate a room with the next number of its floor", 201);
        Write(app.MapPost(CopyFromPath, CopyRoomAsync), "CopyRoomData", "Copy data from another room of the same building", 200);
        Write(app.MapPost(PausePath, PauseRoomAsync), "PauseRoom", "Pause an available room", 200);
        Write(app.MapPost(ResumePath, ResumeRoomAsync), "ResumeRoom", "Resume a paused room", 200);
        Read(app.MapGet(PublicationPath, PublicationAsync), "GetPublicationSteps", "List the steps pending to submit for review", 400, 404);
        Write(app.MapPost(PublishPath, PublishAsync), "SubmitBuildingForReview", "Submit a building for review", 202);
    }

    private static void Read(RouteHandlerBuilder builder, string name, string summary, params int[] extra)
    {
        builder.WithName(name).WithTags("Landlord").WithSummary(summary).Produces(200);
        foreach (var status in new[] { 401, 403, 429, 503, 500 }.Concat(extra).Distinct())
        {
            if (status == 400)
            {
                builder.ProducesValidationProblem(400);
            }
            else
            {
                builder.ProducesProblem(status);
            }
        }

        builder.AllowAnonymous().RequireTokenNotRevoked().DisableRateLimiting();
    }

    private static void Write(RouteHandlerBuilder builder, string name, string summary, int success, bool landlord = true)
    {
        builder.WithName(name).WithTags("Landlord").WithSummary(summary).Produces(success).ProducesValidationProblem(400);
        foreach (var status in new[] { 401, 404, 409, 413, 415, 429, 503, 500 })
        {
            builder.ProducesProblem(status);
        }

        if (landlord)
        {
            builder.ProducesProblem(403);
        }

        builder.AllowAnonymous().RequireTokenNotRevoked().DisableRateLimiting();
    }

    private static Task<IResult> CreateProfileAsync(HttpContext context, LandlordService landlords, IRateLimitStore store, UserRateLimitOptions limits) =>
        Writing(context, store, limits, async userId =>
        {
            var request = await Body<CreateLandlordProfileRequest>(context);
            return Results.Json(await landlords.CreateAsync(userId, request, context.RequestAborted), statusCode: 201);
        });

    private static Task<IResult> GetProfileAsync(HttpContext context, LandlordService landlords, IRateLimitStore store, UserRateLimitOptions limits) =>
        Reading(context, store, limits, async userId =>
            Results.Json(await landlords.GetAsync(userId, context.RequestAborted), statusCode: 200));

    private static Task<IResult> CreateBuildingAsync(HttpContext context, BuildingService buildings, IRateLimitStore store, UserRateLimitOptions limits) =>
        Writing(context, store, limits, async userId =>
        {
            var request = await Body<BuildingRequest>(context);
            return Results.Json(await buildings.CreateAsync(userId, request, context.RequestAborted), statusCode: 201);
        });

    private static Task<IResult> ListBuildingsAsync(HttpContext context, BuildingService buildings, IRateLimitStore store, UserRateLimitOptions limits) =>
        Reading(context, store, limits, async userId =>
        {
            var page = UserEndpointSupport.QueryInteger(context, "page", PropertyQueryValidation.DefaultPage);
            var pageSize = UserEndpointSupport.QueryInteger(context, "pageSize", PropertyQueryValidation.DefaultPageSize);
            return Results.Json(await buildings.ListAsync(userId, page, pageSize, context.RequestAborted), statusCode: 200);
        });

    private static Task<IResult> GetBuildingAsync(string id, HttpContext context, BuildingService buildings, IRateLimitStore store, UserRateLimitOptions limits) =>
        Reading(context, store, limits, async userId =>
            Results.Json(await buildings.GetAsync(userId, id, context.RequestAborted), statusCode: 200));

    private static Task<IResult> UpdateBuildingAsync(string id, HttpContext context, BuildingService buildings, IRateLimitStore store, UserRateLimitOptions limits) =>
        Writing(context, store, limits, async userId =>
        {
            var request = await Body<BuildingRequest>(context);
            return Results.Json(await buildings.UpdateAsync(userId, id, request, context.RequestAborted), statusCode: 200);
        });

    private static Task<IResult> DeleteBuildingAsync(string id, HttpContext context, BuildingService buildings, IRateLimitStore store, UserRateLimitOptions limits) =>
        Writing(context, store, limits, async userId =>
        {
            await buildings.DeleteAsync(userId, id, UserEndpointSupport.QueryText(context, "version"), context.RequestAborted);
            return Results.NoContent();
        });

    private static Task<IResult> GenerateRoomsAsync(string id, HttpContext context, RoomService rooms, IRateLimitStore store, UserRateLimitOptions limits) =>
        Writing(context, store, limits, async userId =>
        {
            await UserEndpointSupport.EnforceAsync(
                context,
                store,
                RateLimitKeys.RoomGenerate(userId),
                limits.RoomGeneratesPerHour,
                limits.GenerateWindow,
                (int)limits.GenerateWindow.TotalSeconds);
            var request = await Body<GenerateRoomsRequest>(context);
            return Results.Json(await rooms.GenerateAsync(userId, id, request, context.RequestAborted), statusCode: 201);
        });

    private static Task<IResult> ListRoomsAsync(string id, HttpContext context, RoomService rooms, IRateLimitStore store, UserRateLimitOptions limits) =>
        Reading(context, store, limits, async userId =>
        {
            var page = UserEndpointSupport.QueryInteger(context, "page", PropertyQueryValidation.DefaultPage);
            var pageSize = UserEndpointSupport.QueryInteger(context, "pageSize", PropertyQueryValidation.DefaultPageSize);
            return Results.Json(await rooms.ListAsync(userId, id, page, pageSize, context.RequestAborted), statusCode: 200);
        });

    private static Task<IResult> UpdateRoomAsync(string id, HttpContext context, RoomService rooms, IRateLimitStore store, UserRateLimitOptions limits) =>
        Writing(context, store, limits, async userId =>
        {
            var request = await Body<UpdateRoomRequest>(context);
            return Results.Json(await rooms.UpdateAsync(userId, id, request, context.RequestAborted), statusCode: 200);
        });

    private static Task<IResult> DeleteRoomAsync(string id, HttpContext context, RoomService rooms, IRateLimitStore store, UserRateLimitOptions limits) =>
        Writing(context, store, limits, async userId =>
        {
            await rooms.DeleteAsync(userId, id, UserEndpointSupport.QueryText(context, "version"), context.RequestAborted);
            return Results.NoContent();
        });

    private static Task<IResult> DuplicateRoomAsync(string id, HttpContext context, RoomService rooms, IRateLimitStore store, UserRateLimitOptions limits) =>
        Writing(context, store, limits, async userId =>
            Results.Json(await rooms.DuplicateAsync(userId, id, context.RequestAborted), statusCode: 201));

    private static Task<IResult> CopyRoomAsync(string id, HttpContext context, RoomService rooms, IRateLimitStore store, UserRateLimitOptions limits) =>
        Writing(context, store, limits, async userId =>
        {
            var request = await Body<CopyRoomRequest>(context);
            return Results.Json(await rooms.CopyFromAsync(userId, id, request, context.RequestAborted), statusCode: 200);
        });

    private static Task<IResult> PauseRoomAsync(string id, HttpContext context, RoomService rooms, IRateLimitStore store, UserRateLimitOptions limits) =>
        Writing(context, store, limits, async userId =>
            Results.Json(await rooms.PauseAsync(userId, id, context.RequestAborted), statusCode: 200));

    private static Task<IResult> ResumeRoomAsync(string id, HttpContext context, RoomService rooms, IRateLimitStore store, UserRateLimitOptions limits) =>
        Writing(context, store, limits, async userId =>
            Results.Json(await rooms.ResumeAsync(userId, id, context.RequestAborted), statusCode: 200));

    private static Task<IResult> PublicationAsync(string id, HttpContext context, BuildingService buildings, IRateLimitStore store, UserRateLimitOptions limits) =>
        Reading(context, store, limits, async userId =>
            Results.Json(await buildings.PublicationAsync(userId, id, context.RequestAborted), statusCode: 200));

    private static Task<IResult> PublishAsync(string id, HttpContext context, BuildingService buildings, IRateLimitStore store, UserRateLimitOptions limits) =>
        Writing(context, store, limits, async userId =>
            Results.Json(await buildings.PublishAsync(userId, id, context.RequestAborted), statusCode: 202));

    private static Task<IResult> Reading(HttpContext context, IRateLimitStore store, UserRateLimitOptions limits, Func<string, Task<IResult>> action) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await UserEndpointSupport.EnforceAsync(context, store, RateLimitKeys.LandlordRead(userId), limits.LandlordReadsPerWindow, limits.Window, limits.RetryAfterSeconds);
            return await action(userId);
        });

    private static Task<IResult> Writing(HttpContext context, IRateLimitStore store, UserRateLimitOptions limits, Func<string, Task<IResult>> action) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await UserEndpointSupport.EnforceAsync(context, store, RateLimitKeys.LandlordWrite(userId), limits.LandlordWritesPerWindow, limits.Window, limits.RetryAfterSeconds);
            return await action(userId);
        });

    private static async Task<T> Body<T>(HttpContext context)
        where T : class =>
        (await UserEndpointSupport.ReadJsonAsync<T>(context, optional: false, MaximumBodyBytes))!;
}
