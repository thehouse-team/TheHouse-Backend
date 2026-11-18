namespace TheHouse.Application.Common;

// NV-10 1.2.0: versiones vigentes de términos y aviso de privacidad y sus
// URLs en la landing page. No son secretos; los textos viven en la landing.
public sealed class LegalOptions
{
    public const string DefaultVersion = "2026-11";

    public string TermsVersion { get; init; } = DefaultVersion;

    public string PrivacyVersion { get; init; } = DefaultVersion;

    // Términos para arrendadores de NV-12 (se aceptan al activar el modo).
    public string LandlordTermsVersion { get; init; } = DefaultVersion;

    public string? TermsUrl { get; init; }

    public string? PrivacyUrl { get; init; }

    public string? AccountDeletionUrl { get; init; }

    public DateTimeOffset UpdatedAt { get; init; } = new(2026, 11, 17, 0, 0, 0, TimeSpan.Zero);

    public bool IsValid() =>
        IsVersion(TermsVersion) && IsVersion(PrivacyVersion) && IsVersion(LandlordTermsVersion) &&
        IsUrl(TermsUrl) && IsUrl(PrivacyUrl) && IsUrl(AccountDeletionUrl);

    private static bool IsVersion(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 20 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.');

    // Opcional, pero si viene debe ser https absoluta.
    private static bool IsUrl(string? value) =>
        value is null ||
        (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && value.Length <= 300);
}
