using Vrs.App.Services;
using Vrs.App.ViewModels;

namespace Vrs.Tests;

public sealed class ProjectFileTreeBuilderServiceTests
{
    [Fact]
    public void ProjectFileTreeBuilder_FiltersNoiseAndPinsRootFolders()
    {
        var projectRoot = CreateTempProjectRoot();

        try
        {
            foreach (var folder in new[] { "Notes", "Other", "scripts", "Audits", "addons", "toolbox", "api-scout", "VisualRuleSystem", "JumpClient" })
            {
                Directory.CreateDirectory(Path.Combine(projectRoot, folder));
            }

            Directory.CreateDirectory(Path.Combine(projectRoot, ".poly"));
            Directory.CreateDirectory(Path.Combine(projectRoot, "bin"));
            Directory.CreateDirectory(Path.Combine(projectRoot, "bridge"));
            WriteFile(projectRoot, ".gitignore", "ignored");
            WriteFile(projectRoot, "input.json", "{}");
            WriteFile(projectRoot, "main.poly", "world");
            WriteFile(projectRoot, "scripts/server/Timer.server.luau", "print('ok')");
            WriteFile(projectRoot, "scripts/server/Timer.server.luau.meta", "{}");
            WriteFile(projectRoot, "scripts/server/cache.tmp", "");

            var service = new ProjectFileTreeBuilderService();
            var result = service.Build(projectRoot, "", new HashSet<string>(StringComparer.OrdinalIgnoreCase), hasPreviousExpansionState: false);
            var root = Assert.Single(result.Roots);
            var names = root.Children.Select(child => child.Entry.Name).ToList();

            Assert.Equal(
                ["scripts", "Audits", "addons", "toolbox", "api-scout", "VisualRuleSystem", "JumpClient", "Notes"],
                names.Take(8));
            Assert.Contains("Other", names);
            Assert.Contains("input.json", names);
            Assert.Contains("main.poly", names);
            Assert.DoesNotContain(".poly", names);
            Assert.DoesNotContain("bin", names);
            Assert.DoesNotContain("bridge", names);
            Assert.DoesNotContain(".gitignore", names);

            var server = root.Children.Single(child => child.Entry.Name == "scripts")
                .Children.Single(child => child.Entry.Name == "server");
            Assert.Equal(["Timer.server.luau"], server.Children.Select(child => child.Entry.Name));
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ProjectFileTreeBuilder_SearchKeepsAncestorsExpanded()
    {
        var projectRoot = CreateTempProjectRoot();

        try
        {
            WriteFile(projectRoot, "scripts/VRS/server/VRS TimerMessage.server.luau", "print('timer')");
            WriteFile(projectRoot, "scripts/client/Other.client.luau", "print('other')");

            var service = new ProjectFileTreeBuilderService();
            var result = service.Build(projectRoot, "TimerMessage", new HashSet<string>(StringComparer.OrdinalIgnoreCase), hasPreviousExpansionState: false);
            var root = Assert.Single(result.Roots);
            var scripts = Assert.Single(root.Children);
            var vrs = Assert.Single(scripts.Children);
            var server = Assert.Single(vrs.Children);
            var file = Assert.Single(server.Children);

            Assert.True(root.IsExpanded);
            Assert.True(scripts.IsExpanded);
            Assert.True(vrs.IsExpanded);
            Assert.True(server.IsExpanded);
            Assert.False(file.IsExpanded);
            Assert.Equal("VRS TimerMessage.server.luau", file.Entry.Name);
            Assert.Contains("filtered by \"TimerMessage\"", result.Status, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ViewModelProjectFileTree_RefreshesFromActiveProjectAndClearsWhenMissing()
    {
        var projectRoot = CreateTempProjectRoot();

        try
        {
            WriteFile(projectRoot, "project.ptproj", "{}");
            WriteFile(projectRoot, "scripts/VRS/server/VRS TimerMessage.server.luau", "print('timer')");

            var viewModel = new MainWindowViewModel
            {
                ActiveProjectRoot = projectRoot
            };

            var root = Assert.Single(viewModel.ProjectFileTreeRoots);
            Assert.Equal("Root", root.Name);
            Assert.Contains(root.Children, child => child.Name == "scripts");
            Assert.Contains("Project files:", viewModel.ProjectFileStatus, StringComparison.Ordinal);

            viewModel.ActiveProjectRoot = "";

            Assert.Empty(viewModel.ProjectFileTreeRoots);
            Assert.Contains("No active Polytoria project folder", viewModel.ProjectFileStatus, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    private static string CreateTempProjectRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vrs-project-files-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteFile(string projectRoot, string relativePath, string content)
    {
        // Test fixtures use the same project-relative slash paths that the UI displays.
        var fullPath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
}
