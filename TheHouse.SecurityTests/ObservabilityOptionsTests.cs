using TheHouse.Configuration;

namespace TheHouse.SecurityTests;

public sealed class ObservabilityOptionsTests
{
    [Fact]
    public void Default_configuration_is_valid()
    {
        Assert.True(
            new ObservabilityOptions().IsValid());
    }

    [Fact]
    public void Otlp_disabled_rejects_latent_endpoint()
    {
        var options = new ObservabilityOptions
        {
            OtlpEnabled = false,
            OtlpEndpoint =
                "https://otel.example.test"
        };

        Assert.False(options.IsValid());
    }

    [Fact]
    public void Remote_https_otlp_is_valid()
    {
        var options = new ObservabilityOptions
        {
            OtlpEnabled = true,
            OtlpEndpoint =
                "https://otel.example.test"
        };

        Assert.True(options.IsValid());
    }

    [Fact]
    public void Remote_http_otlp_is_rejected()
    {
        var options = new ObservabilityOptions
        {
            OtlpEnabled = true,
            OtlpEndpoint =
                "http://otel.example.test"
        };

        Assert.False(options.IsValid());
    }

    [Fact]
    public void Local_http_otlp_is_valid()
    {
        var options = new ObservabilityOptions
        {
            OtlpEnabled = true,
            OtlpEndpoint =
                "http://127.0.0.1:4317"
        };

        Assert.True(options.IsValid());
    }

    [Theory]
    [InlineData("https://user:secret@otel.example.test")]
    [InlineData("https://otel.example.test?token=secret")]
    [InlineData("https://otel.example.test/#secret")]
    public void Otlp_url_cannot_carry_credentials_or_parameters(
        string endpoint)
    {
        var options = new ObservabilityOptions
        {
            OtlpEnabled = true,
            OtlpEndpoint = endpoint
        };

        Assert.False(options.IsValid());
    }
}
