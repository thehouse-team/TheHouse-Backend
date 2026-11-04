using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TheHouse.Infrastructure.Persistence.Mongo.Documents;

public sealed class PropertyDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("price")]
    public decimal Price { get; set; }

    [BsonElement("currency")]
    public string Currency { get; set; } = "MXN";

    [BsonElement("city")]
    public string City { get; set; } = string.Empty;

    [BsonElement("universityId")]
    public string? UniversityId { get; set; }

    [BsonElement("bedrooms")]
    public int Bedrooms { get; set; }

    [BsonElement("bathrooms")]
    public int Bathrooms { get; set; }

    [BsonElement("areaM2")]
    public double AreaM2 { get; set; }

    [BsonElement("amenities")]
    public string[] Amenities { get; set; } =
        Array.Empty<string>();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}
