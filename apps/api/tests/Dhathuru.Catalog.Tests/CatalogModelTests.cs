using Dhathuru.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Dhathuru.Catalog.Tests;

public class CatalogModelTests
{
    [Fact]
    public void Context_uses_its_own_schema_and_builds_without_a_database()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=unused;Database=unused", n => n.UseNetTopologySuite())
            .Options;
        using var db = new CatalogDbContext(options);
        Assert.Equal("catalog", db.Model.GetDefaultSchema());
    }
}
