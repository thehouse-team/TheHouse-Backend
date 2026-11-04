namespace TheHouse.Application.Common;

public sealed class RateLimitOptions
{
    public const int DefaultLoginEmailPerWindow = 5;
    public const int DefaultLoginIpPerWindow = 20;
    public const int DefaultWindowSeconds = 60;

    public int LoginEmailPerWindow { get; init; } =
        DefaultLoginEmailPerWindow;

    public int LoginIpPerWindow { get; init; } =
        DefaultLoginIpPerWindow;

    public int WindowSeconds { get; init; } =
        DefaultWindowSeconds;

    public TimeSpan Window =>
        TimeSpan.FromSeconds(WindowSeconds);

    public int RetryAfterSeconds =>
        WindowSeconds;

    public bool IsValid() =>
        LoginEmailPerWindow > 0 &&
        LoginIpPerWindow >= LoginEmailPerWindow &&
        WindowSeconds > 0;
}
