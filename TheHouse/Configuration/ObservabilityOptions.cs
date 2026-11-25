using System.ComponentModel.DataAnnotations;
using System.Net;

namespace TheHouse.Configuration;

public sealed class ObservabilityOptions
{
    public bool Enabled { get; set; } = true;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string ServiceName { get; set; } =
        "thehouse-backend";

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string ServiceVersion { get; set; } =
        "u2";

    [Range(0.0, 1.0)]
    public double TraceSampleRatio { get; set; } =
        0.10;

    public bool OtlpEnabled { get; set; }

    [StringLength(2048)]
    public string? OtlpEndpoint { get; set; }

    // NV-03 1.3.0-u2: Application Insights con la identidad administrada.
    // La cadena de conexión solo identifica el recurso (sin autenticación
    // local, no sirve para enviar datos sin token de Entra ID).
    public bool AzureMonitorEnabled { get; set; }

    [StringLength(1024)]
    public string? AzureMonitorConnectionString { get; set; }

    // Client id de la identidad asignada por el usuario; vacío = la de
    // ContentSafety:IdentityClientId (misma identidad de la app).
    [StringLength(64)]
    public string? IdentityClientId { get; set; }

    public bool IsValid()
    {
        if (!Enabled)
        {
            return !OtlpEnabled && !AzureMonitorEnabled;
        }

        return IsOtlpValid() && IsAzureMonitorValid();
    }

    private bool IsAzureMonitorValid()
    {
        if (!AzureMonitorEnabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(AzureMonitorConnectionString) ||
            (IdentityClientId is { Length: > 0 } &&
             !Guid.TryParse(IdentityClientId, out _)))
        {
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in AzureMonitorConnectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0 || !values.TryAdd(part[..separator], part[(separator + 1)..]))
            {
                return false;
            }
        }

        return
            values.TryGetValue("InstrumentationKey", out var key) &&
            Guid.TryParse(key, out _) &&
            values.TryGetValue("IngestionEndpoint", out var ingestion) &&
            Uri.TryCreate(ingestion, UriKind.Absolute, out var uri) &&
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrEmpty(uri.UserInfo) &&
            string.IsNullOrEmpty(uri.Query);
    }

    private bool IsOtlpValid()
    {
        if (!OtlpEnabled)
        {
            return string.IsNullOrWhiteSpace(
                OtlpEndpoint);
        }

        if (!Uri.TryCreate(
                OtlpEndpoint,
                UriKind.Absolute,
                out var uri))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(uri.UserInfo) ||
            !string.IsNullOrWhiteSpace(uri.Query) ||
            !string.IsNullOrWhiteSpace(uri.Fragment))
        {
            return false;
        }

        if (uri.Scheme.Equals(
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return
            uri.Scheme.Equals(
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase) &&
            IsLoopback(uri.Host);
    }

    private static bool IsLoopback(string host)
    {
        if (host.Equals(
                "localhost",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return
            IPAddress.TryParse(host, out var ip) &&
            IPAddress.IsLoopback(ip);
    }
}
