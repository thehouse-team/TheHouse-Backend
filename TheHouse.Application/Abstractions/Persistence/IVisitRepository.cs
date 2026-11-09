using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

// NV-17: visitas y registros de idempotencia (24 h).
public interface IVisitRepository
{
    Task<Visit> CreateAsync(Visit visit, CancellationToken cancellationToken);

    Task<Visit?> GetAsync(string id, CancellationToken cancellationToken);

    Task UpdateAsync(Visit visit, CancellationToken cancellationToken);

    // Visitas Requested con horario posterior a `now`.
    Task<IReadOnlyList<Visit>> ListActiveAsync(
        string userId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    // Orden: scheduledAt asc, id asc.
    Task<(IReadOnlyList<Visit> Items, long TotalItems)> ListAsync(
        string userId,
        VisitStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<VisitIdempotencyRecord?> FindIdempotencyAsync(
        string userId,
        string key,
        CancellationToken cancellationToken);

    // Migración: asigna `roomId` a las visitas que solo tienen `propertyId`.
    Task<MigrationCounts> MigrateLegacyAsync(IReadOnlyDictionary<string, string> roomByProperty, CancellationToken cancellationToken);

    Task SaveIdempotencyAsync(
        VisitIdempotencyRecord record,
        CancellationToken cancellationToken);
}

public sealed record VisitIdempotencyRecord(
    string UserId,
    string Key,
    string RequestHash,
    string VisitId,
    DateTimeOffset CreatedAt);
