using System.Security.Claims;
using Dhathuru.Domain.Http;

namespace Dhathuru.Bookings.Services;

/// <summary>The signed-in traveller. Identity (DHONI) issues <c>sub</c> as the <c>usr_</c> id and an <c>email</c> claim.</summary>
public sealed record CurrentUser(string Id, string? Email, string Locale)
{
    public static CurrentUser From(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(id))
        {
            throw ProblemException.Unauthorized();
        }

        var email = principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email);
        var locale = principal.FindFirstValue("locale") ?? "en";
        return new CurrentUser(id, email, locale);
    }
}
