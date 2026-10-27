using System.Text;

namespace Dhathuru.Bookings.Tickets;

/// <summary>
/// Minimal QR Code (model 2) encoder: byte mode, error correction level M, versions 1 to 10 (up to 213 bytes).
/// Written in-house to avoid a dependency for a single use; verified against an independent decoder in the tests' manual check.
/// </summary>
public static class QrEncoder
{
    private sealed record VersionInfo(int Version, int EcPerBlock, int G1Blocks, int G1Data, int G2Blocks, int G2Data)
    {
        public int DataCodewords => G1Blocks * G1Data + G2Blocks * G2Data;
    }

    private static readonly VersionInfo[] Versions =
    [
        new(1, 10, 1, 16, 0, 0),
        new(2, 16, 1, 28, 0, 0),
        new(3, 26, 1, 44, 0, 0),
        new(4, 18, 2, 32, 0, 0),
        new(5, 24, 2, 43, 0, 0),
        new(6, 16, 4, 27, 0, 0),
        new(7, 18, 4, 31, 0, 0),
        new(8, 22, 2, 38, 2, 39),
        new(9, 22, 3, 36, 2, 37),
        new(10, 26, 4, 43, 1, 44),
    ];

    private static readonly int[][] Alignment =
    [
        [], [], [6, 18], [6, 22], [6, 26], [6, 30], [6, 34], [6, 22, 38], [6, 24, 42], [6, 26, 46], [6, 28, 50],
    ];

    private static readonly byte[] Exp = new byte[512];
    private static readonly byte[] Log = new byte[256];

    static QrEncoder()
    {
        var x = 1;
        for (var i = 0; i < 255; i++)
        {
            Exp[i] = (byte)x;
            Log[x] = (byte)i;
            x <<= 1;
            if (x >= 256)
            {
                x ^= 0x11D;
            }
        }
        for (var i = 255; i < 512; i++)
        {
            Exp[i] = Exp[i - 255];
        }
    }

    public const int MaxBytes = 213;

    /// <summary>Returns the module matrix (true = dark), without the quiet zone.</summary>
    public static bool[,] Encode(string text)
    {
        var data = Encoding.UTF8.GetBytes(text);
        var info = Versions.FirstOrDefault(v => data.Length <= v.DataCodewords - (v.Version < 10 ? 2 : 3))
            ?? throw new ArgumentException($"Text is too long for a QR code (max {MaxBytes} bytes).", nameof(text));

        var codewords = Interleave(info, BuildDataCodewords(info, data));
        var size = 17 + 4 * info.Version;
        var modules = new bool[size, size];
        var isFunction = new bool[size, size];
        DrawFunctionPatterns(info.Version, modules, isFunction);
        PlaceData(modules, isFunction, codewords);

        var bestMask = 0;
        var bestPenalty = int.MaxValue;
        for (var mask = 0; mask < 8; mask++)
        {
            var candidate = (bool[,])modules.Clone();
            ApplyMask(candidate, isFunction, mask);
            DrawFormat(candidate, mask);
            var penalty = Penalty(candidate);
            if (penalty < bestPenalty)
            {
                bestPenalty = penalty;
                bestMask = mask;
            }
        }

        ApplyMask(modules, isFunction, bestMask);
        DrawFormat(modules, bestMask);
        return modules;
    }

    public static string ToSvg(bool[,] modules, int quietZone = 4, string dark = "#12343B", string light = "#FFFFFF")
    {
        var n = modules.GetLength(0);
        var total = n + quietZone * 2;
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {total} {total}\" shape-rendering=\"crispEdges\" role=\"img\">");
        sb.Append($"<rect width=\"{total}\" height=\"{total}\" fill=\"{light}\"/><path fill=\"{dark}\" d=\"");
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                if (modules[y, x])
                {
                    sb.Append($"M{x + quietZone} {y + quietZone}h1v1h-1z");
                }
            }
        }
        sb.Append("\"/></svg>");
        return sb.ToString();
    }

    private static byte[] BuildDataCodewords(VersionInfo info, byte[] data)
    {
        var bits = new List<bool>();
        void Append(int value, int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                bits.Add(((value >> i) & 1) != 0);
            }
        }

        Append(0b0100, 4);
        Append(data.Length, info.Version < 10 ? 8 : 16);
        foreach (var b in data)
        {
            Append(b, 8);
        }

        var capacityBits = info.DataCodewords * 8;
        Append(0, Math.Min(4, capacityBits - bits.Count));
        while (bits.Count % 8 != 0)
        {
            bits.Add(false);
        }

        var result = new List<byte>();
        for (var i = 0; i < bits.Count; i += 8)
        {
            var v = 0;
            for (var j = 0; j < 8; j++)
            {
                v = (v << 1) | (bits[i + j] ? 1 : 0);
            }
            result.Add((byte)v);
        }

        for (var pad = 0xEC; result.Count < info.DataCodewords; pad ^= 0xEC ^ 0x11)
        {
            result.Add((byte)pad);
        }
        return [.. result];
    }

    private static byte[] Interleave(VersionInfo info, byte[] data)
    {
        var blocks = new List<byte[]>();
        var offset = 0;
        for (var i = 0; i < info.G1Blocks + info.G2Blocks; i++)
        {
            var length = i < info.G1Blocks ? info.G1Data : info.G2Data;
            blocks.Add(data[offset..(offset + length)]);
            offset += length;
        }

        var generator = Generator(info.EcPerBlock);
        var ec = blocks.Select(b => ReedSolomon(b, generator)).ToList();
        var output = new List<byte>();
        var maxData = blocks.Max(b => b.Length);
        for (var i = 0; i < maxData; i++)
        {
            foreach (var b in blocks)
            {
                if (i < b.Length)
                {
                    output.Add(b[i]);
                }
            }
        }
        for (var i = 0; i < info.EcPerBlock; i++)
        {
            foreach (var e in ec)
            {
                output.Add(e[i]);
            }
        }
        return [.. output];
    }

    private static byte[] Generator(int degree)
    {
        var poly = new byte[] { 1 };
        for (var i = 0; i < degree; i++)
        {
            var next = new byte[poly.Length + 1];
            for (var j = 0; j < poly.Length; j++)
            {
                next[j] ^= poly[j];
                next[j + 1] ^= Multiply(poly[j], Exp[i]);
            }
            poly = next;
        }
        return poly;
    }

    private static byte[] ReedSolomon(byte[] data, byte[] generator)
    {
        var degree = generator.Length - 1;
        var remainder = new byte[degree];
        foreach (var b in data)
        {
            var factor = (byte)(b ^ remainder[0]);
            Array.Copy(remainder, 1, remainder, 0, degree - 1);
            remainder[degree - 1] = 0;
            for (var i = 0; i < degree; i++)
            {
                remainder[i] ^= Multiply(generator[i + 1], factor);
            }
        }
        return remainder;
    }

    private static byte Multiply(byte a, byte b) => a == 0 || b == 0 ? (byte)0 : Exp[Log[a] + Log[b]];

    private static void DrawFunctionPatterns(int version, bool[,] m, bool[,] f)
    {
        var size = m.GetLength(0);

        void Set(int x, int y, bool dark)
        {
            if (x >= 0 && y >= 0 && x < size && y < size)
            {
                m[y, x] = dark;
                f[y, x] = true;
            }
        }

        for (var i = 0; i < size; i++)
        {
            Set(6, i, i % 2 == 0);
            Set(i, 6, i % 2 == 0);
        }

        void Finder(int cx, int cy)
        {
            for (var dy = -4; dy <= 4; dy++)
            {
                for (var dx = -4; dx <= 4; dx++)
                {
                    var dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    Set(cx + dx, cy + dy, dist != 2 && dist != 4);
                }
            }
        }

        Finder(3, 3);
        Finder(size - 4, 3);
        Finder(3, size - 4);

        var positions = Alignment[version];
        for (var i = 0; i < positions.Length; i++)
        {
            for (var j = 0; j < positions.Length; j++)
            {
                if ((i == 0 && j == 0) || (i == 0 && j == positions.Length - 1) || (i == positions.Length - 1 && j == 0))
                {
                    continue;
                }
                for (var dy = -2; dy <= 2; dy++)
                {
                    for (var dx = -2; dx <= 2; dx++)
                    {
                        Set(positions[i] + dx, positions[j] + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
                    }
                }
            }
        }

        // Reserve format areas (drawn per mask) and the dark module.
        DrawFormat(m, 0, f);
        Set(8, size - 8, true);

        if (version >= 7)
        {
            var rem = version;
            for (var i = 0; i < 12; i++)
            {
                rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
            }
            var bits = (version << 12) | rem;
            for (var i = 0; i < 18; i++)
            {
                var dark = ((bits >> i) & 1) != 0;
                var a = size - 11 + i % 3;
                var b = i / 3;
                Set(a, b, dark);
                Set(b, a, dark);
            }
        }
    }

    private static void DrawFormat(bool[,] m, int mask, bool[,]? function = null)
    {
        var size = m.GetLength(0);
        var data = (0b00 << 3) | mask; // level M = 00
        var rem = data;
        for (var i = 0; i < 10; i++)
        {
            rem = (rem << 1) ^ ((rem >> 9) * 0x537);
        }
        var bits = ((data << 10) | rem) ^ 0x5412;

        void Set(int x, int y, bool dark)
        {
            m[y, x] = dark;
            if (function is not null)
            {
                function[y, x] = true;
            }
        }

        bool Bit(int i) => ((bits >> i) & 1) != 0;

        for (var i = 0; i <= 5; i++)
        {
            Set(8, i, Bit(i));
        }
        Set(8, 7, Bit(6));
        Set(8, 8, Bit(7));
        Set(7, 8, Bit(8));
        for (var i = 9; i < 15; i++)
        {
            Set(14 - i, 8, Bit(i));
        }
        for (var i = 0; i < 8; i++)
        {
            Set(size - 1 - i, 8, Bit(i));
        }
        for (var i = 8; i < 15; i++)
        {
            Set(8, size - 15 + i, Bit(i));
        }
        m[size - 8, 8] = true;
    }

    private static void PlaceData(bool[,] m, bool[,] f, byte[] codewords)
    {
        var size = m.GetLength(0);
        var bitIndex = 0;
        var total = codewords.Length * 8;
        for (var right = size - 1; right >= 1; right -= 2)
        {
            if (right == 6)
            {
                right = 5;
            }
            for (var vert = 0; vert < size; vert++)
            {
                for (var j = 0; j < 2; j++)
                {
                    var x = right - j;
                    var upward = ((right + 1) & 2) == 0;
                    var y = upward ? size - 1 - vert : vert;
                    if (!f[y, x] && bitIndex < total)
                    {
                        m[y, x] = ((codewords[bitIndex >> 3] >> (7 - (bitIndex & 7))) & 1) != 0;
                        bitIndex++;
                    }
                }
            }
        }
    }

    private static void ApplyMask(bool[,] m, bool[,] f, int mask)
    {
        var size = m.GetLength(0);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (f[y, x])
                {
                    continue;
                }
                var invert = mask switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (x + y) % 3 == 0,
                    4 => (x / 3 + y / 2) % 2 == 0,
                    5 => x * y % 2 + x * y % 3 == 0,
                    6 => (x * y % 2 + x * y % 3) % 2 == 0,
                    _ => ((x + y) % 2 + x * y % 3) % 2 == 0,
                };
                if (invert)
                {
                    m[y, x] = !m[y, x];
                }
            }
        }
    }

    private static int Penalty(bool[,] m)
    {
        var size = m.GetLength(0);
        var score = 0;

        for (var pass = 0; pass < 2; pass++)
        {
            for (var a = 0; a < size; a++)
            {
                var run = 1;
                for (var b = 1; b < size; b++)
                {
                    bool Cell(int i) => pass == 0 ? m[a, i] : m[i, a];
                    if (Cell(b) == Cell(b - 1))
                    {
                        run++;
                        if (run == 5)
                        {
                            score += 3;
                        }
                        else if (run > 5)
                        {
                            score++;
                        }
                    }
                    else
                    {
                        run = 1;
                    }
                }
            }
        }

        for (var y = 0; y < size - 1; y++)
        {
            for (var x = 0; x < size - 1; x++)
            {
                if (m[y, x] == m[y, x + 1] && m[y, x] == m[y + 1, x] && m[y, x] == m[y + 1, x + 1])
                {
                    score += 3;
                }
            }
        }

        ReadOnlySpan<bool> p1 = [true, false, true, true, true, false, true, false, false, false, false];
        ReadOnlySpan<bool> p2 = [false, false, false, false, true, false, true, true, true, false, true];
        for (var pass = 0; pass < 2; pass++)
        {
            for (var a = 0; a < size; a++)
            {
                for (var b = 0; b <= size - 11; b++)
                {
                    var m1 = true;
                    var m2 = true;
                    for (var k = 0; k < 11; k++)
                    {
                        var cell = pass == 0 ? m[a, b + k] : m[b + k, a];
                        m1 &= cell == p1[k];
                        m2 &= cell == p2[k];
                    }
                    if (m1)
                    {
                        score += 40;
                    }
                    if (m2)
                    {
                        score += 40;
                    }
                }
            }
        }

        var dark = 0;
        foreach (var cell in m)
        {
            if (cell)
            {
                dark++;
            }
        }
        score += Math.Abs(dark * 20 - size * size * 10) / (size * size) * 10;
        return score;
    }
}
