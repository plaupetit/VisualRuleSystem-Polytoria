using System.Text.Json;
using Vrs.App.Services;
using Vrs.Core.Bridge;
using Vrs.Core.Persistence;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    // Creator sync is intentionally one-way for the graph document: bridge
    // files refresh scene hierarchy state, but linked script metadata is loaded
    // only from explicit user clicks.
    public async Task SynchronizeBridgeOnceAsync()
    {
        await RunBridgeAutoSyncAsync().ConfigureAwait(true);
    }

    public void SetWindowFocusState(bool focused)
    {
        IsVrsWindowFocused = focused;
    }

    partial void OnIsVrsWindowFocusedChanged(bool value)
    {
        ApplyBridgeBeatPresentation(lastBridgeSyncResult);
    }

    private async Task<BridgeSyncResult?> RunBridgeAutoSyncAsync(
        bool updateStatus = true,
        string? onlyReportCommandId = null)
    {
        if (isBridgeSyncRunning)
        {
            return null;
        }

        isBridgeSyncRunning = true;
        try
        {
            var projectRoot = ActiveProjectRoot;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                projectRoot = await ResolveActiveProjectRoot().ConfigureAwait(true) ?? "";
            }

            if (string.IsNullOrWhiteSpace(projectRoot) ||
                !projectRuntimeStatus.IsValidProjectRoot(projectRoot))
            {
                ApplyBridgeBeatUnavailable("No valid active Polytoria project is linked.");
                return null;
            }

            var bridgeDirectory = bridge.ResolveBridgeDirectory(projectRoot);
            BridgeDirectory = bridgeDirectory;
            var options = CreateSnapshotReadOptions();
            var sync = await bridgeSync.SyncAsync(bridgeDirectory, options, IsVrsWindowFocused).ConfigureAwait(true);

            ApplyProjectRuntimeStatus(projectRuntimeStatus.BuildLinkedProjectStatus(projectRoot, bridgeDirectory));
            ApplyBridgeSyncResult(sync, options, updateStatus, onlyReportCommandId);
            await PublishBridgeAppStateAsync(bridgeDirectory).ConfigureAwait(true);
            return sync;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            if (updateStatus)
            {
                SetStatus($"Bridge sync skipped: {ex.Message}");
            }

            return null;
        }
        finally
        {
            isBridgeSyncRunning = false;
        }
    }

    private async Task WaitForCreatorCommandResultAsync(string commandId)
    {
        if (string.IsNullOrWhiteSpace(commandId))
        {
            return;
        }

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var sync = await RunBridgeAutoSyncAsync(updateStatus: true, onlyReportCommandId: commandId).ConfigureAwait(true);
            if (string.Equals(sync?.LatestCommandResult?.Id, commandId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Task.Delay(250).ConfigureAwait(true);
        }
    }

    private void ApplyBridgeSyncResult(
        BridgeSyncResult sync,
        SceneSnapshotReadOptions options,
        bool updateStatus,
        string? onlyReportCommandId)
    {
        lastBridgeSyncResult = sync;
        ApplyBridgeBeatPresentation(sync);

        if (sync.SnapshotChanged && sync.Snapshot is not null)
        {
            graph.SceneObjects.Clear();
            foreach (var sceneObject in sync.Snapshot.Objects)
            {
                graph.SceneObjects.Add(sceneObject);
            }

            RefreshSceneObjects();
            SnapshotStatus = FormatSnapshotStatus(sync.Snapshot, options);
            documentStore.MarkDirty(GraphDocumentSection.SceneObjects);
        }

        if (sync.SnapshotChanged || sync.ScriptLinksChanged)
        {
            NotifyDeployScriptPropertiesChanged();
        }

        var reportedCommand = TryUpdateObservedCommandResult(sync.LatestCommandResult, onlyReportCommandId);
        if (updateStatus && reportedCommand is not null)
        {
            SetStatus(FormatCreatorCommandStatus(reportedCommand));
            return;
        }

        if (updateStatus && reportedCommand is null)
        {
            if (sync.SnapshotChanged && sync.Snapshot is not null)
            {
                SetStatus($"Synced Creator snapshot: {sync.Snapshot.Objects.Count} object(s).");
            }
            else if (sync.LatestCommandResult is null && !string.IsNullOrWhiteSpace(sync.StatusText))
            {
                SetStatus(sync.StatusText);
            }
        }
    }

    private CommandResultEntry? TryUpdateObservedCommandResult(
        CommandResultEntry? latestCommandResult,
        string? onlyReportCommandId)
    {
        if (latestCommandResult is null || string.IsNullOrWhiteSpace(latestCommandResult.Id))
        {
            hasInitializedCommandResults = true;
            return null;
        }

        var isNewResult = !string.Equals(latestCommandResult.Id, lastObservedCommandResultId, StringComparison.OrdinalIgnoreCase);
        var isInitialObservation = !hasInitializedCommandResults;
        lastObservedCommandResultId = latestCommandResult.Id;
        hasInitializedCommandResults = true;

        if (!isNewResult)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(onlyReportCommandId))
        {
            return string.Equals(latestCommandResult.Id, onlyReportCommandId, StringComparison.OrdinalIgnoreCase)
                ? latestCommandResult
                : null;
        }

        return isInitialObservation ? null : latestCommandResult;
    }

    private SceneSnapshotReadOptions CreateSnapshotReadOptions()
    {
        return new SceneSnapshotReadOptions
        {
            MaxObjects = SnapshotMaxObjects,
            MaxDepth = SnapshotMaxDepth,
            Search = "",
            IncludeBridgeTrash = IncludeBridgeTrash
        };
    }

    private static string FormatCreatorCommandStatus(CommandResultEntry result)
    {
        var prefix = result.Ok ? "Creator applied" : "Creator blocked";
        var target = !string.IsNullOrWhiteSpace(result.Details.TargetPath)
            ? result.Details.TargetPath
            : result.Path;
        var targetText = string.IsNullOrWhiteSpace(target) ? "" : $" ({target})";
        return $"{prefix}: {result.Message}{targetText}";
    }

    private void ApplyBridgeBeatUnavailable(string detail)
    {
        BridgeBeatText = "Bridge not linked";
        BridgeBeatDetail = detail;
        BridgeBeatBackgroundHex = "#242936";
        BridgeBeatBorderHex = "#64748b";
        BridgeBeatForegroundHex = "#d7e2ee";
    }

    private void ApplyBridgeBeatPresentation(BridgeSyncResult? sync)
    {
        if (sync is null)
        {
            if (string.IsNullOrWhiteSpace(BridgeDirectory))
            {
                ApplyBridgeBeatUnavailable("No active bridge directory is linked.");
                return;
            }

            var focusText = IsVrsWindowFocused ? "VRS focus" : "Live paused";
            BridgeBeatText = $"Addon pending | {focusText}";
            BridgeBeatDetail = BridgeBeatDetailForPending(focusText);
            BridgeBeatBackgroundHex = IsVrsWindowFocused ? "#142338" : "#3b2f12";
            BridgeBeatBorderHex = IsVrsWindowFocused ? "#5eb7ff" : "#e7b93f";
            BridgeBeatForegroundHex = IsVrsWindowFocused ? "#9ed8ff" : "#ffe7a8";
            return;
        }

        var addonText = FormatAddonBeatText(sync.AddonStatus);
        var stateText = IsVrsWindowFocused ? "VRS focus" : "Live paused";
        BridgeBeatText = $"{addonText} | {stateText}";
        BridgeBeatDetail = BridgeBeatDetailForSync(sync);

        if (!sync.HeartbeatWritten || !sync.AddonStatus.Exists || !sync.AddonStatus.IsReadable)
        {
            BridgeBeatBackgroundHex = "#421818";
            BridgeBeatBorderHex = "#ef4444";
            BridgeBeatForegroundHex = "#ffd1d1";
            return;
        }

        if (!sync.AddonStatus.IsFresh || !IsVrsWindowFocused)
        {
            BridgeBeatBackgroundHex = "#3b2f12";
            BridgeBeatBorderHex = "#e7b93f";
            BridgeBeatForegroundHex = "#ffe7a8";
            return;
        }

        BridgeBeatBackgroundHex = "#123522";
        BridgeBeatBorderHex = "#2dd46f";
        BridgeBeatForegroundHex = "#c8ffd9";
    }

    private string BridgeBeatDetailForPending(string focusText)
    {
        return string.Join(
            Environment.NewLine,
            $"Bridge: {BridgeDirectory}",
            "Addon status: waiting for next bridge sync.",
            $"VRS focus: {focusText}.",
            "Live mutation requires VRS focus.");
    }

    private string BridgeBeatDetailForSync(BridgeSyncResult sync)
    {
        var addon = sync.AddonStatus;
        var addonAge = addon.Age is null ? "unknown" : FormatBeatAge(addon.Age.Value);
        var addonUpdated = addon.UpdatedAtUtc?.ToString("O") ?? "unknown";
        var addonState = addon.IsReadable
            ? $"Bridge status: {addon.State}; age: {addonAge}; message: {addon.Message}"
            : addon.Error;
        var heartbeat = sync.HeartbeatWrittenAtUtc is null
            ? "VRS heartbeat: write failed."
            : $"VRS heartbeat: {sync.HeartbeatWrittenAtUtc.Value:O}; TTL: {(int)sync.HeartbeatTimeToLive.TotalSeconds}s.";

        return string.Join(
            Environment.NewLine,
            $"Bridge: {BridgeDirectory}",
            $"Addon status file: {addon.StatusPath}",
            addonState,
            $"Addon updatedAtUtc: {addonUpdated}",
            heartbeat,
            $"Current VRS focus: {(IsVrsWindowFocused ? "focused" : "not focused; live mutation paused")}.",
            $"Last heartbeat focus: {(sync.VrsFocused ? "focused" : "not focused")}.",
            "Live mutation requires VRS focus.");
    }

    private static string FormatAddonBeatText(BridgeAddonStatus addon)
    {
        if (!addon.Exists)
        {
            return "Addon missing";
        }

        if (!addon.IsReadable)
        {
            return "Addon unreadable";
        }

        var age = addon.Age is null ? "unknown" : FormatBeatAge(addon.Age.Value);
        return addon.IsFresh ? $"Addon beat {age}" : $"Addon stale {age}";
    }

    private static string FormatBeatAge(TimeSpan age)
    {
        if (age < TimeSpan.Zero)
        {
            return "future";
        }

        if (age.TotalSeconds < 90)
        {
            return $"{Math.Max(0, (int)age.TotalSeconds)}s";
        }

        if (age.TotalMinutes < 90)
        {
            return $"{(int)age.TotalMinutes}m";
        }

        return $"{(int)age.TotalHours}h";
    }
}
