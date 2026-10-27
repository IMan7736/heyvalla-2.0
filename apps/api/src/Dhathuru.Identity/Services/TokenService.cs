using System.Security.Claims;
using System.Security.Cryptography;
using Dhathuru.Identity.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Dhathuru.Identity.Services;

/// <summary>Issues and describes short-lived ES256 access tokens.</summary>
public sealed class AccessTokenIssuer
{
    public const string SessionClaim = "sid";
    public const string EmailVerifiedClaim = "email_verified";

    private readonly AuthOptions options;
    private readonly TimeProvider time;
    private readonly JsonWebTokenHandler handler = new();

    public AccessTokenIssuer(IOptions<AuthOptions> options, TimeProvider time, SigningKeyProvider keys)
    {
        this.options = options.Value;
        this.time = time;
        SigningCredentials = new SigningCredentials(keys.Key, SecurityAlgorithms.EcdsaSha256);
    }

    internal SigningCredentials SigningCredentials { get; }

    public int ExpiresInSeconds => options.AccessTokenMinutes * 60;

    public string Issue(AppUser user, string sessionId, IEnumerable<string> roles)
    {
        var now = time.GetUtcNow();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(SessionClaim, sessionId),
            new(EmailVerifiedClaim, user.EmailConfirmed ? "true" : "false", ClaimValueTypes.Boolean),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""), // Bookings addresses confirmations from this claim
            new("locale", user.Locale),
        };
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(options.AccessTokenMinutes).UtcDateTime,
            SigningCredentials = SigningCredentials,
        });
    }
}

/// <summary>Holds the ECDSA P-256 key used to sign and validate access tokens.</summary>
public sealed class SigningKeyProvider
{
    public SigningKeyProvider(AuthOptions options, bool allowEphemeral)
    {
        var ecdsa = ECDsa.Create();
        if (!string.IsNullOrWhiteSpace(options.SigningKeyPem))
        {
            ecdsa.ImportFromPem(options.SigningKeyPem.Replace("\\n", "\n", StringComparison.Ordinal));
        }
        else if (allowEphemeral)
        {
            ecdsa.GenerateKey(ECCurve.NamedCurves.nistP256);
        }
        else
        {
            throw new InvalidOperationException("Auth:SigningKeyPem is not configured. Set Auth__SigningKeyPem to an ES256 (P-256) PKCS#8 PEM private key.");
        }

        if (ecdsa.KeySize != 256)
        {
            throw new InvalidOperationException("Auth:SigningKeyPem must be an ES256 (P-256) key.");
        }

        Key = new ECDsaSecurityKey(ecdsa) { KeyId = "dhathuru-1" };
    }

    public ECDsaSecurityKey Key { get; }
}
