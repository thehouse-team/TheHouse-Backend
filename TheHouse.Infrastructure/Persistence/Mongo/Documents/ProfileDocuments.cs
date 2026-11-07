using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TheHouse.Infrastructure.Persistence.Mongo.Documents;

// NV-11: documento de `user_profiles` (único por `userId`).
public sealed class UserProfileDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("name")]
    [BsonIgnoreIfNull]
    public string? Name { get; set; }

    [BsonElement("universityId")]
    [BsonIgnoreIfNull]
    public string? UniversityId { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

// NV-11: documento de `user_preferences` (único por `userId`).
public sealed class UserPreferencesDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("budgetMin")]
    public int? BudgetMin { get; set; }

    [BsonElement("budgetMax")]
    public int? BudgetMax { get; set; }

    [BsonElement("currency")]
    public string Currency { get; set; } = "MXN";

    [BsonElement("maxDistanceKm")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal? MaxDistanceKm { get; set; }

    [BsonElement("essentialServices")]
    public List<string> EssentialServices { get; set; } = [];

    [BsonElement("livingWith")]
    public string? LivingWith { get; set; }

    [BsonElement("placeType")]
    public string? PlaceType { get; set; }

    [BsonElement("pets")]
    public bool? Pets { get; set; }

    [BsonElement("transport")]
    public string? Transport { get; set; }

    [BsonElement("propertyTypes")]
    public List<string> PropertyTypes { get; set; } = [];

    [BsonElement("quickSetupCompleted")]
    public bool QuickSetupCompleted { get; set; }

    [BsonElement("version")]
    public long Version { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
