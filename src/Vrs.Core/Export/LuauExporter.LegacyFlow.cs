using System.Text;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Legacy structured graph and fragment entrypoints kept separate from the readable exporter.
    private static void AppendFlowGraph(StringBuilder builder, Rule rule, IReadOnlyCollection<NodeCatalogEntry> catalog, LuauExportOptions options)
    {
        // Legacy structured export helpers are kept isolated below the readable
        // exporter so older tests and fallback paths do not shape new node APIs.
        var nodesById = rule.Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        var nodeToFragment = NodeToFragmentMap(rule);
        var triggers = rule.Nodes
            .Where(node => node.Enabled && node.Kind == NodeKind.Trigger)
            .Where(node => !nodeToFragment.ContainsKey(node.Id))
            .OrderBy(node => node.GraphX)
            .ThenBy(node => node.GraphY)
            .ToList();
        var fragmentsWithTriggers = rule.Fragments
            .Where(fragment => fragment.NodeIds.Any(nodeId =>
                nodesById.TryGetValue(nodeId, out var node) &&
                node.Enabled &&
                node.Kind == NodeKind.Trigger))
            .OrderBy(fragment => fragment.Kind)
            .ThenBy(fragment => fragment.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (options.IncludeStructureComments)
        {
            builder.AppendLine(LuauCommentTags.IndentedVsrComment(1, "FLOW GRAPH"));
        }

        if (triggers.Count == 0 && fragmentsWithTriggers.Count == 0)
        {
            builder.AppendLine("    vrsLog(\"No enabled Trigger node is configured.\")");
            return;
        }

        foreach (var fragment in fragmentsWithTriggers)
        {
            builder.AppendLine(LuauCommentTags.IndentedVsrComment(1, $"{fragment.Kind.ToString().ToUpperInvariant()}: {EscapeForDoubleQuotedString(fragment.Name)}"));
            builder.AppendLine($"    {FragmentFunctionName(fragment)}(context)");
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reachedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var trigger in triggers)
        {
            AppendTriggerBlock(builder, rule, trigger, catalog, options, nodesById, visited, reachedNodeIds, 1, 0);
        }

        AppendDisconnectedNodeReport(builder, rule, catalog, options, nodesById, reachedNodeIds, nodeToFragment, 1);
    }

    private static void AppendFragmentFunctions(StringBuilder builder, Rule rule, IReadOnlyCollection<NodeCatalogEntry> catalog, LuauExportOptions options)
    {
        if (rule.Fragments.Count == 0)
        {
            return;
        }

        var nodesById = rule.Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var fragment in rule.Fragments.OrderBy(fragment => fragment.Kind).ThenBy(fragment => fragment.Name, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine(LuauCommentTags.VsrComment($"{fragment.Kind.ToString().ToUpperInvariant()}: {fragment.Name}"));
            if (!string.IsNullOrWhiteSpace(fragment.Comment))
            {
                builder.AppendLine(LuauCommentTags.UserComment(fragment.Comment));
            }

            builder.AppendLine($"local function {FragmentFunctionName(fragment)}(context)");
            var fragmentNodes = fragment.NodeIds
                .Where(nodesById.ContainsKey)
                .Select(nodeId => nodesById[nodeId])
                .ToList();
            var triggers = fragmentNodes
                .Where(node => node.Enabled && node.Kind == NodeKind.Trigger)
                .OrderBy(node => node.GraphX)
                .ThenBy(node => node.GraphY)
                .ToList();

            if (triggers.Count == 0)
            {
                builder.AppendLine($"    vrsLog(\"Fragment {EscapeForDoubleQuotedString(fragment.Name)} has no enabled Trigger.\")");
            }
            else
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var reachedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var trigger in triggers)
                {
                    AppendTriggerBlock(builder, rule, trigger, catalog, options, nodesById, visited, reachedNodeIds, 1, 0);
                }

                AppendDisconnectedNodeReport(builder, rule, catalog, options, nodesById, reachedNodeIds, null, 1, fragment.NodeIds);
            }

            builder.AppendLine("end");
            builder.AppendLine();
        }
    }
}
