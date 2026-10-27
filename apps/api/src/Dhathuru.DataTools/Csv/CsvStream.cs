using System.Text;

namespace Dhathuru.DataTools.Csv;

/// <summary>
/// Minimal streaming CSV reader (RFC 4180 quoting). Reads one record at a time so 70 MB files never sit in memory.
/// A UTF-8 byte order mark is skipped. Blank lines are ignored.
/// </summary>
public sealed class CsvStream : IDisposable
{
    private readonly TextReader reader;
    private readonly Dictionary<string, int> columns = new(StringComparer.OrdinalIgnoreCase);

    public CsvStream(TextReader reader)
    {
        this.reader = reader;
        var header = ReadRecord() ?? throw new InvalidDataException("CSV has no header row.");
        for (var i = 0; i < header.Length; i++) columns[header[i].Trim().TrimStart('﻿')] = i;
    }

    public static CsvStream Open(string path) => new(new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1 << 16));

    public IReadOnlyCollection<string> Columns => columns.Keys;

    public int Index(string column) =>
        columns.TryGetValue(column, out var i) ? i : throw new InvalidDataException($"CSV column '{column}' not found. Found: {string.Join(", ", columns.Keys)}.");

    /// <summary>Next row, or null at the end. The array is new for each call.</summary>
    public string[]? Next() => ReadRecord();

    private string[]? ReadRecord()
    {
        while (true)
        {
            var first = reader.Peek();
            if (first < 0) return null;
            if (first is '\r' or '\n')
            {
                reader.Read();
                continue;
            }
            break;
        }

        var fields = new List<string>(16);
        var sb = new StringBuilder();
        var inQuotes = false;
        while (true)
        {
            var c = reader.Read();
            if (c < 0)
            {
                fields.Add(sb.ToString());
                return [.. fields];
            }
            var ch = (char)c;
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        sb.Append('"');
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(ch);
                }
                continue;
            }
            switch (ch)
            {
                case '"' when sb.Length == 0:
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(sb.ToString());
                    sb.Clear();
                    break;
                case '\r':
                    if (reader.Peek() == '\n') reader.Read();
                    fields.Add(sb.ToString());
                    return [.. fields];
                case '\n':
                    fields.Add(sb.ToString());
                    return [.. fields];
                default:
                    sb.Append(ch);
                    break;
            }
        }
    }

    public void Dispose() => reader.Dispose();
}
