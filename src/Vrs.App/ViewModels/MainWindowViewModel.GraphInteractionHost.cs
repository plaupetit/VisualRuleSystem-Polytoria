using Vrs.Core.Persistence;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    // Canvas interaction host implementation: translate control gestures into graph edit service calls.
    public bool CanPasteGraphClipboard => graphClipboard.HasClipboard;

    public void SelectGraphNode(RuleNode? node)
    {
        SelectedNode = node;
        SelectedConnectionIndex = -1;
        SelectedGroupId = "";
        SelectedWireRerouteId = "";
        SelectedNodeIds.Clear();
        if (node is not null)
        {
            SelectedNodeIds.Add(node.Id);
        }
    }

    public void SelectGraphNodes(IReadOnlyList<string> nodeIds, string primaryNodeId = "")
    {
        SelectedConnectionIndex = -1;
        SelectedFragmentId = "";
        SelectedGroupId = "";
        SelectedWireRerouteId = "";
        SelectedNodeIds.Clear();

        var distinctIds = nodeIds
            .Where(id => Nodes.Any(node => string.Equals(node.Id, id, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var id in distinctIds)
        {
            SelectedNodeIds.Add(id);
        }

        var primaryId = string.IsNullOrWhiteSpace(primaryNodeId) ? distinctIds.FirstOrDefault() ?? "" : primaryNodeId;
        SelectedNode = Nodes.FirstOrDefault(node => string.Equals(node.Id, primaryId, StringComparison.OrdinalIgnoreCase));
    }

    public void SelectGraphConnection(int connectionIndex)
    {
        SelectedConnectionIndex = connectionIndex;
        if (connectionIndex >= 0)
        {
            SelectedNode = null;
            SelectedWireRerouteId = "";
        }
    }

    public void SelectGraphFragment(string fragmentId)
    {
        SelectFragmentById(fragmentId);
    }

    public void SelectGraphGroup(string groupId)
    {
        SelectGroupById(groupId);
    }

    public void SelectGraphWireReroute(string rerouteId)
    {
        SelectWireRerouteById(rerouteId);
    }

    public void SetGraphInsertionPoint(float graphX, float graphY)
    {
        CanvasAddGraphX = graphX;
        CanvasAddGraphY = graphY;
    }

    public void UpdateGraphViewport(double width, double height)
    {
        if (width > 0)
        {
            CanvasViewportWidth = width;
        }

        if (height > 0)
        {
            CanvasViewportHeight = height;
        }
    }

    public GraphEditResult TryAddGraphConnection(string fromNodeId, string fromPortId, string toNodeId, string toPortId)
    {
        var result = editor.AddConnection(EnsureRule(), fromNodeId, fromPortId, toNodeId, toPortId);
        if (result.Success && result.Changed)
        {
            documentStore.MarkDirty(GraphDocumentSection.Rules);
        }

        RefreshAll(result.Message);
        return result;
    }

    public void NotifyGraphNodeMoved(string nodeId, float graphX, float graphY)
    {
        var result = editor.MoveNode(EnsureRule(), nodeId, graphX, graphY);
        if (result.Success)
        {
            documentStore.MarkDirty(GraphDocumentSection.ViewState);
        }

        RefreshNodes();
        SetStatus(result.Message);
    }

    public void NotifyGraphGroupsMoved(IReadOnlyList<GraphGroupMove> groupMoves, IReadOnlyList<GraphNodeMove> nodeMoves, IReadOnlyList<GraphWireRerouteMove> rerouteMoves, string primaryGroupId)
    {
        var rule = EnsureRule();
        var result = editor.MoveGroups(rule, groupMoves, nodeMoves, rerouteMoves);
        if (result.Success)
        {
            documentStore.MarkDirty(GraphDocumentSection.ViewState);
            if (!string.IsNullOrWhiteSpace(primaryGroupId))
            {
                var membership = editor.AddUngroupedItemsToGroup(
                    rule,
                    primaryGroupId,
                    nodeMoves.Select(move => move.NodeId),
                    rerouteMoves.Select(move => move.RerouteId));
                if (membership.Success && membership.Changed)
                {
                    documentStore.MarkDirty(GraphDocumentSection.ViewState);
                }
            }
        }

        RefreshNodes();
        SetStatus(result.Message);
        if (!string.IsNullOrWhiteSpace(primaryGroupId))
        {
            SelectGroupById(primaryGroupId);
        }
    }

    public void AddWireRerouteToConnection(int connectionIndex, float graphX, float graphY, int insertAtIndex)
    {
        var result = editor.AddWireReroute(EnsureRule(), connectionIndex, graphX, graphY, insertAtIndex);
        if (result.Success && result.Changed)
        {
            documentStore.MarkDirty(GraphDocumentSection.ViewState);
        }

        RefreshAll(result.Message);
        if (result.Success)
        {
            SelectWireRerouteById(EnsureRule().WireReroutes.Last().Id);
        }
    }

    public void NotifyWireRerouteMoved(string rerouteId, float graphX, float graphY)
    {
        var result = editor.MoveWireReroute(EnsureRule(), rerouteId, graphX, graphY);
        if (result.Success)
        {
            documentStore.MarkDirty(GraphDocumentSection.ViewState);
        }

        RefreshNodes();
        SetStatus(result.Message);
        if (result.Success)
        {
            SelectWireRerouteById(rerouteId);
        }
    }

    public void NotifyGraphGroupResized(string groupId, float width, float height)
    {
        var result = editor.ResizeGroup(EnsureRule(), groupId, width, height);
        if (result.Success)
        {
            documentStore.MarkDirty(GraphDocumentSection.ViewState);
        }

        RefreshNodes();
        SetStatus(result.Message);
        if (result.Success)
        {
            SelectGroupById(groupId);
        }
    }

    public void AutoParentGraphGroup(string groupId)
    {
        var result = editor.AutoParentGroupByBounds(EnsureRule(), groupId);
        if (result.Success && result.Changed)
        {
            documentStore.MarkDirty(GraphDocumentSection.ViewState);
        }

        RefreshNodes();
        if (result.Changed || !result.Success)
        {
            SetStatus(result.Message);
        }
    }

    public void DuplicateGraphNode(string nodeId)
    {
        var result = editor.DuplicateNode(EnsureRule(), nodeId);
        if (result.Success)
        {
            documentStore.MarkDirty(GraphDocumentSection.Rules);
        }

        RefreshAll(result.Message);
    }

    public void DisconnectGraphNode(string nodeId)
    {
        var result = editor.DisconnectNode(EnsureRule(), nodeId);
        if (result.Success && result.Changed)
        {
            documentStore.MarkDirty(GraphDocumentSection.Rules);
        }

        RefreshAll(result.Message, includePreviewDiffInStatus: result.Success && result.Changed);
    }

    public void ToggleGraphNodeEnabled(string nodeId)
    {
        var result = editor.ToggleNodeEnabled(EnsureRule(), nodeId);
        if (result.Success)
        {
            documentStore.MarkDirty(GraphDocumentSection.Rules);
        }

        RefreshAll(result.Message);
    }

    public void ToggleGraphNodeDebug(string nodeId)
    {
        var node = EnsureRule().Nodes.FirstOrDefault(item => string.Equals(item.Id, nodeId, StringComparison.OrdinalIgnoreCase));
        if (node is null)
        {
            SetStatus($"Node not found: {nodeId}");
            return;
        }

        node.DebugEnabled = !node.DebugEnabled;
        documentStore.MarkDirty(GraphDocumentSection.Rules);
        RefreshAll(node.DebugEnabled ? $"Enabled debug marker: {node.Label}" : $"Disabled debug marker: {node.Label}");
    }

    public void ToggleGraphNodeBreakpoint(string nodeId)
    {
        var node = EnsureRule().Nodes.FirstOrDefault(item => string.Equals(item.Id, nodeId, StringComparison.OrdinalIgnoreCase));
        if (node is null)
        {
            SetStatus($"Node not found: {nodeId}");
            return;
        }

        node.Breakpoint = !node.Breakpoint;
        documentStore.MarkDirty(GraphDocumentSection.Rules);
        RefreshAll(node.Breakpoint ? $"Enabled breakpoint marker: {node.Label}" : $"Disabled breakpoint marker: {node.Label}");
    }

    public void AddSelectedCatalogNodeAtGraphPoint(float graphX, float graphY)
    {
        CanvasAddGraphX = graphX;
        CanvasAddGraphY = graphY;
        AddSelectedCatalogNodeAtCanvasPosition();
    }

    public void AddCatalogNodeAtGraphPoint(string catalogId, float graphX, float graphY, string connectFromNodeId = "", string connectFromPortId = "")
    {
        var addNode = graphInteraction.AddCatalogNode(
            EnsureRule(),
            catalog.Nodes,
            catalogId,
            graphX,
            graphY,
            connectFromNodeId,
            connectFromPortId);

        if (!addNode.Result.Success)
        {
            if (addNode.CreatedNode is null)
            {
                SetStatus(addNode.StatusText);
                return;
            }

            RefreshAll(addNode.StatusText);
            return;
        }

        documentStore.MarkDirty(GraphDocumentSection.Rules);
        RefreshAll(addNode.StatusText, includePreviewDiffInStatus: addNode.IncludePreviewDiffInStatus);
        SelectNodeById(addNode.CreatedNode!.Id);
    }

    public void AddCatalogNodeKindAtGraphPoint(NodeKind kind, float graphX, float graphY)
    {
        var entry = graphInteraction.FindAddableCatalogEntryForKind(catalog.Nodes, kind, SelectedScriptKind);
        if (entry is null)
        {
            SetStatus($"No addable {kind} node exists for {SelectedScriptKind} scripts.");
            return;
        }

        SelectedCatalogEntry = entry;
        CanvasAddGraphX = graphX;
        CanvasAddGraphY = graphY;
        AddCatalogNodeAtGraphPoint(entry.IdBase, graphX, graphY);
    }

    public void AddRuleFragmentAtGraphPoint(float graphX, float graphY)
    {
        AddFragmentAtGraphPoint(GraphFragmentKind.Rule, graphX, graphY);
    }

    public void AddStateFragmentAtGraphPoint(float graphX, float graphY)
    {
        AddFragmentAtGraphPoint(GraphFragmentKind.State, graphX, graphY);
    }

    public void CreateFragmentFromSelection(GraphFragmentKind kind)
    {
        var nodeIds = SelectedNodeIds.Count > 0
            ? SelectedNodeIds.ToList()
            : graphInteraction.SelectedNodeIdsForFragment(EnsureRule(), SelectedNode, SelectedConnectionIndex);
        var result = editor.CreateFragmentFromSelection(EnsureRule(), nodeIds, kind);
        if (result.Success)
        {
            documentStore.MarkDirty(GraphDocumentSection.Rules);
        }

        RefreshAll(result.Message);
        if (result.Success)
        {
            SelectFragmentById(EnsureRule().Fragments.Last().Id);
        }
    }

    public void CreateGroupFromSelection()
    {
        var rule = EnsureRule();
        var nodeIds = SelectedNodeIds.Count > 0
            ? SelectedNodeIds.ToList()
            : graphInteraction.SelectedNodeIdsForFragment(rule, SelectedNode, SelectedConnectionIndex);
        var rerouteIds = new List<string>();
        if (!string.IsNullOrWhiteSpace(SelectedWireRerouteId))
        {
            rerouteIds.Add(SelectedWireRerouteId);
        }

        if (SelectedConnectionIndex >= 0 && SelectedConnectionIndex < rule.Connections.Count)
        {
            rerouteIds.AddRange(rule.Connections[SelectedConnectionIndex].RerouteIds);
        }

        var result = editor.CreateGroupFromSelection(rule, nodeIds, rerouteIds);
        if (result.Success)
        {
            documentStore.MarkDirty(GraphDocumentSection.ViewState);
        }

        RefreshAll(result.Message);
        if (result.Success)
        {
            SelectGroupById(EnsureRule().NodeGroups.Last().Id);
        }
    }

    public void CreateEmptyGroupAtGraphPoint(float graphX, float graphY)
    {
        var result = editor.CreateEmptyGroup(EnsureRule(), graphX, graphY);
        if (result.Success)
        {
            documentStore.MarkDirty(GraphDocumentSection.ViewState);
        }

        RefreshAll(result.Message);
        if (result.Success)
        {
            SelectGroupById(EnsureRule().NodeGroups.Last().Id);
        }
    }

    public void ExpandGraphFragment(string fragmentId)
    {
        var result = editor.SetFragmentCollapsed(EnsureRule(), fragmentId, collapsed: false);
        if (result.Success)
        {
            documentStore.MarkDirty(GraphDocumentSection.ViewState);
        }

        RefreshAll(result.Message);
        if (result.Success)
        {
            SelectFragmentById(fragmentId);
        }
    }

    public void FrameGraphView()
    {
        FitGraph();
    }

    public void ShowNodeContextMenu(string nodeId, float graphX, float graphY)
    {
        SetStatus($"Node menu: {nodeId}");
    }

    public void ShowCanvasContextMenu(float graphX, float graphY)
    {
        SetStatus($"Canvas menu at {graphX:0}, {graphY:0}");
    }

    public void CopyGraphSelection()
    {
        var result = graphClipboard.Copy(EnsureRule(), CurrentGraphClipboardSelection());
        OnPropertyChanged(nameof(CanPasteGraphClipboard));
        SetStatus(result.Message);
    }

    public void CutGraphSelection()
    {
        var result = graphClipboard.Copy(EnsureRule(), CurrentGraphClipboardSelection());
        OnPropertyChanged(nameof(CanPasteGraphClipboard));
        if (!result.Success)
        {
            SetStatus(result.Message);
            return;
        }

        DeleteGraphSelection();
        SetStatus($"Cut {result.NodeCount} node(s), {result.GroupCount} group(s).");
    }

    public void PasteGraphClipboard(float graphX, float graphY)
    {
        var pasteX = float.IsNaN(graphX) || float.IsInfinity(graphX) ? CanvasAddGraphX : graphX;
        var pasteY = float.IsNaN(graphY) || float.IsInfinity(graphY) ? CanvasAddGraphY : graphY;
        var result = graphClipboard.Paste(EnsureRule(), pasteX, pasteY);
        if (!result.Success)
        {
            SetStatus(result.Message);
            return;
        }

        documentStore.MarkDirty(GraphDocumentSection.Rules);
        documentStore.MarkDirty(GraphDocumentSection.ViewState);
        RefreshAll(result.Message, includePreviewDiffInStatus: result.Changed);

        if (result.NodeIds.Count > 0)
        {
            SelectGraphNodes(result.NodeIds, result.NodeIds[0]);
        }
        else if (result.GroupIds.Count > 0)
        {
            SelectGroupById(result.GroupIds[0]);
        }
    }

    public void DeleteGraphSelection()
    {
        if (!string.IsNullOrWhiteSpace(SelectedWireRerouteId))
        {
            var result = editor.RemoveWireReroute(EnsureRule(), SelectedWireRerouteId);
            if (result.Success)
            {
                documentStore.MarkDirty(GraphDocumentSection.ViewState);
                SelectedWireRerouteId = "";
            }

            RefreshAll(result.Message);
            return;
        }

        if (!string.IsNullOrWhiteSpace(SelectedGroupId))
        {
            var result = editor.RemoveGroup(EnsureRule(), SelectedGroupId);
            if (result.Success)
            {
                documentStore.MarkDirty(GraphDocumentSection.ViewState);
                SelectedGroupId = "";
            }

            RefreshAll(result.Message);
            return;
        }

        if (SelectedNodeIds.Count > 1)
        {
            var selectedIds = SelectedNodeIds.ToList();
            var results = selectedIds.Select(nodeId => editor.RemoveNode(EnsureRule(), nodeId)).ToList();
            var changed = results.Any(result => result.Success && result.Changed);
            if (changed)
            {
                documentStore.MarkDirty(GraphDocumentSection.Rules);
            }

            SelectedNodeIds.Clear();
            SelectedNode = null;
            SelectedConnectionIndex = -1;
            RefreshAll(changed ? $"Deleted {results.Count(result => result.Success)} selected node(s)." : "No selected nodes were deleted.", includePreviewDiffInStatus: changed);
            return;
        }

        var deleteSelection = graphInteraction.DeleteSelection(EnsureRule(), SelectedFragmentId, SelectedConnectionIndex, SelectedNode);
        if (deleteSelection.ClearFragmentSelection)
        {
            SelectedFragmentId = "";
        }

        if (deleteSelection.ClearConnectionSelection)
        {
            SelectedConnectionIndex = -1;
        }

        if (deleteSelection.ClearNodeSelection)
        {
            SelectedNode = null;
            SelectedNodeIds.Clear();
        }

        if (deleteSelection.Result.Success && deleteSelection.Result.Changed)
        {
            documentStore.MarkDirty(GraphDocumentSection.Rules);
        }

        RefreshAll(
            deleteSelection.Result.Message,
            includePreviewDiffInStatus: deleteSelection.IncludePreviewDiffInStatus && deleteSelection.Result.Success && deleteSelection.Result.Changed);
    }

    private GraphClipboardSelection CurrentGraphClipboardSelection()
    {
        var nodeIds = SelectedNodeIds.Count > 0
            ? SelectedNodeIds.ToList()
            : SelectedNode is not null ? [SelectedNode.Id] : [];

        return new GraphClipboardSelection(
            nodeIds,
            SelectedGroupId,
            SelectedFragmentId,
            SelectedConnectionIndex);
    }

    private void AddFragmentAtGraphPoint(GraphFragmentKind kind, float graphX, float graphY)
    {
        var result = editor.AddFragment(EnsureRule(), kind, null, graphX, graphY);
        if (result.Success)
        {
            documentStore.MarkDirty(GraphDocumentSection.Rules);
        }

        RefreshAll(result.Message);
        if (result.Success)
        {
            SelectFragmentById(EnsureRule().Fragments.Last().Id);
        }
    }

}
