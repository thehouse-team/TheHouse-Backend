using TheHouse.Application.Abstractions.Notifications;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Landlord;
using TheHouse.Application.Contracts.Moderation;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-21: catálogos y validaciones de moderación.
public static class ModerationRules
{
    public static readonly IReadOnlySet<string> ReportTargets = new HashSet<string>(StringComparer.Ordinal) { "building", "room", "user", "review" };

    public static readonly IReadOnlySet<string> ReportReasons = new HashSet<string>(StringComparer.Ordinal) { "fake", "fraud", "inappropriate", "spam", "other" };

    public static readonly IReadOnlySet<string> ReportStatuses = new HashSet<string>(StringComparer.Ordinal) { "open", "resolved", "dismissed" };

    public static readonly IReadOnlySet<string> ReportActions = new HashSet<string>(StringComparer.Ordinal) { "dismiss", "suspend_building", "suspend_user", "hide_review" };

    public static readonly IReadOnlySet<string> StaffRoleNames = new HashSet<string>(StringComparer.Ordinal) { StaffRoles.Reviewer, StaffRoles.Admin };

    public static string? Reason(string? value, bool required)
    {
        if (value is null && !required)
        {
            return null;
        }

        return ListingRules.Text("reason", value, 5, 500);
    }

    public static string ObjectId(string field, string? value) =>
        PropertyQueryValidation.IsObjectId(value)
            ? value!.ToLowerInvariant()
            : throw new RequestValidationException(field, $"{field} must be a 24-character hexadecimal ObjectId.");
}

// NV-21: consulta de suspensión que usa Identity en login y refresh.
public sealed class AccountSuspensionQuery(IUserSuspensionRepository suspensions) : IAccountSuspensionQuery
{
    public async Task<bool> IsSuspendedAsync(string userId, CancellationToken cancellationToken) =>
        await suspensions.GetAsync(userId, cancellationToken) is not null;
}

// NV-21: roles administrativos, consultados en cada solicitud, y bitácora.
public sealed class StaffService(
    IStaffRoleRepository roles,
    IAuditRepository audit,
    IUserRepository users,
    TimeProvider timeProvider)
{
    public async Task RequireAsync(string userId, string role, CancellationToken cancellationToken)
    {
        var granted = await roles.GetAsync(userId, cancellationToken);
        if (granted is null || !granted.Has(role))
        {
            throw new ForbiddenException("role_required");
        }
    }

    public async Task<bool> IsAdminAsync(string userId, CancellationToken cancellationToken) =>
        (await roles.GetAsync(userId, cancellationToken))?.Roles.Contains(StaffRoles.Admin, StringComparer.Ordinal) == true;

    // Solo desde la línea de comandos (`--grant-role`), nunca por la API.
    public async Task<string> GrantByEmailAsync(string? email, string? role, string actor, CancellationToken cancellationToken)
    {
        var (userId, validRole) = await ResolveAsync(email, role, cancellationToken);
        await roles.GrantAsync(userId, validRole, actor, timeProvider.GetUtcNow(), cancellationToken);
        await AuditAsync(actor, "role.granted", "user", userId, validRole, cancellationToken);
        return userId;
    }

    public async Task<string> RevokeByEmailAsync(string? email, string? role, string actor, CancellationToken cancellationToken)
    {
        var (userId, validRole) = await ResolveAsync(email, role, cancellationToken);
        if (!await roles.RevokeAsync(userId, validRole, cancellationToken))
        {
            throw new ConflictException("role_not_granted");
        }

        await AuditAsync(actor, "role.revoked", "user", userId, validRole, cancellationToken);
        return userId;
    }

    public Task AuditAsync(string actorId, string action, string targetType, string targetId, string? reason, CancellationToken cancellationToken) =>
        audit.AppendAsync(new AuditEvent(string.Empty, actorId, action, targetType, targetId, reason, timeProvider.GetUtcNow()), cancellationToken);

    public async Task<AuditListResponse> AuditLogAsync(string userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await RequireAsync(userId, StaffRoles.Admin, cancellationToken);
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        var (items, total) = await audit.ListAsync(page, pageSize, cancellationToken);
        return new AuditListResponse(
            items.Select(item => new AuditEventResponse(item.Id, item.ActorId, item.Action, item.TargetType, item.TargetId, item.Reason, item.CreatedAt)).ToArray(),
            page,
            pageSize,
            total,
            PropertyQueryValidation.CalculateTotalPages(total, pageSize));
    }

    private async Task<(string UserId, string Role)> ResolveAsync(string? email, string? role, CancellationToken cancellationToken)
    {
        if (role is null || !ModerationRules.StaffRoleNames.Contains(role))
        {
            throw new RequestValidationException("role", "role must be reviewer or admin.");
        }

        var normalized = EmailNormalizer.Normalize(email);
        if (!EmailNormalizer.IsValid(normalized))
        {
            throw new RequestValidationException("email", "email must be a valid address.");
        }

        var user = await users.GetByEmailAsync(normalized, cancellationToken) ?? throw new NotFoundException("user");
        return (user.Id, role);
    }
}

// NV-21: revisión de edificios enviados por NV-12.
public sealed class ReviewService(
    StaffService staff,
    BuildingService buildingService,
    IBuildingRepository buildings,
    IRoomRepository rooms,
    IVerificationReviewRepository reviews,
    ILandlordProfileRepository landlords,
    IUniversityRepository universities,
    TimeProvider timeProvider,
    INotificationSink? notifications = null)
{
    private static readonly Dictionary<string, BuildingStatus> Statuses = new(StringComparer.Ordinal)
    {
        ["draft"] = BuildingStatus.Draft,
        ["in_review"] = BuildingStatus.InReview,
        ["verified"] = BuildingStatus.Verified,
        ["rejected"] = BuildingStatus.Rejected,
        ["suspended"] = BuildingStatus.Suspended,
    };

    public async Task<AdminBuildingListResponse> QueueAsync(string userId, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        await staff.RequireAsync(userId, StaffRoles.Reviewer, cancellationToken);
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        var filter = Statuses.TryGetValue(status ?? "in_review", out var value)
            ? value
            : throw new RequestValidationException("status", "status has an unknown value.");

        var (items, total) = await buildings.ListByStatusAsync(filter, page, pageSize, cancellationToken);
        var responses = new List<BuildingResponse>();
        foreach (var building in items)
        {
            responses.Add(await ResponseAsync(building, cancellationToken));
        }

        return new AdminBuildingListResponse(responses, page, pageSize, total, PropertyQueryValidation.CalculateTotalPages(total, pageSize));
    }

    public async Task<AdminBuildingDetailResponse> DetailAsync(string userId, string? id, CancellationToken cancellationToken)
    {
        await staff.RequireAsync(userId, StaffRoles.Reviewer, cancellationToken);
        var building = await LoadAsync(id, cancellationToken);
        var buildingRooms = await rooms.ListAllByBuildingAsync(building.Id, cancellationToken);
        var landlord = await landlords.GetAsync(building.OwnerId, cancellationToken);
        var university = await universities.GetByIdAsync(building.NearestUniversityId, cancellationToken);

        return new AdminBuildingDetailResponse(
            BuildingService.ToResponse(building, university, buildingRooms),
            buildingRooms.OrderBy(room => room.Floor).ThenBy(room => room.Number).Select(RoomService.ToResponse).ToArray(),
            landlord is null ? null : new ReviewLandlord(landlord.FullName, landlord.RentalMode, landlord.Rfc),
            await LastDecisionAsync(building.Id, cancellationToken));
    }

    public async Task<BuildingResponse> DecideAsync(string userId, string? id, string decision, DecisionRequest request, CancellationToken cancellationToken)
    {
        await staff.RequireAsync(userId, StaffRoles.Reviewer, cancellationToken);
        var building = await LoadAsync(id, cancellationToken);
        if (building.OwnerId == userId)
        {
            throw new ForbiddenException("own_building");
        }

        var expected = request.Version ?? throw new RequestValidationException("version", "version is required.");
        var reason = ModerationRules.Reason(request.Reason, required: decision != "approved");
        if (expected != building.Version)
        {
            throw new ConflictException("version_conflict");
        }

        var now = timeProvider.GetUtcNow();
        var (changed, roomStatus) = decision switch
        {
            "approved" => (building.Approve(now), RoomStatus.Available),
            "changes_requested" => (building.RequestChanges(now), RoomStatus.Ready),
            "rejected" => (building.Reject(now), RoomStatus.Ready),
            _ => throw new ArgumentOutOfRangeException(nameof(decision)),
        };

        changed = changed with { Version = expected + 1 };
        if (!await buildings.ReplaceAsync(changed, expected, cancellationToken))
        {
            throw new ConflictException("version_conflict");
        }

        await rooms.SetStatusByBuildingAsync(building.Id, RoomStatus.InReview, roomStatus, now, cancellationToken);
        await reviews.AddAsync(new VerificationReview(building.Id, userId, decision, reason, now), cancellationToken);
        await staff.AuditAsync(userId, "building." + decision, "building", building.Id, reason, cancellationToken);
        var type = decision switch
        {
            "approved" => NotificationTypes.BuildingApproved,
            "changes_requested" => NotificationTypes.BuildingChangesRequested,
            _ => NotificationTypes.BuildingRejected,
        };
        if (notifications is not null)
        {
            await notifications.NotifyAsync(
                new NotificationEvent(type, changed.OwnerId, changed.Id, $"{type}:{changed.Id}:{changed.Version}", BuildingId: changed.Id),
                cancellationToken);
        }

        return await ResponseAsync(changed, cancellationToken);
    }

    public async Task<VerificationResponse> OwnerVerificationAsync(string userId, string? id, CancellationToken cancellationToken)
    {
        var building = await buildingService.LoadOwnedAsync(userId, id, cancellationToken);
        return new VerificationResponse(ListingRules.BuildingStatusText(building.Status), await LastDecisionAsync(building.Id, cancellationToken));
    }

    private async Task<LastDecision?> LastDecisionAsync(string buildingId, CancellationToken cancellationToken) =>
        await reviews.LatestAsync(buildingId, cancellationToken) is { } review
            ? new LastDecision(review.Decision, review.Reason, review.CreatedAt)
            : null;

    private async Task<Building> LoadAsync(string? id, CancellationToken cancellationToken) =>
        await buildings.GetAsync(ModerationRules.ObjectId("id", id), cancellationToken) ?? throw new NotFoundException("building");

    private async Task<BuildingResponse> ResponseAsync(Building building, CancellationToken cancellationToken)
    {
        var university = await universities.GetByIdAsync(building.NearestUniversityId, cancellationToken);
        var buildingRooms = await rooms.ListAllByBuildingAsync(building.Id, cancellationToken);
        return BuildingService.ToResponse(building, university, buildingRooms);
    }
}

// NV-21: suspensión de edificios y cuentas.
public sealed class ModerationService(
    StaffService staff,
    IBuildingRepository buildings,
    IUserSuspensionRepository suspensions,
    IUserRepository users,
    IRefreshTokenStore refreshTokens,
    SessionService sessions,
    TimeProvider timeProvider,
    INotificationSink? notifications = null)
{
    public const string SuspendedReason = "suspended";

    public Task<string> SuspendBuildingAsync(string actorId, string? id, ReasonRequest request, CancellationToken cancellationToken) =>
        ChangeBuildingAsync(actorId, id, request, (building, now) => building.Suspend(now), "building.suspended", cancellationToken);

    public Task<string> UnsuspendBuildingAsync(string actorId, string? id, ReasonRequest request, CancellationToken cancellationToken) =>
        ChangeBuildingAsync(actorId, id, request, (building, now) => building.Unsuspend(now), "building.unsuspended", cancellationToken);

    public async Task<AccountStatusResponse> SuspendUserAsync(string actorId, string? id, ReasonRequest request, CancellationToken cancellationToken)
    {
        await staff.RequireAsync(actorId, StaffRoles.Admin, cancellationToken);
        var userId = ModerationRules.ObjectId("id", id);
        var reason = ModerationRules.Reason(request.Reason, required: true)!;
        _ = await users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("user");

        if (await staff.IsAdminAsync(userId, cancellationToken))
        {
            throw new ConflictException("user_state");
        }

        var now = timeProvider.GetUtcNow();
        if (!await suspensions.CreateAsync(new UserSuspension(userId, reason, actorId, now), cancellationToken))
        {
            throw new ConflictException("user_state");
        }

        // Sesiones: Redis rechaza de inmediato sus access tokens y se revocan
        // sus refresh tokens; login y refresh quedan bloqueados.
        foreach (var sessionId in await refreshTokens.ListActiveSessionIdsAsync(userId, now, cancellationToken))
        {
            await sessions.RevokeSessionAsync(sessionId, SuspendedReason, cancellationToken);
        }

        foreach (var building in (await buildings.ListAllByOwnerAsync(userId, cancellationToken)).Where(building => building.Status == BuildingStatus.Verified))
        {
            var suspended = building.Suspend(now) with { Version = building.Version + 1 };
            await buildings.ReplaceAsync(suspended, building.Version, cancellationToken);
        }

        await staff.AuditAsync(actorId, "user.suspended", "user", userId, reason, cancellationToken);
        return new AccountStatusResponse(userId, true, reason);
    }

    public async Task<AccountStatusResponse> UnsuspendUserAsync(string actorId, string? id, ReasonRequest request, CancellationToken cancellationToken)
    {
        await staff.RequireAsync(actorId, StaffRoles.Admin, cancellationToken);
        var userId = ModerationRules.ObjectId("id", id);
        var reason = ModerationRules.Reason(request.Reason, required: true);
        _ = await users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("user");

        if (!await suspensions.DeleteAsync(userId, cancellationToken))
        {
            throw new ConflictException("user_state");
        }

        await staff.AuditAsync(actorId, "user.unsuspended", "user", userId, reason, cancellationToken);
        return new AccountStatusResponse(userId, false, null);
    }

    private async Task<string> ChangeBuildingAsync(
        string actorId,
        string? id,
        ReasonRequest request,
        Func<Building, DateTimeOffset, Building> change,
        string action,
        CancellationToken cancellationToken)
    {
        await staff.RequireAsync(actorId, StaffRoles.Admin, cancellationToken);
        var reason = ModerationRules.Reason(request.Reason, required: true);
        var building = await buildings.GetAsync(ModerationRules.ObjectId("id", id), cancellationToken) ?? throw new NotFoundException("building");
        var changed = change(building, timeProvider.GetUtcNow()) with { Version = building.Version + 1 };
        if (!await buildings.ReplaceAsync(changed, building.Version, cancellationToken))
        {
            throw new ConflictException("version_conflict");
        }

        await staff.AuditAsync(actorId, action, "building", building.Id, reason, cancellationToken);
        var type = changed.Status == BuildingStatus.Suspended ? NotificationTypes.BuildingSuspended : NotificationTypes.BuildingUnsuspended;
        if (notifications is not null)
        {
            await notifications.NotifyAsync(
                new NotificationEvent(type, changed.OwnerId, changed.Id, $"{type}:{changed.Id}:{changed.Version}", BuildingId: changed.Id),
                cancellationToken);
        }

        return ListingRules.BuildingStatusText(changed.Status);
    }
}

// NV-21: reportes de usuarios y su resolución.
public sealed class ReportService(
    StaffService staff,
    ModerationService moderation,
    IReportRepository reports,
    IBuildingRepository buildings,
    IRoomRepository rooms,
    IUserRepository users,
    TimeProvider timeProvider,
    IReviewRepository? reviews = null,
    ReviewModerationService? reviewModeration = null)
{
    public async Task<ReportCreatedResponse> CreateAsync(string userId, CreateReportRequest request, CancellationToken cancellationToken)
    {
        var targetType = ListingRules.OneOf("targetType", request.TargetType, ModerationRules.ReportTargets);
        var targetId = ModerationRules.ObjectId("targetId", request.TargetId);
        var reason = ListingRules.OneOf("reason", request.Reason, ModerationRules.ReportReasons);
        var details = request.Details is null ? null : ListingRules.Text("details", request.Details, 1, 500);

        if (targetType == "user" && targetId == userId)
        {
            throw new RequestValidationException("targetId", "You cannot report yourself.");
        }

        var exists = targetType switch
        {
            "building" => await buildings.GetAsync(targetId, cancellationToken) is not null,
            "room" => await rooms.GetAsync(targetId, cancellationToken) is not null,
            "review" => reviews is not null && await reviews.GetAsync(targetId, cancellationToken) is not null,
            _ => await users.GetByIdAsync(targetId, cancellationToken) is not null,
        };

        if (!exists)
        {
            throw new NotFoundException(targetType);
        }

        var created = await reports.CreateAsync(
            new Report
            {
                ReporterId = userId,
                TargetType = targetType,
                TargetId = targetId,
                Reason = reason,
                Details = details,
                Status = "open",
                CreatedAt = timeProvider.GetUtcNow(),
            },
            cancellationToken) ?? throw new ConflictException("report_exists");

        return new ReportCreatedResponse(created.Id, created.Status, created.CreatedAt);
    }

    public async Task<AdminReportListResponse> ListAsync(string userId, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        await staff.RequireAsync(userId, StaffRoles.Admin, cancellationToken);
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        var filter = ListingRules.OneOf("status", status ?? "open", ModerationRules.ReportStatuses);
        var (items, total) = await reports.ListAsync(filter, page, pageSize, cancellationToken);
        return new AdminReportListResponse(items.Select(ToResponse).ToArray(), page, pageSize, total, PropertyQueryValidation.CalculateTotalPages(total, pageSize));
    }

    public async Task<AdminReportResponse> ResolveAsync(string userId, string? id, ResolveReportRequest request, CancellationToken cancellationToken)
    {
        await staff.RequireAsync(userId, StaffRoles.Admin, cancellationToken);
        var report = await reports.GetAsync(ModerationRules.ObjectId("id", id), cancellationToken) ?? throw new NotFoundException("report");
        var action = ListingRules.OneOf("action", request.Action, ModerationRules.ReportActions);
        var reason = ModerationRules.Reason(request.Reason, required: true)!;
        if (report.Status != "open")
        {
            throw new ConflictException("report_state");
        }

        var moderationRequest = new ReasonRequest { Reason = reason };
        switch (action)
        {
            case "suspend_building":
                var buildingId = report.TargetType switch
                {
                    "building" => report.TargetId,
                    "room" => (await rooms.GetAsync(report.TargetId, cancellationToken))?.BuildingId ?? throw new NotFoundException("room"),
                    _ => throw new RequestValidationException("action", "Only building or room reports can suspend a building."),
                };
                await moderation.SuspendBuildingAsync(userId, buildingId, moderationRequest, cancellationToken);
                break;
            case "suspend_user":
                var ownerId = report.TargetType switch
                {
                    "user" => report.TargetId,
                    "building" => (await buildings.GetAsync(report.TargetId, cancellationToken))?.OwnerId ?? throw new NotFoundException("building"),
                    "review" => (reviews is null ? null : await reviews.GetAsync(report.TargetId, cancellationToken))?.AuthorId ?? throw new NotFoundException("review"),
                    _ => (await rooms.GetAsync(report.TargetId, cancellationToken))?.OwnerId ?? throw new NotFoundException("room"),
                };
                await moderation.SuspendUserAsync(userId, ownerId, moderationRequest, cancellationToken);
                break;
            case "hide_review":
                if (report.TargetType != "review" || reviewModeration is null)
                {
                    throw new RequestValidationException("action", "Only review reports can hide a review.");
                }

                await reviewModeration.HideAsync(userId, report.TargetId, moderationRequest, cancellationToken);
                break;
        }

        var status = action == "dismiss" ? "dismissed" : "resolved";
        var now = timeProvider.GetUtcNow();
        if (!await reports.CloseAsync(report.Id, status, action, now, cancellationToken))
        {
            throw new ConflictException("report_state");
        }

        await staff.AuditAsync(userId, "report." + status, "report", report.Id, reason, cancellationToken);
        return ToResponse(report with { Status = status, Resolution = action, ResolvedAt = now });
    }

    private static AdminReportResponse ToResponse(Report report) =>
        new(report.Id, report.ReporterId, report.TargetType, report.TargetId, report.Reason, report.Details, report.Status, report.Resolution, report.CreatedAt, report.ResolvedAt);
}
