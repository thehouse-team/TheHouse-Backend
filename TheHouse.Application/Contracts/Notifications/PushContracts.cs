using System.Text.Json.Serialization;

namespace TheHouse.Application.Contracts.Notifications;

// NV-20 1.2.0: contratos de dispositivos (§6.1). El token nunca sale.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PushDeviceRequest(string? FcmToken, string? Platform, string? AppVersion, string? Locale);

public sealed record PushDeviceResponse(
    string InstallationId,
    string Platform,
    string? AppVersion,
    DateTimeOffset RegisteredAt,
    DateTimeOffset UpdatedAt);

public sealed record PushDeviceListResponse(IReadOnlyList<PushDeviceResponse> Items);
