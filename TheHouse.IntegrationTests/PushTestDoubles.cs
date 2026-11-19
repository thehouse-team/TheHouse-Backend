using System.Collections.Concurrent;
using TheHouse.Application.Abstractions.Notifications;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.TestDoubles;

// NV-20 1.2.0: `push_devices` en memoria con las mismas reglas que MongoDB.
public sealed class InMemoryPushDeviceRepository : IPushDeviceRepository
{
    private readonly object gate = new();

    public List<PushDevice> Items { get; } = [];

    public bool Unavailable { get; set; }

    public Task<PushDevice?> GetAsync(string userId, string installationId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Items.FirstOrDefault(item => item.UserId == userId && item.InstallationId == installationId));
        }
    }

    public Task<PushDevice?> FindByInstallationAsync(string installationId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Items.FirstOrDefault(item => item.InstallationId == installationId));
        }
    }

    public Task<IReadOnlyList<PushDevice>> ListAsync(string userId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<PushDevice>>(Items.Where(item => item.UserId == userId).ToList());
        }
    }

    public Task SaveAsync(PushDevice device, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            Items.RemoveAll(item => item.FcmToken == device.FcmToken || (item.UserId == device.UserId && item.InstallationId == device.InstallationId));
            Items.Add(device);
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string userId, string installationId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Items.RemoveAll(item => item.UserId == userId && item.InstallationId == installationId) > 0);
        }
    }

    public Task DeleteByTokenAsync(string fcmToken, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            Items.RemoveAll(item => item.FcmToken == fcmToken);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAllAsync(string userId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            Items.RemoveAll(item => item.UserId == userId);
        }

        return Task.CompletedTask;
    }

    private void Guard(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }
    }
}

// Emisor falso: registra cada envío. Los tokens de `Invalid` responden como
// FCM a un token dado de baja; `FailOnce` falla la primera vez.
public sealed class FakePushSender : IPushSender
{
    public ConcurrentQueue<(string Token, PushMessage Message)> Sent { get; } = new();

    public ConcurrentDictionary<string, bool> Invalid { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, int> FailOnce { get; } = new(StringComparer.Ordinal);

    public int Attempts;

    public bool Enabled { get; set; } = true;

    public Task<PushResult> SendAsync(string fcmToken, PushMessage message, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Attempts);
        if (!Enabled)
        {
            return Task.FromResult(PushResult.Disabled);
        }

        if (Invalid.ContainsKey(fcmToken))
        {
            return Task.FromResult(PushResult.InvalidToken);
        }

        if (FailOnce.TryRemove(fcmToken, out _))
        {
            return Task.FromResult(PushResult.Failed);
        }

        Sent.Enqueue((fcmToken, message));
        return Task.FromResult(PushResult.Sent);
    }
}
