using CommunityToolkit.Mvvm.Input;
using Vrs.App.Services;
using System.Diagnostics;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    // Project files are intentionally read-only in this panel. Creator remains
    // the authority for scene changes; this tree only exposes the active project
    // folder structure for navigation and context.
    [RelayCommand]
    private void RefreshProjectFiles()
    {
        var previouslyExpandedPaths = CollectExpandedProjectFilePaths(ProjectFileTreeRoots);
        var hasPreviousExpansionState = ProjectFileTreeRoots.Count > 0;
        var result = projectFileTreeBuilder.Build(
            ActiveProjectRoot,
            ProjectFileFilter,
            previouslyExpandedPaths,
            hasPreviousExpansionState);

        ApplyProjectFileTreeResult(result);
        _ = RefreshInputActionChoicesAsync();
    }

    private async Task RefreshProjectFilesAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        IsProjectFilesLoading = true;
        ProjectFileStatus = "Loading project files...";
        var previouslyExpandedPaths = CollectExpandedProjectFilePaths(ProjectFileTreeRoots);
        var hasPreviousExpansionState = ProjectFileTreeRoots.Count > 0;
        var projectRoot = ActiveProjectRoot;
        var filter = ProjectFileFilter;

        var result = await Task.Run(
            () => projectFileTreeBuilder.Build(
                projectRoot,
                filter,
                previouslyExpandedPaths,
                hasPreviousExpansionState),
            cancellationToken).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
        ApplyProjectFileTreeResult(result);
        await RefreshInputActionChoicesAsync(cancellationToken).ConfigureAwait(true);
        IsProjectFilesLoading = false;
        stopwatch.Stop();
        Logs.Add($"{DateTimeOffset.Now:HH:mm:ss} Project file tree loaded in {stopwatch.ElapsedMilliseconds} ms.");
    }

    private void ApplyProjectFileTreeResult(ProjectFileTreeBuildResult result)
    {
        ProjectFileTreeRoots.Clear();
        foreach (var root in result.Roots)
        {
            ProjectFileTreeRoots.Add(ToProjectFileItemViewModel(root));
        }

        ProjectFileStatus = result.Status;
    }

    private async Task RefreshInputActionChoicesAsync(CancellationToken cancellationToken = default)
    {
        inputActionChoices = await inputManager.ReadInputActionChoicesAsync(ActiveProjectRoot, cancellationToken).ConfigureAwait(true);
        RefreshSelectedNodeParameters();
    }

    public void SelectProjectFileItem(ProjectFileItemViewModel item)
    {
        SelectedProjectFilePath = item.ProjectRelativePath;
        var displayPath = string.IsNullOrWhiteSpace(item.ProjectRelativePath)
            ? "Root"
            : item.ProjectRelativePath;
        var kind = item.IsDirectory ? "folder" : "file";
        SetStatus($"Selected project {kind}: {displayPath}");
    }

    public bool CanLoadGraphFromProjectFile(ProjectFileItemViewModel item)
    {
        return !item.IsDirectory &&
            item.Entry.Extension.Equals(".luau", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> LoadGraphFromProjectFileAsync(ProjectFileItemViewModel item)
    {
        return await LoadGraphFromProjectFileAsync(item, replaceCurrentGraph: true).ConfigureAwait(true);
    }

    public async Task<bool> LoadGraphFromProjectFileAsync(ProjectFileItemViewModel item, bool replaceCurrentGraph)
    {
        if (!CanLoadGraphFromProjectFile(item))
        {
            SetStatus("Cannot load this file as a VRS graph.");
            return false;
        }

        if (!replaceCurrentGraph && ShouldConfirmGraphLoadReplacement)
        {
            SetStatus("Load cancelled.");
            return false;
        }

        SelectedProjectFilePath = item.ProjectRelativePath;
        var result = await scriptGraphLoader.LoadAsync(ActiveProjectRoot, item.ProjectRelativePath).ConfigureAwait(true);
        if (!result.Succeeded)
        {
            SetStatus(result.StatusText);
            return false;
        }

        return ApplyLoadedScriptGraph(
            result,
            result.ScriptKind,
            result.ScriptName,
            "ProjectFile",
            linkedScriptPath: result.ProjectRelativePath);
    }

    private static HashSet<string> CollectExpandedProjectFilePaths(IEnumerable<ProjectFileItemViewModel> roots)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            CollectExpandedProjectFilePaths(root, paths);
        }

        return paths;
    }

    private static void CollectExpandedProjectFilePaths(ProjectFileItemViewModel item, HashSet<string> paths)
    {
        if (item.IsExpanded)
        {
            paths.Add(item.ProjectRelativePath);
        }

        foreach (var child in item.Children)
        {
            CollectExpandedProjectFilePaths(child, paths);
        }
    }

    private static ProjectFileItemViewModel ToProjectFileItemViewModel(ProjectFileTreeItem item)
    {
        var viewModel = new ProjectFileItemViewModel(item.Entry)
        {
            IsExpanded = item.IsExpanded
        };

        foreach (var child in item.Children)
        {
            viewModel.Children.Add(ToProjectFileItemViewModel(child));
        }

        return viewModel;
    }
}
