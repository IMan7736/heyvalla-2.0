using Dhathuru.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dhathuru.Integrations.Tests;

public class IntegrationsModuleTests
{
    [Fact]
    public void Module_registers_without_error()
    {
        var services = new ServiceCollection();
        var result = services.AddIntegrationsModule(new ConfigurationBuilder().Build());
        Assert.Same(services, result);
    }
}
