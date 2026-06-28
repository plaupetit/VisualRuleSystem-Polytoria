using CommunityToolkit.Mvvm.Input;
using Vrs.Core.Bridge;
using Vrs.Core.Persistence;
using System.Diagnostics;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task RefreshSnapshot()
    {
        var stopwatch = Stopwatch.StartNew();
        IsSnapshotLoading = true;
        SnapshotStatus = "Loading Creator snapshot...";
        try
        {
            var bridgeDirectory = await ResolveBridgeDirectory().ConfigureAwait(true);
            var options = CreateSnapshotReadOptions();

            var result = await bridge.ReadSceneSnapshotObjectsAsync(bridgeDirectory, options).ConfigureAwait(true);
            if (result is null)
            {
                graph.SceneObjects.Clear();
                RefreshSceneObjects();
                SnapshotStatus = "No scene-snapshot.json found for the active bridge.";
                SetStatus(SnapshotStatus);
                return;
            }

            graph.SceneObjects.Clear();
            foreach (var sceneObject in result.Objects)
            {
                graph.SceneObjects.Add(sceneObject);
            }

            RefreshSceneObjects();
            SnapshotStatus = FormatSnapshotStatus(result, options);
            documentStore.MarkDirty(GraphDocumentSection.SceneObjects);
            stopwatch.Stop();
            SetStatus($"Loaded bounded Creator snapshot: {result.Objects.Count} object(s) in {stopwatch.ElapsedMilliseconds} ms.");
        }
        catch (Exception ex)
        {
            SnapshotStatus = $"Snapshot load failed: {ex.Message}";
            SetStatus(SnapshotStatus);
        }
        finally
        {
            IsSnapshotLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRequestCreatorSnapshot))]
    private async Task RequestCreatorSnapshot()
    {
        try
        {
            var bridgeDirectory = await ResolveWorkspaceBridgeDirectoryAsync().ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(bridgeDirectory))
            {
                SnapshotStatus = "Cannot request Creator snapshot: no active Polytoria project is linked.";
                SetStatus(SnapshotStatus);
                return;
            }

            await bridge.WriteHeartbeatAsync(bridgeDirectory, active: true, focused: IsVrsWindowFocused).ConfigureAwait(true);
            var requestId = await bridge.WriteSnapshotRequestAsync(
                bridgeDirectory,
                "manual-vrs-workspace-request",
                "full").ConfigureAwait(true);
            SnapshotStatus = $"Snapshot request queued: {requestId}.";
            SetStatus($"Requested Creator snapshot: {requestId}.");
            ApplyBridgeBeatPresentation(lastBridgeSyncResult);
            await PublishBridgeAppStateAsync(bridgeDirectory).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Text.Json.JsonException)
        {
            SnapshotStatus = $"Snapshot request failed: {ex.Message}";
            SetStatus(SnapshotStatus);
        }
    }
}
