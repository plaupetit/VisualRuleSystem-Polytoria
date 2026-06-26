using System.Text;
using System.Text.Json;
using Vrs.Core.Persistence;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    public static bool TryExtractGraphMetadata(string luau, out RuleGraph? graph)
    {
        graph = null;
        if (string.IsNullOrWhiteSpace(luau))
        {
            return false;
        }

        var metadata = new StringBuilder();
        var insideMetadata = false;
        foreach (var rawLine in luau.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var commentText = TryReadVsrCommentText(rawLine);
            if (commentText is null)
            {
                continue;
            }

            var trimmed = commentText.Trim();
            if (!insideMetadata)
            {
                if (trimmed.Equals("VRS_GRAPH_BEGIN base64-json", StringComparison.Ordinal))
                {
                    metadata.Clear();
                    insideMetadata = true;
                }

                continue;
            }

            if (trimmed.Equals("VRS_GRAPH_END", StringComparison.Ordinal))
            {
                try
                {
                    var json = Encoding.UTF8.GetString(Convert.FromBase64String(metadata.ToString()));
                    graph = RuleGraphJson.Deserialize(json);
                    return true;
                }
                catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
                {
                    graph = null;
                    return false;
                }
            }

            metadata.Append(trimmed);
        }

        return false;
    }

    private static void AppendGraphMetadata(StringBuilder builder, RuleGraph graph)
    {
        // Metadata is intentionally appended after readable script logic so the
        // .luau file remains inspectable while still supporting graph round-trip.
        var metadata = Convert.ToBase64String(Encoding.UTF8.GetBytes(RuleGraphJson.Serialize(CreateScriptMetadataGraph(graph))));
        builder.AppendLine(LuauCommentTags.VsrComment("VRS_GRAPH_BEGIN base64-json"));
        builder.AppendLine(LuauCommentTags.VsrComment(metadata));
        builder.AppendLine(LuauCommentTags.VsrComment("VRS_GRAPH_END"));
        builder.AppendLine();
    }

    private static RuleGraph CreateScriptMetadataGraph(RuleGraph graph)
    {
        // Deployed scripts only need the editable graph. Live Creator snapshots
        // can be large and are refreshed from the current project when a script
        // is loaded back into the editor.
        var metadataGraph = RuleGraphJson.Deserialize(RuleGraphJson.Serialize(graph));
        metadataGraph.SceneObjects.Clear();
        return metadataGraph;
    }

    private static string? TryReadVsrCommentText(string line)
    {
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith("--", StringComparison.Ordinal))
        {
            return null;
        }

        var body = trimmed[2..].TrimStart();
        if (!body.StartsWith(LuauCommentTags.Vsr, StringComparison.Ordinal))
        {
            return null;
        }

        return body[LuauCommentTags.Vsr.Length..].TrimStart();
    }
}
