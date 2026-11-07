using System.Net;
using System.Text.Json;
using TheHouse.Application.Common;

namespace TheHouse.IntegrationTests;

// NV-11 T04–T07: búsqueda pública de universidades por texto y por
// cercanía sobre el seed real (10 universidades con coordenadas).
public sealed class UniversitySearchHttpTests
{
    private const string SearchPath = "/api/v1/universities/search";

    [Theory]
    [InlineData("tula", "UTTT")]
    [InlineData("TÉPEJI", "UAEH-ESTE")]
    [InlineData("uaeh", "UAEH-ESTL")]
    [InlineData("jilotepec", "TESJI")]
    [InlineData("villa del carbon", "UMB-VDC")]
    [InlineData("tula-tepeji", "UTTT")]
    public async Task Text_search_is_partial_and_ignores_accents_and_case(string query, string expectedCode)
    {
        using var factory = new UserResourceFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync($"{SearchPath}?q={Uri.EscapeDataString(query)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("public", response.Headers.CacheControl?.ToString());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(items, item => item.GetProperty("code").GetString() == expectedCode);
        Assert.All(items, item => Assert.Equal(
            new[] { "id", "code", "name", "city" },
            item.EnumerateObject().Select(property => property.Name)));
    }

    [Fact]
    public async Task Text_search_matches_all_three_uaeh_schools()
    {
        using var factory = new UserResourceFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync($"{SearchPath}?q=uaeh");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            new[] { "UAEH-ESAT", "UAEH-ESTE", "UAEH-ESTL" },
            document.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("code").GetString()).OrderBy(code => code));
    }

    [Fact]
    public async Task Near_search_orders_by_distance_and_is_not_cached()
    {
        using var factory = new UserResourceFactory();
        using var client = factory.CreateHttpsClient();

        // Coordenadas del campus de la UTTT.
        using var response = await client.GetAsync($"{SearchPath}?lat=20.00903&lng=-99.34471");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(10, items.Length);
        Assert.Equal("UTTT", items[0].GetProperty("code").GetString());
        Assert.Equal(0.0, items[0].GetProperty("distanceKm").GetDouble());
        var distances = items.Select(item => item.GetProperty("distanceKm").GetDouble()).ToArray();
        Assert.Equal(distances.OrderBy(distance => distance), distances);
        Assert.All(distances, distance => Assert.InRange(distance, 0, 60));
    }

    [Theory]
    [InlineData("?q=a")]
    [InlineData("?q=--")]
    [InlineData("?lat=20&lng=-99&q=tula")]
    [InlineData("?lat=20")]
    [InlineData("?lat=91&lng=-99")]
    [InlineData("?lat=20&lng=-181")]
    [InlineData("?lat=NaN&lng=-99")]
    [InlineData("?lat=abc&lng=-99")]
    [InlineData("?q=tula&q=tepeji")]
    [InlineData("")]
    public async Task Invalid_queries_return_400(string query)
    {
        using var factory = new UserResourceFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(SearchPath + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Long_query_returns_400()
    {
        using var factory = new UserResourceFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync($"{SearchPath}?q={new string('t', 61)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rate_limit_by_ip_and_fail_closed()
    {
        using var factory = new UserResourceFactory(new UserRateLimitOptions { UniversitySearchPerWindow = 1 });
        using var client = factory.CreateHttpsClient();

        (await client.GetAsync($"{SearchPath}?q=tula")).Dispose();
        using var limited = await client.GetAsync($"{SearchPath}?q=tula");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));

        using var other = new UserResourceFactory();
        using var otherClient = other.CreateHttpsClient();
        other.Universities.Unavailable = true;
        using var mongoDown = await otherClient.GetAsync($"{SearchPath}?q=tula");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, mongoDown.StatusCode);
    }

    [Fact]
    public async Task Coordinates_are_not_written_to_logs()
    {
        using var factory = new UserResourceFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync($"{SearchPath}?lat=19.987654&lng=-99.123456");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(factory.Logs, line => line.Contains("19.987654", StringComparison.Ordinal) || line.Contains("99.123456", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Existing_university_endpoints_do_not_expose_coordinates()
    {
        using var factory = new UserResourceFactory();
        using var client = factory.CreateHttpsClient();
        var uttt = factory.Universities.Universities.Single(university => university.Code == "UTTT");

        using var list = await client.GetAsync("/api/v1/universities?pageSize=50");
        using var detail = await client.GetAsync($"/api/v1/universities/{uttt.Id}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var document = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        Assert.Equal(
            new[] { "id", "code", "name", "city", "country", "createdAt" },
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.DoesNotContain("20.009", await list.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
