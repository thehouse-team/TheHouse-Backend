using System.Reflection;
using System.Text.Json.Serialization;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Contracts.SmartMatch;
using TheHouse.Http;

namespace TheHouse.SecurityTests;

// NV-23 1.2.0 T09 y T13 (RNF-11, OWASP API3): Smart Match solo expone el
// desglose público, las métricas no guardan personas y los contratos de
// entrada rechazan campos desconocidos. Solo los endpoints públicos de
// Smart Match aceptan sesión opcional.
public sealed class SmartMatchSecurityTests
{
    [Theory]
    [InlineData(typeof(SmartMatchScoreItem))]
    [InlineData(typeof(SmartMatchFactorResponse))]
    [InlineData(typeof(SmartMatchRecommendation))]
    public void Score_responses_carry_no_people_and_no_model_internals(Type type)
    {
        var fields = type.GetProperties().Select(property => property.Name).ToArray();

        foreach (var forbidden in new[] { "UserId", "OwnerId", "Email", "Phone", "Name", "Feature", "Value", "RawPoints", "RawTotal", "Weights", "Latitude", "Longitude" })
        {
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    [Theory]
    [InlineData(typeof(SmartMatchMetricsResponse))]
    [InlineData(typeof(SmartMatchMetricsTotals))]
    [InlineData(typeof(SmartMatchModelMetrics))]
    [InlineData(typeof(SmartMatchDailyStats))]
    public void Metrics_never_store_or_return_ids(Type type)
    {
        var fields = type.GetProperties().Select(property => property.Name).ToArray();

        foreach (var forbidden in new[] { "UserId", "RoomId", "BuildingId", "OwnerId", "Users", "Rooms", "Email" })
        {
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    [Theory]
    [InlineData(typeof(SmartMatchScoresRequest))]
    [InlineData(typeof(SmartMatchQueryRequest))]
    [InlineData(typeof(SmartMatchFeedbackRequest))]
    [InlineData(typeof(SmartMatchDismissalRequest))]
    [InlineData(typeof(SmartMatchExplanationQueryRequest))]
    public void Request_contracts_reject_unknown_fields(Type type)
    {
        var attribute = type.GetCustomAttribute<JsonUnmappedMemberHandlingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(JsonUnmappedMemberHandling.Disallow, attribute.UnmappedMemberHandling);
        Assert.DoesNotContain(type.GetProperties(), property => property.Name is "UserId" or "ModelVersion" or "MatchScore" or "Breakdown");
    }

    [Fact]
    public void Optional_session_is_a_separate_guard_and_the_default_stays_mandatory()
    {
        Assert.False(RevocationGuard.Instance.Optional);
        Assert.True(RevocationGuard.OptionalInstance.Optional);
        Assert.NotSame(RevocationGuard.Instance, RevocationGuard.OptionalInstance);
    }

    // NV-24 (RF-63): la explicación no trae porcentajes ni puntos por factor ni datos internos.
    [Fact]
    public void Explanation_response_has_no_factor_points_or_internals()
    {
        var fields = typeof(SmartMatchExplanationResponse).GetProperties().Select(property => property.Name).ToArray();

        foreach (var forbidden in new[] { "Breakdown", "Points", "MaxPoints", "Features", "Prompt", "PromptVersion", "Facts", "UserId", "Email" })
        {
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    // NV-24: la clave de servicio de GenAI nunca está en appsettings.
    [Fact]
    public void Genai_service_key_is_not_in_appsettings()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "TheHouse.slnx")))
        {
            root = Directory.GetParent(root)!.FullName;
        }

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "TheHouse"), "appsettings*.json").Where(path => !path.EndsWith("appsettings.Development.json", StringComparison.Ordinal)))
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
            if (document.RootElement.TryGetProperty("GenAI", out var genAi))
            {
                Assert.False(genAi.TryGetProperty("ServiceKey", out _), file);
                Assert.False(genAi.GetProperty("Enabled").GetBoolean(), file);
            }
        }
    }
}
