using Dhathuru.Domain.Seams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dhathuru.Identity;

internal sealed class AccountsMigrator(IServiceScopeFactory scopes) : IModuleMigrator
{
    public string Name => "identity";

    public async Task MigrateAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AccountsDbContext>().Database.MigrateAsync(ct);
    }
}
