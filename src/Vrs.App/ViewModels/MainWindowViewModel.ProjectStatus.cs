using Vrs.App.Services;
using Vrs.Core.Bridge;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    // Project link, bridge location, runtime status, deployment target query, and snapshot status formatting.
    public async Task SetActiveProjectRootAsync(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            SetNoProjectStatus($"No project path selected. Probe: {paths.PathProbeSummary}");
            SetStatus("No project selected.");
            return;
        }

        var fullRoot = Path.GetFullPath(projectRoot);
        if (!projectRuntimeStatus.IsValidProjectRoot(fullRoot))
        {
            SetNoProjectStatus($"Invalid Polytoria project folder: {fullRoot}");
            SetStatus("Selected folder is not a Polytoria project.");
            return;
        }

        await bridge.SaveActiveProjectAsync(paths.ActiveProjectConfigPath, fullRoot).ConfigureAwait(true);
        Directory.CreateDirectory(bridge.ResolveBridgeDirectory(fullRoot));
        bridgeSync.Reset();
        hasInitializedCommandResults = false;
        lastObservedCommandResultId = "";
        SetActiveProjectStatus(fullRoot);
        await RefreshSnapshot().ConfigureAwait(true);
        SetStatus($"Changed active project: {ActiveProjectName}");
    }

    public void CancelProjectSelection()
    {
        SetStatus("Project selection cancelled.");
    }

    private async Task<string> ResolveBridgeDirectory()
    {
        var projectRoot = await ResolveActiveProjectRoot().ConfigureAwait(true);
        if (projectRoot is not null)
        {
            BridgeDirectory = bridge.ResolveBridgeDirectory(projectRoot);
            return BridgeDirectory;
        }

        SetNoProjectStatus($"Demo bridge. Probe: {paths.PathProbeSummary}");
        BridgeDirectory = paths.DemoBridgeDirectory;
        return BridgeDirectory;
    }

    private async Task<string?> ResolveActiveProjectRoot()
    {
        var activeProject = await bridge.LoadActiveProjectAsync(paths.ActiveProjectConfigPath).ConfigureAwait(true);
        if (activeProject is not null &&
            projectRuntimeStatus.IsValidProjectRoot(activeProject.ProjectRoot))
        {
            SetActiveProjectStatus(activeProject.ProjectRoot);
            return Path.GetFullPath(activeProject.ProjectRoot);
        }

        return null;
    }

    private void SetActiveProjectStatus(string projectRoot)
    {
        var fullRoot = Path.GetFullPath(projectRoot);
        BridgeDirectory = bridge.ResolveBridgeDirectory(fullRoot);
        ApplyProjectRuntimeStatus(projectRuntimeStatus.BuildLinkedProjectStatus(fullRoot, BridgeDirectory));
    }

    private void SetNoProjectStatus(string detail)
    {
        BridgeDirectory = "";
        lastBridgeSyncResult = null;
        ApplyBridgeBeatUnavailable(detail);
        ApplyProjectRuntimeStatus(projectRuntimeStatus.BuildNoProjectStatus(detail));
    }

    private void RefreshProjectStatusFromCurrentLink()
    {
        if (string.IsNullOrWhiteSpace(ActiveProjectRoot))
        {
            return;
        }

        if (!projectRuntimeStatus.IsValidProjectRoot(ActiveProjectRoot))
        {
            SetNoProjectStatus($"Invalid or missing Polytoria project folder: {ActiveProjectRoot}");
            return;
        }

        SetActiveProjectStatus(ActiveProjectRoot);
    }

    private void ApplyProjectRuntimeStatus(ProjectRuntimeStatusResult status)
    {
        ActiveProjectName = status.ActiveProjectName;
        ActiveProjectRoot = status.ActiveProjectRoot;
        ActiveProjectPath = status.ActiveProjectPath;
        ProjectStatusText = status.ProjectStatusText;
        ProjectStatusDetail = status.ProjectStatusDetail;
        ProjectStatusBackgroundHex = status.ProjectStatusBackgroundHex;
        ProjectStatusBorderHex = status.ProjectStatusBorderHex;
        ProjectStatusForegroundHex = status.ProjectStatusForegroundHex;
        HasLinkedProject = status.HasLinkedProject;
        IsCreatorRuntimeReady = status.IsCreatorRuntimeReady;
        HasActiveProject = status.HasActiveProject;
        ProjectUiModeText = status.HasLinkedProject
            ? status.IsCreatorRuntimeReady
                ? "Project linked and Creator ready"
                : "Project linked; Creator not running"
            : "No project linked";
    }

    private static string FormatSnapshotStatus(SceneSnapshotReadResult result, SceneSnapshotReadOptions options)
    {
        var reportedTotal = result.DiagnosticObjectCount > 0 ? result.DiagnosticObjectCount : result.ObservedObjects;
        var sizeKb = Math.Max(1, result.FileBytes / 1024);
        var version = result.SnapshotVersion > 0 ? $"v{result.SnapshotVersion}" : "unknown version";
        var limitedText = result.WasLimited ? $", {result.PrunedSubtrees} subtree(s) skipped" : "";

        return $"{result.Objects.Count}/{reportedTotal} shown, depth <= {options.MaxDepth}, max {options.MaxObjects}, {sizeKb} KB, {version}{limitedText}.";
    }
}
