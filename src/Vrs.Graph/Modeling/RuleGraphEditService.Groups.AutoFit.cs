using Vrs.Graph.Model;

namespace Vrs.Graph.Modeling;

public sealed partial class RuleGraphEditService
{
    private void ReassignNodeToContainingGroup(Rule rule, RuleNode node)
    {
        RemoveNodesFromGroups(rule, [node.Id]);
        AssignNodeToContainingGroup(rule, node);
    }

    private bool AutoFitAllGroups(Rule rule)
    {
        var anyChanged = false;
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var group in rule.NodeGroups.OrderByDescending(group => GroupDepth(rule, group.Id)))
            {
                var groupChanged = AutoFitGroup(rule, group);
                changed |= groupChanged;
                anyChanged |= groupChanged;
            }
        }

        return anyChanged;
    }

    private bool AutoFitGroup(Rule rule, RuleNodeGroup group)
    {
        var nodesById = rule.Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        var reroutesById = rule.WireReroutes.ToDictionary(reroute => reroute.Id, StringComparer.OrdinalIgnoreCase);
        var nodes = group.MemberNodeIds
            .Select(id => nodesById.GetValueOrDefault(id))
            .Where(node => node is not null)
            .Select(node => node!)
            .ToList();
        var reroutes = group.MemberRerouteIds
            .Select(id => reroutesById.GetValueOrDefault(id))
            .Where(reroute => reroute is not null)
            .Select(reroute => reroute!)
            .ToList();
        var childGroups = rule.NodeGroups
            .Where(child => string.Equals(child.ParentGroupId, group.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (nodes.Count == 0 && reroutes.Count == 0 && childGroups.Count == 0)
        {
            return false;
        }

        var bounds = BoundsForItems(nodes, reroutes, childGroups);
        var changed = Math.Abs(group.GraphX - bounds.X) > 0.01F ||
            Math.Abs(group.GraphY - bounds.Y) > 0.01F ||
            Math.Abs(group.Width - bounds.Width) > 0.01F ||
            Math.Abs(group.Height - bounds.Height) > 0.01F;
        if (!changed)
        {
            return false;
        }

        group.GraphX = bounds.X;
        group.GraphY = bounds.Y;
        group.Width = bounds.Width;
        group.Height = bounds.Height;
        return true;
    }

    private void AssignNodeToContainingGroup(Rule rule, RuleNode node)
    {
        var parentId = FindDeepestContainingGroup(rule, "", NodeCenter(node));
        if (string.IsNullOrWhiteSpace(parentId))
        {
            return;
        }

        var parent = FindGroup(rule, parentId);
        if (parent is not null && !parent.MemberNodeIds.Contains(node.Id, StringComparer.OrdinalIgnoreCase))
        {
            parent.MemberNodeIds.Add(node.Id);
        }
    }

    private string FindDeepestContainingGroup(Rule rule, string movingGroupId, GraphPoint point)
    {
        return rule.NodeGroups
            .Where(group => !string.Equals(group.Id, movingGroupId, StringComparison.OrdinalIgnoreCase))
            .Where(group => GroupContainsPoint(group, point))
            .Where(group => !IsDescendantGroup(rule, group.Id, movingGroupId))
            .OrderByDescending(group => GroupDepth(rule, group.Id))
            .ThenBy(group => group.Width * group.Height)
            .Select(group => group.Id)
            .FirstOrDefault() ?? "";
    }

    private static int GroupDepth(Rule rule, string groupId)
    {
        var depth = 0;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = rule.NodeGroups.FirstOrDefault(group => string.Equals(group.Id, groupId, StringComparison.OrdinalIgnoreCase));
        while (current is not null && !string.IsNullOrWhiteSpace(current.ParentGroupId) && visited.Add(current.Id))
        {
            depth++;
            current = rule.NodeGroups.FirstOrDefault(group => string.Equals(group.Id, current.ParentGroupId, StringComparison.OrdinalIgnoreCase));
        }

        return depth;
    }

    private static bool IsDescendantGroup(Rule rule, string possibleDescendantId, string ancestorId)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = rule.NodeGroups.FirstOrDefault(group => string.Equals(group.Id, possibleDescendantId, StringComparison.OrdinalIgnoreCase));
        while (current is not null && !string.IsNullOrWhiteSpace(current.ParentGroupId) && visited.Add(current.Id))
        {
            if (string.Equals(current.ParentGroupId, ancestorId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = rule.NodeGroups.FirstOrDefault(group => string.Equals(group.Id, current.ParentGroupId, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }
}
