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

    public bool IsValid()
    {
        if (!Enabled)
        {
            return !OtlpEnabled;
        }

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
