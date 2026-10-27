using System.Net;
using System.Net.Mail;
using System.Threading.Channels;
using Dhathuru.Domain.Seams;
using Microsoft.Extensions.Options;

namespace Dhathuru.Api.Email;

/// <summary>The part that actually talks to a mail server. Tests and future providers replace it.</summary>
public interface IEmailTransport
{
    Task SendAsync(string to, RenderedEmail email, IReadOnlyList<EmailAttachment>? attachments, CancellationToken ct);
}

/// <summary>SMTP transport (Mailpit in dev).</summary>
public sealed class SmtpEmailTransport(IOptions<EmailOptions> options) : IEmailTransport
{
    public async Task SendAsync(string to, RenderedEmail email, IReadOnlyList<EmailAttachment>? attachments, CancellationToken ct)
    {
        var o = options.Value;
        using var message = new MailMessage { From = new MailAddress(o.FromAddress, o.FromName), Subject = email.Subject };
        message.To.Add(new MailAddress(to));
        message.Body = email.Text;
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(email.Html, null, "text/html"));
        var streams = new List<MemoryStream>();
        try
        {
            foreach (var a in attachments ?? [])
            {
                var stream = new MemoryStream(a.Content.ToArray());
                streams.Add(stream);
                message.Attachments.Add(new Attachment(stream, a.FileName, a.ContentType));
            }

            using var client = new SmtpClient(o.Host, o.Port) { EnableSsl = o.EnableSsl };
            if (!string.IsNullOrEmpty(o.Username)) client.Credentials = new NetworkCredential(o.Username, o.Password);
            await client.SendMailAsync(message, ct);
        }
        finally
        {
            foreach (var s in streams) await s.DisposeAsync();
        }
    }
}

/// <summary>Writes only the template code to the log. For environments with no mail server.</summary>
public sealed class LogEmailTransport(ILogger<LogEmailTransport> logger) : IEmailTransport
{
    public Task SendAsync(string to, RenderedEmail email, IReadOnlyList<EmailAttachment>? attachments, CancellationToken ct)
    {
        logger.LogInformation("Email suppressed by the log transport (subject '{Subject}')", email.Subject);
        return Task.CompletedTask;
    }
}

/// <summary>
/// The <see cref="IEmailSender"/> other modules use. It only enqueues, so request time never depends on the mail server
/// (and cannot reveal whether an address exists). Messages still queued when the process stops are lost; v1 accepts that.
/// </summary>
public sealed class QueuedEmailSender : IEmailSender
{
    private readonly Channel<EmailMessage> channel;

    public QueuedEmailSender(IOptions<EmailOptions> options) =>
        channel = Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(options.Value.QueueCapacity) { FullMode = BoundedChannelFullMode.DropWrite });

    public ChannelReader<EmailMessage> Reader => channel.Reader;

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (!EmailTemplates.Knows(message.TemplateCode))
        {
            throw new ArgumentException($"Unknown email template '{message.TemplateCode}'.", nameof(message));
        }

        channel.Writer.TryWrite(message);
        return Task.CompletedTask;
    }
}

/// <summary>Drains the queue with a few retries. Logs template code and attempt, never the address or content.</summary>
public sealed class EmailDeliveryService(
    QueuedEmailSender queue,
    IEmailTransport transport,
    IOptions<EmailOptions> options,
    ILogger<EmailDeliveryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
        {
            await DeliverAsync(message, stoppingToken);
        }
    }

    private async Task DeliverAsync(EmailMessage message, CancellationToken ct)
    {
        var rendered = EmailTemplates.Render(message);
        for (var attempt = 1; attempt <= options.Value.MaxAttempts; attempt++)
        {
            try
            {
                await transport.SendAsync(message.To, rendered, message.Attachments, ct);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Email {Template} failed on attempt {Attempt}: {ErrorType}", message.TemplateCode, attempt, ex.GetType().Name);
                if (attempt < options.Value.MaxAttempts) await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
        }

        logger.LogError("Email {Template} could not be delivered after {Attempts} attempts", message.TemplateCode, options.Value.MaxAttempts);
    }
}
