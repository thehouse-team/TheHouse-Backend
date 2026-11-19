using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Notifications;

// NV-20 1.2.0: dispositivos que reciben push (`push_devices`).
public interface IPushDeviceRepository
{
    Task<PushDevice?> GetAsync(string userId, string installationId, CancellationToken cancellationToken);

    // Instalación registrada por otra cuenta (para detectar conflictos).
    Task<PushDevice?> FindByInstallationAsync(string installationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PushDevice>> ListAsync(string userId, CancellationToken cancellationToken);

    // Crea o reemplaza el dispositivo {userId, installationId}. Antes borra
    // el mismo token en cualquier otra instalación (un token, una cuenta).
    Task SaveAsync(PushDevice device, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string userId, string installationId, CancellationToken cancellationToken);

    Task DeleteByTokenAsync(string fcmToken, CancellationToken cancellationToken);

    Task DeleteAllAsync(string userId, CancellationToken cancellationToken);
}

// Mensaje push ya armado (textos de la bandeja, sin contenido privado).
public sealed record PushMessage(
    string Title,
    string Body,
    string NotificationId,
    string Type,
    string Category,
    string TargetKind,
    string TargetId,
    bool HighPriority,
    string? CollapseKey);

public enum PushResult
{
    Sent,
    InvalidToken,
    Failed,
    Disabled,
}

public interface IPushSender
{
    bool Enabled { get; }

    Task<PushResult> SendAsync(string fcmToken, PushMessage message, CancellationToken cancellationToken);
}

// Envío fuera de la respuesta HTTP; si la cola está llena se descarta (la
// bandeja conserva la notificación).
public interface IPushQueue
{
    bool TryEnqueue(PushRequest request);
}

public sealed record PushRequest(string UserId, PushMessage Message);
