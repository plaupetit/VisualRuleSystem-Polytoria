using System.Text.Json;
using Vrs.Core.Persistence;

namespace Vrs.Core.Bridge;

public sealed partial class BridgeFileService
{
    /// <summary>
    /// Publishes editor liveness for the Creator addon. The heartbeat is
    /// short-lived and safe to overwrite every few seconds.
    /// </summary>
    public async Task WriteHeartbeatAsync(string bridgeDirectory, bool active, bool focused, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(timeToLive ?? TimeSpan.FromSeconds(10));
        var heartbeat = new AppHeartbeat
        {
            SessionId = sessionId,
            Active = active,
            Focused = focused,
            ProcessId = Environment.ProcessId,
            UpdatedAtUtc = now.ToString("O"),
            UpdatedAtUnixSeconds = now.ToUnixTimeSeconds(),
            ExpiresAtUnixSeconds = expiresAt.ToUnixTimeSeconds()
        };

        var json = JsonSerializer.Serialize(heartbeat, VrsJsonContext.Default.AppHeartbeat);
        await WriteTextFileByReplaceAsync(Path.Combine(bridgeDirectory, "app-heartbeat.json"), json, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAppStateAsync(string bridgeDirectory, BridgeAppState state, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(state.SessionId))
        {
            state.SessionId = sessionId;
        }

        if (state.ProcessId == 0)
        {
            state.ProcessId = Environment.ProcessId;
        }

        if (string.IsNullOrWhiteSpace(state.UpdatedAtUtc))
        {
            state.UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
        }

        var json = JsonSerializer.Serialize(state, VrsJsonContext.Default.BridgeAppState);
        await WriteTextFileByReplaceAsync(Path.Combine(bridgeDirectory, "app-state.json"), json, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeAppState?> ReadAppStateAsync(string bridgeDirectory, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(bridgeDirectory, "app-state.json");
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(json, VrsJsonContext.Default.BridgeAppState);
    }

    public async Task<string> WriteSnapshotRequestAsync(
        string bridgeDirectory,
        string reason,
        string mode = "full",
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var request = new SnapshotRequest
        {
            RequestId = $"snapshot_request_{now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            CreatedAtUtc = now.ToString("O"),
            Reason = string.IsNullOrWhiteSpace(reason) ? "manual" : reason.Trim(),
            Mode = string.IsNullOrWhiteSpace(mode) ? "full" : mode.Trim(),
            SessionId = sessionId
        };

        var json = JsonSerializer.Serialize(request, VrsJsonContext.Default.SnapshotRequest);
        await WriteTextFileByReplaceAsync(Path.Combine(bridgeDirectory, "snapshot-request.json"), json, cancellationToken).ConfigureAwait(false);
        return request.RequestId;
    }

    public async Task<SnapshotRequest?> ReadSnapshotRequestAsync(string bridgeDirectory, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(bridgeDirectory, "snapshot-request.json");
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(json, VrsJsonContext.Default.SnapshotRequest);
    }

    public async Task<CommandResults?> ReadCommandResultsAsync(string bridgeDirectory, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(bridgeDirectory, "command-results.json");
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(json, VrsJsonContext.Default.CommandResults);
    }

    public async Task<SceneSnapshot?> ReadSceneSnapshotAsync(string bridgeDirectory, CancellationToken cancellationToken = default)
    {
        var result = await ReadSceneSnapshotObjectsAsync(bridgeDirectory, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return null;
        }

        return new SceneSnapshot
        {
            Format = string.IsNullOrWhiteSpace(result.Format) ? "visual-programming-bridge-scene-snapshot" : result.Format,
            Version = result.Version == 0 ? 1 : result.Version,
            CreatedAtUtc = result.CreatedAtUtc,
            Objects = result.Objects
        };
    }

    public Task<SceneSnapshotReadResult?> ReadSceneSnapshotObjectsAsync(
        string bridgeDirectory,
        SceneSnapshotReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(bridgeDirectory, "scene-snapshot.json");
        return SceneSnapshotReader.ReadAsync(path, options, cancellationToken);
    }

    public async Task WriteStatusAsync(string bridgeDirectory, string state, string message, CancellationToken cancellationToken = default)
    {
        var status = new BridgeStatus
        {
            State = state,
            Message = message,
            UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        };

        var json = JsonSerializer.Serialize(status, VrsJsonContext.Default.BridgeStatus);
        await WriteTextFileByReplaceAsync(Path.Combine(bridgeDirectory, "status.json"), json, cancellationToken).ConfigureAwait(false);
    }
}
