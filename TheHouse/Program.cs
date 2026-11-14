using TheHouse.Configuration;
using TheHouse.Http;
using TheHouse.Health;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);
builder.AddFoundationConfiguration();
builder.AddRegistration();
builder.AddLogin();
builder.AddLogout();
builder.AddSessions();
builder.AddProperties();
builder.AddFavoritesAndVisits();
builder.AddProfile();
builder.AddLandlord();
builder.AddModeration();
builder.AddSearch();
builder.AddMessaging();
builder.AddRentals();
builder.AddNotifications();
builder.AddReviews();
builder.AddUniversities();
builder.AddObservability();

builder.Services.AddFoundationHttp();
builder.Services.AddFoundationOpenApi();
builder.Services.AddFoundationHealth();
builder.Services.AddFoundationRateLimits();
builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(30);
    options.IncludeSubDomains = false;
    options.Preload = false;
});
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true; // Unicamente contenido público con opt-in explícito.
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ["application/json", "text/plain"];
});

var app = builder.Build();

// NV-21: asignación de roles por línea de comandos (nunca por la API).
var roleCommand = RoleCommand.Parse(args);
if (roleCommand is not null)
{
    if (!RoleCommand.Allowed(args, app.Environment.IsProduction()))
    {
        Console.Error.WriteLine($"Role changes are not allowed in Production without {RoleCommand.AllowProductionRoleChange}.");
        Environment.ExitCode = 2;
        return;
    }

    Environment.ExitCode = await roleCommand.RunAsync(app.Services, app.Logger);
    return;
}

// NV-15/NV-17 1.2.0: migración autorizada de favoritos y visitas a cuartos.
if (args.Contains(SearchConfiguration.MigrateToRooms, StringComparer.Ordinal))
{
    var migrationRejection = SearchConfiguration.RejectMigration(
        args,
        app.Services.GetRequiredService<TheHouse.Application.UseCases.RoomMigrationOptions>().Enabled,
        app.Environment.IsProduction());
    if (migrationRejection is not null)
    {
        Console.Error.WriteLine(migrationRejection);
        Environment.ExitCode = 2;
        return;
    }

    try
    {
        var (favoriteCounts, visitCounts) = await app.Services.GetRequiredService<TheHouse.Application.UseCases.RoomMigrationService>().MigrateAsync(CancellationToken.None);
        app.Logger.LogWarning(
            "Migración a cuartos: favoritos migrados={Favorites}, duplicados eliminados={Duplicates}, sin mapeo={FavoritesUnmapped}; visitas migradas={Visits}, sin mapeo={VisitsUnmapped}.",
            favoriteCounts.Migrated,
            favoriteCounts.DuplicatesRemoved,
            favoriteCounts.Unmapped,
            visitCounts.Migrated,
            visitCounts.Unmapped);
        Console.WriteLine("Room migration completed.");
    }
    catch (InvalidOperationException exception)
    {
        Console.Error.WriteLine(exception.Message);
        Environment.ExitCode = 2;
    }

    return;
}

// NV-14: seeds autorizados de colonias y de cuartos de demostración.
var seedPlaces = args.Contains(SearchConfiguration.SeedPlaces, StringComparer.Ordinal);
var seedDemoRooms = args.Contains(SearchConfiguration.SeedDemoRooms, StringComparer.Ordinal);
if (seedPlaces || seedDemoRooms)
{
    var rejection = SearchConfiguration.Reject(
        args,
        app.Services.GetRequiredService<TheHouse.Application.UseCases.PlaceSeedOptions>().Enabled,
        app.Services.GetRequiredService<TheHouse.Application.UseCases.DemoRoomSeedOptions>().Enabled,
        app.Environment.IsProduction());
    if (rejection is not null)
    {
        Console.Error.WriteLine(rejection);
        Environment.ExitCode = 2;
        return;
    }

    app.Logger.LogWarning("Seed autorizado: colonias={Places}, cuartos de demostración={Demo}.", seedPlaces, seedDemoRooms);
    try
    {
        if (seedPlaces)
        {
            var places = await app.Services.GetRequiredService<TheHouse.Application.UseCases.PlaceSeedService>().SeedAsync(CancellationToken.None);
            Console.WriteLine($"Place seed completed ({places}).");
        }

        if (seedDemoRooms)
        {
            var rooms = await app.Services.GetRequiredService<TheHouse.Application.UseCases.DemoRoomSeedService>().SeedAsync(CancellationToken.None);
            Console.WriteLine($"Demo room seed completed ({rooms} created).");
        }
    }
    catch (InvalidOperationException exception)
    {
        Console.Error.WriteLine(exception.Message);
        Environment.ExitCode = 2;
    }

    return;
}

var seedUniversities =
    args.Contains(
        SeedCommandPolicy.SeedUniversities,
        StringComparer.Ordinal);

var seedProperties =
    args.Contains(
        SeedCommandPolicy.SeedProperties,
        StringComparer.Ordinal);

if (seedUniversities || seedProperties)
{
    var universitySeedOptions =
        app.Services.GetRequiredService<
            TheHouse.Application.Common.UniversitySeedOptions>();

    var propertySeedOptions =
        app.Services.GetRequiredService<
            TheHouse.Application.Common.PropertySeedOptions>();

    var decision = SeedCommandPolicy.Evaluate(
        args,
        universitySeedOptions.Enabled,
        propertySeedOptions.Enabled,
        app.Environment.IsProduction());

    var rejection = decision switch
    {
        SeedCommandDecision.UniversitySeedDisabled =>
            "University seed is disabled. Set UniversitySeed__Enabled=true only in an authorized environment.",
        SeedCommandDecision.PropertySeedDisabled =>
            "Property seed is disabled. Set PropertySeed__Enabled=true only in an authorized environment.",
        SeedCommandDecision.ProductionNotAllowed =>
            $"Seed commands are not allowed in Production without {SeedCommandPolicy.AllowProductionSeed}.",
        _ => null
    };

    if (rejection is not null)
    {
        Console.Error.WriteLine(rejection);

        Environment.ExitCode = 2;
        return;
    }

    if (decision == SeedCommandDecision.RunInProduction)
    {
        app.Logger.LogWarning(
            "Seed autorizado en Production mediante {Flag}: universidades={Universities}, propiedades={Properties}.",
            SeedCommandPolicy.AllowProductionSeed,
            seedUniversities,
            seedProperties);
    }

    await using var scope =
        app.Services.CreateAsyncScope();

    if (seedUniversities)
    {
        var universitySeed =
            scope.ServiceProvider
                .GetRequiredService<
                    TheHouse.Application.UseCases.UniversitySeedService>();

        await universitySeed.SeedAsync(
            CancellationToken.None);

        Console.WriteLine(
            "University seed completed.");
    }

    if (seedProperties)
    {
        var propertySeed =
            scope.ServiceProvider
                .GetRequiredService<
                    TheHouse.Application.UseCases.PropertySeedService>();

        try
        {
            await propertySeed.SeedAsync(
                CancellationToken.None);
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(
                exception.Message);

            Environment.ExitCode = 2;
            return;
        }

        Console.WriteLine(
            "Property seed completed.");
    }

    return;
}

// NV-01-R14: primero, para cubrir todas las respuestas.
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<FoundationExceptionMiddleware>();
app.UseForwardedHeaders();
app.UseStatusCodePages(context => ProblemResponses.WriteAsync(context.HttpContext, context.HttpContext.Response.StatusCode));
app.UseMiddleware<RequestBodyLimitMiddleware>();

// Configure the HTTP request pipeline.
if (ApiConventions.ExposesOpenApi(app.Environment))
{
    app.MapOpenApi().WithMetadata(new PublicResponseCompressionAttribute()).DisableRateLimiting();
}
else
{
    app.UseHsts();
}

app.UseWhen(
    context =>
        !context.Request.Path.StartsWithSegments("/health"),
    branch =>
        branch.UseHttpsRedirection());
app.UseRouting();
app.UseRequestTimeouts();
app.UseMiddleware<PublicResponseCompressionMiddleware>();
app.UseRateLimiter();
app.UseMiddleware<TokenRevocationMiddleware>();

app.UseAuthorization();

app.MapControllers();
app.MapFoundationHealth();
app.MapRegistration();
app.MapLogin();
app.MapLogout();
app.MapRefresh();
app.MapUsersMe();
app.MapProperties();
app.MapFavorites();
app.MapVisits();
app.MapLandlordVisits();
app.MapProfile();
app.MapUniversitySearch();
app.MapLandlord();
app.MapAdmin();
app.MapSearch();
app.MapMessaging();
app.MapRentals();
app.MapNotifications();
app.MapReviews();
app.MapUniversities();

app.Run();

public partial class Program;
