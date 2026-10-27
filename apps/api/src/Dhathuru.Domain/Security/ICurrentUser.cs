namespace Dhathuru.Domain.Security;

/// <summary>Role names carried in the access token. Only <see cref="Passenger"/> is issued in v1.</summary>
public static class Roles
{
    public const string Passenger = "passenger";
    public const string Operator = "operator";
    public const string Admin = "admin";
}

/// <summary>Names of the authorisation policies registered by the API host.</summary>
public static class Policies
{
    /// <summary>Any signed-in user.</summary>
    public const string Authenticated = "authenticated";

    /// <summary>Signed in and the email address is verified (booking and payment endpoints).</summary>
    public const string VerifiedEmail = "verified-email";

    public const string Operator = "operator";
    public const string Admin = "admin";
}

/// <summary>The caller of the current request. Resolved from the validated access token, never from the request body.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The user id (<c>usr_...</c>). Throws when the caller is anonymous; check <see cref="IsAuthenticated"/> or require a policy.</summary>
    string UserId { get; }

    /// <summary>The session (refresh token family) the access token belongs to, if any.</summary>
    string? SessionId { get; }

    bool EmailVerified { get; }

    bool IsInRole(string role);
}

/// <summary>
/// Ownership guard against insecure direct object references. Modules call this after loading a resource by id:
/// a resource that belongs to someone else must look exactly like one that does not exist (404, never 403).
/// </summary>
public static class Ownership
{
    public static bool Owns(ICurrentUser user, string? ownerUserId) =>
        user.IsAuthenticated
        && ownerUserId is not null
        && string.Equals(user.UserId, ownerUserId, StringComparison.Ordinal);

    public static bool CanAccess(ICurrentUser user, string? ownerUserId) =>
        Owns(user, ownerUserId) || user.IsInRole(Roles.Admin);
}
