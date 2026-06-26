using System.Text;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Legacy traversal walks enabled flow wires only; node-specific code emission
    // remains in the block, branch, and emitter partials.
    private static bool AppendFlowFromNode(
        StringBuilder builder,
        Rule rule,
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        LuauExportOptions options,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        string fromNodeId,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        int indentLevel,
        int depth)
    {
        if (depth > 128)
        {
            builder.AppendLine($"{IndentText(indentLevel)}vrsLog(\"Flow traversal stopped after 128 steps to avoid an infinite loop.\")");
            return true;
        }

        var outgoing = rule.Connections
            .Where(connection =>
                connection.ConnectionKind == GraphConnectionKind.Flow &&
                string.Equals(connection.From.NodeId, fromNodeId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(connection => FlowPortOrder(connection.From.PortId))
            .ThenBy(connection => connection.To.NodeId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var emittedAnyNode = false;
        foreach (var connection in outgoing)
        {
            if (!nodesById.TryGetValue(connection.To.NodeId, out var node) || !node.Enabled)
            {
                continue;
            }

            if (!visited.Add($"{connection.From.NodeId}:{connection.From.PortId}->{connection.To.NodeId}:{connection.To.PortId}"))
            {
                builder.AppendLine($"{IndentText(indentLevel)}vrsLog(\"Skipped repeated flow into {EscapeForDoubleQuotedString(node.Label)}.\")");
                emittedAnyNode = true;
                continue;
            }

            reachedNodeIds.Add(node.Id);
            emittedAnyNode = true;
            switch (node.Kind)
            {
                case NodeKind.Action:
                    AppendActionBlock(builder, rule, node, catalog, nodesById, options, indentLevel);
                    AppendFlowFromNode(builder, rule, catalog, options, nodesById, node.Id, visited, reachedNodeIds, indentLevel, depth + 1);
                    break;
                case NodeKind.Condition:
                    AppendConditionBlock(builder, rule, node, catalog, options, nodesById, visited, reachedNodeIds, indentLevel, depth + 1);
                    break;
                case NodeKind.Trigger:
                    builder.AppendLine($"{IndentText(indentLevel)}vrsLog(\"Skipped nested Trigger {EscapeForDoubleQuotedString(node.Label)}.\")");
                    break;
            }
        }

        return emittedAnyNode;
    }

    private static void AppendFlowConnection(
        StringBuilder builder,
        Rule rule,
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        LuauExportOptions options,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        GraphConnection connection,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        int indentLevel,
        int depth)
    {
        if (!nodesById.TryGetValue(connection.To.NodeId, out var node) || !node.Enabled)
        {
            return;
        }

        if (!visited.Add($"{connection.From.NodeId}:{connection.From.PortId}->{connection.To.NodeId}:{connection.To.PortId}"))
        {
            builder.AppendLine($"{IndentText(indentLevel)}vrsLog(\"Skipped repeated flow into {EscapeForDoubleQuotedString(node.Label)}.\")");
            return;
        }

        reachedNodeIds.Add(node.Id);
        switch (node.Kind)
        {
            case NodeKind.Action:
                AppendActionBlock(builder, rule, node, catalog, nodesById, options, indentLevel);
                AppendFlowFromNode(builder, rule, catalog, options, nodesById, node.Id, visited, reachedNodeIds, indentLevel, depth + 1);
                break;
            case NodeKind.Condition:
                AppendConditionBlock(builder, rule, node, catalog, options, nodesById, visited, reachedNodeIds, indentLevel, depth + 1);
                break;
            case NodeKind.Trigger:
                builder.AppendLine($"{IndentText(indentLevel)}vrsLog(\"Skipped nested Trigger {EscapeForDoubleQuotedString(node.Label)}.\")");
                break;
        }
    }
}
