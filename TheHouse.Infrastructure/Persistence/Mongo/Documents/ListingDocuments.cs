using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace TheHouse.Infrastructure.Persistence.Mongo.Documents;

// NV-12: `landlord_profiles` (único por `userId`).
public sealed class LandlordProfileDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("fullName")]
    public string FullName { get; set; } = string.Empty;

    [BsonElement("phone")]
    public string Phone { get; set; } = string.Empty;

    [BsonElement("rentalMode")]
    public string RentalMode { get; set; } = string.Empty;

    [BsonElement("rfc")]
    [BsonIgnoreIfNull]
    public string? Rfc { get; set; }

    [BsonElement("acceptedTermsVersion")]
    public string AcceptedTermsVersion { get; set; } = string.Empty;

    [BsonElement("acceptedTermsAt")]
    public DateTime AcceptedTermsAt { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public sealed class AddressDocument
{
    [BsonElement("street")]
    public string Street { get; set; } = string.Empty;

    [BsonElement("number")]
    public string Number { get; set; } = string.Empty;

    [BsonElement("neighborhood")]
    public string Neighborhood { get; set; } = string.Empty;

    [BsonElement("city")]
    public string City { get; set; } = string.Empty;

    [BsonElement("state")]
    public string State { get; set; } = string.Empty;

    [BsonElement("postalCode")]
    public string PostalCode { get; set; } = string.Empty;
}

// NV-12: `buildings` con `location` GeoJSON (índice 2dsphere).
public sealed class BuildingDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("address")]
    public AddressDocument Address { get; set; } = new();

    [BsonElement("location")]
    public GeoJsonPoint<GeoJson2DGeographicCoordinates> Location { get; set; } = null!;

    [BsonElement("nearestUniversityId")]
    public string NearestUniversityId { get; set; } = string.Empty;

    [BsonElement("distanceToUniversityKm")]
    public double DistanceToUniversityKm { get; set; }

    [BsonElement("walkingMinutes")]
    public int WalkingMinutes { get; set; }

    [BsonElement("allowedTenants")]
    public string AllowedTenants { get; set; } = string.Empty;

    [BsonElement("services")]
    public List<string> Services { get; set; } = [];

    [BsonElement("rules")]
    [BsonIgnoreIfNull]
    public string? Rules { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("version")]
    public long Version { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    // NV-14: seed de demostración.
    [BsonElement("demo")]
    [BsonIgnoreIfDefault]
    public bool Demo { get; set; }

    [BsonElement("demoSourceId")]
    [BsonIgnoreIfNull]
    public string? DemoSourceId { get; set; }
}

// NV-12: `rooms` con número único por edificio.
public sealed class RoomDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("buildingId")]
    public string BuildingId { get; set; } = string.Empty;

    [BsonElement("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [BsonElement("number")]
    public int Number { get; set; }

    [BsonElement("floor")]
    public int Floor { get; set; }

    [BsonElement("price")]
    public int? Price { get; set; }

    [BsonElement("sizeM2")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal? SizeM2 { get; set; }

    [BsonElement("type")]
    public string? Type { get; set; }

    [BsonElement("bathroom")]
    public string? Bathroom { get; set; }

    [BsonElement("furniture")]
    public List<string> Furniture { get; set; } = [];

    [BsonElement("availableFrom")]
    public string? AvailableFrom { get; set; }

    [BsonElement("minContract")]
    public string? MinContract { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("version")]
    public long Version { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
