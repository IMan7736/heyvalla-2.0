using Dhathuru.Domain.Http;
using Dhathuru.Domain.Security;
using Dhathuru.Identity.Options;
using Dhathuru.Identity.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Dhathuru.Identity.Endpoints;

public static class IdentityEndpoints
{
    /// <summary>Maps /auth/* and /me/* onto the versioned group.</summary>
    public static RouteGroupBuilder MapIdentityEndpoints(this RouteGroupBuilder v1)
    {
        MapAuth(v1.MapGroup("/auth").WithTags("Auth"));
        MapAccount(v1.MapGroup("/me").WithTags("Account").RequireAuthorization(Policies.Authenticated));
        return v1;
    }

    private static void MapAuth(RouteGroupBuilder auth)
    {
        var anonymous = auth.MapGroup("").AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Auth);

        anonymous.MapPost("/register", async ([FromBody] RegisterRequest body, AccountService accounts, IOptions<AuthOptions> options, CancellationToken ct) =>
            {
                await ConstantTime.RunAsync(options.Value.ConstantTimeFloorMilliseconds, () => accounts.RegisterAsync(body, ct), ct);
                return Results.Accepted();
            })
            .WithName("register").Validate<RouteHandlerBuilder, RegisterRequest>();

        anonymous.MapPost("/login", async ([FromBody] LoginRequest body, AccountService accounts, IOptions<AuthOptions> options, HttpContext http, CancellationToken ct) =>
                Results.Ok(await ConstantTime.RunAsync(options.Value.ConstantTimeFloorMilliseconds,
                    () => accounts.LoginAsync(body, ClientInfo.From(http), ct), ct)))
            .WithName("login").Validate<RouteHandlerBuilder, LoginRequest>();

        auth.MapPost("/refresh", async ([FromBody] RefreshRequest body, AccountService accounts, HttpContext http, CancellationToken ct) =>
                Results.Ok(await accounts.RefreshAsync(body.RefreshToken!, ClientInfo.From(http), ct)))
            // Refresh tokens are 256 random bits, so guessing is not the risk the strict auth limit guards against. Every
            // page load after a quarter hour renews, and many travellers share one address (resort Wi-Fi, mobile carriers).
            .WithName("refreshTokens").AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Default).Validate<RouteHandlerBuilder, RefreshRequest>();

        anonymous.MapPost("/verify-email", async ([FromBody] TokenRequest body, AccountService accounts, CancellationToken ct) =>
            {
                await accounts.VerifyEmailAsync(body.Token!, ct);
                return Results.NoContent();
            })
            .WithName("verifyEmail").Validate<RouteHandlerBuilder, TokenRequest>();

        anonymous.MapPost("/resend-verification", async ([FromBody] EmailRequest body, AccountService accounts, IOptions<AuthOptions> options, CancellationToken ct) =>
            {
                await ConstantTime.RunAsync(options.Value.ConstantTimeFloorMilliseconds, () => accounts.ResendVerificationAsync(body.Email!, ct), ct);
                return Results.Accepted();
            })
            .WithName("resendVerification").Validate<RouteHandlerBuilder, EmailRequest>();

        anonymous.MapPost("/forgot-password", async ([FromBody] EmailRequest body, AccountService accounts, IOptions<AuthOptions> options, CancellationToken ct) =>
            {
                await ConstantTime.RunAsync(options.Value.ConstantTimeFloorMilliseconds, () => accounts.ForgotPasswordAsync(body.Email!, ct), ct);
                return Results.Accepted();
            })
            .WithName("forgotPassword").Validate<RouteHandlerBuilder, EmailRequest>();

        anonymous.MapPost("/reset-password", async ([FromBody] ResetPasswordRequest body, AccountService accounts, CancellationToken ct) =>
            {
                await accounts.ResetPasswordAsync(body, ct);
                return Results.NoContent();
            })
            .WithName("resetPassword").Validate<RouteHandlerBuilder, ResetPasswordRequest>();

        auth.MapPost("/logout", async ([FromBody] RefreshRequest body, AccountService accounts, ICurrentUser user, CancellationToken ct) =>
            {
                await accounts.LogoutAsync(user.UserId, body.RefreshToken!, ct);
                return Results.NoContent();
            })
            .WithName("logout").RequireAuthorization(Policies.Authenticated).Validate<RouteHandlerBuilder, RefreshRequest>();
    }

    private static void MapAccount(RouteGroupBuilder me)
    {
        me.MapGet("", async (AccountService accounts, ICurrentUser user) => Results.Ok(await accounts.GetMeAsync(user.UserId)))
            .WithName("getMe");

        me.MapPatch("", async ([FromBody] UpdateMeRequest body, AccountService accounts, ICurrentUser user, CancellationToken ct) =>
                Results.Ok(await accounts.UpdateMeAsync(user.UserId, body, ct)))
            .WithName("updateMe").Validate<RouteHandlerBuilder, UpdateMeRequest>();

        me.MapDelete("", async ([FromBody] DeleteAccountRequest body, AccountService accounts, ICurrentUser user, CancellationToken ct) =>
            {
                await accounts.DeleteAccountAsync(user.UserId, body, ct);
                return Results.NoContent();
            })
            .WithName("deleteMe").RequireRateLimiting(RateLimitPolicies.Auth).Validate<RouteHandlerBuilder, DeleteAccountRequest>();

        me.MapPost("/password", async ([FromBody] ChangePasswordRequest body, AccountService accounts, ICurrentUser user, CancellationToken ct) =>
            {
                await accounts.ChangePasswordAsync(user.UserId, user.SessionId, body, ct);
                return Results.NoContent();
            })
            .WithName("changePassword").RequireRateLimiting(RateLimitPolicies.Auth).Validate<RouteHandlerBuilder, ChangePasswordRequest>();

        me.MapPost("/email", async ([FromBody] ChangeEmailRequest body, AccountService accounts, ICurrentUser user, CancellationToken ct) =>
            {
                await accounts.RequestEmailChangeAsync(user.UserId, body, ct);
                return Results.Accepted();
            })
            .WithName("changeEmail").RequireRateLimiting(RateLimitPolicies.Auth).Validate<RouteHandlerBuilder, ChangeEmailRequest>();

        me.MapGet("/preferences", async (AccountService accounts, ICurrentUser user) => Results.Ok(await accounts.GetPreferencesAsync(user.UserId)))
            .WithName("getPreferences");

        me.MapPatch("/preferences", async ([FromBody] UpdatePreferencesRequest body, AccountService accounts, ICurrentUser user, CancellationToken ct) =>
                Results.Ok(await accounts.UpdatePreferencesAsync(user.UserId, body, ct)))
            .WithName("updatePreferences").Validate<RouteHandlerBuilder, UpdatePreferencesRequest>();

        me.MapGet("/sessions", async (AccountService accounts, ICurrentUser user, CancellationToken ct) =>
                Results.Ok(new { items = await accounts.ListSessionsAsync(user.UserId, user.SessionId, ct) }))
            .WithName("listSessions");

        me.MapDelete("/sessions/{sessionId}", async (string sessionId, AccountService accounts, ICurrentUser user, CancellationToken ct) =>
            {
                await accounts.RevokeSessionAsync(user.UserId, sessionId, ct);
                return Results.NoContent();
            })
            .WithName("revokeSession");

        me.MapPost("/sessions/revoke-others", async (AccountService accounts, ICurrentUser user, CancellationToken ct) =>
            {
                await accounts.RevokeOtherSessionsAsync(user.UserId, user.SessionId, ct);
                return Results.NoContent();
            })
            .WithName("revokeOtherSessions");

        var travellers = me.MapGroup("/travellers");
        travellers.MapGet("", async (AccountService accounts, ICurrentUser user, CancellationToken ct) =>
                Results.Ok(new { items = await accounts.ListTravellersAsync(user.UserId, ct) }))
            .WithName("listTravellers");

        travellers.MapPost("", async ([FromBody] TravellerInput body, AccountService accounts, ICurrentUser user, CancellationToken ct) =>
            {
                var created = await accounts.CreateTravellerAsync(user.UserId, body, ct);
                return Results.Created($"/v1/me/travellers/{created.Id}", created);
            })
            .WithName("createTraveller").Validate<RouteHandlerBuilder, TravellerInput>();

        travellers.MapPatch("/{travellerId}", async (string travellerId, [FromBody] TravellerInput body, AccountService accounts, ICurrentUser user, CancellationToken ct) =>
                Results.Ok(await accounts.UpdateTravellerAsync(user.UserId, travellerId, body, ct)))
            .WithName("updateTraveller").Validate<RouteHandlerBuilder, TravellerInput>();

        travellers.MapDelete("/{travellerId}", async (string travellerId, AccountService accounts, ICurrentUser user, CancellationToken ct) =>
            {
                await accounts.DeleteTravellerAsync(user.UserId, travellerId, ct);
                return Results.NoContent();
            })
            .WithName("deleteTraveller");
    }
}
