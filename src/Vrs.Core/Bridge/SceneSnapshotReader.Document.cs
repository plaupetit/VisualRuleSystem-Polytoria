using System.Text.Json;

namespace Vrs.Core.Bridge;

public static partial class SceneSnapshotReader
{
    // Parses the snapshot document shell. The recursive object reader owns the
    // Creator hierarchy itself.
    private static void ReadSnapshotObject(
        ref Utf8JsonReader reader,
        SceneSnapshotReadResult result,
        SceneSnapshotReadOptions options)
    {
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var propertyName = reader.GetString() ?? "";
            if (!reader.Read())
            {
                throw new JsonException($"Missing value for snapshot property '{propertyName}'.");
            }

            switch (propertyName)
            {
                case "format":
                    result.Format = ReadString(ref reader);
                    break;
                case "version":
                    result.Version = ReadInt32(ref reader);
                    break;
                case "createdAtUtc":
                    result.CreatedAtUtc = ReadString(ref reader);
                    break;
                case "snapshotVersion":
                    result.SnapshotVersion = ReadInt32(ref reader);
                    break;
                case "runtimeVersion":
                    result.RuntimeVersion = ReadString(ref reader);
                    break;
                case "diagnostics":
                    ReadDiagnostics(ref reader, result);
                    break;
                case "root":
                    ReadSceneNode(ref reader, fallbackDepth: 0, result, options);
                    break;
                default:
                    SkipValue(ref reader);
                    break;
            }
        }
    }
}
