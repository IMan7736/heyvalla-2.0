using System.Security.Cryptography;

namespace Dhathuru.Domain.Ids;

/// <summary>Minimal ULID generator (48-bit time + 80-bit randomness, Crockford base32, 26 chars).</summary>
public static class Ulid
{
    public const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    public const int Length = 26;

    private static readonly object Gate = new();
    private static long lastMillis;
    private static readonly byte[] LastRandom = new byte[10];

    public static string NewString() => NewString(TimeProvider.System.GetUtcNow());

    /// <summary>Monotonic within one millisecond so ids created in a burst still sort in creation order.</summary>
    public static string NewString(DateTimeOffset now)
    {
        var millis = now.ToUnixTimeMilliseconds();
        Span<byte> random = stackalloc byte[10];
        lock (Gate)
        {
            if (millis <= lastMillis)
            {
                millis = lastMillis;
                LastRandom.CopyTo(random);
                IncrementBigEndian(random);
            }
            else
            {
                RandomNumberGenerator.Fill(random);
            }
            lastMillis = millis;
            random.CopyTo(LastRandom);
        }
        return Encode(millis, random);
    }

    public static bool IsValid(ReadOnlySpan<char> value)
    {
        if (value.Length != Length) return false;
        foreach (var c in value)
        {
            if (Alphabet.IndexOf(c) < 0) return false;
        }
        // The first character encodes only 3 bits of the 48-bit timestamp.
        return value[0] <= '7';
    }

    /// <summary>Millisecond timestamp encoded in a valid ULID.</summary>
    public static DateTimeOffset Timestamp(ReadOnlySpan<char> value)
    {
        long millis = 0;
        for (var i = 0; i < 10; i++) millis = (millis << 5) | (long)Alphabet.IndexOf(value[i]);
        return DateTimeOffset.FromUnixTimeMilliseconds(millis);
    }

    private static void IncrementBigEndian(Span<byte> bytes)
    {
        for (var i = bytes.Length - 1; i >= 0; i--)
        {
            if (++bytes[i] != 0) return;
        }
    }

    private static string Encode(long millis, ReadOnlySpan<byte> random)
    {
        Span<char> chars = stackalloc char[Length];
        for (var i = 9; i >= 0; i--)
        {
            chars[i] = Alphabet[(int)(millis & 31)];
            millis >>= 5;
        }
        // 80 random bits -> 16 base32 chars.
        var buffer = 0UL;
        var bits = 0;
        var index = 10;
        foreach (var b in random)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                chars[index++] = Alphabet[(int)((buffer >> bits) & 31)];
            }
        }
        return new string(chars);
    }
}
