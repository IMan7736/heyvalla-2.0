using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Dhathuru.Api.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Dhathuru.Api.Tests;

public sealed class SecurityTests : IDisposable
{
    private readonly ApiFactory api;

    public SecurityTests(PostgresFixture database) => api = new ApiFactory(database);

    public void Dispose() => api.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static object Traveller(string name = "Aishath Naseem") => new { fullName = name, passengerType = "adult", phone = "+9607771234", documentType = "national_id" };

    [Fact]
    public async Task Travellers_are_private_to_their_owner_and_foreign_ids_look_missing()
    {
        var alice = await api.RegisterAndLoginAsync();
        var mallory = await api.RegisterAndLoginAsync();
        var aliceClient = api.As(alice.Tokens.AccessToken);
        var malloryClient = api.As(mallory.Tokens.AccessToken);

        var created = await aliceClient.PostAsJsonAsync("/v1/me/travellers", Traveller(), Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.JsonAsync()).GetProperty("id").GetString()!;
        Assert.StartsWith("trv_", id, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NotFound, (await malloryClient.PatchAsJsonAsync($"/v1/me/travellers/{id}", Traveller("Hijacked"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await malloryClient.DeleteAsync($"/v1/me/travellers/{id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await malloryClient.DeleteAsync("/v1/me/travellers/trv_00000000000000000000000000", Ct)).StatusCode);
        Assert.Equal(0, (await (await malloryClient.GetAsync("/v1/me/travellers", Ct)).JsonAsync()).GetProperty("items").GetArrayLength());

        var mine = await (await aliceClient.GetAsync("/v1/me/travellers", Ct)).JsonAsync();
        Assert.Equal("Aishath Naseem", mine.GetProperty("items")[0].GetProperty("fullName").GetString());
        Assert.False(mine.GetProperty("items")[0].TryGetProperty("documentNumber", out _)); // numbers are never stored
    }

    [Fact]
    public async Task Traveller_input_is_validated_and_limited()
    {
        var user = await api.RegisterAndLoginAsync();
        var client = api.As(user.Tokens.AccessToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/v1/me/travellers", new { fullName = "", passengerType = "adult" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/v1/me/travellers", new { fullName = "X", passengerType = "robot" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/v1/me/travellers", new { fullName = "X", passengerType = "adult", phone = "call me" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/v1/me/travellers", new { fullName = "X", passengerType = "adult", dateOfBirth = "2999-01-01" }, Ct)).StatusCode);

        using var small = new ApiFactory(api.Database, new() { ["Auth:MaxTravellersPerUser"] = "2" });
        var limited = await small.RegisterAndLoginAsync();
        var c = small.As(limited.Tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/v1/me/travellers", Traveller("One"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/v1/me/travellers", Traveller("Two"), Ct)).StatusCode);
        var third = await c.PostAsJsonAsync("/v1/me/travellers", Traveller("Three"), Ct);
        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
        Assert.Equal("TRAVELLER_LIMIT_REACHED", await third.CodeAsync());
    }

    [Fact]
    public async Task Protected_endpoints_answer_401_problem_json_without_a_token_and_with_a_bad_one()
    {
        foreach (var path in new[] { "/v1/me", "/v1/me/travellers", "/v1/me/sessions", "/v1/me/preferences" })
        {
            var response = await api.Anonymous().GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("AUTH_REQUIRED", await response.CodeAsync());
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.As("not.a.jwt").GetAsync("/v1/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task Tokens_signed_with_another_key_or_with_alg_none_are_rejected()
    {
        var user = await api.RegisterAndLoginAsync();
        var real = new JwtSecurityTokenHandler().ReadJwtToken(user.Tokens.AccessToken);

        using var rogue = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var forged = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            real.Issuer, real.Audiences.First(), real.Claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(new ECDsaSecurityKey(rogue), SecurityAlgorithms.EcdsaSha256)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.As(forged).GetAsync("/v1/me", Ct)).StatusCode);

        var none = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            new JwtHeader(), new JwtPayload(real.Issuer, real.Audiences.First(), real.Claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5))));
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.As(none).GetAsync("/v1/me", Ct)).StatusCode);

        var parts = user.Tokens.AccessToken.Split('.');
        var tampered = $"{parts[0]}.{parts[1][..^2]}AA.{parts[2]}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.As(tampered).GetAsync("/v1/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task Access_tokens_carry_the_expected_claims_and_no_personal_data_beyond_the_email_bookings_needs()
    {
        var user = await api.RegisterAndLoginAsync();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(user.Tokens.AccessToken);
        Assert.Equal("ES256", jwt.Header.Alg);
        Assert.Equal(user.Id, jwt.Subject);
        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "passenger");
        // Bookings addresses confirmations from the email claim. The token only travels server to server (httpOnly cookie);
        // nothing else personal may be added: no display name, phone or travel document.
        Assert.Contains(jwt.Claims, c => c.Type == "email" && c.Value == user.Email);
        Assert.DoesNotContain(jwt.Claims, c => c.Type != "email" && c.Value.Contains(user.Email, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(jwt.Claims, c => c.Type is "name" or "given_name" or "family_name" or "phone_number");
        Assert.True(jwt.ValidTo - jwt.ValidFrom <= TimeSpan.FromMinutes(16));
    }

    [Fact]
    public async Task Policies_gate_on_verified_email_and_roles()
    {
        var unverified = await api.RegisterAndLoginAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await api.As(unverified.Tokens.AccessToken).GetAsync("/v1/_test/verified", Ct)).StatusCode);

        var verified = await api.RegisterAndLoginAsync(verify: true);
        Assert.Equal(HttpStatusCode.OK, (await api.As(verified.Tokens.AccessToken).GetAsync("/v1/_test/verified", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.As(verified.Tokens.AccessToken).GetAsync("/v1/_test/admin", Ct)).StatusCode);
    }

    [Fact]
    public async Task Responses_carry_security_headers_a_correlation_id_and_no_cache()
    {
        var response = await api.Anonymous().GetAsync("/v1/health/live", Ct);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-store", response.Headers.GetValues("Cache-Control").Single());
        Assert.Equal(32, response.Headers.GetValues("X-Correlation-Id").Single().Length);

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/nope");
        request.Headers.Add("X-Correlation-Id", "trace-me-12345");
        var echoed = await api.Anonymous().SendAsync(request, Ct);
        Assert.Equal("trace-me-12345", echoed.Headers.GetValues("X-Correlation-Id").Single());
        Assert.Equal("trace-me-12345", (await echoed.JsonAsync()).GetProperty("correlationId").GetString());

        var hostile = new HttpRequestMessage(HttpMethod.Get, "/v1/health/live");
        hostile.Headers.Add("X-Correlation-Id", "bad id\twith spaces");
        Assert.NotEqual("bad id\twith spaces", (await api.Anonymous().SendAsync(hostile, Ct)).Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task Unhandled_exceptions_become_a_generic_500_without_details()
    {
        var response = await api.Anonymous().GetAsync("/v1/_test/boom", Ct);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("secret detail", text, StringComparison.Ordinal);
        Assert.Contains("INTERNAL_ERROR", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logs_never_contain_emails_passwords_or_tokens()
    {
        var email = ApiFactoryExtensions.NewEmail();
        const string password = "my very secret passphrase 42";
        await api.Anonymous().PostAsJsonAsync("/v1/auth/register", new { email, password }, Ct);
        await api.Anonymous().PostAsJsonAsync("/v1/auth/login", new { email, password = "wrong password here" }, Ct);
        var tokens = await api.LoginAsync(email, password);
        await api.As(tokens.AccessToken).GetAsync("/v1/me", Ct);
        await api.Anonymous().PostAsJsonAsync("/v1/auth/forgot-password", new { email }, Ct);
        await api.Anonymous().PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = tokens.RefreshToken }, Ct);

        var everything = string.Join('\n', api.Logs.Lines);
        Assert.NotEmpty(api.Logs.Lines);
        Assert.DoesNotContain(email, everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(password, everything, StringComparison.Ordinal);
        Assert.DoesNotContain(tokens.RefreshToken, everything, StringComparison.Ordinal);
        Assert.DoesNotContain(tokens.AccessToken, everything, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Security_events_are_audited_without_secrets()
    {
        var user = await api.RegisterAndLoginAsync();
        await api.Anonymous().PostAsJsonAsync("/v1/auth/login", new { email = user.Email, password = "wrong wrong wrong" }, Ct);
        var other = await api.RegisterAndLoginAsync();
        var created = await api.As(user.Tokens.AccessToken).PostAsJsonAsync("/v1/me/travellers", Traveller(), Ct);
        var id = (await created.JsonAsync()).GetProperty("id").GetString();
        await api.As(other.Tokens.AccessToken).DeleteAsync($"/v1/me/travellers/{id}", Ct);

        await using var db = await api.Services.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Dhathuru.Api.Persistence.PlatformDbContext>>().CreateDbContextAsync(Ct);
        var entries = db.AuditLog.Where(a => a.ActorUserId == user.Id || a.ActorUserId == other.Id).ToList();
        Assert.Contains(entries, a => a.Action == "auth.registered" && a.ActorUserId == user.Id);
        Assert.Contains(entries, a => a.Action == "auth.login_succeeded" && a.ActorUserId == user.Id);
        Assert.Contains(entries, a => a.Action == "auth.login_failed" && a.ActorUserId == user.Id && a.Outcome == "failure");
        Assert.Contains(entries, a => a.Action == "access.denied" && a.ActorUserId == other.Id && a.SubjectId == id);
        Assert.All(entries, a =>
        {
            Assert.DoesNotContain(user.Email, a.DataJson ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.True(a.NetworkHint is null || a.NetworkHint.EndsWith("/24", StringComparison.Ordinal) || a.NetworkHint.EndsWith("/48", StringComparison.Ordinal));
        });
    }
}

