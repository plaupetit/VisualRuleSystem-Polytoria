using Vrs.Graph.Model;

namespace Vrs.Graph.Modeling;

public sealed partial class RuleGraphEditService
{
    public GraphEditResult AddItemsToGroup(
        Rule rule,
        string groupId,
        IEnumerable<string> nodeIds,
        IEnumerable<string> rerouteIds)
    {
        var group = FindGroup(rule, groupId);
        if (group is null)
        {
            return GraphEditResult.Fail($"Group does not exist: {groupId}");
        }

        var changed = false;
        var selectedNodeIds = nodeIds
            .Where(id => FindNode(rule, id) is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var selectedRerouteIds = rerouteIds
            .Where(id => FindWireReroute(rule, id) is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        RemoveNodesFromGroups(rule, selectedNodeIds);
        RemoveReroutesFromGroups(rule, selectedRerouteIds);
        foreach (var nodeId in selectedNodeIds)
        {
            if (!group.MemberNodeIds.Contains(nodeId, StringComparer.OrdinalIgnoreCase))
            {
                group.MemberNodeIds.Add(nodeId);
                changed = true;
            }
        }

        foreach (var rerouteId in selectedRerouteIds)
        {
            if (!group.MemberRerouteIds.Contains(rerouteId, StringComparer.OrdinalIgnoreCase))
            {
                group.MemberRerouteIds.Add(rerouteId);
                changed = true;
            }
        }

        if (changed)
        {
            AutoFitAllGroups(rule);
        }

        return GraphEditResult.Ok(changed ? $"Added selected items to group: {group.Name}" : $"Group already contains selected items: {group.Name}", changed);
    }

    public GraphEditResult AddUngroupedItemsToGroup(
        Rule rule,
        string groupId,
        IEnumerable<string> nodeIds,
        IEnumerable<string> rerouteIds)
    {
        var group = FindGroup(rule, groupId);
        if (group is null)
        {
            return GraphEditResult.Fail($"Group does not exist: {groupId}");
        }

        var changed = false;
        foreach (var nodeId in nodeIds
            .Where(id => FindNode(rule, id) is not null && FindDirectNodeGroup(rule, id) is null)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            group.MemberNodeIds.Add(nodeId);
            changed = true;
        }

        foreach (var rerouteId in rerouteIds
            .Where(id => FindWireReroute(rule, id) is not null && FindDirectRerouteGroup(rule, id) is null)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            group.MemberRerouteIds.Add(rerouteId);
            changed = true;
        }

        if (changed)
        {
            AutoFitAllGroups(rule);
        }

        return GraphEditResult.Ok(changed ? $"Added ungrouped items to group: {group.Name}" : $"No ungrouped items to add to group: {group.Name}", changed);
    }
}
