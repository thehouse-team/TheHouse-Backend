using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TheHouse.Application.Common;

// NV-24 1.2.1: hechos estructurados, ventajas y desventajas deterministas,
// resumen por plantilla y validación del texto de TheHouse-GenAI. El puntaje
// y el desglose son los de NV-23; aquí nunca se recalculan ni se cambian.
public static partial class SmartMatchExplanationRules
{
    public const string ExplanationVersion = "explanation-1.0.0";

    public const string GeneratedByGenAi = "genai";

    public const string GeneratedByTemplate = "template";

    public const int MaximumSummaryLength = 600;

    public const int MaximumItems = 4;

    public const double ProThreshold = 0.8;

    public const double ConThreshold = 0.4;

    public static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    // Mapeo de servicios del catálogo del backend a los nombres de GenAI (§8).
    public static readonly IReadOnlyDictionary<string, string> ServiceNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["wifi"] = "internet",
        ["water"] = "agua",
        ["electricity"] = "luz",
        ["gas"] = "gas",
        ["parking"] = "estacionamiento",
        ["laundry"] = "lavadora",
        ["furnished"] = "amueblado",
        ["hot_water"] = "agua caliente",
        ["kitchen"] = "cocina",
        ["security"] = "seguridad",
        ["cleaning"] = "limpieza",
    };

    public const string PrivateBathroom = "baño privado";

    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["budget"] = "el presupuesto",
        ["distance"] = "la distancia",
        ["essential_services"] = "los servicios esenciales",
        ["place_type"] = "el tipo de lugar",
        ["property_type"] = "el tipo de inmueble",
        ["extras"] = "los servicios adicionales",
    };

    private static readonly IReadOnlyDictionary<string, string> Pros = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["budget"] = "El precio está dentro de tu presupuesto.",
        ["distance"] = "Está cerca de tu universidad.",
        ["essential_services"] = "Incluye los servicios que consideras esenciales.",
        ["place_type"] = "Es el tipo de lugar que buscas.",
        ["property_type"] = "Es el tipo de inmueble que prefieres.",
        ["extras"] = "Ofrece servicios adicionales a los que pediste.",
    };

    private static readonly IReadOnlyDictionary<string, string> Cons = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["budget"] = "El precio queda muy por debajo del mínimo que indicaste.",
        ["distance"] = "Está más lejos de lo que prefieres.",
        ["essential_services"] = "Le faltan varios de los servicios que consideras esenciales.",
        ["place_type"] = "No es el tipo de lugar que buscas.",
        ["property_type"] = "No es el tipo de inmueble que prefieres.",
        ["extras"] = "Ofrece pocos servicios adicionales.",
    };

    private static readonly IReadOnlyDictionary<string, string> PlaceTitles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["room"] = "Cuarto con baño compartido",
        ["room_private_bath"] = "Cuarto con baño privado",
        ["studio"] = "Departamento completo",
        ["shared"] = "Cuarto compartido",
    };

    private static readonly IReadOnlyDictionary<string, string> PropertyTitles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["house"] = "casa",
        ["apartment"] = "departamento",
        ["student_residence"] = "residencia estudiantil",
    };

    // Sinónimos que GenAI podría usar para un servicio del catálogo.
    private static readonly (string Word, string Service)[] ServiceWords =
    [
        ("agua caliente", "agua caliente"), ("baño privado", PrivateBathroom), ("estacionamiento", "estacionamiento"),
        ("lavadora", "lavadora"), ("amueblado", "amueblado"), ("amueblada", "amueblado"), ("internet", "internet"),
        ("wifi", "internet"), ("wi-fi", "internet"), ("cocina", "cocina"), ("seguridad", "seguridad"),
        ("limpieza", "limpieza"), ("agua", "agua"), ("luz", "luz"), ("electricidad", "luz"), ("gas", "gas"),
    ];

    public static (IReadOnlyList<string> Pros, IReadOnlyList<string> Cons) ProsAndCons(SmartMatchScore score)
    {
        var ratios = score.Breakdown.Select((factor, index) => (factor.Factor, Ratio: factor.MaxPoints <= 0 ? 0 : factor.RawPoints / factor.MaxPoints, index)).ToArray();
        var pros = ratios.Where(item => item.Ratio >= ProThreshold)
            .OrderByDescending(item => item.Ratio).ThenBy(item => item.index)
            .Take(MaximumItems).Select(item => Pros[item.Factor]).ToArray();
        var cons = ratios.Where(item => item.Ratio < ConThreshold)
            .OrderBy(item => item.Ratio).ThenBy(item => item.index)
            .Take(MaximumItems).Select(item => Cons[item.Factor]).ToArray();
        return (pros, cons);
    }

    // Dos factores más altos y el más bajo, con frases fijas; sin porcentajes por factor (RF-63).
    public static string TemplateSummary(SmartMatchScore score)
    {
        var ordered = score.Breakdown
            .Select((factor, index) => (factor.Factor, Ratio: factor.MaxPoints <= 0 ? 0 : factor.RawPoints / factor.MaxPoints, index))
            .OrderByDescending(item => item.Ratio).ThenBy(item => item.index)
            .ToArray();
        var text = new StringBuilder($"Este cuarto tiene {score.MatchScore.ToString(CultureInfo.InvariantCulture)} % de compatibilidad con lo que buscas. ");
        text.Append($"Coincide sobre todo en {Labels[ordered[0].Factor]} y {Labels[ordered[1].Factor]}");
        // El más bajo; en empate, el primero en el orden de los factores de ML-04.
        var lowest = ordered.OrderBy(item => item.Ratio).ThenBy(item => item.index).First();
        text.Append(lowest.Ratio < 1
            ? $"; donde menos coincide es en {Labels[lowest.Factor]}."
            : "; cumple con todo lo que indicaste.");
        return text.ToString();
    }

    public static string NeutralTitle(string placeType, string propertyType, string universityCode)
    {
        var title = $"{PlaceTitles[placeType]} en {PropertyTitles[propertyType]} cerca de {universityCode}";
        return title.Length <= 120 ? title : title[..120];
    }

    public static IReadOnlyList<string> GenAiServices(IEnumerable<string> services, bool privateBathroom = false)
    {
        var names = services.Where(ServiceNames.ContainsKey).Select(service => ServiceNames[service]).Distinct(StringComparer.Ordinal).ToList();
        if (privateBathroom)
        {
            names.Add(PrivateBathroom);
        }

        return [.. names.Take(20)];
    }

    // SHA-256 de las preferencias resueltas: la caché no guarda preferencias ni ids de usuario.
    public static string PreferencesHash(SmartMatchStudent student)
    {
        var canonical = string.Join(
            "|",
            student.UniversityId,
            student.BudgetMin.ToString(CultureInfo.InvariantCulture),
            student.BudgetMax.ToString(CultureInfo.InvariantCulture),
            student.MaxDistanceKm.ToString("R", CultureInfo.InvariantCulture),
            string.Join(",", student.EssentialServices.Order(StringComparer.Ordinal)),
            student.LivingWith,
            student.PlaceType,
            student.Pets ? "1" : "0",
            student.Transport,
            string.Join(",", student.PropertyTypes.Order(StringComparer.Ordinal)));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    // El generador activo forma parte de la llave: una plantilla guardada con
    // GenAI apagado no se reutiliza al encenderlo (y viceversa).
    public static string CacheKey(string roomId, string preferencesHash, string modelVersion, bool genAiEnabled) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{roomId}|{preferencesHash}|{modelVersion}|{ExplanationVersion}|{(genAiEnabled ? GeneratedByGenAi : GeneratedByTemplate)}")));

    // Validación adicional del texto de GenAI (§9 regla 4): texto plano de
    // máximo 600 caracteres; cada número, servicio o universidad debe estar en
    // los hechos enviados. Devuelve el motivo del rechazo o null si es válido.
    public static string? Reject(string? text, GenAiFacts facts, IReadOnlyCollection<string> knownUniversityCodes)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "empty";
        }

        if (text.Length > MaximumSummaryLength)
        {
            return "too_long";
        }

        if (Markup().IsMatch(text))
        {
            return "markup";
        }

        var allowed = new HashSet<decimal>
        {
            facts.MatchScore,
            facts.Price,
            facts.BudgetMax,
            facts.DistanceKm,
            facts.MaxDistanceKm,
        };
        foreach (Match match in Number().Matches(text))
        {
            if (!decimal.TryParse(Normalize(match.Value), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || !allowed.Contains(value))
            {
                return "number";
            }
        }

        var lowered = text.ToLowerInvariant();
        var sent = facts.Services.Concat(facts.EssentialServices).ToHashSet(StringComparer.Ordinal);
        foreach (var (word, service) in ServiceWords)
        {
            var pattern = $@"(?<![\p{{L}}])({Regex.Escape(word)})(?![\p{{L}}])";
            if (Regex.IsMatch(lowered, pattern) && !sent.Contains(service))
            {
                return "service";
            }

            lowered = Regex.Replace(lowered, pattern, " ");
        }

        foreach (var code in knownUniversityCodes.Where(code => !string.Equals(code, facts.University, StringComparison.OrdinalIgnoreCase)))
        {
            if (Regex.IsMatch(text, $@"(?<![\p{{L}}\-])({Regex.Escape(code)})(?![\p{{L}}\-])", RegexOptions.IgnoreCase))
            {
                return "university";
            }
        }

        return null;
    }

    // «2,800» y «2 800» son miles; «1,3» es decimal.
    private static string Normalize(string value)
    {
        var compact = value.Replace(" ", string.Empty, StringComparison.Ordinal);
        return Thousands().IsMatch(compact) ? compact.Replace(",", string.Empty, StringComparison.Ordinal) : compact.Replace(',', '.');
    }

    [GeneratedRegex(@"[<>`*#|\[\]{}]|https?:|www\.", RegexOptions.IgnoreCase)]
    private static partial Regex Markup();

    [GeneratedRegex(@"\d+(?:[.,]\d+)*")]
    private static partial Regex Number();

    [GeneratedRegex(@"^\d{1,3}(,\d{3})+(\.\d+)?$")]
    private static partial Regex Thousands();
}

// Lo único que viaja a TheHouse-GenAI (contrato congelado del 2026-11-18):
// hechos públicos del cuarto y preferencias de búsqueda, sin ids ni textos libres.
public sealed record GenAiFacts(
    int MatchScore,
    string University,
    string Title,
    decimal Price,
    decimal DistanceKm,
    IReadOnlyList<string> Services,
    decimal BudgetMax,
    decimal MaxDistanceKm,
    IReadOnlyList<string> EssentialServices);
