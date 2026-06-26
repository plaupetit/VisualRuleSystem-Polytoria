using Vrs.Graph.Model;

namespace Vrs.Graph.Modeling;

public sealed partial class RuleGraphEditService
{
    private const float GroupPadding = 28.0F;
    private const float GroupHeaderHeight = 34.0F;
    private const float MinimumGroupWidth = 180.0F;
    private const float MinimumGroupHeight = 120.0F;

    // Group bounds include visual chrome so auto-fit preserves readable header
    // space while still describing only authoring metadata.
    private static GraphRect BoundsForItems(IReadOnlyCollection<RuleNode> nodes, IReadOnlyCollection<RuleWireReroute> reroutes)
    {
        return BoundsForItems(nodes, reroutes, []);
    }

    private static GraphRect BoundsForItems(
        IReadOnlyCollection<RuleNode> nodes,
        IReadOnlyCollection<RuleWireReroute> reroutes,
        IReadOnlyCollection<RuleNodeGroup> childGroups)
    {
        var rects = nodes
            .Select(node => new GraphRect(node.GraphX, node.GraphY, RuleGraphGeometryService.NodeWidth, RuleGraphGeometryService.NodeHeightFor(node)))
            .Concat(reroutes.Select(reroute => new GraphRect(reroute.GraphX - 16.0F, reroute.GraphY - 16.0F, 32.0F, 32.0F)))
            .Concat(childGroups.Select(group => new GraphRect(group.GraphX, group.GraphY, group.Width, group.Height)))
            .ToList();
        var minX = rects.Min(rect => rect.X);
        var minY = rects.Min(rect => rect.Y);
        var maxX = rects.Max(rect => rect.Right);
        var maxY = rects.Max(rect => rect.Bottom);
        return new GraphRect(
            minX - GroupPadding,
            minY - GroupHeaderHeight - GroupPadding,
            MathF.Max(MinimumGroupWidth, (maxX - minX) + (GroupPadding * 2.0F)),
            MathF.Max(MinimumGroupHeight, (maxY - minY) + GroupHeaderHeight + (GroupPadding * 2.0F)));
    }

    private static GraphPoint GroupCenter(RuleNodeGroup group)
    {
        return new GraphPoint(group.GraphX + (group.Width * 0.5F), group.GraphY + (group.Height * 0.5F));
    }

    private static bool GroupContainsPoint(RuleNodeGroup group, GraphPoint point)
    {
        return new GraphRect(group.GraphX, group.GraphY, group.Width, group.Height).Contains(point);
    }

    private static GraphPoint NodeCenter(RuleNode node)
    {
        return new GraphPoint(
            node.GraphX + (RuleGraphGeometryService.NodeWidth * 0.5F),
            node.GraphY + (RuleGraphGeometryService.NodeHeightFor(node) * 0.5F));
    }
}
