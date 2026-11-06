using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TheHouse.Infrastructure.Persistence.Mongo.Documents;

// NV-10: documento de `refresh_tokens`. Solo guarda el hash del token.
public sealed class RefreshTokenDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("tokenHash")]
    public string TokenHash { get; set; } = string.Empty;

    [BsonElement("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [BsonElement("sessionExpiresAt")]
    public DateTime SessionExpiresAt { get; set; }

    [BsonElement("rotatedAt")]
    public DateTime? RotatedAt { get; set; }

    [BsonElement("revokedAt")]
    public DateTime? RevokedAt { get; set; }

    [BsonElement("revokedReason")]
    public string? RevokedReason { get; set; }
}
