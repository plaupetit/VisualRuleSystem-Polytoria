using System.Text.Json;

namespace Vrs.Core.Bridge;

public static partial class SceneSnapshotReader
{
    private static void ReadDiagnostics(ref Utf8JsonReader reader, SceneSnapshotReadResult result)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            SkipValue(ref reader);
            return;
        }

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
                throw new JsonException($"Missing value for diagnostics property '{propertyName}'.");
            }

            switch (propertyName)
            {
                case "objectCount":
                    result.DiagnosticObjectCount = ReadInt32(ref reader);
                    break;
                case "watcherCount":
                    result.DiagnosticWatcherCount = ReadInt32(ref reader);
                    break;
                case "truncatedCount":
                    result.DiagnosticTruncatedCount = ReadInt32(ref reader);
                    break;
                case "maxObservedDepth":
                    result.DiagnosticMaxObservedDepth = ReadInt32(ref reader);
                    break;
                case "snapshotVersion":
                    result.SnapshotVersion = ReadInt32(ref reader);
                    break;
                case "runtimeVersion":
                    result.RuntimeVersion = ReadString(ref reader);
                    break;
                default:
                    SkipValue(ref reader);
                    break;
            }
        }
    }
}
