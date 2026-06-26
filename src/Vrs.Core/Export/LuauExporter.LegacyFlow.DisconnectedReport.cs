using System.Text;
using Vrs.Core.Authoring;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Legacy disconnected reports explain graph content that survives export
    // but is not reached by enabled flow wires.
    private static void AppendDisconnectedNodeReport(
        StringBuilder builder,
        Rule rule,
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        LuauExportOptions options,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        IReadOnlySet<string> reachedNodeIds,
        IReadOnlyDictionary<string, GraphFragment>? nodeToFragment,
        int indentLevel,
        IEnumerable<string>? candidateNodeIds = null)
    {
        if (!options.IncludeComments)
        {
            return;
        }

        var candidateSet = candidateNodeIds is null
            ? null
            : new HashSet<string>(candidateNodeIds, StringComparer.OrdinalIgnoreCase);
        var disconnectedNodes = rule.Nodes
            .Where(node => node.Enabled && node.Kind is NodeKind.Action or NodeKind.Condition)
            .Where(node => !reachedNodeIds.Contains(node.Id))
            .Where(node => candidateSet is null || candidateSet.Contains(node.Id))
            .Where(node => candidateSet is not null || nodeToFragment is null || !nodeToFragment.ContainsKey(node.Id))
            .OrderBy(node => DisconnectedNodeOrder(node.Kind))
            .ThenBy(node => node.GraphX)
            .ThenBy(node => node.GraphY)
            .ThenBy(node => node.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (disconnectedNodes.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, "DISCONNECTED NODES START"));
        builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, "These nodes exist in the graph but are not reached by any enabled flow wire."));
        foreach (var node in disconnectedNodes)
        {
            var entry = NodeCatalogService.FindByCatalogId(catalog, node.CatalogId);
            var configuredSummary = NodeReadableSummaryService.BuildNodeSummary(node, entry);
            builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, $"{SectionName(node.Kind)} DISCONNECTED: {node.Label}"));
            if (!string.IsNullOrWhiteSpace(configuredSummary))
            {
                builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, $"Configured as: {configuredSummary}"));
            }

            if (!string.IsNullOrWhiteSpace(node.UserComment))
            {
                builder.AppendLine(LuauCommentTags.IndentedUserComment(indentLevel, node.UserComment));
            }

            builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, "Not executed: connect a Trigger, Action, or Condition output to this node input to run it."));
        }

        builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, "DISCONNECTED NODES END"));
    }
}
