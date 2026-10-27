using Dhathuru.Api.Email;
using Dhathuru.Api.Infrastructure;
using Dhathuru.Domain.Seams;

namespace Dhathuru.Api.Tests;

public class EmailTemplateTests
{
    private static EmailMessage Message(string code, string locale = "en", string? name = "Aishath", string link = "https://app.example/verify-email?token=abc") =>
        new("a@example.com", code, locale, new Dictionary<string, string> { ["name"] = name ?? "", ["link"] = link });

    [Theory]
    [InlineData("account.verify_email")]
    [InlineData("account.already_registered")]
    [InlineData("account.reset_password")]
    [InlineData("account.password_changed")]
    [InlineData("account.confirm_new_email")]
    [InlineData("account.email_changed")]
    [InlineData("account.deleted")]
    public void Every_account_template_renders_text_and_html(string code)
    {
        var email = EmailTemplates.Render(Message(code));
        Assert.False(string.IsNullOrWhiteSpace(email.Subject));
        Assert.Contains("Aishath", email.Text, StringComparison.Ordinal);
        Assert.Contains("<html", email.Html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("booking.confirmed", "Your trip is booked")]
    [InlineData("booking.failed", "We could not complete your booking")]
    [InlineData("booking.cancelled", "Your booking was cancelled")]
    [InlineData("booking.disruption", "A service on your trip changed")]
    public void Booking_templates_render_their_details_escaped(string code, string subject)
    {
        var message = new EmailMessage("a@example.com", code, "en", new Dictionary<string, string>
        {
            ["name"] = "Aishath", ["link"] = "https://app.example/trips/bkg_1", ["bookingId"] = "bkg_01ABC",
            ["total"] = "MVR 4,520.00", ["route"] = "Maafushi to <Kooddoo>", ["refund"] = "MVR 465.00",
            ["failureCode"] = "LEG_UNAVAILABLE", ["kind"] = "delayed", ["delayMinutes"] = "40", ["legNumber"] = "2",
        });
        var email = EmailTemplates.Render(message);
        Assert.Equal(subject, email.Subject);
        Assert.Contains("bkg_01ABC", email.Text, StringComparison.Ordinal);
        Assert.Contains("Maafushi to &lt;Kooddoo&gt;", email.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<Kooddoo>", email.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Details_with_no_value_are_left_out()
    {
        var email = EmailTemplates.Render(new EmailMessage("a@example.com", "booking.cancelled", "en", new Dictionary<string, string> { ["bookingId"] = "bkg_1" }));
        Assert.Contains("Booking: bkg_1", email.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Refund:", email.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_link_appears_in_both_bodies_and_values_are_html_escaped()
    {
        var email = EmailTemplates.Render(Message("account.verify_email", name: "<script>x</script>", link: "https://app.example/v?token=a&b=<c>"));
        Assert.Contains("https://app.example/v?token=a&b=<c>", email.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", email.Html, StringComparison.Ordinal);
        Assert.Contains("a&amp;b=&lt;c&gt;", email.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_locales_fall_back_to_english_and_unknown_templates_fail_loudly()
    {
        Assert.Equal(EmailTemplates.Render(Message("account.deleted")).Subject, EmailTemplates.Render(Message("account.deleted", locale: "dv")).Subject);
        Assert.Throws<InvalidOperationException>(() => EmailTemplates.Render(Message("nope")));
        Assert.False(EmailTemplates.Knows("nope"));
    }

    [Fact]
    public async Task The_queued_sender_rejects_unknown_templates_up_front()
    {
        var sender = new QueuedEmailSender(Microsoft.Extensions.Options.Options.Create(new EmailOptions()));
        await Assert.ThrowsAsync<ArgumentException>(() => sender.SendAsync(Message("nope"), TestContext.Current.CancellationToken));
        await sender.SendAsync(Message("account.deleted"), TestContext.Current.CancellationToken);
        Assert.True(sender.Reader.TryRead(out var queued));
        Assert.Equal("account.deleted", queued!.TemplateCode);
    }
}

public class PiiRedactionTests
{
    [Theory]
    [InlineData("hello aishath@example.com there", "hello a***@example.com there")]
    [InlineData("Authorization: Bearer eyJhbGciOi.abc-def_ghi", "Authorization: Bearer [redacted]")]
    [InlineData("nothing to see", "nothing to see")]
    public void Emails_and_bearer_tokens_are_masked(string input, string expected) =>
        Assert.Equal(expected, PiiRedactionEnricher.Redact(input));
}
