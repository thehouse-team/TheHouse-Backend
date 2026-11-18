using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TheHouse.Infrastructure.Persistence.Mongo.Documents;

public sealed class UserDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    // NV-10 1.2.0: ausente en cuentas anteriores (sin migración).
    [BsonElement("legal")]
    [BsonIgnoreIfNull]
    public LegalAcceptanceDocument? Legal { get; set; }
}

public sealed class LegalAcceptanceDocument
{
    [BsonElement("termsVersion")]
    public string TermsVersion { get; set; } = string.Empty;

    [BsonElement("privacyVersion")]
    public string PrivacyVersion { get; set; } = string.Empty;

    [BsonElement("acceptedAt")]
    public DateTime AcceptedAt { get; set; }
}

// NV-10 1.2.0: marca de una cuenta eliminada; solo id interno y fecha.
public sealed class DeletedAccountDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("deletedAt")]
    public DateTime DeletedAt { get; set; }
}
