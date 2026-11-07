using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Domain.Entities;

namespace TheHouse.TestDoubles;

// NV-21: roles, revisiones, reportes, suspensiones y bitácora en memoria con
// la semántica de Mongo (un reporte abierto por reportante y objetivo; la
// bitácora solo se agrega). Se comparten con las pruebas unitarias.
public sealed class InMemoryModerationRepositories :
    IStaffRoleRepository, IVerificationReviewRepository, IReportRepository, IUserSuspensionRepository, IAuditRepository
{
    private readonly object gate = new();

    private int sequence;

    public Dictionary<string, StaffRoles> Roles { get; } = new(StringComparer.Ordinal);

    public List<VerificationReview> Reviews { get; } = [];

    public Dictionary<string, Report> Reports { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, UserSuspension> Suspensions { get; } = new(StringComparer.Ordinal);

    public List<AuditEvent> Audit { get; } = [];

    public bool Unavailable { get; set; }

    Task<StaffRoles?> IStaffRoleRepository.GetAsync(string userId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Roles.TryGetValue(userId, out var roles) && roles.Roles.Count > 0 ? roles : null);
        }
    }

    public Task<StaffRoles> GrantAsync(string userId, string role, string grantedBy, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var current = Roles.GetValueOrDefault(userId)?.Roles ?? Array.Empty<string>();
            var granted = new StaffRoles(userId, current.Append(role).Distinct(StringComparer.Ordinal).ToArray(), now, grantedBy);
            Roles[userId] = granted;
            return Task.FromResult(granted);
        }
    }

    public Task<bool> RevokeAsync(string userId, string role, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            if (!Roles.TryGetValue(userId, out var current) || !current.Roles.Contains(role, StringComparer.Ordinal))
            {
                return Task.FromResult(false);
            }

            Roles[userId] = current with { Roles = current.Roles.Where(item => item != role).ToArray() };
            return Task.FromResult(true);
        }
    }

    public Task AddAsync(VerificationReview review, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            Reviews.Add(review);
        }

        return Task.CompletedTask;
    }

    public Task<VerificationReview?> LatestAsync(string buildingId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Reviews.LastOrDefault(review => review.BuildingId == buildingId));
        }
    }

    public Task<Report?> CreateAsync(Report report, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            if (Reports.Values.Any(open => open.Status == "open" && open.ReporterId == report.ReporterId && open.TargetType == report.TargetType && open.TargetId == report.TargetId))
            {
                return Task.FromResult<Report?>(null);
            }

            var created = report with { Id = $"d0d0d0d0d0d0d0d0{++sequence:x8}" };
            Reports.Add(created.Id, created);
            return Task.FromResult<Report?>(created);
        }
    }

    Task<Report?> IReportRepository.GetAsync(string id, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Reports.GetValueOrDefault(id));
        }
    }

    Task<(IReadOnlyList<Report> Items, long TotalItems)> IReportRepository.ListAsync(string status, int page, int pageSize, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var matching = Reports.Values.Where(report => report.Status == status).OrderBy(report => report.CreatedAt).ThenBy(report => report.Id, StringComparer.Ordinal).ToList();
            IReadOnlyList<Report> slice = matching.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult((slice, (long)matching.Count));
        }
    }

    public Task<bool> CloseAsync(string id, string status, string resolution, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            if (!Reports.TryGetValue(id, out var report) || report.Status != "open")
            {
                return Task.FromResult(false);
            }

            Reports[id] = report with { Status = status, Resolution = resolution, ResolvedAt = now };
            return Task.FromResult(true);
        }
    }

    Task<UserSuspension?> IUserSuspensionRepository.GetAsync(string userId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Suspensions.GetValueOrDefault(userId));
        }
    }

    public Task<bool> CreateAsync(UserSuspension suspension, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Suspensions.TryAdd(suspension.UserId, suspension));
        }
    }

    public Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Suspensions.Remove(userId));
        }
    }

    public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            Audit.Add(auditEvent with { Id = $"e0e0e0e0e0e0e0e0{++sequence:x8}" });
        }

        return Task.CompletedTask;
    }

    Task<(IReadOnlyList<AuditEvent> Items, long TotalItems)> IAuditRepository.ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var ordered = Audit.AsEnumerable().Reverse().ToList();
            IReadOnlyList<AuditEvent> slice = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult((slice, (long)ordered.Count));
        }
    }

    private void Guard(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Unavailable)
        {
            throw new TheHouse.Application.Common.DependencyUnavailableException();
        }
    }
}
