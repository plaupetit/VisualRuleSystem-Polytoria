using Vrs.Graph.Model;

namespace Vrs.Core.Validation;

public sealed partial class RuleGraphValidator
{
    // Group validation protects visual authoring metadata without making node
    // organization affect exportability or runtime behavior.
    private static void ValidateGroups(Rule rule, IReadOnlySet<string> nodeIds, ValidationResult result)
    {
        var duplicateGroupIds = rule.NodeGroups
            .Where(group => !string.IsNullOrWhiteSpace(group.Id))
            .GroupBy(group => group.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);
        foreach (var duplicate in duplicateGroupIds)
        {
            Add(result, ValidationSeverity.Error, rule.Name, $"Duplicate node group id: {duplicate.Key}");
        }

        var groupIds = rule.NodeGroups
            .Select(group => group.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rerouteIds = rule.WireReroutes
            .Select(reroute => reroute.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var group in rule.NodeGroups)
        {
            if (!string.IsNullOrWhiteSpace(group.ParentGroupId) && !groupIds.Contains(group.ParentGroupId))
            {
                Add(result, ValidationSeverity.Error, group.Name, $"Group references a missing parent group: {group.ParentGroupId}");
            }

            if (string.Equals(group.Id, group.ParentGroupId, StringComparison.OrdinalIgnoreCase))
            {
                Add(result, ValidationSeverity.Error, group.Name, "Group cannot be its own parent.");
            }

            foreach (var missingNodeId in group.MemberNodeIds.Where(id => !nodeIds.Contains(id)))
            {
                Add(result, ValidationSeverity.Error, group.Name, $"Group references a missing node: {missingNodeId}");
            }

            foreach (var missingRerouteId in group.MemberRerouteIds.Where(id => !rerouteIds.Contains(id)))
            {
                Add(result, ValidationSeverity.Error, group.Name, $"Group references a missing wire reroute: {missingRerouteId}");
            }
        }

        foreach (var duplicateNode in rule.NodeGroups
            .SelectMany(group => group.MemberNodeIds.Select(nodeId => (group, nodeId)))
            .Where(item => !string.IsNullOrWhiteSpace(item.nodeId))
            .GroupBy(item => item.nodeId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            Add(result, ValidationSeverity.Error, rule.Name, $"Node belongs to more than one direct group: {duplicateNode.Key}");
        }

        foreach (var duplicateReroute in rule.NodeGroups
            .SelectMany(group => group.MemberRerouteIds.Select(rerouteId => (group, rerouteId)))
            .Where(item => !string.IsNullOrWhiteSpace(item.rerouteId))
            .GroupBy(item => item.rerouteId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            Add(result, ValidationSeverity.Error, rule.Name, $"Wire reroute belongs to more than one direct group: {duplicateReroute.Key}");
        }

        foreach (var group in rule.NodeGroups)
        {
            if (HasGroupParentCycle(rule, group.Id))
            {
                Add(result, ValidationSeverity.Error, group.Name, $"Group parent chain contains a cycle: {group.Id}");
            }
        }
    }

    private static bool HasGroupParentCycle(Rule rule, string groupId)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = rule.NodeGroups.FirstOrDefault(group => string.Equals(group.Id, groupId, StringComparison.OrdinalIgnoreCase));
        while (current is not null && !string.IsNullOrWhiteSpace(current.ParentGroupId))
        {
            if (!visited.Add(current.Id))
            {
                return true;
            }

            current = rule.NodeGroups.FirstOrDefault(group => string.Equals(group.Id, current.ParentGroupId, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }
}
