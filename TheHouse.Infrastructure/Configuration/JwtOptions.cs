using System.Text;

namespace TheHouse.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string DefaultIssuer = "thehouse-api";
    public const string DefaultAudience = "thehouse-clients";
    public const int DefaultExpirationSeconds = 900;
    public const int DefaultClockSkewSeconds = 30;

    public bool Enabled { get; set; }

    public string Issuer { get; set; } = DefaultIssuer;

    public string Audience { get; set; } = DefaultAudience;

    public string? SigningKey { get; set; }

    public int ExpirationSeconds { get; set; } =
        DefaultExpirationSeconds;

    public int ClockSkewSeconds { get; set; } =
        DefaultClockSkewSeconds;

    public int AccessTokenMinutes
    {
        get => ExpirationSeconds / 60;
        set => ExpirationSeconds = checked(value * 60);
    }

    public bool IsValid()
    {
        if (!Enabled)
        {
            return true;
        }

        return
            !string.IsNullOrWhiteSpace(SigningKey) &&
            Encoding.UTF8.GetByteCount(SigningKey) >= 32 &&
            string.Equals(Issuer, DefaultIssuer, StringComparison.Ordinal) &&
            string.Equals(Audience, DefaultAudience, StringComparison.Ordinal) &&
            ExpirationSeconds == DefaultExpirationSeconds &&
            ClockSkewSeconds == DefaultClockSkewSeconds;
    }
}
