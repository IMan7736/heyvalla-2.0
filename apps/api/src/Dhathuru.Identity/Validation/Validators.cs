using System.Text.RegularExpressions;
using Dhathuru.Identity.Entities;
using Dhathuru.Identity.Options;
using Dhathuru.Identity.Services;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Dhathuru.Identity.Validation;

internal static partial class Rules
{
    public static void Email<T>(this IRuleBuilder<T, string?> rule)
    {
        rule.NotEmpty().WithErrorCode("REQUIRED");
        rule.MaximumLength(254).WithErrorCode("TOO_LONG");
        rule.Must(v => string.IsNullOrEmpty(v) || v.Length > 254 || IsEmail(v)).WithErrorCode("EMAIL_INVALID");
    }

    public static bool IsEmail(string? value) =>
        value is not null && value.Length <= 254 && EmailPattern().IsMatch(value) && !value.Contains("..", StringComparison.Ordinal);

    public static void Password<T>(this IRuleBuilder<T, string?> rule, AuthOptions options, Func<T, string?>? email = null) =>
        rule.Custom((value, context) =>
        {
            foreach (var code in PasswordPolicy.Check(value, email?.Invoke(context.InstanceToValidate), options))
            {
                context.AddFailure(new FluentValidation.Results.ValidationFailure(context.PropertyPath, code) { ErrorCode = code });
            }
        });

    public static void Token<T>(this IRuleBuilder<T, string?> rule)
    {
        rule.NotEmpty().WithErrorCode("REQUIRED");
        rule.MaximumLength(256).WithErrorCode("TOKEN_INVALID");
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^\+?[0-9 ()\-]{7,20}$", RegexOptions.CultureInvariant)]
    public static partial Regex PhonePattern();
}

internal sealed class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public RegisterValidator(IOptions<AuthOptions> options)
    {
        var auth = options.Value;
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Password).Password(auth, x => x.Email);
        RuleFor(x => x.DisplayName).MaximumLength(80).WithErrorCode("TOO_LONG");
        RuleFor(x => x.Locale).Must(l => l is null || auth.SupportedLocales.Contains(l)).WithErrorCode("LOCALE_UNSUPPORTED");
    }
}

internal sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithErrorCode("REQUIRED").MaximumLength(254).WithErrorCode("TOO_LONG");
        RuleFor(x => x.Password).NotEmpty().WithErrorCode("REQUIRED").MaximumLength(1024).WithErrorCode("TOO_LONG");
    }
}

internal sealed class RefreshValidator : AbstractValidator<RefreshRequest>
{
    public RefreshValidator() => RuleFor(x => x.RefreshToken).NotEmpty().WithErrorCode("REQUIRED").MaximumLength(256).WithErrorCode("TOKEN_INVALID");
}

internal sealed class TokenValidator : AbstractValidator<TokenRequest>
{
    public TokenValidator() => RuleFor(x => x.Token).Token();
}

internal sealed class EmailRequestValidator : AbstractValidator<EmailRequest>
{
    public EmailRequestValidator() => RuleFor(x => x.Email).Email();
}

internal sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordValidator(IOptions<AuthOptions> options)
    {
        RuleFor(x => x.Token).Token();
        RuleFor(x => x.NewPassword).Password(options.Value);
    }
}

internal sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordValidator(IOptions<AuthOptions> options)
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithErrorCode("REQUIRED").MaximumLength(1024).WithErrorCode("TOO_LONG");
        RuleFor(x => x.NewPassword).Password(options.Value);
    }
}

internal sealed class ChangeEmailValidator : AbstractValidator<ChangeEmailRequest>
{
    public ChangeEmailValidator()
    {
        RuleFor(x => x.NewEmail).Email();
        RuleFor(x => x.Password).NotEmpty().WithErrorCode("REQUIRED").MaximumLength(1024).WithErrorCode("TOO_LONG");
    }
}

internal sealed class DeleteAccountValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountValidator() => RuleFor(x => x.Password).NotEmpty().WithErrorCode("REQUIRED").MaximumLength(1024).WithErrorCode("TOO_LONG");
}

internal sealed class UpdateMeValidator : AbstractValidator<UpdateMeRequest>
{
    public UpdateMeValidator(IOptions<AuthOptions> options)
    {
        RuleFor(x => x.DisplayName).MaximumLength(80).WithErrorCode("TOO_LONG");
        RuleFor(x => x.Locale).Must(l => l is null || options.Value.SupportedLocales.Contains(l)).WithErrorCode("LOCALE_UNSUPPORTED");
    }
}

internal sealed class UpdatePreferencesValidator : AbstractValidator<UpdatePreferencesRequest>
{
    public static readonly IReadOnlyList<string> Modes =
        ["ferry", "speedboat", "seaplane", "domestic_flight", "bus", "taxi", "walk", "resort_transfer"];

    public UpdatePreferencesValidator(IOptions<AuthOptions> options)
    {
        RuleFor(x => x.Language).Must(l => l is null || options.Value.SupportedLocales.Contains(l)).WithErrorCode("LOCALE_UNSUPPORTED");
        RuleFor(x => x.RiskTolerance).Must(r => r is null || RiskToleranceValues.All.Contains(r)).WithErrorCode("RISK_TOLERANCE_INVALID");
        RuleFor(x => x.PreferredModes)
            .Must(m => m is null || m.Count <= AppUser.MaxPreferredModes).WithErrorCode("TOO_MANY")
            .Must(m => m is null || m.All(Modes.Contains)).WithErrorCode("MODE_INVALID");
    }
}

internal sealed class TravellerInputValidator : AbstractValidator<TravellerInput>
{
    private static readonly string[] PassengerTypes = ["adult", "child", "infant"];

    public TravellerInputValidator(TimeProvider time)
    {
        RuleFor(x => x.FullName).NotEmpty().WithErrorCode("REQUIRED").MaximumLength(120).WithErrorCode("TOO_LONG");
        RuleFor(x => x.PassengerType).NotEmpty().WithErrorCode("REQUIRED")
            .Must(t => t is null || PassengerTypes.Contains(t)).WithErrorCode("PASSENGER_TYPE_INVALID");
        RuleFor(x => x.Phone).Must(p => p is null || Rules.PhonePattern().IsMatch(p)).WithErrorCode("PHONE_INVALID");
        RuleFor(x => x.DocumentType).Must(d => d is null || DocumentTypes.All.Contains(d)).WithErrorCode("DOCUMENT_TYPE_INVALID");
        RuleFor(x => x.DateOfBirth).Must(d => d is null || (d <= DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime) && d.Value.Year >= 1900))
            .WithErrorCode("DATE_OF_BIRTH_INVALID");
    }
}
