namespace TheHouse.Domain.Entities;

// NV-21: roles administrativos de una cuenta (`reviewer`, `admin`).
public sealed record StaffRoles(
    string UserId,
    IReadOnlyList<string> Roles,
    DateTimeOffset GrantedAt,
    string GrantedBy)
{
    public const string Reviewer = "reviewer";

    public const string Admin = "admin";

    // `admin` incluye todo lo que puede hacer `reviewer`.
    public bool Has(string role) =>
        Roles.Contains(Admin, StringComparer.Ordinal) || Roles.Contains(role, StringComparer.Ordinal);
}

public sealed record VerificationReview(
    string BuildingId,
    string ReviewerId,
    string Decision,
    string? Reason,
    DateTimeOffset CreatedAt);

public sealed record Report
{
    public string Id { get; init; } = string.Empty;

    public string ReporterId { get; init; } = string.Empty;

    public string TargetType { get; init; } = string.Empty;

    public string TargetId { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string? Details { get; init; }

    public string Status { get; init; } = "open";

    public string? Resolution { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }
}

public sealed record UserSuspension(string UserId, string Reason, string SuspendedBy, DateTimeOffset CreatedAt);

// Bitácora de solo inserción.
public sealed record AuditEvent(
    string Id,
    string ActorId,
    string Action,
    string TargetType,
    string TargetId,
    string? Reason,
    DateTimeOffset CreatedAt);
