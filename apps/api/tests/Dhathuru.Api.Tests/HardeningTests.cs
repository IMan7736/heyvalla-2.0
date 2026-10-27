using System.Net;
using System.Net.Http.Json;
using Dhathuru.Api.Tests.Support;

namespace Dhathuru.Api.Tests;

/// <summary>Findings L-03, L-08, L-13 and L-14 from the QA report.</summary>
public sealed class HardeningTests(PostgresFixture database) : IDisposable
{
    private const string BffKey = "test-only-bff-key";

    private readonly List<ApiFactory> factories = [];

    public void Dispose()
    {
        foreach (var f in factories) f.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ApiFactory Limited(int authPermits = 3, int defaultPermits = 300)
    {
        var api = new ApiFactory(database, new Dictionary<string, string?>
        {
            ["RateLimiting:Enabled"] = "true",
            ["RateLimiting:AuthPermits"] = authPermits.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["RateLimiting:DefaultPermits"] = defaultPermits.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Bff:SharedKey"] = BffKey,
        });
        factories.Add(api);
        return api;
    }

    private static async Task<HttpStatusCode> LoginAsync(HttpClient client, string? forwardedFor = null, string? bffIp = null, string? bffKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = $"nobody-{Guid.NewGuid():N}@example.com", password = "wrong password here" }),
        };
        if (forwardedFor is not null) request.Headers.Add("X-Forwarded-For", forwardedFor);
        if (bffIp is not null) request.Headers.Add("X-Dhathuru-Client-Ip", bffIp);
        if (bffKey is not null) request.Headers.Add("X-Dhathuru-Bff-Key", bffKey);
        return (await client.SendAsync(request, Ct)).StatusCode;
    }

    [Fact]
    public async Task A_spoofed_forwarded_for_header_does_not_change_the_rate_limit_key()
    {
        var client = Limited().Anonymous();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++) statuses.Add(await LoginAsync(client, forwardedFor: $"10.8.{i}.7"));

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task A_client_address_without_the_shared_key_is_ignored()
    {
        var client = Limited().Anonymous();
        var none = new List<HttpStatusCode>();
        var wrong = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++) none.Add(await LoginAsync(client, bffIp: $"198.51.100.{i}"));
        Assert.Equal(HttpStatusCode.TooManyRequests, none[^1]);

        var other = Limited().Anonymous();
        for (var i = 0; i < 5; i++) wrong.Add(await LoginAsync(other, bffIp: $"198.51.100.{i}", bffKey: "not-the-key"));
        Assert.Equal(HttpStatusCode.TooManyRequests, wrong[^1]);
    }

    [Fact]
    public async Task The_bff_with_the_shared_key_gets_one_bucket_per_end_user()
    {
        var client = Limited().Anonymous();
        for (var i = 0; i < 8; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, await LoginAsync(client, bffIp: $"198.51.100.{i}", bffKey: BffKey));

        // The same end user is still limited.
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++) statuses.Add(await LoginAsync(client, bffIp: "203.0.113.50", bffKey: BffKey));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    [Fact]
    public async Task A_rate_limited_reply_says_how_long_to_wait_on_every_limiter()
    {
        var api = Limited(authPermits: 1, defaultPermits: 2);
        var client = api.Anonymous();

        await client.GetAsync("/v1/me", Ct);
        await client.GetAsync("/v1/me", Ct);
        var general = await client.GetAsync("/v1/me", Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, general.StatusCode);
        Assert.True(int.Parse(general.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture) >= 1);
        var body = await general.JsonAsync();
        Assert.Equal("RATE_LIMITED", body.GetProperty("code").GetString());
        Assert.True(body.GetProperty("params").GetProperty("retryAfterSeconds").GetInt32() >= 1);

        var auth = Limited(authPermits: 1);
        var authClient = auth.Anonymous();
        await LoginAsync(authClient);
        var limited = await authClient.PostAsJsonAsync("/v1/auth/login", new { email = "a@example.com", password = "x" }, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True((await limited.JsonAsync()).GetProperty("params").GetProperty("retryAfterSeconds").GetInt32() >= 1);
    }

    [Fact]
    public async Task Infants_cannot_outnumber_adults()
    {
        var api = new ApiFactory(database);
        factories.Add(api);
        var response = await api.Anonymous().PostAsJsonAsync("/v1/trips/search", new
        {
            from = "isl_3J4M7RAMSYA0GV00MYVA92KY3S", to = "isl_1NBQF0FDKZ3TM9NMN6T38ABW79",
            date = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"), time = "06:00", timeMode = "departAfter",
            passengers = new { adults = 1, children = 0, infants = 3 },
            preferences = new { riskTolerance = "balanced" },
        }, Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = (await response.JsonAsync()).GetProperty("errors").EnumerateArray()
            .Single(e => e.GetProperty("field").GetString() == "passengers.infants");
        Assert.Equal("INFANTS_EXCEED_ADULTS", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_idempotent_replay_replays_without_logging_an_error()
    {
        var api = new ApiFactory(database);
        factories.Add(api);
        var user = await api.RegisterAndLoginAsync();
        var client = api.As(user.Tokens.AccessToken);
        var key = $"replay-{Guid.NewGuid():N}";
        var before = api.Logs.Errors.Count; // startup migrations may log; only the replays are under test

        for (var i = 0; i < 3; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/v1/_test/echo") { Content = JsonContent.Create(new { n = 1 }) };
            request.Headers.Add("Idempotency-Key", key);
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request, Ct)).StatusCode);
        }

        Assert.Equal(1, api.Counter.Calls);
        Assert.Empty(api.Logs.Errors.Skip(before));
    }

    [Fact]
    public async Task Renewing_a_session_is_not_held_to_the_strict_sign_in_limit()
    {
        var client = Limited(authPermits: 1).Anonymous();
        for (var i = 0; i < 6; i++)
        {
            var response = await client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = $"rt_{Guid.NewGuid():N}" }, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
