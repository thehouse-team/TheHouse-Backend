using System.ComponentModel.DataAnnotations;

namespace TheHouse.Configuration;

public sealed class FoundationOptions
{
    [Range(1024, 1048576)]
    public int MaxRequestBodyBytes { get; set; } = 65536;

    [Range(1, 120)]
    public int RequestTimeoutSeconds { get; set; } = 15;

    [Range(1, 10)]
    public int HealthTimeoutSeconds { get; set; } = 2;

    [Range(256, 65536)]
    public int CompressionMinimumBytes { get; set; } = 1024;

    [Range(1, 10000)]
    public int RateLimitPermitLimit { get; set; } = 60;

    [Range(1, 3600)]
    public int RateLimitWindowSeconds { get; set; } = 60;

    // max-age de las lecturas públicas cacheables (NV-07-R08, NV-08-R08).
    [Range(0, 300)]
    public int PublicCacheMaxAgeSeconds { get; set; } = 60;
}
