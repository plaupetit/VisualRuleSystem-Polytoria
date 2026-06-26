using System.Text;
using Vrs.Core.Authoring;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Shared comments, ordering, fragment naming, and section labels for legacy flow output.
    private static void AppendNodeBlockStart(
        StringBuilder builder,
        RuleNode node,
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        int indentLevel,
        LuauExportOptions options)
    {
        if (!options.IncludeComments)
        {
            return;
        }

        var entry = NodeCatalogService.FindByCatalogId(catalog, node.CatalogId);
        var configuredSummary = NodeReadableSummaryService.BuildNodeSummary(node, entry);
        builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, $"{SectionName(node.Kind)} START: {node.Label}"));
        if (!string.IsNullOrWhiteSpace(configuredSummary))
        {
            builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, $"Configured as: {configuredSummary}"));
        }

        if (!string.IsNullOrWhiteSpace(node.UserComment))
        {
            builder.AppendLine(LuauCommentTags.IndentedUserComment(indentLevel, node.UserComment));
        }
    }

    private static void AppendNodeBlockEnd(StringBuilder builder, RuleNode node, int indentLevel, LuauExportOptions options)
    {
        if (!options.IncludeComments)
        {
            return;
        }

        builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, $"{SectionName(node.Kind)} END: {node.Label}"));
    }
    private static int FlowPortOrder(string portId)
    {
        return portId switch
        {
            GraphPortDefaults.TrueOut => 0,
            GraphPortDefaults.FlowOut => 1,
            GraphPortDefaults.FalseOut => 2,
            _ => 10
        };
    }

    private static int DisconnectedNodeOrder(NodeKind kind)
    {
        return kind switch
        {
            NodeKind.Action => 0,
            NodeKind.Condition => 1,
            _ => 10
        };
    }

    private static Dictionary<string, GraphFragment> NodeToFragmentMap(Rule rule)
    {
        var result = new Dictionary<string, GraphFragment>(StringComparer.OrdinalIgnoreCase);
        foreach (var fragment in rule.Fragments)
        {
            foreach (var nodeId in fragment.NodeIds)
            {
                result[nodeId] = fragment;
            }
        }

        return result;
    }

    private static string FragmentFunctionName(GraphFragment fragment)
    {
        return $"vrsFragment_{SafeIdentifier(fragment.Id)}";
    }

    private static string SectionName(NodeKind kind)
    {
        return kind switch
        {
            NodeKind.Trigger => "TRIGGER",
            NodeKind.Condition => "CONDITION",
            NodeKind.Action => "ACTION",
            NodeKind.Property => "VALUE",
            NodeKind.Reference => "REFERENCE",
            _ => kind.ToString().ToUpperInvariant()
        };
    }
}
