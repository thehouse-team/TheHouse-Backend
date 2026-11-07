using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Profile;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;
using TheHouse.TestDoubles;

namespace TheHouse.UnitTests;

// NV-11: normalización de búsqueda, validación de preferencias, búsqueda
// de universidades y coordenadas del seed.
public sealed class ProfilePreferencesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 18, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Tula-Tepeji", "tula tepeji")]
    [InlineData("  TÉCNOLÓGICO   de  Jilotepec ", "tecnologico de jilotepec")]
    [InlineData("Villa del Carbón", "villa del carbon")]
    [InlineData("UAEH-ESTE, Tepeji del Río", "uaeh este tepeji del rio")]
    [InlineData("(.*)", "")]
    [InlineData(null, "")]
    public void SearchText_normalizes_accents_case_and_separators(string? input, string expected)
    {
        Assert.Equal(expected, SearchText.Normalize(input));
    }

    [Fact]
    public void Validate_accepts_full_preferences_and_derives_quick_setup()
    {
        var result = PreferencesService.Validate("u1", Full(), Now);

        Assert.True(result.QuickSetupCompleted);
        Assert.Equal("MXN", result.Currency);
        Assert.Equal(new[] { "wifi", "water" }, result.EssentialServices);
        Assert.Equal(Now, result.UpdatedAt);
    }

    [Fact]
    public void Validate_accepts_empty_preferences_without_quick_setup()
    {
        var result = PreferencesService.Validate("u1", new PreferencesRequest(), Now);

        Assert.False(result.QuickSetupCompleted);
        Assert.Empty(result.EssentialServices);
        Assert.Null(result.BudgetMax);
    }

    [Theory]
    [InlineData(0.5, true)]
    [InlineData(30.0, true)]
    [InlineData(12.3, true)]
    [InlineData(0.4, false)]
    [InlineData(30.1, false)]
    [InlineData(1.25, false)]
    public void Validate_checks_distance_range_and_one_decimal(double distance, bool valid)
    {
        var request = new PreferencesRequest { MaxDistanceKm = (decimal)distance };

        if (valid)
        {
            Assert.Equal((decimal)distance, PreferencesService.Validate("u1", request, Now).MaxDistanceKm);
        }
        else
        {
            Assert.Throws<RequestValidationException>(() => PreferencesService.Validate("u1", request, Now));
        }
    }

    [Fact]
    public void Validate_rejects_bad_budgets_catalog_values_and_repeats()
    {
        var invalid = new[]
        {
            new PreferencesRequest { Budget = new BudgetRequest { Min = 4000, Max = 3000 } },
            new PreferencesRequest { Budget = new BudgetRequest { Max = 50_001 } },
            new PreferencesRequest { Budget = new BudgetRequest { Min = -1 } },
            new PreferencesRequest { Budget = new BudgetRequest { Max = 3000, Currency = "USD" } },
            new PreferencesRequest { EssentialServices = ["wifi", "pool"] },
            new PreferencesRequest { EssentialServices = ["wifi", "wifi"] },
            new PreferencesRequest { PropertyTypes = ["castle"] },
            new PreferencesRequest { LivingWith = "Women" },
            new PreferencesRequest { PlaceType = "loft" },
            new PreferencesRequest { Transport = "plane" },
        };

        Assert.All(invalid, request => Assert.Throws<RequestValidationException>(() => PreferencesService.Validate("u1", request, Now)));
    }

    [Fact]
    public async Task Preferences_service_returns_defaults_and_increments_version()
    {
        var store = new InMemoryPreferencesRepository();
        var service = new PreferencesService(store, new ManualTimeProvider(Now));

        var defaults = await service.GetAsync("u1", CancellationToken.None);
        var first = await service.ReplaceAsync("u1", Full(), CancellationToken.None);
        var second = await service.ReplaceAsync("u1", new PreferencesRequest(), CancellationToken.None);

        Assert.Equal(0, defaults.Version);
        Assert.False(defaults.QuickSetupCompleted);
        Assert.Equal(1, first.Version);
        Assert.True(first.QuickSetupCompleted);
        Assert.Equal(2, second.Version);
        Assert.False(second.QuickSetupCompleted);
    }

    [Fact]
    public async Task Profile_service_validates_name_and_university()
    {
        var universities = SeededUniversities();
        var profiles = new InMemoryProfileRepository();
        var service = new ProfileService(profiles, new InMemoryPreferencesRepository(), universities, new ManualTimeProvider(Now));
        var uttt = universities.Items.Single(university => university.Code == "UTTT");

        var updated = await service.UpdateNameAsync("u1", new UpdateProfileRequest { Name = " Ana " }, CancellationToken.None);
        await service.SetUniversityAsync("u1", new SetUniversityRequest { UniversityId = uttt.Id.ToUpperInvariant() }, CancellationToken.None);
        var profile = await service.GetAsync("u1", CancellationToken.None);

        Assert.Equal("Ana", updated.Name);
        Assert.Equal("UTTT", profile.University?.Code);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.UpdateNameAsync("u1", new UpdateProfileRequest { Name = "   " }, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.UpdateNameAsync("u1", new UpdateProfileRequest { Name = new string('x', 81) }, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.SetUniversityAsync("u1", new SetUniversityRequest { UniversityId = "x" }, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SetUniversityAsync("u1", new SetUniversityRequest { UniversityId = "ffffffffffffffffffffffff" }, CancellationToken.None));
    }

    [Fact]
    public async Task University_search_requires_exactly_one_mode()
    {
        var service = new UniversitySearchService(SeededUniversities());

        await Assert.ThrowsAsync<RequestValidationException>(() => service.SearchAsync(null, null, null, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.SearchAsync("tula", "20", "-99", CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.SearchAsync(null, "20", null, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.SearchAsync(null, "Infinity", "-99", CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.SearchAsync("x", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task University_search_by_proximity_returns_sorted_distances()
    {
        var service = new UniversitySearchService(SeededUniversities());

        // Centro de Jilotepec.
        var result = await service.SearchAsync(null, "19.9529", "-99.5326", CancellationToken.None);

        Assert.Equal(10, result.Items.Count);
        Assert.StartsWith("TESJI", result.Items[0].Code, StringComparison.Ordinal);
        Assert.Equal(result.Items.Select(item => item.DistanceKm).OrderBy(distance => distance), result.Items.Select(item => item.DistanceKm));
    }

    [Fact]
    public void Haversine_distance_matches_known_value()
    {
        // UTTT ↔ UAEH Tepeji: unos 10.2 km en línea recta.
        var distance = UniversitySearchService.DistanceKm(20.00903, -99.34471, 19.91703, -99.36116);

        Assert.InRange(distance, 10.0, 10.5);
        Assert.Equal(0, UniversitySearchService.DistanceKm(20, -99, 20, -99));
    }

    [Fact]
    public void Seed_universities_all_have_regional_coordinates()
    {
        var seed = UniversitySeedData.Create();

        Assert.Equal(10, seed.Count);
        Assert.All(seed, university =>
        {
            Assert.NotNull(university.Latitude);
            Assert.NotNull(university.Longitude);
            Assert.InRange(university.Latitude!.Value, 19.6, 20.4);
            Assert.InRange(university.Longitude!.Value, -99.8, -99.0);
        });
    }

    private static PreferencesRequest Full() => new()
    {
        Budget = new BudgetRequest { Min = 1500, Max = 3500, Currency = "MXN" },
        MaxDistanceKm = 2.5m,
        EssentialServices = ["wifi", "water"],
        LivingWith = "any",
        PlaceType = "room",
        Pets = false,
        Transport = "public",
        PropertyTypes = ["house"],
    };

    private static SeedUniversities SeededUniversities()
    {
        var store = new SeedUniversities();
        var index = 0;
        foreach (var university in UniversitySeedData.Create())
        {
            store.Items.Add(new University
            {
                Id = $"0123456789abcd{index++:D10}",
                Code = university.Code,
                Name = university.Name,
                City = university.City,
                Country = university.Country,
                CreatedAt = university.CreatedAt,
                Latitude = university.Latitude,
                Longitude = university.Longitude,
            });
        }

        return store;
    }

    private sealed class SeedUniversities : TheHouse.Application.Abstractions.Persistence.IUniversityRepository
    {
        public List<University> Items { get; } = [];

        public Task<(IReadOnlyList<University> Items, long TotalItems)> ListAsync(int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<University>, long)>((Items, Items.Count));

        public Task<University?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(item => item.Id == id));

        public Task<IReadOnlyList<University>> SearchByTextAsync(string normalizedQuery, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<University>>(Items.Where(item => SearchText.UniversityKey(item.Code, item.Name, item.City).Contains(normalizedQuery, StringComparison.Ordinal)).Take(limit).ToList());

        public Task<IReadOnlyList<University>> SearchNearAsync(double latitude, double longitude, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<University>>(Items.Take(limit).ToList());

        public Task UpsertSeedAsync(IReadOnlyCollection<University> universities, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
