namespace TheHouse.Application.Common;

// NV-11 §6: catálogos cerrados de preferencias (validados con Mobile).
// Un valor nuevo requiere cambio de spec y aviso a Mobile.
public static class PreferenceCatalogs
{
    public const string Currency = "MXN";

    public const int MaximumBudget = 50_000;

    public const decimal MinimumDistanceKm = 0.5m;

    public const decimal MaximumDistanceKm = 30m;

    public static readonly IReadOnlySet<string> EssentialServices = new HashSet<string>(StringComparer.Ordinal)
    {
        "wifi", "water", "electricity", "gas", "hot_water", "laundry",
        "kitchen", "furnished", "parking", "security", "cleaning",
    };

    public static readonly IReadOnlySet<string> LivingWith = new HashSet<string>(StringComparer.Ordinal)
    {
        "women", "men", "any",
    };

    public static readonly IReadOnlySet<string> PlaceTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "room", "room_private_bath", "studio", "shared",
    };

    public static readonly IReadOnlySet<string> Transport = new HashSet<string>(StringComparer.Ordinal)
    {
        "walk", "bike", "public", "car",
    };

    public static readonly IReadOnlySet<string> PropertyTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "house", "apartment", "student_residence",
    };
}
