using System.Collections.Specialized;
using Avalonia;
using Avalonia.Media;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    // Shared geometry, collection, and color helpers used by the canvas slices.
    private Point ToScreen(GraphPoint point)
    {
        var screen = geometry.GraphToScreen(point, new GraphPoint(0, 0), new GraphPoint((float)PanX, (float)PanY), (float)Zoom);
        return new Point(screen.X, screen.Y);
    }

    private GraphPoint ToGraph(Point point)
    {
        return geometry.ScreenToGraph(new GraphPoint((float)point.X, (float)point.Y), new GraphPoint(0, 0), new GraphPoint((float)PanX, (float)PanY), (float)Zoom);
    }

    private Rect ToScreenRect(GraphRect rect)
    {
        var topLeft = ToScreen(new GraphPoint(rect.X, rect.Y));
        return new Rect(topLeft.X, topLeft.Y, rect.Width * Zoom, rect.Height * Zoom);
    }

    private static Rect RectFromPoints(Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        return new Rect(x, y, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }

    private static GraphRect FragmentRect(GraphFragment fragment)
    {
        return new GraphRect(fragment.GraphX, fragment.GraphY, 270.0F, 118.0F);
    }

    private static GraphRect GroupRect(RuleNodeGroup group)
    {
        return new GraphRect(group.GraphX, group.GraphY, group.Width, group.Height);
    }

    private static GraphRect GroupHeaderRect(RuleNodeGroup group)
    {
        return new GraphRect(group.GraphX, group.GraphY, group.Width, 32.0F);
    }

    private static GraphRect GroupResizeHandleRect(RuleNodeGroup group)
    {
        return new GraphRect(group.GraphX + group.Width - 18.0F, group.GraphY + group.Height - 18.0F, 18.0F, 18.0F);
    }

    private static GraphRect WireRerouteRect(RuleWireReroute reroute)
    {
        return new GraphRect(reroute.GraphX - 7.0F, reroute.GraphY - 7.0F, 14.0F, 14.0F);
    }

    private IReadOnlyList<RuleNode> NodeList()
    {
        return Nodes?.OfType<RuleNode>().ToList() ?? [];
    }

    private IReadOnlyList<RuleNode> VisibleNodeList(IReadOnlyList<RuleNode> nodes, IReadOnlyList<GraphFragment> fragments)
    {
        if (ViewMode != GraphViewMode.Simple)
        {
            return nodes;
        }

        var collapsedFragmentIds = fragments
            .Where(fragment => fragment.Collapsed)
            .Select(fragment => fragment.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return nodes
            .Where(node => string.IsNullOrWhiteSpace(node.FragmentId) || !collapsedFragmentIds.Contains(node.FragmentId))
            .ToList();
    }

    private IReadOnlyList<GraphConnection> ConnectionList()
    {
        return Connections?.OfType<GraphConnection>().ToList() ?? [];
    }

    private IReadOnlyList<GraphFragment> FragmentList()
    {
        return Fragments?.OfType<GraphFragment>().ToList() ?? [];
    }

    private IReadOnlyList<RuleNodeGroup> GroupList()
    {
        return NodeGroups?.OfType<RuleNodeGroup>().ToList() ?? [];
    }

    private IReadOnlyList<RuleWireReroute> WireRerouteList()
    {
        return WireReroutes?.OfType<RuleWireReroute>().ToList() ?? [];
    }

    private IReadOnlySet<string> VisibleWireRerouteIds(IReadOnlyList<GraphConnection> connections, IReadOnlySet<string> visibleNodeIds)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var connection in connections.Where(connection => ConnectionVisible(connection, visibleNodeIds)))
        {
            foreach (var rerouteId in connection.RerouteIds)
            {
                result.Add(rerouteId);
            }
        }

        return result;
    }

    private HashSet<string> SelectedNodeIdSet()
    {
        return SelectedNodeIds?.OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
    }

    private IReadOnlyList<RuleNodeGroup> GroupsInDrawOrder(IReadOnlyList<RuleNodeGroup> groups)
    {
        return groups
            .OrderBy(group => GroupDepth(groups, group.Id))
            .ThenBy(group => group.GraphX)
            .ThenBy(group => group.GraphY)
            .ToList();
    }

    private static int GroupDepth(IReadOnlyList<RuleNodeGroup> groups, string groupId)
    {
        var depth = 0;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = groups.FirstOrDefault(group => string.Equals(group.Id, groupId, StringComparison.OrdinalIgnoreCase));
        while (current is not null && !string.IsNullOrWhiteSpace(current.ParentGroupId) && visited.Add(current.Id))
        {
            depth++;
            current = groups.FirstOrDefault(group => string.Equals(group.Id, current.ParentGroupId, StringComparison.OrdinalIgnoreCase));
        }

        return depth;
    }

    private bool ConnectionVisible(GraphConnection connection, IReadOnlySet<string> visibleNodeIds)
    {
        return ViewMode == GraphViewMode.Advanced ||
            (visibleNodeIds.Contains(connection.From.NodeId) && visibleNodeIds.Contains(connection.To.NodeId));
    }

    private void BeginPan(Point point)
    {
        panning = true;
        panStartPointer = point;
        panStartX = PanX;
        panStartY = PanY;
    }

    private void ReplaceCollectionObserver(ref INotifyCollectionChanged? current, INotifyCollectionChanged? next)
    {
        if (current is not null)
        {
            current.CollectionChanged -= CollectionChanged;
        }

        current = next;
        if (current is not null)
        {
            current.CollectionChanged += CollectionChanged;
        }
    }

    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }

    private static double PositiveModulo(double value, double modulo)
    {
        var result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private static IBrush BrushFromHex(string hex)
    {
        return new SolidColorBrush(Color.Parse(hex));
    }

}
