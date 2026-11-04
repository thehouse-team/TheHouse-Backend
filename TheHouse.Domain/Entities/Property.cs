namespace TheHouse.Domain.Entities;

public sealed class Property
{
    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public decimal Price { get; init; }

    public string Currency { get; init; } = "MXN";

    public string City { get; init; } = string.Empty;

    public string? UniversityId { get; init; }

    public int Bedrooms { get; init; }

    public int Bathrooms { get; init; }

    public double AreaM2 { get; init; }

    public IReadOnlyList<string> Amenities { get; init; } =
        Array.Empty<string>();

    public DateTimeOffset CreatedAt { get; init; }
}
