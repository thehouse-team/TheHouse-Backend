using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using TheHouse.Application.Common;

namespace TheHouse.Infrastructure.SmartMatch;

// NV-23 1.3.0 (§9.1): modelo `logreg-1.0.0` de `TheHouse-ML` (release
// `model-v1.0.0`) con ONNX Runtime en CPU. El backend resuelve el perfil,
// aplica HC01–HC03 y calcula las 10 features; el ONNX devuelve los puntos sin
// redondear por factor y el total; el redondeo es el de ML-04 (guía de ML-06).
// Una sola sesión por proceso, reutilizada por todas las consultas.
public sealed class OnnxCompatibilityModel : ICompatibilityModel, IDisposable
{
    public const string InputName = "features";

    public const string FactorPointsName = "factor_points";

    public const string RawTotalName = "raw_total";

    // Feature que se reporta por factor (la primera de cada factor en la guía §9).
    private static readonly string[] FactorFeatures =
        ["price_to_budget", "distance_km", "services_coverage", "place_type_match", "property_type_match", "extra_services_ratio"];

    private readonly InferenceSession session;

    private readonly ICompatibilityModel fallback;

    private readonly Action<string>? invalidVector;

    private readonly string[] labels;

    public OnnxCompatibilityModel(
        byte[] model,
        string expectedSha256,
        string expectedModelVersion,
        string expectedFeatureSchemaVersion,
        IReadOnlyDictionary<string, string> factorLabels,
        ICompatibilityModel fallback,
        Action<string>? invalidVector = null)
    {
        // Primero el hash: un archivo distinto nunca llega a ONNX Runtime.
        var actual = Convert.ToHexStringLower(SHA256.HashData(model));
        if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("model.onnx: unexpected SHA-256.");
        }

        session = new InferenceSession(model);
        try
        {
            var metadata = session.ModelMetadata.CustomMetadataMap;
            Require(metadata, "modelVersion", expectedModelVersion);
            Require(metadata, "featureSchemaVersion", expectedFeatureSchemaVersion);
            Require(metadata, "profile", "full");
            if (expectedFeatureSchemaVersion != SmartMatchRules.FeatureSchemaVersion ||
                !ReadArray<string>(metadata, "featureOrder").SequenceEqual(SmartMatchFeatures.Order) ||
                !ReadArray<string>(metadata, "factorOrder").SequenceEqual(SmartMatchRules.FactorOrder) ||
                !session.InputMetadata.ContainsKey(InputName) ||
                !session.OutputMetadata.ContainsKey(FactorPointsName) ||
                !session.OutputMetadata.ContainsKey(RawTotalName))
            {
                throw new InvalidOperationException("model.onnx: unexpected contract.");
            }

            MaxPointsRaw = ReadArray<double>(metadata, "maxPointsRaw");
            if (MaxPointsRaw.Count != SmartMatchRules.FactorOrder.Length || Math.Abs(MaxPointsRaw.Sum() - 100) > 1e-6)
            {
                throw new InvalidOperationException("model.onnx: unexpected maxPointsRaw.");
            }

            MaxPoints = [.. MaxPointsRaw.Select(SmartMatchRules.FactorPoints)];
            labels = [.. SmartMatchRules.FactorOrder.Select(factor => factorLabels[factor])];
            ModelVersion = expectedModelVersion;
            FeatureSchemaVersion = expectedFeatureSchemaVersion;
            this.fallback = fallback;
            this.invalidVector = invalidVector;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public string ModelVersion { get; }

    public string FeatureSchemaVersion { get; }

    public IReadOnlyList<double> MaxPointsRaw { get; }

    public IReadOnlyList<double> MaxPoints { get; }

    public SmartMatchScore Score(SmartMatchFeatures features) => ScoreMany([features])[0];

    public IReadOnlyList<SmartMatchScore> ScoreMany(IReadOnlyList<SmartMatchFeatures> features)
    {
        var results = new SmartMatchScore?[features.Count];
        var valid = new List<int>(features.Count);
        for (var index = 0; index < features.Count; index++)
        {
            if (SmartMatchRules.IsValid(features[index]))
            {
                valid.Add(index);
            }
            else
            {
                // Error del backend, no del modelo: se registra y ese par usa el modelo base.
                invalidVector?.Invoke(features[index].ToVector().Select(double.IsFinite).All(finite => finite) ? "out-of-range" : "not-finite");
                results[index] = fallback.Score(features[index]);
            }
        }

        if (valid.Count > 0)
        {
            var (points, totals) = Run([.. valid.Select(index => features[index])]);
            for (var row = 0; row < valid.Count; row++)
            {
                results[valid[row]] = ToScore(features[valid[row]], points, totals, row);
            }
        }

        return [.. results.Select(score => score!)];
    }

    // Entrada [N, 10] y salidas [N, 6] y [N] en `double` (nunca `float`).
    public (double[,] FactorPoints, double[] RawTotal) Run(IReadOnlyList<SmartMatchFeatures> rows)
    {
        var width = SmartMatchFeatures.Order.Length;
        var data = new double[rows.Count * width];
        for (var row = 0; row < rows.Count; row++)
        {
            var vector = rows[row].ToVector();
            for (var column = 0; column < width; column++)
            {
                data[(row * width) + column] = vector[column];
            }
        }

        var input = new DenseTensor<double>(data, [rows.Count, width]);
        using var outputs = session.Run([NamedOnnxValue.CreateFromTensor(InputName, input)]);
        var points = outputs.Single(output => output.Name == FactorPointsName).AsTensor<double>();
        var totals = outputs.Single(output => output.Name == RawTotalName).AsTensor<double>();
        var factorPoints = new double[rows.Count, SmartMatchRules.FactorOrder.Length];
        var rawTotal = new double[rows.Count];
        for (var row = 0; row < rows.Count; row++)
        {
            for (var factor = 0; factor < SmartMatchRules.FactorOrder.Length; factor++)
            {
                factorPoints[row, factor] = points[row, factor];
            }

            rawTotal[row] = totals[row];
        }

        return (factorPoints, rawTotal);
    }

    public void Dispose() => session.Dispose();

    private SmartMatchScore ToScore(SmartMatchFeatures features, double[,] points, double[] totals, int row)
    {
        var breakdown = new SmartMatchFactor[SmartMatchRules.FactorOrder.Length];
        for (var factor = 0; factor < breakdown.Length; factor++)
        {
            var raw = points[row, factor];
            breakdown[factor] = new SmartMatchFactor(
                SmartMatchRules.FactorOrder[factor],
                labels[factor],
                FactorFeatures[factor],
                features[FactorFeatures[factor]],
                raw,
                SmartMatchRules.FactorPoints(raw),
                MaxPoints[factor]);
        }

        return new SmartMatchScore(ModelVersion, FeatureSchemaVersion, SmartMatchRules.MatchScore(totals[row]), totals[row], breakdown);
    }

    private static void Require(IReadOnlyDictionary<string, string> metadata, string key, string expected)
    {
        if (!metadata.TryGetValue(key, out var value) || value != expected)
        {
            throw new InvalidOperationException($"model.onnx: unexpected {key}.");
        }
    }

    private static IReadOnlyList<T> ReadArray<T>(IReadOnlyDictionary<string, string> metadata, string key) =>
        metadata.TryGetValue(key, out var json) && JsonSerializer.Deserialize<T[]>(json) is { } values
            ? values
            : throw new InvalidOperationException($"model.onnx: missing {key}.");
}
