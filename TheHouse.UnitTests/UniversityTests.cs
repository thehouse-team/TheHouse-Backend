using System.Text.Json;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Universities;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.UnitTests;

// NV-08 T08: paginación, totalPages, ObjectId, mapping/DTO,
// serialización pública y datos de seed.
public sealed class UniversityTests
{
    // --- Paginación ------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Invalid_page_is_rejected(int page)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => List(page, 20));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    [InlineData(-5)]
    public async Task Invalid_page_size_is_rejected(int pageSize)
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => List(1, pageSize));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 50)]
    [InlineData(9, 50)]
    public async Task Boundary_pagination_is_accepted(
        int page,
        int pageSize)
    {
        var repository = new FakeRepository(CreateUniversities(5));

        var service = new UniversityQueryService(repository);

        var response =
            await service.ListAsync(page, pageSize, default);

        Assert.Equal(page, response.Page);
        Assert.Equal(pageSize, response.PageSize);
    }

    [Fact]
    public async Task Defaults_match_http_contract()
    {
        var repository = new FakeRepository(
            CreateUniversities(1));

        var service = new UniversityQueryService(repository);

        var response =
            await service.ListAsync(
                UniversityQueryValidation.DefaultPage,
                UniversityQueryValidation.DefaultPageSize,
                default);

        var item = Assert.Single(response.Items);

        Assert.Equal("thehouse-0", item.Name);

        Assert.Equal(1, response.Page);
        Assert.Equal(
            UniversityQueryValidation.DefaultPageSize,
            response.PageSize);
    }

    // --- totalPages ------------------------------------------------

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(19, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(50, 50, 1)]
    [InlineData(51, 50, 2)]
    public void Total_pages_covers_partials(
        long totalItems,
        int pageSize,
        int expected)
    {
        Assert.Equal(
            expected,
            UniversityQueryValidation.CalculateTotalPages(
                totalItems,
                pageSize));
    }

    [Fact]
    public async Task Empty_repository_yields_zero_total_pages()
    {
        var service = new UniversityQueryService(
            new FakeRepository([]));

        var response =
            await service.ListAsync(1, 20, default);

        Assert.Empty(response.Items);
        Assert.Equal(0, response.TotalItems);
        Assert.Equal(0, response.TotalPages);
    }

    // --- ObjectId ---------------------------------------------------

    [Fact]
    public void Canonical_object_id_is_valid()
    {
        Assert.True(
            UniversityQueryValidation.IsObjectId(
                "665f1c2a9b1e4a0012345678"));
    }

    [Theory]
    [InlineData("665F1C2A9B1E4A0012345678")]
    [InlineData("0123456789AbCdEf01234567")]
    public void Mixed_case_hex_is_accepted(string id)
    {
        Assert.True(
            UniversityQueryValidation.IsObjectId(id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("665f1c2a9b1e4a001234567")]
    [InlineData("665f1c2a9b1e4a00123456789")]
    [InlineData("665f1c2a9b1e4a001234567g")]
    [InlineData("665f-1c2a9b1e4a00123456")]
    [InlineData("665f1c2a9b1e4a001234\n78")]
    public void Malformed_object_id_is_rejected(string? id)
    {
        Assert.False(
            UniversityQueryValidation.IsObjectId(id));
    }

    // --- Mapping y DTO ---------------------------------------------

    [Fact]
    public async Task Detail_maps_exactly_public_fields()
    {
        var university = CreateUniversities(1)[0];

        var service = new UniversityQueryService(
            new FakeRepository([university]));

        var detail =
            await service.GetByIdAsync(university.Id, default);

        Assert.Equal(university.Id, detail.Id);
        Assert.Equal(university.Code, detail.Code);
        Assert.Equal(university.Name, detail.Name);
        Assert.Equal(university.City, detail.City);
        Assert.Equal(university.Country, detail.Country);
        Assert.Equal(university.CreatedAt, detail.CreatedAt);
    }

    [Fact]
    public void Mapping_rejects_extra_current_fields()
    {
        // El contrato público no incluye campos internos: si se
        // agrega uno al record sin revisión de spec, este test falla.
        var members =
            typeof(UniversityResponse).GetProperties()
                .Select(property =>
                    property.Name.ToLowerInvariant())
                .Order()
                .ToArray();

        Assert.Equal(
            new[]
            {
                "city", "code", "country", "createdat",
                "id", "name"
            },
            members);
    }

    [Fact]
    public void Response_serializes_only_public_names()
    {
        var item = new UniversityResponse(
            "0123456789abcdef01234560",
            "UTTT",
            "Universidad Tecnológica de Tula-Tepeji",
            "Tula de Allende",
            "México",
            new DateTimeOffset(
                2026, 11, 2, 0, 0, 0, TimeSpan.Zero));

        var json = JsonSerializer.Serialize(
            item,
            new JsonSerializerOptions(
                JsonSerializerDefaults.Web));

        using var document = JsonDocument.Parse(json);

        var names = document.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .Order()
            .ToArray();

        Assert.Equal(
            new[]
            {
                "city", "code", "country", "createdAt",
                "id", "name"
            },
            names);
    }

    // --- Seed data ---------------------------------------------------

    // NV-08 1.2.0-u2: catálogo regional Tula–Tepeji–Jilotepec (spec §7.1).
    private static readonly (string Code, string Name, string City)[] RegionalSeed =
    [
        ("UTTT", "Universidad Tecnológica de Tula-Tepeji", "Tula de Allende"),
        ("UAEH-ESTE", "Universidad Autónoma del Estado de Hidalgo, Escuela Superior de Tepeji del Río", "Tepeji del Río de Ocampo"),
        ("UAEH-ESTL", "Universidad Autónoma del Estado de Hidalgo, Escuela Superior de Tlahuelilpan", "Tlahuelilpan"),
        ("UAEH-ESAT", "Universidad Autónoma del Estado de Hidalgo, Escuela Superior de Atotonilco de Tula", "Atotonilco de Tula"),
        ("ITSOEH", "Instituto Tecnológico Superior del Occidente del Estado de Hidalgo", "Mixquiahuala de Juárez"),
        ("TESJI", "Tecnológico de Estudios Superiores de Jilotepec", "Jilotepec"),
        ("UMB-JILO", "Universidad Mexiquense del Bicentenario, Unidad de Estudios Superiores Jilotepec", "Jilotepec"),
        ("UMB-VDC", "Universidad Mexiquense del Bicentenario, Unidad de Estudios Superiores Villa del Carbón", "Villa del Carbón"),
        ("UCAN", "Universidad Canadiense", "Tula de Allende"),
        ("UNE-TULA", "Universidad del Nuevo México", "Tula de Allende"),
    ];

    [Fact]
    public void Seed_contains_exactly_ten_regional_universities()
    {
        var seed = UniversitySeedData.Create();

        Assert.Equal(10, seed.Count);
    }

    [Fact]
    public void Seed_codes_are_exact_and_unique()
    {
        var seed = UniversitySeedData.Create();

        Assert.Equal(
            RegionalSeed.Select(item => item.Code).Order(StringComparer.Ordinal).ToArray(),
            seed.Select(item => item.Code).Order(StringComparer.Ordinal).ToArray());

        Assert.Equal(
            seed.Count,
            seed.Select(item => item.Code)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Fact]
    public void Seed_matches_specification()
    {
        var seed = UniversitySeedData.Create()
            .ToDictionary(item => item.Code, StringComparer.Ordinal);

        foreach (var (code, name, city) in RegionalSeed)
        {
            Assert.Equal(name, seed[code].Name);
            Assert.Equal(city, seed[code].City);
        }

        // La sede de Pachuca (fuera de la región) ya no forma parte del seed.
        Assert.DoesNotContain(seed.Values, item => item.City == "Pachuca de Soto");
    }

    [Fact]
    public void Seed_country_is_mexico()
    {
        var seed = UniversitySeedData.Create();

        Assert.All(
            seed,
            item => Assert.Equal("México", item.Country));
    }

    [Fact]
    public void Seed_created_at_is_deterministic()
    {
        var seed = UniversitySeedData.Create();

        Assert.All(
            seed,
            item => Assert.Equal(
                new DateTimeOffset(
                    2026, 11, 2, 0, 0, 0, TimeSpan.Zero),
                item.CreatedAt));
    }

    [Fact]
    public void Repeated_seed_creation_is_logically_equal()
    {
        var first = UniversitySeedData.Create();
        var second = UniversitySeedData.Create();

        Assert.Equal(
            first.Select(item =>
                    (item.Code, item.Name, item.City, item.Country))
                .ToArray(),
            second.Select(item =>
                    (item.Code, item.Name, item.City, item.Country))
                .ToArray());
    }

    [Fact]
    public void Seed_has_no_personal_or_contact_data()
    {
        var seed = UniversitySeedData.Create();

        foreach (var item in seed)
        {
            var properties =
                typeof(University).GetProperties()
                    .Select(property => property.Name)
                    .Order()
                    .ToArray();

            Assert.Equal(
                new[]
                {
                    "City", "Code", "Country", "CreatedAt",
                    "Id", "Name"
                },
                properties);

            Assert.DoesNotContain("email", item.Name ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("phone", item.Name ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("@", item.City);
            Assert.DoesNotContain("@", item.Country);
        }
    }

    // --- Soportes ---------------------------------------------------

    private static async Task<UniversityListResponse> List(
        int page,
        int pageSize)
    {
        var repository = new FakeRepository(
            CreateUniversities(5));

        var service = new UniversityQueryService(repository);

        return await service.ListAsync(
            page,
            pageSize,
            default);
    }

    private static University[] CreateUniversities(
        int count) =>
        Enumerable
            .Range(0, count)
            .Select(index => new University
            {
                Id = $"0123456789abcdef0123456{index:X}",
                Code = $"U{index:D3}",
                Name = $"thehouse-{index}",
                City = index % 2 == 0
                    ? "Pachuca de Soto"
                    : "Tula de Allende",
                Country = "México",
                CreatedAt = new DateTimeOffset(
                    2026, 11, 2, 0, index, 0, TimeSpan.Zero)
            })
            .ToArray();

    // Un fake con orden estable name ASC / id ASC para validar
    // la lógica del repositorio replicada en el fake de integración.
    private sealed class FakeRepository(University[] source)
        : IUniversityRepository
    {
        public Task<(
            IReadOnlyList<University> Items,
            long TotalItems)> ListAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            UniversityQueryValidation.EnsureValidPagination(
                page,
                pageSize);

            var ordered = source
                .OrderBy(
                    item => item.Name,
                    StringComparer.Ordinal)
                .ThenBy(
                    item => item.Id,
                    StringComparer.Ordinal)
                .ToList();

            var items = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult(
                (
                (IReadOnlyList<University>)items,
                (long)ordered.Count));
        }

        public Task<University?> GetByIdAsync(
            string id,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                source.FirstOrDefault(
                    item => item.Id == id));

        public Task UpsertSeedAsync(
            IReadOnlyCollection<University> universities,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException(
                "seed path covered in dedicated tests");
    }
}
