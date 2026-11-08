namespace LKCode.AddressManagement.Data;

/// <summary>
/// The options of the data access, bound to the configuration section <see cref="SectionName"/>.
/// </summary>
public sealed class DataOptions
{
    /// <summary>
    /// The name of the configuration section that holds the options.
    /// </summary>
    public const string SectionName = "Data";

    /// <summary>
    /// Gets or sets the name of the in-memory database. Must not be empty.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;
}
