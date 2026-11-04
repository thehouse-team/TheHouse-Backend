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

builder.Services.AddFoundationHttp();
builder.Services.AddFoundationOpenApi();
builder.Services.AddFoundationHealth();
builder.Services.AddFoundationRateLimits();
builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true; // Unicamente contenido pÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âºblico con opt-in explÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­cito.
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ["application/json", "text/plain"];
});

var app = builder.Build();

if (args.Contains(
        "--seed-universities",
        StringComparer.Ordinal))
{
    var seedOptions =
        app.Services.GetRequiredService<
            TheHouse.Application.Common.UniversitySeedOptions>();

    if (!seedOptions.Enabled)
    {
        Console.Error.WriteLine(
            "University seed is disabled. Set UniversitySeed__Enabled=true only in an authorized environment.");

        Environment.ExitCode = 2;
        return;
    }

    if (app.Environment.IsProduction())
    {
        Console.Error.WriteLine(
            "University seed is not allowed in Production.");

        Environment.ExitCode = 2;
        return;
    }

    await using var scope =
        app.Services.CreateAsyncScope();

    var seed =
        scope.ServiceProvider
            .GetRequiredService<
                TheHouse.Application.UseCases.UniversitySeedService>();

    await seed.SeedAsync(
        CancellationToken.None);

    Console.WriteLine(
        "University seed completed.");

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

app.UseHttpsRedirection();
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
