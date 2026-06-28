using Vrs.Core.Bridge;
using Vrs.Core.Validation;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    private async Task<string?> ResolveWorkspaceBridgeDirectoryAsync()
    {
        if (!string.IsNullOrWhiteSpace(ActiveProjectRoot) &&
            projectRuntimeStatus.IsValidProjectRoot(ActiveProjectRoot))
        {
            var fullRoot = Path.GetFullPath(ActiveProjectRoot);
            BridgeDirectory = bridge.ResolveBridgeDirectory(fullRoot);
            Directory.CreateDirectory(BridgeDirectory);
            return BridgeDirectory;
        }

        var projectRoot = await ResolveActiveProjectRoot().ConfigureAwait(true);
        if (projectRoot is null)
        {
            return null;
        }

        BridgeDirectory = bridge.ResolveBridgeDirectory(projectRoot);
        Directory.CreateDirectory(BridgeDirectory);
        return BridgeDirectory;
    }

    private async Task PublishBridgeAppStateAsync(string bridgeDirectory)
    {
        if (string.IsNullOrWhiteSpace(bridgeDirectory))
        {
            return;
        }

        await bridge.WriteAppStateAsync(bridgeDirectory, BuildBridgeAppState()).ConfigureAwait(true);
    }

    private BridgeAppState BuildBridgeAppState()
    {
        // This state file is deliberately diagnostic. Creator-side mutations
        // still require explicit command files such as pending-commands.json.
        return new BridgeAppState
        {
            Focused = IsVrsWindowFocused,
            ActiveProjectName = ActiveProjectName,
            ProjectUiMode = ProjectUiModeText,
            ProjectLinked = HasLinkedProject,
            CreatorReady = IsCreatorRuntimeReady,
            ScriptKind = SelectedScriptKind.ToString(),
            AuthorScriptName = graph.Script.ScriptName,
            CreatorScriptName = ScriptCreatorPreviewName,
            ProjectRelativeScriptPath = string.IsNullOrWhiteSpace(graph.Script.ProjectRelativePath)
                ? ScriptFilePreviewPath
                : graph.Script.ProjectRelativePath,
            CreatorObjectPath = graph.Script.CreatorObjectPath,
            SelectedCreatorObjectPath = SelectedCreatorObjectPath,
            DeployParentPath = BridgeParentPath,
            NodeCount = Nodes.Count,
            ValidationMessageCount = ValidationMessages.Count,
            ValidationErrorCount = ValidationMessages.Count(message => message.Severity == ValidationSeverity.Error),
            ValidationWarningCount = ValidationMessages.Count(message => message.Severity == ValidationSeverity.Warning),
            BridgeBeatText = BridgeBeatText,
            BridgeBeatDetail = BridgeBeatDetail,
            SnapshotStatus = SnapshotStatus,
            StatusText = StatusText,
            LuauPreviewSummary = BuildLuauPreviewSummary(),
            RecentLogs = Logs.TakeLast(8).ToList()
        };
    }

    private string BuildLuauPreviewSummary()
    {
        if (string.IsNullOrWhiteSpace(LuauPreview))
        {
            return "";
        }

        var summary = string.Join(
            " | ",
            LuauPreview
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Take(4));

        return TruncateBridgeAppStateText(summary, 500);
    }

    private static string TruncateBridgeAppStateText(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..Math.Max(0, maxLength - 3)] + "...";
    }
}
