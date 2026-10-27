namespace Dhathuru.DataTools.Commands;

/// <summary>Parsed <c>--name value</c> options plus the resolved repo paths every command needs.</summary>
public sealed class CliContext
{
    private readonly Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase);

    public CliContext(IEnumerable<string> args, TextWriter output, TextWriter error)
    {
        Output = output;
        Error = error;
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            if (!list[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Unexpected argument '{list[i]}'. Options look like --name value.");
            var name = list[i][2..];
            options[name] = i + 1 < list.Count && !list[i + 1].StartsWith("--", StringComparison.Ordinal) ? list[++i] : "true";
        }
    }

    public TextWriter Output { get; }

    public TextWriter Error { get; }

    public string RepoRoot { get; init; } = FindRepoRoot(Directory.GetCurrentDirectory());

    public string? Option(string name) => options.GetValueOrDefault(name);

    public string DatasetsDir => Option("datasets") ?? Path.Combine(RepoRoot, "datasets");

    public string SeedDir => Option("seed") ?? Path.Combine(RepoRoot, "apps", "api", "seed");

    public string CacheDir => Option("cache") ?? Path.Combine(RepoRoot, "apps", "api", ".artifacts", "kandu", "cache");

    public static string FindRepoRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "pnpm-workspace.yaml"))) return dir.FullName;
        }
        return start;
    }
}
