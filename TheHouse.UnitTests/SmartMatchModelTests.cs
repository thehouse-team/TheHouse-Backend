using System.Security.Cryptography;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.TestDoubles;

namespace TheHouse.UnitTests;

// NV-23 1.2.0 T01, T02 y T09: traducción al esquema de ML, perfil mínimo,
// restricciones HC01–HC03, las 10 features y `baseline-1.0.0` con el redondeo
// de ML-04 (resultados calculados a mano).
public sealed class SmartMatchModelTests
{
    private static readonly BaselineCompatibilityModel Model = new();

    [Fact]
    public void Baseline_parameters_are_the_published_ml04_file()
    {
        Assert.Equal("baseline-1.0.0", Model.ModelVersion);
        Assert.Equal("2.0.0", Model.FeatureSchemaVersion);
        Assert.Equal(BaselineCompatibilityModel.ResourceSha256, Model.Sha256);

        var score = Model.Score(Features());
        Assert.Equal(
            new[] { "budget", "distance", "essential_services", "place_type", "property_type", "extras" },
            score.Breakdown.Select(factor => factor.Factor));
        Assert.Equal(new[] { 30d, 25, 20, 10, 10, 5 }, score.Breakdown.Select(factor => factor.MaxPoints));
        Assert.Equal(
            new[] { "Presupuesto", "Distancia", "Servicios esenciales", "Tipo de lugar", "Tipo de inmueble", "Extras" },
            score.Breakdown.Select(factor => factor.Label));
    }

    // Ejemplo de la spec ML-04 §6.4: suma sin redondear 77.976190 → 78.
    [Fact]
    public void Spec_example_gives_the_same_breakdown_and_total_as_ml04()
    {
        var score = Model.Score(Features(priceBelowMin: 0.15, distanceRatio: 0.66, coverage: 2.0 / 3, extras: 3.0 / 7));

        Assert.Equal(new[] { 25.5, 17.0, 13.3, 10, 10, 2.1 }, score.Breakdown.Select(factor => factor.Points));
        Assert.Equal(78, score.MatchScore);
        Assert.Equal(77.976190, score.RawTotal, 6);
        Assert.InRange(Math.Abs(score.Breakdown.Sum(factor => factor.Points) - score.RawTotal), 0, 0.3);
    }

    // 30 × (1 − 0.555) = 13.349999999999998 en binario: round6 antes de 1 decimal → 13.4.
    [Fact]
    public void Factor_points_absorb_floating_point_error_before_rounding()
    {
        var score = Model.Score(Features(priceBelowMin: 0.555));

        Assert.Equal(13.4, score.Breakdown[0].Points);
    }

    // 30 + 0 + 0 + 10 + 10 + 0.5 = 50.5 → 51 (mitad hacia arriba; ToEven daría 50).
    [Fact]
    public void Total_rounds_half_up_and_never_to_even()
    {
        var score = Model.Score(Features(distanceRatio: 1.5, coverage: 0, extras: 0.1));

        Assert.Equal(50.5, score.RawTotal, 9);
        Assert.Equal(51, score.MatchScore);
        Assert.Equal(50, (int)Math.Round(50.5));
    }

    [Theory]
    [InlineData(0.0, 25.0)]
    [InlineData(0.5, 25.0)]
    [InlineData(0.75, 12.5)]
    [InlineData(1.0, 0.0)]
    [InlineData(3.0, 0.0)]
    public void Distance_curve_is_full_until_half_and_falls_to_zero(double ratio, double points)
    {
        Assert.Equal(points, Model.Score(Features(distanceRatio: ratio)).Breakdown[1].Points);
    }

    [Theory]
    [InlineData(0.0, 30.0)]
    [InlineData(0.5, 15.0)]
    [InlineData(1.0, 0.0)]
    public void Budget_curve_decays_below_the_minimum(double priceBelowMin, double points)
    {
        Assert.Equal(points, Model.Score(Features(priceBelowMin: priceBelowMin)).Breakdown[0].Points);
    }

    [Fact]
    public void Score_is_deterministic()
    {
        var features = Features(priceBelowMin: 0.2, distanceRatio: 0.8, coverage: 0.5, extras: 0.3);

        Assert.Equal(Model.Score(features).RawTotal, Model.Score(features).RawTotal);
        Assert.Equal(Model.Score(features).MatchScore, Model.Score(features).MatchScore);
    }

    [Theory]
    [InlineData("single", "shared", "rooms_building", "room")]
    [InlineData("single", "private", "rooms_building", "room_private_bath")]
    [InlineData("double", "private", "house_rooms", "shared")]
    [InlineData("shared", "shared", "house_rooms", "shared")]
    [InlineData("single", "private", "full_apartment", "studio")]
    [InlineData("double", "private", "full_apartment", "studio")]
    public void Place_type_is_translated_from_nv12(string roomType, string bathroom, string buildingType, string expected)
    {
        Assert.Equal(expected, SmartMatchRules.PlaceTypeOf(roomType, bathroom, buildingType));
    }

    [Theory]
    [InlineData("house_rooms", "house")]
    [InlineData("full_apartment", "apartment")]
    [InlineData("rooms_building", "student_residence")]
    public void Property_type_is_translated_from_nv12(string buildingType, string expected)
    {
        Assert.Equal(expected, SmartMatchRules.PropertyTypeOf(buildingType));
    }

    [Fact]
    public void Minimum_profile_reports_every_missing_field()
    {
        var (student, missing) = SmartMatchRules.Resolve(null, new UserPreferences { UserId = "u" });

        Assert.Null(student);
        Assert.Equal(new[] { "university", "budget.max", "livingWith", "placeType" }, missing);
        Assert.Equal(4, SmartMatchRules.Resolve(null, null).Missing.Count);
    }

    [Fact]
    public void Missing_optional_fields_take_the_schema_defaults()
    {
        var (student, missing) = SmartMatchRules.Resolve("uni", new UserPreferences { BudgetMax = 4000, LivingWith = "any", PlaceType = "room" });

        Assert.Empty(missing);
        Assert.Equal(0, student!.BudgetMin);
        Assert.Equal(30, student.MaxDistanceKm);
        Assert.Empty(student.EssentialServices);
        Assert.False(student.Pets);
        Assert.Equal("public", student.Transport);
        Assert.Empty(student.PropertyTypes);
    }

    [Fact]
    public void Hard_constraints_report_all_violations_with_strict_living_with()
    {
        var student = Student(budgetMax: 3000, livingWith: "women", pets: true);

        Assert.Equal(new[] { "budget", "living_with" }, SmartMatchRules.Violations(student, Room(price: 3500, livingWith: "any")));
        Assert.Empty(SmartMatchRules.Violations(student, Room(price: 3000, livingWith: "women")));
        Assert.Equal(new[] { "living_with" }, SmartMatchRules.Violations(student, Room(livingWith: "men")));
        Assert.Empty(SmartMatchRules.Violations(Student(livingWith: "any"), Room(livingWith: "men")));
    }

    // NV-12 no registra mascotas: desconocido no excluye; un `false` sí.
    [Fact]
    public void Pets_only_exclude_when_the_room_says_no()
    {
        var student = Student(pets: true);

        Assert.Empty(SmartMatchRules.Violations(student, Room(petsAllowed: null)));
        Assert.Equal(new[] { "pets" }, SmartMatchRules.Violations(student, Room(petsAllowed: false)));
        Assert.Equal(1, SmartMatchRules.Features(student, Room(petsAllowed: null)).PetsCompatible);
        Assert.Equal(0, SmartMatchRules.Features(student, Room(petsAllowed: false)).PetsCompatible);
    }

    [Fact]
    public void Features_follow_the_schema_formulas()
    {
        var student = Student(budgetMin: 2000, budgetMax: 4000, maxDistanceKm: 4, transport: "bike", services: ["wifi", "water", "gas"], placeType: "room", propertyTypes: ["house"]);
        var room = Room(price: 1500, distanceKm: 3, services: ["wifi", "water", "laundry", "parking"], placeType: "room", propertyType: "apartment");

        var features = SmartMatchRules.Features(student, room);

        Assert.Equal(3, features.DistanceKm);
        Assert.Equal(1.5, features.EffectiveDistanceKm);
        Assert.Equal(1500 / 4000.0, features.PriceToBudget);
        Assert.Equal(2 / 3.0, features.ServicesCoverage);
        Assert.Equal(1, features.PlaceTypeMatch);
        Assert.Equal(0, features.PropertyTypeMatch);
        Assert.Equal(0.75, features.DistanceRatio);
        Assert.Equal(0.25, features.PriceBelowMin);
        Assert.Equal(2 / 8.0, features.ExtraServicesRatio);
        Assert.Equal(SmartMatchFeatures.Order.Length, features.ToVector().Count);
    }

    [Fact]
    public void Feature_edge_cases_match_the_schema()
    {
        var all = PreferenceCatalogs.EssentialServices.ToArray();

        Assert.Equal(1, SmartMatchRules.Features(Student(services: []), Room(services: [])).ServicesCoverage);
        Assert.Equal(0, SmartMatchRules.Features(Student(services: all), Room(services: all)).ExtraServicesRatio);
        Assert.Equal(2 / 11.0, SmartMatchRules.Features(Student(services: []), Room(services: ["wifi", "gas"])).ExtraServicesRatio);
        Assert.Equal(0, SmartMatchRules.Features(Student(budgetMin: 0), Room(price: 900)).PriceBelowMin);
        Assert.Equal(1, SmartMatchRules.Features(Student(propertyTypes: []), Room(propertyType: "house")).PropertyTypeMatch);
        Assert.Equal(900, SmartMatchRules.Features(Student(budgetMax: 0), Room(price: 900)).PriceToBudget);
    }

    // T09 (RNF-11): el perfil de Smart Match no tiene datos personales.
    [Fact]
    public void Student_profile_has_no_personal_fields()
    {
        var fields = typeof(SmartMatchStudent).GetProperties().Select(property => property.Name).ToArray();

        foreach (var forbidden in new[] { "Name", "Email", "Phone", "Gender", "Age", "UserId", "Latitude", "Longitude" })
        {
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    [Fact]
    public async Task Deletion_participant_erases_feedback_and_dismissals_only_for_the_user()
    {
        var store = new InMemorySmartMatchRepository();
        var now = DateTimeOffset.UtcNow;
        await store.SaveFeedbackAsync("alice", "room1", true, "baseline-1.0.0", now, default);
        await store.AddDismissalAsync("alice", "room2", now, default);
        await store.AddDismissalAsync("bob", "room2", now, default);
        var participant = new SmartMatchDeletionParticipant(store);

        Assert.Empty((await participant.CheckAsync("alice", now, default)).Effects);
        await participant.EraseAsync("alice", now, default);
        await participant.EraseAsync("alice", now, default);

        Assert.Empty(store.Feedback);
        Assert.Equal(new[] { "bob" }, store.Dismissals.Keys.Select(key => key.UserId));
    }

    [Fact]
    public void Resource_hash_matches_the_embedded_file()
    {
        using var stream = typeof(BaselineCompatibilityModel).Assembly.GetManifestResourceStream(BaselineCompatibilityModel.ResourceName)!;
        Assert.Equal(BaselineCompatibilityModel.ResourceSha256, Convert.ToHexStringLower(SHA256.HashData(stream)));
    }

    private static SmartMatchFeatures Features(double priceBelowMin = 0, double distanceRatio = 0, double coverage = 1, double placeType = 1, double propertyType = 1, double extras = 0) =>
        new(1, 0.4, 0.5, coverage, placeType, propertyType, 1, distanceRatio, priceBelowMin, extras);

    private static SmartMatchStudent Student(
        int budgetMin = 0,
        int budgetMax = 5000,
        double maxDistanceKm = 30,
        string[]? services = null,
        string livingWith = "any",
        string placeType = "room",
        bool pets = false,
        string transport = "public",
        string[]? propertyTypes = null) =>
        new("uni", budgetMin, budgetMax, maxDistanceKm, (services ?? []).ToHashSet(StringComparer.Ordinal), livingWith, placeType, pets, transport, (propertyTypes ?? []).ToHashSet(StringComparer.Ordinal));

    private static SmartMatchRoom Room(
        int price = 3000,
        double distanceKm = 2,
        string[]? services = null,
        string placeType = "room",
        string propertyType = "student_residence",
        string livingWith = "any",
        bool? petsAllowed = null) =>
        new("room", price, placeType, propertyType, livingWith, (services ?? []).ToHashSet(StringComparer.Ordinal), distanceKm, petsAllowed);
}
