using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TheHouse.Infrastructure.Persistence.Mongo.Documents;

// NV-15: documento de `favorites`.
public sealed class FavoriteDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("propertyId")]
    public string PropertyId { get; set; } = string.Empty;

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

    [BsonElement("propertyId")]
    public string PropertyId { get; set; } = string.Empty;

    [BsonElement("scheduledAt")]
    public DateTime ScheduledAt { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("message")]
    public string? Message { get; set; }

    [BsonElement("cancelReason")]
    public string? CancelReason { get; set; }

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
