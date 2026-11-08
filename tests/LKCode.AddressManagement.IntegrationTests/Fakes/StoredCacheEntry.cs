namespace LKCode.AddressManagement.IntegrationTests.Fakes;

/// <summary>
/// An entry that was stored in the output cache.
/// </summary>
/// <param name="Key">The cache key of the response.</param>
/// <param name="Tags">The tags of the response.</param>
/// <param name="ValidFor">How long the response is valid.</param>
public sealed record StoredCacheEntry(string Key, IReadOnlyList<string> Tags, TimeSpan ValidFor);
