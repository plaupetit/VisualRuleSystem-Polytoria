using Vrs.Graph.Model;

namespace Vrs.Graph.Modeling;

public readonly record struct GraphPoint(float X, float Y);

public readonly record struct GraphRect(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;

    public bool Contains(GraphPoint point)
    {
        return point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;
    }
}

public readonly record struct GraphBezier(GraphPoint P0, GraphPoint P1, GraphPoint P2, GraphPoint P3);

public sealed class GraphPortLayout
{
    public string NodeId { get; set; } = "";
    public string PortId { get; set; } = "";
    public string Label { get; set; } = "";
    public NodePortDirection Direction { get; set; }
    public NodePortKind PortKind { get; set; }
    public string DataType { get; set; } = "Flow";
    public string ColorHex { get; set; } = "#7c8794";
    public GraphPoint Point { get; set; }
}

public sealed class GraphPinHit
{
    public string NodeId { get; set; } = "";
    public string PortId { get; set; } = "";
    public bool IsInput => Direction == NodePortDirection.Input;
    public NodePortDirection Direction { get; set; }
    public NodePortKind PortKind { get; set; }
    public string ColorHex { get; set; } = "#7c8794";
}

public sealed class GraphConnectionHit
{
    public int ConnectionIndex { get; set; } = -1;
    public int SegmentIndex { get; set; } = -1;
}

public sealed class GraphWireSegment
{
    public int SegmentIndex { get; set; }
    public GraphPoint From { get; set; }
    public GraphPoint To { get; set; }
    public string FromColorHex { get; set; } = "#7c8794";
    public string ToColorHex { get; set; } = "#7c8794";
    public string FromDirection { get; set; } = WireRerouteDirection.Right;
    public string ToDirection { get; set; } = WireRerouteDirection.Left;
}

public sealed class RuleGraphGeometryService
{
    public const float NodeWidth = 300.0F;
    public const float NodeHeight = 132.0F;
    public const float PinRadius = 7.0F;
    private const float PortTop = 56.0F;
    private const float PortSpacing = 26.0F;
    private const float NodeBottomPadding = 24.0F;

    public GraphPoint ScreenToGraph(GraphPoint screen, GraphPoint origin, GraphPoint pan, float zoom)
    {
        var safeZoom = Math.Max(0.1F, zoom);
        return new GraphPoint(
            (screen.X - origin.X - pan.X) / safeZoom,
            (screen.Y - origin.Y - pan.Y) / safeZoom);
    }

    public GraphPoint GraphToScreen(GraphPoint graph, GraphPoint origin, GraphPoint pan, float zoom)
    {
        return new GraphPoint(
            origin.X + pan.X + (graph.X * zoom),
            origin.Y + pan.Y + (graph.Y * zoom));
    }

    public GraphRect NodeRect(RuleNode node)
    {
        var x = node.GraphPositionSet ? node.GraphX : 0;
        var y = node.GraphPositionSet ? node.GraphY : 0;
        return new GraphRect(x, y, NodeWidth, NodeHeightFor(node));
    }

    public static float NodeHeightFor(RuleNode node)
    {
        var inputCount = node.Ports.Count(port => port.Direction == NodePortDirection.Input);
        var outputCount = node.Ports.Count(port => port.Direction == NodePortDirection.Output);
        var maxPortCount = Math.Max(inputCount, outputCount);
        if (maxPortCount <= 1)
        {
            return NodeHeight;
        }

        return Math.Max(NodeHeight, PortTop + ((maxPortCount - 1) * PortSpacing) + NodeBottomPadding);
    }

    public IReadOnlyList<GraphPortLayout> InputPins(RuleNode node)
    {
        return PortLayouts(node, NodePortDirection.Input);
    }

    public IReadOnlyList<GraphPortLayout> OutputPins(RuleNode node)
    {
        return PortLayouts(node, NodePortDirection.Output);
    }

    public GraphPortLayout? FindPortLayout(RuleNode node, string portId)
    {
        return InputPins(node)
            .Concat(OutputPins(node))
            .FirstOrDefault(port => string.Equals(port.PortId, portId, StringComparison.OrdinalIgnoreCase));
    }

    public GraphPinHit? HitTestPin(IEnumerable<RuleNode> nodes, GraphPoint graphPoint)
    {
        foreach (var node in nodes.Reverse())
        {
            foreach (var layout in InputPins(node).Concat(OutputPins(node)))
            {
                if (IsNearPoint(graphPoint, layout.Point, PinRadius + 3.0F))
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

    public RuleNode? HitTestNode(IEnumerable<RuleNode> nodes, GraphPoint graphPoint)
    {
        return nodes.Reverse().FirstOrDefault(node => NodeRect(node).Contains(graphPoint));
    }

    public GraphConnectionHit? HitTestConnection(IReadOnlyList<GraphConnection> connections, IReadOnlyDictionary<string, RuleNode> nodesById, GraphPoint graphPoint)
    {
        return HitTestConnection(connections, nodesById, new Dictionary<string, RuleWireReroute>(StringComparer.OrdinalIgnoreCase), graphPoint);
    }

    public GraphConnectionHit? HitTestConnection(
        IReadOnlyList<GraphConnection> connections,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        IReadOnlyDictionary<string, RuleWireReroute> reroutesById,
        GraphPoint graphPoint)
    {
        for (var index = connections.Count - 1; index >= 0; index--)
        {
            var connection = connections[index];
            if (!TryGetConnectionPathSegments(connection, nodesById, reroutesById, out var segments))
            {
                continue;
            }

            foreach (var segment in segments)
            {
                var bezier = BuildGraphLinkBezier(segment.From, segment.To, segment.FromDirection, segment.ToDirection);
                if (IsNearBezier(graphPoint, bezier, 8.0F))
                {
                    return new GraphConnectionHit { ConnectionIndex = index, SegmentIndex = segment.SegmentIndex };
                }
            }
        }

        return null;
    }

    public bool TryGetConnectionPoints(
        GraphConnection connection,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        out GraphPortLayout from,
        out GraphPortLayout to)
    {
        from = new GraphPortLayout();
        to = new GraphPortLayout();
        if (!nodesById.TryGetValue(connection.From.NodeId, out var fromNode) ||
            !nodesById.TryGetValue(connection.To.NodeId, out var toNode))
        {
            return false;
        }

        var fromLayout = FindPortLayout(fromNode, connection.From.PortId);
        var toLayout = FindPortLayout(toNode, connection.To.PortId);
        if (fromLayout is null || toLayout is null)
        {
            return false;
        }

        from = fromLayout;
        to = toLayout;
        return true;
    }

    public bool TryGetConnectionPathSegments(
        GraphConnection connection,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        IReadOnlyDictionary<string, RuleWireReroute> reroutesById,
        out IReadOnlyList<GraphWireSegment> segments)
    {
        segments = [];
        if (!TryGetConnectionPoints(connection, nodesById, out var from, out var to))
        {
            return false;
        }

        var anchors = new List<(GraphPoint Point, string ColorHex, string InputDirection, string OutputDirection)>
        {
            (from.Point, from.ColorHex, WireRerouteDirection.Left, WireRerouteDirection.Right)
        };

        foreach (var rerouteId in connection.RerouteIds)
        {
            if (!reroutesById.TryGetValue(rerouteId, out var reroute))
            {
                continue;
            }

            anchors.Add((
                new GraphPoint(reroute.GraphX, reroute.GraphY),
                "#9fd3ff",
                WireRerouteDirection.Normalize(reroute.InputDirection, WireRerouteDirection.Left),
                WireRerouteDirection.Normalize(reroute.OutputDirection, WireRerouteDirection.Right)));
        }

        anchors.Add((to.Point, to.ColorHex, WireRerouteDirection.Left, WireRerouteDirection.Right));

        var result = new List<GraphWireSegment>();
        for (var index = 0; index < anchors.Count - 1; index++)
        {
            var start = anchors[index];
            var end = anchors[index + 1];
            result.Add(new GraphWireSegment
            {
                SegmentIndex = index,
                From = start.Point,
                To = end.Point,
                FromColorHex = start.ColorHex,
                ToColorHex = end.ColorHex,
                FromDirection = index == 0 ? WireRerouteDirection.Right : start.OutputDirection,
                ToDirection = index == anchors.Count - 2 ? WireRerouteDirection.Left : end.InputDirection
            });
        }

        segments = result;
        return result.Count > 0;
    }

    public GraphBezier BuildGraphLinkBezier(GraphPoint from, GraphPoint to)
    {
        return BuildGraphLinkBezier(from, to, WireRerouteDirection.Right, WireRerouteDirection.Left);
    }

    public GraphBezier BuildGraphLinkBezier(GraphPoint from, GraphPoint to, string fromDirection, string toDirection)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        var curve = MathF.Max(48.0F, distance * 0.35F);
        var fromVector = DirectionVector(fromDirection, positiveFallback: new GraphPoint(1.0F, 0.0F));
        var toVector = DirectionVector(toDirection, positiveFallback: new GraphPoint(-1.0F, 0.0F));
        return new GraphBezier(
            from,
            new GraphPoint(from.X + (fromVector.X * curve), from.Y + (fromVector.Y * curve)),
            new GraphPoint(to.X + (toVector.X * curve), to.Y + (toVector.Y * curve)),
            to);
    }

    public GraphPoint CubicBezierPoint(GraphBezier bezier, float t)
    {
        var u = 1.0F - t;
        var tt = t * t;
        var uu = u * u;
        var uuu = uu * u;
        var ttt = tt * t;

        return new GraphPoint(
            (uuu * bezier.P0.X) + (3.0F * uu * t * bezier.P1.X) + (3.0F * u * tt * bezier.P2.X) + (ttt * bezier.P3.X),
            (uuu * bezier.P0.Y) + (3.0F * uu * t * bezier.P1.Y) + (3.0F * u * tt * bezier.P2.Y) + (ttt * bezier.P3.Y));
    }

    public bool IsNearPoint(GraphPoint point, GraphPoint center, float radius)
    {
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        return (dx * dx) + (dy * dy) <= radius * radius;
    }

    private IReadOnlyList<GraphPortLayout> PortLayouts(RuleNode node, NodePortDirection direction)
    {
        var rect = NodeRect(node);
        var ports = node.Ports
            .Where(port => port.Direction == direction)
            .OrderBy(port => port.Order)
            .ThenBy(port => port.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<GraphPortLayout>();
        for (var index = 0; index < ports.Count; index++)
        {
            var port = ports[index];
            result.Add(new GraphPortLayout
            {
                NodeId = node.Id,
                PortId = port.Id,
                Label = port.Label,
                Direction = direction,
                PortKind = port.PortKind,
                DataType = port.DataType,
                ColorHex = port.ColorHex,
                Point = new GraphPoint(
                    direction == NodePortDirection.Input ? rect.X : rect.Right,
                    PortY(rect, index, ports.Count))
            });
        }

        return result;
    }

    private static float PortY(GraphRect rect, int index, int count)
    {
        if (count <= 1)
        {
            return rect.Y + (rect.Height * 0.5F);
        }

        return rect.Y + PortTop + (index * PortSpacing);
    }

    private bool IsNearBezier(GraphPoint point, GraphBezier bezier, float maxDistance)
    {
        var previous = bezier.P0;
        for (var step = 1; step <= 24; step++)
        {
            var t = step / 24.0F;
            var current = CubicBezierPoint(bezier, t);
            if (DistanceToSegment(point, previous, current) <= maxDistance)
            {
                return true;
            }

            previous = current;
        }

        return false;
    }

    private static float DistanceToSegment(GraphPoint point, GraphPoint start, GraphPoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared <= 0.001F)
        {
            var px = point.X - start.X;
            var py = point.Y - start.Y;
            return MathF.Sqrt((px * px) + (py * py));
        }

        var t = Math.Clamp(((point.X - start.X) * dx + ((point.Y - start.Y) * dy)) / lengthSquared, 0.0F, 1.0F);
        var projection = new GraphPoint(start.X + (t * dx), start.Y + (t * dy));
        var deltaX = point.X - projection.X;
        var deltaY = point.Y - projection.Y;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static GraphPoint DirectionVector(string direction, GraphPoint positiveFallback)
    {
        var normalized = WireRerouteDirection.Normalize(direction, "");
        return normalized switch
        {
            WireRerouteDirection.Left => new GraphPoint(-1.0F, 0.0F),
            WireRerouteDirection.Right => new GraphPoint(1.0F, 0.0F),
            WireRerouteDirection.Up => new GraphPoint(0.0F, -1.0F),
            WireRerouteDirection.Down => new GraphPoint(0.0F, 1.0F),
            _ => positiveFallback
        };
    }
}
