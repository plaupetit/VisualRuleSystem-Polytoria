using Vrs.Graph.Model;

namespace Vrs.Core.Validation;

public sealed partial class RuleGraphValidator
{
    // Fragment validation protects grouping metadata without changing node or
    // connection validation order in the main rule pass.
    private static void ValidateFragments(Rule rule, IReadOnlySet<string> nodeIds, ValidationResult result)
    {
        var duplicateIds = rule.Fragments
            .Where(fragment => !string.IsNullOrWhiteSpace(fragment.Id))
            .GroupBy(fragment => fragment.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);
        foreach (var duplicate in duplicateIds)
        {
            Add(result, ValidationSeverity.Error, rule.Name, $"Duplicate fragment id: {duplicate.Key}");
        }

        var fragmentIds = rule.Fragments.Select(fragment => fragment.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var node in rule.Nodes.Where(node => !string.IsNullOrWhiteSpace(node.FragmentId) && !fragmentIds.Contains(node.FragmentId)))
        {
            Add(result, ValidationSeverity.Error, node.Label, $"Node references a missing fragment: {node.FragmentId}");
        }

        if (rule.Fragments.Count > 0)
        {
            foreach (var orphan in rule.Nodes.Where(node => string.IsNullOrWhiteSpace(node.FragmentId)))
            {
                Add(result, ValidationSeverity.Warning, orphan.Label, "Node is not assigned to a State/Rule fragment.");
            }
        }

        var connectionIds = rule.Connections.Select(connection => connection.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var fragment in rule.Fragments)
        {
            if (fragment.NodeIds.Count == 0)
            {
                Add(result, ValidationSeverity.Warning, fragment.Name, "Fragment has no nodes yet.");
            }

            foreach (var missingNodeId in fragment.NodeIds.Where(id => !nodeIds.Contains(id)))
            {
                Add(result, ValidationSeverity.Error, fragment.Name, $"Fragment references a missing node: {missingNodeId}");
            }

            foreach (var missingConnectionId in fragment.ConnectionIds.Where(id => !connectionIds.Contains(id)))
            {
                Add(result, ValidationSeverity.Error, fragment.Name, $"Fragment references a missing connection: {missingConnectionId}");
            }

            if (fragment.Kind == GraphFragmentKind.State && !fragment.NodeIds.Any(id =>
                rule.Nodes.Any(node => string.Equals(node.Id, id, StringComparison.OrdinalIgnoreCase) &&
                    node.Kind == NodeKind.Trigger &&
                    node.Enabled)))
            {
                Add(result, ValidationSeverity.Warning, fragment.Name, "State fragment has no enabled Trigger node.");
            }

            if (fragment.Kind == GraphFragmentKind.Macro && fragment.NodeIds.Contains(fragment.Id, StringComparer.OrdinalIgnoreCase))
            {
                Add(result, ValidationSeverity.Error, fragment.Name, "Macro fragment cannot reference itself.");
            }
        }
    }
}
