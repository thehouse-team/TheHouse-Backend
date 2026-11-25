using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using TheHouse.Domain.Entities;

namespace TheHouse.Application.Common;

// NV-23 1.2.0: modelo base de Smart Match alineado con `TheHouse-ML` ML-04
// 1.0.0 (`baseline-1.0.0`, esquema de features 2.0.0). Todo es cálculo puro:
// mismas entradas → mismo resultado (RNF-13).

// Perfil del estudiante ya resuelto con los valores por omisión del esquema.
public sealed record SmartMatchStudent(
    string UniversityId,
    int BudgetMin,
    int BudgetMax,
    double MaxDistanceKm,
    IReadOnlySet<string> EssentialServices,
    string LivingWith,
    string PlaceType,
    bool Pets,
    string Transport,
    IReadOnlySet<string> PropertyTypes);

// Cuarto publicado traducido a los catálogos del esquema de ML. `PetsAllowed`
// es null porque NV-12 aún no registra el dato (desconocido = no excluye).
public sealed record SmartMatchRoom(
    string RoomId,
    int PriceMxn,
    string PlaceType,
    string PropertyType,
    string LivingWith,
    IReadOnlySet<string> Services,
    double DistanceKm,
    bool? PetsAllowed);

// Las 10 features del par en el orden de `feature_order` del esquema 2.0.0.
public sealed record SmartMatchFeatures(
    double DistanceKm,
    double EffectiveDistanceKm,
    double PriceToBudget,
    double ServicesCoverage,
    double PlaceTypeMatch,
    double PropertyTypeMatch,
    double PetsCompatible,
    double DistanceRatio,
    double PriceBelowMin,
    double ExtraServicesRatio)
{
    public static readonly string[] Order =
    [
        "distance_km", "effective_distance_km", "price_to_budget", "services_coverage", "place_type_match",
        "property_type_match", "pets_compatible", "distance_ratio", "price_below_min", "extra_services_ratio",
    ];

    public IReadOnlyList<double> ToVector() =>
        [DistanceKm, EffectiveDistanceKm, PriceToBudget, ServicesCoverage, PlaceTypeMatch, PropertyTypeMatch, PetsCompatible, DistanceRatio, PriceBelowMin, ExtraServicesRatio];

    public double this[string feature] => Array.IndexOf(Order, feature) is var index and >= 0
        ? ToVector()[index]
        : throw new InvalidOperationException($"Unknown Smart Match feature '{feature}'.");
}

public sealed record SmartMatchFactor(string Factor, string Label, string Feature, double Value, double RawPoints, double Points, double MaxPoints);

public sealed record SmartMatchScore(string ModelVersion, string FeatureSchemaVersion, int MatchScore, double RawTotal, IReadOnlyList<SmartMatchFactor> Breakdown);

public interface ICompatibilityModel
{
    string ModelVersion { get; }

    string FeatureSchemaVersion { get; }

    SmartMatchScore Score(SmartMatchFeatures features);

    // NV-23 1.3.0: todos los candidatos de una consulta en una sola llamada
    // (el ONNX recibe un lote [N, 10]); el modelo base puntúa par por par.
    IReadOnlyList<SmartMatchScore> ScoreMany(IReadOnlyList<SmartMatchFeatures> features) => [.. features.Select(Score)];
}

public static class SmartMatchRules
{
    public const string FeatureSchemaVersion = "2.0.0";

    public const double DefaultMaxDistanceKm = 30;

    public const int MaximumScoreRooms = 50;

    public const int MaximumCandidates = 500;

    public static readonly TimeSpan DismissalDuration = TimeSpan.FromDays(30);

    public static readonly TimeSpan StatsRetention = TimeSpan.FromDays(400);

    public const int MaximumMetricsDays = 90;

    public const int DefaultMetricsDays = 30;

    public const string Budget = "budget";

    public const string LivingWithReason = "living_with";

    public const string PetsReason = "pets";

    public const string Unavailable = "unavailable";

    public const string ProfileIncomplete = "smart_match_profile_incomplete";

    // Orden de los factores del desglose (ML-04 y `factorOrder` de ML-06).
    public static readonly string[] FactorOrder = ["budget", "distance", "essential_services", "place_type", "property_type", "extras"];

    // Features binarias del esquema 2.0.0 (0 o 1).
    private static readonly HashSet<int> BinaryFeatures = [4, 5, 6];

    // Features acotadas a [0, 1] en el esquema 2.0.0 (`range`).
    private static readonly HashSet<int> UnitFeatures = [3, 8, 9];

    // ML-03: factores de distancia efectiva por medio de transporte.
    private static readonly IReadOnlyDictionary<string, double> TransportFactors = new Dictionary<string, double>(StringComparer.Ordinal)
    {
        ["walk"] = 1.0,
        ["bike"] = 0.5,
        ["public"] = 0.4,
        ["car"] = 0.25,
    };

    // Perfil mínimo del esquema 2.0.0 (igual a la configuración rápida de NV-11)
    // y valores por omisión. Devuelve los campos faltantes si no se puede calcular.
    public static (SmartMatchStudent? Student, IReadOnlyList<string> Missing) Resolve(string? universityId, UserPreferences? preferences)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(universityId))
        {
            missing.Add("university");
        }

        if (preferences?.BudgetMax is null)
        {
            missing.Add("budget.max");
        }

        if (preferences?.LivingWith is null)
        {
            missing.Add("livingWith");
        }

        if (preferences?.PlaceType is null)
        {
            missing.Add("placeType");
        }

        if (missing.Count > 0)
        {
            return (null, missing);
        }

        return (new SmartMatchStudent(
            universityId!,
            preferences!.BudgetMin ?? 0,
            preferences.BudgetMax!.Value,
            preferences.MaxDistanceKm is { } distance ? (double)distance : DefaultMaxDistanceKm,
            preferences.EssentialServices.ToHashSet(StringComparer.Ordinal),
            preferences.LivingWith!,
            preferences.PlaceType!,
            preferences.Pets ?? false,
            preferences.Transport ?? "public",
            preferences.PropertyTypes.ToHashSet(StringComparer.Ordinal)), []);
    }

    // Traducción de NV-12 a los catálogos de NV-11 que usa el esquema de ML.
    public static SmartMatchRoom Translate(Room room, Building building, double distanceKm) =>
        Translate(room.Id, room.Price ?? 0, room.Type, room.Bathroom, building.Type, building.AllowedTenants, building.Services, distanceKm);

    public static SmartMatchRoom Translate(
        string roomId,
        int price,
        string? roomType,
        string? bathroom,
        string buildingType,
        string allowedTenants,
        IReadOnlyList<string> services,
        double distanceKm) =>
        new(
            roomId,
            price,
            PlaceTypeOf(roomType, bathroom, buildingType),
            PropertyTypeOf(buildingType),
            allowedTenants,
            services.ToHashSet(StringComparer.Ordinal),
            distanceKm,
            null);

    public static string PlaceTypeOf(string? roomType, string? bathroom, string buildingType) =>
        buildingType == "full_apartment" ? "studio"
        : roomType is "double" or "shared" ? "shared"
        : bathroom == "private" ? "room_private_bath"
        : "room";

    public static string PropertyTypeOf(string buildingType) => buildingType switch
    {
        "house_rooms" => "house",
        "full_apartment" => "apartment",
        "rooms_building" => "student_residence",
        _ => throw new InvalidOperationException($"Unknown building type '{buildingType}'."),
    };

    // HC01–HC03 de ML-03; se reportan todas las que apliquen.
    public static IReadOnlyList<string> Violations(SmartMatchStudent student, SmartMatchRoom room)
    {
        var violated = new List<string>();
        if (room.PriceMxn > student.BudgetMax)
        {
            violated.Add(Budget);
        }

        if (student.LivingWith != "any" && room.LivingWith != student.LivingWith)
        {
            violated.Add(LivingWithReason);
        }

        if (student.Pets && room.PetsAllowed == false)
        {
            violated.Add(PetsReason);
        }

        return violated;
    }

    // Fórmulas de `feature_schema.json` 2.0.0.
    public static SmartMatchFeatures Features(SmartMatchStudent student, SmartMatchRoom room)
    {
        var distance = room.DistanceKm;
        var transport = TransportFactors.TryGetValue(student.Transport, out var factor) ? factor : TransportFactors["public"];
        var essential = student.EssentialServices;
        var coverage = essential.Count == 0 ? 1.0 : essential.Count(room.Services.Contains) / (double)essential.Count;
        var unrequested = PreferenceCatalogs.EssentialServices.Count - essential.Count;
        var extras = unrequested == 0 ? 0.0 : room.Services.Count(service => !essential.Contains(service)) / (double)unrequested;
        var belowMin = student.BudgetMin > 0 && room.PriceMxn < student.BudgetMin
            ? (student.BudgetMin - room.PriceMxn) / (double)student.BudgetMin
            : 0.0;
        return new SmartMatchFeatures(
            distance,
            distance * transport,
            room.PriceMxn / (double)Math.Max(student.BudgetMax, 1),
            coverage,
            student.PlaceType == room.PlaceType ? 1 : 0,
            student.PropertyTypes.Count == 0 || student.PropertyTypes.Contains(room.PropertyType) ? 1 : 0,
            !student.Pets || room.PetsAllowed != false ? 1 : 0,
            distance / student.MaxDistanceKm,
            belowMin,
            extras);
    }

    // Redondeo «mitad hacia arriba» de ML-04 §6.3 (M-ML-031): primero a 6
    // decimales para absorber el error de punto flotante. Nunca ToEven.
    public static double RoundHalfUp(double value, int decimals) =>
        Math.Round(Math.Round(value, 6, MidpointRounding.AwayFromZero), decimals, MidpointRounding.AwayFromZero);

    // Puntos de un factor con 1 decimal; `-0` se escribe como `0` (M-ML-046:
    // el ONNX puede devolver -2.45e-16 en un factor que está en su mínimo).
    public static double FactorPoints(double raw)
    {
        var points = RoundHalfUp(raw, 1);
        return points == 0 ? 0.0 : points;
    }

    // `matchScore` = floor(round6(Σ) + 0.5), acotado a 0–100.
    public static int MatchScore(double rawTotal) => (int)Math.Clamp(RoundHalfUp(rawTotal, 0), 0, 100);

    // Guía de ML-06 §6.2: el vector debe ser finito, dentro de su `range` y
    // con 0 o 1 en las binarias antes de llegar al ONNX.
    public static bool IsValid(SmartMatchFeatures features)
    {
        var vector = features.ToVector();
        for (var index = 0; index < vector.Count; index++)
        {
            var value = vector[index];
            if (!double.IsFinite(value) || value < 0 ||
                (UnitFeatures.Contains(index) && value > 1) ||
                (BinaryFeatures.Contains(index) && value is not (0 or 1)))
            {
                return false;
            }
        }

        return true;
    }
}

// `baseline-1.0.0`: pesos, curvas y redondeo leídos del recurso copiado de
// `TheHouse-ML` (src/thehouse_ml/models/baseline_params.json, commit 85d1e4b).
public sealed class BaselineCompatibilityModel : ICompatibilityModel
{
    public const string ResourceName = "TheHouse.Application.SmartMatch.baseline-1.0.0.json";

    // SHA-256 del recurso: cambiar pesos, curvas o redondeo exige una versión
    // `baseline-x.y.z` nueva (ML-04), así que la prueba compara este valor.
    public const string ResourceSha256 = "51b663e840eb0b5d12592802093050e28091bf91279db6aece7c237dd5737111";

    private readonly IReadOnlyList<Factor> factors;

    public BaselineCompatibilityModel()
    {
        var bytes = ReadResource();
        Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        var metadata = root.GetProperty("metadata");
        ModelVersion = metadata.GetProperty("model_version").GetString()!;
        FeatureSchemaVersion = metadata.GetProperty("feature_schema_version").GetString()!;
        var rounding = root.GetProperty("rounding");
        if (rounding.GetProperty("factor_points").GetProperty("decimals").GetInt32() != 1 ||
            rounding.GetProperty("factor_points").GetProperty("pre_round_decimals").GetInt32() != 6 ||
            rounding.GetProperty("total").GetProperty("pre_round_decimals").GetInt32() != 6 ||
            rounding.GetProperty("total").GetProperty("mode").GetString() != "half_up")
        {
            throw new InvalidOperationException("baseline-1.0.0: unsupported rounding.");
        }

        factors = [.. root.GetProperty("factors").EnumerateArray().Select(item => new Factor(
            item.GetProperty("id").GetString()!,
            item.GetProperty("label").GetString()!,
            item.GetProperty("weight").GetDouble(),
            item.GetProperty("feature").GetString()!,
            [.. item.GetProperty("curve").GetProperty("points").EnumerateArray().Select(point => (point[0].GetDouble(), point[1].GetDouble()))]))];
        if (Math.Abs(factors.Sum(factor => factor.Weight) - root.GetProperty("total_points").GetDouble()) > 1e-9 ||
            FeatureSchemaVersion != SmartMatchRules.FeatureSchemaVersion ||
            factors.Any(factor => !SmartMatchFeatures.Order.Contains(factor.Feature)))
        {
            throw new InvalidOperationException("baseline-1.0.0: invalid parameters.");
        }
    }

    public string ModelVersion { get; }

    public string FeatureSchemaVersion { get; }

    public string Sha256 { get; }

    // Etiquetas en español por factor; el ONNX usa las mismas (mismos factores).
    public IReadOnlyDictionary<string, string> Labels => factors.ToDictionary(factor => factor.Id, factor => factor.Label, StringComparer.Ordinal);

    public SmartMatchScore Score(SmartMatchFeatures features)
    {
        var breakdown = factors.Select(factor =>
        {
            var value = features[factor.Feature];
            var raw = factor.Weight * Curve(factor.Points, value);
            return new SmartMatchFactor(factor.Id, factor.Label, factor.Feature, value, raw, SmartMatchRules.FactorPoints(raw), factor.Weight);
        }).ToArray();
        // Suma de izquierda a derecha en el orden de la configuración, como ML.
        var total = 0.0;
        foreach (var item in breakdown)
        {
            total += item.RawPoints;
        }

        return new SmartMatchScore(ModelVersion, FeatureSchemaVersion, SmartMatchRules.MatchScore(total), total, breakdown);
    }

    // Lineal por tramos con extremos constantes (`curve_semantics`).
    private static double Curve(IReadOnlyList<(double X, double Y)> points, double x)
    {
        if (x <= points[0].X)
        {
            return points[0].Y;
        }

        for (var index = 1; index < points.Count; index++)
        {
            var (x1, y1) = points[index];
            if (x <= x1)
            {
                var (x0, y0) = points[index - 1];
                return y0 + ((y1 - y0) * (x - x0) / (x1 - x0));
            }
        }

        return points[^1].Y;
    }

    private static byte[] ReadResource()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing resource {ResourceName}.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private sealed record Factor(string Id, string Label, double Weight, string Feature, IReadOnlyList<(double X, double Y)> Points);
}
