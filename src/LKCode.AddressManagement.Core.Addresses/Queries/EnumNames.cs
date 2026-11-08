using System.Collections.Frozen;

namespace LKCode.AddressManagement.Core.Addresses.Queries;

/// <summary>
/// Maps the values of an enum to their names in the API and back.
/// </summary>
/// <typeparam name="TEnum">The type of the enum.</typeparam>
/// <remarks>Names are found ignoring the case; values without a name are treated as undefined.</remarks>
internal sealed class EnumNames<TEnum>
    where TEnum : struct, Enum
{
    private readonly FrozenDictionary<TEnum, string> _namesByValue;
    private readonly FrozenDictionary<string, TEnum> _valuesByName;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnumNames{TEnum}"/> class.
    /// </summary>
    /// <param name="entries">The values with their names, in the order of <see cref="Names"/>.</param>
    public EnumNames(params (TEnum Value, string Name)[] entries)
    {
        Names = [.. entries.Select(entry => entry.Name)];
        _namesByValue = entries.ToFrozenDictionary(entry => entry.Value, entry => entry.Name);
        _valuesByName = entries.ToFrozenDictionary(
            entry => entry.Name,
            entry => entry.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the names of all values.
    /// </summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>
    /// Gets the name of the <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="parameterName">The name of the parameter that passed the value, for the exception.</param>
    /// <returns>The name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The <paramref name="value"/> has no name.</exception>
    public string GetName(TEnum value, string parameterName) =>
        _namesByValue.TryGetValue(value, out string? name)
            ? name
            : throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"The value is no defined {typeof(TEnum).Name}.");

    /// <summary>
    /// Finds the value with the <paramref name="name"/>, ignoring the case.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="value">The value if it was found; otherwise the default value.</param>
    /// <returns><see langword="true"/> if there is a value with the name; otherwise <see langword="false"/>.</returns>
    public bool TryParse(string? name, out TEnum value)
    {
        if (name is null)
        {
            value = default;
            return false;
        }

        return _valuesByName.TryGetValue(name, out value);
    }
}
