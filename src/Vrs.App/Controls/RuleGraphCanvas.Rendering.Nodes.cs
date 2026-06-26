using Avalonia;
using Avalonia.Media;
using Vrs.App.Services;
using Vrs.Core.Authoring;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    private void DrawNode(DrawingContext context, RuleNode node, bool selected)
    {
        var rect = ToScreenRect(geometry.NodeRect(node));
        var style = GraphThemeDefinition.StyleFor(node.Kind);
        var header = HeaderPalette(node.Kind);
        var accent = BrushFromHex(header.HeaderHex);
        var fill = BrushFromHex(selected ? style.SelectedFillHex : style.FillHex);
        var border = selected ? new Pen(accent, 2.5) : new Pen(new SolidColorBrush(Color.Parse("#415166")), 1.2);

        context.DrawRectangle(fill, border, rect, 7, 7);
        var headerHeight = 38.0 * Zoom;
        context.DrawRectangle(BrushFromHex(header.HeaderHex), null, new Rect(rect.X, rect.Y, rect.Width, headerHeight), 7, 7);

        var indicatorWidth = NodeStatusIndicatorWidth(node);
        var textLeft = rect.X + (12.0 * Zoom);
        var textWidth = Math.Max(32.0, rect.Width - (24.0 * Zoom) - indicatorWidth);
        using (context.PushClip(rect))
        {
            var title = NodeDisplayName(node);
            DrawText(context, title, textLeft + (1.0 * Zoom), rect.Y + (9.0 * Zoom), 16.0 * Zoom, new SolidColorBrush(Color.Parse("#06111d")), maxWidth: textWidth);
            DrawText(context, title, textLeft, rect.Y + (8.0 * Zoom), 16.0 * Zoom, Brushes.White, maxWidth: textWidth);
            DrawNodeStatusIndicators(context, node, rect);

            DrawText(context, NodePreviewText(node), textLeft, rect.Y + (61.0 * Zoom), 12.0 * Zoom, new SolidColorBrush(Color.Parse("#d6e2ef")), maxWidth: textWidth, maxLineCount: 2);
        }

        foreach (var input in VisibleInputPins(node))
        {
            DrawPin(context, input, selected);
            DrawPortLabel(context, input, input.Label, leftSide: true);
        }

        foreach (var output in VisibleOutputPins(node))
        {
            DrawPin(context, output, selected);
            DrawPortLabel(context, output, output.Label, leftSide: false);
        }
    }

    private static string NodeDisplayName(RuleNode node)
    {
        return NodeReadableSummaryService.BuildNodeDisplayName(node);
    }

    private string NodePreviewText(RuleNode node)
    {
        return NodeReadableSummaryService.BuildNodeSummary(node, CatalogEntryFor(node));
    }

    private NodeCatalogEntry? CatalogEntryFor(RuleNode node)
    {
        return CatalogEntries?
            .OfType<NodeCatalogEntry>()
            .FirstOrDefault(entry =>
                string.Equals(entry.IdBase, node.CatalogId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.Type, node.Type, StringComparison.OrdinalIgnoreCase));
    }

    private double NodeStatusIndicatorWidth(RuleNode node)
    {
        var badges = NodeCanvasPresentation.BuildStatusBadges(node);
        if (Zoom < 0.65 || badges.Count == 0)
        {
            return 0;
        }

        return (badges.Sum(StatusBadgeWidth) + ((badges.Count - 1) * 4.0) + 8.0) * Zoom;
    }

    private void DrawNodeStatusIndicators(DrawingContext context, RuleNode node, Rect rect)
    {
        var badges = NodeCanvasPresentation.BuildStatusBadges(node);
        if (Zoom < 0.65 || badges.Count == 0)
        {
            return;
        }

        var x = rect.Right - (8.0 * Zoom);
        foreach (var badge in badges.AsEnumerable().Reverse())
        {
            var width = StatusBadgeWidth(badge) * Zoom;
            var badgeRect = new Rect(x - width, rect.Y + (9.0 * Zoom), width, 18.0 * Zoom);
            context.DrawRectangle(BrushFromHex(badge.FillHex), new Pen(BrushFromHex(badge.BorderHex), 1.0), badgeRect, 3, 3);
            DrawText(
                context,
                badge.Label,
                badgeRect.X + (6.0 * Zoom),
                badgeRect.Y + (3.0 * Zoom),
                9.0 * Zoom,
                BrushFromHex(badge.TextHex),
                FontWeight.SemiBold,
                badgeRect.Width - (8.0 * Zoom));
            x = badgeRect.X - (4.0 * Zoom);
        }
    }

    private static double StatusBadgeWidth(NodeCanvasStatusBadge badge)
    {
        return badge.Label.Length >= 4 ? 42.0 : 34.0;
    }

    private void DrawPin(DrawingContext context, GraphPortLayout layout, bool selected)
    {
        var center = ToScreen(layout.Point);
        var radius = RuleGraphGeometryService.PinRadius * Zoom;
        IBrush fill = BrushFromHex(layout.ColorHex);
        IBrush border = selected ? Brushes.White : new SolidColorBrush(Color.Parse("#d9edf7"));
        context.DrawEllipse(fill, new Pen(border, 1.5), center, radius, radius);
    }

    private void DrawPortLabel(DrawingContext context, GraphPortLayout layout, string label, bool leftSide)
    {
        if (Zoom < 0.55 || string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        if (layout.PortKind == NodePortKind.Flow && IsGenericFlowLabel(label))
        {
            return;
        }

        var point = ToScreen(layout.Point);
        var x = leftSide ? point.X + (12.0 * Zoom) : point.X - (54.0 * Zoom);
        DrawText(context, label, x, point.Y - (8.0 * Zoom), 10.5 * Zoom, Brushes.White, maxWidth: 48.0 * Zoom);
    }

    private static bool IsGenericFlowLabel(string label)
    {
        return string.Equals(label, "In", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(label, "Out", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(label, "Flow In", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(label, "Flow Out", StringComparison.OrdinalIgnoreCase);
    }
}
