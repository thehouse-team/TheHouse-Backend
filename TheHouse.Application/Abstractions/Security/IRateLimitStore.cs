using TheHouse.Application.Common;

namespace TheHouse.Application.Abstractions.Security;

public interface IRateLimitStore
{
    Task<RateLimitDecision> ConsumeAsync(
        string key,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken);
}
