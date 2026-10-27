using System.Security.Cryptography;

namespace Dhathuru.Api.Infrastructure;

/// <summary>
/// Development only: fills in ticket-signing keys that <c>scripts/dev-setup.sh</c> would normally put in user-secrets, so a
/// fresh checkout still boots. The values are random per process (nothing is committed), so tickets issued before a restart
/// stop verifying; run dev-setup once for stable keys. Never applied outside Development.
/// </summary>
public static class DevelopmentDefaults
{
    public static void AddEphemeralBookingKeys(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment()) return;

        var config = builder.Configuration;
        var generated = new Dictionary<string, string?>();
        if (string.IsNullOrWhiteSpace(config["Bookings:Tickets:ActiveKeyId"]) && string.IsNullOrWhiteSpace(config["Bookings:Tickets:Keys:dev"]))
        {
            generated["Bookings:Tickets:ActiveKeyId"] = "dev";
            generated["Bookings:Tickets:Keys:dev"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }
        if (string.IsNullOrWhiteSpace(config["Bookings:Verification:OperatorKey"]))
        {
            generated["Bookings:Verification:OperatorKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        }
        if (generated.Count > 0) config.AddInMemoryCollection(generated);
    }
}
