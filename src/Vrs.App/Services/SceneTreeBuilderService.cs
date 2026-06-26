using Vrs.Graph.Model;

namespace Vrs.App.Services;

/// <summary>
/// Builds the Creator hierarchy as neutral tree records so filtering and
/// expansion rules can be tested without Avalonia view models.
/// </summary>
public sealed class SceneTreeBuilderService
{
    public SceneTreeBuildResult Build(
        IEnumerable<SceneObject> sceneObjects,
        string filter,
        IReadOnlySet<string> previouslyExpandedPaths,
        bool hasPreviousExpansionState)
    {
        var trimmedFilter = filter.Trim();
        var isFiltering = !string.IsNullOrWhiteSpace(trimmedFilter);
        var source = sceneObjects
            .Where(item => !string.IsNullOrWhiteSpace(item.Path))
            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        var items = source.ToDictionary(item => item.Path, item => new MutableSceneTreeItem(item), StringComparer.OrdinalIgnoreCase);

        // Creator Explorer order is user-facing context, so hierarchy rebuilds
        // keep the snapshot order instead of sorting by path or object name.
        foreach (var sceneObject in source)
        {
            var item = items[sceneObject.Path];
            var parentPath = ParentPath(sceneObject.Path);
            if (!string.IsNullOrWhiteSpace(parentPath) && items.TryGetValue(parentPath, out var parent))
            {
                parent.Children.Add(item);
            }
        }

        var filteredRoots = items.Values
            .Where(item => string.IsNullOrWhiteSpace(ParentPath(item.Path)) || !items.ContainsKey(ParentPath(item.Path)))
            .Select(item => FilterSceneTree(item, trimmedFilter))
            .Where(item => item is not null)
            .Cast<MutableSceneTreeItem>()
            .ToList();

        var resultRoots = new List<SceneTreeItem>();
        foreach (var root in filteredRoots)
        {
            resultRoots.Add(ToImmutable(root, isFiltering, previouslyExpandedPaths, hasPreviousExpansionState));
        }

        return new SceneTreeBuildResult(resultRoots);
    }

    private static SceneTreeItem ToImmutable(
        MutableSceneTreeItem item,
        bool isFiltering,
        IReadOnlySet<string> previouslyExpandedPaths,
        bool hasPreviousExpansionState)
    {
        var children = item.Children
            .Select(child => ToImmutable(child, isFiltering, previouslyExpandedPaths, hasPreviousExpansionState))
            .ToList();
        var isExpanded = isFiltering
            ? item.Children.Count > 0
            : IsDefaultExpandedSceneRoot(item) ||
              (hasPreviousExpansionState && previouslyExpandedPaths.Contains(item.Path));

        return new SceneTreeItem(item.SceneObject, children, isExpanded);
    }

    private static MutableSceneTreeItem? FilterSceneTree(MutableSceneTreeItem item, string filter)
    {
        var filteredChildren = item.Children
            .Select(child => FilterSceneTree(child, filter))
            .Where(child => child is not null)
            .Cast<MutableSceneTreeItem>()
            .ToList();

        if (!MatchesSceneFilter(item.SceneObject, filter) && filteredChildren.Count == 0)
        {
            return null;
        }

        var copy = new MutableSceneTreeItem(item.SceneObject);
        foreach (var child in filteredChildren)
        {
            copy.Children.Add(child);
        }

        return copy;
    }

    private static bool MatchesSceneFilter(SceneObject item, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return Contains(item.Name, filter) ||
            Contains(item.Path, filter) ||
            Contains(item.Kind, filter) ||
            Contains(item.LinkedScriptPath, filter);
    }

    private static bool IsDefaultExpandedSceneRoot(MutableSceneTreeItem item)
    {
        return item.Path.Equals("World", StringComparison.OrdinalIgnoreCase) ||
            item.Path.Equals("Root", StringComparison.OrdinalIgnoreCase);
    }

    private static string ParentPath(string path)
    {
        var normalized = path.Trim().Trim('/');
        var index = normalized.LastIndexOf('/');
        return index <= 0 ? "" : normalized[..index];
    }

    private static bool Contains(string value, string search)
    {
        return value.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class MutableSceneTreeItem
    {
        public MutableSceneTreeItem(SceneObject sceneObject)
        {
            SceneObject = sceneObject;
        }

        public SceneObject SceneObject { get; }
        public List<MutableSceneTreeItem> Children { get; } = [];
        public string Name => string.IsNullOrWhiteSpace(SceneObject.Name) ? SceneObject.Path : SceneObject.Name;
        public string Path => SceneObject.Path;
        public string Kind => SceneObject.Kind;
        public string LinkedScriptPath => SceneObject.LinkedScriptPath;
    }
}

/// <summary>
/// Result of rebuilding the Creator hierarchy from neutral scene-object paths.
/// </summary>
public sealed record SceneTreeBuildResult(IReadOnlyList<SceneTreeItem> Roots);

/// <summary>
/// Container-neutral tree node used before Avalonia-specific view models are created.
/// </summary>
public sealed record SceneTreeItem(
    SceneObject SceneObject,
    IReadOnlyList<SceneTreeItem> Children,
    bool IsExpanded);
