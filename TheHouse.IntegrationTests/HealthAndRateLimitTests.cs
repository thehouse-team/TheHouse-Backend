using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TheHouse.Application.Abstractions.Health;

namespace TheHouse.IntegrationTests;

public sealed class HealthAndRateLimitTests
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_is_minimal_healthy_when_external_dependencies_are_disabled(string path)
    {
        using var factory = new FoundationFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"status\":\"Healthy\"}", await response.Content.ReadAsStringAsync());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData("MongoDB")]
    [InlineData("Redis")]
    public async Task Enabled_unconfigured_boundary_is_not_ready_but_remains_live(string dependency)
    {
        var settings = DependencySettings(dependency);
        using var factory = new FoundationFactory(settings);
        using var client = factory.CreateHttpsClient();
        using var ready = await client.GetAsync("/health/ready");
        await ErrorContractTests.AssertProblem(ready, 503);
        using var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.DoesNotContain(dependency, await ready.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Healthy_probe_can_replace_the_boundary_through_DI_only()
    {
        using var factory = new FoundationFactory(DependencySettings("MongoDB"));
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IMongoHealthProbe>(new HealthyMongoProbe()))));
        using var client = configured.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Slow_probe_has_bounded_readiness_and_does_not_affect_liveness()
    {
        var settings = DependencySettings("MongoDB");
        settings["Foundation:HealthTimeoutSeconds"] = "1";
        using var factory = new FoundationFactory(settings);
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IMongoHealthProbe>(new SlowMongoProbe()))));
        using var client = configured.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.Timeout = TimeSpan.FromSeconds(5);
        using var ready = await client.GetAsync("/health/ready");
        await ErrorContractTests.AssertProblem(ready, 503);
        using var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task Local_quota_returns_429_retry_after_and_excludes_health()
    {
        using var factory = new ProbeFactory(new Dictionary<string, string?> { ["Foundation:RateLimitPermitLimit"] = "2" });
        using var client = factory.CreateHttpsClient();
        for (var i = 0; i < 2; i++)
        {
            using var accepted = await client.GetAsync("/api/v1/foundation-test/limited");
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.2");
        using var rejected = await client.GetAsync("/api/v1/foundation-test/limited");
        await ErrorContractTests.AssertProblem(rejected, 429);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        using var live = await client.GetAsync("/health/live");
        using var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    private static Dictionary<string, string?> DependencySettings(string dependency) => dependency == "MongoDB"
        ? new() { ["MongoDB:Enabled"] = "true", ["MongoDB:ConnectionString"] = "mongodb://localhost:27017/?tls=true", ["MongoDB:Database"] = "foundation_fixture" }
        : new() { ["Redis:Enabled"] = "true", ["Redis:ConnectionString"] = "localhost:6379,ssl=true" };

    private sealed class HealthyMongoProbe : IMongoHealthProbe
    {
        public ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);
    }

    private sealed class SlowMongoProbe : IMongoHealthProbe
    {
        public async ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return false;
        }
    }
}
