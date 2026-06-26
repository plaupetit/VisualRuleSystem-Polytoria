namespace Vrs.App.Services;

public sealed record ProjectFileTreeBuildResult(
    IReadOnlyList<ProjectFileTreeItem> Roots,
    string Status,
    int VisibleItems,
    bool WasLimited)
{
    public static ProjectFileTreeBuildResult Empty(string status)
    {
        return new ProjectFileTreeBuildResult([], status, VisibleItems: 0, WasLimited: false);
    }
}

public sealed record ProjectFileTreeItem(
    ProjectFileTreeEntry Entry,
    IReadOnlyList<ProjectFileTreeItem> Children,
    bool IsExpanded);

public sealed record ProjectFileTreeEntry(
    string Name,
    string FullPath,
    string ProjectRoot,
    string ProjectRelativePath,
    string Extension,
    bool IsDirectory)
{
    public static ProjectFileTreeEntry Root(string projectRoot)
    {
        return new ProjectFileTreeEntry(
            Name: "Root",
            FullPath: projectRoot,
            ProjectRoot: projectRoot,
            ProjectRelativePath: "",
            Extension: "",
            IsDirectory: true);
    }
}
