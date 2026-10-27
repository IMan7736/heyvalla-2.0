using Dhathuru.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dhathuru.Identity.Tests;

public class AccountsModelTests
{
    [Fact]
    public void Context_uses_its_own_schema_and_builds_without_a_database()
    {
        var options = new DbContextOptionsBuilder<AccountsDbContext>().UseNpgsql("Host=unused;Database=unused").Options;
        using var db = new AccountsDbContext(options);
        Assert.Equal("identity", db.Model.GetDefaultSchema());
        Assert.NotNull(db.Model.FindEntityType(typeof(AppUser)));
    }
}
