using Avalonia;
using Avalonia.Input;
using Vrs.Graph.Modeling;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        var graphPoint = ToGraph(point);
        Host?.SetGraphInsertionPoint(graphPoint.X, graphPoint.Y);

        if (nodePaletteOpen && HandleNodePalettePointerMoved(point))
        {
            ClearHoveredNode();
            e.Handled = true;
            return;
        }

        if (pendingOutputPin is not null)
        {
            ClearHoveredNode();
            pendingGraphPoint = graphPoint;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(draggedNodeId))
        {
            ClearHoveredNode();
            var deltaX = graphPoint.X - dragStartGraph.X;
            var deltaY = graphPoint.Y - dragStartGraph.Y;
            foreach (var node in NodeList().Where(item => draggedNodeStarts.ContainsKey(item.Id)))
            {
                var start = draggedNodeStarts[node.Id];
                var constrained = ConstrainNodePositionForDrag(node, start.X + deltaX, start.Y + deltaY);
                node.GraphX = constrained.X;
                node.GraphY = constrained.Y;
                node.GraphPositionSet = true;
            }

            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(draggedWireRerouteId))
        {
            ClearHoveredNode();
            var reroute = WireRerouteList().FirstOrDefault(item => string.Equals(item.Id, draggedWireRerouteId, StringComparison.OrdinalIgnoreCase));
            if (reroute is not null)
            {
                var constrained = ConstrainReroutePositionForDrag(
                    reroute,
                    draggedWireRerouteStart.X + (graphPoint.X - dragStartGraph.X),
                    draggedWireRerouteStart.Y + (graphPoint.Y - dragStartGraph.Y));
                reroute.GraphX = constrained.X;
                reroute.GraphY = constrained.Y;
                InvalidateVisual();
            }

            e.Handled = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(draggedGroupId))
        {
            ClearHoveredNode();
            var deltaX = graphPoint.X - dragStartGraph.X;
            var deltaY = graphPoint.Y - dragStartGraph.Y;
            foreach (var group in GroupList().Where(item => draggedGroupStarts.ContainsKey(item.Id)))
            {
                var start = draggedGroupStarts[group.Id];
                group.GraphX = start.X + deltaX;
                group.GraphY = start.Y + deltaY;
            }

            foreach (var node in NodeList().Where(item => draggedNodeStarts.ContainsKey(item.Id)))
            {
                var start = draggedNodeStarts[node.Id];
                node.GraphX = start.X + deltaX;
                node.GraphY = start.Y + deltaY;
                node.GraphPositionSet = true;
            }

            foreach (var reroute in WireRerouteList().Where(item => draggedWireRerouteStarts.ContainsKey(item.Id)))
            {
                var start = draggedWireRerouteStarts[reroute.Id];
                reroute.GraphX = start.X + deltaX;
                reroute.GraphY = start.Y + deltaY;
            }

            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (resizingGroup)
        {
            ClearHoveredNode();
            var group = GroupList().FirstOrDefault(item => string.Equals(item.Id, resizingGroupId, StringComparison.OrdinalIgnoreCase));
            if (group is not null)
            {
                group.Width = MathF.Max(180.0F, resizedGroupStartWidth + (graphPoint.X - dragStartGraph.X));
                group.Height = MathF.Max(120.0F, resizedGroupStartHeight + (graphPoint.Y - dragStartGraph.Y));
                InvalidateVisual();
            }

            e.Handled = true;
            return;
        }

        if (selectingNodes)
        {
            ClearHoveredNode();
            selectionCurrentPoint = point;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (panning)
        {
            ClearHoveredNode();
            PanX = panStartX + (point.X - panStartPointer.X);
            PanY = panStartY + (point.Y - panStartPointer.Y);
            e.Handled = true;
            return;
        }

        UpdateHoveredNode(point, graphPoint);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ClearHoveredNode();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var properties = e.GetCurrentPoint(this).Properties;
        var graphPoint = ToGraph(e.GetPosition(this));

        if (pendingOutputPin is not null)
        {
            if (properties.PointerUpdateKind == PointerUpdateKind.RightButtonReleased)
            {
                CancelPendingConnection(e.Pointer);
                e.Handled = true;
                return;
            }

            if (properties.PointerUpdateKind != PointerUpdateKind.LeftButtonReleased)
            {
                e.Handled = true;
                return;
            }

            var fromPin = pendingOutputPin;
            var inputPin = HitTestVisiblePin(VisibleNodeList(NodeList(), FragmentList()), graphPoint);
            if (inputPin is not null && inputPin.IsInput)
            {
                _ = Host?.TryAddGraphConnection(pendingOutputPin.NodeId, pendingOutputPin.PortId, inputPin.NodeId, inputPin.PortId);
            }
            else
            {
                OpenNodePalette(graphPoint, fromPin);
            }

            pendingOutputPin = null;
            e.Pointer.Capture(null);
            e.Handled = true;
            InvalidateVisual();
            return;
        }

        if (!string.IsNullOrWhiteSpace(draggedNodeId))
        {
            var movedNodes = BuildDraggedNodeMoves();
            foreach (var move in movedNodes)
            {
                Host?.NotifyGraphNodeMoved(move.NodeId, move.GraphX, move.GraphY);
            }

            draggedNodeId = "";
            draggedNodeStarts.Clear();
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(draggedWireRerouteId))
        {
            var reroute = WireRerouteList().FirstOrDefault(item => string.Equals(item.Id, draggedWireRerouteId, StringComparison.OrdinalIgnoreCase));
            if (reroute is not null)
            {
                Host?.NotifyWireRerouteMoved(reroute.Id, reroute.GraphX, reroute.GraphY);
            }

            draggedWireRerouteId = "";
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(draggedGroupId))
        {
            var primaryGroupId = draggedGroupId;
            Host?.NotifyGraphGroupsMoved(BuildDraggedGroupMoves(), BuildDraggedNodeMoves(), BuildDraggedWireRerouteMoves(), primaryGroupId);
            Host?.AutoParentGraphGroup(primaryGroupId);
            draggedGroupId = "";
            draggedGroupStarts.Clear();
            draggedNodeStarts.Clear();
            draggedWireRerouteStarts.Clear();
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (resizingGroup)
        {
            var group = GroupList().FirstOrDefault(item => string.Equals(item.Id, resizingGroupId, StringComparison.OrdinalIgnoreCase));
            if (group is not null)
            {
                Host?.NotifyGraphGroupResized(group.Id, group.Width, group.Height);
            }

            resizingGroup = false;
            resizingGroupId = "";
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (selectingNodes)
        {
            selectionCurrentPoint = e.GetPosition(this);
            var selectionSize = RectFromPoints(selectionStartPoint, selectionCurrentPoint).Size;
            if (selectionSize.Width > 4 || selectionSize.Height > 4)
            {
                var selected = NodesInsideSelection(graphPoint);
                Host?.SelectGraphNodes(selected, selected.FirstOrDefault() ?? "");
            }
            else
            {
                ClearCanvasSelection();
            }

            selectingNodes = false;
            e.Pointer.Capture(null);
            e.Handled = true;
            InvalidateVisual();
            return;
        }

        if (panning)
        {
            panning = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void CancelPendingConnection(IPointer pointer)
    {
        pendingOutputPin = null;
        pointer.Capture(null);
        InvalidateVisual();
    }

    private void UpdateHoveredNode(Point point, GraphPoint graphPoint)
    {
        var node = geometry.HitTestNode(VisibleNodeList(NodeList(), FragmentList()), graphPoint);
        var nextId = node?.Id ?? "";
        var movedEnough = Math.Abs(point.X - nodeTooltipPointerPoint.X) > 4.0 ||
            Math.Abs(point.Y - nodeTooltipPointerPoint.Y) > 4.0;

        if (string.Equals(hoveredNodeId, nextId, StringComparison.Ordinal) && (!movedEnough || string.IsNullOrWhiteSpace(nextId)))
        {
            return;
        }

        hoveredNodeId = nextId;
        nodeTooltipPointerPoint = point;
        InvalidateVisual();
    }

    private void ClearHoveredNode()
    {
        if (string.IsNullOrWhiteSpace(hoveredNodeId))
        {
            return;
        }

        hoveredNodeId = "";
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var pointer = e.GetPosition(this);
        if (nodePaletteOpen && NodePaletteBounds(NodePaletteEntries()).Contains(pointer))
        {
            var entries = NodePaletteEntries();
            var maxVisible = NodePaletteMaxVisibleEntries(entries.Count);
            nodePaletteScrollIndex = Math.Clamp(nodePaletteScrollIndex + (e.Delta.Y < 0 ? 1 : -1), 0, Math.Max(0, entries.Count - maxVisible));
            nodePaletteSelectedIndex = Math.Clamp(nodePaletteSelectedIndex, nodePaletteScrollIndex, Math.Max(nodePaletteScrollIndex, Math.Min(entries.Count - 1, nodePaletteScrollIndex + maxVisible - 1)));
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        var before = ToGraph(pointer);
        var factor = e.Delta.Y > 0 ? 1.12 : 0.88;
        Zoom = Math.Clamp(Zoom * factor, 0.25, 2.5);
        PanX = pointer.X - (before.X * Zoom);
        PanY = pointer.Y - (before.Y * Zoom);
        e.Handled = true;
    }
}
