namespace Dhathuru.Integrations.Providers;

/// <summary>Opens after <c>threshold</c> consecutive failures, stays open for <c>openFor</c>, then lets one trial call through.</summary>
public sealed class CircuitBreaker(int threshold, TimeSpan openFor, TimeProvider time)
{
    private readonly object gate = new();
    private int failures;
    private DateTimeOffset? openedAt;
    private bool trialInFlight;

    public bool IsOpen
    {
        get
        {
            lock (gate) return openedAt is not null && time.GetUtcNow() - openedAt < openFor;
        }
    }

    /// <summary>True if a call may go to the provider now (closed, or open long enough for one trial).</summary>
    public bool TryAcquire()
    {
        lock (gate)
        {
            if (openedAt is null) return true;
            if (time.GetUtcNow() - openedAt < openFor) return false;
            if (trialInFlight) return false;
            trialInFlight = true;
            return true;
        }
    }

    public void RecordSuccess()
    {
        lock (gate)
        {
            failures = 0;
            openedAt = null;
            trialInFlight = false;
        }
    }

    public void RecordFailure()
    {
        lock (gate)
        {
            trialInFlight = false;
            failures++;
            if (failures >= threshold || openedAt is not null) openedAt = time.GetUtcNow();
        }
    }
}
