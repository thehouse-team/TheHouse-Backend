using System.Text.Json;
using TheHouse.Application.Common;

namespace TheHouse.UnitTests;

// NV-23 1.2.0 (evidencia provisional de la T07): 2,000 vectores de features
// puntuados con la implementación de referencia de TheHouse-ML
// (`thehouse_ml.models.baseline_score.score_features`, commit 85d1e4b). El
// C# debe dar exactamente los mismos puntos, suma y `matchScore`. No sustituye
// los casos dorados de ML-04 T05.
public sealed class SmartMatchParityTests
{
    [Fact]
    public void Baseline_matches_the_ml_reference_implementation_bit_for_bit()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "smart-match-parity-ml-85d1e4b.json")));
        var model = new BaselineCompatibilityModel();
        var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToArray();

        Assert.Equal(2000, cases.Length);
        foreach (var item in cases)
        {
            var values = item.GetProperty("features").EnumerateArray().Select(value => value.GetDouble()).ToArray();
            var features = new SmartMatchFeatures(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8], values[9]);
            var score = model.Score(features);

            Assert.Equal(item.GetProperty("points").EnumerateArray().Select(value => value.GetDouble()), score.Breakdown.Select(factor => factor.Points));
            Assert.Equal(item.GetProperty("rawTotal").GetDouble(), score.RawTotal);
            Assert.Equal(item.GetProperty("matchScore").GetInt32(), score.MatchScore);
        }
    }
}
