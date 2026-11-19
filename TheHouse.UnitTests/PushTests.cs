using TheHouse.Application.Abstractions.Notifications;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Notifications;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;
using TheHouse.TestDoubles;

namespace TheHouse.UnitTests;

// NV-20 1.2.0: dispositivos, despacho y cliente de FCM.
public sealed class PushTests
{
    private const string Alice = "665f1c2a9b1e4a00000a11ce";
    private const string Bob = "665f1c2a9b1e4a0000000b0b";
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 18, 0, 0, TimeSpan.Zero);

    private readonly InMemoryPushDeviceRepository devices = new();
    private readonly ManualTimeProvider clock = new(Now);

    private PushDeviceService Service() => new(devices, clock);

    [Fact]
    public async Task Register_creates_then_updates_and_never_returns_the_token()
    {
        var installation = Guid.NewGuid().ToString();
        var (created, isNew) = await Service().RegisterAsync(Alice, installation, Request(Token("a")), CancellationToken.None);
        Assert.True(isNew);
        clock.Now = Now.AddHours(1);
        var (updated, again) = await Service().RegisterAsync(Alice, installation.ToUpperInvariant(), Request(Token("b"), "1.4.0"), CancellationToken.None);

        Assert.False(again);
        Assert.Equal(created.RegisteredAt, updated.RegisteredAt);
        Assert.Equal(Now.AddHours(1), updated.UpdatedAt);
        Assert.Equal("1.4.0", updated.AppVersion);
        Assert.Equal(Token("b"), Assert.Single(devices.Items).FcmToken);
        Assert.DoesNotContain(nameof(PushDevice.FcmToken), typeof(PushDeviceResponse).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public async Task Invalid_input_is_rejected()
    {
        var service = Service();
        await Assert.ThrowsAsync<NotFoundException>(() => service.RegisterAsync(Alice, "no-es-uuid", Request(Token("a")), CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.RegisterAsync(Alice, Guid.NewGuid().ToString(), Request("corto"), CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.RegisterAsync(Alice, Guid.NewGuid().ToString(), Request(Token("a") + " <script>"), CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.RegisterAsync(Alice, Guid.NewGuid().ToString(), new PushDeviceRequest(Token("a"), "ios", null, null), CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.RegisterAsync(Alice, Guid.NewGuid().ToString(), Request(Token("a"), "1.0 beta"), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(Alice, Guid.NewGuid().ToString(), CancellationToken.None));
        Assert.Empty(devices.Items);
    }

    [Fact]
    public async Task Sixth_device_replaces_the_oldest()
    {
        var first = Guid.NewGuid().ToString();
        await Service().RegisterAsync(Alice, first, Request(Token("0")), CancellationToken.None);
        for (var index = 1; index <= PushRules.MaximumDevices; index++)
        {
            clock.Now = Now.AddMinutes(index);
            await Service().RegisterAsync(Alice, Guid.NewGuid().ToString(), Request(Token(index.ToString())), CancellationToken.None);
        }

        var list = await Service().ListAsync(Alice, CancellationToken.None);
        Assert.Equal(PushRules.MaximumDevices, list.Items.Count);
        Assert.DoesNotContain(list.Items, item => item.InstallationId == first);
    }

    [Fact]
    public async Task Same_phone_moves_between_accounts_but_a_different_token_conflicts()
    {
        var installation = Guid.NewGuid().ToString();
        await Service().RegisterAsync(Alice, installation, Request(Token("phone")), CancellationToken.None);

        // Mismo teléfono (mismo token) con otra cuenta: se mueve.
        await Service().RegisterAsync(Bob, installation, Request(Token("phone")), CancellationToken.None);
        Assert.Equal(Bob, Assert.Single(devices.Items).UserId);

        // Misma instalación con otro token en otra cuenta: conflicto.
        var conflict = await Assert.ThrowsAsync<ConflictException>(() => Service().RegisterAsync(Alice, installation, Request(Token("other")), CancellationToken.None));
        Assert.Equal("installation_conflict", conflict.Reason);

        // El mismo token en otra instalación sale de la cuenta anterior.
        await Service().RegisterAsync(Alice, Guid.NewGuid().ToString(), Request(Token("phone")), CancellationToken.None);
        Assert.Equal(Alice, Assert.Single(devices.Items).UserId);
    }

    [Fact]
    public void Push_uses_the_inbox_text_priority_and_collapse_key()
    {
        var message = PushRules.Message(new Notification
        {
            Id = "n1",
            Type = NotificationTypes.MessageReceived,
            Category = NotificationCategories.Requests,
            Title = "Mensaje nuevo",
            Body = "Ana te escribió sobre Casa Verde 101.",
            TargetKind = NotificationTargets.Conversation,
            TargetId = "c1",
        });
        Assert.True(message.HighPriority);
        Assert.Equal("conversation-c1", message.CollapseKey);
        Assert.Equal("Ana te escribió sobre Casa Verde 101.", message.Body);

        var normal = PushRules.Message(new Notification { Id = "n2", Type = NotificationTypes.RequestReceived, Category = NotificationCategories.Requests, TargetKind = "request", TargetId = "r1" });
        Assert.False(normal.HighPriority);
        Assert.Null(normal.CollapseKey);
    }

    [Fact]
    public async Task Dispatcher_sends_to_every_device_drops_invalid_tokens_and_retries_once()
    {
        var sender = new FakePushSender();
        var waits = 0;
        var dispatcher = new PushDispatcher(devices, sender, (_, _) => { waits++; return Task.CompletedTask; });
        foreach (var name in new[] { "ok", "gone", "flaky" })
        {
            await devices.SaveAsync(new PushDevice { UserId = Bob, InstallationId = Guid.NewGuid().ToString(), FcmToken = Token(name), Platform = "android" }, CancellationToken.None);
        }

        await devices.SaveAsync(new PushDevice { UserId = Alice, InstallationId = Guid.NewGuid().ToString(), FcmToken = Token("alice"), Platform = "android" }, CancellationToken.None);
        sender.Invalid[Token("gone")] = true;
        sender.FailOnce[Token("flaky")] = 1;

        var sent = await dispatcher.DispatchAsync(new PushRequest(Bob, Message()), CancellationToken.None);

        Assert.Equal(2, sent);
        Assert.Equal(1, waits);
        Assert.DoesNotContain(devices.Items, item => item.FcmToken == Token("gone"));
        Assert.DoesNotContain(sender.Sent, item => item.Token == Token("alice"));

        sender.Enabled = false;
        Assert.Equal(0, await new PushDispatcher(devices, sender).DispatchAsync(new PushRequest(Bob, Message()), CancellationToken.None));
    }

    // --- Apoyo -------------------------------------------------------

    private static string Token(string seed) => "fcm-" + seed.PadRight(24, 'x') + ":APA91b";

    private static PushDeviceRequest Request(string token, string? appVersion = null) => new(token, "android", appVersion, "es-MX");

    private static PushMessage Message() =>
        new("Mensaje nuevo", "Ana te escribió sobre Casa Verde.", "n1", NotificationTypes.MessageReceived, "requests", "conversation", "c1", true, "conversation-c1");

}
