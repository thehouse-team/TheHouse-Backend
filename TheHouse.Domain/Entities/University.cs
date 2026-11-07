namespace TheHouse.Domain.Entities;

public sealed class University
{
    public string Id { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    // NV-11: punto del campus (WGS84). Nulo si aún no se cargó con el seed.
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }
}
