using Dhathuru.Domain.Http;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Security;
using Dhathuru.Domain.Seams;
using Dhathuru.Identity.Email;
using Dhathuru.Identity.Entities;
using Dhathuru.Identity.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Dhathuru.Identity.Services;

/// <summary>
/// All account behaviour. Endpoints stay thin; every rule that matters for security lives here:
/// no enumeration, rotating refresh tokens with reuse detection, lockout, single-use emailed tokens.
/// </summary>
public sealed class AccountService(
    AccountsDbContext db,
    UserManager<AppUser> users,
    IPasswordHasher<AppUser> hasher,
    AccessTokenIssuer accessTokens,
    AccountEmails emails,
    IAuditLog audit,
    IMemoryCache cache,
    IEnumerable<IAccountDeletionHandler> deletionHandlers,
    IOptions<AuthOptions> authOptions,
    TimeProvider time)
{
    private readonly AuthOptions options = authOptions.Value;

    // A real hash of a random value, so "no such account" costs the same as "wrong password".
    private static readonly string DummyPassword = Secrets.NewToken();
    private string? dummyHash;

    private string DummyHash => dummyHash ??= hasher.HashPassword(new AppUser(), DummyPassword);

    // ---------- Registration and email verification ----------

    /// <summary>Always completes the same way for the caller; an existing address gets an "already registered" email instead.</summary>
    public async Task RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email!.Trim();
        var existing = await users.FindByEmailAsync(email);
        if (existing is null)
        {
            var user = new AppUser
            {
                Id = $"usr_{Ulid.NewString()}",
                UserName = email,
                Email = email,
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
                Locale = request.Locale ?? options.SupportedLocales[0],
                CreatedAt = time.GetUtcNow(),
            };
            var created = await users.CreateAsync(user, request.Password!);
            if (created.Succeeded)
            {
                await users.AddToRoleAsync(user, Roles.Passenger);
                var token = await IssueAccountTokenAsync(user.Id, AccountTokenPurpose.VerifyEmail, null, TimeSpan.FromHours(options.VerifyEmailTokenHours), ct);
                await emails.SendVerifyEmailAsync(user, token, ct);
                await audit.WriteAsync(new AuditEvent(AuditActions.Registered, user.Id, "user", user.Id), ct);
                return;
            }

            if (!created.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            {
                throw ProblemException.Validation(new FieldError("password", "PASSWORD_REJECTED"));
            }

            existing = await users.FindByEmailAsync(email); // lost a registration race
            if (existing is null) return;
        }

        hasher.HashPassword(existing, request.Password!); // same cost as the new-account branch
        if (TryCooldown("exists", existing.Id))
        {
            await emails.SendAccountExistsAsync(existing, ct);
        }
        await audit.WriteAsync(new AuditEvent(AuditActions.RegisterExisting, existing.Id, "user", existing.Id), ct);
    }

    /// <summary>Consumes a verify-email or change-email token.</summary>
    public async Task VerifyEmailAsync(string token, CancellationToken ct)
    {
        var record = await ConsumeTokenAsync(token, [AccountTokenPurpose.VerifyEmail, AccountTokenPurpose.ChangeEmail], ct)
                     ?? throw TokenInvalid();
        var user = await users.FindByIdAsync(record.UserId) ?? throw TokenInvalid();

        if (record.Purpose == AccountTokenPurpose.VerifyEmail)
        {
            user.EmailConfirmed = true;
            await users.UpdateAsync(user);
            await audit.WriteAsync(new AuditEvent(AuditActions.EmailVerified, user.Id, "user", user.Id), ct);
            return;
        }

        var newEmail = record.Payload ?? throw TokenInvalid();
        var oldEmail = user.Email!;
        if (await users.FindByEmailAsync(newEmail) is not null) throw TokenInvalid();
        user.Email = newEmail;
        user.UserName = newEmail;
        user.EmailConfirmed = true;
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded) throw TokenInvalid();
        await audit.WriteAsync(new AuditEvent(AuditActions.EmailChanged, user.Id, "user", user.Id), ct);
        await emails.SendEmailChangedAsync(oldEmail, user.Locale, user.DisplayName, ct);
    }

    public async Task ResendVerificationAsync(string email, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || user.EmailConfirmed || !TryCooldown("verify", user.Id)) return;
        await InvalidateTokensAsync(user.Id, AccountTokenPurpose.VerifyEmail, ct);
        var token = await IssueAccountTokenAsync(user.Id, AccountTokenPurpose.VerifyEmail, null, TimeSpan.FromHours(options.VerifyEmailTokenHours), ct);
        await emails.SendVerifyEmailAsync(user, token, ct);
    }

    // ---------- Login, refresh, logout ----------

    public async Task<TokenPair> LoginAsync(LoginRequest request, ClientInfo client, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email!.Trim());
        if (user is null)
        {
            hasher.VerifyHashedPassword(new AppUser(), DummyHash, request.Password!);
            await audit.WriteAsync(new AuditEvent(AuditActions.LoginFailed, null, Outcome: AuditOutcome.Failure), ct);
            throw InvalidCredentials();
        }

        if (await users.IsLockedOutAsync(user))
        {
            hasher.VerifyHashedPassword(user, DummyHash, request.Password!);
            await audit.WriteAsync(new AuditEvent(AuditActions.LoginFailed, user.Id, "user", user.Id, AuditOutcome.Denied,
                new Dictionary<string, string> { ["reason"] = "locked" }), ct);
            throw InvalidCredentials();
        }

        if (!await users.CheckPasswordAsync(user, request.Password!))
        {
            await users.AccessFailedAsync(user);
            var lockedNow = await users.IsLockedOutAsync(user);
            await audit.WriteAsync(new AuditEvent(lockedNow ? AuditActions.LockedOut : AuditActions.LoginFailed, user.Id, "user", user.Id, AuditOutcome.Failure), ct);
            throw InvalidCredentials();
        }

        await users.ResetAccessFailedCountAsync(user);
        var pair = await StartSessionAsync(user, client, ct);
        await audit.WriteAsync(new AuditEvent(AuditActions.LoginSucceeded, user.Id, "user", user.Id), ct);
        return pair;
    }

    public async Task<TokenPair> RefreshAsync(string refreshToken, ClientInfo client, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var hash = Secrets.Hash(refreshToken);
        var token = await db.RefreshTokens.Include(t => t.Session).FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
                    ?? throw InvalidRefreshToken();
        var session = token.Session!;

        if (session.RevokedAt is not null || session.ExpiresAt <= now || token.ExpiresAt <= now) throw InvalidRefreshToken();

        // Atomic single use: of two concurrent presentations only one gets rows == 1.
        var claimed = token.UsedAt is null
            && await db.RefreshTokens.Where(t => t.Id == token.Id && t.UsedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), ct) == 1;
        if (!claimed)
        {
            await EndSessionAsync(session.Id, "reuse_detected", ct);
            await audit.WriteAsync(new AuditEvent(AuditActions.RefreshReuseDetected, session.UserId, "session", session.Id, AuditOutcome.Denied), ct);
            throw InvalidRefreshToken();
        }

        var user = await users.FindByIdAsync(session.UserId);
        if (user is null)
        {
            await EndSessionAsync(session.Id, "user_missing", ct);
            throw InvalidRefreshToken();
        }

        var next = Secrets.NewToken("rt_");
        db.RefreshTokens.Add(new RefreshToken
        {
            SessionId = session.Id,
            TokenHash = Secrets.Hash(next),
            CreatedAt = now,
            ExpiresAt = Earliest(session.ExpiresAt, now.AddDays(options.RefreshIdleDays)),
        });
        await db.Sessions.Where(s => s.Id == session.Id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.LastUsedAt, now)
            .SetProperty(x => x.UserAgent, client.UserAgent ?? session.UserAgent)
            .SetProperty(x => x.NetworkHint, client.NetworkHint ?? session.NetworkHint), ct);
        await db.SaveChangesAsync(ct);

        var roles = await users.GetRolesAsync(user);
        return Pair(accessTokens.Issue(user, session.Id, roles), next);
    }

    public async Task LogoutAsync(string userId, string refreshToken, CancellationToken ct)
    {
        var hash = Secrets.Hash(refreshToken);
        var sessionId = await db.RefreshTokens.Where(t => t.TokenHash == hash && t.Session!.UserId == userId)
            .Select(t => t.SessionId).FirstOrDefaultAsync(ct);
        if (sessionId is null) return; // unknown or foreign token: nothing to do, and nothing to learn
        await EndSessionAsync(sessionId, "logout", ct);
        await audit.WriteAsync(new AuditEvent(AuditActions.Logout, userId, "session", sessionId), ct);
    }

    // ---------- Passwords ----------

    public async Task ForgotPasswordAsync(string email, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            hasher.HashPassword(new AppUser(), DummyPassword);
            return;
        }

        if (!TryCooldown("reset", user.Id)) return;
        await InvalidateTokensAsync(user.Id, AccountTokenPurpose.ResetPassword, ct);
        var token = await IssueAccountTokenAsync(user.Id, AccountTokenPurpose.ResetPassword, null, TimeSpan.FromMinutes(options.ResetPasswordTokenMinutes), ct);
        await emails.SendResetPasswordAsync(user, token, ct);
        await audit.WriteAsync(new AuditEvent(AuditActions.PasswordResetRequested, user.Id, "user", user.Id), ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        // Check everything that can be wrong with the new password BEFORE spending the single-use token.
        var peeked = await PeekTokenAsync(request.Token!, AccountTokenPurpose.ResetPassword, ct) ?? throw TokenInvalid();
        var user = await users.FindByIdAsync(peeked.UserId) ?? throw TokenInvalid();
        var policy = PasswordPolicy.Check(request.NewPassword, user.Email, options);
        if (policy.Count > 0)
        {
            throw ProblemException.Validation([.. policy.Select(c => new FieldError("newPassword", c))]);
        }

        _ = await ConsumeTokenAsync(request.Token!, [AccountTokenPurpose.ResetPassword], ct) ?? throw TokenInvalid();

        await SetPasswordAsync(user, request.NewPassword!);
        user.EmailConfirmed = true; // the reset link proves control of the mailbox
        await users.UpdateAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        await RevokeAllSessionsAsync(user.Id, null, "password_reset", ct);
        await audit.WriteAsync(new AuditEvent(AuditActions.PasswordReset, user.Id, "user", user.Id), ct);
        await emails.SendPasswordChangedAsync(user, ct);
    }

    public async Task ChangePasswordAsync(string userId, string? currentSessionId, ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await RequireUserAsync(userId);
        await RequirePasswordAsync(user, request.CurrentPassword!, ct);
        var policy = PasswordPolicy.Check(request.NewPassword, user.Email, options);
        if (policy.Count > 0) throw ProblemException.Validation([.. policy.Select(c => new FieldError("newPassword", c))]);

        await SetPasswordAsync(user, request.NewPassword!);
        await RevokeAllSessionsAsync(userId, currentSessionId, "password_changed", ct);
        await audit.WriteAsync(new AuditEvent(AuditActions.PasswordChanged, userId, "user", userId), ct);
        await emails.SendPasswordChangedAsync(user, ct);
    }

    // ---------- Profile ----------

    public async Task<UserDto> GetMeAsync(string userId) => ToDto(await RequireUserAsync(userId));

    public async Task<UserDto> UpdateMeAsync(string userId, UpdateMeRequest request, CancellationToken ct)
    {
        var user = await RequireUserAsync(userId);
        if (request.DisplayName is not null) user.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim();
        if (request.Locale is not null) user.Locale = request.Locale;
        await users.UpdateAsync(user);
        await audit.WriteAsync(new AuditEvent(AuditActions.ProfileUpdated, userId, "user", userId), ct);
        return ToDto(user);
    }

    public async Task<PreferencesDto> GetPreferencesAsync(string userId) => ToPreferences(await RequireUserAsync(userId));

    public async Task<PreferencesDto> UpdatePreferencesAsync(string userId, UpdatePreferencesRequest request, CancellationToken ct)
    {
        var user = await RequireUserAsync(userId);
        if (request.Language is not null) user.Locale = request.Language;
        if (request.RiskTolerance is not null) user.RiskTolerance = request.RiskTolerance;
        if (request.PreferredModes is not null) user.PreferredModes = [.. request.PreferredModes.Distinct()];
        await users.UpdateAsync(user);
        await audit.WriteAsync(new AuditEvent(AuditActions.ProfileUpdated, userId, "user", userId), ct);
        return ToPreferences(user);
    }

    /// <summary>Starts an email change. Always looks successful; the link goes to the NEW address.</summary>
    public async Task RequestEmailChangeAsync(string userId, ChangeEmailRequest request, CancellationToken ct)
    {
        var user = await RequireUserAsync(userId);
        await RequirePasswordAsync(user, request.Password!, ct);
        var newEmail = request.NewEmail!.Trim();
        if (string.Equals(newEmail, user.Email, StringComparison.OrdinalIgnoreCase)) return;
        if (await users.FindByEmailAsync(newEmail) is not null) return;
        if (!TryCooldown("change-email", user.Id)) return;

        await InvalidateTokensAsync(userId, AccountTokenPurpose.ChangeEmail, ct);
        var token = await IssueAccountTokenAsync(userId, AccountTokenPurpose.ChangeEmail, newEmail, TimeSpan.FromHours(options.ChangeEmailTokenHours), ct);
        await emails.SendConfirmNewEmailAsync(user, newEmail, token, ct);
        await audit.WriteAsync(new AuditEvent(AuditActions.EmailChangeRequested, userId, "user", userId), ct);
    }

    public async Task DeleteAccountAsync(string userId, DeleteAccountRequest request, CancellationToken ct)
    {
        var user = await RequireUserAsync(userId);
        await RequirePasswordAsync(user, request.Password!, ct);

        foreach (var handler in deletionHandlers)
        {
            await handler.OnAccountDeletingAsync(userId, ct);
        }

        var (email, locale, name) = (user.Email!, user.Locale, user.DisplayName);
        var result = await users.DeleteAsync(user); // sessions, tokens and travellers cascade
        if (!result.Succeeded) throw new InvalidOperationException("Account deletion failed.");
        await audit.WriteAsync(new AuditEvent(AuditActions.AccountDeleted, userId, "user", userId), ct);
        await emails.SendAccountDeletedAsync(email, locale, name, ct);
    }

    // ---------- Sessions ----------

    public async Task<IReadOnlyList<SessionDto>> ListSessionsAsync(string userId, string? currentSessionId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var rows = await db.Sessions.AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastUsedAt).ToListAsync(ct);
        return [.. rows.Select(s => new SessionDto(s.Id, s.CreatedAt, s.LastUsedAt, s.UserAgent, s.NetworkHint, s.Id == currentSessionId))];
    }

    public async Task RevokeSessionAsync(string userId, string sessionId, CancellationToken ct)
    {
        var owned = await db.Sessions.AnyAsync(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null, ct);
        if (!owned) throw ProblemException.NotFound();
        await EndSessionAsync(sessionId, "user_revoked", ct);
        await audit.WriteAsync(new AuditEvent(AuditActions.SessionRevoked, userId, "session", sessionId), ct);
    }

    public async Task RevokeOtherSessionsAsync(string userId, string? currentSessionId, CancellationToken ct)
    {
        await RevokeAllSessionsAsync(userId, currentSessionId, "user_revoked_all", ct);
        await audit.WriteAsync(new AuditEvent(AuditActions.SessionsRevokedAll, userId, "user", userId), ct);
    }

    /// <summary>Used by bearer validation: a revoked or expired session must stop working before the access token expires.</summary>
    public Task<bool> IsSessionActiveAsync(string userId, string sessionId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return db.Sessions.AnyAsync(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now, ct);
    }

    // ---------- Travellers ----------

    public async Task<IReadOnlyList<TravellerDto>> ListTravellersAsync(string userId, CancellationToken ct) =>
        [.. (await db.Travellers.AsNoTracking().Where(t => t.UserId == userId).OrderBy(t => t.CreatedAt).ToListAsync(ct)).Select(ToDto)];

    public async Task<TravellerDto> CreateTravellerAsync(string userId, TravellerInput input, CancellationToken ct)
    {
        if (await db.Travellers.CountAsync(t => t.UserId == userId, ct) >= options.MaxTravellersPerUser)
        {
            throw ProblemException.Conflict("TRAVELLER_LIMIT_REACHED", new Dictionary<string, object?> { ["max"] = options.MaxTravellersPerUser });
        }

        var now = time.GetUtcNow();
        var traveller = new Traveller { Id = $"trv_{Ulid.NewString()}", UserId = userId, CreatedAt = now, UpdatedAt = now };
        Apply(traveller, input);
        db.Travellers.Add(traveller);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEvent(AuditActions.TravellerCreated, userId, "traveller", traveller.Id), ct);
        return ToDto(traveller);
    }

    public async Task<TravellerDto> UpdateTravellerAsync(string userId, string travellerId, TravellerInput input, CancellationToken ct)
    {
        var traveller = await db.Travellers.FirstOrDefaultAsync(t => t.Id == travellerId && t.UserId == userId, ct)
                        ?? throw await DeniedAsNotFoundAsync(userId, "traveller", travellerId, ct);
        Apply(traveller, input);
        traveller.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEvent(AuditActions.TravellerUpdated, userId, "traveller", travellerId), ct);
        return ToDto(traveller);
    }

    public async Task DeleteTravellerAsync(string userId, string travellerId, CancellationToken ct)
    {
        var traveller = await db.Travellers.FirstOrDefaultAsync(t => t.Id == travellerId && t.UserId == userId, ct)
                        ?? throw await DeniedAsNotFoundAsync(userId, "traveller", travellerId, ct);
        db.Travellers.Remove(traveller);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEvent(AuditActions.TravellerDeleted, userId, "traveller", travellerId), ct);
    }

    // ---------- Internals ----------

    private async Task<ProblemException> DeniedAsNotFoundAsync(string userId, string type, string id, CancellationToken ct)
    {
        // Someone else's traveller looks exactly like a missing one; we only record that it happened when it was a real foreign id.
        if (await db.Travellers.AnyAsync(t => t.Id == id, ct))
        {
            await audit.WriteAsync(new AuditEvent(AuditActions.AccessDenied, userId, type, id, AuditOutcome.Denied), ct);
        }
        return ProblemException.NotFound();
    }

    private static void Apply(Traveller traveller, TravellerInput input)
    {
        traveller.FullName = input.FullName!.Trim();
        traveller.PassengerType = input.PassengerType!;
        traveller.DateOfBirth = input.DateOfBirth;
        traveller.Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();
        traveller.DocumentType = input.DocumentType ?? DocumentTypes.None;
    }

    private async Task<TokenPair> StartSessionAsync(AppUser user, ClientInfo client, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var session = new RefreshSession
        {
            Id = $"ses_{Ulid.NewString()}",
            UserId = user.Id,
            CreatedAt = now,
            LastUsedAt = now,
            ExpiresAt = now.AddDays(options.SessionDays),
            UserAgent = client.UserAgent,
            NetworkHint = client.NetworkHint,
        };
        var refresh = Secrets.NewToken("rt_");
        session.Tokens.Add(new RefreshToken
        {
            TokenHash = Secrets.Hash(refresh),
            CreatedAt = now,
            ExpiresAt = Earliest(session.ExpiresAt, now.AddDays(options.RefreshIdleDays)),
        });
        db.Sessions.Add(session);
        await db.SaveChangesAsync(ct);

        // Keep the list bounded: drop the least recently used sessions beyond the limit.
        var stale = await db.Sessions.Where(s => s.UserId == user.Id && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastUsedAt).Skip(options.MaxSessionsPerUser).Select(s => s.Id).ToListAsync(ct);
        foreach (var id in stale) await EndSessionAsync(id, "session_limit", ct);

        var roles = await users.GetRolesAsync(user);
        return Pair(accessTokens.Issue(user, session.Id, roles), refresh);
    }

    private TokenPair Pair(string access, string refresh) => new(access, refresh, "Bearer", accessTokens.ExpiresInSeconds);

    private Task EndSessionAsync(string sessionId, string reason, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return db.Sessions.Where(s => s.Id == sessionId && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now).SetProperty(x => x.RevokedReason, reason), ct);
    }

    private Task RevokeAllSessionsAsync(string userId, string? exceptSessionId, string reason, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return db.Sessions.Where(s => s.UserId == userId && s.RevokedAt == null && s.Id != exceptSessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now).SetProperty(x => x.RevokedReason, reason), ct);
    }

    private async Task<string> IssueAccountTokenAsync(string userId, string purpose, string? payload, TimeSpan lifetime, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var token = Secrets.NewToken();
        db.AccountTokens.Add(new AccountToken
        {
            UserId = userId,
            Purpose = purpose,
            TokenHash = Secrets.Hash(token),
            Payload = payload,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
        });
        await db.SaveChangesAsync(ct);
        return token;
    }

    private Task InvalidateTokensAsync(string userId, string purpose, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return db.AccountTokens.Where(t => t.UserId == userId && t.Purpose == purpose && t.ConsumedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumedAt, now), ct);
    }

    private async Task<AccountToken?> PeekTokenAsync(string token, string purpose, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var hash = Secrets.Hash(token);
        return await db.AccountTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose && t.ConsumedAt == null && t.ExpiresAt > now, ct);
    }

    /// <summary>Atomically marks a valid token as used and returns it; null when it is unknown, expired, used or of another purpose.</summary>
    private async Task<AccountToken?> ConsumeTokenAsync(string token, string[] purposes, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var hash = Secrets.Hash(token);
        var record = await db.AccountTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == hash && purposes.Contains(t.Purpose) && t.ConsumedAt == null && t.ExpiresAt > now, ct);
        if (record is null) return null;
        var rows = await db.AccountTokens.Where(t => t.Id == record.Id && t.ConsumedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumedAt, now), ct);
        return rows == 1 ? record : null;
    }

    private async Task SetPasswordAsync(AppUser user, string newPassword)
    {
        // Hash and store directly: the password policy was already enforced by PasswordPolicy, and Identity's
        // token providers are not needed because reset tokens are our own single-use account tokens.
        user.PasswordHash = hasher.HashPassword(user, newPassword);
        var result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded) throw ProblemException.Validation(new FieldError("newPassword", "PASSWORD_REJECTED"));
    }

    private async Task RequirePasswordAsync(AppUser user, string password, CancellationToken ct)
    {
        if (await users.IsLockedOutAsync(user)) throw new ProblemException(StatusCodes.Status403Forbidden, "PASSWORD_INCORRECT");
        if (await users.CheckPasswordAsync(user, password)) return;
        await users.AccessFailedAsync(user);
        await audit.WriteAsync(new AuditEvent(AuditActions.LoginFailed, user.Id, "user", user.Id, AuditOutcome.Failure,
            new Dictionary<string, string> { ["context"] = "reauthentication" }), ct);
        throw new ProblemException(StatusCodes.Status403Forbidden, "PASSWORD_INCORRECT");
    }

    private async Task<AppUser> RequireUserAsync(string userId) =>
        await users.FindByIdAsync(userId) ?? throw ProblemException.Unauthorized();

    /// <summary>One email of a kind per account per cooldown window (in-process; a shared store can replace it behind this method).</summary>
    private bool TryCooldown(string kind, string key)
    {
        if (options.EmailCooldownSeconds <= 0) return true;
        var cacheKey = $"email-cooldown:{kind}:{key}";
        if (cache.TryGetValue(cacheKey, out _)) return false;
        cache.Set(cacheKey, true, TimeSpan.FromSeconds(options.EmailCooldownSeconds));
        return true;
    }

    private static DateTimeOffset Earliest(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private static ProblemException InvalidCredentials() => new(StatusCodes.Status401Unauthorized, "INVALID_CREDENTIALS");

    private static ProblemException InvalidRefreshToken() => new(StatusCodes.Status401Unauthorized, "INVALID_REFRESH_TOKEN");

    private static ProblemException TokenInvalid() => ProblemException.Validation(new FieldError("token", "TOKEN_INVALID"));

    private static UserDto ToDto(AppUser u) => new(u.Id, u.Email!, u.EmailConfirmed, u.DisplayName, u.Locale, u.CreatedAt);

    private static PreferencesDto ToPreferences(AppUser u) => new(u.Locale, u.RiskTolerance, u.PreferredModes);

    private static TravellerDto ToDto(Traveller t) => new(t.Id, t.FullName, t.PassengerType, t.DateOfBirth, t.Phone, t.DocumentType);
}
