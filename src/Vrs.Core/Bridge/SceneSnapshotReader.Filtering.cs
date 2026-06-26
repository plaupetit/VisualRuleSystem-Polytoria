using Vrs.Graph.Model;

namespace Vrs.Core.Bridge;

public static partial class SceneSnapshotReader
{
    private static void FinishSceneNode(
        SceneObject node,
        int depth,
        SceneSnapshotReadResult result,
        SceneSnapshotReadOptions options)
    {
        result.ObservedObjects++;

        if (string.IsNullOrWhiteSpace(node.Path) && !string.IsNullOrWhiteSpace(node.Name))
        {
            node.Path = node.Name;
        }

        if (string.IsNullOrWhiteSpace(node.Name) && !string.IsNullOrWhiteSpace(node.Path))
        {
            var slashIndex = node.Path.LastIndexOf('/');
            node.Name = slashIndex >= 0 ? node.Path[(slashIndex + 1)..] : node.Path;
        }

        if (string.IsNullOrWhiteSpace(node.Kind))
        {
            node.Kind = "Object";
        }

        if (depth > options.MaxDepth)
        {
            result.SkippedByDepth++;
            return;
        }

        if (IsBridgeTrashPath(node.Path, options))
        {
            result.SkippedByBridgeTrash++;
            return;
        }

        if (!MatchesSearch(node, options.Search))
        {
            result.SkippedBySearch++;
            return;
        }

        if (result.Objects.Count >= options.MaxObjects)
        {
            // Display limiting keeps UI snapshots bounded without changing the
            // raw diagnostic counters reported by Creator.
            result.SkippedByDisplayLimit++;
            return;
        }

        result.Objects.Add(node);
    }

    private static bool ReachedDisplayLimit(SceneSnapshotReadResult result, SceneSnapshotReadOptions options)
    {
        return options.StopAfterDisplayLimit && result.Objects.Count >= options.MaxObjects;
    }

    private static bool MatchesSearch(SceneObject node, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return Contains(node.Path, search) ||
            Contains(node.Name, search) ||
            Contains(node.Kind, search) ||
            Contains(node.LinkedScriptPath, search);
    }

    private static bool IsBridgeTrashPath(string path, SceneSnapshotReadOptions options)
    {
        // Bridge trash is implementation detail from the Creator addon; hidden
        // by default so users only see authored hierarchy objects.
        return !options.IncludeBridgeTrash &&
            path.Contains("/VisualBridgeTrash", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string value, string search)
    {
        return value.Contains(search, StringComparison.OrdinalIgnoreCase);
    }
}
