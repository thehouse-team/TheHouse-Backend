using System.ComponentModel.DataAnnotations;

namespace TheHouse.Infrastructure.Configuration;

public sealed class MongoOptions
{
    public bool Enabled { get; set; }
    public string? ConnectionString { get; set; }
    public string? Database { get; set; }

    [Range(1, 30)]
    public int ConnectTimeoutSeconds { get; set; } = 5;

    [Range(1, 1000)]
    public int MaxPoolSize { get; set; } = 100;

    public bool IsValid() => !Enabled ||
        (!string.IsNullOrWhiteSpace(Database) && Database.Length <= 64 &&
         Uri.TryCreate(ConnectionString, UriKind.Absolute, out var uri) &&
         uri.Scheme is "mongodb" or "mongodb+srv" && !string.IsNullOrWhiteSpace(uri.Host) &&
         !uri.Query.Contains("tls=false", StringComparison.OrdinalIgnoreCase) &&
         !uri.Query.Contains("ssl=false", StringComparison.OrdinalIgnoreCase));
}
