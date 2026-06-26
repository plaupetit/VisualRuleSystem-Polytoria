using System.Collections.ObjectModel;
using Vrs.App.Icons;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

public sealed class SceneHierarchyItemViewModel : ViewModelBase
{
    private bool isExpanded;

    public SceneHierarchyItemViewModel(SceneObject sceneObject)
    {
        SceneObject = sceneObject;
    }

    public SceneObject SceneObject { get; }
    public ObservableCollection<SceneHierarchyItemViewModel> Children { get; } = [];
    public bool IsExpanded
    {
        get => isExpanded;
        set => SetProperty(ref isExpanded, value);
    }

    public string Name => string.IsNullOrWhiteSpace(SceneObject.Name) ? SceneObject.Path : SceneObject.Name;
    public string Kind => SceneObject.Kind;
    public string Path => SceneObject.Path;
    public bool HasChildren => Children.Count > 0;
    public bool IsLinkedScript => SceneObject.IsLinkedScript;
    public string LinkedScriptPath => SceneObject.LinkedScriptPath;
    public bool IsScriptLike =>
        IsLinkedScript ||
        SceneObject.IsVisualScriptName ||
        Kind.Contains("Script", StringComparison.OrdinalIgnoreCase) ||
        LinkedScriptPath.EndsWith(".luau", StringComparison.OrdinalIgnoreCase);
    public bool ShowsScriptBadge => IsScriptLike || !string.IsNullOrWhiteSpace(LinkedScriptPath);
    public IconDescriptor Icon => IconRegistry.ForSceneKind(Kind);
    public string IconPath => Icon.Path;
    public string IconAccentHex => Icon.AccentHex;
    public string IconBackgroundHex => Icon.BackgroundHex;
    public string IconTooltip
    {
        get => string.IsNullOrWhiteSpace(LinkedScriptPath)
            ? $"{Name}\n{Kind}\n{Path}"
            : $"{Name}\n{Kind}\n{Path}\n{LinkedScriptPath}";
    }
}
