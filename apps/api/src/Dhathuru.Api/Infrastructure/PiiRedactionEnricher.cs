using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Dhathuru.Api.Infrastructure;

/// <summary>
/// Last line of defence against personal data in logs: email addresses are masked and bearer tokens removed from any
/// string property or rendered value. Code should not log these in the first place; this catches mistakes.
/// </summary>
public sealed partial class PiiRedactionEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties.ToList())
        {
            if (property.Value is ScalarValue { Value: string text })
            {
                var redacted = Redact(text);
                if (!ReferenceEquals(redacted, text) && redacted != text)
                {
                    logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, new ScalarValue(redacted)));
                }
            }
        }
    }

    public static string Redact(string text)
    {
        text = EmailPattern().Replace(text, m => MaskEmail(m.Value));
        return BearerPattern().Replace(text, "Bearer [redacted]");
    }

    public static string MaskEmail(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at <= 0 ? "[email]" : $"{email[0]}***{email[at..]}";
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex BearerPattern();
}
