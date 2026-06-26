using Vrs.Graph.Model;

namespace Vrs.Graph.Modeling;

public sealed partial class RuleGraphEditService
{
    private RuleNodeGroup? FindGroup(Rule rule, string groupId)
    {
        return rule.NodeGroups.FirstOrDefault(group => string.Equals(group.Id, groupId, StringComparison.OrdinalIgnoreCase));
    }

    private RuleNodeGroup? FindDirectNodeGroup(Rule rule, string nodeId)
    {
        return rule.NodeGroups.FirstOrDefault(group => group.MemberNodeIds.Contains(nodeId, StringComparer.OrdinalIgnoreCase));
    }

    private RuleNodeGroup? FindDirectRerouteGroup(Rule rule, string rerouteId)
    {
        return rule.NodeGroups.FirstOrDefault(group => group.MemberRerouteIds.Contains(rerouteId, StringComparer.OrdinalIgnoreCase));
    }

    private static void RemoveNodesFromGroups(Rule rule, IReadOnlyCollection<string> nodeIds)
    {
        foreach (var group in rule.NodeGroups)
        {
            group.MemberNodeIds.RemoveAll(id => nodeIds.Contains(id, StringComparer.OrdinalIgnoreCase));
        }
    }

    private static void RemoveReroutesFromGroups(Rule rule, IReadOnlyCollection<string> rerouteIds)
    {
        foreach (var group in rule.NodeGroups)
        {
            group.MemberRerouteIds.RemoveAll(id => rerouteIds.Contains(id, StringComparer.OrdinalIgnoreCase));
        }
    }

    private static string CreateGroupId(Rule rule)
    {
        for (var index = 1; index < 10_000; index++)
        {
            var candidate = $"GROUP_{index}";
            if (rule.NodeGroups.All(group => !string.Equals(group.Id, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }

        return $"GROUP_{Guid.NewGuid():N}";
    }

    private static string DefaultGroupName(int index)
    {
        return $"Group {index}";
    }
}
