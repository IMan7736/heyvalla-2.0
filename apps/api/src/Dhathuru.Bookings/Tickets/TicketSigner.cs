using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Dhathuru.Bookings.Tickets;

public sealed class TicketSigningOptions
{
    public const string Section = "Bookings:Tickets";

    /// <summary>Key id used for new tickets. Old ids stay in <see cref="Keys"/> so earlier tickets still verify.</summary>
    public string ActiveKeyId { get; set; } = "k1";

    /// <summary>Key id to base64 secret (32+ bytes). Set via user-secrets or environment, never in source.</summary>
    public Dictionary<string, string> Keys { get; set; } = new(StringComparer.Ordinal);
}

public enum TicketVerdict { Valid, Malformed, UnknownKey, BadSignature }

public sealed record ParsedTicket(string TicketId, string KeyId, string Nonce);

/// <summary>
/// Signs ticket payloads with HMAC-SHA256. Payload: <c>dhathuru:v1.{kid}.{ticketId}.{nonce}.{mac}</c>, where the MAC covers
/// everything before it. Rotation: add a new key, switch <see cref="TicketSigningOptions.ActiveKeyId"/>, keep the old key until
/// its tickets expire.
/// </summary>
public sealed class TicketSigner
{
    public const string Prefix = "dhathuru:v1.";

    private readonly string _activeKeyId;
    private readonly Dictionary<string, byte[]> _keys;

    public TicketSigner(IOptions<TicketSigningOptions> options)
    {
        var value = options.Value;
        _keys = value.Keys.ToDictionary(k => k.Key, k => Convert.FromBase64String(k.Value), StringComparer.Ordinal);
        if (!_keys.TryGetValue(value.ActiveKeyId, out var active) || active.Length < 32)
        {
            throw new InvalidOperationException(
                $"Ticket signing key '{value.ActiveKeyId}' is missing or shorter than 32 bytes. Set {TicketSigningOptions.Section}:Keys.");
        }
        _activeKeyId = value.ActiveKeyId;
    }

    public string ActiveKeyId => _activeKeyId;

    public static string NewNonce() => Base64Url(RandomNumberGenerator.GetBytes(12));

    public string Sign(string ticketId, string nonce, string? keyId = null)
    {
        var kid = keyId ?? _activeKeyId;
        var body = $"{kid}.{ticketId}.{nonce}";
        return $"{Prefix}{kid}.{ticketId}.{nonce}.{Mac(kid, body)}";
    }

    public TicketVerdict Verify(string payload, out ParsedTicket? ticket)
    {
        ticket = null;
        if (payload is null || !payload.StartsWith(Prefix, StringComparison.Ordinal) || payload.Length > 256)
        {
            return TicketVerdict.Malformed;
        }

        var parts = payload[Prefix.Length..].Split('.');
        if (parts.Length != 4 || parts.Any(p => p.Length == 0))
        {
            return TicketVerdict.Malformed;
        }

        var (kid, id, nonce, mac) = (parts[0], parts[1], parts[2], parts[3]);
        if (!_keys.ContainsKey(kid))
        {
            return TicketVerdict.UnknownKey;
        }

        var expected = Encoding.ASCII.GetBytes(Mac(kid, $"{kid}.{id}.{nonce}"));
        var actual = Encoding.ASCII.GetBytes(mac);
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            return TicketVerdict.BadSignature;
        }

        ticket = new ParsedTicket(id, kid, nonce);
        return TicketVerdict.Valid;
    }

    private string Mac(string keyId, string body)
    {
        using var hmac = new HMACSHA256(_keys[keyId]);
        return Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)));
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
