using System.Text.Json;

namespace Vrs.Core.Bridge;

/// <summary>
/// Streams Creator scene snapshots into neutral scene object records for UI
/// browsing and parameter choices. This reader deliberately stays independent
/// of Avalonia and of bridge command execution.
/// </summary>
public static partial class SceneSnapshotReader
{
    public static async Task<SceneSnapshotReadResult?> ReadAsync(
        string snapshotPath,
        SceneSnapshotReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(snapshotPath))
        {
            return null;
        }

        var normalized = Normalize(options);
        var bytes = await File.ReadAllBytesAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("scene-snapshot.json must start with a JSON object.");
        }

        var result = new SceneSnapshotReadResult
        {
            FileBytes = bytes.LongLength
        };

        ReadSnapshotObject(ref reader, result, normalized);
        return result;
    }
}
