using Vrs.App.Services;
using Vrs.Core.Catalog;
using Vrs.Core.Persistence;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    // Inspector and state-rule presentation logic derived from the selected graph objects.
    private SelectedNodeInspectorPresentation BuildSelectedNodePresentation()
    {
        return selectionInspector.BuildNodePresentation(SelectedNode, catalog.Nodes);
    }

    private void RefreshSelectedNodeParameters()
    {
        SelectedNodeParameters.Clear();
        SelectedNodeColorPickers.Clear();
        if (SelectedNode is null)
        {
            return;
        }

        var entry = NodeCatalogService.FindByCatalogId(catalog.Nodes, SelectedNode.CatalogId);
        if (entry is not null && NodeCatalogService.BackfillMissingParameters(SelectedNode, entry))
        {
            documentStore.MarkDirty(GraphDocumentSection.Rules);
        }

        var sceneOptions = graph.SceneObjects.Where(sceneObject => !string.IsNullOrWhiteSpace(sceneObject.Path)).ToList();
        var colorParameterKeys = AddColorPickersForSelectedNode(SelectedNode, entry);
        var hideLegacyVectorComponents = HasVectorParameter(SelectedNode, entry);
        foreach (var parameter in SelectedNode.Parameters)
        {
            if (colorParameterKeys.Contains(parameter.Key))
            {
                continue;
            }

            var definition = entry?.Parameters.FirstOrDefault(item => string.Equals(item.Key, parameter.Key, StringComparison.OrdinalIgnoreCase));
            if (hideLegacyVectorComponents && IsLegacyVectorComponent(parameter.Key))
            {
                continue;
            }

            SelectedNodeParameters.Add(new NodeParameterEditorViewModel(
                parameter,
                definition,
                sceneOptions,
                catalog.Nodes,
                SelectedNode.Parameters,
                OnSelectedNodeParameterChanged,
                nodeType: SelectedNode.Type,
                inputActionChoices: inputActionChoices));
        }
    }

    private static bool HasVectorParameter(RuleNode node, NodeCatalogEntry? entry)
    {
        return node.Parameters.Any(parameter => parameter.Key.Equals("vector", StringComparison.OrdinalIgnoreCase) ||
                parameter.Key.Equals("lookPosition", StringComparison.OrdinalIgnoreCase)) ||
            entry?.Parameters.Any(parameter =>
                parameter.Key.Equals("vector", StringComparison.OrdinalIgnoreCase) ||
                parameter.Key.Equals("lookPosition", StringComparison.OrdinalIgnoreCase) ||
                parameter.Type.Contains("Vector3", StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static bool IsLegacyVectorComponent(string key)
    {
        return key.Equals("x", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("y", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("z", StringComparison.OrdinalIgnoreCase);
    }

    private HashSet<string> AddColorPickersForSelectedNode(RuleNode node, NodeCatalogEntry? entry)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var red = node.Parameters.FirstOrDefault(parameter => parameter.Key.Equals("r", StringComparison.OrdinalIgnoreCase));
        var green = node.Parameters.FirstOrDefault(parameter => parameter.Key.Equals("g", StringComparison.OrdinalIgnoreCase));
        var blue = node.Parameters.FirstOrDefault(parameter => parameter.Key.Equals("b", StringComparison.OrdinalIgnoreCase));
        if (red is null || green is null || blue is null)
        {
            return keys;
        }

        keys.Add(red.Key);
        keys.Add(green.Key);
        keys.Add(blue.Key);
        var title = entry is null ? "Polytoria Color" : $"{entry.Label} Color";
        SelectedNodeColorPickers.Add(new NodeColorPickerViewModel(red, green, blue, title, OnSelectedNodeParameterChanged, SessionRecentColors));
        return keys;
    }

    private void RefreshInspectorSummary()
    {
        var summary = selectionInspector.BuildInspectorSummary(EnsureRule(), SelectedNode, SelectedGroupId, SelectedWireRerouteId, SelectedFragmentId, SelectedConnectionIndex);
        InspectorTitle = summary.Title;
        InspectorDescription = summary.Description;
        InspectorDetail = summary.Detail;
    }

    private void OnSelectedNodeParameterChanged()
    {
        documentStore.MarkDirty(GraphDocumentSection.Rules);
        CanvasRevision++;
        foreach (var parameter in SelectedNodeParameters)
        {
            parameter.NotifyVisibilityChanged();
        }

        RefreshValidation();
        RefreshLuauPreview();
        OnPropertyChanged(nameof(SelectedNodeConfiguredSummary));
        SetStatus("Updated node parameter.");
    }

    private void OnSelectedNodeConfigurationChanged(string status)
    {
        documentStore.MarkDirty(GraphDocumentSection.Rules);
        CanvasRevision++;
        RefreshValidation();
        RefreshLuauPreview();
        RefreshInspectorSummary();
        SetStatus(status);
    }

    private void ApplyGroupEdit(GraphEditResult result)
    {
        if (result.Success && result.Changed)
        {
            documentStore.MarkDirty(GraphDocumentSection.ViewState);
            CanvasRevision++;
        }

        NotifySelectedGroupInspectorPropertiesChanged();
        RefreshInspectorSummary();
        SetStatus(result.Message);
    }

    private void ApplyWireRerouteEdit(GraphEditResult result)
    {
        if (result.Success && result.Changed)
        {
            documentStore.MarkDirty(GraphDocumentSection.ViewState);
            CanvasRevision++;
        }

        NotifySelectedWireRerouteInspectorPropertiesChanged();
        RefreshInspectorSummary();
        SetStatus(result.Message);
    }

    private void RefreshStateRuleRows(Rule rule)
    {
        StateRuleRows.Clear();
        foreach (var row in selectionInspector.BuildStateRuleRows(rule))
        {
            StateRuleRows.Add(new StateRuleRowViewModel
            {
                Id = row.Id,
                Name = row.Name,
                Kind = row.Kind,
                TriggerSummary = row.TriggerSummary,
                ConditionSummary = row.ConditionSummary,
                ActionSummary = row.ActionSummary,
                Comment = row.Comment,
                Collapsed = row.Collapsed
            });
        }
    }

    private void NotifySelectedNodeInspectorPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasSelectedNode));
        OnPropertyChanged(nameof(ShowsGenericInspectorHeader));
        OnPropertyChanged(nameof(SelectedNodeKindText));
        OnPropertyChanged(nameof(SelectedNodeTypeText));
        OnPropertyChanged(nameof(SelectedNodeBlockTitle));
        OnPropertyChanged(nameof(SelectedNodeHumanVerb));
        OnPropertyChanged(nameof(SelectedNodeBlockBadge));
        OnPropertyChanged(nameof(SelectedNodeBlockAccentHex));
        OnPropertyChanged(nameof(SelectedNodeBlockFillHex));
        OnPropertyChanged(nameof(SelectedNodeBlockSubtitle));
        OnPropertyChanged(nameof(SelectedNodeConfiguredSummary));
        OnPropertyChanged(nameof(SelectedNodeDetailsOpen));
        OnPropertyChanged(nameof(SelectedNodeEnabled));
        OnPropertyChanged(nameof(SelectedNodeDebugEnabled));
        OnPropertyChanged(nameof(SelectedNodeFallbackMode));
        OnPropertyChanged(nameof(SelectedNodeUserComment));
        OnPropertyChanged(nameof(SelectedNodeColorPickers));
    }

    private void NotifySelectedFragmentInspectorPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasSelectedFragment));
        OnPropertyChanged(nameof(ShowsGenericInspectorHeader));
        OnPropertyChanged(nameof(SelectedFragment));
        OnPropertyChanged(nameof(SelectedFragmentKindText));
        OnPropertyChanged(nameof(SelectedFragmentCollapsed));
    }

    private void NotifySelectedGroupInspectorPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasSelectedGroup));
        OnPropertyChanged(nameof(ShowsGenericInspectorHeader));
        OnPropertyChanged(nameof(SelectedGroup));
        OnPropertyChanged(nameof(SelectedGroupName));
        OnPropertyChanged(nameof(SelectedGroupColor));
        OnPropertyChanged(nameof(SelectedGroupMemberCount));
        OnPropertyChanged(nameof(SelectedGroupSizeText));
        OnPropertyChanged(nameof(SelectedGroupParentText));
    }

    private void NotifySelectedWireRerouteInspectorPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasSelectedWireReroute));
        OnPropertyChanged(nameof(ShowsGenericInspectorHeader));
        OnPropertyChanged(nameof(SelectedWireReroute));
        OnPropertyChanged(nameof(SelectedWireReroutePositionText));
        OnPropertyChanged(nameof(SelectedWireRerouteWireText));
        OnPropertyChanged(nameof(SelectedWireRerouteInputDirection));
        OnPropertyChanged(nameof(SelectedWireRerouteOutputDirection));
    }

    private void SelectNodeById(string nodeId)
    {
        SelectedConnectionIndex = -1;
        SelectedFragmentId = "";
        SelectedNode = Nodes.FirstOrDefault(node => string.Equals(node.Id, nodeId, StringComparison.OrdinalIgnoreCase));
        SelectedWireRerouteId = "";
    }

    private void SelectFragmentById(string fragmentId)
    {
        if (string.IsNullOrWhiteSpace(fragmentId))
        {
            SelectedFragmentId = "";
            return;
        }

        SelectedNode = null;
        SelectedConnectionIndex = -1;
        SelectedGroupId = "";
        SelectedWireRerouteId = "";
        SelectedFragmentId = fragmentId;
    }

    private void SelectGroupById(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            SelectedGroupId = "";
            return;
        }

        SelectedNode = null;
        SelectedConnectionIndex = -1;
        SelectedFragmentId = "";
        SelectedWireRerouteId = "";
        SelectedNodeIds.Clear();
        SelectedGroupId = groupId;
    }

    private void SelectWireRerouteById(string rerouteId)
    {
        if (string.IsNullOrWhiteSpace(rerouteId))
        {
            SelectedWireRerouteId = "";
            return;
        }

        SelectedNode = null;
        SelectedConnectionIndex = -1;
        SelectedFragmentId = "";
        SelectedGroupId = "";
        SelectedNodeIds.Clear();
        SelectedWireRerouteId = rerouteId;
    }

}
