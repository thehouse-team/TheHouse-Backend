using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TheHouse.Http;

namespace TheHouse.IntegrationTests;

// NV-01-R14 / T19: `X-Content-Type-Options: nosniff` en toda respuesta
// (200, 304, 4xx y health), incluidas las de los manejadores de error.
public sealed class SecurityHeadersTests
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/api/v1/universities")]
    [InlineData("/api/v1/universities/not-an-id")]
    [InlineData("/api/v1/does-not-exist")]
    public async Task Get_responses_include_nosniff(string path)
    {
        using var factory = new UniversityFactory();
        UniversityHttpTests.Seed(factory.Universities);
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(path);

        AssertNoSniff(response);
    }

    [Fact]
    public async Task Not_modified_response_includes_nosniff()
    {
        using var factory = new UniversityFactory();
        UniversityHttpTests.Seed(factory.Universities);
        using var client = factory.CreateHttpsClient();

        using var first = await client.GetAsync("/api/v1/universities");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/universities");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var notModified = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
        AssertNoSniff(notModified);
    }

    [Fact]
    public async Task Error_problem_details_include_nosniff()
    {
        using var factory = new LoginFactory();
        using var client = factory.CreateHttpsClient();

        using var badRequest = await client.PostAsJsonAsync("/api/v1/auth/login", new { unexpected = 1 });
        using var unauthorized = await client.GetAsync("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.BadRequest, badRequest.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        AssertNoSniff(badRequest);
        AssertNoSniff(unauthorized);
    }

    private static void AssertNoSniff(HttpResponseMessage response)
    {
        Assert.True(
            response.Headers.TryGetValues(SecurityHeadersMiddleware.ContentTypeOptions, out var values),
            $"Falta {SecurityHeadersMiddleware.ContentTypeOptions} en {(int)response.StatusCode}.");
        Assert.Equal("nosniff", Assert.Single(values!));
    }
}
