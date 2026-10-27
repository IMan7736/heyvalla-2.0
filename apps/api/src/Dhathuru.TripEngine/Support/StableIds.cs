using System.Security.Cryptography;
using System.Text;
using Dhathuru.Domain.Ids;

namespace Dhathuru.TripEngine.Support;

/// <summary>
/// Ids derived from content so the same input and snapshot always produce the same output (determinism requirement).
/// A hash is encoded into the 26-character ULID alphabet; the first character is kept within the valid 3-bit range.
/// </summary>
internal static class StableIds
{
    public static string Ulid(string material)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        var chars = new char[Dhathuru.Domain.Ids.Ulid.Length];
        var buffer = 0UL;
        var bits = 0;
        var index = 0;
        foreach (var b in hash)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5 && index < chars.Length)
            {
                bits -= 5;
                var v = (int)((buffer >> bits) & 31);
                if (index == 0) v &= 7;
                chars[index++] = Dhathuru.Domain.Ids.Ulid.Alphabet[v];
            }
            if (index == chars.Length) break;
        }
        return new string(chars);
    }

    public static LegId Leg(string material) => LegId.Parse("leg_" + Ulid("leg|" + material));

    public static TripOptionId Option(string material) => TripOptionId.Parse("trp_" + Ulid("trp|" + material));

    public static SearchId Search(string material) => SearchId.Parse("srch_" + Ulid("srch|" + material));
}
