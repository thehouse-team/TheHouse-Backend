namespace TheHouse.Application.Common;

// NV-10: vigencias de la sesión y límites del endpoint de refresh.
public sealed class RefreshTokenOptions
{
    public const int DefaultTokenLifetimeDays = 30;

    public const int DefaultSessionLifetimeDays = 90;

    public int TokenLifetimeDays { get; init; } =
        DefaultTokenLifetimeDays;

    public int SessionLifetimeDays { get; init; } =
        DefaultSessionLifetimeDays;

    // Debe cubrir la vida del access token más la tolerancia de reloj
    // (900 s + 30 s en NV-05) para rechazar todo access token de una
    // sesión revocada hasta que expire por sí solo.
    public int RevokedSessionTtlSeconds { get; init; } = 930;

    public int RefreshIpPerWindow { get; init; } = 30;

    public int WindowSeconds { get; init; } = 60;

    public TimeSpan TokenLifetime =>
        TimeSpan.FromDays(TokenLifetimeDays);

    public TimeSpan SessionLifetime =>
        TimeSpan.FromDays(SessionLifetimeDays);

    public TimeSpan RevokedSessionTtl =>
        TimeSpan.FromSeconds(RevokedSessionTtlSeconds);

    public TimeSpan Window =>
        TimeSpan.FromSeconds(WindowSeconds);

    public int RetryAfterSeconds => WindowSeconds;

    public bool IsValid() =>
        TokenLifetimeDays is >= 1 and <= 30 &&
        SessionLifetimeDays is >= 1 and <= 90 &&
        TokenLifetimeDays <= SessionLifetimeDays &&
        RevokedSessionTtlSeconds is >= 930 and <= 3660 &&
        RefreshIpPerWindow is >= 1 and <= 1000 &&
        WindowSeconds is >= 1 and <= 3600;
}
