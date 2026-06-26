namespace Vrs.Tests;

public sealed class DocumentationTests
{
    [Fact]
    public void Readme_LinksPublicDocumentationPages()
    {
        var readme = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "README.md"));

        Assert.Contains("docs/USER_GUIDE.md", readme, StringComparison.Ordinal);
        Assert.Contains("docs/WORKFLOWS.md", readme, StringComparison.Ordinal);
        Assert.Contains("docs/TROUBLESHOOTING.md", readme, StringComparison.Ordinal);
        Assert.Contains("docs/API_COVERAGE.md", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicDocs_DoNotReferenceLocalPrivateFiles()
    {
        var docsRoot = Path.Combine(TestPaths.RepositoryRoot, "docs");
        var files = Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.TopDirectoryOnly).ToList();

        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("AGENTS.md", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Notes/", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("D:\\", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("C:\\Users", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void UserGuideDocumentsCurrentCriticalUiLabels()
    {
        var guide = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "docs", "USER_GUIDE.md"));
        var workflows = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "docs", "WORKFLOWS.md"));
        var mainWindow = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "src", "Vrs.App", "Views", "MainWindow.axaml"));
        var hierarchyMenus = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "src", "Vrs.App", "Views", "MainWindow.ContextMenus.cs"));

        foreach (var label in new[] { "Deploy File", "Input Manager", "Output: F11", "Graph" })
        {
            Assert.Contains(label, guide, StringComparison.Ordinal);
            Assert.Contains(label, mainWindow, StringComparison.Ordinal);
        }

        Assert.Contains("Deploy Saved Script Instance Here", workflows, StringComparison.Ordinal);
        Assert.Contains("Deploy Saved Script Instance Here", hierarchyMenus, StringComparison.Ordinal);
    }
}
