using System.ComponentModel.DataAnnotations;
using System.Text;

namespace TheHouse.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public bool Enabled { get; set; }
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public string? SigningKey { get; set; }

    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(0, 60)]
    public int ClockSkewSeconds { get; set; } = 30;

    public bool IsValid() => !Enabled ||
        (!string.IsNullOrWhiteSpace(Issuer) && Issuer.Length <= 256 &&
         !string.IsNullOrWhiteSpace(Audience) && Audience.Length <= 256 &&
         !string.IsNullOrWhiteSpace(SigningKey) && Encoding.UTF8.GetByteCount(SigningKey) >= 32);
}
