using System.Text;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Readable comments and disconnected-node report helpers.
    private static void AppendReadableNodeSummary(StringBuilder builder, RuleNode node, IReadOnlyCollection<NodeCatalogEntry> catalog)
    {
        if (!string.IsNullOrWhiteSpace(node.UserComment))
        {
            builder.AppendLine(LuauCommentTags.UserComment(node.UserComment));
        }
    }

    private static void AppendReadableDisconnectedReport(
        StringBuilder builder,
        Rule rule,
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        IReadOnlySet<string> reachedNodeIds)
    {
    }

}
