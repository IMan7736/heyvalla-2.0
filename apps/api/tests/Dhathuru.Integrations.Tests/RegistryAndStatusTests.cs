using Dhathuru.Catalog.Services;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Transport;
using Dhathuru.Integrations.Providers;
using Dhathuru.Integrations.Status;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dhathuru.Integrations.Tests;

public class RegistryAndStatusTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ProviderRegistry Registry(World w, Dictionary<string, string?> config, params ILiveProviderFactory[] live) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(config).Build(), w.Directory, w.Board, live, new ServiceCollection().BuildServiceProvider(), w.Clock, NullLoggerFactory.Instance);

    private sealed class LiveFactory(string id, ITransportProvider provider) : ILiveProviderFactory
    {
        public string ProviderId => id;

        public ITransportProvider Create(IServiceProvider services) => provider;
    }

    [Fact]
    public void Default_kind_is_mock_and_is_overridable_per_provider()
    {
        var w = new World();
        var registry = Registry(w, new() { ["Providers:mtcc:Kind"] = "static" });
        Assert.Equal(ProviderKind.Mock, registry.Get("manta-air").Kind);
        Assert.Equal(ProviderKind.Static, registry.Get("mtcc").Kind);
        Assert.Same(registry.Get("mtcc"), registry.Get("mtcc"));
    }

    [Fact]
    public void Default_section_changes_every_provider_without_a_specific_setting()
    {
        var w = new World();
        var registry = Registry(w, new() { ["Providers:Default:Kind"] = "static", ["Providers:maldivian:Kind"] = "mock" });
        Assert.Equal(ProviderKind.Static, registry.Get("manta-air").Kind);
        Assert.Equal(ProviderKind.Mock, registry.Get("maldivian").Kind);
    }

    [Fact]
    public void Unknown_kind_is_a_configuration_error_not_a_silent_default()
    {
        var w = new World();
        var registry = Registry(w, new() { ["Providers:mtcc:Kind"] = "quantum" });
        Assert.Throws<InvalidOperationException>(() => registry.Get("mtcc"));
    }

    [Fact]
    public void Live_without_an_adapter_degrades_to_static_instead_of_failing()
    {
        var w = new World();
        var registry = Registry(w, new() { ["Providers:mtcc:Kind"] = "live" });
        Assert.Equal(ProviderKind.Static, registry.Get("mtcc").Kind);
    }

    [Fact]
    public void Live_with_an_adapter_is_wrapped_in_the_resilience_decorator()
    {
        var w = new World();
        var inner = w.Mock("mtcc"); // any implementation stands in for a real adapter
        var registry = Registry(w, new() { ["Providers:mtcc:Kind"] = "live" }, new LiveFactory("mtcc", inner));
        var provider = registry.Get("mtcc");
        Assert.IsType<ResilientProvider>(provider);
        Assert.Equal(ProviderKind.Live, provider.Kind);
    }

    [Fact]
    public void Resilience_options_come_from_configuration()
    {
        var w = new World();
        var registry = Registry(w, new() { ["Providers:mtcc:Kind"] = "live", ["Providers:mtcc:Resilience:FailureThreshold"] = "1", ["Providers:mtcc:Resilience:Timeout"] = "00:00:01" },
            new LiveFactory("mtcc", w.Mock("mtcc")));
        Assert.IsType<ResilientProvider>(registry.Get("mtcc"));
    }

    private static (ServiceStatusService Status, World World, ProviderRegistry Registry) StatusSetup(string kind)
    {
        var w = new World();
        var registry = Registry(w, new() { ["Providers:Default:Kind"] = kind });
        return (new ServiceStatusService(w.Catalog, registry, w.Clock), w, registry);
    }

    [Fact]
    public async Task Status_of_a_mock_run_follows_scripted_disruptions_and_reports_the_source()
    {
        var (status, w, registry) = StatusSetup("mock");
        var svc = w.Service("flight-mle-gan-out");
        var dep = await w.NextDeparture(registry.Get(w.ProviderOf(svc)), svc.Slug);

        var onTime = await status.GetAsync(svc.Id, dep.ServiceRunId, Ct);
        Assert.Equal(ServiceRunState.OnTime, onTime!.State);
        Assert.Equal("mock", onTime.Source);

        w.Board.DelayRun(dep.ServiceRunId, 25);
        var delayed = await status.GetAsync(svc.Id, dep.ServiceRunId, Ct);
        Assert.Equal(ServiceRunState.Delayed, delayed!.State);
        Assert.Equal(25, delayed.DelayMinutes);
        Assert.Equal("simulated", delayed.Source);
    }

    [Fact]
    public async Task Status_without_a_run_id_reports_the_next_run()
    {
        var (status, w, _) = StatusSetup("mock");
        var svc = w.Service("flight-mle-gan-out");
        var result = await status.GetAsync(svc.Id, null, Ct);
        Assert.NotNull(result);
        Assert.StartsWith(svc.Id.Value, result.ServiceRunId);
        Assert.True(ServiceCalendar.TryParseRunId(result.ServiceRunId, out _, out var departAt));
        Assert.True(departAt > w.Clock.GetUtcNow());
    }

    [Fact]
    public async Task Static_status_is_unknown_and_labelled_static_never_on_time()
    {
        var (status, w, _) = StatusSetup("static");
        var result = await status.GetAsync(w.Service("flight-mle-gan-out").Id, null, Ct);
        Assert.Equal(ServiceRunState.Unknown, result!.State);
        Assert.Equal("static", result.Source);
    }

    [Fact]
    public async Task Unknown_service_is_null_and_a_run_of_another_service_is_rejected()
    {
        var (status, w, registry) = StatusSetup("mock");
        Assert.Null(await status.GetAsync(ServiceId.New(), null, Ct));
        var a = w.Service("flight-mle-gan-out");
        var b = w.Service("flight-mle-hanimaadhoo-out");
        var depB = await w.NextDeparture(registry.Get(w.ProviderOf(b)), b.Slug);
        Assert.Null(await status.GetAsync(a.Id, depB.ServiceRunId, Ct));
    }

    [Fact]
    public void Wire_names_for_states_match_the_contract()
    {
        Assert.Equal("on_time", StatusEndpoints.ToWire(ServiceRunState.OnTime));
        Assert.Equal("delayed", StatusEndpoints.ToWire(ServiceRunState.Delayed));
        Assert.Equal("cancelled", StatusEndpoints.ToWire(ServiceRunState.Cancelled));
        Assert.Equal("unknown", StatusEndpoints.ToWire(ServiceRunState.Unknown));
    }

    [Fact]
    public void Module_registration_exposes_the_registry_the_board_and_both_endpoint_modules()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddLogging();
        services.AddSingleton<Catalog.Data.ICatalogDataSource>(new Catalog.Data.StaticCatalogDataSource(new World().Data));
        services.AddSingleton<CatalogCache>();
        var config = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddIntegrationsModule(config);
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IProviderRegistry>());
        Assert.NotNull(provider.GetRequiredService<DisruptionBoard>());
        Assert.Contains(provider.GetServices<Domain.Http.IEndpointModule>(), m => m is StatusEndpointModule);
    }
}
