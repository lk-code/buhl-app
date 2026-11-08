namespace LKCode.AddressManagement.Service;

/// <summary>
/// The options of the server-side output cache, bound to the configuration section <see cref="SectionName"/>.
/// </summary>
public sealed class CachingOptions
{
    /// <summary>
    /// The name of the configuration section that holds the options.
    /// </summary>
    public const string SectionName = "Caching";

    /// <summary>
    /// The longest allowed <see cref="Expiration"/>: one day.
    /// </summary>
    public static readonly TimeSpan MaxExpiration = TimeSpan.FromDays(1);

    /// <summary>
    /// The <see cref="Expiration"/> if the configuration contains none: five minutes.
    /// </summary>
    public static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets how long a cached response is served at most. Every change of an address removes the affected
    /// responses earlier; the expiration only limits how long a response can stay stale if that fails. Must be
    /// greater than zero and at most <see cref="MaxExpiration"/>.
    /// </summary>
    public TimeSpan Expiration { get; set; } = DefaultExpiration;
}
