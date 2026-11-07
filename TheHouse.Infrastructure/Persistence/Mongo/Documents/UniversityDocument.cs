using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace TheHouse.Infrastructure.Persistence.Mongo.Documents;

public sealed class UniversityDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("city")]
    public string City { get; set; } = string.Empty;

    [BsonElement("country")]
    public string Country { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    // NV-11: punto del campus (índice 2dsphere) y texto normalizado para
    // búsqueda parcial. Ausentes hasta que corre el seed con coordenadas.
    [BsonElement("location")]
    [BsonIgnoreIfNull]
    public GeoJsonPoint<GeoJson2DGeographicCoordinates>? Location { get; set; }

    [BsonElement("searchKey")]
    [BsonIgnoreIfNull]
    public string? SearchKey { get; set; }
}
