using System.Net;
using Dhathuru.Domain.Seams;

namespace Dhathuru.Api.Email;

public sealed record RenderedEmail(string Subject, string Text, string Html);

/// <summary>
/// Renders emails by template code and locale. English is the fallback for any locale without its own table,
/// so adding a language is adding one more entry to <see cref="Catalogue"/>.
/// </summary>
public static class EmailTemplates
{
    private sealed record Detail(string Label, string Key);

    private sealed record Template(string Subject, string Heading, string Body, string? ButtonLabel, string Footer, Detail[]? Details = null);

    private static readonly Dictionary<string, Dictionary<string, Template>> Catalogue = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new(StringComparer.Ordinal)
        {
            ["account.verify_email"] = new("Confirm your email for Dhathuru", "Confirm your email",
                "Welcome to Dhathuru. Please confirm this address so we can send your tickets and trip updates here.", "Confirm email",
                "If you did not create an account you can ignore this message. The link works once and expires in 24 hours."),
            ["account.already_registered"] = new("You already have a Dhathuru account", "You already have an account",
                "Someone tried to register with this address, but an account already exists. If it was you, you can sign in or reset your password.", "Reset password",
                "If it was not you, no action is needed. Your account has not been changed."),
            ["account.reset_password"] = new("Reset your Dhathuru password", "Reset your password",
                "We received a request to reset your password. Use the button below to choose a new one.", "Choose a new password",
                "If you did not ask for this you can ignore it. The link works once and expires in one hour."),
            ["account.password_changed"] = new("Your Dhathuru password was changed", "Password changed",
                "The password for your account was just changed and you were signed out of your other devices.", "Secure my account",
                "If this was not you, reset your password now using the button above."),
            ["account.confirm_new_email"] = new("Confirm your new email for Dhathuru", "Confirm your new email",
                "You asked to use this address for your Dhathuru account. Confirm to complete the change.", "Confirm new email",
                "If you did not ask for this you can ignore it. Your account email stays the same until you confirm."),
            ["account.email_changed"] = new("Your Dhathuru email was changed", "Email changed",
                "The email address on your Dhathuru account was changed. This address no longer receives account emails.", "Secure my account",
                "If this was not you, contact us and reset your password immediately."),
            ["account.deleted"] = new("Your Dhathuru account was deleted", "Account deleted",
                "Your account and personal data have been deleted. Anonymised booking records may be kept for accounting.", null,
                "Thank you for travelling with us."),
            ["booking.confirmed"] = new("Your trip is booked", "Your trip is booked",
                "Your booking is confirmed and every leg is ticketed. Your itinerary is attached as a PDF, and your tickets are in the app.", "View my trip",
                "Please arrive at each departure point before its check-in cut-off. Keep your tickets handy.",
                [new("Route", "route"), new("Total paid", "total"), new("Booking", "bookingId")]),
            ["booking.failed"] = new("We could not complete your booking", "We could not complete your booking",
                "One of the services on your trip could not be confirmed, so nothing has been booked. Any payment has been refunded to your card.", "Plan another trip",
                "Refunds can take a few days to appear, depending on your bank.",
                [new("Route", "route"), new("Amount", "total"), new("Booking", "bookingId"), new("Reason code", "failureCode")]),
            ["booking.cancelled"] = new("Your booking was cancelled", "Your booking was cancelled",
                "Your booking has been cancelled as requested.", "View booking",
                "Refunds can take a few days to appear, depending on your bank.",
                [new("Route", "route"), new("Refund", "refund"), new("Booking", "bookingId")]),
            ["booking.disruption"] = new("A service on your trip changed", "A service on your trip changed",
                "A service on your trip has been delayed or cancelled. Open your trip to see what changed and a suggested safe re-plan.", "Review my trip",
                "We only suggest changes that keep every connection safe.",
                [new("Route", "route"), new("Change", "kind"), new("Delay (minutes)", "delayMinutes"), new("Leg", "legNumber"), new("Booking", "bookingId")]),
        },
    };

    public static bool Knows(string code) => Catalogue["en"].ContainsKey(code);

    public static RenderedEmail Render(EmailMessage message)
    {
        var table = Catalogue.TryGetValue(message.Locale, out var found) ? found : Catalogue["en"];
        if (!table.TryGetValue(message.TemplateCode, out var template) && !Catalogue["en"].TryGetValue(message.TemplateCode, out template))
        {
            throw new InvalidOperationException($"Unknown email template '{message.TemplateCode}'.");
        }

        message.Data.TryGetValue("name", out var name);
        message.Data.TryGetValue("link", out var link);
        var greeting = string.IsNullOrWhiteSpace(name) ? "Hello," : $"Hello {name},";

        var button = template.ButtonLabel is not null && link is not null
            ? $"""<p style="margin:28px 0"><a href="{WebUtility.HtmlEncode(link)}" style="background:#087F8C;color:#FFFFFF;text-decoration:none;padding:12px 22px;border-radius:10px;font-weight:600;display:inline-block">{WebUtility.HtmlEncode(template.ButtonLabel)}</a></p><p style="font-size:13px;color:#456068">Or copy this link: <br><span style="word-break:break-all">{WebUtility.HtmlEncode(link)}</span></p>"""
            : "";
        var details = (template.Details ?? [])
            .Where(d => message.Data.TryGetValue(d.Key, out var v) && !string.IsNullOrWhiteSpace(v))
            .Select(d => (d.Label, Value: message.Data[d.Key]))
            .ToList();
        var detailText = details.Count == 0 ? "" : string.Join("\n", details.Select(d => $"{d.Label}: {d.Value}")) + "\n\n";
        var detailHtml = details.Count == 0
            ? ""
            : "<table style=\"margin:20px 0 0;border-collapse:collapse;width:100%\">"
              + string.Concat(details.Select(d => $"<tr><td style=\"padding:6px 12px 6px 0;color:#456068;font-size:14px\">{WebUtility.HtmlEncode(d.Label)}</td><td style=\"padding:6px 0;font-weight:600\">{WebUtility.HtmlEncode(d.Value)}</td></tr>"))
              + "</table>";
        var text = $"{greeting}\n\n{template.Body}\n\n{detailText}{(template.ButtonLabel is not null && link is not null ? $"{template.ButtonLabel}: {link}\n\n" : "")}{template.Footer}\n\nDhathuru";
        var html = $"""
            <!doctype html><html><body style="margin:0;background:#FAF7F0;font-family:system-ui,-apple-system,Segoe UI,sans-serif;color:#12343B">
            <div style="max-width:520px;margin:0 auto;padding:32px 20px">
            <p style="font-size:20px;font-weight:700;letter-spacing:.02em;margin:0 0 20px">Dhathuru</p>
            <div style="background:#FFFFFF;border-radius:16px;padding:28px">
            <h1 style="font-size:20px;margin:0 0 12px">{WebUtility.HtmlEncode(template.Heading)}</h1>
            <p style="margin:0 0 8px">{WebUtility.HtmlEncode(greeting)}</p>
            <p style="margin:0">{WebUtility.HtmlEncode(template.Body)}</p>
            {detailHtml}
            {button}
            <p style="font-size:13px;color:#456068;margin:20px 0 0">{WebUtility.HtmlEncode(template.Footer)}</p>
            </div></div></body></html>
            """;
        return new RenderedEmail(template.Subject, text, html);
    }
}
