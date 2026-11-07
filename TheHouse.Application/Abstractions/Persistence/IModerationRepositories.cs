using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

// NV-21: `staff_roles` (único por usuario).
public interface IStaffRoleRepository
{
    Task<StaffRoles?> GetAsync(string userId, CancellationToken cancellationToken);

    Task<StaffRoles> GrantAsync(string userId, string role, string grantedBy, DateTimeOffset now, CancellationToken cancellationToken);

    // false si el usuario no tenía ese rol.
    Task<bool> RevokeAsync(string userId, string role, CancellationToken cancellationToken);
}

// NV-21: historial de decisiones de revisión.
public interface IVerificationReviewRepository
{
    Task AddAsync(VerificationReview review, CancellationToken cancellationToken);

    Task<VerificationReview?> LatestAsync(string buildingId, CancellationToken cancellationToken);
}

// NV-21: reportes; uno abierto por reportante y objetivo.
public interface IReportRepository
{
    // null si el reportante ya tiene un reporte abierto de ese objetivo.
    Task<Report?> CreateAsync(Report report, CancellationToken cancellationToken);

    Task<Report?> GetAsync(string id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Report> Items, long TotalItems)> ListAsync(string status, int page, int pageSize, CancellationToken cancellationToken);

    // Cierra el reporte solo si sigue abierto.
    Task<bool> CloseAsync(string id, string status, string resolution, DateTimeOffset now, CancellationToken cancellationToken);
}

// NV-21: cuentas suspendidas.
public interface IUserSuspensionRepository
{
    Task<UserSuspension?> GetAsync(string userId, CancellationToken cancellationToken);

    Task<bool> CreateAsync(UserSuspension suspension, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken);
}

// NV-21: bitácora de solo inserción (no hay métodos de cambio ni borrado).
public interface IAuditRepository
{
    Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

    Task<(IReadOnlyList<AuditEvent> Items, long TotalItems)> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}
