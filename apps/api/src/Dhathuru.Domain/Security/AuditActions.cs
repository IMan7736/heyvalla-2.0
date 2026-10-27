namespace Dhathuru.Domain.Security;

/// <summary>Stable audit action codes shared across modules.</summary>
public static class AuditActions
{
    public const string Registered = "auth.registered";
    public const string RegisterExisting = "auth.register_existing_email";
    public const string LoginSucceeded = "auth.login_succeeded";
    public const string LoginFailed = "auth.login_failed";
    public const string LockedOut = "auth.locked_out";
    public const string Logout = "auth.logout";
    public const string RefreshReuseDetected = "auth.refresh_reuse_detected";
    public const string EmailVerified = "auth.email_verified";
    public const string PasswordResetRequested = "auth.password_reset_requested";
    public const string PasswordReset = "auth.password_reset";
    public const string PasswordChanged = "auth.password_changed";
    public const string EmailChangeRequested = "auth.email_change_requested";
    public const string EmailChanged = "auth.email_changed";
    public const string SessionRevoked = "auth.session_revoked";
    public const string SessionsRevokedAll = "auth.sessions_revoked_all";
    public const string AccountDeleted = "account.deleted";
    public const string ProfileUpdated = "account.profile_updated";
    public const string TravellerCreated = "account.traveller_created";
    public const string TravellerUpdated = "account.traveller_updated";
    public const string TravellerDeleted = "account.traveller_deleted";
    public const string AccessDenied = "access.denied";
}
