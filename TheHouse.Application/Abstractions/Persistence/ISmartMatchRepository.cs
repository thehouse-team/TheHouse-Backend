namespace TheHouse.Application.Abstractions.Persistence;

// NV-23 1.2.0: colecciones propias del módulo SmartMatch
// (`smart_match_feedback`, `smart_match_dismissals`, `smart_match_daily_stats`).
public interface ISmartMatchRepository
{
    // La última opinión del usuario sobre el cuarto reemplaza a la anterior.
    Task SaveFeedbackAsync(string userId, string roomId, bool useful, string modelVersion, DateTimeOffset now, CancellationToken cancellationToken);

    // Idempotente: repetirlo reinicia los 30 días.
    Task AddDismissalAsync(string userId, string roomId, DateTimeOffset now, CancellationToken cancellationToken);

    Task RemoveDismissalAsync(string userId, string roomId, CancellationToken cancellationToken);

    // Descartes vigentes (el TTL de MongoDB puede tardar en borrar los vencidos).
    Task<IReadOnlySet<string>> ListDismissedRoomIdsAsync(string userId, DateTimeOffset since, CancellationToken cancellationToken);

    Task<long> CountActiveDismissalsAsync(DateTimeOffset since, CancellationToken cancellationToken);

    Task<IReadOnlyList<SmartMatchFeedbackCount>> CountFeedbackAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    // Suma atómica (`$inc`) a los contadores del día y la versión del modelo.
    Task IncrementStatsAsync(SmartMatchDailyStats delta, CancellationToken cancellationToken);

    Task<IReadOnlyList<SmartMatchDailyStats>> ListStatsAsync(string fromDay, string toDay, CancellationToken cancellationToken);

    // NV-10: borra la retroalimentación y los descartes del usuario.
    Task DeleteUserAsync(string userId, CancellationToken cancellationToken);
}

public sealed record SmartMatchFeedbackCount(string ModelVersion, bool Useful, long Count);

public sealed record SmartMatchDailyStats(
    string Day,
    string ModelVersion,
    long ScoreRequests,
    long RecommendationRequests,
    long GuestRequests,
    long RoomsScored,
    long EligibleRooms,
    long ScoreSum,
    long Bucket0To39,
    long Bucket40To59,
    long Bucket60To79,
    long Bucket80To100);

// Registro de métricas que nunca hace fallar la respuesta al usuario.
public interface ISmartMatchStatsSink
{
    Task RecordAsync(SmartMatchDailyStats delta, CancellationToken cancellationToken);
}
