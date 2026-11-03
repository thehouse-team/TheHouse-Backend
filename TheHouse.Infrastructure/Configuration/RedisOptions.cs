using System.ComponentModel.DataAnnotations;

namespace TheHouse.Infrastructure.Configuration;

public sealed class RedisOptions
{
    public bool Enabled { get; set; }
    public string? ConnectionString { get; set; }

    [Range(100, 30000)]
    public int TimeoutMilliseconds { get; set; } = 5000;

    // Sintaxis de boundary U2; el adaptador de NV-03 hará el parsing del driver.
    public bool IsValid() => !Enabled ||
        (!string.IsNullOrWhiteSpace(ConnectionString) &&
         ConnectionString.Length <= 4096 &&
         !ConnectionString.Contains('\r') && !ConnectionString.Contains('\n') &&
         !ConnectionString.Contains("ssl=false", StringComparison.OrdinalIgnoreCase));
}
