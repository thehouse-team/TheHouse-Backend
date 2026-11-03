using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TheHouse.Application.Abstractions.Health;
using TheHouse.Infrastructure.Configuration;

namespace TheHouse.Health;

public sealed class DependencyReadinessCheck(IOptions<MongoOptions> mongo, IOptions<RedisOptions> redis,
    IMongoHealthProbe mongoProbe, IRedisHealthProbe redisProbe) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (mongo.Value.Enabled && !await mongoProbe.IsReadyAsync(cancellationToken))
                return HealthCheckResult.Unhealthy();
            if (redis.Value.Enabled && !await redisProbe.IsReadyAsync(cancellationToken))
                return HealthCheckResult.Unhealthy();
            return HealthCheckResult.Healthy();
        }
        catch (Exception)
        {
            // No adjuntar excepciones del adaptador al resultado/log de health.
            return HealthCheckResult.Unhealthy();
        }
    }
}
