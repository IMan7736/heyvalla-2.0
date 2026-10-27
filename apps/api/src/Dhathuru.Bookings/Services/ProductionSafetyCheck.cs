using Dhathuru.Bookings.Payments;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dhathuru.Bookings.Services;

/// <summary>
/// Refuses to start in Production while payments could be faked: the in-house <see cref="FakeGateway"/> accepts
/// <c>tok_fake_success</c> without charging anyone, and simulation lets a caller script provider events.
/// Registered before the background workers so a bad configuration stops the host before anything runs.
/// </summary>
public sealed class ProductionSafetyCheck(IHostEnvironment environment, IPaymentGateway gateway, IOptions<BookingsOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsProduction())
        {
            return Task.CompletedTask;
        }
        if (gateway is FakeGateway)
        {
            throw new InvalidOperationException(
                "Bookings: FakeGateway is registered in Production. Register a real IPaymentGateway (see docs/payments/GATEWAY-READINESS.md) before starting.");
        }
        if (options.Value.EnableSimulation)
        {
            throw new InvalidOperationException("Bookings: Bookings:EnableSimulation must be false in Production.");
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
