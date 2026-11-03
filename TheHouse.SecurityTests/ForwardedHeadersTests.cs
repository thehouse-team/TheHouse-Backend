using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TheHouse.Configuration;

namespace TheHouse.SecurityTests;

public sealed class ForwardedHeadersTests
{
    [Theory]
    [InlineData(false, "10.10.0.10", "10.10.0.10", "http")]
    [InlineData(true, "10.10.0.11", "10.10.0.11", "http")]
    [InlineData(true, "10.10.0.10", "203.0.113.5", "https")]
    public async Task Only_explicitly_trusted_proxy_can_forward(bool enabled, string remote, string expectedIp, string expectedScheme)
    {
        await using var app = Build(enabled);
        var context = Context(remote);
        await Middleware(app).Invoke(context);
        Assert.Equal(expectedIp, context.Connection.RemoteIpAddress?.ToString());
        Assert.Equal(expectedScheme, context.Request.Scheme);
        Assert.Equal("localhost", context.Request.Host.Value);
        Assert.Equal("", context.Request.PathBase.Value);
    }

    [Fact]
    public async Task Extra_untrusted_hops_are_not_consumed()
    {
        await using var app = Build(true);
        var context = Context("10.10.0.10");
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.1, 203.0.113.5";
        context.Request.Headers["X-Forwarded-Proto"] = "http, https";
        await Middleware(app).Invoke(context);
        Assert.Equal("203.0.113.5", context.Connection.RemoteIpAddress?.ToString());
        Assert.Equal("https", context.Request.Scheme);
    }

    [Fact]
    public async Task Asymmetric_headers_are_ignored()
    {
        await using var app = Build(true);
        var context = Context("10.10.0.10");
        context.Request.Headers.Remove("X-Forwarded-Proto");
        await Middleware(app).Invoke(context);
        Assert.Equal("10.10.0.10", context.Connection.RemoteIpAddress?.ToString());
        Assert.Equal("http", context.Request.Scheme);
    }

    [Fact]
    public async Task Explicit_network_is_supported_without_trusting_other_networks()
    {
        await using var app = Build(true, network: true);
        var trusted = Context("10.10.1.15");
        var unknown = Context("10.10.2.15");
        await Middleware(app).Invoke(trusted);
        await Middleware(app).Invoke(unknown);
        Assert.Equal("203.0.113.5", trusted.Connection.RemoteIpAddress?.ToString());
        Assert.Equal("10.10.2.15", unknown.Connection.RemoteIpAddress?.ToString());
    }

    private static WebApplication Build(bool enabled, bool network = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production", Args = [] });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Proxy:Enabled"] = enabled.ToString(),
            [network ? "Proxy:KnownNetworks:0" : "Proxy:KnownProxies:0"] = network ? "10.10.1.0/24" : "10.10.0.10"
        });
        builder.AddFoundationConfiguration();
        Assert.Equal("false", builder.Configuration["forwardedHeaders_enabled"]);
        return builder.Build();
    }

    private static ForwardedHeadersMiddleware Middleware(WebApplication app) => new(
        _ => Task.CompletedTask, NullLoggerFactory.Instance, app.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>());

    private static DefaultHttpContext Context(string remote)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.5";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers["X-Forwarded-Host"] = "attacker.invalid";
        context.Request.Headers["X-Forwarded-Prefix"] = "/untrusted";
        return context;
    }
}
