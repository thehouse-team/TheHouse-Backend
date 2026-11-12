using System.Globalization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-18: documento de `agreements`. El código de cierre va embebido y solo
// con sus hashes; `open` es true mientras está pendiente o activo (índice
// único parcial por cuarto).
public sealed class AgreementDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("buildingId")]
    public string BuildingId { get; set; } = string.Empty;

    [BsonElement("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [BsonElement("studentId")]
    public string StudentId { get; set; } = string.Empty;

    [BsonElement("conversationId")]
    public string ConversationId { get; set; } = string.Empty;

    [BsonElement("monthlyRent")]
    public int MonthlyRent { get; set; }

    [BsonElement("deposit")]
    public int Deposit { get; set; }

    [BsonElement("startDate")]
    public string StartDate { get; set; } = string.Empty;

    [BsonElement("durationMonths")]
    public int DurationMonths { get; set; }

    [BsonElement("endDate")]
    public string EndDate { get; set; } = string.Empty;

    [BsonElement("paymentDay")]
    public int PaymentDay { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = "pending";

    [BsonElement("open")]
    public bool Open { get; set; }

    [BsonElement("code")]
    [BsonIgnoreIfNull]
    public ClosingCodeDocument? Code { get; set; }

    [BsonElement("endReason")]
    [BsonIgnoreIfNull]
    public string? EndReason { get; set; }

    [BsonElement("endNote")]
    [BsonIgnoreIfNull]
    public string? EndNote { get; set; }

    [BsonElement("history")]
    public List<AgreementHistoryDocument> History { get; set; } = [];

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [BsonElement("activatedAt")]
    [BsonIgnoreIfNull]
    public DateTime? ActivatedAt { get; set; }

    [BsonElement("endedAt")]
    [BsonIgnoreIfNull]
    public DateTime? EndedAt { get; set; }

    [BsonElement("cancelledAt")]
    [BsonIgnoreIfNull]
    public DateTime? CancelledAt { get; set; }
}

public sealed class ClosingCodeDocument
{
    [BsonElement("codeHash")]
    public string CodeHash { get; set; } = string.Empty;

    [BsonElement("qrTokenHash")]
    public string QrTokenHash { get; set; } = string.Empty;

    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [BsonElement("usedAt")]
    [BsonIgnoreIfNull]
    public DateTime? UsedAt { get; set; }

    [BsonElement("failedAttempts")]
    public int FailedAttempts { get; set; }
}

public sealed class AgreementHistoryDocument
{
    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("at")]
    public DateTime At { get; set; }

    [BsonElement("actor")]
    public string Actor { get; set; } = string.Empty;
}

public sealed class MongoAgreementRepository(IMongoDatabase database) : IAgreementRepository, IDisposable
{
    private const string Date = "yyyy-MM-dd";

    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile bool ready;

    private IMongoCollection<AgreementDocument> Collection =>
        database.GetCollection<AgreementDocument>("agreements");

    public async Task<Agreement?> CreateAsync(Agreement agreement, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureIndexesAsync(cancellationToken);
            var document = ToDocument(agreement);
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

    public Task<Agreement?> GetAsync(string id, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                return null;
            }

            var document = await Collection.Find(item => item.Id == objectId).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : Map(document);
        });

    public Task<Agreement?> FindByCodeHashAsync(string codeHash, CancellationToken cancellationToken) =>
        FindByAsync(Builders<AgreementDocument>.Filter.Eq(item => item.Code!.CodeHash, codeHash), cancellationToken);

    public Task<Agreement?> FindByQrTokenHashAsync(string qrTokenHash, CancellationToken cancellationToken) =>
        FindByAsync(Builders<AgreementDocument>.Filter.Eq(item => item.Code!.QrTokenHash, qrTokenHash), cancellationToken);

    public async Task<bool> ReplaceIfUnchangedAsync(Agreement agreement, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureIndexesAsync(cancellationToken);
            var objectId = ObjectId.Parse(agreement.Id);
            var expected = MongoDates.ToUtc(expectedUpdatedAt);
            var result = await Collection.ReplaceOneAsync(
                item => item.Id == objectId && item.UpdatedAt == expected,
                ToDocument(agreement),
                cancellationToken: cancellationToken);
            return result.ModifiedCount == 1;
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

    public Task RecordFailedAttemptAsync(string id, CancellationToken cancellationToken) =>
        MongoCalls.Run(() => Collection.UpdateOneAsync(
            item => item.Id == ObjectId.Parse(id) && item.Code != null,
            Builders<AgreementDocument>.Update.Inc(item => item.Code!.FailedAttempts, 1),
            cancellationToken: cancellationToken));

    public Task<(IReadOnlyList<Agreement> Items, long TotalItems)> ListByOwnerAsync(
        string ownerId,
        AgreementStatus? status,
        string? roomId,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var builder = Builders<AgreementDocument>.Filter;
            var filter = builder.Eq(item => item.OwnerId, ownerId);
            if (status is { } value)
            {
                filter &= builder.Eq(item => item.Status, Wire(value));
            }

            if (roomId is not null)
            {
                filter &= builder.Eq(item => item.RoomId, roomId);
            }

            var total = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            var documents = await Collection.Find(filter)
                .SortByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
                .Skip((page - 1) * pageSize).Limit(pageSize)
                .ToListAsync(cancellationToken);
            return ((IReadOnlyList<Agreement>)documents.Select(Map).ToArray(), total);
        });

    public Task<Agreement?> GetActiveForStudentAsync(string studentId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var active = Wire(AgreementStatus.Active);
            var document = await Collection.Find(item => item.StudentId == studentId && item.Status == active)
                .SortByDescending(item => item.ActivatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : Map(document);
        });

    public Task<bool> HasRentedAsync(string studentId, string roomId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var statuses = new[] { Wire(AgreementStatus.Active), Wire(AgreementStatus.Ended) };
            var filter = Builders<AgreementDocument>.Filter;
            return await Collection.CountDocumentsAsync(
                filter.Eq(item => item.StudentId, studentId) & filter.Eq(item => item.RoomId, roomId) & filter.In(item => item.Status, statuses),
                new CountOptions { Limit = 1 },
                cancellationToken) > 0;
        });

    private Task<Agreement?> FindByAsync(FilterDefinition<AgreementDocument> filter, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var documents = await Collection.Find(filter)
                .SortByDescending(item => item.UpdatedAt)
                .Limit(5)
                .ToListAsync(cancellationToken);
            var chosen = documents.FirstOrDefault(item => item.Status == Wire(AgreementStatus.Pending)) ?? documents.FirstOrDefault();
            return chosen is null ? null : Map(chosen);
        });

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
                var keys = Builders<AgreementDocument>.IndexKeys;
                await Collection.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<AgreementDocument>(
                            keys.Ascending(item => item.RoomId),
                            new CreateIndexOptions<AgreementDocument>
                            {
                                Unique = true,
                                Name = "ux_agreements_open_room",
                                PartialFilterExpression = Builders<AgreementDocument>.Filter.Eq(item => item.Open, true),
                            }),
                        new CreateIndexModel<AgreementDocument>(
                            keys.Ascending(item => item.Code!.CodeHash),
                            new CreateIndexOptions { Name = "ix_agreements_code_hash", Sparse = true }),
                        new CreateIndexModel<AgreementDocument>(
                            keys.Ascending(item => item.Code!.QrTokenHash),
                            new CreateIndexOptions { Name = "ix_agreements_qr_token_hash", Sparse = true }),
                        new CreateIndexModel<AgreementDocument>(
                            keys.Ascending(item => item.OwnerId).Descending(item => item.CreatedAt),
                            new CreateIndexOptions { Name = "ix_agreements_owner_created" }),
                        new CreateIndexModel<AgreementDocument>(
                            keys.Ascending(item => item.StudentId).Ascending(item => item.Status),
                            new CreateIndexOptions { Name = "ix_agreements_student_status" }),
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

    private static string Wire(AgreementStatus status) => status.ToString().ToLowerInvariant();

    private static AgreementDocument ToDocument(Agreement agreement) =>
        new()
        {
            Id = ObjectId.Parse(agreement.Id),
            RequestId = agreement.RequestId,
            RoomId = agreement.RoomId,
            BuildingId = agreement.BuildingId,
            OwnerId = agreement.OwnerId,
            StudentId = agreement.StudentId,
            ConversationId = agreement.ConversationId,
            MonthlyRent = agreement.Terms.MonthlyRent,
            Deposit = agreement.Terms.Deposit,
            StartDate = agreement.Terms.StartDate.ToString(Date, CultureInfo.InvariantCulture),
            DurationMonths = agreement.Terms.DurationMonths,
            EndDate = agreement.Terms.EndDate.ToString(Date, CultureInfo.InvariantCulture),
            PaymentDay = agreement.Terms.PaymentDay,
            Status = Wire(agreement.Status),
            Open = agreement.IsOpen,
            Code = agreement.Code is not { } code
                ? null
                : new ClosingCodeDocument
                {
                    CodeHash = code.CodeHash,
                    QrTokenHash = code.QrTokenHash,
                    ExpiresAt = MongoDates.ToUtc(code.ExpiresAt),
                    UsedAt = code.UsedAt is { } used ? MongoDates.ToUtc(used) : null,
                    FailedAttempts = code.FailedAttempts,
                },
            EndReason = agreement.EndReason,
            EndNote = agreement.EndNote,
            History = agreement.History
                .Select(entry => new AgreementHistoryDocument { Status = Wire(entry.Status), At = MongoDates.ToUtc(entry.At), Actor = entry.Actor })
                .ToList(),
            CreatedAt = MongoDates.ToUtc(agreement.CreatedAt),
            UpdatedAt = MongoDates.ToUtc(agreement.UpdatedAt),
            ActivatedAt = agreement.ActivatedAt is { } activated ? MongoDates.ToUtc(activated) : null,
            EndedAt = agreement.EndedAt is { } ended ? MongoDates.ToUtc(ended) : null,
            CancelledAt = agreement.CancelledAt is { } cancelled ? MongoDates.ToUtc(cancelled) : null,
        };

    private static Agreement Map(AgreementDocument document) =>
        new()
        {
            Id = document.Id.ToString(),
            RequestId = document.RequestId,
            RoomId = document.RoomId,
            BuildingId = document.BuildingId,
            OwnerId = document.OwnerId,
            StudentId = document.StudentId,
            ConversationId = document.ConversationId,
            Terms = new AgreementTerms(
                document.MonthlyRent,
                document.Deposit,
                DateOnly.ParseExact(document.StartDate, Date, CultureInfo.InvariantCulture),
                document.DurationMonths,
                DateOnly.ParseExact(document.EndDate, Date, CultureInfo.InvariantCulture),
                document.PaymentDay),
            Status = Enum.Parse<AgreementStatus>(document.Status, ignoreCase: true),
            Code = document.Code is not { } code
                ? null
                : new ClosingCode(
                    code.CodeHash,
                    code.QrTokenHash,
                    MongoDates.FromUtc(code.ExpiresAt),
                    code.UsedAt is { } used ? MongoDates.FromUtc(used) : null,
                    code.FailedAttempts),
            EndReason = document.EndReason,
            EndNote = document.EndNote,
            History = document.History
                .Select(entry => new AgreementHistoryEntry(Enum.Parse<AgreementStatus>(entry.Status, ignoreCase: true), MongoDates.FromUtc(entry.At), entry.Actor))
                .ToArray(),
            CreatedAt = MongoDates.FromUtc(document.CreatedAt),
            UpdatedAt = MongoDates.FromUtc(document.UpdatedAt),
            ActivatedAt = document.ActivatedAt is { } activated ? MongoDates.FromUtc(activated) : null,
            EndedAt = document.EndedAt is { } ended ? MongoDates.FromUtc(ended) : null,
            CancelledAt = document.CancelledAt is { } cancelled ? MongoDates.FromUtc(cancelled) : null,
        };

    public void Dispose() => gate.Dispose();
}

public sealed class UnavailableAgreementRepository : IAgreementRepository
{
    public Task<Agreement?> CreateAsync(Agreement agreement, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<Agreement?> GetAsync(string id, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<Agreement?> FindByCodeHashAsync(string codeHash, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<Agreement?> FindByQrTokenHashAsync(string qrTokenHash, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<bool> ReplaceIfUnchangedAsync(Agreement agreement, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException();

    public Task RecordFailedAttemptAsync(string id, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<(IReadOnlyList<Agreement> Items, long TotalItems)> ListByOwnerAsync(
        string ownerId,
        AgreementStatus? status,
        string? roomId,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException();

    public Task<Agreement?> GetActiveForStudentAsync(string studentId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<bool> HasRentedAsync(string studentId, string roomId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();
}
