namespace TheHouse.Domain.Entities;

// NV-20 1.2.0: instalación de la app que recibe push. El token de FCM es un
// dato personal: nunca se devuelve ni se registra en logs.
public sealed record PushDevice
{
    public string UserId { get; init; } = string.Empty;

    // UUID que la app genera una vez por instalación.
    public string InstallationId { get; init; } = string.Empty;

    public string FcmToken { get; init; } = string.Empty;

    public string Platform { get; init; } = string.Empty;

    public string? AppVersion { get; init; }

    public string? Locale { get; init; }

    public DateTimeOffset RegisteredAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
