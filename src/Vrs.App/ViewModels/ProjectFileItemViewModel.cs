using System.Collections.ObjectModel;
using Vrs.App.Icons;
using Vrs.App.Services;

namespace Vrs.App.ViewModels;

public sealed class ProjectFileItemViewModel : ViewModelBase
{
    private bool isExpanded;

    public ProjectFileItemViewModel(ProjectFileTreeEntry entry)
    {
        Entry = entry;
    }

    public ProjectFileTreeEntry Entry { get; }
    public ObservableCollection<ProjectFileItemViewModel> Children { get; } = [];

    public bool IsExpanded
    {
        get => isExpanded;
        set => SetProperty(ref isExpanded, value);
    }

    public string Name => Entry.Name;
    public string FullPath => Entry.FullPath;
    public string ProjectRelativePath => Entry.ProjectRelativePath;
    public bool IsDirectory => Entry.IsDirectory;
    public string Kind => IsDirectory ? "Folder" : FileKindLabel(Entry.Extension);
    public IconDescriptor Icon => IconRegistry.ForProjectFile(Name, Entry.Extension, IsDirectory);
    public string IconPath => Icon.Path;
    public string IconAccentHex => Icon.AccentHex;
    public string IconBackgroundHex => Icon.BackgroundHex;
    public string IconTooltip => string.IsNullOrWhiteSpace(ProjectRelativePath)
        ? $"{Name}\n{FullPath}"
        : $"{Name}\n{Kind}\n{ProjectRelativePath}\n{FullPath}";

    private static string FileKindLabel(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".luau" or ".lua" => "Luau Script",
            ".json" or ".ptproj" => "Project JSON",
            ".md" => "Markdown",
            ".ptaddon" => "Creator Addon",
            ".poly" => "Polytoria World",
            ".model" or ".ptmodel" => "Polytoria Model",
            ".ps1" or ".cmd" or ".bat" => "Tool Script",
            _ => "File"
        };
    }
}
