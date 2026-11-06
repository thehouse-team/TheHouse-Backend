using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.IntegrationTests;

// NV-07 T08: listado con datos, vacío, página fuera de rango,
// detalle 200/400/404/429/503 con Problem Details sanitizados.
public sealed class PropertyHttpTests
{
    private const string ListPath = "/api/v1/properties";

    [Fact]
    public async Task List_returns_paginated_public_data()
    {
        using var factory = new PropertyFactory();

        Seed(factory.Properties);

        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(
            $"{ListPath}?page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = body.RootElement;

        Assert.Equal(2, root.GetProperty("page").GetInt32());
        Assert.Equal(2, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, root.GetProperty("totalItems").GetInt64());
        Assert.Equal(2, root.GetProperty("totalPages").GetInt32());

        var items = root.GetProperty("items");

        Assert.Equal(1, items.GetArrayLength());

        // Orden estable: createdAt descendente.
        Assert.Equal("thehouse-0", items[0].GetProperty("title").GetString());

        // Solo campos públicos en el ítem.
        var names = items[0].EnumerateObject()
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

    [Fact]
    public async Task Empty_list_is_200_with_zero_totals()
    {
        using var factory = new PropertyFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(ListPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = body.RootElement;

        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
        Assert.Equal(0, root.GetProperty("totalItems").GetInt64());
        Assert.Equal(0, root.GetProperty("totalPages").GetInt32());
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Out_of_range_page_is_200_empty()
    {
        using var factory = new PropertyFactory();
        Seed(factory.Properties);
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(
            $"{ListPath}?page=99&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        // La página fuera de rango no es un error: vuelve vacía,
        // pero totalPages refleja el total real.
        Assert.Equal(
            0,
            body.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(
            1,
            body.RootElement.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Default_pagination_is_applied()
    {
        using var factory = new PropertyFactory();
        Seed(factory.Properties);
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(ListPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = body.RootElement;

        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Detail_returns_public_fields()
    {
        using var factory = new PropertyFactory();
        Seed(factory.Properties);
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(
            $"{ListPath}/0123456789abcdef01234561");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = body.RootElement;

        Assert.Equal("0123456789abcdef01234561", root.GetProperty("id").GetString());
        Assert.Equal("thehouse-1", root.GetProperty("title").GetString());
        Assert.Equal("Descripción pública de la propiedad.", root.GetProperty("description").GetString());
        Assert.Equal("wifi", root.GetProperty("amenities")[0].GetString());

        var names = root.EnumerateObject()
            .Select(property => property.Name)
            .Order()
            .ToArray();

        Assert.Equal(
            new[]
            {
                "amenities", "areaM2", "bathrooms", "bedrooms", "city",
                "createdAt", "currency", "description", "id", "price",
                "title", "universityId"
            },
            names);
    }

    [Fact]
    public async Task Unknown_but_valid_id_is_404()
    {
        using var factory = new PropertyFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(
            $"{ListPath}/665f1c2a9b1e4a0012999990");

        await ErrorContractTests.AssertProblem(response, 404);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "Recurso no encontrado",
            body.RootElement.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("/api/v1/properties/not-a-24hex-id")]
    [InlineData("/api/v1/properties/0123456789abcdef0123456g")]
    [InlineData("/api/v1/properties/0123456789abcdef0123456")]
    public async Task Malformed_id_is_400(string path)
    {
        using var factory = new PropertyFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(path);

        await ErrorContractTests.AssertProblem(response, 400);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "Validación fallida",
            body.RootElement.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("page=abc")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=51")]
    [InlineData("pageSize=jiji")]
    [InlineData("page=1&page=2")]
    public async Task Invalid_pagination_is_400(string query)
    {
        using var factory = new PropertyFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(
            $"{ListPath}?{query}");

        await ErrorContractTests.AssertProblem(response, 400);
    }

    [Fact]
    public async Task Repository_failure_is_503_fail_closed()
    {
        using var factory = new PropertyFactory
        {
            Properties = { Unavailable = true }
        };

        using var client = factory.CreateHttpsClient();

        using var list = await client.GetAsync(ListPath);
        await ErrorContractTests.AssertProblem(list, 503);

        using var detail = await client.GetAsync(
            $"{ListPath}/0123456789abcdef01234561");
        await ErrorContractTests.AssertProblem(detail, 503);

        using var body = JsonDocument.Parse(
            await list.Content.ReadAsStringAsync());

        var raw =
            await list.Content.ReadAsStringAsync() +
            await detail.Content.ReadAsStringAsync();

        Assert.DoesNotContain(
            "Mongo",
            raw,
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            "Servicio no disponible",
            body.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Rate_limit_is_429_with_retry_after()
    {
        using var factory = new PropertyFactory
        {
            RequestsPerWindow = 2
        };

        Seed(factory.Properties);
        using var client = factory.CreateHttpsClient();

        using var first = await client.GetAsync(
            $"{ListPath}?pageSize=1");

        using var second = await client.GetAsync(
            $"{ListPath}?page=2&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        using var third = await client.GetAsync(ListPath);

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal(60, third.Headers.RetryAfter!.Delta!.Value.TotalSeconds);

        await ErrorContractTests.AssertProblem(third, 429);

        using var body = JsonDocument.Parse(
            await third.Content.ReadAsStringAsync());

        Assert.Equal(
            "Demasiadas solicitudes",
            body.RootElement.GetProperty("title").GetString());
    }

    // --- Soportes --------------------------------------------------

    private static Property Seed(
        PropertyTestStore store)
    {
        store.Properties.Add(
            new Property
            {
                Id = "0123456789abcdef01234560",
                Title = "thehouse-0",
                Description = "Descripción pública de la propiedad.",
                Price = 450.0m,
                Currency = "USD",
                City = "San José",
                UniversityId = "665f1c2a9b1e4a0012345679",
                Bedrooms = 2,
                Bathrooms = 1,
                AreaM2 = 65,
                Amenities = ["wifi", "amueblado"],
                CreatedAt = At(-2)
            });

        store.Properties.Add(
            new Property
            {
                Id = "0123456789abcdef01234561",
                Title = "thehouse-1",
                Description = "Descripción pública de la propiedad.",
                Price = 451.0m,
                Currency = "USD",
                City = "San José",
                UniversityId = "665f1c2a9b1e4a0012345679",
                Bedrooms = 2,
                Bathrooms = 1,
                AreaM2 = 65,
                Amenities = ["wifi"],
                CreatedAt = At(-1)
            });

        var third =
            new Property
            {
                Id = "0123456789abcdef01234562",
                Title = "thehouse-2",
                Description = "Descripción pública de la propiedad.",
                Price = 452.0m,
                Currency = "USD",
                City = "Heredia",
                UniversityId = "665f1c2a9b1e4a001234567a",
                Bedrooms = 3,
                Bathrooms = 2,
                AreaM2 = 80,
                Amenities = ["wifi", "parqueo"],
                CreatedAt = At(0)
            };

        store.Properties.Add(third);

        return third;
    }

    private static DateTimeOffset At(int index) =>
        new DateTimeOffset(
            2026, 11, 2, 12, 0, 0,
            TimeSpan.Zero)
        .AddMinutes(index);
}
