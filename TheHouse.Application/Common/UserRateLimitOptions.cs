namespace TheHouse.Application.Common;

// NV-15/NV-17: límites por usuario (llave = hash del `sub`) en Redis.
public sealed class UserRateLimitOptions
{
    public int FavoritesPerWindow { get; init; } = 60;

    public int VisitsPerWindow { get; init; } = 60;

    public int VisitCreatesPerDay { get; init; } = 10;

    // NV-11: perfil y preferencias por usuario; búsqueda pública por IP.
    public int ProfilePerWindow { get; init; } = 60;

    public int UniversitySearchPerWindow { get; init; } = 60;

    // NV-12: arrendador por usuario; generar cuartos 10 por hora.
    public int LandlordReadsPerWindow { get; init; } = 60;

    public int LandlordWritesPerWindow { get; init; } = 30;

    public int RoomGeneratesPerHour { get; init; } = 10;

    public TimeSpan GenerateWindow { get; init; } = TimeSpan.FromHours(1);

    public TimeSpan Window { get; init; } = TimeSpan.FromSeconds(60);

    public TimeSpan CreateWindow { get; init; } = TimeSpan.FromDays(1);

    public int RetryAfterSeconds => (int)Window.TotalSeconds;

    public int CreateRetryAfterSeconds => 3600;
}
