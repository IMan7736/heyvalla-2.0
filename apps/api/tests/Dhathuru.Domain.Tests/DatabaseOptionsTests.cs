using Dhathuru.Domain.Configuration;
using Microsoft.Extensions.Configuration;

namespace Dhathuru.Domain.Tests;

public class DatabaseOptionsTests
{
    [Fact]
    public void Builds_a_connection_string_from_configuration()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Name"] = "dhathuru_atoll",
            ["Database:Password"] = "p;w",
        }).Build();

        var cs = config.GetDatabaseOptions().ToConnectionString();

        Assert.Contains("Database=dhathuru_atoll", cs, StringComparison.Ordinal);
        Assert.Contains("Password=\"p;w\"", cs, StringComparison.Ordinal);
        Assert.Contains("Host=localhost", cs, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_password_fails_loudly() =>
        Assert.Throws<InvalidOperationException>(() => new DatabaseOptions().ToConnectionString());
}
