using System.Collections.Concurrent;
using Microsoft.AspNetCore.OutputCaching;

namespace LKCode.AddressManagement.IntegrationTests.Fakes;

/// <summary>
/// Passes every call to the <paramref name="inner"/> store of the host and records which responses are stored, with
/// their tags and expiration.
/// </summary>
public sealed class RecordingOutputCacheStore(IOutputCacheStore inner) : IOutputCacheStore
{
    private readonly ConcurrentQueue<StoredCacheEntry> _storedEntries = new();

    /// <summary>
    /// Gets all entries stored so far, in their order.
    /// </summary>
    public IReadOnlyList<StoredCacheEntry> StoredEntries => [.. _storedEntries];

    public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken) =>
        inner.EvictByTagAsync(tag, cancellationToken);

    public ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken) =>
        inner.GetAsync(key, cancellationToken);

    public ValueTask SetAsync(
        string key,
        byte[] value,
        string[]? tags,
        TimeSpan validFor,
        CancellationToken cancellationToken)
    {
        _storedEntries.Enqueue(new StoredCacheEntry(key, tags ?? [], validFor));

        return inner.SetAsync(key, value, tags, validFor, cancellationToken);
    }
}
