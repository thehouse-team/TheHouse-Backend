using TheHouse.Application.Abstractions.Moderation;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.TestDoubles;

namespace TheHouse.UnitTests;

// NV-21 1.2.0: umbral, lista de términos, registro sin contenido y falla cerrada.
public sealed class ContentModerationTests
{
    private const string User = "665f1c2a9b1e4a00000a11ce";
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 18, 0, 0, TimeSpan.Zero);

    private readonly InMemoryModerationRejectionRepository rejections = new();

    private ContentModerationService Service(IContentSafetyClient client, params string[] blocklist) =>
        new(client, rejections, new ContentModerationOptions { Blocklist = blocklist }, new ManualTimeProvider(Now));

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(4, true)]
    [InlineData(6, true)]
    public async Task Severity_four_or_more_is_rejected(int severity, bool rejected)
    {
        var service = Service(new FixedClient(new ContentSafetyScore("Violence", severity)));
        var check = service.CheckTextAsync(User, "chat.text", [new ModerationText("text", "texto")], CancellationToken.None);

        if (rejected)
        {
            var error = await Assert.ThrowsAsync<ContentRejectedException>(() => check);
            Assert.Equal("text", error.Field);
            Assert.Equal(["violence"], error.Categories);
            var logged = Assert.Single(rejections.Items);
            Assert.Equal((User, "text", "chat.text", severity), (logged.UserId, logged.Kind, logged.Source, logged.MaxSeverity));
        }
        else
        {
            await check;
            Assert.Empty(rejections.Items);
        }
    }

    [Fact]
    public async Task Category_thresholds_override_the_default()
    {
        var service = new ContentModerationService(
            new FixedClient(new ContentSafetyScore("Violence", 2), new ContentSafetyScore("Sexual", 2)),
            rejections,
            new ContentModerationOptions { Thresholds = new Dictionary<string, int> { ["Violence"] = 2 } },
            new ManualTimeProvider(Now));

        var error = await Assert.ThrowsAsync<ContentRejectedException>(() => service.CheckTextAsync(User, "profile.name", [new ModerationText("name", "x")], CancellationToken.None));
        Assert.Equal(["violence"], error.Categories);
    }

    [Fact]
    public async Task Blocklist_matches_whole_words_without_accents_or_case()
    {
        var client = new FixedClient();
        var service = Service(client, "pendejo", "Cabrón");

        await Assert.ThrowsAsync<ContentRejectedException>(() => service.CheckTextAsync(User, "profile.name", [new ModerationText("name", "Eres un PENDÉJO!")], CancellationToken.None));
        await Assert.ThrowsAsync<ContentRejectedException>(() => service.CheckTextAsync(User, "profile.name", [new ModerationText("name", "qué cabron")], CancellationToken.None));
        await Assert.ThrowsAsync<ContentRejectedException>(() => service.CheckTextAsync(User, "building.name", [new ModerationText("name", "Edificio de pendejos")], CancellationToken.None));
        await Assert.ThrowsAsync<ContentRejectedException>(() => service.CheckTextAsync(User, "building.name", [new ModerationText("name", "puros cabrones")], CancellationToken.None));
        await service.CheckTextAsync(User, "profile.name", [new ModerationText("name", "Calle Pendejosa 12")], CancellationToken.None);
        Assert.Equal(["blocklist"], rejections.Items[0].Categories);
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task Unavailable_cloud_fails_closed_and_a_failed_log_still_rejects()
    {
        var down = Service(new FixedClient { Down = true });
        await Assert.ThrowsAsync<ModerationUnavailableException>(() => down.CheckTextAsync(User, "chat.text", [new ModerationText("text", "hola")], CancellationToken.None));
        await Assert.ThrowsAsync<ModerationUnavailableException>(() => down.CheckImageAsync(User, "room.photo", "file", new byte[] { 1, 2 }, CancellationToken.None));

        rejections.Unavailable = true;
        var strict = Service(new FixedClient(new ContentSafetyScore("Hate", 6)));
        await Assert.ThrowsAsync<ContentRejectedException>(() => strict.CheckTextAsync(User, "chat.text", [new ModerationText("text", "x")], CancellationToken.None));
    }

    [Fact]
    public async Task Disabled_cloud_only_applies_the_blocklist_and_empty_texts_are_skipped()
    {
        var client = new FixedClient(new ContentSafetyScore("Hate", 6)) { IsEnabled = false };
        var service = Service(client, "pendejo");
        await service.CheckTextAsync(User, "chat.text", [new ModerationText("text", "texto cualquiera")], CancellationToken.None);
        await service.CheckImageAsync(User, "room.photo", "file", new byte[] { 1 }, CancellationToken.None);
        await ((IContentModerator?)null).TextAsync(User, "chat.text", CancellationToken.None, new ModerationText("text", "x"));
        await service.TextAsync(User, "chat.text", CancellationToken.None, new ModerationText("text", "   "), new ModerationText("note", null));
        Assert.Equal(0, client.Calls);
        Assert.Equal("self_harm", ContentModerationRules.Category("SelfHarm"));
    }

    private sealed class FixedClient(params ContentSafetyScore[] scores) : IContentSafetyClient
    {
        public int Calls;

        public bool Down { get; init; }

        public bool IsEnabled { get; init; } = true;

        public bool Enabled => IsEnabled;

        public Task<IReadOnlyList<ContentSafetyScore>> AnalyzeTextAsync(string text, CancellationToken cancellationToken) => Result();

        public Task<IReadOnlyList<ContentSafetyScore>> AnalyzeImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken) => Result();

        private Task<IReadOnlyList<ContentSafetyScore>> Result()
        {
            Calls++;
            return Down ? throw new DependencyUnavailableException() : Task.FromResult<IReadOnlyList<ContentSafetyScore>>(scores);
        }
    }
}
