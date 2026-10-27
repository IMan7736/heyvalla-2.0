using Dhathuru.Bookings.Payments;
using Dhathuru.Bookings.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dhathuru.Bookings.Tests;

public class ProductionSafetyTests
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "test";

        public string ContentRootPath { get; set; } = "/";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IServiceProvider Services(string environment, bool simulation, bool realGateway)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Bookings:EnableSimulation"] = simulation ? "true" : "false" })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new Env(environment));
        if (realGateway)
        {
            // Stands in for a real gateway: anything that is not the in-house fake.
            services.AddSingleton<IPaymentGateway>(new FlakyGateway(new FakeGateway(Options.Create(new FakeGatewayOptions()))));
        }
        services.AddBookingsModule(configuration);
        return services.BuildServiceProvider();
    }

    private static Task Start(IServiceProvider sp) =>
        sp.GetServices<IHostedService>().OfType<ProductionSafetyCheck>().Single().StartAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task Production_with_the_fake_gateway_refuses_to_start()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Start(Services("Production", simulation: false, realGateway: false)));
        Assert.Contains("FakeGateway", error.Message);
    }

    [Fact]
    public async Task Production_with_simulation_on_refuses_to_start()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Start(Services("Production", simulation: true, realGateway: true)));
        Assert.Contains("EnableSimulation", error.Message);
    }

    [Fact]
    public async Task Production_with_a_real_gateway_and_no_simulation_starts()
    {
        await Start(Services("Production", simulation: false, realGateway: true));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public async Task Other_environments_may_use_the_fake_gateway_and_simulation(string environment)
    {
        await Start(Services(environment, simulation: true, realGateway: false));
    }

    [Fact]
    public void The_check_runs_before_the_background_workers()
    {
        var hosted = Services("Production", simulation: false, realGateway: false).GetServices<IHostedService>().Select(s => s.GetType()).ToList();
        Assert.True(hosted.IndexOf(typeof(ProductionSafetyCheck)) < hosted.IndexOf(typeof(HoldSweeper)));
    }
}
