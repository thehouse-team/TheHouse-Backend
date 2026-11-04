namespace TheHouse.Application.Common;

public sealed record RateLimitDecision(bool IsAllowed, long Count, TimeSpan RetryAfter);
