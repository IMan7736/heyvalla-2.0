using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dhathuru.Bookings.Services;

/// <summary>Expires stale holds (releasing provider seats) and finishes anything a crash left half done.</summary>
public sealed class HoldSweeper(IServiceScopeFactory scopes, IOptions<BookingsOptions> options, ILogger<HoldSweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.SweeperEnabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.Value.SweeperInterval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var touched = await scope.ServiceProvider.GetRequiredService<BookingService>().ReconcileAsync(stoppingToken);
                if (touched > 0)
                {
                    logger.LogInformation("Reconciled {Count} bookings.", touched);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Hold sweeper pass failed.");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    internal static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

/// <summary>Polls providers for delays and cancellations on upcoming confirmed legs.</summary>
public sealed class DisruptionMonitor(IServiceScopeFactory scopes, IOptions<BookingsOptions> options, ILogger<DisruptionMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.DisruptionMonitorEnabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.Value.DisruptionPollInterval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<DisruptionService>().PollAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Disruption poll failed.");
            }
        }
        while (await HoldSweeper.WaitAsync(timer, stoppingToken));
    }
}
