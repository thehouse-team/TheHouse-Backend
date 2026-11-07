using System.Text.Json.Serialization;

namespace TheHouse.Application.Contracts.Profile;

// NV-11 §6.1: contratos estrictos (campos desconocidos → 400).
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateProfileRequest
{
    public string? Name { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SetUniversityRequest
{
    public string? UniversityId { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class BudgetRequest
{
    public int? Min { get; init; }

    public int? Max { get; init; }

    public string? Currency { get; init; }
}

// `quickSetupCompleted`, `version` y `updatedAt` no existen aquí: si el
// cliente los envía, la deserialización falla (OWASP API3).
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PreferencesRequest
{
    public BudgetRequest? Budget { get; init; }

    public decimal? MaxDistanceKm { get; init; }

    public IReadOnlyList<string>? EssentialServices { get; init; }

    public string? LivingWith { get; init; }

    public string? PlaceType { get; init; }

    public bool? Pets { get; init; }

    public string? Transport { get; init; }

    public IReadOnlyList<string>? PropertyTypes { get; init; }
}

public sealed record UniversitySummary(string Id, string Code, string Name, string City);

public sealed record ProfileResponse(
    string? Name,
    UniversitySummary? University,
    bool QuickSetupCompleted,
    DateTimeOffset? UpdatedAt);

public sealed record BudgetResponse(int? Min, int? Max, string Currency);

public sealed record PreferencesResponse(
    BudgetResponse Budget,
    decimal? MaxDistanceKm,
    IReadOnlyList<string> EssentialServices,
    string? LivingWith,
    string? PlaceType,
    bool? Pets,
    string? Transport,
    IReadOnlyList<string> PropertyTypes,
    bool QuickSetupCompleted,
    long Version,
    DateTimeOffset? UpdatedAt);

public sealed record UniversitySearchItem(
    string Id,
    string Code,
    string Name,
    string City,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? DistanceKm);

public sealed record UniversitySearchResponse(IReadOnlyList<UniversitySearchItem> Items);
