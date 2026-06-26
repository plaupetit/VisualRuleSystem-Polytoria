using Vrs.Core.Authoring;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using Vrs.Graph.Theming;

namespace Vrs.App.Services;

/// <summary>
/// Builds canvas-only node presentation data so hover help and status badges
/// stay testable outside Avalonia drawing code.
/// </summary>
public sealed class NodeCanvasPresentationService
{
    public IReadOnlyList<NodeCanvasStatusBadge> BuildStatusBadges(RuleNode node)
    {
        var badges = new List<NodeCanvasStatusBadge>();
        if (node.DebugEnabled)
        {
            badges.Add(new NodeCanvasStatusBadge("DBG", "#102d42", "#4fb8ff", "#bfe9ff"));
        }

        if (node.Breakpoint)
        {
            badges.Add(new NodeCanvasStatusBadge("BRK", "#45181c", "#ff6370", "#ffd5da"));
        }

        if (!string.IsNullOrWhiteSpace(node.UserComment))
        {
            badges.Add(new NodeCanvasStatusBadge("NOTE", "#10261e", "#5ee6a8", "#dafbe8"));
        }

        return badges;
    }

    public NodeCanvasTooltip BuildTooltip(RuleNode node, NodeCatalogEntry? catalogEntry)
    {
        var lines = new List<string>
        {
            $"Type: {node.Kind} / {FirstNonEmpty(node.Type, node.CatalogId, node.Id)}"
        };

        var description = FirstNonEmpty(node.Description, catalogEntry?.Description);
        if (!string.IsNullOrWhiteSpace(description))
        {
            lines.Add($"What it does: {description}");
        }

        var configured = NodeReadableSummaryService.BuildNodeSummary(node, catalogEntry);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            lines.Add($"Configured as: {configured}");
        }

        if (!string.IsNullOrWhiteSpace(node.UserComment))
        {
            lines.Add($"Author note: {node.UserComment.Trim()}");
        }

        var statuses = BuildStatusText(node);
        if (!string.IsNullOrWhiteSpace(statuses))
        {
            lines.Add($"Status: {statuses}");
        }

        var style = GraphTheme.Default.StyleFor(node.Kind);
        return new NodeCanvasTooltip(
            NodeReadableSummaryService.BuildNodeDisplayName(node),
            string.IsNullOrWhiteSpace(style.AccentHex) ? "#aeb8c4" : style.AccentHex,
            lines);
    }

    private static string BuildStatusText(RuleNode node)
    {
        var statuses = new List<string>();
        if (!node.Enabled)
        {
            statuses.Add("Disabled");
        }

        if (node.DebugEnabled)
        {
            statuses.Add("Debug logs enabled");
        }

        if (node.Breakpoint)
        {
            statuses.Add("Breakpoint");
        }

        return string.Join(", ", statuses);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";
    }
}

public sealed record NodeCanvasStatusBadge(string Label, string FillHex, string BorderHex, string TextHex);

public sealed record NodeCanvasTooltip(string Title, string AccentHex, IReadOnlyList<string> Lines);
