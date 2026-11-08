namespace LKCode.AddressManagement.IntegrationTests.Fakes;

/// <summary>
/// Lets a fixed number of concurrent callers wait for each other, so that all of them continue only when the last
/// one has arrived.
/// </summary>
/// <param name="participants">The number of callers to wait for.</param>
public sealed class Rendezvous(int participants)
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrived;

    /// <summary>
    /// Gets the number of callers that have arrived.
    /// </summary>
    public int Arrived => Volatile.Read(ref _arrived);

    /// <summary>
    /// Arrives and waits until all participants have arrived.
    /// </summary>
    /// <exception cref="TimeoutException">Not all participants arrived in time.</exception>
    public async Task ArriveAndWaitAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _arrived) == participants)
        {
            _allArrived.TrySetResult();
        }

        await _allArrived.Task.WaitAsync(_timeout, cancellationToken);
    }
}
