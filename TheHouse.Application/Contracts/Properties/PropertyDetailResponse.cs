namespace TheHouse.Application.Contracts.Properties;

public sealed record PropertyDetailResponse(
    string Id,
    string Title,
    string? Description,
    decimal Price,
    string Currency,
    string City,
    string? UniversityId,
    int Bedrooms,
    int Bathrooms,
    double AreaM2,
    IReadOnlyList<string> Amenities,
    DateTimeOffset CreatedAt);
