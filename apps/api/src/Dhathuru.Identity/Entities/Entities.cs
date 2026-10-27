namespace Dhathuru.Identity.Entities;

/// <summary>A signed-in device: one refresh token family. Revoking the session kills every token in it.</summary>
public sealed class RefreshSession
{
    public string Id { get; set; } = "";

    public string UserId { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastUsedAt { get; set; }

    /// <summary>Absolute end of the session; refresh tokens cannot extend it.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokedReason { get; set; }

    /// <summary>Truncated user agent, only used to help the user recognise the device.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Client address with the host part removed (a /24 or /48 prefix), never the full address.</summary>
    public string? NetworkHint { get; set; }

    public List<RefreshToken> Tokens { get; set; } = [];
}

/// <summary>One issued refresh token. Only a SHA-256 hash is stored.</summary>
public sealed class RefreshToken
{
    public long Id { get; set; }

    public string SessionId { get; set; } = "";

    public RefreshSession? Session { get; set; }

    public string TokenHash { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set when the token was exchanged. Presenting it again is treated as theft.</summary>
    public DateTimeOffset? UsedAt { get; set; }
}

public static class AccountTokenPurpose
{
    public const string VerifyEmail = "verify_email";
    public const string ResetPassword = "reset_password";
    public const string ChangeEmail = "change_email";
}

/// <summary>Single-use emailed token (verify email, reset password, confirm new email). Only a hash is stored.</summary>
public sealed class AccountToken
{
    public long Id { get; set; }

    public string UserId { get; set; } = "";

    public string Purpose { get; set; } = "";

    public string TokenHash { get; set; } = "";

    /// <summary>For <see cref="AccountTokenPurpose.ChangeEmail"/>: the address to switch to.</summary>
    public string? Payload { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}

public static class DocumentTypes
{
    public const string None = "none";
    public const string NationalId = "national_id";
    public const string Passport = "passport";

    public static readonly IReadOnlyList<string> All = [None, NationalId, Passport];
}

/// <summary>A person the user books for. v1 stores the kind of ID they carry, never the number.</summary>
public sealed class Traveller
{
    public string Id { get; set; } = "";

    public string UserId { get; set; } = "";

    public string FullName { get; set; } = "";

    /// <summary>adult | child | infant</summary>
    public string PassengerType { get; set; } = "adult";

    public DateOnly? DateOfBirth { get; set; }

    public string? Phone { get; set; }

    public string DocumentType { get; set; } = DocumentTypes.None;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
