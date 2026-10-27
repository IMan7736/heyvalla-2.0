using Microsoft.Extensions.Configuration;

namespace Dhathuru.Domain.Configuration;

public static class DatabaseOptionsExtensions
{
    public static DatabaseOptions GetDatabaseOptions(this IConfiguration configuration) =>
        configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
}
