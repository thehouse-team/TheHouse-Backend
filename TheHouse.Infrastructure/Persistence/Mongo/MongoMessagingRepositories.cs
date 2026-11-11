using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Domain.Entities;
using TheHouse.Application.Common;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-16: documentos de `rental_requests`, `conversations` y `messages`.
public sealed class RentalRequestDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("buildingId")]
    public string BuildingId { get; set; } = string.Empty;

    [BsonElement("studentId")]
    public string StudentId { get; set; } = string.Empty;

    [BsonElement("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [BsonElement("quickQuestions")]
    public List<string> QuickQuestions { get; set; } = [];

    [BsonElement("message")]
    [BsonIgnoreIfNull]
    public string? Message { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = "pending";

    // true mientras está pendiente o aceptada (índice único parcial).
    [BsonElement("active")]
    public bool Active { get; set; }

    [BsonElement("rejectionReason")]
    [BsonIgnoreIfNull]
    public string? RejectionReason { get; set; }

    [BsonElement("rejectionNote")]
    [BsonIgnoreIfNull]
    public string? RejectionNote { get; set; }

    [BsonElement("conversationId")]
    public string ConversationId { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public sealed class ConversationDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("studentId")]
    public string StudentId { get; set; } = string.Empty;

    [BsonElement("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = "open";

    [BsonElement("lastMessageAt")]
    public DateTime LastMessageAt { get; set; }

    // Hex en minúsculas de 24 caracteres: se compara como texto y conserva el orden.
    [BsonElement("lastMessageId")]
    [BsonIgnoreIfNull]
    public string? LastMessageId { get; set; }

    [BsonElement("studentLastReadId")]
    [BsonIgnoreIfNull]
    public string? StudentLastReadId { get; set; }

    [BsonElement("ownerLastReadId")]
    [BsonIgnoreIfNull]
    public string? OwnerLastReadId { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public sealed class ChatMessageDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("conversationId")]
    public string ConversationId { get; set; } = string.Empty;

    [BsonElement("senderId")]
    public string SenderId { get; set; } = string.Empty;

    [BsonElement("senderRole")]
    public string SenderRole { get; set; } = string.Empty;

    [BsonElement("text")]
    public string Text { get; set; } = string.Empty;

    [BsonElement("clientMessageId")]
    public string ClientMessageId { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public sealed class MongoRentalRequestRepository(IMongoDatabase database) : IRentalRequestRepository, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile bool ready;

    private IMongoCollection<RentalRequestDocument> Collection =>
        database.GetCollection<RentalRequestDocument>("rental_requests");

    public async Task<RentalRequest?> CreateAsync(RentalRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureIndexesAsync(cancellationToken);
            var document = ToDocument(request, ObjectId.TryParse(request.Id, out var id) ? id : ObjectId.GenerateNewId());
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

    public Task<RentalRequest?> GetAsync(string id, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                return null;
            }

            var document = await Collection.Find(item => item.Id == objectId).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : Map(document);
        });

    public Task DeleteAsync(string id, CancellationToken cancellationToken) =>
        MongoCalls.Run(() => Collection.DeleteOneAsync(item => item.Id == ObjectId.Parse(id), cancellationToken));

    public Task<long> CountActiveByStudentAsync(string studentId, CancellationToken cancellationToken) =>
        MongoCalls.Run(() => Collection.CountDocumentsAsync(item => item.StudentId == studentId && item.Active, cancellationToken: cancellationToken));

    public Task<(IReadOnlyList<RentalRequest> Items, long TotalItems)> ListByStudentAsync(
        string studentId,
        RentalRequestStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var filter = Builders<RentalRequestDocument>.Filter.Eq(item => item.StudentId, studentId);
        return ListAsync(WithStatus(filter, status), page, pageSize, cancellationToken);
    }

    public Task<(IReadOnlyList<RentalRequest> Items, long TotalItems)> ListByOwnerAsync(
        string ownerId,
        RentalRequestStatus? status,
        string? roomId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var builder = Builders<RentalRequestDocument>.Filter;
        var filter = builder.Eq(item => item.OwnerId, ownerId);
        if (roomId is not null)
        {
            filter &= builder.Eq(item => item.RoomId, roomId);
        }

        return ListAsync(WithStatus(filter, status), page, pageSize, cancellationToken);
    }

    public Task<bool> UpdateStatusAsync(RentalRequest updated, RentalRequestStatus expected, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var objectId = ObjectId.Parse(updated.Id);
            var expectedText = Wire(expected);
            var result = await Collection.UpdateOneAsync(
                item => item.Id == objectId && item.Status == expectedText,
                Builders<RentalRequestDocument>.Update
                    .Set(item => item.Status, Wire(updated.Status))
                    .Set(item => item.Active, updated.IsActive)
                    .Set(item => item.RejectionReason, updated.Rejection?.Reason)
                    .Set(item => item.RejectionNote, updated.Rejection?.Note)
                    .Set(item => item.UpdatedAt, MongoDates.ToUtc(updated.UpdatedAt)),
                cancellationToken: cancellationToken);
            return result.ModifiedCount == 1;
        });

    private static FilterDefinition<RentalRequestDocument> WithStatus(FilterDefinition<RentalRequestDocument> filter, RentalRequestStatus? status) =>
        status is { } value ? filter & Builders<RentalRequestDocument>.Filter.Eq(item => item.Status, Wire(value)) : filter;

    private Task<(IReadOnlyList<RentalRequest> Items, long TotalItems)> ListAsync(
        FilterDefinition<RentalRequestDocument> filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var total = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            var documents = await Collection.Find(filter)
                .SortByDescending(item => item.UpdatedAt).ThenByDescending(item => item.Id)
                .Skip((page - 1) * pageSize).Limit(pageSize)
                .ToListAsync(cancellationToken);
            return ((IReadOnlyList<RentalRequest>)documents.Select(Map).ToArray(), total);
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
                var keys = Builders<RentalRequestDocument>.IndexKeys;
                await Collection.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<RentalRequestDocument>(
                            keys.Ascending(item => item.StudentId).Ascending(item => item.RoomId),
                            new CreateIndexOptions<RentalRequestDocument>
                            {
                                Unique = true,
                                Name = "ux_rental_requests_active_student_room",
                                PartialFilterExpression = Builders<RentalRequestDocument>.Filter.Eq(item => item.Active, true),
                            }),
                        new CreateIndexModel<RentalRequestDocument>(
                            keys.Ascending(item => item.StudentId).Descending(item => item.UpdatedAt),
                            new CreateIndexOptions { Name = "ix_rental_requests_student_updated" }),
                        new CreateIndexModel<RentalRequestDocument>(
                            keys.Ascending(item => item.OwnerId).Ascending(item => item.Status).Descending(item => item.UpdatedAt),
                            new CreateIndexOptions { Name = "ix_rental_requests_owner_status_updated" }),
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

    private static string Wire(RentalRequestStatus status) => status.ToString().ToLowerInvariant();

    private static RentalRequestDocument ToDocument(RentalRequest request, ObjectId id) =>
        new()
        {
            Id = id,
            RoomId = request.RoomId,
            BuildingId = request.BuildingId,
            StudentId = request.StudentId,
            OwnerId = request.OwnerId,
            QuickQuestions = [.. request.QuickQuestions],
            Message = request.Message,
            Status = Wire(request.Status),
            Active = request.IsActive,
            RejectionReason = request.Rejection?.Reason,
            RejectionNote = request.Rejection?.Note,
            ConversationId = request.ConversationId,
            CreatedAt = MongoDates.ToUtc(request.CreatedAt),
            UpdatedAt = MongoDates.ToUtc(request.UpdatedAt),
        };

    private static RentalRequest Map(RentalRequestDocument document) =>
        new()
        {
            Id = document.Id.ToString(),
            RoomId = document.RoomId,
            BuildingId = document.BuildingId,
            StudentId = document.StudentId,
            OwnerId = document.OwnerId,
            QuickQuestions = document.QuickQuestions.ToArray(),
            Message = document.Message,
            Status = Enum.Parse<RentalRequestStatus>(document.Status, ignoreCase: true),
            Rejection = document.RejectionReason is null ? null : new RequestRejection(document.RejectionReason, document.RejectionNote),
            ConversationId = document.ConversationId,
            CreatedAt = MongoDates.FromUtc(document.CreatedAt),
            UpdatedAt = MongoDates.FromUtc(document.UpdatedAt),
        };

    public void Dispose() => gate.Dispose();
}

public sealed class MongoConversationRepository(IMongoDatabase database) : IConversationRepository, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile bool ready;

    private IMongoCollection<ConversationDocument> Conversations =>
        database.GetCollection<ConversationDocument>("conversations");

    private IMongoCollection<ChatMessageDocument> Messages =>
        database.GetCollection<ChatMessageDocument>("messages");

    public Task<Conversation> CreateAsync(Conversation conversation, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var document = new ConversationDocument
            {
                Id = ObjectId.TryParse(conversation.Id, out var id) ? id : ObjectId.GenerateNewId(),
                RequestId = conversation.RequestId,
                RoomId = conversation.RoomId,
                StudentId = conversation.StudentId,
                OwnerId = conversation.OwnerId,
                Status = Wire(conversation.Status),
                LastMessageAt = MongoDates.ToUtc(conversation.LastMessageAt),
                CreatedAt = MongoDates.ToUtc(conversation.CreatedAt),
            };
            await Conversations.InsertOneAsync(document, cancellationToken: cancellationToken);
            return Map(document);
        });

    public Task<Conversation?> GetAsync(string id, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                return null;
            }

            var document = await Conversations.Find(item => item.Id == objectId).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : Map(document);
        });

    public Task CloseAsync(string id, CancellationToken cancellationToken) =>
        MongoCalls.Run(() => Conversations.UpdateOneAsync(
            item => item.Id == ObjectId.Parse(id),
            Builders<ConversationDocument>.Update.Set(item => item.Status, "closed"),
            cancellationToken: cancellationToken));

    public Task<(IReadOnlyList<Conversation> Items, long TotalItems)> ListForUserAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var builder = Builders<ConversationDocument>.Filter;
            var filter = builder.Or(builder.Eq(item => item.StudentId, userId), builder.Eq(item => item.OwnerId, userId));
            var total = await Conversations.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            var documents = await Conversations.Find(filter)
                .SortByDescending(item => item.LastMessageAt).ThenByDescending(item => item.Id)
                .Skip((page - 1) * pageSize).Limit(pageSize)
                .ToListAsync(cancellationToken);
            return ((IReadOnlyList<Conversation>)documents.Select(Map).ToArray(), total);
        });

    public async Task<(ChatMessage Message, bool Created)> AppendAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureIndexesAsync(cancellationToken);
            var document = new ChatMessageDocument
            {
                Id = ObjectId.GenerateNewId(),
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                SenderRole = message.SenderRole,
                Text = message.Text,
                ClientMessageId = message.ClientMessageId,
                CreatedAt = MongoDates.ToUtc(message.CreatedAt),
            };
            await Messages.InsertOneAsync(document, cancellationToken: cancellationToken);

            // Solo avanza: un mensaje más antiguo nunca reemplaza al último.
            var id = document.Id.ToString();
            var builder = Builders<ConversationDocument>.Filter;
            await Conversations.UpdateOneAsync(
                builder.Eq(item => item.Id, ObjectId.Parse(message.ConversationId)) &
                builder.Or(builder.Eq(item => item.LastMessageId, null), builder.Lt(item => item.LastMessageId, id)),
                Builders<ConversationDocument>.Update
                    .Set(item => item.LastMessageId, id)
                    .Set(item => item.LastMessageAt, document.CreatedAt),
                cancellationToken: cancellationToken);
            return (MapMessage(document), true);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await MongoCalls.Run(() => Messages
                .Find(item => item.ConversationId == message.ConversationId &&
                              item.SenderId == message.SenderId &&
                              item.ClientMessageId == message.ClientMessageId)
                .FirstOrDefaultAsync(cancellationToken));
            return existing is null ? throw MongoFailure.Unavailable() : (MapMessage(existing), false);
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

    public Task<IReadOnlyList<ChatMessage>> ListAfterAsync(
        string conversationId,
        string? afterId,
        int limit,
        CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var builder = Builders<ChatMessageDocument>.Filter;
            var filter = builder.Eq(item => item.ConversationId, conversationId);
            if (afterId is not null)
            {
                filter &= builder.Gt(item => item.Id, ObjectId.Parse(afterId));
            }

            var documents = await Messages.Find(filter).SortBy(item => item.Id).Limit(limit).ToListAsync(cancellationToken);
            return (IReadOnlyList<ChatMessage>)documents.Select(MapMessage).ToArray();
        });

    public Task<ChatMessage?> GetMessageAsync(string conversationId, string messageId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            if (!ObjectId.TryParse(messageId, out var objectId))
            {
                return null;
            }

            var document = await Messages
                .Find(item => item.Id == objectId && item.ConversationId == conversationId)
                .FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : MapMessage(document);
        });

    public Task<int> CountUnreadAsync(
        string conversationId,
        string readerId,
        string? afterId,
        int cap,
        CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var builder = Builders<ChatMessageDocument>.Filter;
            var filter = builder.Eq(item => item.ConversationId, conversationId) & builder.Ne(item => item.SenderId, readerId);
            if (afterId is not null && ObjectId.TryParse(afterId, out var after))
            {
                filter &= builder.Gt(item => item.Id, after);
            }

            return (int)await Messages.CountDocumentsAsync(filter, new CountOptions { Limit = cap }, cancellationToken);
        });

    public Task MarkReadAsync(string conversationId, bool readerIsStudent, string messageId, CancellationToken cancellationToken) =>
        MongoCalls.Run(() =>
        {
            var builder = Builders<ConversationDocument>.Filter;
            var objectId = ObjectId.Parse(conversationId);
            System.Linq.Expressions.Expression<Func<ConversationDocument, string?>> field =
                readerIsStudent ? item => item.StudentLastReadId : item => item.OwnerLastReadId;
            return Conversations.UpdateOneAsync(
                builder.Eq(item => item.Id, objectId) & builder.Or(builder.Eq(field, null), builder.Lt(field, messageId)),
                Builders<ConversationDocument>.Update.Set(field, messageId),
                cancellationToken: cancellationToken);
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
                var conversationKeys = Builders<ConversationDocument>.IndexKeys;
                await Conversations.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<ConversationDocument>(
                            conversationKeys.Ascending(item => item.RequestId),
                            new CreateIndexOptions { Unique = true, Name = "ux_conversations_request" }),
                        new CreateIndexModel<ConversationDocument>(
                            conversationKeys.Ascending(item => item.StudentId).Descending(item => item.LastMessageAt),
                            new CreateIndexOptions { Name = "ix_conversations_student_last" }),
                        new CreateIndexModel<ConversationDocument>(
                            conversationKeys.Ascending(item => item.OwnerId).Descending(item => item.LastMessageAt),
                            new CreateIndexOptions { Name = "ix_conversations_owner_last" }),
                    ],
                    cancellationToken);

                var messageKeys = Builders<ChatMessageDocument>.IndexKeys;
                await Messages.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<ChatMessageDocument>(
                            messageKeys.Ascending(item => item.ConversationId).Ascending(item => item.Id),
                            new CreateIndexOptions { Name = "ix_messages_conversation_id" }),
                        new CreateIndexModel<ChatMessageDocument>(
                            messageKeys.Ascending(item => item.ConversationId).Ascending(item => item.SenderId).Ascending(item => item.ClientMessageId),
                            new CreateIndexOptions { Unique = true, Name = "ux_messages_client_id" }),
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

    private static string Wire(ConversationStatus status) => status.ToString().ToLowerInvariant();

    private static Conversation Map(ConversationDocument document) =>
        new()
        {
            Id = document.Id.ToString(),
            RequestId = document.RequestId,
            RoomId = document.RoomId,
            StudentId = document.StudentId,
            OwnerId = document.OwnerId,
            Status = Enum.Parse<ConversationStatus>(document.Status, ignoreCase: true),
            LastMessageAt = MongoDates.FromUtc(document.LastMessageAt),
            LastMessageId = document.LastMessageId,
            StudentLastReadId = document.StudentLastReadId,
            OwnerLastReadId = document.OwnerLastReadId,
            CreatedAt = MongoDates.FromUtc(document.CreatedAt),
        };

    private static ChatMessage MapMessage(ChatMessageDocument document) =>
        new()
        {
            Id = document.Id.ToString(),
            ConversationId = document.ConversationId,
            SenderId = document.SenderId,
            SenderRole = document.SenderRole,
            Text = document.Text,
            ClientMessageId = document.ClientMessageId,
            CreatedAt = MongoDates.FromUtc(document.CreatedAt),
        };

    public void Dispose() => gate.Dispose();
}

// Sin MongoDB configurado: falla cerrado con 503.
public sealed class UnavailableRentalRequestRepository : IRentalRequestRepository
{
    public Task<RentalRequest?> CreateAsync(RentalRequest request, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<RentalRequest?> GetAsync(string id, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task DeleteAsync(string id, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<long> CountActiveByStudentAsync(string studentId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<(IReadOnlyList<RentalRequest> Items, long TotalItems)> ListByStudentAsync(string studentId, RentalRequestStatus? status, int page, int pageSize, CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException();

    public Task<(IReadOnlyList<RentalRequest> Items, long TotalItems)> ListByOwnerAsync(string ownerId, RentalRequestStatus? status, string? roomId, int page, int pageSize, CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException();

    public Task<bool> UpdateStatusAsync(RentalRequest updated, RentalRequestStatus expected, CancellationToken cancellationToken) => throw new DependencyUnavailableException();
}

public sealed class UnavailableConversationRepository : IConversationRepository
{
    public Task<Conversation> CreateAsync(Conversation conversation, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<Conversation?> GetAsync(string id, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task CloseAsync(string id, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<(IReadOnlyList<Conversation> Items, long TotalItems)> ListForUserAsync(string userId, int page, int pageSize, CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException();

    public Task<(ChatMessage Message, bool Created)> AppendAsync(ChatMessage message, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<IReadOnlyList<ChatMessage>> ListAfterAsync(string conversationId, string? afterId, int limit, CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException();

    public Task<ChatMessage?> GetMessageAsync(string conversationId, string messageId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<int> CountUnreadAsync(string conversationId, string readerId, string? afterId, int cap, CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException();

    public Task MarkReadAsync(string conversationId, bool readerIsStudent, string messageId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();
}
