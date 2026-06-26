using Avalonia.Media;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;
using Vrs.Graph.Theming;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    private void DrawConnection(
        DrawingContext context,
        GraphConnection connection,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        IReadOnlyDictionary<string, RuleWireReroute> reroutesById,
        bool selected)
    {
        if (!geometry.TryGetConnectionPathSegments(connection, nodesById, reroutesById, out var segments))
        {
            return;
        }

        foreach (var segment in segments)
        {
            DrawBezierGradient(context, segment, selected ? 3.5 : 2.2, selected);
        }
    }

    private void DrawPendingConnection(DrawingContext context, IReadOnlyList<RuleNode> nodes, IReadOnlyDictionary<string, RuleNode> nodesById)
    {
        if (pendingOutputPin is null || !nodesById.TryGetValue(pendingOutputPin.NodeId, out var fromNode))
        {
            return;
        }

        var fromLayout = geometry.FindPortLayout(fromNode, pendingOutputPin.PortId);
        if (fromLayout is null)
        {
            return;
        }

        var hoverPin = HitTestVisiblePin(nodes, pendingGraphPoint);
        var endColor = hoverPin is not null && hoverPin.IsInput ? hoverPin.ColorHex : "#798896";
        DrawBezierGradient(context, fromLayout.Point, pendingGraphPoint, fromLayout.ColorHex, endColor, 2.2, selected: false);
    }

    private void DrawBezierGradient(DrawingContext context, GraphPoint from, GraphPoint to, string startHex, string endHex, double thickness, bool selected)
    {
        DrawBezierGradient(
            context,
            new GraphWireSegment
            {
                From = from,
                To = to,
                FromColorHex = startHex,
                ToColorHex = endHex,
                FromDirection = WireRerouteDirection.Right,
                ToDirection = WireRerouteDirection.Left
            },
            thickness,
            selected);
    }

    private void DrawBezierGradient(DrawingContext context, GraphWireSegment segment, double thickness, bool selected)
    {
        var bezier = geometry.BuildGraphLinkBezier(segment.From, segment.To, segment.FromDirection, segment.ToDirection);
        var previous = ToScreen(bezier.P0);
        for (var step = 1; step <= 36; step++)
        {
            var t = step / 36.0F;
            var currentGraph = geometry.CubicBezierPoint(bezier, t);
            var current = ToScreen(currentGraph);
            var color = GraphColor.Interpolate(GraphColor.FromHex(segment.FromColorHex), GraphColor.FromHex(segment.ToColorHex), t);
            if (selected)
            {
                color = color.Lighten(0.22);
            }

            context.DrawLine(new Pen(BrushFromHex(color.ToHex()), thickness), previous, current);
            previous = current;
        }
    }

    private void DrawWireReroutes(DrawingContext context, IReadOnlyList<RuleWireReroute> reroutes, IReadOnlySet<string> visibleRerouteIds)
    {
        foreach (var reroute in reroutes)
        {
            if (visibleRerouteIds.Count > 0 && !visibleRerouteIds.Contains(reroute.Id))
            {
                continue;
            }

            var selected = string.Equals(SelectedWireRerouteId, reroute.Id, StringComparison.OrdinalIgnoreCase);
            var rect = ToScreenRect(WireRerouteRect(reroute));
            var fill = selected ? BrushFromHex("#d8f3ff") : BrushFromHex("#101923");
            var border = selected ? new Pen(BrushFromHex("#7dd3fc"), 2.2) : new Pen(BrushFromHex("#9fd3ff"), 1.6);
            context.DrawRectangle(fill, border, rect, 3.0, 3.0);

            if (Zoom >= 0.65)
            {
                var center = ToScreen(new GraphPoint(reroute.GraphX, reroute.GraphY));
                context.DrawEllipse(BrushFromHex("#38bdf8"), null, center, Math.Max(2.0, 2.4 * Zoom), Math.Max(2.0, 2.4 * Zoom));
            }
        }
    }
}
