namespace TheHouse.Application.Common;

public sealed class PropertyRateLimitOptions
{
    public const int DefaultRequestsPerWindow = 60;
    public const int DefaultWindowSeconds = 60;

    public int RequestsPerWindow { get; init; } =
        DefaultRequestsPerWindow;

    public int WindowSeconds { get; init; } =
        DefaultWindowSeconds;

    public TimeSpan Window =>
        TimeSpan.FromSeconds(WindowSeconds);

    public int RetryAfterSeconds =>
        DefaultWindowSeconds;

    public bool IsValid =>
        RequestsPerWindow == DefaultRequestsPerWindow &&
        WindowSeconds == DefaultWindowSeconds;
}
