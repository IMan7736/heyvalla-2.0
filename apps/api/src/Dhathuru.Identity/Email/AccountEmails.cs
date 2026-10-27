using Dhathuru.Domain.Seams;
using Dhathuru.Identity.Options;
using Microsoft.Extensions.Options;

namespace Dhathuru.Identity.Email;

/// <summary>Template codes understood by the email sender (rendered and localised there).</summary>
public static class EmailTemplateCodes
{
    public const string VerifyEmail = "account.verify_email";
    public const string AccountExists = "account.already_registered";
    public const string ResetPassword = "account.reset_password";
    public const string PasswordChanged = "account.password_changed";
    public const string ConfirmNewEmail = "account.confirm_new_email";
    public const string EmailChanged = "account.email_changed";
    public const string AccountDeleted = "account.deleted";
}

/// <summary>Builds the account emails. Links point at the web app; the token is the only secret in the data.</summary>
public sealed class AccountEmails(IEmailSender sender, IOptions<AuthOptions> options)
{
    private readonly AuthOptions options = options.Value;

    public Task SendVerifyEmailAsync(AppUser user, string token, CancellationToken ct) =>
        Send(user.Email!, user.Locale, EmailTemplateCodes.VerifyEmail, ct, ("name", user.DisplayName ?? ""), ("link", Link("verify-email", token)));

    public Task SendAccountExistsAsync(AppUser user, CancellationToken ct) =>
        Send(user.Email!, user.Locale, EmailTemplateCodes.AccountExists, ct, ("name", user.DisplayName ?? ""), ("link", Link("forgot-password")));

    public Task SendResetPasswordAsync(AppUser user, string token, CancellationToken ct) =>
        Send(user.Email!, user.Locale, EmailTemplateCodes.ResetPassword, ct, ("name", user.DisplayName ?? ""), ("link", Link("reset-password", token)));

    public Task SendPasswordChangedAsync(AppUser user, CancellationToken ct) =>
        Send(user.Email!, user.Locale, EmailTemplateCodes.PasswordChanged, ct, ("name", user.DisplayName ?? ""), ("link", Link("forgot-password")));

    public Task SendConfirmNewEmailAsync(AppUser user, string newEmail, string token, CancellationToken ct) =>
        Send(newEmail, user.Locale, EmailTemplateCodes.ConfirmNewEmail, ct, ("name", user.DisplayName ?? ""), ("link", Link("verify-email", token)));

    public Task SendEmailChangedAsync(string oldEmail, string locale, string? name, CancellationToken ct) =>
        Send(oldEmail, locale, EmailTemplateCodes.EmailChanged, ct, ("name", name ?? ""), ("link", Link("forgot-password")));

    public Task SendAccountDeletedAsync(string email, string locale, string? name, CancellationToken ct) =>
        Send(email, locale, EmailTemplateCodes.AccountDeleted, ct, ("name", name ?? ""));

    private string Link(string path, string? token = null)
    {
        var url = $"{options.WebBaseUrl.TrimEnd('/')}/{path}";
        return token is null ? url : $"{url}?token={Uri.EscapeDataString(token)}";
    }

    private Task Send(string to, string locale, string code, CancellationToken ct, params (string Key, string Value)[] data) =>
        sender.SendAsync(new EmailMessage(to, code, locale, data.ToDictionary(d => d.Key, d => d.Value)), ct);
}
