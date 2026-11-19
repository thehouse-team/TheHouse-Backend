using System.Globalization;
using System.Text;
using TheHouse.Application.Abstractions.Moderation;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Moderation;
using TheHouse.Domain.Entities;

namespace TheHouse.Application.UseCases;

// NV-21 1.2.0: opciones de la revisión automática (no son secretos).
public sealed class ContentModerationOptions
{
    // Severidad mínima que se rechaza (escala 0, 2, 4, 6).
    public int Threshold { get; init; } = 4;

    // Umbral por categoría de Content Safety (Hate, Sexual, Violence, SelfHarm);
    // la que no aparezca usa `Threshold`.
    public IReadOnlyDictionary<string, int> Thresholds { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    public int ThresholdFor(string category) => Thresholds.TryGetValue(category, out var value) ? value : Threshold;

    // Lista corta de términos prohibidos en español (configuración).
    public IReadOnlyList<string> Blocklist { get; init; } = [];
}

public static class ContentModerationRules
{
    public const string Text = "text";
    public const string Image = "image";
    public const string Blocklisted = "blocklist";

    public static readonly TimeSpan Retention = TimeSpan.FromDays(180);

    // Categorías de Azure AI Content Safety → códigos del contrato.
    public static string Category(string category) => category switch
    {
        "Hate" => "hate",
        "Sexual" => "sexual",
        "Violence" => "violence",
        "SelfHarm" => "self_harm",
        _ => category.ToLowerInvariant(),
    };

    // Minúsculas y sin acentos, para comparar palabras completas.
    public static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
            }
        }

        return builder.ToString();
    }

    public static bool HasBlockedTerm(string text, IReadOnlyCollection<string> normalizedTerms)
    {
        if (normalizedTerms.Count == 0)
        {
            return false;
        }

        // Palabra completa, también en plural (-s, -es): «pendejos», «cabrones».
        // Una frase («te voy a matar») se busca como palabras seguidas.
        var tokens = Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words = tokens.ToHashSet(StringComparer.Ordinal);
        var joined = " " + string.Join(' ', tokens) + " ";
        return normalizedTerms.Any(term => term.Contains(' ', StringComparison.Ordinal)
            ? joined.Contains(" " + term + " ", StringComparison.Ordinal)
            : words.Contains(term) || words.Contains(term + "s") || words.Contains(term + "es"));
    }
}

// Revisa y, si rechaza, registra el rechazo sin el contenido. Falla cerrado:
// si la API de la nube no responde, no se guarda nada (503).
public sealed class ContentModerationService(
    IContentSafetyClient client,
    IModerationRejectionRepository rejections,
    ContentModerationOptions options,
    TimeProvider timeProvider) : IContentModerator
{
    private readonly string[] blocked = [.. options.Blocklist
        .Select(ContentModerationRules.Normalize)
        .Select(term => string.Join(' ', term.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
        .Where(term => term.Length > 0)
        .Distinct(StringComparer.Ordinal)];

    public async Task CheckTextAsync(string userId, string source, IReadOnlyList<ModerationText> texts, CancellationToken cancellationToken)
    {
        foreach (var (field, text) in texts)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (ContentModerationRules.HasBlockedTerm(text, blocked))
            {
                await RejectAsync(userId, ContentModerationRules.Text, field, source, [ContentModerationRules.Blocklisted], 0, cancellationToken);
            }

            if (client.Enabled)
            {
                await JudgeAsync(userId, ContentModerationRules.Text, field, source, await AnalyzeAsync(() => client.AnalyzeTextAsync(text, cancellationToken)), cancellationToken);
            }
        }
    }

    public async Task CheckImageAsync(string userId, string source, string field, ReadOnlyMemory<byte> image, CancellationToken cancellationToken)
    {
        if (client.Enabled)
        {
            await JudgeAsync(userId, ContentModerationRules.Image, field, source, await AnalyzeAsync(() => client.AnalyzeImageAsync(image, cancellationToken)), cancellationToken);
        }
    }

    private static async Task<IReadOnlyList<ContentSafetyScore>> AnalyzeAsync(Func<Task<IReadOnlyList<ContentSafetyScore>>> call)
    {
        try
        {
            return await call();
        }
        catch (DependencyUnavailableException)
        {
            throw new ModerationUnavailableException();
        }
    }

    private Task JudgeAsync(string userId, string kind, string field, string source, IReadOnlyList<ContentSafetyScore> scores, CancellationToken cancellationToken)
    {
        var flagged = scores.Where(score => score.Severity > 0 && score.Severity >= options.ThresholdFor(score.Category)).ToArray();
        return flagged.Length == 0
            ? Task.CompletedTask
            : RejectAsync(
                userId,
                kind,
                field,
                source,
                [.. flagged.Select(score => ContentModerationRules.Category(score.Category)).Distinct(StringComparer.Ordinal)],
                flagged.Max(score => score.Severity),
                cancellationToken);
    }

    private async Task RejectAsync(string userId, string kind, string field, string source, IReadOnlyList<string> categories, int severity, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        try
        {
            await rejections.AddAsync(new ModerationRejection(MessagingIds.New(now), userId, kind, field, source, categories, severity, now), cancellationToken);
        }
        catch (DependencyUnavailableException)
        {
            // El registro es para el administrador: si falla, el rechazo sigue.
        }

        throw new ContentRejectedException(field, categories);
    }
}

// Vista de rechazos para revisor o admin (NV-21). Nunca incluye el contenido.
public sealed class ModerationRejectionService(StaffService staff, IModerationRejectionRepository rejections)
{
    public async Task<ModerationRejectionListResponse> ListAsync(string actorId, string? userId, string? kind, int page, int pageSize, CancellationToken cancellationToken)
    {
        await staff.RequireAsync(actorId, StaffRoles.Reviewer, cancellationToken);
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        if (userId is not null)
        {
            MessagingRules.EnsureId(userId, "userId");
        }

        if (kind is not null and not (ContentModerationRules.Text or ContentModerationRules.Image))
        {
            throw new RequestValidationException("kind", "kind must be text or image.");
        }

        var (items, total) = await rejections.ListAsync(userId?.ToLowerInvariant(), kind, page, pageSize, cancellationToken);
        return new ModerationRejectionListResponse(
            [.. items.Select(item => new ModerationRejectionResponse(item.Id, item.UserId, item.Kind, item.Field, item.Source, item.Categories, item.MaxSeverity, item.CreatedAt))],
            page,
            pageSize,
            total,
            (int)Math.Ceiling(total / (double)pageSize));
    }
}
