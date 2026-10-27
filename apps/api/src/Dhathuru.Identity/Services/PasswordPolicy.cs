using Dhathuru.Identity.Options;

namespace Dhathuru.Identity.Services;

/// <summary>
/// Length based password policy (NIST SP 800-63B style): no composition rules, but a minimum length,
/// a maximum that protects the hasher, and rejection of the most common choices and of the account's own email.
/// A breached-password lookup can be added behind this interface without touching callers.
/// </summary>
public static class PasswordPolicy
{
    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password1", "password12", "password123", "passw0rd123", "1234567890", "12345678910", "0123456789",
        "qwertyuiop", "qwerty12345", "qwerty123456", "1q2w3e4r5t", "1qaz2wsx3edc", "iloveyou123", "iloveyou12",
        "abcdefghij", "abcd123456", "letmein1234", "welcome123", "welcome1234", "admin12345", "administrator",
        "changeme123", "trustno1234", "monkey12345", "dragon12345", "sunshine123", "princess123", "football123",
        "baseball123", "superman123", "123456789a", "a123456789", "1234567890a", "maldives123", "maldives1234",
        "dhathuru123", "dhathuru1234", "malemaldives", "heyvalla123", "0000000000", "1111111111", "aaaaaaaaaa",
        "password!", "p@ssw0rd123", "p@ssword123", "passwordpassword", "123123123123", "11111111111", "1234512345",
    };

    public static IReadOnlyList<string> Check(string? password, string? email, AuthOptions options)
    {
        var problems = new List<string>();
        if (string.IsNullOrEmpty(password) || password.Length < options.PasswordMinLength)
        {
            problems.Add("PASSWORD_TOO_SHORT");
            return problems;
        }

        if (password.Length > options.PasswordMaxLength)
        {
            problems.Add("PASSWORD_TOO_LONG");
            return problems;
        }

        if (Common.Contains(password) || password.Distinct().Count() <= 2)
        {
            problems.Add("PASSWORD_TOO_COMMON");
        }

        if (!string.IsNullOrEmpty(email))
        {
            var local = email.Split('@')[0];
            if (local.Length >= 4 && password.Contains(local, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add("PASSWORD_CONTAINS_EMAIL");
            }
        }

        return problems;
    }
}
