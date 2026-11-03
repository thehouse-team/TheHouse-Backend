using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TheHouse.Configuration;

namespace TheHouse.Http;

public static class FoundationRateLimits
{
    public const string PerEndpoint = "foundation-endpoint";

    public static void AddFoundationRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<FoundationOptions>>((options, limits) =>
        {
            var settings = limits.Value;
            FixedWindowRateLimiterOptions Window() => new()
            {
                PermitLimit = settings.RateLimitPermitLimit,
                Window = TimeSpan.FromSeconds(settings.RateLimitWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            };
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.GetEndpoint()?.Metadata.GetMetadata<DisableRateLimitingAttribute>() is not null
                    ? RateLimitPartition.GetNoLimiter("operational")
                    : RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => Window()));
            options.AddPolicy(PerEndpoint, context => RateLimitPartition.GetFixedWindowLimiter(
                $"{context.GetEndpoint()?.DisplayName}:{ClientKey(context)}", _ => Window()));
            options.RejectionStatusCode = 429;
            options.OnRejected = async (rejection, cancellationToken) =>
            {
                var retry = rejection.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait)
                    ? Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)) : settings.RateLimitWindowSeconds;
                rejection.HttpContext.Response.Headers.RetryAfter = retry.ToString(CultureInfo.InvariantCulture);
                await ProblemResponses.WriteAsync(rejection.HttpContext, 429);
            };
        });
    }

    private static string ClientKey(HttpContext context)
    {
        var subject = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        return subject is { Length: > 0 and <= 256 }
            ? "subject:" + subject
            : "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }
}
