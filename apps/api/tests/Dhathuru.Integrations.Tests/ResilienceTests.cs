using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Transport;
using Dhathuru.Integrations.Providers;

namespace Dhathuru.Integrations.Tests;

public class CircuitBreakerTests
{
    [Fact]
    public void Opens_after_the_threshold_and_allows_one_trial_after_the_open_period()
    {
        var clock = new TestClock(World.Start);
        var breaker = new CircuitBreaker(3, TimeSpan.FromSeconds(30), clock);
        for (var i = 0; i < 2; i++)
        {
            Assert.True(breaker.TryAcquire());
            breaker.RecordFailure();
        }
        Assert.False(breaker.IsOpen);
        breaker.RecordFailure();
        Assert.True(breaker.IsOpen);
        Assert.False(breaker.TryAcquire());

        clock.Advance(TimeSpan.FromSeconds(31));
        Assert.True(breaker.TryAcquire());   // the single trial
        Assert.False(breaker.TryAcquire());  // others wait for its result
        breaker.RecordFailure();             // trial failed: open again
        Assert.True(breaker.IsOpen);
        clock.Advance(TimeSpan.FromSeconds(31));
        Assert.True(breaker.TryAcquire());
        breaker.RecordSuccess();
        Assert.False(breaker.IsOpen);
        Assert.True(breaker.TryAcquire());
    }

    [Fact]
    public void A_success_resets_the_failure_count()
    {
        var breaker = new CircuitBreaker(2, TimeSpan.FromSeconds(30), new TestClock(World.Start));
        breaker.RecordFailure();
        breaker.RecordSuccess();
        breaker.RecordFailure();
        Assert.False(breaker.IsOpen);
    }
}

public class ResilientProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Scriptable stand-in for a real operator API.</summary>
    private sealed class FakeLive(string id) : ITransportProvider
    {
        public Func<CancellationToken, Task>? Behaviour { get; set; }

        public int Calls { get; private set; }

        public string ProviderId => id;

        public ProviderKind Kind => ProviderKind.Live;

        public async Task<IReadOnlyList<ServiceDeparture>> GetDeparturesAsync(DepartureQuery q, CancellationToken ct)
        {
            Calls++;
            if (Behaviour is not null) await Behaviour(ct);
            return [new ServiceDeparture("live-run", ServiceId.New(), q.From, q.To, q.WindowStart, q.WindowStart.AddHours(1), Money.FromMvr(1), PricingKind.Fixed, 7)];
        }

        public Task<Availability> GetAvailabilityAsync(AvailabilityQuery q, CancellationToken ct) => Task.FromResult(new Availability(true, 3));

        public async Task<HoldResult> HoldAsync(HoldRequest r, CancellationToken ct)
        {
            Calls++;
            if (Behaviour is not null) await Behaviour(ct);
            return new HoldResult(true, "live-hold", DateTimeOffset.UnixEpoch);
        }

        public Task<ConfirmResult> ConfirmAsync(ConfirmRequest r, CancellationToken ct) => Task.FromResult(new ConfirmResult(true, "p"));

        public Task<CancelResult> CancelAsync(CancelRequest r, CancellationToken ct) => Task.FromResult(new CancelResult(true));

        public Task<ServiceStatus> GetStatusAsync(string serviceRunId, CancellationToken ct) =>
            Task.FromResult(new ServiceStatus(serviceRunId, ServiceRunState.OnTime, 0, DateTimeOffset.UnixEpoch));
    }

    private static (ResilientProvider Provider, FakeLive Live, World World, DepartureQuery Query) Setup(ResilientOptions? options = null)
    {
        var w = new World();
        var svc = w.Service("speedboat-male-maafushi-out");
        var live = new FakeLive(w.ProviderOf(svc));
        var fallback = w.Static(live.ProviderId);
        var provider = new ResilientProvider(live, fallback, options ?? new ResilientOptions { Timeout = TimeSpan.FromMilliseconds(150) }, w.Clock);
        var (start, end) = Domain.Time.MaldivesTime.DayBounds(new DateOnly(2026, 10, 27));
        return (provider, live, w, new DepartureQuery(svc.From!.Value, svc.To!.Value, start, end));
    }

    [Fact]
    public async Task Healthy_live_provider_is_used_and_reported_live()
    {
        var (p, _, _, q) = Setup();
        var result = await p.GetDeparturesAsync(q, Ct);
        Assert.Equal("live-run", result.Single().ServiceRunId);
        Assert.Equal("live", p.LastSource);
        Assert.Equal(ProviderKind.Live, p.Kind);
    }

    [Fact]
    public async Task Failure_falls_back_to_the_static_timetable_and_says_so()
    {
        var (p, live, _, q) = Setup();
        live.Behaviour = _ => throw new HttpRequestException("503");
        var result = await p.GetDeparturesAsync(q, Ct);
        Assert.NotEmpty(result);
        Assert.All(result, d => Assert.NotEqual("live-run", d.ServiceRunId));
        Assert.Equal("static", p.LastSource);
    }

    [Fact]
    public async Task A_slow_provider_times_out_and_falls_back()
    {
        var (p, live, _, q) = Setup();
        live.Behaviour = ct => Task.Delay(TimeSpan.FromSeconds(10), ct);
        var started = DateTime.UtcNow;
        var result = await p.GetDeparturesAsync(q, Ct);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(3));
        Assert.NotEmpty(result);
        Assert.Equal("static", p.LastSource);
    }

    [Fact]
    public async Task Circuit_opens_after_repeated_failures_and_stops_calling_the_provider()
    {
        var (p, live, w, q) = Setup(new ResilientOptions { Timeout = TimeSpan.FromSeconds(1), FailureThreshold = 3, OpenFor = TimeSpan.FromSeconds(30), DepartureCacheTtl = TimeSpan.Zero });
        live.Behaviour = _ => throw new HttpRequestException("down");
        for (var i = 0; i < 3; i++) await p.GetDeparturesAsync(q, Ct);
        Assert.True(p.CircuitOpen);
        var callsWhenOpen = live.Calls;
        await p.GetDeparturesAsync(q, Ct);
        Assert.Equal(callsWhenOpen, live.Calls);

        // provider recovers; after the open period one trial succeeds and live is back
        live.Behaviour = null;
        w.Clock.Advance(TimeSpan.FromSeconds(31));
        await p.GetDeparturesAsync(q, Ct);
        Assert.Equal("live", p.LastSource);
        Assert.False(p.CircuitOpen);
    }

    [Fact]
    public async Task Departures_are_cached_briefly_to_protect_the_provider()
    {
        var (p, live, w, q) = Setup(new ResilientOptions { DepartureCacheTtl = TimeSpan.FromSeconds(30) });
        await p.GetDeparturesAsync(q, Ct);
        await p.GetDeparturesAsync(q, Ct);
        Assert.Equal(1, live.Calls);
        w.Clock.Advance(TimeSpan.FromSeconds(31));
        await p.GetDeparturesAsync(q, Ct);
        Assert.Equal(2, live.Calls);
    }

    [Fact]
    public async Task Fallback_answers_are_not_cached_as_if_they_were_live()
    {
        var (p, live, _, q) = Setup(new ResilientOptions { Timeout = TimeSpan.FromSeconds(1), DepartureCacheTtl = TimeSpan.FromMinutes(5), FailureThreshold = 10 });
        live.Behaviour = _ => throw new HttpRequestException("down");
        await p.GetDeparturesAsync(q, Ct);
        live.Behaviour = null;
        var again = await p.GetDeparturesAsync(q, Ct);
        Assert.Equal("live-run", again.Single().ServiceRunId);
    }

    [Fact]
    public async Task Writes_never_fall_back_to_a_timetable_that_cannot_sell_seats()
    {
        var (p, live, _, _) = Setup(new ResilientOptions { Timeout = TimeSpan.FromSeconds(1) });
        live.Behaviour = _ => throw new HttpRequestException("down");
        var hold = await p.HoldAsync(new HoldRequest("run", 1, "k", TimeSpan.FromMinutes(5)), Ct);
        Assert.False(hold.Success);
        Assert.Equal(ResilientProvider.Unavailable, hold.FailureCode);
    }

    [Fact]
    public async Task Callers_cancellation_is_not_swallowed_as_a_provider_failure()
    {
        var (p, live, _, q) = Setup(new ResilientOptions { Timeout = TimeSpan.FromSeconds(30) });
        live.Behaviour = ct => Task.Delay(TimeSpan.FromSeconds(10), ct);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => p.GetDeparturesAsync(q, cts.Token));
        Assert.False(p.CircuitOpen);
    }
}
