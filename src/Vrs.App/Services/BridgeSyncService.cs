using System.Security.Cryptography;
using System.Text.Json;
using Vrs.Core.Bridge;

namespace Vrs.App.Services;

/// <summary>
/// Keeps the file bridge warm and reports Creator-side file changes without
/// coupling bridge polling to Avalonia state.
/// </summary>
public sealed class BridgeSyncService
{
    private static readonly TimeSpan HeartbeatTimeToLive = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AddonStatusFreshWindow = TimeSpan.FromMinutes(2);
    private readonly BridgeFileService bridge;
    private readonly Func<DateTimeOffset> nowProvider;
    private BridgeSyncState state = new();

    public BridgeSyncService(BridgeFileService bridge, Func<DateTimeOffset>? nowProvider = null)
    {
        this.bridge = bridge;
        this.nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<BridgeSyncResult> SyncAsync(
        string bridgeDirectory,
        SceneSnapshotReadOptions snapshotOptions,
        bool isVrsFocused = false,
        CancellationToken cancellationToken = default)
    {
        var observedAtUtc = nowProvider();
        if (string.IsNullOrWhiteSpace(bridgeDirectory))
        {
            return BridgeSyncResult.Empty("No bridge directory is linked.", isVrsFocused, HeartbeatTimeToLive, observedAtUtc);
        }

        Directory.CreateDirectory(bridgeDirectory);
        var heartbeatWritten = await TryWriteHeartbeatAsync(bridgeDirectory, isVrsFocused, cancellationToken).ConfigureAwait(false);
        var heartbeatWrittenAtUtc = heartbeatWritten ? observedAtUtc : (DateTimeOffset?)null;
        var addonStatus = ReadAddonStatus(bridgeDirectory, observedAtUtc);
        var snapshotFile = Probe(Path.Combine(bridgeDirectory, "scene-snapshot.json"));
        var scriptLinksFile = Probe(Path.Combine(bridgeDirectory, "script-links.json"));
        var commandResultsFile = Probe(Path.Combine(bridgeDirectory, "command-results.json"));

        SceneSnapshotReadResult? snapshot = null;
        var snapshotChanged = HasChanged(state.SceneSnapshot, snapshotFile);
        if (snapshotChanged && snapshotFile.Exists)
        {
            snapshot = await bridge.ReadSceneSnapshotObjectsAsync(bridgeDirectory, snapshotOptions, cancellationToken).ConfigureAwait(false);
        }

        var scriptLinksChanged = HasChanged(state.ScriptLinks, scriptLinksFile);
        CommandResults? commandResults = null;
        var latestCommandResult = default(CommandResultEntry);
        var commandResultsChanged = HasChanged(state.CommandResults, commandResultsFile);
        if (commandResultsChanged && commandResultsFile.Exists)
        {
            commandResults = await bridge.ReadCommandResultsAsync(bridgeDirectory, cancellationToken).ConfigureAwait(false);
            latestCommandResult = commandResults?.Results.LastOrDefault();
        }

        state = state with
        {
            BridgeDirectory = bridgeDirectory,
            SceneSnapshot = snapshotFile,
            ScriptLinks = scriptLinksFile,
            CommandResults = commandResultsFile,
            LastHeartbeatAtUtc = heartbeatWrittenAtUtc ?? state.LastHeartbeatAtUtc
        };

        return new BridgeSyncResult(
            HeartbeatWritten: heartbeatWritten,
            VrsFocused: isVrsFocused,
            HeartbeatWrittenAtUtc: heartbeatWrittenAtUtc,
            HeartbeatTimeToLive: HeartbeatTimeToLive,
            AddonStatus: addonStatus,
            SnapshotChanged: snapshotChanged,
            Snapshot: snapshot,
            ScriptLinksChanged: scriptLinksChanged,
            CommandResultsChanged: commandResultsChanged,
            CommandResults: commandResults,
            LatestCommandResult: latestCommandResult,
            StatusText: FormatStatus(snapshotChanged, snapshot, scriptLinksChanged, latestCommandResult));
    }

    public void Reset()
    {
        state = new BridgeSyncState();
    }

    private async Task<bool> TryWriteHeartbeatAsync(string bridgeDirectory, bool isVrsFocused, CancellationToken cancellationToken)
    {
        try
        {
            await bridge.WriteHeartbeatAsync(
                bridgeDirectory,
                active: true,
                focused: isVrsFocused,
                HeartbeatTimeToLive,
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string FormatStatus(
        bool snapshotChanged,
        SceneSnapshotReadResult? snapshot,
        bool scriptLinksChanged,
        CommandResultEntry? latestCommandResult)
    {
        if (latestCommandResult is not null)
        {
            var prefix = latestCommandResult.Ok ? "Creator applied" : "Creator blocked";
            return $"{prefix}: {latestCommandResult.Message}";
        }

        if (snapshotChanged && snapshot is not null)
        {
            return $"Synced Creator snapshot: {snapshot.Objects.Count} object(s).";
        }

        if (scriptLinksChanged)
        {
            return "Synced Creator script links.";
        }

        return "";
    }

    private static BridgeAddonStatus ReadAddonStatus(string bridgeDirectory, DateTimeOffset observedAtUtc)
    {
        var statusPath = Path.Combine(bridgeDirectory, "status.json");
        if (!File.Exists(statusPath))
        {
            return BridgeAddonStatus.Missing(statusPath);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(statusPath));
            var root = document.RootElement;
            var state = ReadJsonString(root, "state") ?? ReadJsonString(root, "State") ?? "unknown";
            var message = ReadJsonString(root, "message") ?? ReadJsonString(root, "Message") ?? "";
            var updatedAtText = ReadJsonString(root, "updatedAtUtc") ?? ReadJsonString(root, "UpdatedAtUtc") ?? "";
            if (!DateTimeOffset.TryParse(updatedAtText, out var updatedAtUtc))
            {
                return BridgeAddonStatus.Unreadable(statusPath, $"Bridge status has no readable updatedAtUtc. State: {state}");
            }

            var normalizedUpdatedAtUtc = updatedAtUtc.ToUniversalTime();
            var age = observedAtUtc - normalizedUpdatedAtUtc;
            return new BridgeAddonStatus(
                StatusPath: statusPath,
                Exists: true,
                IsReadable: true,
                State: state,
                Message: message,
                UpdatedAtUtc: normalizedUpdatedAtUtc,
                Age: age,
                IsFresh: age <= AddonStatusFreshWindow,
                Error: "");
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return BridgeAddonStatus.Unreadable(statusPath, $"Bridge status unreadable: {ex.Message}");
        }
    }

    private static string? ReadJsonString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static BridgeFileStamp Probe(string path)
    {
        if (!File.Exists(path))
        {
            return new BridgeFileStamp(path, Exists: false, LastWriteTimeUtc: default, Length: 0, ContentHash: "");
        }

        var info = new FileInfo(path);
        return new BridgeFileStamp(path, Exists: true, info.LastWriteTimeUtc, info.Length, ContentHash: ContentHash(path));
    }

    private static string ContentHash(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    private static bool HasChanged(BridgeFileStamp previous, BridgeFileStamp current)
    {
        if (string.IsNullOrWhiteSpace(previous.Path))
        {
            return current.Exists;
        }

        return previous.Exists != current.Exists ||
            !string.Equals(previous.Path, current.Path, StringComparison.OrdinalIgnoreCase) ||
            previous.LastWriteTimeUtc != current.LastWriteTimeUtc ||
            previous.Length != current.Length ||
            !string.Equals(previous.ContentHash, current.ContentHash, StringComparison.Ordinal);
    }
}

public sealed record BridgeAddonStatus(
    string StatusPath,
    bool Exists,
    bool IsReadable,
    string State,
    string Message,
    DateTimeOffset? UpdatedAtUtc,
    TimeSpan? Age,
    bool IsFresh,
    string Error)
{
    public static BridgeAddonStatus Unlinked() =>
        new(
            StatusPath: "",
            Exists: false,
            IsReadable: false,
            State: "",
            Message: "",
            UpdatedAtUtc: null,
            Age: null,
            IsFresh: false,
            Error: "No bridge directory is linked.");

    public static BridgeAddonStatus Missing(string statusPath) =>
        new(
            StatusPath: statusPath,
            Exists: false,
            IsReadable: false,
            State: "missing",
            Message: "",
            UpdatedAtUtc: null,
            Age: null,
            IsFresh: false,
            Error: $"Bridge status missing: {statusPath}");

    public static BridgeAddonStatus Unreadable(string statusPath, string error) =>
        new(
            StatusPath: statusPath,
            Exists: true,
            IsReadable: false,
            State: "unreadable",
            Message: "",
            UpdatedAtUtc: null,
            Age: null,
            IsFresh: false,
            Error: error);
}

public sealed record BridgeSyncResult(
    bool HeartbeatWritten,
    bool VrsFocused,
    DateTimeOffset? HeartbeatWrittenAtUtc,
    TimeSpan HeartbeatTimeToLive,
    BridgeAddonStatus AddonStatus,
    bool SnapshotChanged,
    SceneSnapshotReadResult? Snapshot,
    bool ScriptLinksChanged,
    bool CommandResultsChanged,
    CommandResults? CommandResults,
    CommandResultEntry? LatestCommandResult,
    string StatusText)
{
    public static BridgeSyncResult Empty(
        string statusText,
        bool vrsFocused = false,
        TimeSpan? heartbeatTimeToLive = null,
        DateTimeOffset? observedAtUtc = null) =>
        new(
            HeartbeatWritten: false,
            VrsFocused: vrsFocused,
            HeartbeatWrittenAtUtc: null,
            HeartbeatTimeToLive: heartbeatTimeToLive ?? TimeSpan.FromSeconds(10),
            AddonStatus: BridgeAddonStatus.Unlinked(),
            SnapshotChanged: false,
            Snapshot: null,
            ScriptLinksChanged: false,
            CommandResultsChanged: false,
            CommandResults: null,
            LatestCommandResult: null,
            StatusText: statusText);
}

internal sealed record BridgeSyncState(
    string BridgeDirectory = "",
    BridgeFileStamp SceneSnapshot = default,
    BridgeFileStamp ScriptLinks = default,
    BridgeFileStamp CommandResults = default,
    DateTimeOffset LastHeartbeatAtUtc = default);

internal readonly record struct BridgeFileStamp(
    string Path,
    bool Exists,
    DateTime LastWriteTimeUtc,
    long Length,
    string ContentHash);
