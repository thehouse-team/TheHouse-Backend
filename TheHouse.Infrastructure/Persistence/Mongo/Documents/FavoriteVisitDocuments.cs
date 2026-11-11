using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TheHouse.Infrastructure.Persistence.Mongo.Documents;

// NV-15 1.2.0: documento de `favorites`. `propertyId` solo existe en los
// favoritos de 1.1.0 aún no migrados.
public sealed class FavoriteDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("roomId")]
    [BsonIgnoreIfNull]
    public string? RoomId { get; set; }

    [BsonElement("propertyId")]
    [BsonIgnoreIfNull]
    public string? PropertyId { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

// NV-17: documento de `visits`.
public sealed class VisitDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    // NV-17 1.2.0: `propertyId` solo en visitas de 1.1.0 aún no migradas.
    [BsonElement("roomId")]
    [BsonIgnoreIfNull]
    public string? RoomId { get; set; }

    [BsonElement("propertyId")]
    [BsonIgnoreIfNull]
    public string? PropertyId { get; set; }

    [BsonElement("scheduledAt")]
    public DateTime ScheduledAt { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("message")]
    public string? Message { get; set; }

    [BsonElement("cancelReason")]
    public string? CancelReason { get; set; }

    // NV-17 1.3.0: parte del arrendador.
    [BsonElement("previousScheduledAt")]
    [BsonIgnoreIfNull]
    public DateTime? PreviousScheduledAt { get; set; }

    [BsonElement("cancelNote")]
    [BsonIgnoreIfNull]
    public string? CancelNote { get; set; }

    [BsonElement("cancelledBy")]
    [BsonIgnoreIfNull]
    public string? CancelledBy { get; set; }

    [BsonElement("rescheduleNote")]
    [BsonIgnoreIfNull]
    public string? RescheduleNote { get; set; }

    [BsonElement("confirmedAt")]
    [BsonIgnoreIfNull]
    public DateTime? ConfirmedAt { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [BsonElement("cancelledAt")]
    public DateTime? CancelledAt { get; set; }

    [BsonElement("history")]
    public List<VisitHistoryDocument> History { get; set; } = [];
}

public sealed class VisitHistoryDocument
{
    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("at")]
    public DateTime At { get; set; }

    [BsonElement("actor")]
    public string Actor { get; set; } = string.Empty;
}

// NV-17: registro de Idempotency-Key con TTL de 24 h (solo hash del cuerpo).
public sealed class VisitIdempotencyDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("key")]
    public string Key { get; set; } = string.Empty;

    [BsonElement("requestHash")]
    public string RequestHash { get; set; } = string.Empty;

    [BsonElement("visitId")]
    public string VisitId { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}
