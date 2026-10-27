using System.Diagnostics;

namespace Dhathuru.Identity.Services;

/// <summary>
/// Pads enumeration-sensitive operations to a minimum duration so that "no such account" and "account exists"
/// cannot be told apart by timing. Failures are padded too, because they are the interesting case.
/// </summary>
internal static class ConstantTime
{
    public static async Task<T> RunAsync<T>(int floorMilliseconds, Func<Task<T>> action, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            return await action();
        }
        finally
        {
            var remaining = floorMilliseconds - (int)watch.ElapsedMilliseconds;
            if (remaining > 0) await Task.Delay(remaining, CancellationToken.None);
            ct.ThrowIfCancellationRequested();
        }
    }

    public static Task RunAsync(int floorMilliseconds, Func<Task> action, CancellationToken ct) =>
        RunAsync<bool>(floorMilliseconds, async () =>
        {
            await action();
            return true;
        }, ct);
}
