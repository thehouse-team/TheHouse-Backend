namespace TheHouse.Domain.Exceptions;

public sealed class RateLimitExceededException : Exception
{
    public RateLimitExceededException(int retryAfterSeconds)
        : base("The request rate limit was exceeded.")
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    public int RetryAfterSeconds { get; }
}
