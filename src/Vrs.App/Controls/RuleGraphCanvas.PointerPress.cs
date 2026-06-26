using Avalonia;
using Avalonia.Input;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    // Pointer-press handling decides which gesture owns the pointer; the move
    // and release slices then continue that gesture from stored canvas state.
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var point = e.GetPosition(this);
        var properties = e.GetCurrentPoint(this).Properties;
        var graphPoint = ToGraph(point);
        Host?.SetGraphInsertionPoint(graphPoint.X, graphPoint.Y);

        if (nodePaletteOpen && HandleNodePalettePointerPressed(point, properties))
        {
            e.Handled = true;
            return;
        }

        if (properties.IsRightButtonPressed)
        {
            if (TryHandleRightPointerPress(graphPoint, e.Pointer, e.KeyModifiers))
            {
                e.Handled = true;
            }

            return;
        }

        if (TryBeginPanPointerPress(point, properties, e.Pointer))
        {
            e.Handled = true;
            return;
        }

        if (!properties.IsLeftButtonPressed)
        {
            return;
        }

        var fragments = FragmentList();
        var groups = GroupList();
        var nodes = VisibleNodeList(NodeList(), fragments);
        var connections = ConnectionList();
        var visibleNodeIds = nodes.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var visibleRerouteIds = VisibleWireRerouteIds(connections, visibleNodeIds);
        if (TryBeginPinPointerPress(nodes, graphPoint, e.Pointer) ||
            TryBeginWireRerouteDragPointerPress(WireRerouteList(), visibleRerouteIds, graphPoint, e.Pointer) ||
            TryBeginNodeDragPointerPress(nodes, graphPoint, e.Pointer, e.KeyModifiers) ||
            TryBeginGroupResizePointerPress(groups, graphPoint, e.Pointer) ||
            TryBeginGroupHeaderDragPointerPress(groups, graphPoint, e.Pointer) ||
            TrySelectFragmentPointerPress(fragments, graphPoint) ||
            TrySelectConnectionPointerPress(nodes, graphPoint) ||
            TryBeginGroupBodyDragPointerPress(groups, graphPoint, e.Pointer) ||
            TrySelectGroupPointerPress(groups, graphPoint))
        {
            e.Handled = true;
            return;
        }

        BeginSelectionRectangle(point, graphPoint, e.Pointer);
        e.Handled = true;
    }

    private bool TryHandleRightPointerPress(GraphPoint graphPoint, IPointer pointer, KeyModifiers modifiers)
    {
        if (pendingOutputPin is not null)
        {
            CancelPendingConnection(pointer);
            return true;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift) && TryDeleteConnectionAtGraphPoint(graphPoint))
        {
            InvalidateVisual();
            return true;
        }

        OpenContextMenu(graphPoint);
        return true;
    }

    private bool TryBeginPanPointerPress(Point point, PointerPointProperties properties, IPointer pointer)
    {
        if (!properties.IsMiddleButtonPressed && (!properties.IsLeftButtonPressed || !spacePanning))
        {
            return false;
        }

        BeginPan(point);
        pointer.Capture(this);
        return true;
    }

    private bool TryBeginPinPointerPress(IReadOnlyList<RuleNode> nodes, GraphPoint graphPoint, IPointer pointer)
    {
        var pin = HitTestVisiblePin(nodes, graphPoint);
        if (pin is not null && pin.IsInput && TryBeginReconnectFromInputPin(pin, graphPoint))
        {
            pointer.Capture(this);
            InvalidateVisual();
            return true;
        }

        if (pin is null || pin.IsInput)
        {
            return false;
        }

        pendingOutputPin = pin;
        pendingGraphPoint = graphPoint;
        Host?.SelectGraphConnection(-1);
        Host?.SelectGraphFragment("");
        Host?.SelectGraphGroup("");
        Host?.SelectGraphWireReroute("");
        pointer.Capture(this);
        InvalidateVisual();
        return true;
    }

    private bool TryBeginWireRerouteDragPointerPress(IReadOnlyList<RuleWireReroute> reroutes, IReadOnlySet<string> visibleRerouteIds, GraphPoint graphPoint, IPointer pointer)
    {
        var reroute = HitTestWireReroute(reroutes, visibleRerouteIds, graphPoint);
        if (reroute is null)
        {
            return false;
        }

        Host?.SelectGraphNode(null);
        Host?.SelectGraphConnection(-1);
        Host?.SelectGraphFragment("");
        Host?.SelectGraphGroup("");
        Host?.SelectGraphWireReroute(reroute.Id);
        draggedWireRerouteId = reroute.Id;
        dragStartGraph = graphPoint;
        draggedWireRerouteStart = new GraphPoint(reroute.GraphX, reroute.GraphY);
        pointer.Capture(this);
        InvalidateVisual();
        return true;
    }

    private bool TryBeginNodeDragPointerPress(IReadOnlyList<RuleNode> nodes, GraphPoint graphPoint, IPointer pointer, KeyModifiers modifiers)
    {
        var node = geometry.HitTestNode(nodes, graphPoint);
        if (node is null)
        {
            return false;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            ToggleNodeSelection(node);
            InvalidateVisual();
            return true;
        }

        Host?.SelectGraphNode(node);
        Host?.SelectGraphFragment("");
        Host?.SelectGraphGroup("");
        Host?.SelectGraphWireReroute("");
        draggedNodeId = node.Id;
        dragStartGraph = graphPoint;
        draggedNodeStart = new GraphPoint(node.GraphX, node.GraphY);
        draggedNodeStarts.Clear();
        var selectedIds = SelectedNodeIdSet();
        var dragIds = selectedIds.Contains(node.Id) && selectedIds.Count > 1
            ? selectedIds
            : new HashSet<string>([node.Id], StringComparer.OrdinalIgnoreCase);
        foreach (var dragged in nodes.Where(item => dragIds.Contains(item.Id)))
        {
            draggedNodeStarts[dragged.Id] = new GraphPoint(dragged.GraphX, dragged.GraphY);
        }

        pointer.Capture(this);
        InvalidateVisual();
        return true;
    }

    private bool TryBeginGroupResizePointerPress(IReadOnlyList<RuleNodeGroup> groups, GraphPoint graphPoint, IPointer pointer)
    {
        var group = HitTestGroupResizeHandle(groups, graphPoint);
        if (group is null || !string.Equals(group.Id, SelectedGroupId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Host?.SelectGraphGroup(group.Id);
        resizingGroup = true;
        resizingGroupId = group.Id;
        dragStartGraph = graphPoint;
        resizedGroupStartWidth = group.Width;
        resizedGroupStartHeight = group.Height;
        pointer.Capture(this);
        InvalidateVisual();
        return true;
    }

    private bool TryBeginGroupHeaderDragPointerPress(IReadOnlyList<RuleNodeGroup> groups, GraphPoint graphPoint, IPointer pointer)
    {
        var group = HitTestGroupHeader(groups, graphPoint);
        if (group is null)
        {
            return false;
        }

        BeginGroupDrag(group, graphPoint, pointer);
        return true;
    }

    private bool TryBeginGroupBodyDragPointerPress(IReadOnlyList<RuleNodeGroup> groups, GraphPoint graphPoint, IPointer pointer)
    {
        var group = HitTestGroup(groups, graphPoint);
        if (group is null)
        {
            return false;
        }

        BeginGroupDrag(group, graphPoint, pointer);
        return true;
    }

    private void BeginGroupDrag(RuleNodeGroup group, GraphPoint graphPoint, IPointer pointer)
    {
        Host?.SelectGraphGroup(group.Id);
        draggedGroupId = group.Id;
        dragStartGraph = graphPoint;
        CaptureGroupDragStarts(group.Id);
        pointer.Capture(this);
        InvalidateVisual();
    }

    private bool TrySelectGroupPointerPress(IReadOnlyList<RuleNodeGroup> groups, GraphPoint graphPoint)
    {
        var group = HitTestGroup(groups, graphPoint);
        if (group is null)
        {
            return false;
        }

        Host?.SelectGraphNode(null);
        Host?.SelectGraphConnection(-1);
        Host?.SelectGraphFragment("");
        Host?.SelectGraphWireReroute("");
        Host?.SelectGraphGroup(group.Id);
        InvalidateVisual();
        return true;
    }

    private bool TrySelectFragmentPointerPress(IReadOnlyList<GraphFragment> fragments, GraphPoint graphPoint)
    {
        var fragment = HitTestFragment(fragments, graphPoint);
        if (fragment is null)
        {
            return false;
        }

        Host?.SelectGraphNode(null);
        Host?.SelectGraphConnection(-1);
        Host?.SelectGraphGroup("");
        Host?.SelectGraphWireReroute("");
        Host?.SelectGraphFragment(fragment.Id);
        InvalidateVisual();
        return true;
    }

    private bool TrySelectConnectionPointerPress(IReadOnlyList<RuleNode> nodes, GraphPoint graphPoint)
    {
        var nodesById = nodes.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var reroutesById = WireRerouteList().ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var visibleNodeIds = nodes.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var connectionHit = HitTestVisibleConnection(ConnectionList(), nodesById, reroutesById, visibleNodeIds, graphPoint);
        if (connectionHit is null)
        {
            return false;
        }

        Host?.SelectGraphNode(null);
        Host?.SelectGraphConnection(connectionHit.ConnectionIndex);
        Host?.SelectGraphFragment("");
        Host?.SelectGraphGroup("");
        Host?.SelectGraphWireReroute("");
        InvalidateVisual();
        return true;
    }

    private void ClearCanvasSelection()
    {
        Host?.SelectGraphNode(null);
        Host?.SelectGraphConnection(-1);
        Host?.SelectGraphFragment("");
        Host?.SelectGraphGroup("");
        Host?.SelectGraphWireReroute("");
    }

    private void ToggleNodeSelection(RuleNode node)
    {
        var selected = SelectedNodeIdSet();
        if (!selected.Add(node.Id))
        {
            selected.Remove(node.Id);
        }

        Host?.SelectGraphNodes(selected.ToList(), selected.Contains(node.Id) ? node.Id : selected.FirstOrDefault() ?? "");
    }

    private void BeginSelectionRectangle(Point point, GraphPoint graphPoint, IPointer pointer)
    {
        ClearCanvasSelection();
        selectingNodes = true;
        selectionStartPoint = point;
        selectionCurrentPoint = point;
        selectionStartGraph = graphPoint;
        pointer.Capture(this);
    }
}
