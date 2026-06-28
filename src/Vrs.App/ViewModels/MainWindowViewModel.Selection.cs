using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    private enum CanvasSelectionTarget
    {
        None,
        Node,
        Connection,
        Fragment,
        Group,
        WireReroute
    }

    private bool preserveSelectedNodeIdsForSelectionChange;

    private void ClearInactiveCanvasSelections(CanvasSelectionTarget activeTarget, bool clearSelectedNodeIds)
    {
        // Selection is exposed as several bindable properties for XAML, but
        // delete/edit commands must see exactly one active canvas target.
        if (activeTarget != CanvasSelectionTarget.Node && SelectedNode is not null)
        {
            SelectedNode = null;
        }

        if (activeTarget != CanvasSelectionTarget.Connection && SelectedConnectionIndex >= 0)
        {
            SelectedConnectionIndex = -1;
        }

        if (activeTarget != CanvasSelectionTarget.Fragment && !string.IsNullOrWhiteSpace(SelectedFragmentId))
        {
            SelectedFragmentId = "";
        }

        if (activeTarget != CanvasSelectionTarget.Group && !string.IsNullOrWhiteSpace(SelectedGroupId))
        {
            SelectedGroupId = "";
        }

        if (activeTarget != CanvasSelectionTarget.WireReroute && !string.IsNullOrWhiteSpace(SelectedWireRerouteId))
        {
            SelectedWireRerouteId = "";
        }

        if (clearSelectedNodeIds)
        {
            SelectedNodeIds.Clear();
        }
    }

    private void ReplaceSelectedNodeIds(IEnumerable<string> nodeIds)
    {
        SelectedNodeIds.Clear();
        foreach (var nodeId in nodeIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            SelectedNodeIds.Add(nodeId);
        }
    }

    private bool HasActiveNonNodeCanvasSelection()
    {
        return SelectedConnectionIndex >= 0 ||
            !string.IsNullOrWhiteSpace(SelectedFragmentId) ||
            !string.IsNullOrWhiteSpace(SelectedGroupId) ||
            !string.IsNullOrWhiteSpace(SelectedWireRerouteId);
    }

    private void SelectNodePreservingMultiSelection(RuleNode? node, IReadOnlyList<string> selectedNodeIds)
    {
        ReplaceSelectedNodeIds(selectedNodeIds);
        preserveSelectedNodeIdsForSelectionChange = true;
        try
        {
            SelectedNode = node;
        }
        finally
        {
            preserveSelectedNodeIdsForSelectionChange = false;
        }

        ClearInactiveCanvasSelections(node is null ? CanvasSelectionTarget.None : CanvasSelectionTarget.Node, clearSelectedNodeIds: false);
    }
}
