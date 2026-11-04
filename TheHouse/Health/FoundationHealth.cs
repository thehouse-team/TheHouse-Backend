using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TheHouse.Configuration;
using TheHouse.Http;

namespace TheHouse.Health;

public static class FoundationHealth
{
    public static void AddFoundationHealth(this IServiceCollection services)
    {
        services.AddHealthChecks();
        services.AddOptions<HealthCheckServiceOptions>().Configure<IOptions<FoundationOptions>>((options, limits) =>
            options.Registrations.Add(new HealthCheckRegistration("dependencies",
                provider => ActivatorUtilities.CreateInstance<DependencyReadinessCheck>(provider),
                HealthStatus.Unhealthy, ["ready"], TimeSpan.FromSeconds(limits.Value.HealthTimeoutSeconds))));
    }

    public static void MapFoundationHealth(this WebApplication app)
    {
        Map("/health/live", "Liveness", false);
        Map("/health/ready", "Readiness", true);

        void Map(string path, string name, bool readiness) => app.MapGet(path,
            (HttpContext context, HealthCheckService checks, IOptions<FoundationOptions> limits) =>
                CheckAsync(context, checks, limits.Value, readiness))
            .WithName(name).WithTags("Health").WithSummary(name)
            .Produces<HealthResponse>(200).ProducesProblem(503).ProducesProblem(500).ProducesProblem(504)
            .AllowAnonymous().DisableRateLimiting();
    }

    private static async Task<IResult> CheckAsync(HttpContext context, HealthCheckService checks,
        FoundationOptions limits, bool readiness)
    {
        context.Response.Headers.CacheControl = "no-store";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(limits.HealthTimeoutSeconds));
        try
        {
            var report = await checks.CheckHealthAsync(registration => readiness && registration.Tags.Contains("ready"), timeout.Token);
            return report.Status == HealthStatus.Healthy
                ? Results.Ok(new HealthResponse("Healthy"))
                : Results.Problem(ProblemResponses.Create(context, 503));
        }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            return Results.Problem(ProblemResponses.Create(context, 503));
        }
    }
}

public sealed record HealthResponse(string Status);
