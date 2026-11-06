using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TheHouse.Application.Abstractions.Health;
using TheHouse.Configuration;
using TheHouse.Infrastructure.Configuration;

namespace TheHouse.IntegrationTests;

public sealed class ConfigurationTests
{
    [Fact]
    public void Default_configuration_builds_without_connections_or_tokens()
    {
        using var factory = new FoundationFactory();
        using var client = factory.CreateHttpsClient();
        Assert.False(factory.Services.GetRequiredService<IOptions<MongoOptions>>().Value.Enabled);
        Assert.False(factory.Services.GetRequiredService<IOptions<RedisOptions>>().Value.Enabled);
        Assert.False(factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value.Enabled);
        Assert.Same(factory.Services.GetRequiredService<IMongoHealthProbe>(), factory.Services.GetRequiredService<IMongoHealthProbe>());
        Assert.Same(factory.Services.GetRequiredService<IRedisHealthProbe>(), factory.Services.GetRequiredService<IRedisHealthProbe>());
    }

    [Theory]
    [InlineData("Foundation:MaxRequestBodyBytes", "0")]
    [InlineData("Foundation:HealthTimeoutSeconds", "0")]
    [InlineData("Foundation:RequestTimeoutSeconds", "0")]
    [InlineData("Foundation:RateLimitPermitLimit", "0")]
    [InlineData("Foundation:CompressionMinimumBytes", "0")]
    [InlineData("Foundation:PublicCacheMaxAgeSeconds", "-1")]
    [InlineData("Foundation:PublicCacheMaxAgeSeconds", "301")]
    [InlineData("MongoDB:MaxPoolSize", "0")]
    [InlineData("MongoDB:ConnectTimeoutSeconds", "0")]
    [InlineData("MongoDB:Enabled", "true")]
    [InlineData("Redis:Enabled", "true")]
    [InlineData("Redis:TimeoutMilliseconds", "0")]
    [InlineData("Jwt:Enabled", "true")]
    [InlineData("Jwt:AccessTokenMinutes", "0")]
    [InlineData("Jwt:ClockSkewSeconds", "61")]
    [InlineData("Proxy:Enabled", "true")]
    [InlineData("Proxy:ForwardLimit", "0")]
    [InlineData("Proxy:KnownNetworks:0", "0.0.0.0/0")]
    [InlineData("Proxy:KnownProxies:0", "0.0.0.0")]
    public void Invalid_configuration_fails_before_serving_requests(string key, string value)
    {
        using var factory = new FoundationFactory(new Dictionary<string, string?> { [key] = value });
        Assert.Throws<OptionsValidationException>(() => factory.CreateHttpsClient());
    }

    [Theory]
    [InlineData("Foundation:RateLimitPermitLimit")]
    [InlineData("MongoDB:MaxPoolSize")]
    [InlineData("Redis:Enabled")]
    [InlineData("Jwt:ClockSkewSeconds")]
    [InlineData("Proxy:ForwardLimit")]
    public void Binding_errors_do_not_echo_raw_values(string key)
    {
        const string marker = "invalid-input-marker-not-a-credential";
        using var factory = new FoundationFactory(new Dictionary<string, string?> { [key] = marker });
        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateHttpsClient());
        Assert.DoesNotContain(marker, error.ToString());
        Assert.Null(error.InnerException);
    }

    [Fact]
    public void Unknown_option_is_rejected_without_echoing_it()
    {
        using var factory = new FoundationFactory(new Dictionary<string, string?> { ["Jwt:UnrecognizedOption"] = "unused-marker" });
        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateHttpsClient());
        Assert.DoesNotContain("unused-marker", error.ToString());
        Assert.DoesNotContain("UnrecognizedOption", error.ToString());
    }
}
