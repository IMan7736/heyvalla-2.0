using System.Net;
using System.Net.Http.Json;
using Dhathuru.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dhathuru.Api.Tests;

public class HealthEndpointTests
{
    private sealed class FakeProbe(bool up) : IDatabaseProbe
    {
        public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(up);
    }

    private static HttpClient Client(bool databaseUp) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Database:Password", "test-only");
            b.UseSetting("Database:MigrateOnStartup", "false");
            b.ConfigureServices(s =>
            {
                s.RemoveAll<IDatabaseProbe>();
                s.AddSingleton<IDatabaseProbe>(new FakeProbe(databaseUp));
            });
        }).CreateClient();

    [Fact]
    public async Task Health_is_ok_when_the_database_is_up()
    {
        var response = await Client(true).GetAsync("/v1/health", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>(TestContext.Current.CancellationToken);
        Assert.Equal("ok", body!["status"].GetString());
        Assert.Equal("up", body["checks"].GetProperty("database").GetString());
    }

    [Fact]
    public async Task Health_is_degraded_with_503_when_the_database_is_down()
    {
        var response = await Client(false).GetAsync("/v1/health", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_routes_answer_with_problem_json()
    {
        var response = await Client(true).GetAsync("/v1/nope", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"code\":\"NOT_FOUND\"", body, StringComparison.Ordinal);
    }
}
