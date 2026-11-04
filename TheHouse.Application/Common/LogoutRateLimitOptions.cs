namespace TheHouse.Application.Common;

public sealed class LogoutRateLimitOptions
{
    public const int DefaultLogoutIpPerWindow = 30;
    public const int DefaultWindowSeconds = 60;

    public int LogoutIpPerWindow { get; init; } =
        DefaultLogoutIpPerWindow;

    public int WindowSeconds { get; init; } =
        DefaultWindowSeconds;

    public TimeSpan Window =>
        TimeSpan.FromSeconds(WindowSeconds);

    public int RetryAfterSeconds =>
        WindowSeconds;

    public bool IsValid() =>
        LogoutIpPerWindow > 0 &&
        WindowSeconds > 0;
}
