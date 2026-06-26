using System.Text.Json;
using Vrs.Graph.Model;

namespace Vrs.Core.Bridge;

public static partial class SceneSnapshotReader
{
    private static void ReadSceneNode(
        ref Utf8JsonReader reader,
        int fallbackDepth,
        SceneSnapshotReadResult result,
        SceneSnapshotReadOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            SkipValue(ref reader);
            return;
        }

        var node = new SceneObject();
        var depth = fallbackDepth;
        var hasExplicitDepth = false;
        var shouldPruneChildren = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                FinishSceneNode(node, hasExplicitDepth ? depth : fallbackDepth, result, options);
                return;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var propertyName = reader.GetString() ?? "";
            if (!reader.Read())
            {
                throw new JsonException($"Missing value for scene object property '{propertyName}'.");
            }

            switch (propertyName)
            {
                case "objectId":
                    node.Id = ReadString(ref reader);
                    break;
                case "name":
                    node.Name = ReadString(ref reader);
                    break;
                case "className":
                    node.Kind = ReadString(ref reader);
                    break;
                case "path":
                    node.Path = ReadString(ref reader);
                    shouldPruneChildren = shouldPruneChildren || IsBridgeTrashPath(node.Path, options);
                    break;
                case "linkedScriptPath":
                    node.LinkedScriptPath = ReadString(ref reader);
                    break;
                case "isLinkedScript":
                    node.IsLinkedScript = ReadBoolean(ref reader);
                    break;
                case "isVisualScriptName":
                    node.IsVisualScriptName = ReadBoolean(ref reader);
                    break;
                case "depth":
                    depth = ReadInt32(ref reader);
                    hasExplicitDepth = true;
                    break;
                case "children":
                    ReadChildren(ref reader, hasExplicitDepth ? depth : fallbackDepth, shouldPruneChildren, result, options);
                    break;
                default:
                    SkipValue(ref reader);
                    break;
            }
        }
    }

    private static void ReadChildren(
        ref Utf8JsonReader reader,
        int parentDepth,
        bool shouldPruneChildren,
        SceneSnapshotReadResult result,
        SceneSnapshotReadOptions options)
    {
        if (shouldPruneChildren || parentDepth >= options.MaxDepth || ReachedDisplayLimit(result, options))
        {
            // Pruning skips a whole subtree when it cannot affect visible output.
            result.PrunedSubtrees++;
            SkipValue(ref reader);
            return;
        }

        switch (reader.TokenType)
        {
            case JsonTokenType.StartArray:
                ReadChildrenArray(ref reader, parentDepth, result, options);
                break;
            case JsonTokenType.StartObject:
                ReadChildrenObject(ref reader, parentDepth, result, options);
                break;
            default:
                SkipValue(ref reader);
                break;
        }
    }

    private static void ReadChildrenArray(
        ref Utf8JsonReader reader,
        int parentDepth,
        SceneSnapshotReadResult result,
        SceneSnapshotReadOptions options)
    {
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return;
            }

            if (ReachedDisplayLimit(result, options))
            {
                result.PrunedSubtrees++;
                SkipValue(ref reader);
                continue;
            }

            ReadSceneNode(ref reader, parentDepth + 1, result, options);
        }
    }

    private static void ReadChildrenObject(
        ref Utf8JsonReader reader,
        int parentDepth,
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

            if (!reader.Read())
            {
                throw new JsonException("Missing value for children object entry.");
            }

            if (ReachedDisplayLimit(result, options))
            {
                result.PrunedSubtrees++;
                SkipValue(ref reader);
                continue;
            }

            ReadSceneNode(ref reader, parentDepth + 1, result, options);
        }
    }
}
