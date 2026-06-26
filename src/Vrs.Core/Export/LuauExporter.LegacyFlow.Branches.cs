using System.Text;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Legacy condition branches keep TRUE/FALSE branch comments and branch-local
    // traversal separate from the generic flow walker.
    private static void AppendConditionBranch(
        StringBuilder builder,
        Rule rule,
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        LuauExportOptions options,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        string conditionNodeId,
        string portId,
        string branchName,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        int indentLevel,
        int depth)
    {
        var conditionLabel = nodesById.TryGetValue(conditionNodeId, out var condition)
            ? condition.Label
            : conditionNodeId;

        if (options.IncludeComments)
        {
            builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, $"{branchName} BRANCH START: {conditionLabel}"));
        }

        var branchConnections = rule.Connections
            .Where(connection =>
                connection.ConnectionKind == GraphConnectionKind.Flow &&
                string.Equals(connection.From.NodeId, conditionNodeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(connection.From.PortId, portId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(connection => connection.To.NodeId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (branchConnections.Count == 0)
        {
            if (options.IncludeComments)
            {
                builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, $"No {branchName} branch actions configured."));
            }
        }
        else
        {
            foreach (var connection in branchConnections)
            {
                AppendFlowConnection(builder, rule, catalog, options, nodesById, connection, visited, reachedNodeIds, indentLevel, depth);
            }
        }

        if (options.IncludeComments)
        {
            builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, $"{branchName} BRANCH END: {conditionLabel}"));
        }
    }
}
