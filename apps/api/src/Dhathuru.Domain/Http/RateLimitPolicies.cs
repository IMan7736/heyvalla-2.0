namespace Dhathuru.Domain.Http;

/// <summary>Rate limiter policy names registered by the API host; modules attach them with <c>RequireRateLimiting</c>.</summary>
public static class RateLimitPolicies
{
    /// <summary>Strict, per client address. For credential and email-sending endpoints.</summary>
    public const string Auth = "auth";

    /// <summary>Per user (or client address when anonymous) for everything else.</summary>
    public const string Default = "default";
}
