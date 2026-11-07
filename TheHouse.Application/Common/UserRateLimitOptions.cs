namespace TheHouse.Application.Common;

// NV-15/NV-17: límites por usuario (llave = hash del `sub`) en Redis.
public sealed class UserRateLimitOptions
{
    public int FavoritesPerWindow { get; init; } = 60;

    public int VisitsPerWindow { get; init; } = 60;

    public int VisitCreatesPerDay { get; init; } = 10;

    public TimeSpan Window { get; init; } = TimeSpan.FromSeconds(60);

    public TimeSpan CreateWindow { get; init; } = TimeSpan.FromDays(1);

    public int RetryAfterSeconds => (int)Window.TotalSeconds;

    public int CreateRetryAfterSeconds => 3600;
}
