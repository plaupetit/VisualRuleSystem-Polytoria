using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.App.Services;

/// <summary>
/// Coordinates graph gesture decisions that are independent from Avalonia
/// bindings. The ViewModel remains responsible for selection properties,
/// dirty flags, and refresh notifications.
/// </summary>
public sealed class GraphInteractionService
{
    private readonly RuleGraphEditService editor;

    public GraphInteractionService(RuleGraphEditService? editor = null)
    {
        this.editor = editor ?? new RuleGraphEditService();
    }

    public GraphAddCatalogNodeResult AddCatalogNode(
        Rule rule,
        IEnumerable<NodeCatalogEntry> catalogEntries,
        string catalogId,
        float graphX,
        float graphY,
        string connectFromNodeId = "",
        string connectFromPortId = "")
    {
        var entry = NodeCatalogService.FindByCatalogId(catalogEntries, catalogId);
        if (entry is null || !NodeCatalogService.IsAddable(entry))
        {
            return new GraphAddCatalogNodeResult(
                Result: GraphEditResult.Fail($"No addable catalog node exists: {catalogId}"),
                CreatedNode: null,
                StatusText: $"No addable catalog node exists: {catalogId}",
                IncludePreviewDiffInStatus: false);
        }

        var node = NodeCatalogService.CreateNode(entry, graphX, graphY);
        var addResult = editor.AddNode(rule, node);
        if (!addResult.Success)
        {
            return new GraphAddCatalogNodeResult(addResult, node, addResult.Message, false);
        }

        var status = addResult.Message;
        var connectedFromPendingWire = false;
        if (!string.IsNullOrWhiteSpace(connectFromNodeId) && !string.IsNullOrWhiteSpace(connectFromPortId))
        {
            var input = FirstCompatibleInputPort(rule, connectFromNodeId, connectFromPortId, node);
            if (input is not null)
            {
                var connectionResult = editor.AddConnection(rule, connectFromNodeId, connectFromPortId, node.Id, input.Id);
                connectedFromPendingWire = connectionResult.Success && connectionResult.Changed;
                status = connectionResult.Success
                    ? $"Added and connected node: {node.Label}"
                    : $"Added node: {node.Label}. {connectionResult.Message}";
            }
            else
            {
                status = $"Added node: {node.Label}. No compatible input port was found.";
            }
        }

        return new GraphAddCatalogNodeResult(addResult, node, status, connectedFromPendingWire);
    }

    public NodeCatalogEntry? FindAddableCatalogEntryForKind(
        IEnumerable<NodeCatalogEntry> catalogEntries,
        NodeKind kind,
        GraphScriptKind scriptKind)
    {
        return catalogEntries.FirstOrDefault(item =>
            item.Kind == kind &&
            NodeCatalogService.IsAddable(item) &&
            NodeCatalogService.IsCompatibleWithScriptKind(item, scriptKind));
    }

    public IReadOnlyList<string> SelectedNodeIdsForFragment(
        Rule rule,
        RuleNode? selectedNode,
        int selectedConnectionIndex)
    {
        if (selectedNode is not null)
        {
            return [selectedNode.Id];
        }

        if (selectedConnectionIndex >= 0 && selectedConnectionIndex < rule.Connections.Count)
        {
            var connection = rule.Connections[selectedConnectionIndex];
            return [connection.From.NodeId, connection.To.NodeId];
        }

        return [];
    }

    public GraphDeleteSelectionResult DeleteSelection(
        Rule rule,
        string selectedFragmentId,
        int selectedConnectionIndex,
        RuleNode? selectedNode)
    {
        if (!string.IsNullOrWhiteSpace(selectedFragmentId))
        {
            return new GraphDeleteSelectionResult(
                editor.RemoveFragment(rule, selectedFragmentId),
                ClearFragmentSelection: true,
                ClearConnectionSelection: false,
                ClearNodeSelection: false,
                IncludePreviewDiffInStatus: true);
        }

        if (selectedConnectionIndex >= 0)
        {
            return new GraphDeleteSelectionResult(
                editor.RemoveConnection(rule, selectedConnectionIndex),
                ClearFragmentSelection: false,
                ClearConnectionSelection: true,
                ClearNodeSelection: false,
                IncludePreviewDiffInStatus: true);
        }

        if (selectedNode is not null)
        {
            return new GraphDeleteSelectionResult(
                editor.RemoveNode(rule, selectedNode.Id),
                ClearFragmentSelection: false,
                ClearConnectionSelection: false,
                ClearNodeSelection: true,
                IncludePreviewDiffInStatus: true);
        }

        return new GraphDeleteSelectionResult(
            GraphEditResult.Fail("No node or wire is selected."),
            ClearFragmentSelection: false,
            ClearConnectionSelection: false,
            ClearNodeSelection: false,
            IncludePreviewDiffInStatus: false);
    }

    private NodePort? FirstCompatibleInputPort(Rule rule, string fromNodeId, string fromPortId, RuleNode node)
    {
        return node.Ports
            .Where(port => port.Direction == NodePortDirection.Input)
            .OrderBy(port => port.Order)
            .ThenBy(port => port.Label, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(port => editor.ValidateEndpointPair(rule, fromNodeId, fromPortId, node.Id, port.Id).Success);
    }
}

/// <summary>
/// Result of a catalog-node insertion, including the created model node when insertion succeeded.
/// </summary>
/// <remarks>
/// The ViewModel uses this payload to update selection, status text, and preview
/// diff behavior without duplicating graph edit rules.
/// </remarks>
public sealed record GraphAddCatalogNodeResult(
    GraphEditResult Result,
    RuleNode? CreatedNode,
    string StatusText,
    bool IncludePreviewDiffInStatus);

/// <summary>
/// Result of deleting the current graph selection and the UI selections that must be cleared afterward.
/// </summary>
public sealed record GraphDeleteSelectionResult(
    GraphEditResult Result,
    bool ClearFragmentSelection,
    bool ClearConnectionSelection,
    bool ClearNodeSelection,
    bool IncludePreviewDiffInStatus);
