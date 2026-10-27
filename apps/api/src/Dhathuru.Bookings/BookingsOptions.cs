namespace Dhathuru.Bookings;

public sealed class BookingsOptions
{
    public const string Section = "Bookings";

    /// <summary>How long a hold lasts before it is released (docs/CONTRACT.md: default 12 minutes).</summary>
    public TimeSpan HoldTtl { get; set; } = TimeSpan.FromMinutes(12);

    /// <summary>Extra time a provider hold outlives ours, so a payment that lands at the last second can still confirm.</summary>
    public TimeSpan ProviderHoldGrace { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>How long we wait for the payment gateway before looking the charge up.</summary>
    public TimeSpan PaymentTimeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>A booking stuck in payment_pending longer than this after its hold is reconciled by the sweeper.</summary>
    public TimeSpan PaymentPendingGrace { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Booking-level service fee in minor units, shown as its own price line. Zero by default and then omitted.</summary>
    public long ServiceFeeMinor { get; set; }

    public TimeSpan SweeperInterval { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan DisruptionPollInterval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Poll providers for status of confirmed legs departing within this horizon.</summary>
    public TimeSpan DisruptionHorizon { get; set; } = TimeSpan.FromHours(36);

    public bool SweeperEnabled { get; set; } = true;

    public bool DisruptionMonitorEnabled { get; set; } = true;

    /// <summary>Public web origin used for links in emails.</summary>
    public string WebBaseUrl { get; set; } = "http://localhost:3000";

    /// <summary>Development only. Lets the fake <c>tok_fake_leg_failure</c> token fail a leg confirmation, and enables the disruption simulator endpoint.</summary>
    public bool EnableSimulation { get; set; }
}
