using System.Net;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TheHouse.Configuration;
using TheHouse.Infrastructure.Moderation;
using TheHouse.TestDoubles;

namespace TheHouse.IntegrationTests;

// NV-03 1.3.0-u2 (T11): exportación a Application Insights con la identidad
// administrada de la app, sin llaves. Ninguna prueba envía datos a Azure.
public sealed class AzureMonitorTelemetryTests
{
    private const string ConnectionString =
        "InstrumentationKey=00000000-0000-4000-8000-000000000001;IngestionEndpoint=https://ingest.example.test/";

    [Fact]
    public async Task Identity_token_is_requested_for_azure_monitor()
    {
        var handler = new IdentityServer();
        using var http = new HttpClient(handler);
        using var identity = new ContainerAppsIdentityTokenSource(
            http, "http://localhost:42356/msi/token", "test-header", "1b0c4e3f-0000-4000-8000-000000000001",
            new ManualTimeProvider(DateTimeOffset.UtcNow), ContainerAppsIdentityTokenSource.MonitorResource);
        var credential = new ManagedIdentityTokenCredential(identity, TimeProvider.System);

        var token = await credential.GetTokenAsync(new TokenRequestContext(["https://monitor.azure.com//.default"]), CancellationToken.None);
        var again = credential.GetToken(new TokenRequestContext(["https://monitor.azure.com//.default"]), CancellationToken.None);

        Assert.Equal("test-access", token.Token);
        Assert.Equal("test-access", again.Token);
        Assert.True(token.ExpiresOn > DateTimeOffset.UtcNow);
        Assert.Equal(1, handler.TokenRequests);
        Assert.Contains("resource=https%3A%2F%2Fmonitor.azure.com", handler.LastTokenUrl, StringComparison.Ordinal);
        Assert.Contains("client_id=1b0c4e3f-0000-4000-8000-000000000001", handler.LastTokenUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_identity_fails_without_exposing_details()
    {
        using var http = new HttpClient(new IdentityServer());
        using var none = new ContainerAppsIdentityTokenSource(http, null, null, null, TimeProvider.System, ContainerAppsIdentityTokenSource.MonitorResource);
        var credential = new ManagedIdentityTokenCredential(none, TimeProvider.System);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => credential.GetTokenAsync(new TokenRequestContext(["https://monitor.azure.com//.default"]), CancellationToken.None).AsTask());

        Assert.DoesNotContain("test-access", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Api_starts_and_serves_requests_with_azure_monitor_enabled()
    {
        using var factory = new FoundationFactory(new Dictionary<string, string?>
        {
            ["Observability:AzureMonitorEnabled"] = "true",
            ["Observability:AzureMonitorConnectionString"] = ConnectionString,
        });
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(factory.Services.GetService<TracerProvider>());
        Assert.NotNull(factory.Services.GetService<MeterProvider>());
    }

    [Fact]
    public void Azure_monitor_without_connection_string_fails_before_serving_requests()
    {
        using var factory = new FoundationFactory(new Dictionary<string, string?>
        {
            ["Observability:AzureMonitorEnabled"] = "true",
        });

        Assert.Throws<OptionsValidationException>(() => factory.CreateHttpsClient());
    }

    [Fact]
    public void Only_the_allowed_metrics_are_exported()
    {
        Assert.Equal(6, ObservabilityConfiguration.ExportedMetrics.Count);
        Assert.Contains("http.server.request.duration", ObservabilityConfiguration.ExportedMetrics);
        Assert.DoesNotContain("kestrel.active_connections", ObservabilityConfiguration.ExportedMetrics);
        Assert.DoesNotContain("http.client.request.duration", ObservabilityConfiguration.ExportedMetrics);
    }

    private sealed class IdentityServer : HttpMessageHandler
    {
        public int TokenRequests;

        public string LastTokenUrl = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            TokenRequests++;
            LastTokenUrl = request.RequestUri!.ToString();
            Assert.Equal("test-header", request.Headers.GetValues("X-IDENTITY-HEADER").Single());
            var expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"test-access\",\"expires_on\":\"" + expires + "\"}", Encoding.UTF8, "application/json"),
            });
        }
    }
}
