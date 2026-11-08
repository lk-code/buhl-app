using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace LKCode.AddressManagement.IntegrationTests.Fakes;

/// <summary>
/// Collects the Serilog events of a host. The host picks the sink up from the service collection.
/// </summary>
public sealed class CollectingLogEventSink : ILogEventSink
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(10);

    private readonly ConcurrentQueue<LogEvent> _events = new();

    public void Emit(LogEvent logEvent)
    {
        _events.Enqueue(logEvent);
    }

    /// <summary>
    /// Waits for the first event that matches the <paramref name="predicate"/>. Some events, e.g. the request log,
    /// are written after the client has received the response, so they may arrive later.
    /// </summary>
    /// <returns>The matching event, or <see langword="null"/> if none arrived within five seconds.</returns>
    public async Task<LogEvent?> WaitForAsync(Func<LogEvent, bool> predicate)
    {
        using var timeout = new CancellationTokenSource(_timeout);
        LogEvent? match = _events.FirstOrDefault(predicate);
        while (match is null && !timeout.IsCancellationRequested)
        {
            await Task.Delay(_pollInterval, CancellationToken.None);
            match = _events.FirstOrDefault(predicate);
        }

        return match;
    }
}
