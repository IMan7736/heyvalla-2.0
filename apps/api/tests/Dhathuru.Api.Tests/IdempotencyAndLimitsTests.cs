using System.Net;
using System.Net.Http.Json;
using Dhathuru.Api.Tests.Support;

namespace Dhathuru.Api.Tests;

public sealed class IdempotencyAndLimitsTests(PostgresFixture database) : IDisposable
{
    private readonly ApiFactory api = new(database);
    private readonly List<IDisposable> extra = [];

    public void Dispose()
    {
        api.Dispose();
        foreach (var e in extra) e.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static HttpRequestMessage Post(string key, object body, string path = "/v1/_test/echo")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (key.Length > 0) request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    [Fact]
    public async Task A_retry_with_the_same_key_replays_the_stored_response_without_running_the_endpoint_again()
    {
        var user = await api.RegisterAndLoginAsync();
        var client = api.As(user.Tokens.AccessToken);
        var key = $"key-{Guid.NewGuid():N}";

        var first = await client.SendAsync(Post(key, new { amount = 5 }), Ct);
        var second = await client.SendAsync(Post(key, new { amount = 5 }), Ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(Ct), await second.Content.ReadAsStringAsync(Ct));
        Assert.Equal("true", second.Headers.GetValues("Idempotent-Replayed").Single());
        Assert.False(first.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(1, api.Counter.Calls);
    }

    [Fact]
    public async Task Reusing_a_key_for_a_different_request_is_a_conflict()
    {
        var user = await api.RegisterAndLoginAsync();
        var client = api.As(user.Tokens.AccessToken);
        var key = $"key-{Guid.NewGuid():N}";
        await client.SendAsync(Post(key, new { amount = 5 }), Ct);

        var reused = await client.SendAsync(Post(key, new { amount = 6 }), Ct);
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await reused.CodeAsync());
    }

    [Fact]
    public async Task The_key_header_is_required_and_must_be_well_formed()
    {
        var user = await api.RegisterAndLoginAsync();
        var client = api.As(user.Tokens.AccessToken);
        var missing = await client.SendAsync(Post("", new { a = 1 }), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REQUIRED", await missing.CodeAsync());
        var bad = await client.SendAsync(Post("x", new { a = 1 }), Ct);
        Assert.Equal("IDEMPOTENCY_KEY_INVALID", await bad.CodeAsync());
    }

    [Fact]
    public async Task Keys_are_private_to_the_caller()
    {
        var alice = await api.RegisterAndLoginAsync();
        var bob = await api.RegisterAndLoginAsync();
        var key = $"key-{Guid.NewGuid():N}";
        await api.As(alice.Tokens.AccessToken).SendAsync(Post(key, new { who = "alice" }), Ct);

        var bobs = await api.As(bob.Tokens.AccessToken).SendAsync(Post(key, new { who = "bob" }), Ct);
        Assert.Equal(HttpStatusCode.OK, bobs.StatusCode);
        Assert.False(bobs.Headers.Contains("Idempotent-Replayed"));
        Assert.Contains("bob", await bobs.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(2, api.Counter.Calls);
    }

    [Fact]
    public async Task Server_errors_are_not_stored_so_a_retry_really_retries()
    {
        var user = await api.RegisterAndLoginAsync();
        var client = api.As(user.Tokens.AccessToken);
        var key = $"key-{Guid.NewGuid():N}";
        Assert.Equal(HttpStatusCode.InternalServerError, (await client.SendAsync(Post(key, new { a = 1 }, "/v1/_test/fail"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, (await client.SendAsync(Post(key, new { a = 1 }, "/v1/_test/fail"), Ct)).StatusCode);
        Assert.Equal(2, api.Counter.Calls);
    }

    [Fact]
    public async Task Parallel_requests_with_one_key_run_the_endpoint_once()
    {
        var user = await api.RegisterAndLoginAsync();
        var client = api.As(user.Tokens.AccessToken);
        var key = $"key-{Guid.NewGuid():N}";
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.SendAsync(Post(key, new { a = 1 }), Ct)));
        Assert.Equal(1, api.Counter.Calls);
        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task Auth_endpoints_are_rate_limited_per_client_with_a_problem_response()
    {
        var limited = new ApiFactory(database, new() { ["RateLimiting:Enabled"] = "true", ["RateLimiting:AuthPermits"] = "3" });
        extra.Add(limited);
        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 5; i++)
        {
            last = await limited.Anonymous().PostAsJsonAsync("/v1/auth/login", new { email = "nobody@example.com", password = "whatever password" }, Ct);
            statuses.Add(last.StatusCode);
        }

        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.Unauthorized));
        Assert.Equal(2, statuses.Count(s => s == HttpStatusCode.TooManyRequests));
        Assert.Equal("RATE_LIMITED", await last!.CodeAsync());
        Assert.True(last!.Headers.Contains("Retry-After"));
        Assert.Equal(HttpStatusCode.OK, (await limited.Anonymous().GetAsync("/v1/health/live", Ct)).StatusCode); // health is exempt
    }
}
