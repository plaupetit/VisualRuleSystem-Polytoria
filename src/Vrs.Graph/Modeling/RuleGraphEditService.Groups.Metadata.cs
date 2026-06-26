using Vrs.Graph.Model;

namespace Vrs.Graph.Modeling;

public sealed partial class RuleGraphEditService
{
    public GraphEditResult RenameGroup(Rule rule, string groupId, string name)
    {
        var group = FindGroup(rule, groupId);
        if (group is null)
        {
            return GraphEditResult.Fail($"Group does not exist: {groupId}");
        }

        var normalized = string.IsNullOrWhiteSpace(name) ? "Group" : name.Trim();
        if (string.Equals(group.Name, normalized, StringComparison.Ordinal))
        {
            return GraphEditResult.Ok($"Group name unchanged: {group.Name}", changed: false);
        }

        group.Name = normalized;
        return GraphEditResult.Ok($"Renamed group: {group.Name}");
    }

    public GraphEditResult SetGroupColor(Rule rule, string groupId, string color)
    {
        var group = FindGroup(rule, groupId);
        if (group is null)
        {
            return GraphEditResult.Fail($"Group does not exist: {groupId}");
        }

        var normalized = string.IsNullOrWhiteSpace(color) ? "Teal" : color.Trim();
        if (string.Equals(group.Color, normalized, StringComparison.Ordinal))
        {
            return GraphEditResult.Ok($"Group color unchanged: {group.Name}", changed: false);
        }

        group.Color = normalized;
        return GraphEditResult.Ok($"Updated group color: {group.Name}");
    }

    public GraphEditResult SetGroupParent(Rule rule, string groupId, string parentGroupId)
    {
        var group = FindGroup(rule, groupId);
        if (group is null)
        {
            return GraphEditResult.Fail($"Group does not exist: {groupId}");
        }

        var normalizedParentId = parentGroupId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedParentId))
        {
            if (string.IsNullOrWhiteSpace(group.ParentGroupId))
            {
                return GraphEditResult.Ok($"Group parent unchanged: {group.Name}", changed: false);
            }

            group.ParentGroupId = "";
            AutoFitAllGroups(rule);
            return GraphEditResult.Ok($"Removed group parent: {group.Name}");
        }

        if (string.Equals(group.Id, normalizedParentId, StringComparison.OrdinalIgnoreCase))
        {
            return GraphEditResult.Fail("A group cannot be its own parent.");
        }

        if (FindGroup(rule, normalizedParentId) is null)
        {
            return GraphEditResult.Fail($"Parent group does not exist: {normalizedParentId}");
        }

        if (IsDescendantGroup(rule, normalizedParentId, group.Id))
        {
            return GraphEditResult.Fail("A group cannot be moved inside one of its descendants.");
        }

        if (string.Equals(group.ParentGroupId, normalizedParentId, StringComparison.OrdinalIgnoreCase))
        {
            return GraphEditResult.Ok($"Group parent unchanged: {group.Name}", changed: false);
        }

        group.ParentGroupId = normalizedParentId;
        AutoFitAllGroups(rule);
        return GraphEditResult.Ok($"Updated group parent: {group.Name}");
    }

    public GraphEditResult AutoParentGroupByBounds(Rule rule, string groupId)
    {
        var group = FindGroup(rule, groupId);
        if (group is null)
        {
            return GraphEditResult.Fail($"Group does not exist: {groupId}");
        }

        var parentId = FindDeepestContainingGroup(rule, group.Id, GroupCenter(group));
        var result = SetGroupParent(rule, group.Id, parentId);
        if (!result.Success || result.Changed)
        {
            return result;
        }

        var fitChanged = AutoFitAllGroups(rule);
        return GraphEditResult.Ok(
            fitChanged ? $"Updated group bounds: {group.Name}" : result.Message,
            fitChanged);
    }

    public GraphEditResult RemoveGroup(Rule rule, string groupId)
    {
        var group = FindGroup(rule, groupId);
        if (group is null)
        {
            return GraphEditResult.Fail($"Group does not exist: {groupId}");
        }

        rule.NodeGroups.Remove(group);
        foreach (var child in rule.NodeGroups.Where(item => string.Equals(item.ParentGroupId, group.Id, StringComparison.OrdinalIgnoreCase)))
        {
            child.ParentGroupId = group.ParentGroupId;
        }

        AutoFitAllGroups(rule);
        return GraphEditResult.Ok($"Removed group: {group.Name}");
    }

    public GraphEditResult NormalizeNodeGroups(Rule rule)
    {
        var changed = false;
        var nodeIds = rule.Nodes.Select(node => node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rerouteIds = rule.WireReroutes.Select(reroute => reroute.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var groupIds = rule.NodeGroups.Select(group => group.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var claimedNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var claimedReroutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in rule.NodeGroups)
        {
            changed |= group.MemberNodeIds.RemoveAll(id => !nodeIds.Contains(id)) > 0;
            changed |= group.MemberRerouteIds.RemoveAll(id => !rerouteIds.Contains(id)) > 0;
            for (var index = group.MemberNodeIds.Count - 1; index >= 0; index--)
            {
                if (claimedNodes.Add(group.MemberNodeIds[index]))
                {
                    continue;
                }

                group.MemberNodeIds.RemoveAt(index);
                changed = true;
            }

            for (var index = group.MemberRerouteIds.Count - 1; index >= 0; index--)
            {
                if (claimedReroutes.Add(group.MemberRerouteIds[index]))
                {
                    continue;
                }

                group.MemberRerouteIds.RemoveAt(index);
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(group.ParentGroupId) && !groupIds.Contains(group.ParentGroupId))
            {
                group.ParentGroupId = "";
                changed = true;
            }
        }

        return GraphEditResult.Ok(changed ? "Normalized node groups." : "Node groups are already normalized.", changed);
    }
}
