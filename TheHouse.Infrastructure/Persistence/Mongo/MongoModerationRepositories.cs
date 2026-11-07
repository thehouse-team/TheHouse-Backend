using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-21: documentos de moderación.
public sealed class StaffRolesDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("roles")]
    public List<string> Roles { get; set; } = [];

    [BsonElement("grantedAt")]
    public DateTime GrantedAt { get; set; }

    [BsonElement("grantedBy")]
    public string GrantedBy { get; set; } = string.Empty;
}

public sealed class VerificationReviewDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("buildingId")]
    public string BuildingId { get; set; } = string.Empty;

    [BsonElement("reviewerId")]
    public string ReviewerId { get; set; } = string.Empty;

    [BsonElement("decision")]
    public string Decision { get; set; } = string.Empty;

    [BsonElement("reason")]
    [BsonIgnoreIfNull]
    public string? Reason { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public sealed class ReportDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("reporterId")]
    public string ReporterId { get; set; } = string.Empty;

    [BsonElement("targetType")]
    public string TargetType { get; set; } = string.Empty;

    [BsonElement("targetId")]
    public string TargetId { get; set; } = string.Empty;

    [BsonElement("reason")]
    public string Reason { get; set; } = string.Empty;

    [BsonElement("details")]
    [BsonIgnoreIfNull]
    public string? Details { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = "open";

    [BsonElement("resolution")]
    [BsonIgnoreIfNull]
    public string? Resolution { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("resolvedAt")]
    [BsonIgnoreIfNull]
    public DateTime? ResolvedAt { get; set; }
}

public sealed class UserSuspensionDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("reason")]
    public string Reason { get; set; } = string.Empty;

    [BsonElement("suspendedBy")]
    public string SuspendedBy { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public sealed class AuditEventDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("actorId")]
    public string ActorId { get; set; } = string.Empty;

    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    [BsonElement("targetType")]
    public string TargetType { get; set; } = string.Empty;

    [BsonElement("targetId")]
    public string TargetId { get; set; } = string.Empty;

    [BsonElement("reason")]
    [BsonIgnoreIfNull]
    public string? Reason { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public sealed class MongoStaffRoleRepository(IMongoDatabase database) : IStaffRoleRepository, IDisposable
{
    private readonly UniqueUserIndex<StaffRolesDocument> index =
        new(database.GetCollection<StaffRolesDocument>("staff_roles"), "ux_staff_roles_user");

    public Task<StaffRoles?> GetAsync(string userId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var document = await index.Collection.Find(roles => roles.UserId == userId).FirstOrDefaultAsync(cancellationToken);
            return document is null || document.Roles.Count == 0
                ? null
                : new StaffRoles(document.UserId, document.Roles.ToArray(), MongoDates.FromUtc(document.GrantedAt), document.GrantedBy);
        });

    public Task<StaffRoles> GrantAsync(string userId, string role, string grantedBy, DateTimeOffset now, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await index.EnsureAsync(cancellationToken);
            var document = await MongoCalls.RetryOnDuplicateAsync(() => index.Collection.FindOneAndUpdateAsync(
                roles => roles.UserId == userId,
                Builders<StaffRolesDocument>.Update
                    .AddToSet(roles => roles.Roles, role)
                    .Set(roles => roles.GrantedAt, MongoDates.ToUtc(now))
                    .Set(roles => roles.GrantedBy, grantedBy)
                    .SetOnInsert(roles => roles.UserId, userId),
                new FindOneAndUpdateOptions<StaffRolesDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
                cancellationToken));
            return new StaffRoles(document.UserId, document.Roles.ToArray(), MongoDates.FromUtc(document.GrantedAt), document.GrantedBy);
        });

    public Task<bool> RevokeAsync(string userId, string role, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var result = await index.Collection.UpdateOneAsync(
                roles => roles.UserId == userId && roles.Roles.Contains(role),
                Builders<StaffRolesDocument>.Update.Pull(roles => roles.Roles, role),
                cancellationToken: cancellationToken);
            return result.ModifiedCount == 1;
        });

    public void Dispose() => index.Dispose();
}

public sealed class MongoVerificationReviewRepository(IMongoDatabase database) : IVerificationReviewRepository
{
    private IMongoCollection<VerificationReviewDocument> Collection => database.GetCollection<VerificationReviewDocument>("verification_reviews");

    public Task AddAsync(VerificationReview review, CancellationToken cancellationToken) =>
        MongoCalls.Run(() => Collection.InsertOneAsync(
            new VerificationReviewDocument
            {
                Id = ObjectId.GenerateNewId(),
                BuildingId = review.BuildingId,
                ReviewerId = review.ReviewerId,
                Decision = review.Decision,
                Reason = review.Reason,
                CreatedAt = MongoDates.ToUtc(review.CreatedAt),
            },
            cancellationToken: cancellationToken));

    public Task<VerificationReview?> LatestAsync(string buildingId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var document = await Collection.Find(review => review.BuildingId == buildingId)
                .SortByDescending(review => review.CreatedAt)
                .ThenByDescending(review => review.Id)
                .FirstOrDefaultAsync(cancellationToken);
            return document is null
                ? null
                : new VerificationReview(document.BuildingId, document.ReviewerId, document.Decision, document.Reason, MongoDates.FromUtc(document.CreatedAt));
        });
}

public sealed class MongoReportRepository(IMongoDatabase database) : IReportRepository, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile bool ready;

    private IMongoCollection<ReportDocument> Collection => database.GetCollection<ReportDocument>("reports");

    public async Task<Report?> CreateAsync(Report report, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureIndexesAsync(cancellationToken);
            var document = new ReportDocument
            {
                Id = ObjectId.GenerateNewId(),
                ReporterId = report.ReporterId,
                TargetType = report.TargetType,
                TargetId = report.TargetId,
                Reason = report.Reason,
                Details = report.Details,
                Status = "open",
                CreatedAt = MongoDates.ToUtc(report.CreatedAt),
            };
            await Collection.InsertOneAsync(document, cancellationToken: cancellationToken);
            return Map(document);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            throw MongoFailure.Unavailable();
        }
    }

    public Task<Report?> GetAsync(string id, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                return null;
            }

            var document = await Collection.Find(report => report.Id == objectId).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : Map(document);
        });

    public Task<(IReadOnlyList<Report> Items, long TotalItems)> ListAsync(string status, int page, int pageSize, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var filter = Builders<ReportDocument>.Filter.Eq(report => report.Status, status);
            var total = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            var documents = await Collection.Find(filter)
                .SortBy(report => report.CreatedAt).ThenBy(report => report.Id)
                .Skip((page - 1) * pageSize).Limit(pageSize)
                .ToListAsync(cancellationToken);
            return ((IReadOnlyList<Report>)documents.Select(Map).ToArray(), total);
        });

    public Task<bool> CloseAsync(string id, string status, string resolution, DateTimeOffset now, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var objectId = ObjectId.Parse(id);
            var result = await Collection.UpdateOneAsync(
                report => report.Id == objectId && report.Status == "open",
                Builders<ReportDocument>.Update
                    .Set(report => report.Status, status)
                    .Set(report => report.Resolution, resolution)
                    .Set(report => report.ResolvedAt, MongoDates.ToUtc(now)),
                cancellationToken: cancellationToken);
            return result.ModifiedCount == 1;
        });

    // Un reporte abierto por reportante y objetivo (índice único parcial).
    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (ready)
        {
            return;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!ready)
            {
                var keys = Builders<ReportDocument>.IndexKeys;
                await Collection.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<ReportDocument>(
                            keys.Ascending(report => report.ReporterId).Ascending(report => report.TargetType).Ascending(report => report.TargetId),
                            new CreateIndexOptions<ReportDocument>
                            {
                                Unique = true,
                                Name = "ux_reports_open_reporter_target",
                                PartialFilterExpression = Builders<ReportDocument>.Filter.Eq(report => report.Status, "open"),
                            }),
                        new CreateIndexModel<ReportDocument>(
                            keys.Ascending(report => report.Status).Ascending(report => report.CreatedAt),
                            new CreateIndexOptions { Name = "ix_reports_status_created" }),
                    ],
                    cancellationToken);
                ready = true;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static Report Map(ReportDocument document) =>
        new()
        {
            Id = document.Id.ToString(),
            ReporterId = document.ReporterId,
            TargetType = document.TargetType,
            TargetId = document.TargetId,
            Reason = document.Reason,
            Details = document.Details,
            Status = document.Status,
            Resolution = document.Resolution,
            CreatedAt = MongoDates.FromUtc(document.CreatedAt),
            ResolvedAt = document.ResolvedAt is { } resolved ? MongoDates.FromUtc(resolved) : null,
        };

    public void Dispose() => gate.Dispose();
}

public sealed class MongoUserSuspensionRepository(IMongoDatabase database) : IUserSuspensionRepository, IDisposable
{
    private readonly UniqueUserIndex<UserSuspensionDocument> index =
        new(database.GetCollection<UserSuspensionDocument>("user_suspensions"), "ux_user_suspensions_user");

    public Task<UserSuspension?> GetAsync(string userId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var document = await index.Collection.Find(suspension => suspension.UserId == userId).FirstOrDefaultAsync(cancellationToken);
            return document is null
                ? null
                : new UserSuspension(document.UserId, document.Reason, document.SuspendedBy, MongoDates.FromUtc(document.CreatedAt));
        });

    public async Task<bool> CreateAsync(UserSuspension suspension, CancellationToken cancellationToken)
    {
        try
        {
            await index.EnsureAsync(cancellationToken);
            await index.Collection.InsertOneAsync(
                new UserSuspensionDocument
                {
                    Id = ObjectId.GenerateNewId(),
                    UserId = suspension.UserId,
                    Reason = suspension.Reason,
                    SuspendedBy = suspension.SuspendedBy,
                    CreatedAt = MongoDates.ToUtc(suspension.CreatedAt),
                },
                cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            throw MongoFailure.Unavailable();
        }
    }

    public Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
            (await index.Collection.DeleteOneAsync(suspension => suspension.UserId == userId, cancellationToken)).DeletedCount == 1);

    public void Dispose() => index.Dispose();
}

// Solo inserción y lectura: no existe método de actualización ni de borrado.
public sealed class MongoAuditRepository(IMongoDatabase database) : IAuditRepository
{
    private IMongoCollection<AuditEventDocument> Collection => database.GetCollection<AuditEventDocument>("audit_events");

    public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken) =>
        MongoCalls.Run(() => Collection.InsertOneAsync(
            new AuditEventDocument
            {
                Id = ObjectId.GenerateNewId(),
                ActorId = auditEvent.ActorId,
                Action = auditEvent.Action,
                TargetType = auditEvent.TargetType,
                TargetId = auditEvent.TargetId,
                Reason = auditEvent.Reason,
                CreatedAt = MongoDates.ToUtc(auditEvent.CreatedAt),
            },
            cancellationToken: cancellationToken));

    public Task<(IReadOnlyList<AuditEvent> Items, long TotalItems)> ListAsync(int page, int pageSize, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var total = await Collection.CountDocumentsAsync(FilterDefinition<AuditEventDocument>.Empty, cancellationToken: cancellationToken);
            var documents = await Collection.Find(FilterDefinition<AuditEventDocument>.Empty)
                .SortByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
                .Skip((page - 1) * pageSize).Limit(pageSize)
                .ToListAsync(cancellationToken);
            return ((IReadOnlyList<AuditEvent>)documents
                .Select(item => new AuditEvent(item.Id.ToString(), item.ActorId, item.Action, item.TargetType, item.TargetId, item.Reason, MongoDates.FromUtc(item.CreatedAt)))
                .ToArray(), total);
        });
}

// Sin MongoDB habilitado, la moderación falla cerrada (503).
public sealed class UnavailableModerationRepositories :
    IStaffRoleRepository, IVerificationReviewRepository, IReportRepository, IUserSuspensionRepository, IAuditRepository
{
    Task<StaffRoles?> IStaffRoleRepository.GetAsync(string userId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<StaffRoles> GrantAsync(string userId, string role, string grantedBy, DateTimeOffset now, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<bool> RevokeAsync(string userId, string role, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task AddAsync(VerificationReview review, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<VerificationReview?> LatestAsync(string buildingId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<Report?> CreateAsync(Report report, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    Task<Report?> IReportRepository.GetAsync(string id, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    Task<(IReadOnlyList<Report> Items, long TotalItems)> IReportRepository.ListAsync(string status, int page, int pageSize, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<bool> CloseAsync(string id, string status, string resolution, DateTimeOffset now, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    Task<UserSuspension?> IUserSuspensionRepository.GetAsync(string userId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<bool> CreateAsync(UserSuspension suspension, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    Task<(IReadOnlyList<AuditEvent> Items, long TotalItems)> IAuditRepository.ListAsync(int page, int pageSize, CancellationToken cancellationToken) => throw new DependencyUnavailableException();
}
