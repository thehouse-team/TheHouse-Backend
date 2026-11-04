using System.Text;

namespace TheHouse.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string DefaultIssuer = "thehouse-api";
    public const string DefaultAudience = "thehouse-clients";
    public const int DefaultExpirationSeconds = 900;
    public const int DefaultAccessTokenMinutes = 15;
    public const int DefaultClockSkewSeconds = 30;

    public bool Enabled { get; set; }

    public string Issuer { get; set; } = DefaultIssuer;

    public string Audience { get; set; } = DefaultAudience;

    public string? SigningKey { get; set; }

    // Compatibilidad NV-01.
    public int AccessTokenMinutes { get; set; } =
        DefaultAccessTokenMinutes;

    // Contrato NV-05.
    public int ExpirationSeconds { get; set; } =
        DefaultExpirationSeconds;

    public int ClockSkewSeconds { get; set; } =
        DefaultClockSkewSeconds;

    public bool IsValid()
    {
        // Estas restricciones son válidas incluso con JWT
        // deshabilitado. Mantiene las garantías de Foundation/NV-01.
        if (AccessTokenMinutes <= 0 ||
            AccessTokenMinutes > 60 ||
            ExpirationSeconds <= 0 ||
            ExpirationSeconds > 3600 ||
            ClockSkewSeconds < 0 ||
            ClockSkewSeconds > 60)
        {
            return false;
        }

        if (!Enabled)
        {
            return true;
        }

        return
            !string.IsNullOrWhiteSpace(SigningKey) &&
            Encoding.UTF8.GetByteCount(SigningKey) >= 32 &&
            string.Equals(
                Issuer,
                DefaultIssuer,
                StringComparison.Ordinal) &&
            string.Equals(
                Audience,
                DefaultAudience,
                StringComparison.Ordinal) &&
            AccessTokenMinutes == DefaultAccessTokenMinutes &&
            ExpirationSeconds == DefaultExpirationSeconds &&
            ClockSkewSeconds == DefaultClockSkewSeconds;
    }
}
