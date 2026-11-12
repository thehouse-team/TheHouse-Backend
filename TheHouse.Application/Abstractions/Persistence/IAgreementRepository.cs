using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

// NV-18: `agreements` (módulo Rentals). Único parcial `{ roomId }` en
// convenios abiertos (pendientes o activos).
public interface IAgreementRepository
{
    // Devuelve null si el cuarto ya tiene un convenio abierto.
    Task<Agreement?> CreateAsync(Agreement agreement, CancellationToken cancellationToken);

    Task<Agreement?> GetAsync(string id, CancellationToken cancellationToken);

    // Convenio cuyo código vigente o usado tiene ese hash; si hubiera varios,
    // primero el pendiente y luego el más reciente.
    Task<Agreement?> FindByCodeHashAsync(string codeHash, CancellationToken cancellationToken);

    Task<Agreement?> FindByQrTokenHashAsync(string qrTokenHash, CancellationToken cancellationToken);

    // Reemplaza el convenio solo si nadie lo cambió desde `expectedUpdatedAt`.
    // Si el cambio choca con otro convenio abierto del cuarto, devuelve false.
    Task<bool> ReplaceIfUnchangedAsync(Agreement agreement, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken);

    // Suma un intento fallido al código vigente (operación atómica).
    Task RecordFailedAttemptAsync(string id, CancellationToken cancellationToken);

    // Orden: createdAt desc, id desc.
    Task<(IReadOnlyList<Agreement> Items, long TotalItems)> ListByOwnerAsync(
        string ownerId,
        AgreementStatus? status,
        string? roomId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Agreement?> GetActiveForStudentAsync(string studentId, CancellationToken cancellationToken);

    // NV-19: convenios activos o terminados del estudiante en ese cuarto.
    Task<bool> HasRentedAsync(string studentId, string roomId, CancellationToken cancellationToken);
}
