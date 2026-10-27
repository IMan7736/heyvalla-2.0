namespace Dhathuru.Api.Email;

/// <summary>
/// <c>Email</c> configuration section. Development points at Mailpit (SMTP 1025, UI 8025).
/// A Resend adapter would implement <see cref="IEmailTransport"/> and be selected with <c>Provider</c>; it is documented, not wired.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>smtp | log. "log" renders and logs the template code only (never the message), for environments without a mail server.</summary>
    public string Provider { get; set; } = "smtp";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    public bool EnableSsl { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = "no-reply@dhathuru.mv";

    public string FromName { get; set; } = "Dhathuru";

    public int QueueCapacity { get; set; } = 1000;

    public int MaxAttempts { get; set; } = 3;
}
