using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Dhathuru.Api.Tests.Support.PostgresFixture))]

namespace Dhathuru.Api.Tests.Support;

/// <summary>One real PostGIS container for the whole test assembly (the image is already used by docker-compose).</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgis/postgis:17-3.5")
        .WithUsername("dhathuru")
        .WithPassword("test-only-password")
        .WithDatabase("dhathuru_test")
        .Build();

    public string Host => container.Hostname;

    public int Port => container.GetMappedPublicPort(5432);

    public ValueTask InitializeAsync() => new(container.StartAsync());

    public ValueTask DisposeAsync() => container.DisposeAsync();
}
