using System.Diagnostics.CodeAnalysis;
using Vrs.App.Services;
using Vrs.Core.Catalog;
using Vrs.Core.Export;
using Vrs.Core.Persistence;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    // Graph refresh pipeline: synchronize collections, validation, preview, scene tree, and export command state after mutations.
    private bool HasExportableRule => graphRefresh.HasExportableRule(graph);

    private bool CanExportLuau() => HasExportableRule;

    private bool CanDeployScriptFile() => HasExportableRule && CanUseCreatorBridgeCommands && graph.AuthoringMode == GraphAuthoringMode.CreatorLinked;

    private bool TryGetExportableRule([NotNullWhen(true)] out Rule? rule)
    {
        return graphRefresh.TryGetExportableRule(graph, out rule);
    }

    private Rule EnsureRule()
    {
        return graphRefresh.EnsureRule(graph);
    }

    private void RefreshAll(string status, bool includePreviewDiffInStatus = false)
    {
        GraphName = graph.Name;
        if (graphRefresh.NormalizeHumanFlowPorts(graph))
        {
            documentStore.MarkDirty(GraphDocumentSection.Rules);
        }

        if (graphRefresh.BackfillCatalogParameters(graph, catalog.Nodes))
        {
            documentStore.MarkDirty(GraphDocumentSection.Rules);
        }

        var groupNormalize = editor.NormalizeNodeGroups(EnsureRule());
        if (groupNormalize.Success && groupNormalize.Changed)
        {
            documentStore.MarkDirty(GraphDocumentSection.ViewState);
        }

        var rerouteNormalize = editor.NormalizeWireReroutes(EnsureRule());
        if (rerouteNormalize.Success && rerouteNormalize.Changed)
        {
            documentStore.MarkDirty(GraphDocumentSection.ViewState);
        }

        NotifyScriptBindingPropertiesChanged();
        OnPropertyChanged(nameof(ShouldConfirmGraphLoadReplacement));
        RefreshSceneObjects();
        RefreshNodes();
        RefreshValidation();
        var previewDiff = RefreshLuauPreview(trackVisualDiff: includePreviewDiffInStatus);
        NotifyExportCommandStateChanged();
        SetStatus(GraphRefreshService.CombineStatusWithPreviewDiff(status, previewDiff));
    }

    private void NotifyExportCommandStateChanged()
    {
        ExportLuauCommand.NotifyCanExecuteChanged();
        DeployScriptFileCommand.NotifyCanExecuteChanged();
        EnsureInputManagerCommand.NotifyCanExecuteChanged();
        QueueCreateFolderCommand.NotifyCanExecuteChanged();
        RequestCreatorSnapshotCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRequestCreatorSnapshot));
        OnPropertyChanged(nameof(VisualScriptingWorkspaceSummary));
        OnPropertyChanged(nameof(CreatorBridgeWorkspaceSummary));
        NotifyDeployScriptPropertiesChanged();
    }

    private void NotifyDeployScriptPropertiesChanged()
    {
        OnPropertyChanged(nameof(ScriptFilePreviewPath));
        OnPropertyChanged(nameof(ScriptFilePreviewText));
        OnPropertyChanged(nameof(VisualScriptingWorkspaceSummary));
        OnPropertyChanged(nameof(CreatorBridgeWorkspaceSummary));
    }

    private void SetStatus(string status)
    {
        StatusText = status;
        Logs.Add($"{DateTimeOffset.Now:HH:mm:ss} {status}");
    }

    private void RefreshCatalogList()
    {
        var visibleEntries = catalogIndexService.Search(catalog.Nodes, CatalogSearch)
            .Where(NodeCatalogService.IsAddable)
            .ToList();

        CatalogEntries.Clear();
        foreach (var entry in visibleEntries)
        {
            CatalogEntries.Add(entry);
        }

        if (SelectedCatalogEntry is null || !visibleEntries.Contains(SelectedCatalogEntry))
        {
            SelectedCatalogEntry = visibleEntries.FirstOrDefault();
        }
    }

    private void RefreshSceneObjects()
    {
        SceneObjects.Clear();
        foreach (var sceneObject in graph.SceneObjects)
        {
            SceneObjects.Add(sceneObject);
        }

        RefreshSceneTree();
        RefreshSelectedNodeParameters();
    }

    private void RefreshSceneTree()
    {
        var isFiltering = !string.IsNullOrWhiteSpace(SceneFilter);
        var previouslyExpandedPaths = ResolveSceneExpansionPathsForRefresh(isFiltering, out var hasPreviousExpansionState);
        SceneTreeRoots.Clear();
        var result = sceneTreeBuilder.Build(graph.SceneObjects, SceneFilter, previouslyExpandedPaths, hasPreviousExpansionState);

        foreach (var root in result.Roots)
        {
            SceneTreeRoots.Add(ToSceneHierarchyItemViewModel(root));
        }

        if (!isFiltering)
        {
            unfilteredSceneExpandedPaths = CollectExpandedScenePaths(SceneTreeRoots);
        }

        sceneTreeWasFiltering = isFiltering;
    }

    private HashSet<string> ResolveSceneExpansionPathsForRefresh(bool isFiltering, out bool hasPreviousExpansionState)
    {
        // Filtered trees auto-expand matching ancestors. Preserve the last
        // unfiltered expansion state so clearing search does not make those
        // temporary expansions permanent.
        if (isFiltering)
        {
            if (!sceneTreeWasFiltering)
            {
                unfilteredSceneExpandedPaths = CollectExpandedScenePaths(SceneTreeRoots);
            }

            hasPreviousExpansionState = unfilteredSceneExpandedPaths.Count > 0;
            return new HashSet<string>(unfilteredSceneExpandedPaths, StringComparer.OrdinalIgnoreCase);
        }

        if (sceneTreeWasFiltering)
        {
            hasPreviousExpansionState = unfilteredSceneExpandedPaths.Count > 0;
            return new HashSet<string>(unfilteredSceneExpandedPaths, StringComparer.OrdinalIgnoreCase);
        }

        hasPreviousExpansionState = SceneTreeRoots.Count > 0;
        return CollectExpandedScenePaths(SceneTreeRoots);
    }

    private static HashSet<string> CollectExpandedScenePaths(IEnumerable<SceneHierarchyItemViewModel> roots)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            CollectExpandedScenePaths(root, paths);
        }

        return paths;
    }

    private static void CollectExpandedScenePaths(SceneHierarchyItemViewModel item, HashSet<string> paths)
    {
        if (item.IsExpanded && !string.IsNullOrWhiteSpace(item.Path))
        {
            paths.Add(item.Path);
        }

        foreach (var child in item.Children)
        {
            CollectExpandedScenePaths(child, paths);
        }
    }

    private static SceneHierarchyItemViewModel ToSceneHierarchyItemViewModel(SceneTreeItem item)
    {
        var viewModel = new SceneHierarchyItemViewModel(item.SceneObject)
        {
            IsExpanded = item.IsExpanded
        };

        foreach (var child in item.Children)
        {
            viewModel.Children.Add(ToSceneHierarchyItemViewModel(child));
        }

        return viewModel;
    }

    private void RefreshNodes()
    {
        Nodes.Clear();
        Connections.Clear();
        Fragments.Clear();
        NodeGroups.Clear();
        WireReroutes.Clear();
        var rule = EnsureRule();
        foreach (var node in rule.Nodes)
        {
            Nodes.Add(node);
        }

        foreach (var connection in rule.Connections)
        {
            Connections.Add(connection);
        }

        foreach (var fragment in rule.Fragments)
        {
            Fragments.Add(fragment);
        }

        foreach (var group in rule.NodeGroups)
        {
            NodeGroups.Add(group);
        }

        foreach (var reroute in rule.WireReroutes)
        {
            WireReroutes.Add(reroute);
        }

        RefreshStateRuleRows(rule);

        if (SelectedNode is not null && Nodes.All(node => !ReferenceEquals(node, SelectedNode)))
        {
            SelectedNode = Nodes.FirstOrDefault(node => node.Id == SelectedNode.Id);
        }

        if (SelectedNode is null && !HasActiveNonNodeCanvasSelection())
        {
            SelectedNode = Nodes.FirstOrDefault();
        }

        if (SelectedConnectionIndex >= Connections.Count)
        {
            SelectedConnectionIndex = -1;
        }

        if (!string.IsNullOrWhiteSpace(SelectedFragmentId) && Fragments.All(fragment => !string.Equals(fragment.Id, SelectedFragmentId, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedFragmentId = "";
        }

        if (!string.IsNullOrWhiteSpace(SelectedGroupId) && NodeGroups.All(group => !string.Equals(group.Id, SelectedGroupId, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedGroupId = "";
        }

        if (!string.IsNullOrWhiteSpace(SelectedWireRerouteId) && WireReroutes.All(reroute => !string.Equals(reroute.Id, SelectedWireRerouteId, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedWireRerouteId = "";
        }

        for (var index = SelectedNodeIds.Count - 1; index >= 0; index--)
        {
            if (Nodes.Any(node => string.Equals(node.Id, SelectedNodeIds[index], StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            SelectedNodeIds.RemoveAt(index);
        }

        NotifySelectedNodeInspectorPropertiesChanged();
        NotifySelectedFragmentInspectorPropertiesChanged();
        NotifySelectedGroupInspectorPropertiesChanged();
        NotifySelectedWireRerouteInspectorPropertiesChanged();
        RefreshInspectorSummary();
    }

    private void RefreshValidation()
    {
        ValidationMessages.Clear();
        foreach (var message in graphRefresh.ValidateGraph(graph, catalog.Nodes))
        {
            ValidationMessages.Add(message);
        }
    }

    private LuauPreviewDiffResult RefreshLuauPreview(bool trackVisualDiff = false)
    {
        var preview = graphRefresh.BuildLuauPreview(graph, catalog.Nodes, LuauPreview, trackVisualDiff);
        LuauPreview = preview.PreviewText;
        if (preview.HighlightedLineNumbers is not null)
        {
            LuauPreviewHighlightedLineNumbers = preview.HighlightedLineNumbers;
        }

        if (preview.IncrementFocusRequest)
        {
            LuauPreviewFocusLineNumber = preview.FocusLineNumber;
            LuauPreviewFocusRequestId++;
        }

        return preview.Diff;
    }

}
