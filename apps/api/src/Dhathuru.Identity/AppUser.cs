using Microsoft.AspNetCore.Identity;

namespace Dhathuru.Identity;

/// <summary>Account. The Id is a prefixed ULID (usr_...).</summary>
public sealed class AppUser : IdentityUser
{
    public const int MaxPreferredModes = 8;

    public string? DisplayName { get; set; }

    /// <summary>UI language (also the language of emails).</summary>
    public string Locale { get; set; } = "en";

    /// <summary>cautious | balanced | relaxed. Default for trip searches.</summary>
    public string RiskTolerance { get; set; } = RiskToleranceValues.Balanced;

    /// <summary>Transport modes the user prefers, as contract mode codes (ferry, speedboat, ...).</summary>
    public List<string> PreferredModes { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class RiskToleranceValues
{
    public const string Cautious = "cautious";
    public const string Balanced = "balanced";
    public const string Relaxed = "relaxed";

    public static readonly IReadOnlyList<string> All = [Cautious, Balanced, Relaxed];
}
