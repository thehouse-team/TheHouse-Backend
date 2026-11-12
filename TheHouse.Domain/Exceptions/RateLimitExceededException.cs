namespace TheHouse.Domain.Exceptions;

// `Code` (opcional) distingue un bloqueo específico del límite general
// (NV-18: `too_many_attempts`).
public sealed class RateLimitExceededException : Exception
{
    public RateLimitExceededException(int retryAfterSeconds, string? code = null)
        : base("The request rate limit was exceeded.")
    {
        RetryAfterSeconds = retryAfterSeconds;
        Code = code;
    }

    public int RetryAfterSeconds { get; }

    public string? Code { get; }
}
