using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    private void CaptureGroupDragStarts(string groupId)
    {
        draggedGroupStarts.Clear();
        draggedNodeStarts.Clear();
        draggedWireRerouteStarts.Clear();
        var groups = GroupList();
        var draggedGroupIds = DescendantGroupIds(groups, groupId);
        var draggedGroupRects = groups
            .Where(group => draggedGroupIds.Contains(group.Id))
            .Select(GroupRect)
            .ToList();
        var directNodeGroups = DirectNodeGroupIds(groups);
        var directRerouteGroups = DirectRerouteGroupIds(groups);

        foreach (var group in groups.Where(group => draggedGroupIds.Contains(group.Id)))
        {
            draggedGroupStarts[group.Id] = new GraphPoint(group.GraphX, group.GraphY);
        }

        var draggedNodeIds = groups
            .Where(group => draggedGroupIds.Contains(group.Id))
            .SelectMany(group => group.MemberNodeIds)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var node in NodeList().Where(node => draggedNodeIds.Contains(node.Id) || ShouldCaptureNodeWithGroupDrag(node, draggedGroupIds, draggedGroupRects, directNodeGroups)))
        {
            draggedNodeStarts[node.Id] = new GraphPoint(node.GraphX, node.GraphY);
        }

        var draggedRerouteIds = groups
            .Where(group => draggedGroupIds.Contains(group.Id))
            .SelectMany(group => group.MemberRerouteIds)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var reroute in WireRerouteList().Where(reroute => draggedRerouteIds.Contains(reroute.Id) || ShouldCaptureRerouteWithGroupDrag(reroute, draggedGroupIds, draggedGroupRects, directRerouteGroups)))
        {
            draggedWireRerouteStarts[reroute.Id] = new GraphPoint(reroute.GraphX, reroute.GraphY);
        }
    }

    private static HashSet<string> DescendantGroupIds(IReadOnlyList<RuleNodeGroup> groups, string rootGroupId)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootGroupId };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var group in groups)
            {
                if (string.IsNullOrWhiteSpace(group.ParentGroupId) ||
                    !result.Contains(group.ParentGroupId) ||
                    !result.Add(group.Id))
                {
                    continue;
                }

                changed = true;
            }
        }

        return result;
    }

    private IReadOnlyList<GraphGroupMove> BuildDraggedGroupMoves()
    {
        var groupsById = GroupList().ToDictionary(group => group.Id, StringComparer.OrdinalIgnoreCase);
        return draggedGroupStarts
            .Where(item => groupsById.ContainsKey(item.Key))
            .Select(item =>
            {
                var group = groupsById[item.Key];
                return new GraphGroupMove(group.Id, group.GraphX, group.GraphY);
            })
            .ToList();
    }

    private IReadOnlyList<GraphNodeMove> BuildDraggedNodeMoves()
    {
        return draggedNodeStarts
            .Select(item => NodeList().FirstOrDefault(node => string.Equals(node.Id, item.Key, StringComparison.OrdinalIgnoreCase)))
            .Where(node => node is not null)
            .Select(node => new GraphNodeMove(node!.Id, node.GraphX, node.GraphY))
            .ToList();
    }

    private IReadOnlyList<GraphWireRerouteMove> BuildDraggedWireRerouteMoves()
    {
        return draggedWireRerouteStarts
            .Select(item => WireRerouteList().FirstOrDefault(reroute => string.Equals(reroute.Id, item.Key, StringComparison.OrdinalIgnoreCase)))
            .Where(reroute => reroute is not null)
            .Select(reroute => new GraphWireRerouteMove(reroute!.Id, reroute.GraphX, reroute.GraphY))
            .ToList();
    }

    private IReadOnlyList<string> NodesInsideSelection(GraphPoint currentGraphPoint)
    {
        var minX = MathF.Min(selectionStartGraph.X, currentGraphPoint.X);
        var minY = MathF.Min(selectionStartGraph.Y, currentGraphPoint.Y);
        var maxX = MathF.Max(selectionStartGraph.X, currentGraphPoint.X);
        var maxY = MathF.Max(selectionStartGraph.Y, currentGraphPoint.Y);
        var selection = new GraphRect(minX, minY, maxX - minX, maxY - minY);

        return VisibleNodeList(NodeList(), FragmentList())
            .Where(node => Intersects(selection, geometry.NodeRect(node)))
            .Select(node => node.Id)
            .ToList();
    }

    private static bool Intersects(GraphRect a, GraphRect b)
    {
        return a.X <= b.Right && a.Right >= b.X && a.Y <= b.Bottom && a.Bottom >= b.Y;
    }

    private static IReadOnlyDictionary<string, string> DirectNodeGroupIds(IReadOnlyList<RuleNodeGroup> groups)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            foreach (var nodeId in group.MemberNodeIds.Where(nodeId => !result.ContainsKey(nodeId)))
            {
                result[nodeId] = group.Id;
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string> DirectRerouteGroupIds(IReadOnlyList<RuleNodeGroup> groups)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            foreach (var rerouteId in group.MemberRerouteIds.Where(rerouteId => !result.ContainsKey(rerouteId)))
            {
                result[rerouteId] = group.Id;
            }
        }

        return result;
    }

    private static bool ShouldCaptureNodeWithGroupDrag(
        RuleNode node,
        IReadOnlySet<string> draggedGroupIds,
        IReadOnlyList<GraphRect> draggedGroupRects,
        IReadOnlyDictionary<string, string> directNodeGroups)
    {
        if (directNodeGroups.TryGetValue(node.Id, out var directGroupId))
        {
            return draggedGroupIds.Contains(directGroupId);
        }

        return draggedGroupRects.Any(rect => rect.Contains(NodeCenter(node)));
    }

    private static bool ShouldCaptureRerouteWithGroupDrag(
        RuleWireReroute reroute,
        IReadOnlySet<string> draggedGroupIds,
        IReadOnlyList<GraphRect> draggedGroupRects,
        IReadOnlyDictionary<string, string> directRerouteGroups)
    {
        if (directRerouteGroups.TryGetValue(reroute.Id, out var directGroupId))
        {
            return draggedGroupIds.Contains(directGroupId);
        }

        return draggedGroupRects.Any(rect => rect.Contains(new GraphPoint(reroute.GraphX, reroute.GraphY)));
    }

    private GraphPoint ConstrainNodePositionForDrag(RuleNode node, float graphX, float graphY)
    {
        return new GraphPoint(graphX, graphY);
    }

    private GraphPoint ConstrainReroutePositionForDrag(RuleWireReroute reroute, float graphX, float graphY)
    {
        return new GraphPoint(graphX, graphY);
    }

    private static GraphPoint NodeCenter(RuleNode node)
    {
        return new GraphPoint(
            node.GraphX + (RuleGraphGeometryService.NodeWidth * 0.5F),
            node.GraphY + (RuleGraphGeometryService.NodeHeightFor(node) * 0.5F));
    }

}
