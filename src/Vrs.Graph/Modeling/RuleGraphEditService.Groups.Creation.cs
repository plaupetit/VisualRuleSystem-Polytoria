using Vrs.Graph.Model;

namespace Vrs.Graph.Modeling;

public sealed partial class RuleGraphEditService
{
    // Group mutations are visual-only authoring metadata. They deliberately do
    // not update connections, execution order, or exported Luau behavior.
    public GraphEditResult CreateGroupFromSelection(
        Rule rule,
        IEnumerable<string> nodeIds,
        string? name = null,
        string color = "Teal")
    {
        return CreateGroupFromSelection(rule, nodeIds, [], name, color);
    }

    public GraphEditResult CreateGroupFromSelection(
        Rule rule,
        IEnumerable<string> nodeIds,
        IEnumerable<string> rerouteIds,
        string? name = null,
        string color = "Teal")
    {
        var selectedIds = nodeIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var selectedRerouteIds = rerouteIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (selectedIds.Count == 0 && selectedRerouteIds.Count == 0)
        {
            return GraphEditResult.Fail("Select at least one node or wire reroute before creating a group.");
        }

        var selectedNodes = selectedIds
            .Select(id => FindNode(rule, id))
            .ToList();
        if (selectedNodes.Any(node => node is null))
        {
            return GraphEditResult.Fail("Group selection contains a missing node.");
        }

        var selectedReroutes = selectedRerouteIds
            .Select(id => FindWireReroute(rule, id))
            .ToList();
        if (selectedReroutes.Any(reroute => reroute is null))
        {
            return GraphEditResult.Fail("Group selection contains a missing wire reroute.");
        }

        var nodes = selectedNodes.OfType<RuleNode>().ToList();
        var reroutes = selectedReroutes.OfType<RuleWireReroute>().ToList();
        RemoveNodesFromGroups(rule, selectedIds);
        RemoveReroutesFromGroups(rule, selectedRerouteIds);

        var bounds = BoundsForItems(nodes, reroutes);
        var group = new RuleNodeGroup
        {
            Id = CreateGroupId(rule),
            Name = string.IsNullOrWhiteSpace(name) ? DefaultGroupName(rule.NodeGroups.Count + 1) : name!,
            Color = string.IsNullOrWhiteSpace(color) ? "Teal" : color,
            MemberNodeIds = selectedIds,
            MemberRerouteIds = selectedRerouteIds,
            GraphX = bounds.X,
            GraphY = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height
        };

        rule.NodeGroups.Add(group);
        var parentId = FindDeepestContainingGroup(rule, group.Id, GroupCenter(group));
        group.ParentGroupId = parentId;
        AutoFitAllGroups(rule);

        return GraphEditResult.Ok($"Created node group: {group.Name}");
    }

    public GraphEditResult CreateEmptyGroup(
        Rule rule,
        float graphX,
        float graphY,
        string? name = null,
        string color = "Teal")
    {
        var group = new RuleNodeGroup
        {
            Id = CreateGroupId(rule),
            Name = string.IsNullOrWhiteSpace(name) ? DefaultGroupName(rule.NodeGroups.Count + 1) : name!,
            Color = string.IsNullOrWhiteSpace(color) ? "Teal" : color,
            GraphX = graphX,
            GraphY = graphY,
            Width = 360.0F,
            Height = 220.0F
        };

        rule.NodeGroups.Add(group);
        group.ParentGroupId = FindDeepestContainingGroup(rule, group.Id, GroupCenter(group));
        AutoFitAllGroups(rule);
        return GraphEditResult.Ok($"Created empty node group: {group.Name}");
    }
}
