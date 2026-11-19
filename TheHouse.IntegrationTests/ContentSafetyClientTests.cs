using System.Net;
using System.Text;
using System.Text.Json;
using TheHouse.Application.Common;
using TheHouse.Configuration;
using TheHouse.Infrastructure.Moderation;
using TheHouse.TestDoubles;

namespace TheHouse.IntegrationTests;

// NV-21 1.2.0: cliente de Azure AI Content Safety contra un servidor falso y
// token de la identidad administrada de Container Apps (sin llaves).
public sealed class ContentSafetyClientTests
{
    private static readonly Uri Endpoint = new("https://cs-thehouse-test.cognitiveservices.azure.com/");

    [Fact]
    public async Task Client_sends_text_and_images_with_the_managed_identity_token()
    {
        var handler = new FakeServer();
        using var http = new HttpClient(handler);
        using var identity = new ContainerAppsIdentityTokenSource(http, "http://localhost:42356/msi/token", "test-header", "1b0c4e3f-0000-4000-8000-000000000001", new ManualTimeProvider(DateTimeOffset.UtcNow));
        var client = new AzureContentSafetyClient(http, Endpoint, identity);

        var text = await client.AnalyzeTextAsync("hola", CancellationToken.None);
        Assert.Equal(6, text.Single(score => score.Category == "Hate").Severity);
        await client.AnalyzeImageAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);

        Assert.Equal(1, handler.TokenRequests);
        Assert.Contains("client_id=1b0c4e3f-0000-4000-8000-000000000001", handler.LastTokenUrl, StringComparison.Ordinal);
        Assert.Contains("resource=https%3A%2F%2Fcognitiveservices.azure.com", handler.LastTokenUrl, StringComparison.Ordinal);
        Assert.Equal("https://cs-thehouse-test.cognitiveservices.azure.com/contentsafety/image:analyze?api-version=2023-10-01", handler.LastUrl);
        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), body.RootElement.GetProperty("image").GetProperty("content").GetString());
    }

    [Fact]
    public async Task Quota_errors_and_missing_identity_fail_closed_and_server_errors_retry_once()
    {
        var handler = new FakeServer();
        using var http = new HttpClient(handler);
        using var identity = new ContainerAppsIdentityTokenSource(http, "http://localhost:42356/msi/token", "test-header", null, new ManualTimeProvider(DateTimeOffset.UtcNow));
        var client = new AzureContentSafetyClient(http, Endpoint, identity);

        handler.Next.Enqueue(HttpStatusCode.TooManyRequests);
        await Assert.ThrowsAsync<DependencyUnavailableException>(() => client.AnalyzeTextAsync("hola", CancellationToken.None));

        handler.Next.Enqueue(HttpStatusCode.InternalServerError);
        Assert.NotEmpty(await client.AnalyzeTextAsync("hola", CancellationToken.None));

        handler.Next.Enqueue(HttpStatusCode.InternalServerError);
        handler.Next.Enqueue(HttpStatusCode.InternalServerError);
        await Assert.ThrowsAsync<DependencyUnavailableException>(() => client.AnalyzeTextAsync("hola", CancellationToken.None));

        using var none = new ContainerAppsIdentityTokenSource(http, null, null, null, TimeProvider.System);
        await Assert.ThrowsAsync<DependencyUnavailableException>(() => new AzureContentSafetyClient(http, Endpoint, none).AnalyzeTextAsync("hola", CancellationToken.None));
    }

    [Theory]
    [InlineData("https://cs-thehouse-u2.cognitiveservices.azure.com/", true)]
    [InlineData("http://cs-thehouse-u2.cognitiveservices.azure.com/", false)]
    [InlineData("https://evil.example/cognitiveservices.azure.com", false)]
    [InlineData("https://169.254.169.254/", false)]
    public void Only_azure_ai_endpoints_are_accepted(string endpoint, bool valid) =>
        Assert.Equal(valid, ContentModerationConfiguration.IsContentSafetyEndpoint(endpoint));

    private sealed class FakeServer : HttpMessageHandler
    {
        public int TokenRequests;

        public string LastTokenUrl = string.Empty;

        public string? LastUrl;

        public string? LastBody;

        public Queue<HttpStatusCode> Next { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            if (url.StartsWith("http://localhost:42356/msi/token", StringComparison.Ordinal))
            {
                TokenRequests++;
                LastTokenUrl = url;
                Assert.Equal("test-header", request.Headers.GetValues("X-IDENTITY-HEADER").Single());
                var expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
                return Json(HttpStatusCode.OK, "{\"access_token\":\"test-access\",\"expires_on\":\"" + expires + "\"}");
            }

            Assert.Equal("Bearer test-access", request.Headers.Authorization!.ToString());
            LastUrl = url;
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var status = Next.Count > 0 ? Next.Dequeue() : HttpStatusCode.OK;
            return status == HttpStatusCode.OK
                ? Json(status, "{\"categoriesAnalysis\":[{\"category\":\"Hate\",\"severity\":6},{\"category\":\"Sexual\",\"severity\":0}]}")
                : Json(status, "{}");
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
