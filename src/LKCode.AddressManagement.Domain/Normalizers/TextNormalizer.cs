using System.Globalization;
using System.Text;
using LKCode.AddressManagement.Domain.Contracts;

namespace LKCode.AddressManagement.Domain.Normalizers;

/// <summary>
/// Default implementation of <see cref="ITextNormalizer"/>.
/// </summary>
public sealed class TextNormalizer : ITextNormalizer
{
    /// <inheritdoc />
    public string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string collapsed = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        string lowerCase = collapsed.ToLowerInvariant();
        string withoutSpecialCharacters = ReplaceSpecialCharacters(lowerCase);
        string withoutAccents = RemoveAccents(withoutSpecialCharacters);

        return withoutAccents
            .Replace("ae", "a", StringComparison.Ordinal)
            .Replace("oe", "o", StringComparison.Ordinal)
            .Replace("ue", "u", StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public string NormalizePostalCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();
    }

    private static string ReplaceSpecialCharacters(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            if (GetSpecialCharacterReplacement(character) is { } replacement)
            {
                builder.Append(replacement);
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Returns the replacement for lowercase characters that have no Unicode decomposition into a base letter
    /// and diacritics, or <see langword="null"/> if the character needs no replacement.
    /// </summary>
    /// <remarks>
    /// 'İ' (U+0130) is listed because invariant lowercasing keeps it unchanged and its decomposition
    /// would leave an uppercase 'I'.
    /// </remarks>
    private static string? GetSpecialCharacterReplacement(char character) => character switch
    {
        'ß' => "ss",
        'æ' => "ae",
        'œ' => "oe",
        'ø' => "o",
        'ł' => "l",
        'đ' => "d",
        'þ' => "th",
        'ð' => "d",
        'ı' => "i",
        'ħ' => "h",
        'ŧ' => "t",
        'İ' => "i",
        _ => null,
    };

    private static string RemoveAccents(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
