namespace Vrs.App.Services;

/// <summary>
/// Resolves repo-local folders both from source runs and from bin/Debug output.
/// </summary>
public sealed class WorkspacePathService
{
    public string WorkspaceRoot { get; }
    public string ExecutableDirectory { get; }
    public string WorkingDirectory { get; }
    public string CatalogRoot => Path.Combine(WorkspaceRoot, "data", "catalog");
    public string GraphSavePath => Path.Combine(WorkspaceRoot, "projects", "current.graph.json");
    public string PortableGraphPath => Path.Combine(WorkspaceRoot, "projects", "current.vrs-script.json");
    public string ExportDirectory => Path.Combine(WorkspaceRoot, "exports");
    public string DemoBridgeDirectory => Path.Combine(WorkspaceRoot, "bridge");
    public string ActiveProjectConfigPath { get; }
    public string PathProbeSummary { get; }

    public WorkspacePathService()
    {
        ExecutableDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        WorkingDirectory = Path.GetFullPath(Environment.CurrentDirectory);
        var probe = ProbePaths(ExecutableDirectory, WorkingDirectory);

        WorkspaceRoot = probe.WorkspaceRoot;
        ActiveProjectConfigPath = probe.ActiveProjectConfigPath;
        PathProbeSummary = probe.Summary;
    }

    private static PathProbe ProbePaths(string executableDirectory, string workingDirectory)
    {
        var candidates = CandidateDirectories(executableDirectory, workingDirectory).ToList();
        var activeConfig = candidates
            .Select(directory => Path.Combine(directory, "active-polytoria-project.json"))
            .FirstOrDefault(File.Exists);

        var solutionRoot = candidates.FirstOrDefault(directory => File.Exists(Path.Combine(directory, "VisualRuleSystem.Polytoria.slnx")));
        var catalogRoot = candidates.FirstOrDefault(directory => Directory.Exists(Path.Combine(directory, "data", "catalog")));
        var workspaceRoot = solutionRoot ?? catalogRoot ?? executableDirectory;
        var activeConfigPath = activeConfig ?? Path.Combine(workspaceRoot, "active-polytoria-project.json");
        var summary = $"cwd={workingDirectory}; exe={executableDirectory}; workspace={workspaceRoot}; activeConfig={activeConfigPath}";

        return new PathProbe(workspaceRoot, activeConfigPath, summary);
    }

    private static IEnumerable<string> CandidateDirectories(string executableDirectory, string workingDirectory)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[] { workingDirectory, executableDirectory })
        {
            var current = new DirectoryInfo(root);
            while (current is not null)
            {
                var fullName = current.FullName;
                if (seen.Add(fullName))
                {
                    yield return fullName;
                }

                current = current.Parent;
            }
        }
    }

    private sealed record PathProbe(string WorkspaceRoot, string ActiveProjectConfigPath, string Summary);
}
