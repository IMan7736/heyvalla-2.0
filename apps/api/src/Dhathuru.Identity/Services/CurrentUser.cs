using System.Security.Claims;
using Dhathuru.Domain.Security;
using Microsoft.AspNetCore.Http;

namespace Dhathuru.Identity.Services;

internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string UserId =>
        Principal?.FindFirstValue("sub") is { Length: > 0 } id && IsAuthenticated
            ? id
            : throw new InvalidOperationException("There is no authenticated user on this request.");

    public string? SessionId => Principal?.FindFirstValue(AccessTokenIssuer.SessionClaim);

    public bool EmailVerified => Principal?.FindFirstValue(AccessTokenIssuer.EmailVerifiedClaim) == "true";

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;
}
