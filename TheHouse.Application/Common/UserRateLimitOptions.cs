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

    // NV-21: panel interno por usuario; reportes 60/min y 10 por día.
    public int AdminPerWindow { get; init; } = 120;

    public int ReportsPerWindow { get; init; } = 60;

    public int ReportCreatesPerDay { get; init; } = 10;

    // NV-14: búsqueda pública por IP y detalle por usuario.
    public int SearchPerWindow { get; init; } = 60;

    public int RoomDetailPerWindow { get; init; } = 60;

    // NV-16: 60/min general, 10 solicitudes nuevas al día, 30 mensajes/min y
    // 12 consultas de mensajes por minuto por conversación.
    public int MessagingPerWindow { get; init; } = 60;

    public int RequestCreatesPerDay { get; init; } = 10;

    public int MessageSendsPerWindow { get; init; } = 30;

    public int MessagePollsPerWindow { get; init; } = 12;

    // NV-18: 60/min general; código de cierre 10/min por usuario, 30/min por
    // IP y 5 intentos por cuenta en 15 minutos (RNF-17).
    public int RentalsPerWindow { get; init; } = 60;

    public int ClosingCodePerWindow { get; init; } = 10;

    public int ClosingCodeIpPerWindow { get; init; } = 30;

    public int ClosingCodeAttempts { get; init; } = 5;

    public TimeSpan ClosingCodeAttemptsWindow { get; init; } = TimeSpan.FromMinutes(15);

    public int ClosingCodeAttemptsRetryAfterSeconds => (int)ClosingCodeAttemptsWindow.TotalSeconds;

    public TimeSpan Window { get; init; } = TimeSpan.FromSeconds(60);

    public TimeSpan CreateWindow { get; init; } = TimeSpan.FromDays(1);

    public int RetryAfterSeconds => (int)Window.TotalSeconds;

    public int CreateRetryAfterSeconds => 3600;
}
