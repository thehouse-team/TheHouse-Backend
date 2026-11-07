using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;

namespace TheHouse.IntegrationTests;

// NV-08 T09 (seed aislado): el seed REAL de MongoDB no se ejecuta.
// Se usa exclusivamente el fake `UniversityTestStore` con la
// semántica de upsert por `code` del repositorio real.
public sealed class UniversitySeedTests
{
    [Fact]
    public async Task Seed_on_empty_store_creates_exactly_ten_regional()
    {
        var store = new UniversityTestStore();
        var service = new UniversitySeedService(store);

        Assert.Empty(store.Universities);

        await service.SeedAsync(default);

        // NV-08 1.2.0-u2: catálogo regional Tula–Tepeji–Jilotepec.
        Assert.Equal(
            new[] { "ITSOEH", "TESJI", "UAEH-ESAT", "UAEH-ESTE", "UAEH-ESTL", "UCAN", "UMB-JILO", "UMB-VDC", "UNE-TULA", "UTTT" },
            store.Universities
                .Select(item => item.Code)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public async Task Repeated_seed_does_not_duplicate()
    {
        var store = new UniversityTestStore();
        var service = new UniversitySeedService(store);

        await service.SeedAsync(default);
        await service.SeedAsync(default);

        Assert.Equal(10, store.Universities.Count);
    }

    [Fact]
    public async Task Seed_keeps_non_seed_universities()
    {
        var store = new UniversityTestStore();
        var service = new UniversitySeedService(store);

        store.Universities.Add(
            new University
            {
                Id = "0123456789abcdef09876543",
                Code = "AAA",
                Name = "Ajena al seed",
                City = "Ciudad",
                Country = "México",
                CreatedAt = new DateTimeOffset(
                    2026, 10, 1, 0, 0, 0, TimeSpan.Zero)
            });

        await service.SeedAsync(default);

        Assert.Equal(11, store.Universities.Count);
        Assert.Contains(
            store.Universities,
            item => item.Code == "AAA");
    }

    [Fact]
    public async Task Second_seed_updates_by_code()
    {
        var store = new UniversityTestStore();
        var service = new UniversitySeedService(store);

        await service.SeedAsync(default);

        var original = store.Universities
            .ToDictionary(item => item.Code, item => item.Id);

        await service.SeedAsync(default);

        Assert.Equal(10, store.Universities.Count);

        // El segundo seed actualiza por `code` en lugar de insertar:
        // los identificadores no cambian.
        foreach (var item in store.Universities)
        {
            Assert.Equal(
                original[item.Code],
                item.Id);
        }
    }

    [Fact]
    public async Task Upsert_preserves_original_created_at()
    {
        var store = new UniversityTestStore();
        var service = new UniversitySeedService(store);

        await service.SeedAsync(default);

        var createdBefore = store.Universities
            .ToDictionary(item => item.Code, item => item.CreatedAt);

        // Un segundo seed no altera `createdAt` de los documentos.
        await service.SeedAsync(default);

        foreach (var item in store.Universities)
        {
            Assert.Equal(
                createdBefore[item.Code],
                item.CreatedAt);
        }
    }

    [Fact]
    public async Task Upsert_applies_seed_field_updates()
    {
        var store = new UniversityTestStore();
        var service = new UniversitySeedService(store);

        // Un registro de seed con datos desactualizados.
        store.Universities.Add(
            new University
            {
                Id = "0123456789abcdef01234560",
                Code = "UTTT",
                Name = "Nombre anterior",
                City = "Ciudad anterior",
                Country = "País anterior",
                CreatedAt = new DateTimeOffset(
                    2026, 9, 1, 0, 0, 0, TimeSpan.Zero)
            });

        await service.SeedAsync(default);

        Assert.Equal(10, store.Universities.Count);

        var uttt = store.Universities
            .First(item => item.Code == "UTTT");

        Assert.Equal(
            "Universidad Tecnológica de Tula-Tepeji",
            uttt.Name);
        Assert.Equal("Tula de Allende", uttt.City);
        Assert.Equal("México", uttt.Country);

        // El upsert conserva el `createdAt` original.
        Assert.Equal(
            new DateTimeOffset(
                2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            uttt.CreatedAt);
    }

    [Fact]
    public async Task Seed_is_disabled_by_default()
    {
        // Sin configuración, `UniversitySeedOptions.Enabled` es false:
        // el host no ejecuta el seed en el arranque normal.
        using var factory = new UniversityFactory();

        var options = factory.Services
            .GetRequiredService<UniversitySeedOptions>();

        Assert.False(options.Enabled);
    }

    [Fact]
    public async Task Store_failure_during_seed_is_fail_closed()
    {
        var store = new UniversityTestStore
        {
            Unavailable = true
        };

        var service = new UniversitySeedService(store);

        await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => service.SeedAsync(default));
    }
}
