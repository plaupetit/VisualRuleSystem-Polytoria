namespace Vrs.App.Services;

/// <summary>
/// Builds a read-only project file tree for the active Polytoria project.
/// The scanner deliberately skips noisy/generated folders so the UI mirrors
/// Creator's File Browser without turning bridge output into the main view.
/// </summary>
public sealed class ProjectFileTreeBuilderService
{
    private const int DefaultMaxItems = 1500;
    private const int DefaultMaxDepth = 8;

    private static readonly string[] PinnedRootFolders =
    [
        "scripts",
        "Audits",
        "addons",
        "toolbox",
        "api-scout",
        "VisualRuleSystem",
        "JumpClient",
        "Notes"
    ];

    private static readonly HashSet<string> SkippedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".poly",
        ".git",
        ".vs",
        "bin",
        "obj",
        "exports",
        "bridge",
        "__pycache__"
    };

    private static readonly HashSet<string> SkippedFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".meta",
        ".bak",
        ".tmp",
        ".pyc"
    };

    public ProjectFileTreeBuildResult Build(
        string projectRoot,
        string filter,
        IReadOnlySet<string> previouslyExpandedPaths,
        bool hasPreviousExpansionState,
        int maxItems = DefaultMaxItems,
        int maxDepth = DefaultMaxDepth)
    {
        if (string.IsNullOrWhiteSpace(projectRoot) || !Directory.Exists(projectRoot))
        {
            return ProjectFileTreeBuildResult.Empty("No active Polytoria project folder.");
        }

        var fullRoot = Path.GetFullPath(projectRoot);
        var root = new MutableProjectFileTreeItem(ProjectFileTreeEntry.Root(fullRoot));
        var state = new ScanState(Math.Max(1, maxItems), Math.Max(1, maxDepth));
        ScanDirectory(fullRoot, root, state, depth: 0);
        SortChildren(root, isRoot: true);

        var trimmedFilter = filter.Trim();
        var filteredRoot = FilterTree(root, trimmedFilter);
        if (filteredRoot is null)
        {
            filteredRoot = new MutableProjectFileTreeItem(ProjectFileTreeEntry.Root(fullRoot));
        }

        var immutableRoot = ToImmutable(
            filteredRoot,
            isFiltering: !string.IsNullOrWhiteSpace(trimmedFilter),
            previouslyExpandedPaths,
            hasPreviousExpansionState);
        var status = FormatStatus(fullRoot, state, immutableRoot, trimmedFilter);

        return new ProjectFileTreeBuildResult([immutableRoot], status, state.VisibleItems, state.WasLimited);
    }

    private static void ScanDirectory(string directory, MutableProjectFileTreeItem parent, ScanState state, int depth)
    {
        if (depth >= state.MaxDepth || state.VisibleItems >= state.MaxItems)
        {
            state.WasLimited = true;
            return;
        }

        foreach (var child in SafeEnumerateFileSystemEntries(directory))
        {
            if (state.VisibleItems >= state.MaxItems)
            {
                state.WasLimited = true;
                return;
            }

            if (!TryCreateEntry(child, parent.Entry.ProjectRoot, out var entry) || ShouldSkip(entry))
            {
                continue;
            }

            var item = new MutableProjectFileTreeItem(entry);
            parent.Children.Add(item);
            state.VisibleItems++;

            if (entry.IsDirectory)
            {
                ScanDirectory(entry.FullPath, item, state, depth + 1);
            }
        }
    }

    private static IEnumerable<string> SafeEnumerateFileSystemEntries(string directory)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(directory).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return [];
        }
    }

    private static bool TryCreateEntry(string path, string projectRoot, out ProjectFileTreeEntry entry)
    {
        entry = default!;
        try
        {
            var attributes = File.GetAttributes(path);
            var isDirectory = attributes.HasFlag(FileAttributes.Directory);
            var name = Path.GetFileName(path);
            var relativePath = NormalizeProjectPath(Path.GetRelativePath(projectRoot, path));
            entry = new ProjectFileTreeEntry(
                Name: name,
                FullPath: Path.GetFullPath(path),
                ProjectRoot: projectRoot,
                ProjectRelativePath: relativePath,
                Extension: isDirectory ? "" : Path.GetExtension(path),
                IsDirectory: isDirectory);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static bool ShouldSkip(ProjectFileTreeEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            return true;
        }

        if (entry.Name.StartsWith(".", StringComparison.Ordinal))
        {
            return true;
        }

        if (entry.IsDirectory)
        {
            return SkippedDirectoryNames.Contains(entry.Name);
        }

        return SkippedFileExtensions.Contains(entry.Extension);
    }

    private static void SortChildren(MutableProjectFileTreeItem item, bool isRoot)
    {
        var sorted = item.Children
            .OrderBy(child => RootPinIndex(child, isRoot))
            .ThenBy(child => child.Entry.IsDirectory ? 0 : 1)
            .ThenBy(child => child.Entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        item.Children.Clear();
        foreach (var child in sorted)
        {
            SortChildren(child, isRoot: false);
            item.Children.Add(child);
        }
    }

    private static int RootPinIndex(MutableProjectFileTreeItem item, bool isRoot)
    {
        if (!isRoot || !item.Entry.IsDirectory)
        {
            return PinnedRootFolders.Length;
        }

        var index = Array.FindIndex(PinnedRootFolders, value => value.Equals(item.Entry.Name, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? PinnedRootFolders.Length : index;
    }

    private static MutableProjectFileTreeItem? FilterTree(MutableProjectFileTreeItem item, string filter)
    {
        var filteredChildren = item.Children
            .Select(child => FilterTree(child, filter))
            .Where(child => child is not null)
            .Cast<MutableProjectFileTreeItem>()
            .ToList();

        if (!MatchesFilter(item.Entry, filter) && filteredChildren.Count == 0)
        {
            return null;
        }

        var copy = new MutableProjectFileTreeItem(item.Entry);
        foreach (var child in filteredChildren)
        {
            copy.Children.Add(child);
        }

        return copy;
    }

    private static bool MatchesFilter(ProjectFileTreeEntry entry, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            entry.ProjectRelativePath.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            entry.Extension.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectFileTreeItem ToImmutable(
        MutableProjectFileTreeItem item,
        bool isFiltering,
        IReadOnlySet<string> previouslyExpandedPaths,
        bool hasPreviousExpansionState)
    {
        var children = item.Children
            .Select(child => ToImmutable(child, isFiltering, previouslyExpandedPaths, hasPreviousExpansionState))
            .ToList();
        var isRoot = string.IsNullOrWhiteSpace(item.Entry.ProjectRelativePath);
        var isExpanded = isRoot ||
            (isFiltering && children.Count > 0) ||
            (hasPreviousExpansionState && previouslyExpandedPaths.Contains(item.Entry.ProjectRelativePath));

        return new ProjectFileTreeItem(item.Entry, children, isExpanded);
    }

    private static string FormatStatus(string projectRoot, ScanState state, ProjectFileTreeItem root, string filter)
    {
        var childCount = root.Children.Count;
        var limitedText = state.WasLimited ? $", limited to {state.VisibleItems} item(s)" : "";
        var filterText = string.IsNullOrWhiteSpace(filter) ? "" : $", filtered by \"{filter}\"";
        return $"Project files: {childCount} root item(s), {state.VisibleItems} shown{limitedText}{filterText}. Root: {projectRoot}";
    }

    private static string NormalizeProjectPath(string path)
    {
        return path == "."
            ? ""
            : path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private sealed class MutableProjectFileTreeItem
    {
        public MutableProjectFileTreeItem(ProjectFileTreeEntry entry)
        {
            Entry = entry;
        }

        public ProjectFileTreeEntry Entry { get; }
        public List<MutableProjectFileTreeItem> Children { get; } = [];
    }

    private sealed class ScanState
    {
        public ScanState(int maxItems, int maxDepth)
        {
            MaxItems = maxItems;
            MaxDepth = maxDepth;
        }

        public int MaxItems { get; }
        public int MaxDepth { get; }
        public int VisibleItems { get; set; }
        public bool WasLimited { get; set; }
    }
}
