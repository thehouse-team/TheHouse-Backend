using Microsoft.Extensions.DependencyInjection;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;

namespace TheHouse.IntegrationTests;

// NV-07: el seed REAL de MongoDB no se ejecuta. Se usa
// exclusivamente el fake `PropertyTestStore` con la semántica de
// upsert por `_id` del repositorio real. Las propiedades se vinculan
// con las universidades del seed NV-08 por `code`.
public sealed class PropertySeedTests
{
    [Fact]
    public async Task Seed_on_empty_store_creates_twelve_linked_properties()
    {
        var universities = new UniversityTestStore();
        await new UniversitySeedService(universities)
            .SeedAsync(default);

        var properties = new PropertyTestStore();
        var service = new PropertySeedService(
            properties,
            universities);

        await service.SeedAsync(default);

        Assert.Equal(12, properties.Properties.Count);
        Assert.Equal(
            12,
            properties.Properties
                .Select(property => property.Id)
                .Distinct(StringComparer.Ordinal)
                .Count());

        var universityIds = universities.Universities
            .Select(university => university.Id)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(
            properties.Properties,
            property =>
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        property.UniversityId));
                Assert.Contains(
                    property.UniversityId!,
                    universityIds);
            });
    }

    [Fact]
    public async Task Repeated_seed_does_not_duplicate()
    {
        var universities = new UniversityTestStore();
        await new UniversitySeedService(universities)
            .SeedAsync(default);

        var properties = new PropertyTestStore();
        var service = new PropertySeedService(
            properties,
            universities);

        await service.SeedAsync(default);
        await service.SeedAsync(default);

        Assert.Equal(12, properties.Properties.Count);
    }

    [Fact]
    public async Task Seed_keeps_non_seed_properties()
    {
        var universities = new UniversityTestStore();
        await new UniversitySeedService(universities)
            .SeedAsync(default);

        var properties = new PropertyTestStore();

        properties.Properties.Add(
            new Property
            {
                Id = "0123456789abcdef01234567",
                Title = "Propiedad ajena al seed",
                Price = 1000.0m,
                Currency = "MXN",
                City = "Ciudad",
                UniversityId = null,
                Bedrooms = 1,
                Bathrooms = 1,
                AreaM2 = 30,
                Amenities = [],
                CreatedAt = new DateTimeOffset(
                    2026, 10, 1, 0, 0, 0, TimeSpan.Zero)
            });

        var service = new PropertySeedService(
            properties,
            universities);

        await service.SeedAsync(default);

        Assert.Equal(13, properties.Properties.Count);
        Assert.Contains(
            properties.Properties,
            property => property.Title == "Propiedad ajena al seed");
    }

    [Fact]
    public async Task Second_seed_preserves_created_at()
    {
        var universities = new UniversityTestStore();
        await new UniversitySeedService(universities)
            .SeedAsync(default);

        var properties = new PropertyTestStore();
        var service = new PropertySeedService(
            properties,
            universities);

        await service.SeedAsync(default);

        var createdBefore = properties.Properties
            .ToDictionary(
                property => property.Id,
                property => property.CreatedAt,
                StringComparer.Ordinal);

        await service.SeedAsync(default);

        foreach (var property in properties.Properties)
        {
            Assert.Equal(
                createdBefore[property.Id],
                property.CreatedAt);
        }
    }

    [Fact]
    public async Task Seed_requires_university_seed()
    {
        var universities = new UniversityTestStore();
        var properties = new PropertyTestStore();
        var service = new PropertySeedService(
            properties,
            universities);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SeedAsync(default));

        Assert.Empty(properties.Properties);
    }

    [Fact]
    public void Seed_is_disabled_by_default()
    {
        using var factory = new PropertyFactory();

        var options = factory.Services
            .GetRequiredService<PropertySeedOptions>();

        Assert.False(options.Enabled);
    }

    [Fact]
    public async Task Store_failure_during_seed_is_fail_closed()
    {
        var universities = new UniversityTestStore();
        await new UniversitySeedService(universities)
            .SeedAsync(default);

        var properties = new PropertyTestStore
        {
            Unavailable = true
        };

        var service = new PropertySeedService(
            properties,
            universities);

        await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => service.SeedAsync(default));
    }
}
