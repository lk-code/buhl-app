using LKCode.AddressManagement.Domain.Contracts;

namespace LKCode.AddressManagement.Domain.Normalizers;

/// <summary>
/// Default implementation of <see cref="ITextNormalizer"/>.
/// </summary>
public sealed class TextNormalizer : ITextNormalizer
{
    /// <inheritdoc />
    public string Normalize(string value) => throw new NotImplementedException();
}
