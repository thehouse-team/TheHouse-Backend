using TheHouse.Application.Common;

namespace TheHouse.Application.Abstractions.SmartMatch;

// NV-24 1.2.1: cliente de `TheHouse-GenAI` (`explain-match`). Nunca lanza:
// cualquier falla se devuelve como resultado para seguir con la plantilla.
public interface IGenAiExplanationClient
{
    bool Enabled { get; }

    Task<GenAiExplanationResult> ExplainAsync(GenAiFacts facts, string? requestId, CancellationToken cancellationToken);
}

public enum GenAiOutcome
{
    Disabled,
    Generated,
    NotGenerated,
    CircuitOpen,
    Failed,
}

public sealed record GenAiExplanationResult(GenAiOutcome Outcome, string? Explanation = null, string? PromptVersion = null)
{
    public static GenAiExplanationResult Disabled { get; } = new(GenAiOutcome.Disabled);
}

// Explicación en caché (24 h): sin preferencias, ids de usuario ni texto del arrendador.
public sealed record CachedExplanation(string Summary, IReadOnlyList<string> Pros, IReadOnlyList<string> Cons, string GeneratedBy);

public interface ISmartMatchExplanationCache
{
    Task<CachedExplanation?> GetAsync(string key, DateTimeOffset now, CancellationToken cancellationToken);

    Task SetAsync(string key, CachedExplanation explanation, DateTimeOffset now, CancellationToken cancellationToken);
}

public sealed class DisabledGenAiExplanationClient : IGenAiExplanationClient
{
    public bool Enabled => false;

    public Task<GenAiExplanationResult> ExplainAsync(GenAiFacts facts, string? requestId, CancellationToken cancellationToken) =>
        Task.FromResult(GenAiExplanationResult.Disabled);
}
