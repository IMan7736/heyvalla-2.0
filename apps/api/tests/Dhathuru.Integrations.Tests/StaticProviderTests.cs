using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Time;
using Dhathuru.Domain.Transport;
using Dhathuru.Integrations.Providers;

namespace Dhathuru.Integrations.Tests;

public class StaticProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lists_only_departures_inside_the_half_open_window_for_its_own_operator()
    {
        var w = new World();
        var svc = w.Service("speedboat-male-maafushi-out");
        var provider = w.Static(w.ProviderOf(svc));
        var from = MaldivesTime.FromLocal(new DateOnly(2026, 10, 27), new TimeOnly(11, 0));
        var window = new DepartureQuery(svc.From!.Value, svc.To!.Value, from, from.AddHours(3)); // 11:00-14:00 local, 14:00 excluded
        var departures = await provider.GetDeparturesAsync(window, Ct);
        Assert.Equal(["11:00"], departures.Select(d => MaldivesTime.ToLocal(d.DepartAt).ToString("HH:mm")));
        Assert.Equal(svc.Fare, departures[0].Fare);
        Assert.Null(departures[0].SeatsLeft);
    }

    [Fact]
    public async Task Another_operators_provider_does_not_see_the_service()
    {
        var w = new World();
        var svc = w.Service("speedboat-male-maafushi-out");
        var other = w.Static("maldivian");
        var (start, end) = MaldivesTime.DayBounds(new DateOnly(2026, 10, 27));
        Assert.Empty(await other.GetDeparturesAsync(new DepartureQuery(svc.From!.Value, svc.To!.Value, start, end), Ct));
    }

    [Fact]
    public async Task Window_across_two_local_days_returns_both_days()
    {
        var w = new World();
        var svc = w.Service("speedboat-male-maafushi-out");
        var provider = w.Static(w.ProviderOf(svc));
        var start = MaldivesTime.FromLocal(new DateOnly(2026, 10, 27), new TimeOnly(15, 0));
        var departures = await provider.GetDeparturesAsync(new DepartureQuery(svc.From!.Value, svc.To!.Value, start, start.AddHours(24)), Ct);
        Assert.Equal(["16:30", "08:30", "11:00", "14:00"], departures.OrderBy(d => d.DepartAt).Select(d => MaldivesTime.ToLocal(d.DepartAt).ToString("HH:mm")));
    }

    [Fact]
    public async Task It_never_pretends_to_sell_seats_or_know_the_status()
    {
        var w = new World();
        var provider = w.Static(w.ProviderOf(w.Service("speedboat-male-maafushi-out")));
        var dep = await w.NextDeparture(provider, "speedboat-male-maafushi-out");

        Assert.Equal(ProviderKind.Static, provider.Kind);
        Assert.Equal("static", provider.LastSource);
        var availability = await provider.GetAvailabilityAsync(new AvailabilityQuery(dep.ServiceRunId, 2), Ct);
        Assert.True(availability.Available);
        Assert.Null(availability.SeatsLeft);
        Assert.Equal(dep.Fare * 2, availability.Price);

        Assert.Equal(StaticTimetableProvider.ReadOnlyFailure, (await provider.HoldAsync(new HoldRequest(dep.ServiceRunId, 1, "k", TimeSpan.FromMinutes(10)), Ct)).FailureCode);
        Assert.False((await provider.ConfirmAsync(new ConfirmRequest("h", "k"), Ct)).Success);
        Assert.False((await provider.CancelAsync(new CancelRequest("p", "k"), Ct)).Success);
        var status = await provider.GetStatusAsync(dep.ServiceRunId, Ct);
        Assert.Equal(ServiceRunState.Unknown, status.State);
    }

    [Fact]
    public async Task Unknown_or_departed_runs_are_unavailable()
    {
        var w = new World();
        var provider = w.Static(w.ProviderOf(w.Service("speedboat-male-maafushi-out")));
        Assert.False((await provider.GetAvailabilityAsync(new AvailabilityQuery("garbage", 1), Ct)).Available);
        var dep = await w.NextDeparture(provider, "speedboat-male-maafushi-out");
        w.Clock.Advance(TimeSpan.FromHours(24));
        Assert.False((await provider.GetAvailabilityAsync(new AvailabilityQuery(dep.ServiceRunId, 1), Ct)).Available);
    }

    [Fact]
    public async Task Taxi_is_on_demand_with_matrix_fare_and_rounded_start()
    {
        var w = new World();
        var taxi = w.Data.Services.Single(s => s.OnDemand);
        var provider = w.Static(w.ProviderOf(taxi));
        var male = w.Data.Places.Single(p => p.ZoneKey == "male").Id;
        var hulhumale = w.Data.Places.Single(p => p.ZoneKey == "hulhumale-1").Id;
        var departures = await provider.GetDeparturesAsync(new DepartureQuery(male, hulhumale, w.Clock.GetUtcNow().AddMinutes(2), w.Clock.GetUtcNow().AddHours(1)), Ct);
        var only = Assert.Single(departures);
        Assert.Equal(Domain.Pricing.Money.FromMvr(85), only.Fare);
        Assert.Equal(0, only.DepartAt.Minute % 5);
        Assert.True(only.DepartAt >= w.Clock.GetUtcNow().AddMinutes(2));
        Assert.Equal(Domain.Transport.PricingKind.Fixed, only.Pricing);
        Assert.Contains("@", only.ServiceRunId);
    }
}
