using Dhathuru.Catalog.Data;

namespace Dhathuru.Catalog.Tests;

internal static class TestSupport
{
    /// <summary>The real seed directory of the repo (apps/api/seed), found by walking up from the test binary.</summary>
    public static string SeedDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "seed", SeedFiles.CatalogFile);
            if (File.Exists(candidate)) return Path.GetDirectoryName(candidate)!;
            var nested = Path.Combine(dir.FullName, "apps", "api", "seed", SeedFiles.CatalogFile);
            if (File.Exists(nested)) return Path.GetDirectoryName(nested)!;
        }
        throw new DirectoryNotFoundException("apps/api/seed not found above " + AppContext.BaseDirectory);
    }

    public static CatalogData LoadSeed() => SeedFiles.Load(SeedDirectory());

    public static CatalogIndexFactory Index(CatalogData data) => new(data);

    public sealed class CatalogIndexFactory(CatalogData data)
    {
        public Services.CatalogIndex Build() => new(data);
    }
}

/// <summary>Manually advanced clock for deterministic tests.</summary>
internal sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now += by;
}
