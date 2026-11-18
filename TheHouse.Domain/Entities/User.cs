namespace TheHouse.Domain.Entities;

public sealed class User
{
    public string Id { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string PasswordHash { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    // NV-10 1.2.0: versiones de términos y aviso de privacidad aceptadas.
    public LegalAcceptance? Legal { get; init; }
}

public sealed record LegalAcceptance(string TermsVersion, string PrivacyVersion, DateTimeOffset AcceptedAt);
