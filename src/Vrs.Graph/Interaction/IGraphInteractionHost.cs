using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.Graph.Interaction;

/// <summary>
/// Boundary used by graph controls to request durable graph edits and UI-owned
/// state changes without depending on Avalonia view models or app services.
/// </summary>
/// <remarks>
/// Implementations own mutation, validation, preview refresh, dirty state, and
/// command availability. Canvas code may track pointer interaction locally, but
/// it should route persistent model changes through this host.
/// </remarks>
public interface IGraphInteractionHost
{
    /// <summary>True when the host has graph items available for an internal VRS paste operation.</summary>
    bool CanPasteGraphClipboard { get; }

    /// <summary>Selects a node, or clears node selection when <paramref name="node"/> is null.</summary>
    void SelectGraphNode(RuleNode? node);

    /// <summary>Selects multiple nodes for authoring commands such as visual grouping.</summary>
    void SelectGraphNodes(IReadOnlyList<string> nodeIds, string primaryNodeId = "");

    /// <summary>Selects a connection by its current rule connection index.</summary>
    void SelectGraphConnection(int connectionIndex);

    /// <summary>Selects a fragment by stable graph fragment id.</summary>
    void SelectGraphFragment(string fragmentId);

    /// <summary>Selects a visual-only node group by stable group id.</summary>
    void SelectGraphGroup(string groupId);

    /// <summary>Selects a visual-only wire reroute by stable reroute id.</summary>
    void SelectGraphWireReroute(string rerouteId);

    /// <summary>Stores the graph-space point used by palette insertion and context actions.</summary>
    void SetGraphInsertionPoint(float graphX, float graphY);

    /// <summary>Reports the current viewport size so fit and insertion workflows can use real canvas bounds.</summary>
    void UpdateGraphViewport(double width, double height);

    /// <summary>Attempts to add a validated connection and returns the edit result for UI feedback.</summary>
    GraphEditResult TryAddGraphConnection(string fromNodeId, string fromPortId, string toNodeId, string toPortId);

    /// <summary>Persists a node move after the canvas drag interaction has produced graph-space coordinates.</summary>
    void NotifyGraphNodeMoved(string nodeId, float graphX, float graphY);

    /// <summary>Persists a batch move for visual groups and the nodes moved with them.</summary>
    void NotifyGraphGroupsMoved(IReadOnlyList<GraphGroupMove> groupMoves, IReadOnlyList<GraphNodeMove> nodeMoves, IReadOnlyList<GraphWireRerouteMove> rerouteMoves, string primaryGroupId);

    /// <summary>Persists a visual group resize after the canvas drag interaction finishes.</summary>
    void NotifyGraphGroupResized(string groupId, float width, float height);

    /// <summary>Adds a visual-only reroute to a connection at a graph-space point.</summary>
    void AddWireRerouteToConnection(int connectionIndex, float graphX, float graphY, int insertAtIndex);

    /// <summary>Persists a visual-only wire reroute move after canvas drag.</summary>
    void NotifyWireRerouteMoved(string rerouteId, float graphX, float graphY);

    /// <summary>Updates a moved group's parent based on its final visual bounds.</summary>
    void AutoParentGraphGroup(string groupId);

    /// <summary>Duplicates the selected graph node and lets the host choose placement and follow-up selection.</summary>
    void DuplicateGraphNode(string nodeId);

    /// <summary>Removes all connections attached to the node without deleting the node itself.</summary>
    void DisconnectGraphNode(string nodeId);

    /// <summary>Toggles whether the node participates in validation, preview, and export.</summary>
    void ToggleGraphNodeEnabled(string nodeId);

    /// <summary>Toggles runtime debug logging markers for the node.</summary>
    void ToggleGraphNodeDebug(string nodeId);

    /// <summary>Toggles the authoring breakpoint marker for the node.</summary>
    void ToggleGraphNodeBreakpoint(string nodeId);

    /// <summary>Adds the catalog selection at the requested graph-space point.</summary>
    void AddSelectedCatalogNodeAtGraphPoint(float graphX, float graphY);

    /// <summary>
    /// Adds a catalog node at the requested graph-space point and optionally connects it from an existing output port.
    /// </summary>
    void AddCatalogNodeAtGraphPoint(string catalogId, float graphX, float graphY, string connectFromNodeId = "", string connectFromPortId = "");

    /// <summary>Adds a generic node by kind when the user inserts from a broad palette category.</summary>
    void AddCatalogNodeKindAtGraphPoint(NodeKind kind, float graphX, float graphY);

    /// <summary>Adds a visual fragment that groups rule nodes.</summary>
    void AddRuleFragmentAtGraphPoint(float graphX, float graphY);

    /// <summary>Adds a visual fragment that groups state-machine context.</summary>
    void AddStateFragmentAtGraphPoint(float graphX, float graphY);

    /// <summary>Creates a fragment around the current host-owned graph selection.</summary>
    void CreateFragmentFromSelection(GraphFragmentKind kind);

    /// <summary>Creates a visual-only group around the current host-owned node selection.</summary>
    void CreateGroupFromSelection();

    /// <summary>Creates an empty visual-only group at the requested graph-space point.</summary>
    void CreateEmptyGroupAtGraphPoint(float graphX, float graphY);

    /// <summary>Expands or focuses a fragment from a collapsed or summarized presentation.</summary>
    void ExpandGraphFragment(string fragmentId);

    /// <summary>Requests a viewport fit around the current visible graph content.</summary>
    void FrameGraphView();

    /// <summary>Shows a node context menu at a graph-space position chosen by the canvas.</summary>
    void ShowNodeContextMenu(string nodeId, float graphX, float graphY);

    /// <summary>Shows a canvas context menu at a graph-space position chosen by the canvas.</summary>
    void ShowCanvasContextMenu(float graphX, float graphY);

    /// <summary>Copies the current host-owned graph selection into the internal VRS clipboard.</summary>
    void CopyGraphSelection();

    /// <summary>Copies the current host-owned graph selection, then deletes it only when copying succeeded.</summary>
    void CutGraphSelection();

    /// <summary>Pastes the internal VRS clipboard at the requested graph-space position.</summary>
    void PasteGraphClipboard(float graphX, float graphY);

    /// <summary>Deletes the current host-owned graph selection.</summary>
    void DeleteGraphSelection();
}
