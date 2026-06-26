using System.Text.Json;

namespace Vrs.Core.Bridge;

public static partial class SceneSnapshotReader
{
    private static string ReadString(ref Utf8JsonReader reader)
    {
        return reader.TokenType == JsonTokenType.String ? reader.GetString() ?? "" : "";
    }

    private static int ReadInt32(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var value))
        {
            return value;
        }

        return 0;
    }

    private static bool ReadBoolean(ref Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            _ => false
        };
    }

    private static void SkipValue(ref Utf8JsonReader reader)
    {
        if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
        {
            reader.Skip();
        }
    }
}
