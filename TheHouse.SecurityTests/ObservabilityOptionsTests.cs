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

    // NV-03 1.3.0-u2: Application Insights con identidad administrada.
    private const string ConnectionString =
        "InstrumentationKey=00000000-0000-4000-8000-000000000001;IngestionEndpoint=https://eastus2-3.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus2.livediagnostics.monitor.azure.com/";

    [Fact]
    public void Azure_monitor_is_off_by_default()
    {
        Assert.False(new ObservabilityOptions().AzureMonitorEnabled);
    }

    [Fact]
    public void Azure_monitor_with_a_valid_connection_string_is_valid()
    {
        var options = new ObservabilityOptions
        {
            AzureMonitorEnabled = true,
            AzureMonitorConnectionString = ConnectionString,
            IdentityClientId = "1b0c4e3f-0000-4000-8000-000000000001"
        };

        Assert.True(options.IsValid());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("InstrumentationKey=not-a-guid;IngestionEndpoint=https://eastus2-3.in.applicationinsights.azure.com/")]
    [InlineData("InstrumentationKey=00000000-0000-4000-8000-000000000001")]
    [InlineData("InstrumentationKey=00000000-0000-4000-8000-000000000001;IngestionEndpoint=http://ingest.example.test/")]
    [InlineData("InstrumentationKey=00000000-0000-4000-8000-000000000001;IngestionEndpoint=https://user:secret@ingest.example.test/")]
    [InlineData("InstrumentationKey=00000000-0000-4000-8000-000000000001;IngestionEndpoint=https://ingest.example.test/?code=secret")]
    [InlineData("InstrumentationKey=00000000-0000-4000-8000-000000000001;InstrumentationKey=00000000-0000-4000-8000-000000000002;IngestionEndpoint=https://ingest.example.test/")]
    [InlineData("InstrumentationKey=00000000-0000-4000-8000-000000000001;sin-separador;IngestionEndpoint=https://ingest.example.test/")]
    public void Azure_monitor_rejects_missing_or_unsafe_connection_strings(string? connectionString)
    {
        var options = new ObservabilityOptions
        {
            AzureMonitorEnabled = true,
            AzureMonitorConnectionString = connectionString
        };

        Assert.False(options.IsValid());
    }

    [Fact]
    public void Azure_monitor_rejects_an_invalid_identity_client_id()
    {
        var options = new ObservabilityOptions
        {
            AzureMonitorEnabled = true,
            AzureMonitorConnectionString = ConnectionString,
            IdentityClientId = "not-a-guid"
        };

        Assert.False(options.IsValid());
    }

    [Fact]
    public void Azure_monitor_requires_observability_enabled()
    {
        var options = new ObservabilityOptions
        {
            Enabled = false,
            AzureMonitorEnabled = true,
            AzureMonitorConnectionString = ConnectionString
        };

        Assert.False(options.IsValid());
    }
}
