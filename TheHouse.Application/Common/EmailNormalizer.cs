using System.Text.RegularExpressions;

namespace TheHouse.Application.Common;

public static partial class EmailNormalizer
{
    public static string Normalize(string? email)
    {
        return (email ?? string.Empty).Trim().ToLowerInvariant();
    }

    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
        {
            return false;
        }

        if (email.Any(character => !char.IsAscii(character) || char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            return false;
        }

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex != email.LastIndexOf('@') || atIndex == email.Length - 1)
        {
            return false;
        }

        var local = email[..atIndex];
        var domain = email[(atIndex + 1)..];

        var labels = domain.Split('.');
        return local.Length <= 64 && !local.StartsWith('.') && !local.EndsWith('.') &&
            !local.Contains("..", StringComparison.Ordinal) && SimpleLocalPattern().IsMatch(local) &&
            labels.Length >= 2 && labels.All(label => label.Length is >= 1 and <= 63 &&
                char.IsAsciiLetterOrDigit(label[0]) && char.IsAsciiLetterOrDigit(label[^1]) &&
                label.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'));
    }

    [GeneratedRegex(@"^[a-z0-9!#$%&'*+/=?^_`{|}~.\-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SimpleLocalPattern();
}
