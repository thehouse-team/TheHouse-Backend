using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TheHouse.Application.Common;
using TheHouse.Configuration;
using TheHouse.Domain.Entities;
using TheHouse.Infrastructure.SmartMatch;

namespace TheHouse.IntegrationTests;

// NV-23 1.3.0 (T06 y T07): modelo `logreg-1.0.0` del release inmutable
// `model-v1.0.0` de TheHouse-ML. Los casos dorados pasan por el código real
// del backend (perfil, HC01–HC03, features, ONNX y redondeo).
public sealed class SmartMatchOnnxTests
{
    private const string Folder = "SmartMatch/model-v1.0.0";

    private const string PinnedSha256 = "36c867188232a6cfedd949171121f3273f606ba5057dc5d3eb24cafd672bdcc5";

    private static readonly BaselineCompatibilityModel Baseline = new();

    private static readonly Lazy<OnnxCompatibilityModel> Onnx = new(() => new OnnxCompatibilityModel(
        File.ReadAllBytes(PathOf("model.onnx")), PinnedSha256, "logreg-1.0.0", "2.0.0", Baseline.Labels, Baseline));

    private static readonly Dictionary<string, string> Constraints = new(StringComparer.Ordinal)
    {
        [SmartMatchRules.Budget] = "HC01_budget",
        [SmartMatchRules.LivingWithReason] = "HC02_living_with",
        [SmartMatchRules.PetsReason] = "HC03_pets",
    };

    public static TheoryData<string> LogregCases() => CaseIds("golden_cases.json");

    public static TheoryData<string> BaselineCases() => CaseIds("baseline_golden_cases.json");

    [Fact]
    public void Release_files_match_their_published_and_pinned_hashes()
    {
        var sums = File.ReadAllLines(PathOf("SHA256SUMS")).Where(line => line.Length > 0).ToArray();
        Assert.Equal(6, sums.Length);
        foreach (var line in sums)
        {
            var parts = line.Split("  ", 2);
            Assert.Equal(parts[0], Hash(PathOf(parts[1])));
        }

        Assert.Equal(PinnedSha256, Hash(PathOf("model.onnx")));
        using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
        var model = settings.RootElement.GetProperty("SmartMatch").GetProperty("Model");
        Assert.Equal(PinnedSha256, model.GetProperty("Sha256").GetString());
        Assert.False(model.GetProperty("Enabled").GetBoolean());
        Assert.Equal("logreg-1.0.0", model.GetProperty("ModelVersion").GetString());
    }

    [Fact]
    public void Model_metadata_gives_the_published_max_points()
    {
        Assert.Equal("logreg-1.0.0", Onnx.Value.ModelVersion);
        Assert.Equal([9.6, 72.6, 6.7, 4.9, 1.7, 4.5], Onnx.Value.MaxPoints);
    }

    [Theory]
    [MemberData(nameof(LogregCases))]
    public void Logreg_golden_case_matches(string id)
    {
        using var document = Case("golden_cases.json", id);
        var root = document.RootElement;
        var input = root.GetProperty("input");
        var expected = root.GetProperty("expected");
        var result = expected.GetProperty("result");

        if (input.TryGetProperty("features", out var vector))
        {
            // Solo inferencia (algunos fuera de `range`: el backend real los rechaza antes).
            var features = FeaturesOf(vector);
            var (points, totals) = Onnx.Value.Run([features]);
            AssertOnnx(expected.GetProperty("onnx"), points, totals);
            AssertScored(result, Onnx.Value.ModelVersion, [.. Enumerable.Range(0, 6).Select(k => points[0, k])], totals[0], Onnx.Value.MaxPoints);
            return;
        }

        var outcome = Evaluate(input, out var student, out var room);
        if (outcome is not null)
        {
            AssertNotScored(result, expected, outcome, student, room);
            return;
        }

        var computed = SmartMatchRules.Features(student!, room!);
        AssertFeatures(expected.GetProperty("features"), computed);
        var (raw, total) = Onnx.Value.Run([computed]);
        AssertOnnx(expected.GetProperty("onnx"), raw, total);

        var score = Onnx.Value.ScoreMany([computed]).Single();
        Assert.Equal("logreg-1.0.0", score.ModelVersion);
        AssertScored(result, score.ModelVersion, [.. score.Breakdown.Select(factor => factor.RawPoints)], score.RawTotal, Onnx.Value.MaxPoints);
        Assert.Equal(result.GetProperty("matchScore").GetInt32(), score.MatchScore);
        var breakdown = result.GetProperty("breakdown").EnumerateArray().ToArray();
        for (var factor = 0; factor < 6; factor++)
        {
            Assert.Equal(breakdown[factor].GetProperty("factor").GetString(), score.Breakdown[factor].Factor);
            Assert.Equal(breakdown[factor].GetProperty("points").GetDouble(), score.Breakdown[factor].Points);
            Assert.Equal(breakdown[factor].GetProperty("maxPoints").GetDouble(), score.Breakdown[factor].MaxPoints);
            Assert.False(double.IsNegative(score.Breakdown[factor].Points));
        }
    }

    [Theory]
    [MemberData(nameof(BaselineCases))]
    public void Baseline_golden_case_matches(string id)
    {
        using var document = Case("baseline_golden_cases.json", id);
        var root = document.RootElement;
        var expected = root.GetProperty("expected");
        var result = expected.GetProperty("result");

        var outcome = Evaluate(root.GetProperty("input"), out var student, out var room);
        if (outcome is not null)
        {
            AssertNotScored(result, expected, outcome, student, room);
            return;
        }

        var computed = SmartMatchRules.Features(student!, room!);
        AssertFeatures(expected.GetProperty("features"), computed);
        var score = ((ICompatibilityModel)Baseline).ScoreMany([computed]).Single();
        Assert.Equal(result.GetProperty("modelVersion").GetString(), score.ModelVersion);
        Assert.InRange(score.RawTotal - expected.GetProperty("rawTotal").GetDouble(), -1e-6, 1e-6);
        Assert.Equal(result.GetProperty("matchScore").GetInt32(), score.MatchScore);
        var breakdown = result.GetProperty("breakdown").EnumerateArray().ToArray();
        for (var factor = 0; factor < 6; factor++)
        {
            Assert.Equal(breakdown[factor].GetProperty("factor").GetString(), score.Breakdown[factor].Factor);
            Assert.Equal(breakdown[factor].GetProperty("points").GetDouble(), score.Breakdown[factor].Points);
            Assert.Equal(breakdown[factor].GetProperty("maxPoints").GetDouble(), score.Breakdown[factor].MaxPoints);
            Assert.Equal(breakdown[factor].GetProperty("feature").GetString(), score.Breakdown[factor].Feature);
            Assert.InRange(score.Breakdown[factor].Value - breakdown[factor].GetProperty("value").GetDouble(), -1e-6, 1e-6);
        }
    }

    [Fact]
    public void Enabled_model_is_selected_only_after_all_checks()
    {
        var logger = NullLogger.Instance;
        var options = new SmartMatchModelOptions
        {
            Enabled = true,
            Path = PathOf("model.onnx"),
            Sha256 = PinnedSha256,
            ModelVersion = "logreg-1.0.0",
        };

        var selected = SmartMatchModelSelection.Create(options, Baseline, logger);
        Assert.Equal("onnx", selected.Reason);
        Assert.Equal("logreg-1.0.0", selected.Model.ModelVersion);

        Assert.Equal("disabled", SmartMatchModelSelection.Create(new SmartMatchModelOptions(), Baseline, logger).Reason);
        Assert.Same(Baseline, SmartMatchModelSelection.Create(new SmartMatchModelOptions { Enabled = true, Path = PathOf("missing.onnx"), Sha256 = PinnedSha256, ModelVersion = "logreg-1.0.0" }, Baseline, logger).Model);
        Assert.Same(Baseline, SmartMatchModelSelection.Create(new SmartMatchModelOptions { Enabled = true, Path = options.Path, Sha256 = new string('0', 64), ModelVersion = "logreg-1.0.0" }, Baseline, logger).Model);
        Assert.Same(Baseline, SmartMatchModelSelection.Create(new SmartMatchModelOptions { Enabled = true, Path = options.Path, Sha256 = PinnedSha256, ModelVersion = "logreg-9.9.9" }, Baseline, logger).Model);
        Assert.Same(Baseline, SmartMatchModelSelection.Create(new SmartMatchModelOptions { Enabled = true, Path = options.Path, Sha256 = PinnedSha256, ModelVersion = "logreg-1.0.0", FeatureSchemaVersion = "3.0.0" }, Baseline, logger).Model);
    }

    [Fact]
    public void A_tampered_model_is_rejected_before_loading()
    {
        var bytes = File.ReadAllBytes(PathOf("model.onnx"));
        bytes[^1] ^= 0xFF;

        var error = Assert.Throws<InvalidOperationException>(() =>
            new OnnxCompatibilityModel(bytes, PinnedSha256, "logreg-1.0.0", "2.0.0", Baseline.Labels, Baseline));

        Assert.Contains("SHA-256", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_vectors_use_the_baseline_for_that_room_only()
    {
        var reasons = new List<string>();
        using var model = new OnnxCompatibilityModel(File.ReadAllBytes(PathOf("model.onnx")), PinnedSha256, "logreg-1.0.0", "2.0.0", Baseline.Labels, Baseline, reasons.Add);
        var valid = SmartMatchModelSelection.StartupCase;
        var invalid = valid with { PlaceTypeMatch = 0.5 };
        var notFinite = valid with { DistanceKm = double.NaN };

        var scores = model.ScoreMany([valid, invalid, notFinite]);

        Assert.Equal(["logreg-1.0.0", "baseline-1.0.0", "baseline-1.0.0"], scores.Select(score => score.ModelVersion));
        Assert.Equal(SmartMatchModelSelection.StartupExpectedScore, scores[0].MatchScore);
        Assert.Equal(["out-of-range", "not-finite"], reasons);
    }

    [Fact]
    public void Two_hundred_rooms_score_in_less_than_twenty_milliseconds()
    {
        var student = new SmartMatchStudent("u", 1000, 6000, 15, new HashSet<string>(["wifi", "water"]), "any", "room", false, "public", new HashSet<string>());
        var services = new[] { "wifi", "water", "electricity", "kitchen", "furnished", "laundry" };
        var rooms = Enumerable.Range(0, 200).Select(index => new SmartMatchRoom(
            $"room-{index:D3}", 1500 + (index * 20), index % 3 == 0 ? "studio" : "room", "student_residence", "any",
            services.Take(1 + (index % services.Length)).ToHashSet(), index * 0.07, null)).ToArray();

        double Measure()
        {
            var watch = Stopwatch.StartNew();
            Onnx.Value.ScoreMany([.. rooms.Select(room => SmartMatchRules.Features(student, room))]);
            return watch.Elapsed.TotalMilliseconds;
        }

        for (var warmup = 0; warmup < 10; warmup++)
        {
            Measure();
        }

        var samples = Enumerable.Range(0, 100).Select(_ => Measure()).Order().ToArray();
        var median = (samples[49] + samples[50]) / 2;
        Assert.True(median < 20, $"Mediana de 200 cuartos: {median:F3} ms.");
    }

    [Fact]
    public void Api_uses_the_onnx_model_when_enabled()
    {
        using var factory = new FoundationFactory(new Dictionary<string, string?> { ["SmartMatch:Model:Enabled"] = "true" });
        using var client = factory.CreateHttpsClient();

        var selection = factory.Services.GetRequiredService<SmartMatchModelSelection>();

        Assert.Equal("onnx", selection.Reason);
        Assert.Equal("logreg-1.0.0", factory.Services.GetRequiredService<ICompatibilityModel>().ModelVersion);
    }

    // Perfil incompleto, restricciones o null si el par se puntúa.
    private static IReadOnlyList<string>? Evaluate(JsonElement input, out SmartMatchStudent? student, out SmartMatchRoom? room)
    {
        var s = input.GetProperty("student");
        var r = input.GetProperty("room");
        var preferences = new UserPreferences
        {
            BudgetMin = IntOrNull(s, "budget.min"),
            BudgetMax = IntOrNull(s, "budget.max"),
            MaxDistanceKm = IsNull(s, "maxDistanceKm") ? null : s.GetProperty("maxDistanceKm").GetDecimal(),
            EssentialServices = Strings(s, "essentialServices"),
            LivingWith = StringOrNull(s, "livingWith"),
            PlaceType = StringOrNull(s, "placeType"),
            Pets = IsNull(s, "pets") ? null : s.GetProperty("pets").GetBoolean(),
            Transport = StringOrNull(s, "transport"),
            PropertyTypes = Strings(s, "propertyTypes"),
        };
        var code = s.GetProperty("university_code").GetString()!;
        var (resolved, missing) = SmartMatchRules.Resolve(code, preferences);
        student = resolved;
        room = null;
        if (resolved is null)
        {
            return ["incomplete", .. missing];
        }

        var campus = CampusOf(code);
        room = new SmartMatchRoom(
            "golden",
            r.GetProperty("price_mxn").GetInt32(),
            r.GetProperty("placeType").GetString()!,
            r.GetProperty("propertyType").GetString()!,
            r.GetProperty("livingWith").GetString()!,
            Strings(r, "services").ToHashSet(StringComparer.Ordinal),
            HaversineKm(campus.Lat, campus.Lon, r.GetProperty("lat").GetDouble(), r.GetProperty("lon").GetDouble()),
            r.GetProperty("pets_allowed").GetBoolean());
        var violated = SmartMatchRules.Violations(resolved, room);
        return violated.Count > 0 ? ["excluded", .. violated.Select(reason => Constraints[reason])] : null;
    }

    private static void AssertNotScored(JsonElement result, JsonElement expected, IReadOnlyList<string> outcome, SmartMatchStudent? student, SmartMatchRoom? room)
    {
        if (outcome[0] == "incomplete")
        {
            Assert.True(result.GetProperty("incomplete").GetBoolean());
            Assert.Equal(result.GetProperty("missingFields").EnumerateArray().Select(item => item.GetString()), outcome.Skip(1));
            return;
        }

        Assert.True(result.GetProperty("excluded").GetBoolean());
        Assert.Equal(result.GetProperty("violatedConstraints").EnumerateArray().Select(item => item.GetString()), outcome.Skip(1));
        AssertFeatures(expected.GetProperty("features"), SmartMatchRules.Features(student!, room!));
    }

    private static void AssertScored(JsonElement result, string modelVersion, IReadOnlyList<double> rawPoints, double rawTotal, IReadOnlyList<double> maxPoints)
    {
        Assert.Equal(result.GetProperty("modelVersion").GetString(), modelVersion);
        Assert.Equal(result.GetProperty("matchScore").GetInt32(), SmartMatchRules.MatchScore(rawTotal));
        var breakdown = result.GetProperty("breakdown").EnumerateArray().ToArray();
        for (var factor = 0; factor < 6; factor++)
        {
            Assert.Equal(breakdown[factor].GetProperty("points").GetDouble(), SmartMatchRules.FactorPoints(rawPoints[factor]));
            Assert.Equal(breakdown[factor].GetProperty("maxPoints").GetDouble(), maxPoints[factor]);
        }
    }

    private static void AssertOnnx(JsonElement onnx, double[,] points, double[] totals)
    {
        var expectedPoints = onnx.GetProperty("factor_points").EnumerateArray().Select(item => item.GetDouble()).ToArray();
        for (var factor = 0; factor < 6; factor++)
        {
            Assert.InRange(points[0, factor] - expectedPoints[factor], -1e-4, 1e-4);
        }

        Assert.InRange(totals[0] - onnx.GetProperty("raw_total").GetDouble(), -1e-4, 1e-4);
    }

    private static void AssertFeatures(JsonElement expected, SmartMatchFeatures computed)
    {
        foreach (var feature in SmartMatchFeatures.Order)
        {
            Assert.InRange(computed[feature] - expected.GetProperty(feature).GetDouble(), -1e-6, 1e-6);
        }
    }

    private static SmartMatchFeatures FeaturesOf(JsonElement vector)
    {
        var values = SmartMatchFeatures.Order.Select(feature => vector.GetProperty(feature).GetDouble()).ToArray();
        return new SmartMatchFeatures(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8], values[9]);
    }

    // Haversine de referencia de la guía de ML-06 (§6.1): radio 6,371,000 m.
    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusM = 6_371_000.0;
        static double ToRad(double degrees) => degrees * Math.PI / 180.0;
        double phi1 = ToRad(lat1), phi2 = ToRad(lat2);
        double dPhi = phi2 - phi1, dLambda = ToRad(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dPhi / 2), 2) + (Math.Cos(phi1) * Math.Cos(phi2) * Math.Pow(Math.Sin(dLambda / 2), 2));
        a = Math.Min(1.0, Math.Max(0.0, a));
        return 2.0 * EarthRadiusM * Math.Asin(Math.Sqrt(a)) / 1000.0;
    }

    private static (double Lat, double Lon) CampusOf(string code)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(PathOf("golden_cases.json")));
        var campus = document.RootElement.GetProperty("metadata").GetProperty("campuses").GetProperty(code);
        return (campus.GetProperty("lat").GetDouble(), campus.GetProperty("lon").GetDouble());
    }

    private static TheoryData<string> CaseIds(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(PathOf(file)));
        var data = new TheoryData<string>();
        foreach (var item in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            data.Add(item.GetProperty("id").GetString()!);
        }

        return data;
    }

    private static JsonDocument Case(string file, string id)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(PathOf(file)));
        var item = document.RootElement.GetProperty("cases").EnumerateArray().Single(entry => entry.GetProperty("id").GetString() == id);
        return JsonDocument.Parse(item.GetRawText());
    }

    private static bool IsNull(JsonElement element, string name) =>
        !element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null;

    private static int? IntOrNull(JsonElement element, string name) => IsNull(element, name) ? null : element.GetProperty(name).GetInt32();

    private static string? StringOrNull(JsonElement element, string name) => IsNull(element, name) ? null : element.GetProperty(name).GetString();

    private static string[] Strings(JsonElement element, string name) =>
        IsNull(element, name) ? [] : [.. element.GetProperty(name).EnumerateArray().Select(item => item.GetString()!)];

    private static string PathOf(string file) => Path.Combine(AppContext.BaseDirectory, Folder, file);

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
