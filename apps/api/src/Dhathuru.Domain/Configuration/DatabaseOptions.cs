namespace Dhathuru.Domain.Configuration;

/// <summary>
/// Postgres connection settings bound from the <c>Database</c> configuration section.
/// Host, port and user have local defaults; the password comes only from user-secrets or the environment
/// (<c>Database__Password</c>). Each agent picks its own database with <c>Database__Name</c>.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5432;

    public string Name { get; set; } = "dhathuru_main";

    public string Username { get; set; } = "dhathuru";

    public string? Password { get; set; }

    public string ToConnectionString()
    {
        if (string.IsNullOrWhiteSpace(Password))
            throw new InvalidOperationException(
                "Database:Password is not set. Run scripts/dev-setup.sh or set Database__Password (never commit it).");
        return $"Host={Quote(Host)};Port={Port};Database={Quote(Name)};Username={Quote(Username)};Password={Quote(Password)}";
    }

    private static string Quote(string value) =>
        value.Contains(';') || value.Contains('"') || value.Contains('\'') || value.Contains('=')
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
}
