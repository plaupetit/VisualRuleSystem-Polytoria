using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Vrs.Graph.Model;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    private void DrawNodeHoverTooltip(DrawingContext context, IReadOnlyList<RuleNode> visibleNodes)
    {
        if (nodePaletteOpen ||
            pendingOutputPin is not null ||
            !string.IsNullOrWhiteSpace(draggedNodeId) ||
            panning ||
            string.IsNullOrWhiteSpace(hoveredNodeId))
        {
            return;
        }

        var node = visibleNodes.FirstOrDefault(item =>
            string.Equals(item.Id, hoveredNodeId, StringComparison.OrdinalIgnoreCase));
        if (node is null)
        {
            return;
        }

        var tooltip = NodeCanvasPresentation.BuildTooltip(node, CatalogEntryFor(node));
        var nodeBounds = ToScreenRect(geometry.NodeRect(node));
        const double width = 380;
        const double horizontalPadding = 12;
        const double verticalPadding = 10;
        var contentWidth = width - horizontalPadding * 2;

        var titleLayout = CreateNodePaletteTooltipLayout(
            tooltip.Title,
            12,
            BrushFromHex("#d8dde4"),
            FontWeight.SemiBold,
            contentWidth);
        var bodyLayouts = tooltip.Lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => CreateNodePaletteTooltipLayout(line, 10.5, BrushFromHex("#aeb8c4"), FontWeight.Normal, contentWidth))
            .ToList();
        var bodyHeight = bodyLayouts.Sum(layout => layout.Height) + Math.Max(0, bodyLayouts.Count - 1) * 5;
        var height = verticalPadding + titleLayout.Height + (bodyLayouts.Count > 0 ? 8 + bodyHeight : 0) + verticalPadding;
        var placement = PlaceTooltipAroundAnchor(nodeBounds, width, height, 12, nodeTooltipPointerPoint);
        var bounds = placement.Bounds;

        context.DrawRectangle(BrushFromHex("#1b1f26"), new Pen(BrushFromHex("#515151"), 1), bounds, 5, 5);
        context.DrawRectangle(BrushFromHex(tooltip.AccentHex), null, TooltipAccentBounds(bounds, placement.AccentSide), 5, 5);
        titleLayout.Draw(context, new Point(bounds.X + horizontalPadding, bounds.Y + verticalPadding));

        var lineY = bounds.Y + verticalPadding + titleLayout.Height + 8;
        foreach (var layout in bodyLayouts)
        {
            layout.Draw(context, new Point(bounds.X + horizontalPadding, lineY));
            lineY += layout.Height + 5;
        }
    }

    private TooltipPlacement PlaceTooltipAroundAnchor(
        Rect anchorBounds,
        double width,
        double height,
        double gap,
        Point preferredPoint)
    {
        const double margin = 8;
        var maxX = Math.Max(margin, Bounds.Width - width - margin);
        var maxY = Math.Max(margin, Bounds.Height - height - margin);
        var lateralY = Math.Clamp(preferredPoint.Y + 14, margin, maxY);

        var rightX = anchorBounds.Right + gap;
        if (rightX + width <= Bounds.Width - margin)
        {
            return new TooltipPlacement(new Rect(rightX, lateralY, width, height), TooltipAccentSide.Left);
        }

        var leftX = anchorBounds.X - width - gap;
        if (leftX >= margin)
        {
            return new TooltipPlacement(new Rect(leftX, lateralY, width, height), TooltipAccentSide.Right);
        }

        var centeredX = Math.Clamp(anchorBounds.Center.X - width / 2, margin, maxX);
        var bottomY = anchorBounds.Bottom + gap;
        if (bottomY + height <= Bounds.Height - margin)
        {
            return new TooltipPlacement(new Rect(centeredX, bottomY, width, height), TooltipAccentSide.Top);
        }

        var topY = anchorBounds.Y - height - gap;
        if (topY >= margin)
        {
            return new TooltipPlacement(new Rect(centeredX, topY, width, height), TooltipAccentSide.Bottom);
        }

        // Last-resort clamp still keeps the accent pointing toward the closest available vertical side.
        var bottomSpace = Bounds.Height - margin - bottomY;
        var topSpace = topY - margin;
        var fallbackY = bottomSpace >= topSpace
            ? Math.Clamp(bottomY, margin, maxY)
            : Math.Clamp(topY, margin, maxY);
        var accentSide = bottomSpace >= topSpace ? TooltipAccentSide.Top : TooltipAccentSide.Bottom;
        return new TooltipPlacement(new Rect(centeredX, fallbackY, width, height), accentSide);
    }

    private static Rect TooltipAccentBounds(Rect tooltipBounds, TooltipAccentSide side)
    {
        const double stripeWidth = 4;
        // Keep the accent stripe on the side facing the palette/node that spawned the tooltip.
        return side switch
        {
            TooltipAccentSide.Right => new Rect(tooltipBounds.Right - stripeWidth, tooltipBounds.Y, stripeWidth, tooltipBounds.Height),
            TooltipAccentSide.Top => new Rect(tooltipBounds.X, tooltipBounds.Y, tooltipBounds.Width, stripeWidth),
            TooltipAccentSide.Bottom => new Rect(tooltipBounds.X, tooltipBounds.Bottom - stripeWidth, tooltipBounds.Width, stripeWidth),
            _ => new Rect(tooltipBounds.X, tooltipBounds.Y, stripeWidth, tooltipBounds.Height)
        };
    }

    private enum TooltipAccentSide
    {
        Left,
        Right,
        Top,
        Bottom
    }

    private readonly record struct TooltipPlacement(Rect Bounds, TooltipAccentSide AccentSide);
}
