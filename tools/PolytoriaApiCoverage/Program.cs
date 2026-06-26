namespace Vrs.Tools.PolytoriaApiCoverage;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = CoverageToolOptionsParser.Parse(args, Environment.CurrentDirectory);
            var result = await new PolytoriaApiCoverageRunner().RunAsync(options, CancellationToken.None);
            Console.WriteLine($"Generated {Path.Combine(options.OutputDirectory, "API_COVERAGE.md")}");
            Console.WriteLine($"Catalog nodes: {result.Catalog.TotalNodes}; official types: {result.Source.Types.Count}; official enums: {result.Source.Enums.Count}");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or HttpRequestException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}

public static class CoverageToolOptionsParser
{
    public static CoverageToolOptions Parse(IReadOnlyList<string> args, string startDirectory)
    {
        var repositoryRoot = FindRepositoryRoot(startDirectory);
        var catalogRoot = Path.Combine(repositoryRoot, "data", "catalog");
        var outputDirectory = Path.Combine(repositoryRoot, "docs");
        var cacheDirectory = Path.Combine(repositoryRoot, ".codex-temp", "polytoria-api-coverage");
        string? sourceDirectory = null;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            string NextValue()
            {
                if (i + 1 >= args.Count)
                {
                    throw new InvalidOperationException($"Missing value for {arg}.");
                }

                return args[++i];
            }

            switch (arg)
            {
                case "--repo-root":
                    repositoryRoot = Path.GetFullPath(NextValue());
                    catalogRoot = Path.Combine(repositoryRoot, "data", "catalog");
                    outputDirectory = Path.Combine(repositoryRoot, "docs");
                    cacheDirectory = Path.Combine(repositoryRoot, ".codex-temp", "polytoria-api-coverage");
                    break;
                case "--catalog-root":
                    catalogRoot = Path.GetFullPath(NextValue());
                    break;
                case "--output-dir":
                    outputDirectory = Path.GetFullPath(NextValue());
                    break;
                case "--cache-dir":
                    cacheDirectory = Path.GetFullPath(NextValue());
                    break;
                case "--source-dir":
                    sourceDirectory = Path.GetFullPath(NextValue());
                    break;
                default:
                    throw new InvalidOperationException($"Unknown argument: {arg}");
            }
        }

        return new CoverageToolOptions(repositoryRoot, catalogRoot, outputDirectory, cacheDirectory, sourceDirectory);
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "VisualRuleSystem.Polytoria.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find VisualRuleSystem.Polytoria.slnx.");
    }
}
