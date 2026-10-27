using System.Security.Cryptography;
using System.Text;

namespace Dhathuru.Identity.Services;

internal static class Secrets
{
    /// <summary>URL-safe random token, 256 bits.</summary>
    public static string NewToken(string prefix = "")
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return prefix + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
