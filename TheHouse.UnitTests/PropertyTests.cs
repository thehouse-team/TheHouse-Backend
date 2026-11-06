using System.Text.Json;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Properties;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.UnitTests;

// NV-06 T07: paginación, totalPages, ObjectId, DTO y proyección.
public sealed class PropertyTests
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
    [InlineData(7, 50)]
    public async Task Boundary_pagination_is_accepted(
        int page,
        int pageSize)
    {
        var response = await List(page, pageSize);

        Assert.Equal(page, response.Page);
        Assert.Equal(pageSize, response.PageSize);
    }

    // --- totalPages ------------------------------------------------

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(50, 50, 1)]
    public static void Total_pages_is_ceiled_division(
        long totalItems,
        int pageSize,
        int expected)
    {
        Assert.Equal(
            expected,
            PropertyQueryValidation.CalculateTotalPages(
                totalItems,
                pageSize));
    }

    [Fact]
    public async Task Total_pages_reflects_stored_items()
    {
        var repository = new FakeRepository(Create(15, 0));
        var service = new PropertyQueryService(repository);

        var first =
            await service.ListAsync(1, 10, default);

        Assert.Equal(15, first.TotalItems);
        Assert.Equal(2, first.TotalPages);
    }

    // --- ObjectId --------------------------------------------------

    [Fact]
    public void Canonical_object_id_is_valid()
    {
        Assert.True(
            PropertyQueryValidation.IsObjectId(
                "665f1c2a9b1e4a0012345678"));
    }

    [Fact]
    public void Uppercase_object_id_is_accepted()
    {
        Assert.True(
            PropertyQueryValidation.IsObjectId(
                "665F1C2A9B1E4A0012345678"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("665f1c2a9b1e4a001234567")]
    [InlineData("665f1c2a9b1e4a00123456789")]
    [InlineData("665f1c2a9b1e4a001234567g")]
    [InlineData("665f1c2a9b1e4a001234567 ")]
    public void Malformed_object_id_is_rejected(string? id)
    {
        Assert.False(
            PropertyQueryValidation.IsObjectId(id));
    }

    // --- DTO y proyección ------------------------------------------

    [Fact]
    public async Task List_item_maps_exactly_public_fields()
    {
        var repository = new FakeRepository(Create(1, 0));

        var service = new PropertyQueryService(repository);

        var response =
            await service.ListAsync(1, 20, default);

        var item = Assert.Single(response.Items);

        Assert.Equal("0123456789abcdef01234560", item.Id);
        Assert.Equal("thehouse-0", item.Title);
        Assert.Equal(450.0m, item.Price);
        Assert.Equal("USD", item.Currency);
        Assert.Equal("San José", item.City);
        Assert.Equal(
            "665f1c2a9b1e4a0012345679",
            item.UniversityId);
        Assert.Equal(2, item.Bedrooms);
        Assert.Equal(1, item.Bathrooms);
        Assert.Equal(65.0, item.AreaM2);
    }

    [Fact]
    public async Task Detail_maps_exactly_public_fields()
    {
        var repository = new FakeRepository(Create(1, 0));
        var service = new PropertyQueryService(repository);

        var detail =
            await service.GetByIdAsync(
                "0123456789abcdef01234560",
                default);

        Assert.Equal(
            "0123456789abcdef01234560",
            detail.Id);

        Assert.Equal(
            "thehouse-0",
            detail.Title);

        Assert.Equal(
            "Descripción pública de la propiedad.",
            detail.Description);

        Assert.Equal(450.0m, detail.Price);
        Assert.Equal("USD", detail.Currency);
        Assert.Equal("San José", detail.City);
        Assert.Equal(
            "665f1c2a9b1e4a0012345679",
            detail.UniversityId);
        Assert.Equal(2, detail.Bedrooms);
        Assert.Equal(1, detail.Bathrooms);
        Assert.Equal(65.0, detail.AreaM2);
        Assert.Equal(
            new[] { "wifi", "amueblado" },
            detail.Amenities.ToArray());
    }

    [Fact]
    public async Task Unknown_detail_is_404_signal()
    {
        var service = new PropertyQueryService(
            new FakeRepository([]));

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetByIdAsync(
                "665f1c2a9b1e4a0012345678",
                default));
    }

    [Fact]
    public void List_item_serializes_only_public_names()
    {
        var item = new PropertyListItemResponse(
            "0123456789abcdef01234561",
            "Departamento A",
            450.0m,
            "USD",
            "San José",
            "665f1c2a9b1e4a0012345679",
            2,
            1,
            65.0,
            new DateTimeOffset(
                2026, 11, 2, 12, 0, 0,
                TimeSpan.Zero));

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
                "areaM2", "bathrooms", "bedrooms", "city",
                "createdAt", "currency", "id", "price",
                "title", "universityId"
            },
            names);
    }

    // --- Soportes --------------------------------------------------

    private static async Task<PropertyListResponse> List(
        int page,
        int pageSize)
    {
        var repository = new FakeRepository(
            Create(5, 0));

        var service = new PropertyQueryService(repository);

        return await service.ListAsync(
            page,
            pageSize,
            default);
    }

    private static Property[] Create(
        int count,
        int offset) =>
        Enumerable
            .Range(0, count)
            .Select(index =>
            {
                var minutes = offset + index;
                return new Property
                {
                    Id = $"0123456789abcdef0123456{index:X}",
                    Title = $"thehouse-{index}",
                    Description = "Descripción pública de la propiedad.",
                    Price = 450.0m + index,
                    Currency = "USD",
                    City = "San José",
                    UniversityId = "665f1c2a9b1e4a0012345679",
                    Bedrooms = 2,
                    Bathrooms = 1,
                    AreaM2 = 65,
                    Amenities = ["wifi", "amueblado"],
                    CreatedAt =
                        new DateTimeOffset(
                            2026, 11, 2, 12, minutes, 0,
                            TimeSpan.Zero)
                };
            })
            .ToArray();

    private sealed class FakeRepository(Property[] properties)
        : IPropertyRepository
    {
        public Task<(
            IReadOnlyList<Property> Items,
            long TotalItems)> ListAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            PropertyQueryValidation.EnsureValidPagination(
                page,
                pageSize);

            var items = properties
                .OrderByDescending(property => property.CreatedAt)
                .ThenByDescending(property => property.Id, StringComparer.Ordinal)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult(
                ((IReadOnlyList<Property>)items,
                (long)properties.Length));
        }

        public Task<Property?> GetByIdAsync(
            string id,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                properties.FirstOrDefault(
                    property => property.Id == id));

        public Task UpsertSeedAsync(
            IReadOnlyCollection<Property> seeded,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
