namespace Dhathuru.Identity.Options;

/// <summary>Everything tunable about accounts. Safe defaults; override under the <c>Auth</c> configuration section.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string Issuer { get; set; } = "https://api.dhathuru.mv";

    public string Audience { get; set; } = "dhathuru";

    /// <summary>
    /// ES256 private key as PEM (PKCS#8). Required outside Development; in Development a throw-away key is generated
    /// at startup (tokens then stop working after a restart). Set via user-secrets or <c>Auth__SigningKeyPem</c>.
    /// </summary>
    public string? SigningKeyPem { get; set; }

    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Absolute session length. Refresh tokens rotate but never extend this.</summary>
    public int SessionDays { get; set; } = 30;

    /// <summary>A refresh token unused for this long dies, even inside the session lifetime.</summary>
    public int RefreshIdleDays { get; set; } = 14;

    /// <summary>
    /// Enumeration-sensitive endpoints (login, register, forgot/resend) take at least this long, whichever branch runs,
    /// so response time does not reveal whether an account exists. Set to 0 in tests.
    /// </summary>
    public int ConstantTimeFloorMilliseconds { get; set; } = 350;

    public int VerifyEmailTokenHours { get; set; } = 24;

    public int ResetPasswordTokenMinutes { get; set; } = 60;

    public int ChangeEmailTokenHours { get; set; } = 24;

    /// <summary>Minimum seconds between two emails of the same kind to the same account.</summary>
    public int EmailCooldownSeconds { get; set; } = 60;

    public int MaxTravellersPerUser { get; set; } = 20;

    public int MaxSessionsPerUser { get; set; } = 20;

    public int PasswordMinLength { get; set; } = 10;

    public int PasswordMaxLength { get; set; } = 128;

    public int LockoutMaxFailedAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;

    /// <summary>Public base URL of the web app, used to build links in emails.</summary>
    public string WebBaseUrl { get; set; } = "http://localhost:3000";

    public string[] SupportedLocales { get; set; } = ["en"];
}
