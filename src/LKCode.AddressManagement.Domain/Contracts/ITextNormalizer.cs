namespace LKCode.AddressManagement.Domain.Contracts;

/// <summary>
/// Normalizes texts so that different spellings of the same value can be compared and searched.
/// </summary>
public interface ITextNormalizer
{
    /// <summary>
    /// Normalizes a text such as a city name: collapses whitespace, converts to lowercase, resolves special
    /// characters and accents and replaces the digraphs <c>ae</c>, <c>oe</c> and <c>ue</c>, so that
    /// "Köln", "Koeln" and "KOLN" result in the same value.
    /// </summary>
    /// <param name="value">The text to normalize.</param>
    /// <returns>The normalized text; an empty string if <paramref name="value"/> contains only whitespace.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    string Normalize(string value);
}
