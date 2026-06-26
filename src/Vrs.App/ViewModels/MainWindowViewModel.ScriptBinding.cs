using CommunityToolkit.Mvvm.Input;
using Vrs.Core.Persistence;
using Vrs.Core.Samples;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    private const string VisualScriptNamePrefix = "VRS ";

    [RelayCommand]
    private void NewServerScript()
    {
        StartNewScript(GraphScriptKind.Server);
    }

    [RelayCommand]
    private void NewLocalScript()
    {
        StartNewScript(GraphScriptKind.Local);
    }

    [RelayCommand]
    private void NewModuleScript()
    {
        StartNewScript(GraphScriptKind.Module);
    }

    [RelayCommand]
    private void LoadTimerMessageSample()
    {
        var liveSceneObjects = graph.SceneObjects.ToList();
        graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        graph.SceneObjects = liveSceneObjects.Count > 0 ? liveSceneObjects : graph.SceneObjects;
        BridgeScriptName = graph.Script.ScriptName;
        GraphAutosaveEnabled = graph.Script.AutosaveEnabled;
        documentStore.MarkDirty([GraphDocumentSection.Metadata, GraphDocumentSection.Rules, GraphDocumentSection.ViewState]);
        RefreshAll("Loaded TimerMessage sample graph.");
    }

    public void StartNewScript(GraphScriptKind scriptKind, string scriptName = "NewVisualScript")
    {
        var liveSceneObjects = graph.SceneObjects.ToList();
        graph = SampleGraphFactory.CreateEmptyDraftGraph(scriptKind, scriptName);
        graph.SceneObjects = liveSceneObjects;
        RuleGraphDocumentNormalizer.ApplyScriptBinding(
            graph,
            scriptKind,
            scriptName,
            HasLinkedProject ? GraphAuthoringMode.CreatorLinked : GraphAuthoringMode.PolyCreatorLessDraft,
            "Created",
            lockScriptKind: true,
            creatorParentPath: BridgeParentPath);
        BridgeScriptName = graph.Script.ScriptName;
        GraphAutosaveEnabled = graph.Script.AutosaveEnabled;
        documentStore.MarkDirty([GraphDocumentSection.Metadata, GraphDocumentSection.Rules, GraphDocumentSection.ViewState]);
        RefreshAll($"Created empty {scriptKind} script graph.");
    }

    [RelayCommand]
    private void ApplyScriptRename()
    {
        var oldScriptName = graph.Script.ScriptName;
        var oldAuthorName = NormalizeDraftScriptName(oldScriptName);
        var oldCreatorName = scriptDeployment.ResolveScriptName(oldScriptName, graph.Rules.FirstOrDefault());
        var nextAuthorName = NormalizeDraftScriptName(DraftScriptName);
        var rule = graph.Rules.FirstOrDefault();
        var shouldRenameRule = RuleNameTracksScript(rule, oldScriptName, oldAuthorName, oldCreatorName);

        ApplyScriptBinding(
            graph.Script.ScriptKind,
            nextAuthorName,
            "Renamed",
            lockScriptKind: graph.Script.IsScriptKindLocked);

        BridgeScriptName = nextAuthorName;
        DraftScriptName = nextAuthorName;
        if (shouldRenameRule && rule is not null)
        {
            rule.Name = nextAuthorName;
        }

        graph.Script.ProjectRelativePath = "";
        graph.Script.LinkedScriptPath = "";
        graph.Script.CreatorObjectPath = "";
        documentStore.MarkDirty([GraphDocumentSection.Metadata, GraphDocumentSection.Rules]);
        RefreshAll($"Renamed script target: {ScriptCreatorPreviewName}.");
    }

    private bool TryChangeScriptKind(GraphScriptKind scriptKind)
    {
        if (GraphHasAuthoredContent() || graph.Script.IsScriptKindLocked)
        {
            SetStatus("Script type is locked for this graph. Create a new empty script graph to choose another type.");
            return false;
        }

        ApplyScriptBinding(scriptKind, graph.Script.ScriptName, graph.Script.Source, lockScriptKind: false);
        documentStore.MarkDirty(GraphDocumentSection.Metadata);
        RefreshAll($"Script type: {scriptKind}");
        return true;
    }

    private void ApplyScriptBinding(
        GraphScriptKind scriptKind,
        string scriptName,
        string source,
        bool lockScriptKind,
        string creatorObjectPath = "",
        string linkedScriptPath = "")
    {
        RuleGraphDocumentNormalizer.ApplyScriptBinding(
            graph,
            scriptKind,
            scriptName,
            HasLinkedProject ? GraphAuthoringMode.CreatorLinked : GraphAuthoringMode.PolyCreatorLessDraft,
            source,
            lockScriptKind,
            BridgeParentPath,
            creatorObjectPath,
            linkedScriptPath);
        GraphAutosaveEnabled = graph.Script.AutosaveEnabled;
        NotifyScriptBindingPropertiesChanged();
    }

    private void NotifyScriptBindingPropertiesChanged()
    {
        OnPropertyChanged(nameof(SelectedScriptKind));
        OnPropertyChanged(nameof(ScriptBindingSummary));
        OnPropertyChanged(nameof(ScriptBindingTooltip));
        OnPropertyChanged(nameof(ScriptCreatorPreviewName));
        OnPropertyChanged(nameof(ScriptFilePreviewPath));
        OnPropertyChanged(nameof(ScriptCreatorPreviewText));
        OnPropertyChanged(nameof(ScriptFilePreviewText));
        OnPropertyChanged(nameof(IsPolyCreatorLessDraft));
        NotifyDeployScriptPropertiesChanged();
    }

    private static bool RuleNameTracksScript(Rule? rule, params string[] names)
    {
        if (rule is null || string.IsNullOrWhiteSpace(rule.Name))
        {
            return true;
        }

        return names
            .Append("NewVisualScript")
            .Append("VisualRuleScript")
            .Any(name => !string.IsNullOrWhiteSpace(name) &&
                rule.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeDraftScriptName(string scriptName)
    {
        var trimmed = string.IsNullOrWhiteSpace(scriptName) ? "" : scriptName.Trim();
        while (trimmed.StartsWith(VisualScriptNamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[VisualScriptNamePrefix.Length..].TrimStart();
        }

        var collapsed = string.Join(" ", trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(collapsed) ? "NewVisualScript" : collapsed;
    }

    private bool GraphHasAuthoredContent()
    {
        var rule = graph.Rules.FirstOrDefault();
        return rule is not null &&
            (rule.Nodes.Count > 0 ||
            rule.Connections.Count > 0 ||
            rule.Fragments.Count > 0 ||
            rule.NodeGroups.Count > 0 ||
            rule.WireReroutes.Count > 0 ||
            rule.ScriptVariables.Count > 0);
    }
}
