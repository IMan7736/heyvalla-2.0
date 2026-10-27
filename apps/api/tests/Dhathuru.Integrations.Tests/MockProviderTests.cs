using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Transport;
using Dhathuru.Integrations.Providers;

namespace Dhathuru.Integrations.Tests;

public class MockProviderTests
{
    private const string Flight = "flight-mle-gan-out";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly TimeSpan HoldFor = TimeSpan.FromMinutes(12);

    private static (World World, MockTransportProvider Provider) Setup(string slug = Flight)
    {
        var w = new World();
        return (w, w.Mock(w.ProviderOf(w.Service(slug))));
    }

    [Fact]
    public async Task Departures_carry_deterministic_seats_left_within_capacity()
    {
        var (w, p) = Setup();
        var first = await w.NextDeparture(p, Flight);
        var again = await w.NextDeparture(w.Mock(p.ProviderId), Flight);
        Assert.Equal(first.SeatsLeft, again.SeatsLeft);
        Assert.InRange(first.SeatsLeft!.Value, 0, MockTransportProvider.CapacityFor(TransportMode.DomesticFlight));
        Assert.Equal(ProviderKind.Mock, p.Kind);
        Assert.Equal("mock", p.LastSource);
    }

    [Fact]
    public async Task Different_runs_have_different_occupancy_so_demos_are_not_flat()
    {
        var (w, p) = Setup();
        var s = w.Service(Flight);
        var (start, end) = Domain.Time.MaldivesTime.DayBounds(new DateOnly(2026, 10, 28));
        var runs = await p.GetDeparturesAsync(new DepartureQuery(s.From!.Value, s.To!.Value, start, end), Ct);
        Assert.True(runs.Select(r => r.SeatsLeft).Distinct().Count() > 1);
    }

    [Fact]
    public async Task Hold_reduces_seats_and_expires_after_its_ttl()
    {
        var (w, p) = Setup();
        var dep = await w.NextDeparture(p, Flight);
        var before = dep.SeatsLeft!.Value;

        var hold = await p.HoldAsync(new HoldRequest(dep.ServiceRunId, 2, "k1", HoldFor), Ct);
        Assert.True(hold.Success);
        Assert.Equal(w.Clock.GetUtcNow() + HoldFor, hold.ExpiresAt);
        Assert.Equal(before - 2, (await p.GetAvailabilityAsync(new AvailabilityQuery(dep.ServiceRunId, 1), Ct)).SeatsLeft);

        w.Clock.Advance(HoldFor + TimeSpan.FromSeconds(1));
        Assert.Equal(before, (await p.GetAvailabilityAsync(new AvailabilityQuery(dep.ServiceRunId, 1), Ct)).SeatsLeft);
        var confirm = await p.ConfirmAsync(new ConfirmRequest(hold.HoldReference!, "c1"), Ct);
        Assert.Equal(MockTransportProvider.HoldExpired, confirm.FailureCode);
    }

    [Fact]
    public async Task Hold_is_idempotent_by_key()
    {
        var (w, p) = Setup();
        var dep = await w.NextDeparture(p, Flight);
        var a = await p.HoldAsync(new HoldRequest(dep.ServiceRunId, 3, "same", HoldFor), Ct);
        var b = await p.HoldAsync(new HoldRequest(dep.ServiceRunId, 3, "same", HoldFor), Ct);
        Assert.Equal(a, b);
        Assert.Equal(dep.SeatsLeft - 3, (await p.GetAvailabilityAsync(new AvailabilityQuery(dep.ServiceRunId, 1), Ct)).SeatsLeft);
    }

    [Fact]
    public async Task Cannot_hold_more_than_the_seats_left_and_a_failure_can_be_retried_later()
    {
        var (w, p) = Setup();
        var dep = await w.NextDeparture(p, Flight);
        var tooMany = dep.SeatsLeft!.Value + 1;
        var denied = await p.HoldAsync(new HoldRequest(dep.ServiceRunId, tooMany, "k", HoldFor), Ct);
        Assert.False(denied.Success);
        Assert.Equal(MockTransportProvider.SoldOut, denied.FailureCode);
        var ok = await p.HoldAsync(new HoldRequest(dep.ServiceRunId, 1, "k", HoldFor), Ct);
        Assert.True(ok.Success, "failed attempts are not cached under the idempotency key");
    }

    [Fact]
    public async Task Two_holds_cannot_oversell_the_last_seats()
    {
        var (w, p) = Setup();
        var dep = await w.NextDeparture(p, Flight);
        var seats = dep.SeatsLeft!.Value;
        var results = await Task.WhenAll(Enumerable.Range(0, seats + 5).Select(i =>
            Task.Run(() => p.HoldAsync(new HoldRequest(dep.ServiceRunId, 1, $"k{i}", HoldFor), Ct), Ct)));
        Assert.Equal(seats, results.Count(r => r.Success));
        Assert.All(results.Where(r => !r.Success), r => Assert.Equal(MockTransportProvider.SoldOut, r.FailureCode));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Passenger_count_must_be_positive(int passengers)
    {
        var (w, p) = Setup();
        var dep = await w.NextDeparture(p, Flight);
        Assert.Equal(MockTransportProvider.InvalidPassengers, (await p.HoldAsync(new HoldRequest(dep.ServiceRunId, passengers, "k", HoldFor), Ct)).FailureCode);
    }

    [Fact]
    public async Task Confirm_is_idempotent_and_keeps_the_seats()
    {
        var (w, p) = Setup();
        var dep = await w.NextDeparture(p, Flight);
        var hold = await p.HoldAsync(new HoldRequest(dep.ServiceRunId, 2, "h", HoldFor), Ct);
        var c1 = await p.ConfirmAsync(new ConfirmRequest(hold.HoldReference!, "c"), Ct);
        var c2 = await p.ConfirmAsync(new ConfirmRequest(hold.HoldReference!, "c"), Ct);
        Assert.True(c1.Success);
        Assert.Equal(c1, c2);
        // a different key for the same hold returns the same booking, not a second one
        var c3 = await p.ConfirmAsync(new ConfirmRequest(hold.HoldReference!, "other"), Ct);
        Assert.Equal(c1.ProviderReference, c3.ProviderReference);

        w.Clock.Advance(HoldFor * 3);
        Assert.Equal(dep.SeatsLeft - 2, (await p.GetAvailabilityAsync(new AvailabilityQuery(dep.ServiceRunId, 1), Ct)).SeatsLeft);
    }

    [Fact]
    public async Task Confirm_of_an_unknown_hold_fails()
    {
        var (_, p) = Setup();
        Assert.Equal(MockTransportProvider.HoldNotFound, (await p.ConfirmAsync(new ConfirmRequest("hld_nope", "c"), Ct)).FailureCode);
    }

    [Fact]
    public async Task Scripted_confirm_failure_hits_once_then_recovers()
    {
        var (w, p) = Setup();
        var dep = await w.NextDeparture(p, Flight);
        p.FailNextConfirm(dep.ServiceRunId);
        var hold = await p.HoldAsync(new HoldRequest(dep.ServiceRunId, 1, "h", HoldFor), Ct);
        var failed = await p.ConfirmAsync(new ConfirmRequest(hold.HoldReference!, "c1"), Ct);
        Assert.Equal(MockTransportProvider.ProviderRejected, failed.FailureCode);
        Assert.True((await p.ConfirmAsync(new ConfirmRequest(hold.HoldReference!, "c2"), Ct)).Success);
    }

    [Theory]
    [InlineData(300, 100)]   // 5 h before: full refund
    [InlineData(100, 50)]    // 100 min before: half
    [InlineData(20, 0)]      // 20 min before: nothing
    public async Task Cancel_refunds_by_time_to_departure(int minutesBefore, int percent)
    {
        var (w, p) = Setup();
        var dep = await w.NextDeparture(p, Flight);
        var hold = await p.HoldAsync(new HoldRequest(dep.ServiceRunId, 2, "h", HoldFor), Ct);
        var confirm = await p.ConfirmAsync(new ConfirmRequest(hold.HoldReference!, "c"), Ct);
        w.Clock.Advance(dep.DepartAt - w.Clock.GetUtcNow() - TimeSpan.FromMinutes(minutesBefore));

        var cancel = await p.CancelAsync(new CancelRequest(confirm.ProviderReference!, "x"), Ct);
        Assert.True(cancel.Success);
        Assert.Equal(new Money(dep.Fare.AmountMinor * 2 * percent / 100), cancel.Refund);
    }

    [Fact]
    public async Task Cancel_frees_the_seats_and_is_idempotent()
    {
        var (w, p) = Setup();
        var dep = await w.NextDeparture(p, Flight);
        var hold = await p.HoldAsync(new HoldRequest(dep.ServiceRunId, 4, "h", HoldFor), Ct);
        var confirm = await p.ConfirmAsync(new ConfirmRequest(hold.HoldReference!, "c"), Ct);
        var first = await p.CancelAsync(new CancelRequest(confirm.ProviderReference!, "x"), Ct);
        var replay = await p.CancelAsync(new CancelRequest(confirm.ProviderReference!, "x"), Ct);
        var again = await p.CancelAsync(new CancelRequest(confirm.ProviderReference!, "y"), Ct);
        Assert.Equal(first, replay);
        Assert.Equal(new Money(0), again.Refund); // a second cancel never refunds twice
        Assert.Equal(dep.SeatsLeft, (await p.GetAvailabilityAsync(new AvailabilityQuery(dep.ServiceRunId, 1), Ct)).SeatsLeft);
        Assert.Equal(MockTransportProvider.BookingNotFound, (await p.CancelAsync(new CancelRequest("pvr_unknown", "z"), Ct)).FailureCode);
    }

    [Fact]
    public async Task Departed_and_unknown_runs_cannot_be_held()
    {
        var (w, p) = Setup();
        var dep = await w.NextDeparture(p, Flight);
        Assert.Equal(MockTransportProvider.RunNotFound, (await p.HoldAsync(new HoldRequest("svc_00000000000000000000000000:2026-10-27T07:00Z", 1, "a", HoldFor), Ct)).FailureCode);
        w.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(MockTransportProvider.RunDeparted, (await p.HoldAsync(new HoldRequest(dep.ServiceRunId, 1, "b", HoldFor), Ct)).FailureCode);
    }

    [Fact]
    public async Task Scripted_delay_shows_in_status_and_marks_the_source_simulated()
    {
        var (w, p) = Setup();
        var dep = await w.NextDeparture(p, Flight);
        Assert.Equal(ServiceRunState.OnTime, (await p.GetStatusAsync(dep.ServiceRunId, Ct)).State);

        w.Board.DelayRun(dep.ServiceRunId, 25);
        var status = await p.GetStatusAsync(dep.ServiceRunId, Ct);
        Assert.Equal(ServiceRunState.Delayed, status.State);
        Assert.Equal(25, status.DelayMinutes);
        Assert.Equal("simulated", p.LastSource);

        w.Board.Clear();
        Assert.Equal("mock", p.LastSource);
        Assert.Equal(ServiceRunState.OnTime, (await p.GetStatusAsync(dep.ServiceRunId, Ct)).State);
    }

    [Fact]
    public async Task Cancelled_run_disappears_from_departures_and_cannot_be_held()
    {
        var (w, p) = Setup();
        var dep = await w.NextDeparture(p, Flight);
        w.Board.CancelRun(dep.ServiceRunId);
        Assert.Equal(ServiceRunState.Cancelled, (await p.GetStatusAsync(dep.ServiceRunId, Ct)).State);
        var next = await w.NextDeparture(p, Flight);
        Assert.NotEqual(dep.ServiceRunId, next.ServiceRunId);
        Assert.Equal(MockTransportProvider.RunCancelled, (await p.HoldAsync(new HoldRequest(dep.ServiceRunId, 1, "k", HoldFor), Ct)).FailureCode);
        Assert.False((await p.GetAvailabilityAsync(new AvailabilityQuery(dep.ServiceRunId, 1), Ct)).Available);
    }

    [Fact]
    public async Task Service_wide_disruption_applies_to_every_run_and_run_level_overrides_it()
    {
        var (w, p) = Setup();
        var s = w.Service(Flight);
        var dep = await w.NextDeparture(p, Flight);
        w.Board.DelayService(s.Id, 40);
        Assert.Equal(40, (await p.GetStatusAsync(dep.ServiceRunId, Ct)).DelayMinutes);
        w.Board.DelayRun(dep.ServiceRunId, 10);
        Assert.Equal(10, (await p.GetStatusAsync(dep.ServiceRunId, Ct)).DelayMinutes);
        Assert.True(w.Board.ClearRun(dep.ServiceRunId));
        Assert.Equal(40, (await p.GetStatusAsync(dep.ServiceRunId, Ct)).DelayMinutes);
    }

    [Fact]
    public async Task Taxi_is_always_available_and_bookable_on_demand()
    {
        var w = new World();
        var taxi = w.Data.Services.Single(s => s.OnDemand);
        var p = w.Mock(w.ProviderOf(taxi));
        var a = w.Data.Places.Single(x => x.ZoneKey == "velana").Id;
        var b = w.Data.Places.Single(x => x.ZoneKey == "male").Id;
        var dep = (await p.GetDeparturesAsync(new DepartureQuery(a, b, w.Clock.GetUtcNow(), w.Clock.GetUtcNow().AddHours(1)), Ct)).Single();
        Assert.Equal(4, dep.SeatsLeft);
        var hold = await p.HoldAsync(new HoldRequest(dep.ServiceRunId, 2, "k", HoldFor), Ct);
        Assert.True(hold.Success);
        Assert.True((await p.ConfirmAsync(new ConfirmRequest(hold.HoldReference!, "c"), Ct)).Success);
        Assert.Equal(Money.FromMvr(70), dep.Fare);
    }

    [Fact]
    public void Refund_policy_is_a_pure_function()
    {
        var departAt = new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);
        var paid = Money.FromMvr(100);
        Assert.Equal(paid, MockTransportProvider.Refund(paid, departAt, departAt.AddHours(-2)));
        Assert.Equal(Money.FromMvr(50), MockTransportProvider.Refund(paid, departAt, departAt.AddMinutes(-119)));
        Assert.Equal(Money.FromMvr(50), MockTransportProvider.Refund(paid, departAt, departAt.AddMinutes(-30)));
        Assert.Equal(Money.Zero, MockTransportProvider.Refund(paid, departAt, departAt.AddMinutes(-29)));
        Assert.Equal(Money.Zero, MockTransportProvider.Refund(paid, departAt, departAt.AddMinutes(5)));
    }
}
