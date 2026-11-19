using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Moderation;

// NV-21 1.2.0: puerto compartido para revisar texto e imágenes ANTES de
// guardarlos (como INotificationSink: ningún módulo depende de Moderation).
// Lanza ContentRejectedException (422) o ModerationUnavailableException (503).
public interface IContentModerator
{
    Task CheckTextAsync(string userId, string source, IReadOnlyList<ModerationText> texts, CancellationToken cancellationToken);

    Task CheckImageAsync(string userId, string source, string field, ReadOnlyMemory<byte> image, CancellationToken cancellationToken);
}

public sealed record ModerationText(string Field, string? Text);

public sealed class ContentRejectedException(string field, IReadOnlyList<string> categories) : Exception("Content rejected.")
{
    public string Field { get; } = field;

    public IReadOnlyList<string> Categories { get; } = categories;
}

// La revisión no respondió o se agotó el cupo: no se guarda nada (falla cerrado).
public sealed class ModerationUnavailableException() : Exception("Content moderation unavailable.");

// Resultado de la API de la nube: categoría y severidad (0, 2, 4 o 6).
public sealed record ContentSafetyScore(string Category, int Severity);

// Cliente de Azure AI Content Safety. Lanza DependencyUnavailableException si
// no responde o se agota el cupo.
public interface IContentSafetyClient
{
    bool Enabled { get; }

    Task<IReadOnlyList<ContentSafetyScore>> AnalyzeTextAsync(string text, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContentSafetyScore>> AnalyzeImageAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken);
}

// `moderation_rejections`: rechazos sin el contenido.
public interface IModerationRejectionRepository
{
    Task AddAsync(ModerationRejection rejection, CancellationToken cancellationToken);

    Task<(IReadOnlyList<ModerationRejection> Items, long TotalItems)> ListAsync(
        string? userId,
        string? kind,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

public static class ContentModeration
{
    // Llamada segura desde los casos de uso: sin moderador registrado no hace nada.
    public static Task TextAsync(this IContentModerator? moderator, string userId, string source, CancellationToken cancellationToken, params ModerationText[] texts) =>
        moderator is null || texts.All(text => string.IsNullOrWhiteSpace(text.Text))
            ? Task.CompletedTask
            : moderator.CheckTextAsync(userId, source, texts, cancellationToken);

    public static Task ImageAsync(this IContentModerator? moderator, string userId, string source, string field, ReadOnlyMemory<byte> image, CancellationToken cancellationToken) =>
        moderator is null ? Task.CompletedTask : moderator.CheckImageAsync(userId, source, field, image, cancellationToken);
}
