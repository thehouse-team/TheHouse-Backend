namespace TheHouse.Application.Contracts.Properties;

public sealed record PropertyListItemResponse(
    string Id,
    string Title,
    decimal Price,
    string Currency,
    string City,
    string? UniversityId,
    int Bedrooms,
    int Bathrooms,
    double AreaM2,
    DateTimeOffset CreatedAt);
