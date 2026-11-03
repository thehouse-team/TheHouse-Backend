using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using TheHouse.Http;

namespace TheHouse.IntegrationTests;

public sealed class ErrorContractTests
{
    [Theory]
    [InlineData("validation", 400)]
    [InlineData("bad-request", 400)]
    [InlineData("timeout", 504)]
    [InlineData("cancel", 503)]
    [InlineData("unknown", 500)]
    public async Task Exceptions_have_safe_problem_details(string kind, int expected)
    {
        using var factory = new ProbeFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync($"/api/v1/foundation-test/exception/{kind}");
        await AssertProblem(response, expected);
        Assert.DoesNotContain("internal-marker", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"name\":\"ok\",\"count\":0}")]
    [InlineData("{\"name\":\"ok\",\"count\":\"raw-input-marker\"}")]
    [InlineData("{\"name\":\"ok\",\"count\":1,\"unexpected\":true}")]
    [InlineData("{")]
    public async Task Invalid_json_and_models_return_sanitized_400(string json)
    {
        using var factory = new ProbeFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.PostAsync("/api/v1/foundation-test/validate", new StringContent(json, Encoding.UTF8, "application/json"));
        await AssertProblem(response, 400);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("raw-input-marker", body);
        Assert.True(JsonDocument.Parse(body).RootElement.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Valid_input_is_processed_at_http_boundary()
    {
        using var factory = new ProbeFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.PostAsJsonAsync("/api/v1/foundation-test/validate", new { name = "example", count = 1 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Large_body_returns_413()
    {
        using var factory = new ProbeFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.PostAsync("/api/v1/foundation-test/validate", new StringContent(new string('a', 65537), Encoding.UTF8, "application/json"));
        await AssertProblem(response, 413);
    }

    [Fact]
    public async Task Request_timeout_returns_safe_504()
    {
        using var factory = new ProbeFactory(new Dictionary<string, string?> { ["Foundation:RequestTimeoutSeconds"] = "1" });
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync("/api/v1/foundation-test/timeout");
        await AssertProblem(response, 504);
    }

    [Fact]
    public async Task Client_cancellation_does_not_attempt_a_response()
    {
        var context = new DefaultHttpContext { RequestAborted = new CancellationToken(true) };
        context.Response.Body = new MemoryStream();
        var middleware = new FoundationExceptionMiddleware(_ => throw new OperationCanceledException(), NullLogger<FoundationExceptionMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(499, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task Unknown_route_is_problem_details_even_with_non_json_accept()
    {
        using var factory = new FoundationFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        using var response = await client.GetAsync("/not-a-route");
        await AssertProblem(response, 404);
    }

    internal static async Task AssertProblem(HttpResponseMessage response, int expected)
    {
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(expected, json.RootElement.GetProperty("status").GetInt32());
        Assert.StartsWith("https://httpstatuses.io/", json.RootElement.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
        Assert.False(json.RootElement.TryGetProperty("detail", out _));
        Assert.False(json.RootElement.TryGetProperty("instance", out _));
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", body, StringComparison.OrdinalIgnoreCase);
    }
}
