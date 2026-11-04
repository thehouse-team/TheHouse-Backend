namespace TheHouse.Application.Contracts.Universities;

public sealed record UniversityResponse(
    string Id,
    string Code,
    string Name,
    string City,
    string Country,
    DateTimeOffset CreatedAt);
