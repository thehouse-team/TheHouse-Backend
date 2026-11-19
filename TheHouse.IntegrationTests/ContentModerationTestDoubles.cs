using System.Collections.Concurrent;
using TheHouse.Application.Abstractions.Moderation;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.TestDoubles;

// NV-21 1.2.0: Azure AI Content Safety falso. Un texto con `FlaggedMarker`
// sale con odio de severidad 6; las imágenes se marcan con `FlagImages`.
public sealed class FakeContentSafetyClient : IContentSafetyClient
{
    public const string FlaggedMarker = "contenido-ofensivo-de-prueba";

    public bool Enabled { get; set; } = true;

    public bool Unavailable { get; set; }

    public bool FlagImages { get; set; }

    public int TextCalls;

    public int ImageCalls;

    public ConcurrentQueue<string> Texts { get; } = new();

    public Task<IReadOnlyList<ContentSafetyScore>> AnalyzeTextAsync(string text, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref TextCalls);
        Texts.Enqueue(text);
        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }

        return Task.FromResult(Scores(text.Contains(FlaggedMarker, StringComparison.Ordinal) ? 6 : 0));
    }

    public Task<IReadOnlyList<ContentSafetyScore>> AnalyzeImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ImageCalls);
        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }

        return Task.FromResult(Scores(FlagImages ? 4 : 0, "Sexual"));
    }

    private static IReadOnlyList<ContentSafetyScore> Scores(int severity, string flagged = "Hate") =>
        [.. new[] { "Hate", "Sexual", "Violence", "SelfHarm" }.Select(category => new ContentSafetyScore(category, category == flagged ? severity : 0))];
}

public sealed class InMemoryModerationRejectionRepository : IModerationRejectionRepository
{
    private readonly object gate = new();

    public List<ModerationRejection> Items { get; } = [];

    public bool Unavailable { get; set; }

    public Task AddAsync(ModerationRejection rejection, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }

        lock (gate)
        {
            Items.Add(rejection);
        }

        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<ModerationRejection> Items, long TotalItems)> ListAsync(string? userId, string? kind, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }

        lock (gate)
        {
            var all = Items.Where(item => (userId is null || item.UserId == userId) && (kind is null || item.Kind == kind)).OrderByDescending(item => item.CreatedAt).ToList();
            return Task.FromResult<(IReadOnlyList<ModerationRejection>, long)>((all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), all.Count));
        }
    }
}
