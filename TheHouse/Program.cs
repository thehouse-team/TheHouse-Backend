using TheHouse.Configuration;
using TheHouse.Http;
using TheHouse.Health;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);
builder.AddFoundationConfiguration();
builder.AddRegistration();
builder.AddLogin();
builder.AddLogout();
builder.AddProperties();
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

var seedUniversities =
    args.Contains(
        "--seed-universities",
        StringComparer.Ordinal);

var seedProperties =
    args.Contains(
        "--seed-properties",
        StringComparer.Ordinal);

if (seedUniversities || seedProperties)
{
    var universitySeedOptions =
        app.Services.GetRequiredService<
            TheHouse.Application.Common.UniversitySeedOptions>();

    var propertySeedOptions =
        app.Services.GetRequiredService<
            TheHouse.Application.Common.PropertySeedOptions>();

    if (seedUniversities &&
        !universitySeedOptions.Enabled)
    {
        Console.Error.WriteLine(
            "University seed is disabled. Set UniversitySeed__Enabled=true only in an authorized environment.");

        Environment.ExitCode = 2;
        return;
    }

    if (seedProperties &&
        !propertySeedOptions.Enabled)
    {
        Console.Error.WriteLine(
            "Property seed is disabled. Set PropertySeed__Enabled=true only in an authorized environment.");

        Environment.ExitCode = 2;
        return;
    }

    if (app.Environment.IsProduction())
    {
        Console.Error.WriteLine(
            "Seed commands are not allowed in Production.");

        Environment.ExitCode = 2;
        return;
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

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<FoundationExceptionMiddleware>();
app.UseForwardedHeaders();
app.UseStatusCodePages(context => ProblemResponses.WriteAsync(context.HttpContext, context.HttpContext.Response.StatusCode));
app.UseMiddleware<RequestBodyLimitMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
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
app.MapProperties();
app.MapUniversities();

app.Run();

public partial class Program;
