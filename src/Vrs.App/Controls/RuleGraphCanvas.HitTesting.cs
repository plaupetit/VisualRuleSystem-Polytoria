using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    // Hit-testing helpers only: visible ports, cable targeting, collapsed fragments, and reconnect/delete detection.
    private IReadOnlyList<GraphPortLayout> VisibleInputPins(RuleNode node)
    {
        return geometry.InputPins(node).Where(layout => ShouldShowPort(node, layout)).ToList();
    }

    private IReadOnlyList<GraphPortLayout> VisibleOutputPins(RuleNode node)
    {
        return geometry.OutputPins(node).Where(layout => ShouldShowPort(node, layout)).ToList();
    }

    private bool ShouldShowPort(RuleNode node, GraphPortLayout layout)
    {
        if (layout.PortKind == NodePortKind.Flow)
        {
            return true;
        }

        return ViewMode == GraphViewMode.Advanced ||
            ShowAdvancedPins ||
            node.ExposeAdvancedPorts;
    }

    private GraphPinHit? HitTestVisiblePin(IEnumerable<RuleNode> nodes, GraphPoint graphPoint)
    {
        foreach (var node in nodes.Reverse())
        {
            foreach (var layout in VisibleInputPins(node).Concat(VisibleOutputPins(node)))
            {
                if (geometry.IsNearPoint(graphPoint, layout.Point, RuleGraphGeometryService.PinRadius + 3.0F))
                {
                    return new GraphPinHit
                    {
                        NodeId = node.Id,
                        PortId = layout.PortId,
                        Direction = layout.Direction,
                        PortKind = layout.PortKind,
                        ColorHex = layout.ColorHex
                    };
                }
            }
        }

        return null;
    }

    private GraphConnectionHit? HitTestVisibleConnection(
        IReadOnlyList<GraphConnection> connections,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        IReadOnlyDictionary<string, RuleWireReroute> reroutesById,
        IReadOnlySet<string> visibleNodeIds,
        GraphPoint graphPoint)
    {
        for (var index = connections.Count - 1; index >= 0; index--)
        {
            if (!ConnectionVisible(connections[index], visibleNodeIds))
            {
                continue;
            }

            var hit = geometry.HitTestConnection([connections[index]], nodesById, reroutesById, graphPoint);
            if (hit is not null)
            {
                return new GraphConnectionHit { ConnectionIndex = index, SegmentIndex = hit.SegmentIndex };
            }
        }

        return null;
    }

    private RuleWireReroute? HitTestWireReroute(IReadOnlyList<RuleWireReroute> reroutes, IReadOnlySet<string> visibleRerouteIds, GraphPoint graphPoint)
    {
        var hitRadius = Math.Max(9.0F, 13.0F / Math.Max(0.25F, (float)Zoom));
        return reroutes
            .Where(reroute => visibleRerouteIds.Count == 0 || visibleRerouteIds.Contains(reroute.Id))
            .Reverse()
            .FirstOrDefault(reroute => geometry.IsNearPoint(graphPoint, new GraphPoint(reroute.GraphX, reroute.GraphY), hitRadius));
    }

    private bool TryDeleteConnectionAtGraphPoint(GraphPoint graphPoint)
    {
        var fragments = FragmentList();
        var nodes = VisibleNodeList(NodeList(), fragments);
        var pin = HitTestVisiblePin(nodes, graphPoint);
        if (pin is not null && TryDeleteConnectionForPin(pin))
        {
            return true;
        }

        var nodesById = nodes.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var reroutesById = WireRerouteList().ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var visibleNodeIds = nodes.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var connectionHit = HitTestVisibleConnection(ConnectionList(), nodesById, reroutesById, visibleNodeIds, graphPoint);
        if (connectionHit is null)
        {
            return false;
        }

        Host?.SelectGraphConnection(connectionHit.ConnectionIndex);
        Host?.DeleteGraphSelection();
        return true;
    }

    private bool TryDeleteConnectionForPin(GraphPinHit pin)
    {
        var connections = ConnectionList();
        for (var index = connections.Count - 1; index >= 0; index--)
        {
            var connection = connections[index];
            if (EndpointMatches(connection.From, pin) || EndpointMatches(connection.To, pin))
            {
                Host?.SelectGraphConnection(index);
                Host?.DeleteGraphSelection();
                return true;
            }
        }

        return false;
    }

    private bool TryBeginReconnectFromInputPin(GraphPinHit inputPin, GraphPoint graphPoint)
    {
        if (!inputPin.IsInput)
        {
            return false;
        }

        var connections = ConnectionList();
        for (var index = connections.Count - 1; index >= 0; index--)
        {
            var connection = connections[index];
            if (!EndpointMatches(connection.To, inputPin))
            {
                continue;
            }

            var sourceNode = NodeList().FirstOrDefault(node =>
                string.Equals(node.Id, connection.From.NodeId, StringComparison.OrdinalIgnoreCase));
            if (sourceNode is null)
            {
                return false;
            }

            var sourcePort = geometry.FindPortLayout(sourceNode, connection.From.PortId);
            if (sourcePort is null)
            {
                return false;
            }

            Host?.SelectGraphConnection(index);
            Host?.DeleteGraphSelection();
            Host?.SelectGraphConnection(-1);
            Host?.SelectGraphFragment("");

            pendingOutputPin = new GraphPinHit
            {
                NodeId = connection.From.NodeId,
                PortId = connection.From.PortId,
                Direction = NodePortDirection.Output,
                PortKind = sourcePort.PortKind,
                ColorHex = sourcePort.ColorHex
            };
            pendingGraphPoint = graphPoint;
            return true;
        }

        return false;
    }

    private static bool EndpointMatches(GraphEndpoint endpoint, GraphPinHit pin)
    {
        return string.Equals(endpoint.NodeId, pin.NodeId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(endpoint.PortId, pin.PortId, StringComparison.OrdinalIgnoreCase);
    }

    private GraphFragment? HitTestFragment(IEnumerable<GraphFragment> fragments, GraphPoint graphPoint)
    {
        if (ViewMode != GraphViewMode.Simple)
        {
            return null;
        }

        return fragments
            .Reverse()
            .FirstOrDefault(fragment => fragment.Collapsed && FragmentRect(fragment).Contains(graphPoint));
    }

    private RuleNodeGroup? HitTestGroup(IReadOnlyList<RuleNodeGroup> groups, GraphPoint graphPoint)
    {
        return GroupsInDrawOrder(groups)
            .Reverse()
            .FirstOrDefault(group => GroupRect(group).Contains(graphPoint));
    }

    private RuleNodeGroup? HitTestGroupHeader(IReadOnlyList<RuleNodeGroup> groups, GraphPoint graphPoint)
    {
        return GroupsInDrawOrder(groups)
            .Reverse()
            .FirstOrDefault(group => GroupHeaderRect(group).Contains(graphPoint));
    }

    private RuleNodeGroup? HitTestGroupResizeHandle(IReadOnlyList<RuleNodeGroup> groups, GraphPoint graphPoint)
    {
        return GroupsInDrawOrder(groups)
            .Reverse()
            .FirstOrDefault(group => GroupResizeHandleRect(group).Contains(graphPoint));
    }

}
