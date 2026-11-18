using System.Text.Json.Serialization;

namespace TheHouse.Application.Contracts.Roommates;

// NV-22 §6.1: perfil de compañero. Las entradas rechazan campos
// desconocidos (asignación masiva): ni `userId` ni `universityId` del cliente.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RoommateHabitsInput
{
    public string? Schedule { get; init; }

    public string? Cleaning { get; init; }

    public string? Noise { get; init; }

    public string? Smoking { get; init; }

    public string? Pets { get; init; }

    public string? Social { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RoommateProfileInput
{
    public bool? Visible { get; init; }

    public int? BudgetMin { get; init; }

    public int? BudgetMax { get; init; }

    public string? Zone { get; init; }

    public string? MoveIn { get; init; }

    public int? DurationMonths { get; init; }

    public bool? HasPlace { get; init; }

    public RoommateHabitsInput? Habits { get; init; }

    public string? About { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ContactRoommateRequest
{
    public string? Message { get; init; }
}

public sealed record RoommateHabitsResponse(string Schedule, string Cleaning, string Noise, string Smoking, string Pets, string Social);

public sealed record RoommateUniversity(string Id, string Name);

public sealed record MyRoommateProfileResponse(
    string Id,
    bool Visible,
    int BudgetMin,
    int BudgetMax,
    string Zone,
    string MoveIn,
    int DurationMonths,
    bool HasPlace,
    RoommateHabitsResponse Habits,
    string? About,
    RoommateUniversity University,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

// Tarjeta de otro estudiante: id del perfil (nunca del usuario) y primer nombre.
public sealed record RoommateCard(
    string Id,
    string Name,
    RoommateUniversity University,
    int BudgetMin,
    int BudgetMax,
    string Zone,
    string MoveIn,
    int DurationMonths,
    bool HasPlace,
    RoommateHabitsResponse Habits,
    string? About,
    int Matches,
    DateTimeOffset UpdatedAt);

public sealed record RoommateListResponse(IReadOnlyList<RoommateCard> Items, int Page, int PageSize, long TotalItems, int TotalPages);

public sealed record ContactRoommateResponse(string ConversationId);
