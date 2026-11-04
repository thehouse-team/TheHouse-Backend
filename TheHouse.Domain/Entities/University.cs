namespace TheHouse.Domain.Entities;

public sealed class University
{
    public string Id { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
}
