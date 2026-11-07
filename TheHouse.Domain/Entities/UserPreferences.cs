namespace TheHouse.Domain.Entities;

// NV-11: preferencias de búsqueda del estudiante (entrada de búsqueda y
// Smart Match). `QuickSetupCompleted` y `Version` los calcula el servidor.
public sealed class UserPreferences
{
    public string UserId { get; init; } = string.Empty;

    public int? BudgetMin { get; init; }

    public int? BudgetMax { get; init; }

    public string Currency { get; init; } = "MXN";

    public decimal? MaxDistanceKm { get; init; }

    public IReadOnlyList<string> EssentialServices { get; init; } = Array.Empty<string>();

    public string? LivingWith { get; init; }

    public string? PlaceType { get; init; }

    public bool? Pets { get; init; }

    public string? Transport { get; init; }

    public IReadOnlyList<string> PropertyTypes { get; init; } = Array.Empty<string>();

    public bool QuickSetupCompleted { get; init; }

    public long Version { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    // Configuración rápida (RF-72): presupuesto máximo, con quién vivir y tipo de lugar.
    public static bool IsQuickSetupComplete(int? budgetMax, string? livingWith, string? placeType) =>
        budgetMax is not null && livingWith is not null && placeType is not null;
}
