using System.Globalization;
using System.Text;

namespace TheHouse.Application.Common;

// NV-11: normalización para búsqueda sin acentos ni mayúsculas. La misma
// función genera `searchKey` en el seed y normaliza el texto del usuario.
public static class SearchText
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasSpace = true;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                // Guiones, comas y espacios cuentan como un solo separador.
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().TrimEnd();
    }

    public static string UniversityKey(string code, string name, string city) =>
        Normalize($"{code} {name} {city}");
}
