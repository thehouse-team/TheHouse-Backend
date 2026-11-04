using System.Net;
using System.Text.Json;
using TheHouse.Http;

namespace TheHouse.IntegrationTests;

public sealed class OpenApiContractTests
{
    [Fact]
    public async Task Openapi_is_reproducible_versioned_and_contains_only_authorized_operations()
    {
        using var factory = new FoundationFactory();
        using var client = factory.CreateHttpsClient();
        var first = await client.GetStringAsync("/openapi/v1.json");
        var second = await client.GetStringAsync("/openapi/v1.json");
        Assert.Equal(first, second);
        using var document = JsonDocument.Parse(first);
        Assert.StartsWith("3.", document.RootElement.GetProperty("openapi").GetString());
        Assert.Equal(ApiConventions.Version, document.RootElement.GetProperty("info").GetProperty("version").GetString());
        Assert.Equal("/api/v1", ApiConventions.Prefix);
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
            Assert.True(path.Name.StartsWith("/health/", StringComparison.Ordinal) || path.Name == RegistrationEndpoint.Path || path.Name == LoginEndpoint.Path || path.Name == LogoutEndpoint.Path);
        Assert.DoesNotContain("WeatherForecast", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/properties", first);
        Assert.DoesNotContain("/universities", first);
        Assert.DoesNotContain("SigningKey", first);
        Assert.DoesNotContain("ConnectionString", first);
    }

    [Theory]
    [InlineData("/api/v2/foundation-test")]
    [InlineData("/weatherforecast")]
    [InlineData("/api/v1/auth/register/not-a-route")]
    [InlineData("/api/v1/auth/login/not-a-route")]
    [InlineData("/api/v1/auth/logout/not-a-route")]
    [InlineData("/api/v1/properties")]
    [InlineData("/api/v1/universities")]
    public async Task Unsupported_or_functional_routes_are_not_exposed(string path)
    {
        using var factory = new FoundationFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync(path);
        await ErrorContractTests.AssertProblem(response, 404);
    }

    [Fact]
    public async Task Openapi_is_not_publicly_enabled_in_production()
    {
        using var factory = new FoundationFactory(environment: "Production");
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
