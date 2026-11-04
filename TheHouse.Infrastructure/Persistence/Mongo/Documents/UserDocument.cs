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
}
