using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.IntegrationTests;

// NV-08 T09: listado y detalle con fakes aislados (sin MongoDB real).
public sealed class UniversityHttpTests
{
    private const string ListPath = "/api/v1/universities";

    [Fact]
    public async Task List_returns_stable_sorted_public_data()
    {
        using var factory = new UniversityFactory();
        Seed(factory.Universities);
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(ListPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = body.RootElement;

        // Orden estable: name ASC, luego id ASC.
        Assert.Equal(
            new[] { "AAA", "ITSOEH", "TESJI", "UAEH-ESAT", "UAEH-ESTE", "UAEH-ESTL", "UCAN", "UMB-JILO", "UMB-VDC", "UTTT", "UNE-TULA" },
            root.GetProperty("items")
                .EnumerateArray()
                .Select(item => item.GetProperty("code").GetString())
                .ToArray());

        Assert.Equal(11, root.GetProperty("totalItems").GetInt64());
        Assert.Equal(1, root.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Same_name_orders_by_id()
    {
        using var factory = new UniversityFactory();

        factory.Universities.Universities.Add(
            new University
            {
                Id = "0123456789abcdef01234561",
                Code = "IDB",
                Name = "Igual",
                City = "P",
                Country = "México",
                CreatedAt = DateTimeOffset.UtcNow
            });

        factory.Universities.Universities.Add(
            new University
            {
                Id = "0123456789abcdef01234560",
                Code = "IDA",
                Name = "Igual",
                City = "P",
                Country = "México",
                CreatedAt = DateTimeOffset.UtcNow
            });

        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(ListPath);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var codes = body.RootElement
            .GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("code").GetString())
            .ToArray();

        Assert.Equal(new[] { "IDA", "IDB" }, codes);
    }

    [Fact]
    public async Task Defaults_are_1_and_20()
    {
        using var factory = new UniversityFactory();
        Seed(factory.Universities);
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(ListPath);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = body.RootElement;

        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(11, root.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Pagination_returns_correct_slice()
    {
        using var factory = new UniversityFactory();
        Seed(factory.Universities);
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(
            $"{ListPath}?page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = body.RootElement;

        Assert.Equal(2, root.GetProperty("page").GetInt32());
        Assert.Equal(2, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(11, root.GetProperty("totalItems").GetInt64());
        Assert.Equal(6, root.GetProperty("totalPages").GetInt32());

        var items = root.GetProperty("items");

        Assert.Equal(2, items.GetArrayLength());

        var names = items
            .EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToArray();

        Assert.Equal(
            new[] { "Tecnológico de Estudios Superiores de Jilotepec", "Universidad Autónoma del Estado de Hidalgo, Escuela Superior de Atotonilco de Tula" },
            names);
    }

    [Fact]
    public async Task Empty_list_is_200_with_zero_totals()
    {
        using var factory = new UniversityFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(ListPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = body.RootElement;

        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
        Assert.Equal(0, root.GetProperty("totalItems").GetInt64());
        Assert.Equal(0, root.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Out_of_range_page_is_200_empty()
    {
        using var factory = new UniversityFactory();
        Seed(factory.Universities);
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(
            $"{ListPath}?page=99&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = body.RootElement;

        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
        Assert.Equal(1, root.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Unknown_query_parameters_are_ignored()
    {
        using var factory = new UniversityFactory();
        Seed(factory.Universities);
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(
            $"{ListPath}?country=México&foo=bar&page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var items = body.RootElement.GetProperty("items");

        Assert.Equal(2, items.GetArrayLength());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("page=abc")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=51")]
    [InlineData("pageSize=xyz")]
    [InlineData("page=1&page=2")]
    public async Task Invalid_pagination_is_400(string query)
    {
        using var factory = new UniversityFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(
            $"{ListPath}?{query}");

        await ErrorContractTests.AssertProblem(response, 400);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "Validación fallida",
            body.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Existing_detail_returns_public_fields()
    {
        using var factory = new UniversityFactory();
        var ids = Seed(factory.Universities);
        using var client = factory.CreateHttpsClient();

        var utttId = ids["UTTT"];

        using var response = await client.GetAsync(
            $"{ListPath}/{utttId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = body.RootElement;

        Assert.Equal(utttId, root.GetProperty("id").GetString());
        Assert.Equal("UTTT", root.GetProperty("code").GetString());
        Assert.Equal("Universidad Tecnológica de Tula-Tepeji", root.GetProperty("name").GetString());
        Assert.Equal("Tula de Allende", root.GetProperty("city").GetString());
        Assert.Equal("México", root.GetProperty("country").GetString());
        Assert.True(
            root.GetProperty("createdAt").GetDateTimeOffset()
                == new DateTimeOffset(
                    2026, 11, 2, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Unknown_valid_id_is_404()
    {
        using var factory = new UniversityFactory();
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
    [InlineData("/api/v1/universities/not-a-24hex-id")]
    [InlineData("/api/v1/universities/0123456789abcdef0123456g")]
    [InlineData("/api/v1/universities/0123456789abcdef0123456")]
    public async Task Malformed_id_is_400(string path)
    {
        using var factory = new UniversityFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(path);

        await ErrorContractTests.AssertProblem(response, 400);
    }

    [Fact]
    public async Task Repository_failure_is_503_fail_closed()
    {
        using var factory = new UniversityFactory
        {
            Universities = { Unavailable = true }
        };

        using var client = factory.CreateHttpsClient();

        using var list = await client.GetAsync(ListPath);
        await ErrorContractTests.AssertProblem(list, 503);

        using var detail = await client.GetAsync(
            $"{ListPath}/0123456789abcdef01234560");
        await ErrorContractTests.AssertProblem(detail, 503);
    }

    [Fact]
    public async Task Rate_limit_is_429_with_retry_after_60()
    {
        using var factory = new UniversityFactory
        {
            RequestsPerWindow = 2
        };

        using var client = factory.CreateHttpsClient();

        using var first = await client.GetAsync(ListPath);
        using var second = await client.GetAsync(ListPath);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        using var third = await client.GetAsync(ListPath);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal(60, third.Headers.RetryAfter!.Delta!.Value.TotalSeconds);

        await ErrorContractTests.AssertProblem(third, 429);

        using var detail = await client.GetAsync(
            $"{ListPath}/0123456789abcdef01234560");
        Assert.Equal(HttpStatusCode.TooManyRequests, detail.StatusCode);

        using var body = JsonDocument.Parse(
            await third.Content.ReadAsStringAsync());

        Assert.Equal(
            "Demasiadas solicitudes",
            body.RootElement.GetProperty("title").GetString());
    }

    // --- Soportes --------------------------------------------------

    internal static Dictionary<string, string> Seed(
        UniversityTestStore store)
    {
        // Exactamente el conjunto del seed real (catálogo regional NV-08 1.2.0-u2).
        var seed = UniversitySeedData.Create();
        var index = 0;

        foreach (var university in seed)
        {
            store.Universities.Add(
                new University
                {
                    Id = $"0123456789876543{index++:D8}",
                    Code = university.Code,
                    Name = university.Name,
                    City = university.City,
                    Country = university.Country,
                    CreatedAt = university.CreatedAt
                });
        }

        // Una universidad ajena al seed con name que ordena primero.
        store.Universities.Add(
            new University
            {
                Id = "0123456789abcdef09876543",
                Code = "AAA",
                Name = "AAA",
                City = "Ciudad",
                Country = "México",
                CreatedAt =
                    new DateTimeOffset(
                        2026, 10, 1, 0, 0, 0, TimeSpan.Zero)
            });

        return store.Universities
            .ToDictionary(item => item.Code, item => item.Id);
    }
}
