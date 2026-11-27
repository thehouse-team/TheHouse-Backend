using TheHouse.Application.Abstractions.Notifications;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Notifications;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-20 1.2.0: reglas del push con FCM.
public static class PushRules
{
    public const int MaximumDevices = 5;

    public const string Android = "android";

    // Avisos que el usuario debe ver de inmediato (prioridad alta en Android).
    public static readonly IReadOnlySet<string> HighPriority = new HashSet<string>(StringComparer.Ordinal)
    {
        NotificationTypes.MessageReceived,
        NotificationTypes.VisitConfirmed,
        NotificationTypes.VisitCancelled,
        NotificationTypes.RequestAccepted,
        NotificationTypes.AgreementProposed,
    };

    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public static string InstallationId(string? value) =>
        Guid.TryParseExact(value, "D", out var id) ? id.ToString("D") : throw new NotFoundException("device");

    // Tokens de FCM: base64url con `:`; de 20 a 4096 caracteres.
    public static string Token(string? value) =>
        value is { Length: >= 20 and <= 4096 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is ':' or '_' or '-')
            ? value
            : throw new RequestValidationException("fcmToken", "fcmToken is invalid.");

    public static string? Optional(string? value, string field, int maximum) =>
        value is null
            ? null
            : value.Length <= maximum && value.Length > 0 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or '+')
                ? value
                : throw new RequestValidationException(field, $"{field} is invalid.");

    // El mismo texto corto de la bandeja: nunca el contenido de un mensaje.
    public static PushMessage Message(Notification notification) =>
        new(
            notification.Title,
            notification.Body,
            notification.Id,
            notification.Type,
            notification.Category,
            notification.TargetKind,
            notification.TargetId,
            HighPriority.Contains(notification.Type),
            notification.Type == NotificationTypes.MessageReceived ? "conversation-" + notification.TargetId : null);
}

// Registro, lista y borrado de dispositivos. El usuario sale del token.
public sealed class PushDeviceService(IPushDeviceRepository devices, TimeProvider timeProvider)
{
    public async Task<(PushDeviceResponse Device, bool Created)> RegisterAsync(
        string userId,
        string? installationId,
        PushDeviceRequest? request,
        CancellationToken cancellationToken)
    {
        var installation = PushRules.InstallationId(installationId);
        var token = PushRules.Token(request?.FcmToken);
        if (request?.Platform != PushRules.Android)
        {
            throw new RequestValidationException("platform", "platform must be android.");
        }

        var appVersion = PushRules.Optional(request.AppVersion, "appVersion", 32);
        var locale = PushRules.Optional(request.Locale, "locale", 16);

        // Otra cuenta con la misma instalación: si trae el mismo token es el
        // mismo teléfono (se mueve); si no, es un conflicto.
        if (await devices.FindByInstallationAsync(installation, cancellationToken) is { } other && other.UserId != userId)
        {
            if (other.FcmToken != token)
            {
                throw new ConflictException("installation_conflict");
            }

            await devices.DeleteAsync(other.UserId, installation, cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        var existing = await devices.GetAsync(userId, installation, cancellationToken);
        var device = new PushDevice
        {
            UserId = userId,
            InstallationId = installation,
            FcmToken = token,
            Platform = PushRules.Android,
            AppVersion = appVersion,
            Locale = locale,
            RegisteredAt = existing?.RegisteredAt ?? now,
            UpdatedAt = now,
        };
        await devices.SaveAsync(device, cancellationToken);

        // Máximo 5 por usuario: el sexto reemplaza al más antiguo.
        var all = await devices.ListAsync(userId, cancellationToken);
        foreach (var old in all.OrderByDescending(item => item.UpdatedAt).Skip(PushRules.MaximumDevices))
        {
            await devices.DeleteAsync(userId, old.InstallationId, cancellationToken);
        }

        return (ToResponse(device), existing is null);
    }

    public async Task DeleteAsync(string userId, string? installationId, CancellationToken cancellationToken)
    {
        var installation = PushRules.InstallationId(installationId);
        if (!await devices.DeleteAsync(userId, installation, cancellationToken))
        {
            throw new NotFoundException("device");
        }
    }

    public async Task<PushDeviceListResponse> ListAsync(string userId, CancellationToken cancellationToken) =>
        new([.. (await devices.ListAsync(userId, cancellationToken)).OrderByDescending(item => item.UpdatedAt).Select(ToResponse)]);

    private static PushDeviceResponse ToResponse(PushDevice device) =>
        new(device.InstallationId, device.Platform, device.AppVersion, device.RegisteredAt, device.UpdatedAt);
}

// Envía un push a todos los dispositivos del destinatario. Token inválido →
// se borra; otra falla → un reintento. Nunca lanza hacia la acción original.
public sealed class PushDispatcher(IPushDeviceRepository devices, IPushSender sender, Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    private readonly Func<TimeSpan, CancellationToken, Task> wait = delay ?? Task.Delay;

    public async Task<int> DispatchAsync(PushRequest request, CancellationToken cancellationToken)
    {
        if (!sender.Enabled)
        {
            return 0;
        }

        var sent = 0;
        foreach (var device in await devices.ListAsync(request.UserId, cancellationToken))
        {
            var result = await sender.SendAsync(device.FcmToken, request.Message, cancellationToken);
            if (result == PushResult.Failed)
            {
                await wait(PushRules.RetryDelay, cancellationToken);
                result = await sender.SendAsync(device.FcmToken, request.Message, cancellationToken);
            }

            if (result == PushResult.InvalidToken)
            {
                await devices.DeleteByTokenAsync(device.FcmToken, cancellationToken);
            }
            else if (result == PushResult.Sent)
            {
                sent++;
            }
        }

        return sent;
    }
}
