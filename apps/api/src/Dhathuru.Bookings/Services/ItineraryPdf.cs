using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Dhathuru.Bookings.Domain;
using Dhathuru.Bookings.Tickets;
using Dhathuru.Domain.Time;

namespace Dhathuru.Bookings.Services;

/// <summary>
/// Dependency free PDF itinerary (Helvetica, A4, QR codes drawn as vector squares). Written in-house so no third party
/// PDF library or licence is involved; see docs/payments/GATEWAY-READINESS.md for notes. Text is limited to Latin-1.
/// </summary>
public static class ItineraryPdf
{
    private const double PageWidth = 595;
    private const double PageHeight = 842;
    private const double Margin = 48;

    public static byte[] Render(Booking booking, Func<Ticket, string>? ticketPayload = null)
    {
        var pages = new List<StringBuilder>();
        StringBuilder page = NewPage(pages);
        var y = PageHeight - Margin;

        void Need(double height)
        {
            if (y - height < Margin)
            {
                page = NewPage(pages);
                y = PageHeight - Margin;
            }
        }

        void Text(string text, double size, bool bold, double x, double color = 0)
        {
            Need(size + 6);
            page.Append(CultureInfo.InvariantCulture,
                $"{color:0.##} g BT /{(bold ? "F2" : "F1")} {size:0.##} Tf {x:0.##} {y - size:0.##} Td ({Escape(text)}) Tj ET\n");
            y -= size + 6;
        }

        Text("Dhathuru itinerary", 20, true, Margin);
        Text($"Booking {booking.Id}", 10, false, Margin, 0.35);
        Text($"Status: {WireNames.Of(booking.Status).Replace('_', ' ')}", 10, false, Margin, 0.35);
        y -= 8;

        foreach (var leg in booking.Legs.OrderBy(l => l.LegIndex))
        {
            var json = JsonNode.Parse(leg.LegJson)!.AsObject();
            var depart = MaldivesTime.ToLocal(LegSnapshots.DepartAt(json));
            var arrive = MaldivesTime.ToLocal(LegSnapshots.ArriveAt(json));
            Need(54);
            Text($"{leg.LegIndex + 1}. {Title(json["mode"]!.GetValue<string>())}: {LegSnapshots.PlaceName(json, "from")} to {LegSnapshots.PlaceName(json, "to")}", 12, true, Margin);
            Text($"Departs {depart:ddd d MMM yyyy HH:mm}  |  Arrives {arrive:HH:mm}  (Maldives time, UTC+5)", 10, false, Margin);
            var reference = leg.ProviderReference is null ? "" : $"  |  Ref {leg.ProviderReference}";
            Text($"Fare MVR {leg.AmountMinor / 100}.{leg.AmountMinor % 100:00}{reference}{(leg.Bookable ? "" : "  |  pay on board")}", 10, false, Margin, 0.35);
            y -= 6;
        }

        Text($"Total paid: {BookingNotifier.Format(booking.TotalMinor, booking.Currency)}", 12, true, Margin);
        y -= 8;

        if (ticketPayload is not null)
        {
            var passengers = booking.Passengers.ToDictionary(p => p.Id);
            var legs = booking.Legs.ToDictionary(l => l.Id);
            foreach (var ticket in booking.Tickets.Where(t => t.Status == TicketStatus.Valid).OrderBy(t => legs[t.BookingLegId].LegIndex))
            {
                Need(150);
                var leg = legs[ticket.BookingLegId];
                var json = JsonNode.Parse(leg.LegJson)!.AsObject();
                var top = y;
                Text($"Ticket - leg {leg.LegIndex + 1}", 11, true, Margin + 140);
                Text(passengers[ticket.PassengerId].FullName, 10, false, Margin + 140);
                Text($"{LegSnapshots.PlaceName(json, "from")} to {LegSnapshots.PlaceName(json, "to")}", 10, false, Margin + 140);
                Text(ticket.Id, 8, false, Margin + 140, 0.4);
                DrawQr(page, QrEncoder.Encode(ticketPayload(ticket)), Margin, top, 120);
                y = Math.Min(y, top - 130);
            }
        }

        Text("Show the QR code at boarding. Times are Indian/Maldives (UTC+5). Sea routes are illustrative, never navigational.", 8, false, Margin, 0.4);
        return Assemble(pages);
    }

    private static StringBuilder NewPage(List<StringBuilder> pages)
    {
        var page = new StringBuilder();
        pages.Add(page);
        return page;
    }

    private static void DrawQr(StringBuilder page, bool[,] modules, double x, double top, double size)
    {
        var n = modules.GetLength(0);
        var cell = size / (n + 2);
        page.Append("0 g\n");
        for (var row = 0; row < n; row++)
        {
            for (var col = 0; col < n; col++)
            {
                if (modules[row, col])
                {
                    page.Append(CultureInfo.InvariantCulture,
                        $"{x + (col + 1) * cell:0.###} {top - (row + 2) * cell:0.###} {cell:0.###} {cell:0.###} re f\n");
                }
            }
        }
    }

    private static string Title(string snake)
    {
        var text = snake.Replace('_', ' ');
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string Escape(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text.Replace("→", "->"))
        {
            switch (ch)
            {
                case '(' or ')' or '\\':
                    sb.Append('\\').Append(ch);
                    break;
                case < ' ':
                    sb.Append(' ');
                    break;
                case > 'ÿ':
                    sb.Append('?');
                    break;
                case > '~':
                    sb.Append('\\').Append(Convert.ToString(ch, 8).PadLeft(3, '0'));
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }
        return sb.ToString();
    }

    private static byte[] Assemble(List<StringBuilder> pages)
    {
        var latin1 = Encoding.Latin1;
        var objects = new List<byte[]>();
        // 1 catalog, 2 pages, 3 F1, 4 F2, then page/content pairs.
        var pageObjectIds = Enumerable.Range(0, pages.Count).Select(i => 5 + i * 2).ToList();
        objects.Add(latin1.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"));
        objects.Add(latin1.GetBytes($"<< /Type /Pages /Kids [{string.Join(' ', pageObjectIds.Select(id => $"{id} 0 R"))}] /Count {pages.Count} >>"));
        objects.Add(latin1.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
        objects.Add(latin1.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"));
        for (var i = 0; i < pages.Count; i++)
        {
            var content = latin1.GetBytes(pages[i].ToString());
            objects.Add(latin1.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {6 + i * 2} 0 R >>"));
            var stream = new List<byte>(latin1.GetBytes($"<< /Length {content.Length} >>\nstream\n"));
            stream.AddRange(content);
            stream.AddRange(latin1.GetBytes("\nendstream"));
            objects.Add([.. stream]);
        }

        using var ms = new MemoryStream();
        void Write(string s) => ms.Write(latin1.GetBytes(s));
        Write("%PDF-1.4\n");
        var offsets = new List<long>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(ms.Position);
            Write($"{i + 1} 0 obj\n");
            ms.Write(objects[i]);
            Write("\nendobj\n");
        }

        var xref = ms.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            Write($"{offset:D10} 00000 n \n");
        }
        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }
}
