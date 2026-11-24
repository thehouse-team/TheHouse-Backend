using TheHouse.Application.Common;

namespace TheHouse.UnitTests;

// NV-24 1.2.1 T01, T02, T05 y T07: ventajas y desventajas por umbral,
// plantilla determinista, título neutro, mapeo de servicios, llave de caché y
// validación del texto de TheHouse-GenAI.
public sealed class SmartMatchExplanationTests
{
    private static readonly BaselineCompatibilityModel Model = new();

    private static readonly string[] Codes = ["UTTT", "UAEH-ESTE", "ITSOEH", "TESJI"];

    private static readonly GenAiFacts Facts = new(91, "UTTT", "Cuarto con baño privado en residencia estudiantil cerca de UTTT", 2800, 1.3m, ["internet", "agua", "baño privado"], 3000, 3, ["internet"]);

    [Fact]
    public void Pros_and_cons_follow_the_80_and_40_percent_thresholds()
    {
        // budget 30/30, distance 17/25 (68 %), servicios 13.3/20, lugar 10, inmueble 0, extras 0.
        var score = Model.Score(new SmartMatchFeatures(1, 0.4, 0.5, 2.0 / 3, 1, 0, 1, 0.66, 0, 0));
        var (pros, cons) = SmartMatchExplanationRules.ProsAndCons(score);

        Assert.Equal(new[] { "El precio está dentro de tu presupuesto.", "Es el tipo de lugar que buscas." }, pros);
        Assert.Equal(new[] { "No es el tipo de inmueble que prefieres.", "Ofrece pocos servicios adicionales." }, cons);
    }

    [Fact]
    public void Pros_and_cons_are_at_most_four()
    {
        var all = Model.Score(new SmartMatchFeatures(1, 0.4, 0.5, 1, 1, 1, 1, 0, 0, 1));
        var none = Model.Score(new SmartMatchFeatures(40, 16, 0.5, 0, 0, 0, 1, 2, 1, 0));

        Assert.Equal(4, SmartMatchExplanationRules.ProsAndCons(all).Pros.Count);
        Assert.Empty(SmartMatchExplanationRules.ProsAndCons(all).Cons);
        Assert.Equal(4, SmartMatchExplanationRules.ProsAndCons(none).Cons.Count);
    }

    [Fact]
    public void Template_is_deterministic_short_and_without_factor_percentages()
    {
        var score = Model.Score(new SmartMatchFeatures(1, 0.4, 0.5, 2.0 / 3, 1, 0, 1, 0.66, 0, 0));

        var summary = SmartMatchExplanationRules.TemplateSummary(score);

        Assert.Equal(summary, SmartMatchExplanationRules.TemplateSummary(score));
        Assert.InRange(summary.Length, 1, SmartMatchExplanationRules.MaximumSummaryLength);
        Assert.Equal(
            $"Este cuarto tiene {score.MatchScore} % de compatibilidad con lo que buscas. Coincide sobre todo en el presupuesto y el tipo de lugar; donde menos coincide es en el tipo de inmueble.",
            summary);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(summary, "%"));
    }

    [Fact]
    public void Title_is_neutral_and_built_from_catalogs()
    {
        Assert.Equal("Cuarto con baño privado en residencia estudiantil cerca de UTTT", SmartMatchExplanationRules.NeutralTitle("room_private_bath", "student_residence", "UTTT"));
        Assert.Equal("Departamento completo en departamento cerca de TESJI", SmartMatchExplanationRules.NeutralTitle("studio", "apartment", "TESJI"));
    }

    [Fact]
    public void Services_are_mapped_to_genai_names()
    {
        Assert.Equal(new[] { "internet", "agua caliente", "baño privado" }, SmartMatchExplanationRules.GenAiServices(["wifi", "hot_water", "unknown"], privateBathroom: true));
        Assert.Equal(11, SmartMatchExplanationRules.ServiceNames.Count);
        Assert.All(PreferenceCatalogs.EssentialServices, service => Assert.True(SmartMatchExplanationRules.ServiceNames.ContainsKey(service)));
    }

    [Fact]
    public void Cache_key_changes_with_preferences_and_hides_them()
    {
        var a = Student(4000);
        var b = Student(4500);

        var hashA = SmartMatchExplanationRules.PreferencesHash(a);

        Assert.Equal(hashA, SmartMatchExplanationRules.PreferencesHash(Student(4000)));
        Assert.NotEqual(hashA, SmartMatchExplanationRules.PreferencesHash(b));
        Assert.Matches("^[0-9a-f]{64}$", hashA);
        Assert.NotEqual(
            SmartMatchExplanationRules.CacheKey("room1", hashA, "baseline-1.0.0", genAiEnabled: true),
            SmartMatchExplanationRules.CacheKey("room1", hashA, "model-1.0.0", genAiEnabled: true));
        Assert.NotEqual(
            SmartMatchExplanationRules.CacheKey("room1", hashA, "baseline-1.0.0", genAiEnabled: false),
            SmartMatchExplanationRules.CacheKey("room1", hashA, "baseline-1.0.0", genAiEnabled: true));
    }

    [Theory]
    [InlineData("Esta propiedad coincide en 91% porque se ubica a 1.3 km de la UTTT, ofrece internet y cuesta $2,800 MXN.")]
    [InlineData("Cuesta 2800 pesos, dentro de tu presupuesto de 3,000 y a menos de 3 km; tiene wifi y baño privado.")]
    [InlineData("Te queda a 1,3 km de la UTTT y tiene agua e internet.")]
    public void Valid_genai_text_is_accepted(string text)
    {
        Assert.Null(SmartMatchExplanationRules.Reject(text, Facts, Codes));
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("Cuesta $2,500 al mes.", "number")]
    [InlineData("Está a 5 km de la UTTT.", "number")]
    [InlineData("Incluye estacionamiento y cocina.", "service")]
    [InlineData("Está cerca del ITSOEH.", "university")]
    [InlineData("<b>Excelente</b> opción", "markup")]
    [InlineData("Visita https://ejemplo.com para más.", "markup")]
    [InlineData("**Muy recomendable**", "markup")]
    public void Invalid_genai_text_is_rejected(string text, string reason)
    {
        Assert.Equal(reason, SmartMatchExplanationRules.Reject(text, Facts, Codes));
    }

    [Fact]
    public void Too_long_text_is_rejected()
    {
        Assert.Equal("too_long", SmartMatchExplanationRules.Reject(new string('a', 601), Facts, Codes));
    }

    // T07: los hechos que viajan a GenAI no tienen ids, nombres ni textos libres.
    [Fact]
    public void Genai_facts_have_no_ids_or_free_text()
    {
        var fields = typeof(GenAiFacts).GetProperties().Select(property => property.Name).ToArray();

        foreach (var forbidden in new[] { "RoomId", "BuildingId", "UserId", "OwnerId", "Name", "Address", "Description", "Rules", "Email", "Phone", "Latitude", "Longitude" })
        {
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    private static SmartMatchStudent Student(int budgetMax) =>
        new("uni", 1000, budgetMax, 5, new HashSet<string>(StringComparer.Ordinal) { "wifi" }, "any", "room", false, "public", new HashSet<string>(StringComparer.Ordinal));
}
