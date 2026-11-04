using System.Net;
using System.Text.Json;

namespace TheHouse.IntegrationTests;

public sealed class PaginationHttpTests
{
    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("page=2147483648")]
    [InlineData("page=invalid-marker")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=-1")]
    [InlineData("pageSize=51")]
    [InlineData("pageSize=invalid-marker")]
    public async Task Invalid_pagination_returns_400_not_unbounded_results(string query)
    {
        using var factory = new ProbeFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync("/api/v1/foundation-test/pagination?" + query);
        await ErrorContractTests.AssertProblem(response, 400);
        Assert.DoesNotContain("invalid-marker", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("", 1, 20)]
    [InlineData("?page=2&pageSize=50", 2, 50)]
    [InlineData("?unknown=ignored", 1, 20)]
    public async Task Pagination_contract_has_exact_metadata_and_allows_empty_pages(string query, int page, int size)
    {
        using var factory = new ProbeFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync("/api/v1/foundation-test/pagination" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;
        Assert.Equal(new[] { "items", "page", "pageSize", "totalItems", "totalPages" }, body.EnumerateObject().Select(property => property.Name));
        Assert.Equal(page, body.GetProperty("page").GetInt32());
        Assert.Equal(size, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
        Assert.Equal(0, body.GetProperty("totalItems").GetInt64());
        Assert.Equal(0, body.GetProperty("totalPages").GetInt64());
    }
}
