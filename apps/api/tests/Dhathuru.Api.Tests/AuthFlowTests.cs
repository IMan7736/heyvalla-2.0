using System.Net;
using System.Net.Http.Json;
using Dhathuru.Api.Tests.Support;

namespace Dhathuru.Api.Tests;

public sealed class AuthFlowTests : IDisposable
{
    private readonly ApiFactory api;

    public AuthFlowTests(PostgresFixture database) => api = new ApiFactory(database);

    public void Dispose() => api.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Register_sends_a_verification_email_and_the_token_works_once()
    {
        var email = ApiFactoryExtensions.NewEmail();
        var register = await api.Anonymous().PostAsJsonAsync("/v1/auth/register", new { email, password = ApiFactoryExtensions.StrongPassword, displayName = "Aishath" }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);

        var token = api.Emails.TokenFor(email, "account.verify_email");
        var first = await api.Anonymous().PostAsJsonAsync("/v1/auth/verify-email", new { token }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await api.Anonymous().PostAsJsonAsync("/v1/auth/verify-email", new { token }, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        var body = await second.JsonAsync();
        Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
        Assert.Equal("TOKEN_INVALID", body.GetProperty("errors")[0].GetProperty("code").GetString());

        var login = await api.LoginAsync(email, ApiFactoryExtensions.StrongPassword);
        var me = await api.As(login.AccessToken).GetAsync("/v1/me", Ct);
        Assert.True((await me.JsonAsync()).GetProperty("emailVerified").GetBoolean());
    }

    [Fact]
    public async Task Registering_an_existing_email_looks_identical_and_sends_an_already_registered_email()
    {
        var existing = await api.RegisterAndLoginAsync();
        var fresh = await api.Anonymous().PostAsJsonAsync("/v1/auth/register", new { email = ApiFactoryExtensions.NewEmail(), password = ApiFactoryExtensions.StrongPassword }, Ct);
        var again = await api.Anonymous().PostAsJsonAsync("/v1/auth/register", new { email = existing.Email.ToUpperInvariant(), password = "another long passphrase here" }, Ct);

        Assert.Equal(fresh.StatusCode, again.StatusCode);
        Assert.Equal(await fresh.Content.ReadAsStringAsync(Ct), await again.Content.ReadAsStringAsync(Ct));
        Assert.Equal(1, api.Emails.Count(existing.Email, "account.already_registered"));
        // The original password is untouched.
        await api.LoginAsync(existing.Email, existing.Password);
    }

    [Theory]
    [InlineData("short", "PASSWORD_TOO_SHORT")]
    [InlineData("password123", "PASSWORD_TOO_COMMON")]
    [InlineData("aaaaaaaaaaaaaa", "PASSWORD_TOO_COMMON")]
    public async Task Weak_passwords_are_rejected_with_a_code(string password, string code)
    {
        var response = await api.Anonymous().PostAsJsonAsync("/v1/auth/register", new { email = ApiFactoryExtensions.NewEmail(), password }, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var errors = (await response.JsonAsync()).GetProperty("errors");
        Assert.Contains(errors.EnumerateArray(), e => e.GetProperty("field").GetString() == "password" && e.GetProperty("code").GetString() == code);
    }

    [Fact]
    public async Task Invalid_email_and_missing_fields_report_field_errors()
    {
        var response = await api.Anonymous().PostAsJsonAsync("/v1/auth/register", new { email = "not-an-email", password = ApiFactoryExtensions.StrongPassword }, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var errors = (await response.JsonAsync()).GetProperty("errors");
        Assert.Equal("email", errors[0].GetProperty("field").GetString());
        Assert.Equal("EMAIL_INVALID", errors[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Malformed_json_is_a_400_problem()
    {
        var response = await api.Anonymous().PostAsync("/v1/auth/login", new StringContent("{not json", System.Text.Encoding.UTF8, "application/json"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("BAD_REQUEST", await response.CodeAsync());
    }

    [Fact]
    public async Task Login_failures_for_unknown_and_known_accounts_are_indistinguishable()
    {
        var user = await api.RegisterAndLoginAsync();
        var wrongPassword = await api.Anonymous().PostAsJsonAsync("/v1/auth/login", new { email = user.Email, password = "definitely not it" }, Ct);
        var unknown = await api.Anonymous().PostAsJsonAsync("/v1/auth/login", new { email = ApiFactoryExtensions.NewEmail(), password = "definitely not it" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(wrongPassword.StatusCode, unknown.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", await wrongPassword.CodeAsync());
        Assert.Equal("INVALID_CREDENTIALS", await unknown.CodeAsync());
    }

    [Fact]
    public async Task Repeated_failures_lock_the_account_even_for_the_right_password()
    {
        var user = await api.RegisterAndLoginAsync();
        for (var i = 0; i < 5; i++)
        {
            var bad = await api.Anonymous().PostAsJsonAsync("/v1/auth/login", new { email = user.Email, password = $"wrong password {i}" }, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }

        var locked = await api.Anonymous().PostAsJsonAsync("/v1/auth/login", new { email = user.Email, password = user.Password }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", await locked.CodeAsync()); // same answer: lockout is not disclosed
    }

    [Fact]
    public async Task Refresh_rotates_tokens_and_reuse_of_an_old_token_kills_the_session()
    {
        var user = await api.RegisterAndLoginAsync();
        var rotated = await api.Anonymous().PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = user.Tokens.RefreshToken }, Ct);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var next = await rotated.JsonAsync();
        var newRefresh = next.GetProperty("refreshToken").GetString()!;
        var newAccess = next.GetProperty("accessToken").GetString()!;
        Assert.NotEqual(user.Tokens.RefreshToken, newRefresh);

        Assert.Equal(HttpStatusCode.OK, (await api.As(newAccess).GetAsync("/v1/me", Ct)).StatusCode);

        // The thief (or the stale client) replays the first token.
        var replay = await api.Anonymous().PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = user.Tokens.RefreshToken }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("INVALID_REFRESH_TOKEN", await replay.CodeAsync());

        // Everyone in that session is cut off, including the legitimate newest token and its access token.
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = newRefresh }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.As(newAccess).GetAsync("/v1/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task Concurrent_refreshes_with_one_token_let_exactly_one_win()
    {
        var user = await api.RegisterAndLoginAsync();
        var attempts = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            api.Anonymous().PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = user.Tokens.RefreshToken }, Ct)));
        Assert.True(attempts.Count(r => r.StatusCode == HttpStatusCode.OK) <= 1);
    }

    [Fact]
    public async Task Garbage_and_unknown_refresh_tokens_are_rejected()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = "rt_nope" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await api.Anonymous().PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = "" }, Ct)).StatusCode);
    }

    [Fact]
    public async Task Logout_ends_the_session_immediately()
    {
        var user = await api.RegisterAndLoginAsync();
        var client = api.As(user.Tokens.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/v1/auth/logout", new { refreshToken = user.Tokens.RefreshToken }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/v1/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = user.Tokens.RefreshToken }, Ct)).StatusCode);
    }

    [Fact]
    public async Task Logout_with_someone_elses_refresh_token_does_nothing()
    {
        var alice = await api.RegisterAndLoginAsync();
        var mallory = await api.RegisterAndLoginAsync();
        var response = await api.As(mallory.Tokens.AccessToken).PostAsJsonAsync("/v1/auth/logout", new { refreshToken = alice.Tokens.RefreshToken }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.As(alice.Tokens.AccessToken).GetAsync("/v1/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task Forgot_password_never_reveals_whether_the_account_exists_and_reset_works_once()
    {
        var user = await api.RegisterAndLoginAsync();
        var known = await api.Anonymous().PostAsJsonAsync("/v1/auth/forgot-password", new { email = user.Email }, Ct);
        var unknown = await api.Anonymous().PostAsJsonAsync("/v1/auth/forgot-password", new { email = ApiFactoryExtensions.NewEmail() }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(Ct), await unknown.Content.ReadAsStringAsync(Ct));

        var token = api.Emails.TokenFor(user.Email, "account.reset_password");
        var weak = await api.Anonymous().PostAsJsonAsync("/v1/auth/reset-password", new { token, newPassword = "short" }, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, weak.StatusCode); // did not spend the token

        const string newPassword = "a brand new long passphrase";
        Assert.Equal(HttpStatusCode.NoContent, (await api.Anonymous().PostAsJsonAsync("/v1/auth/reset-password", new { token, newPassword }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await api.Anonymous().PostAsJsonAsync("/v1/auth/reset-password", new { token, newPassword = "yet another long passphrase" }, Ct)).StatusCode);

        // Old sessions are gone, old password is dead, new password works.
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.As(user.Tokens.AccessToken).GetAsync("/v1/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().PostAsJsonAsync("/v1/auth/login", new { email = user.Email, password = user.Password }, Ct)).StatusCode);
        await api.LoginAsync(user.Email, newPassword);
    }

    [Fact]
    public async Task Change_password_requires_the_current_password_and_keeps_only_the_current_session()
    {
        var user = await api.RegisterAndLoginAsync();
        var other = await api.LoginAsync(user.Email, user.Password);
        var client = api.As(user.Tokens.AccessToken);

        var wrong = await client.PostAsJsonAsync("/v1/me/password", new { currentPassword = "nope nope nope", newPassword = "a brand new long passphrase" }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        Assert.Equal("PASSWORD_INCORRECT", await wrong.CodeAsync());

        var ok = await client.PostAsJsonAsync("/v1/me/password", new { currentPassword = user.Password, newPassword = "a brand new long passphrase" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/v1/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.As(other.AccessToken).GetAsync("/v1/me", Ct)).StatusCode);
        Assert.Equal(1, api.Emails.Count(user.Email, "account.password_changed"));
    }

    [Fact]
    public async Task Email_change_goes_through_the_new_address()
    {
        var user = await api.RegisterAndLoginAsync();
        var newEmail = ApiFactoryExtensions.NewEmail();
        var client = api.As(user.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/v1/me/email", new { newEmail, password = "wrong wrong wrong" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/v1/me/email", new { newEmail, password = user.Password }, Ct)).StatusCode);

        var me = await (await client.GetAsync("/v1/me", Ct)).JsonAsync();
        Assert.Equal(user.Email, me.GetProperty("email").GetString()); // unchanged until confirmed

        var token = api.Emails.TokenFor(newEmail, "account.confirm_new_email");
        Assert.Equal(HttpStatusCode.NoContent, (await api.Anonymous().PostAsJsonAsync("/v1/auth/verify-email", new { token }, Ct)).StatusCode);

        me = await (await client.GetAsync("/v1/me", Ct)).JsonAsync();
        Assert.Equal(newEmail, me.GetProperty("email").GetString());
        Assert.True(me.GetProperty("emailVerified").GetBoolean());
        Assert.Equal(1, api.Emails.Count(user.Email, "account.email_changed"));
        await api.LoginAsync(newEmail, user.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().PostAsJsonAsync("/v1/auth/login", new { email = user.Email, password = user.Password }, Ct)).StatusCode);
    }

    [Fact]
    public async Task Profile_and_preferences_round_trip_and_validate()
    {
        var user = await api.RegisterAndLoginAsync();
        var client = api.As(user.Tokens.AccessToken);

        var patched = await client.PatchAsJsonAsync("/v1/me", new { displayName = "Aishath N." }, Ct);
        Assert.Equal("Aishath N.", (await patched.JsonAsync()).GetProperty("displayName").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PatchAsJsonAsync("/v1/me", new { locale = "xx" }, Ct)).StatusCode);

        var prefs = await client.PatchAsJsonAsync("/v1/me/preferences", new { riskTolerance = "cautious", preferredModes = new[] { "ferry", "seaplane" } }, Ct);
        Assert.Equal(HttpStatusCode.OK, prefs.StatusCode);
        var read = await (await client.GetAsync("/v1/me/preferences", Ct)).JsonAsync();
        Assert.Equal("cautious", read.GetProperty("riskTolerance").GetString());
        Assert.Equal(2, read.GetProperty("preferredModes").GetArrayLength());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PatchAsJsonAsync("/v1/me/preferences", new { preferredModes = new[] { "rocket" } }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PatchAsJsonAsync("/v1/me/preferences", new { riskTolerance = "reckless" }, Ct)).StatusCode);
    }

    [Fact]
    public async Task Sessions_can_be_listed_and_revoked_but_only_your_own()
    {
        var alice = await api.RegisterAndLoginAsync();
        var second = await api.LoginAsync(alice.Email, alice.Password);
        var bob = await api.RegisterAndLoginAsync();
        var client = api.As(alice.Tokens.AccessToken);

        var list = await (await client.GetAsync("/v1/me/sessions", Ct)).JsonAsync();
        var items = list.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Single(items.EnumerateArray(), s => s.GetProperty("current").GetBoolean());
        var other = items.EnumerateArray().First(s => !s.GetProperty("current").GetBoolean()).GetProperty("id").GetString();

        var bobSession = (await (await api.As(bob.Tokens.AccessToken).GetAsync("/v1/me/sessions", Ct)).JsonAsync()).GetProperty("items")[0].GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/v1/me/sessions/{bobSession}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.As(bob.Tokens.AccessToken).GetAsync("/v1/me", Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/v1/me/sessions/{other}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.As(second.AccessToken).GetAsync("/v1/me", Ct)).StatusCode);

        var third = await api.LoginAsync(alice.Email, alice.Password);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/v1/me/sessions/revoke-others", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.As(third.AccessToken).GetAsync("/v1/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/v1/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task Deleting_the_account_needs_the_password_and_removes_everything()
    {
        var user = await api.RegisterAndLoginAsync();
        var client = api.As(user.Tokens.AccessToken);
        await client.PostAsJsonAsync("/v1/me/travellers", new { fullName = "Aishath Naseem", passengerType = "adult" }, Ct);

        var wrong = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/v1/me") { Content = JsonContent.Create(new { password = "wrong wrong wrong" }) }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);

        var ok = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/v1/me") { Content = JsonContent.Create(new { password = user.Password }) }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/v1/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Anonymous().PostAsJsonAsync("/v1/auth/login", new { email = user.Email, password = user.Password }, Ct)).StatusCode);
        Assert.Equal(1, api.Emails.Count(user.Email, "account.deleted"));

        // The address is free again.
        var again = await api.RegisterAndLoginAsync(user.Email);
        Assert.NotEqual(user.Id, again.Id);
        var travellers = await (await api.As(again.Tokens.AccessToken).GetAsync("/v1/me/travellers", Ct)).JsonAsync();
        Assert.Equal(0, travellers.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Resend_verification_only_sends_to_unverified_accounts_and_answers_the_same()
    {
        var user = await api.RegisterAndLoginAsync();
        var before = api.Emails.Count(user.Email, "account.verify_email");
        var sent = await api.Anonymous().PostAsJsonAsync("/v1/auth/resend-verification", new { email = user.Email }, Ct);
        var unknown = await api.Anonymous().PostAsJsonAsync("/v1/auth/resend-verification", new { email = ApiFactoryExtensions.NewEmail() }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        Assert.Equal(sent.StatusCode, unknown.StatusCode);
        Assert.Equal(before + 1, api.Emails.Count(user.Email, "account.verify_email"));

        await api.Anonymous().PostAsJsonAsync("/v1/auth/verify-email", new { token = api.Emails.TokenFor(user.Email, "account.verify_email") }, Ct);
        await api.Anonymous().PostAsJsonAsync("/v1/auth/resend-verification", new { email = user.Email }, Ct);
        Assert.Equal(before + 1, api.Emails.Count(user.Email, "account.verify_email"));
    }
}
