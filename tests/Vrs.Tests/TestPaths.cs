namespace Vrs.Tests;

internal static class TestPaths
{
    public static string RepositoryRoot => FindRepositoryRoot(AppContext.BaseDirectory);
    public static string CatalogRoot => Path.Combine(RepositoryRoot, "data", "catalog");

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

        throw new DirectoryNotFoundException("Could not find VisualRuleSystem.Polytoria.slnx from test output.");
    }
}
