namespace Dhathuru.Domain.Seams;

/// <summary>Email seam (DHONI implements: Mailpit in dev, Resend later).</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

/// <summary>Templates are identified by code; the sender renders them. <c>Data</c> must not carry secrets beyond the link the user needs.</summary>
public sealed record EmailMessage(
    string To,
    string TemplateCode,
    string Locale,
    IReadOnlyDictionary<string, string> Data,
    IReadOnlyList<EmailAttachment>? Attachments = null);

public sealed record EmailAttachment(string FileName, string ContentType, ReadOnlyMemory<byte> Content);
