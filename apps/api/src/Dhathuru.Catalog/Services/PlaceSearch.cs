using System.Globalization;
using System.Text;
using Dhathuru.Catalog.Data;
using Dhathuru.Domain.Places;

namespace Dhathuru.Catalog.Services;

public sealed record PlaceMatch(CatalogPlace Place, int Score, double? DistanceKm);

/// <summary>
/// Forgiving place search: exact, prefix, word prefix, substring, then edit distance, on both the plain
/// name and a phonetic key that folds common Maldivian romanisation variants (Thulusdhoo / Tulusdoo,
/// Hulhumalé / Hulumale, Maafushi / Mafushi). Names in other locales are matched as plain text.
/// </summary>
public static class PlaceSearch
{
    public const int MinScore = 30;

    public static IReadOnlyList<PlaceMatch> Search(IEnumerable<CatalogPlace> places, string? query, PlaceType? type, GeoPoint? near)
    {
        var candidates = places.Where(p => type is null || p.Type == type).ToList();
        var q = Normalize(query);
        var results = new List<PlaceMatch>();
        foreach (var p in candidates)
        {
            var distance = near is { } n ? GeoMath.HaversineKm(n, p.Geo) : (double?)null;
            if (q.Length == 0)
            {
                results.Add(new PlaceMatch(p, 0, distance));
                continue;
            }
            var score = Score(p, q);
            if (score >= MinScore) results.Add(new PlaceMatch(p, score, distance));
        }
        return results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.DistanceKm ?? double.MaxValue)
            .ThenBy(r => TypeRank(r.Place.Type))
            .ThenBy(r => r.Place.Name["en"], StringComparer.Ordinal)
            .ToList();
    }

    private static int TypeRank(PlaceType t) => t switch
    {
        PlaceType.Island => 0,
        PlaceType.Airport => 1,
        PlaceType.Jetty => 2,
        PlaceType.Atoll => 3,
        PlaceType.TaxiStand => 4,
        _ => 5,
    };

    internal static int Score(CatalogPlace place, string normalizedQuery)
    {
        var best = 0;
        if (place.Code is { } code && Normalize(code) == normalizedQuery) best = 95;
        var phoneticQuery = Phonetic(normalizedQuery);
        foreach (var raw in place.Name.Values)
        {
            var name = Normalize(raw);
            if (name.Length == 0) continue;
            best = Math.Max(best, ScoreName(name, normalizedQuery, phoneticQuery));
        }
        return best;
    }

    private static int ScoreName(string name, string q, string phoneticQ)
    {
        if (name == q) return 100;
        if (name.StartsWith(q, StringComparison.Ordinal)) return 80;
        var phoneticName = Phonetic(name);
        if (phoneticName == phoneticQ) return 90;
        if (phoneticName.StartsWith(phoneticQ, StringComparison.Ordinal)) return 70;
        var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Any(t => t.StartsWith(q, StringComparison.Ordinal))) return 65;
        if (name.Contains(q, StringComparison.Ordinal)) return 55;
        if (phoneticName.Contains(phoneticQ, StringComparison.Ordinal)) return 45;

        // Edit distance against the whole name and each word, tolerant of one or two typos.
        var allowed = phoneticQ.Length <= 4 ? 0 : phoneticQ.Length <= 7 ? 1 : 2;
        if (allowed == 0) return 0;
        var bestDistance = int.MaxValue;
        foreach (var candidate in tokens.Select(Phonetic).Append(phoneticName))
        {
            bestDistance = Math.Min(bestDistance, EditDistance(phoneticQ, candidate));
            if (candidate.Length > phoneticQ.Length)
                bestDistance = Math.Min(bestDistance, EditDistance(phoneticQ, candidate[..phoneticQ.Length]));
        }
        return bestDistance <= allowed ? 40 - bestDistance * 5 : 0;
    }

    /// <summary>Lower case, accents removed, anything but letters and digits becomes a single space.</summary>
    /// <remarks>Accents are folded with an explicit table: the host runs with invariant globalization, where Unicode normalisation is not available.</remarks>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var sb = new StringBuilder(value.Length);
        var lastSpace = true;
        foreach (var raw in value)
        {
            var ch = FoldAccent(char.ToLowerInvariant(raw));
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                lastSpace = false;
            }
            else if (!lastSpace)
            {
                sb.Append(' ');
                lastSpace = true;
            }
        }
        return sb.ToString().Trim();
    }

    private static char FoldAccent(char c) => c switch
    {
        'à' or 'á' or 'â' or 'ã' or 'ä' or 'å' or 'ā' => 'a',
        'ç' or 'č' => 'c',
        'è' or 'é' or 'ê' or 'ë' or 'ē' => 'e',
        'ì' or 'í' or 'î' or 'ï' or 'ī' => 'i',
        'ñ' or 'ṇ' => 'n',
        'ò' or 'ó' or 'ô' or 'õ' or 'ö' or 'ō' => 'o',
        'š' or 'ş' => 's',
        'ù' or 'ú' or 'û' or 'ü' or 'ū' => 'u',
        'ý' or 'ÿ' => 'y',
        'ṭ' => 't',
        'ḍ' => 'd',
        _ => c,
    };

    /// <summary>Coarse sound-alike key. Not reversible; only used to compare two strings.</summary>
    public static string Phonetic(string normalized)
    {
        var s = normalized
            .Replace("dh", "d", StringComparison.Ordinal)
            .Replace("th", "t", StringComparison.Ordinal)
            .Replace("sh", "s", StringComparison.Ordinal)
            .Replace("kh", "k", StringComparison.Ordinal)
            .Replace("gh", "g", StringComparison.Ordinal)
            .Replace("ph", "f", StringComparison.Ordinal)
            .Replace("aa", "a", StringComparison.Ordinal)
            .Replace("ee", "i", StringComparison.Ordinal)
            .Replace("oo", "u", StringComparison.Ordinal)
            .Replace("ou", "u", StringComparison.Ordinal)
            .Replace('w', 'v')
            .Replace('y', 'i')
            .Replace('h', ' ')
            .Replace(" ", "", StringComparison.Ordinal);
        // collapse doubled letters (Hulhumale -> hulumale, Mathiveri unaffected)
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (sb.Length == 0 || sb[^1] != ch) sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>Optimal string alignment distance (insert, delete, substitute, adjacent swap).</summary>
    public static int EditDistance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        }
        return d[a.Length, b.Length];
    }
}
