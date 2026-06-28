using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Vrs.App.Services;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    // Rendering and layout helpers for the GC2-style node palette browser.
    private void DrawNodePalette(DrawingContext context)
    {
        if (!nodePaletteOpen)
        {
            return;
        }

        var entries = NodePaletteEntries();
        var allEntries = NodePaletteEntries(includeIncompatible: true);
        CoerceNodePaletteSelection(entries);
        EnsureNodePaletteSelectionVisible(entries.Count);

        var bounds = NodePaletteBounds(entries);
        context.DrawRectangle(BrushFromHex("#2b2b2b"), new Pen(BrushFromHex("#515151"), 1), bounds, 4, 4);
        DrawNodePaletteHeader(context, bounds, entries.Count);

        if (entries.Count == 0)
        {
            var message = allEntries.Count > 0 && nodePaletteCompatibleOnly
                ? "Only incompatible nodes here. Turn off Compatible to inspect them."
                : "No node matches this search or folder.";
            DrawText(context, message, bounds.X + 12, bounds.Y + 112, 12, BrushFromHex("#c7d7e8"), maxWidth: bounds.Width - 24, maxLineCount: 2);
            DrawNodePaletteTooltip(context, bounds, null);
            return;
        }

        foreach (var row in NodePaletteRows(entries))
        {
            var paletteRow = entries[row.EntryIndex];
            DrawNodePaletteRow(context, row.Bounds, paletteRow, row.EntryIndex == nodePaletteSelectedIndex);
        }

        var selected = nodePaletteSelectedIndex >= 0 && nodePaletteSelectedIndex < entries.Count
            ? entries[nodePaletteSelectedIndex]
            : null;
        DrawNodePaletteTooltip(context, bounds, selected);
    }

    private void DrawNodePaletteHeader(DrawingContext context, Rect bounds, int resultCount)
    {
        var title = nodePaletteConnectFrom is null ? "Add Node Here" : "Add Node And Connect";
        DrawText(context, title, bounds.X + 12, bounds.Y + 8, 12.5, BrushFromHex("#b9c3cf"), FontWeight.SemiBold, bounds.Width - 246);

        var apiSurfaceRect = NodePaletteApiSurfaceToggleBounds(bounds);
        var apiSurfaceLabel = NodePaletteApiSurfaceFilterText();
        var apiSurfacePen = nodePaletteApiSurfaceFilter switch
        {
            NodePaletteApiSurfaceFilter.Creator => "#f59e0b",
            NodePaletteApiSurfaceFilter.All => "#aeb8c4",
            _ => "#60a5fa"
        };
        var apiSurfaceFill = nodePaletteApiSurfaceFilter switch
        {
            NodePaletteApiSurfaceFilter.Creator => "#352a1d",
            NodePaletteApiSurfaceFilter.All => "#343434",
            _ => "#1d2a38"
        };
        context.DrawRectangle(BrushFromHex(apiSurfaceFill), new Pen(BrushFromHex(apiSurfacePen), 1), apiSurfaceRect, 4, 4);
        DrawText(context, apiSurfaceLabel, apiSurfaceRect.X + 8, apiSurfaceRect.Y + 4, 10.5, BrushFromHex(apiSurfacePen), FontWeight.SemiBold, apiSurfaceRect.Width - 12);

        var compatibleRect = NodePaletteCompatibleToggleBounds(bounds);
        var compatibleFill = nodePaletteCompatibleOnly ? "#1d2a38" : "#343434";
        var compatiblePen = nodePaletteCompatibleOnly ? "#34d399" : "#5c6570";
        context.DrawRectangle(BrushFromHex(compatibleFill), new Pen(BrushFromHex(compatiblePen), 1), compatibleRect, 4, 4);
        DrawText(context, "Compatible", compatibleRect.X + 8, compatibleRect.Y + 4, 10.5, BrushFromHex(compatiblePen), FontWeight.SemiBold, compatibleRect.Width - 12);

        var searchRect = NodePaletteSearchBounds(bounds);
        context.DrawRectangle(BrushFromHex("#171717"), new Pen(BrushFromHex("#454545"), 1), searchRect, 3, 3);
        var searchText = string.IsNullOrWhiteSpace(nodePaletteSearch) ? "Search nodes..." : nodePaletteSearch;
        var searchBrush = string.IsNullOrWhiteSpace(nodePaletteSearch) ? BrushFromHex("#79818b") : Brushes.White;
        DrawText(context, searchText, searchRect.X + 9, searchRect.Y + 6, 12, searchBrush, maxWidth: searchRect.Width - 18);

        var breadcrumbRect = NodePaletteBreadcrumbBounds(bounds);
        context.DrawRectangle(BrushFromHex("#383838"), new Pen(BrushFromHex("#242424"), 1), breadcrumbRect);
        if (CanGoBackNodePalette())
        {
            var backRect = NodePaletteBackBounds(bounds);
            DrawText(context, "<", backRect.X + 8, backRect.Y + 5, 13, BrushFromHex("#aab4c0"), FontWeight.Bold, backRect.Width - 8);
        }

        DrawText(
            context,
            NodePaletteBreadcrumbText(resultCount),
            breadcrumbRect.X + 42,
            breadcrumbRect.Y + 6,
            12,
            BrushFromHex("#d0d0d0"),
            FontWeight.SemiBold,
            breadcrumbRect.Width - 84);
    }

    private void DrawNodePaletteRow(DrawingContext context, Rect bounds, NodePaletteBrowserRow row, bool selected)
    {
        var compatible = row.Kind == NodePaletteBrowserRowKind.Folder || row.IsCompatible;
        var fill = selected ? "#225f93" : "#2b2b2b";
        var textBrush = compatible ? BrushFromHex("#d8dde4") : BrushFromHex("#7f8894");
        var metaBrush = compatible ? BrushFromHex("#a7b2bf") : BrushFromHex("#d19a66");
        var searchMode = IsNodePaletteSearchActive();

        context.DrawRectangle(BrushFromHex(fill), null, bounds);
        DrawNodePaletteIconChip(context, bounds, row, compatible);
        if (row.Kind == NodePaletteBrowserRowKind.Folder)
        {
            DrawText(context, row.Label, bounds.X + 40, bounds.Y + 6, 12.5, textBrush, FontWeight.SemiBold, bounds.Width - 82);
            DrawText(context, ">", bounds.Right - 22, bounds.Y + 6, 12.5, BrushFromHex("#a0a7ad"), FontWeight.SemiBold, 16);
            return;
        }

        var labelY = searchMode ? bounds.Y + 4 : bounds.Y + 6;
        DrawText(context, row.Label, bounds.X + 40, labelY, 12.5, textBrush, FontWeight.SemiBold, bounds.Width - 132);
        if (!string.IsNullOrWhiteSpace(row.RuntimeLabel))
        {
            var badgeRect = new Rect(bounds.Right - 74, bounds.Y + (bounds.Height - 18) / 2, 62, 18);
            var badgeColor = compatible ? "#51aee8" : "#6f7780";
            context.DrawRectangle(BrushFromHex("#1a1f25"), new Pen(BrushFromHex(badgeColor), 1), badgeRect, 3, 3);
            DrawText(context, row.RuntimeLabel, badgeRect.X + 7, badgeRect.Y + 3, 9.5, BrushFromHex(badgeColor), FontWeight.SemiBold, badgeRect.Width - 10);
        }

        if (searchMode)
        {
            var detail = compatible
                ? FirstNonEmpty(row.MatchSummary, row.DomainLabel)
                : FirstNonEmpty(row.IncompatibilityReason, row.MatchSummary, row.DomainLabel);
            DrawText(context, detail, bounds.X + 40, bounds.Y + 22, 10.5, metaBrush, maxWidth: bounds.Width - 118);
        }
        else if (!compatible && !string.IsNullOrWhiteSpace(row.IncompatibilityReason))
        {
            DrawText(context, row.IncompatibilityReason, bounds.X + 40, bounds.Y + 20, 9.5, metaBrush, maxWidth: bounds.Width - 118);
        }
    }

    private void DrawNodePaletteIconChip(
        DrawingContext context,
        Rect rowBounds,
        NodePaletteBrowserRow row,
        bool compatible)
    {
        var accent = compatible ? row.IconAccentHex : "#d19a66";
        var background = compatible ? row.IconBackgroundHex : "#3a2514";
        var chip = new Rect(rowBounds.X + 10, rowBounds.Y + (rowBounds.Height - 18) / 2, 22, 18);
        context.DrawRectangle(BrushFromHex(background), new Pen(BrushFromHex(accent), 1), chip, 3, 3);
        var glyphSize = row.IconGlyph.Length > 1 ? 8.5 : 10.5;
        var glyphX = row.IconGlyph.Length > 1 ? chip.X + 3.2 : chip.X + 7.2;
        DrawText(context, row.IconGlyph, glyphX, chip.Y + 3, glyphSize, BrushFromHex(accent), FontWeight.SemiBold, chip.Width - 4);
    }

    private void DrawNodePaletteTooltip(
        DrawingContext context,
        Rect paletteBounds,
        NodePaletteBrowserRow? selected)
    {
        var tooltip = NodePaletteTooltipFor(paletteBounds, selected);
        if (tooltip is null)
        {
            return;
        }

        var paragraphs = NodePaletteTooltipLines(tooltip.Value.Body);
        const double width = 360;
        const double horizontalPadding = 12;
        const double verticalPadding = 10;
        var contentWidth = width - horizontalPadding * 2;
        var titleLayout = CreateNodePaletteTooltipLayout(
            tooltip.Value.Title,
            12,
            BrushFromHex("#d8dde4"),
            FontWeight.SemiBold,
            contentWidth);
        var bodyLayouts = paragraphs
            .Select(paragraph => CreateNodePaletteTooltipLayout(paragraph, 10.5, BrushFromHex("#aeb8c4"), FontWeight.Normal, contentWidth))
            .ToList();
        var bodyHeight = bodyLayouts.Sum(layout => layout.Height) + Math.Max(0, bodyLayouts.Count - 1) * 5;
        var height = verticalPadding + titleLayout.Height + (bodyLayouts.Count > 0 ? 8 + bodyHeight : 0) + verticalPadding;
        var preferredPoint = nodePalettePointerInside
            ? nodePalettePointerPoint
            : new Point(paletteBounds.X, paletteBounds.Y);
        var placement = PlaceTooltipAroundAnchor(paletteBounds, width, height, 10, preferredPoint);
        var bounds = placement.Bounds;

        context.DrawRectangle(BrushFromHex("#1b1f26"), new Pen(BrushFromHex("#515151"), 1), bounds, 5, 5);
        context.DrawRectangle(BrushFromHex(tooltip.Value.AccentHex), null, TooltipAccentBounds(bounds, placement.AccentSide), 5, 5);
        titleLayout.Draw(context, new Point(bounds.X + horizontalPadding, bounds.Y + verticalPadding));

        var lineY = bounds.Y + verticalPadding + titleLayout.Height + 8;
        foreach (var layout in bodyLayouts)
        {
            layout.Draw(context, new Point(bounds.X + horizontalPadding, lineY));
            lineY += layout.Height + 5;
        }
    }

    private NodePaletteTooltip? NodePaletteTooltipFor(Rect paletteBounds, NodePaletteBrowserRow? selected)
    {
        if (nodePalettePointerInside)
        {
            if (NodePaletteCompatibleToggleBounds(paletteBounds).Contains(nodePalettePointerPoint))
            {
                var mode = nodePaletteCompatibleOnly
                    ? "Only compatible nodes are shown. Press Ctrl+K or click to inspect incompatible choices."
                    : "Compatible and incompatible nodes are shown. Incompatible rows explain what blocks them.";
                return new NodePaletteTooltip("Compatible filter", mode, nodePaletteCompatibleOnly ? "#34d399" : "#aeb8c4");
            }

            if (NodePaletteApiSurfaceToggleBounds(paletteBounds).Contains(nodePalettePointerPoint))
            {
                var mode = nodePaletteApiSurfaceFilter switch
                {
                    NodePaletteApiSurfaceFilter.Creator => "Only Creator/editor/addon API nodes are shown. Press Ctrl+G or click to cycle.",
                    NodePaletteApiSurfaceFilter.All => "Gameplay and Creator API nodes are shown together. Press Ctrl+G or click to cycle.",
                    _ => "Only gameplay runtime API nodes are shown. Runtime UI nodes stay in Gameplay. Press Ctrl+G or click to cycle."
                };
                return new NodePaletteTooltip("API surface filter", mode, NodePaletteApiSurfaceAccentHex());
            }

            if (CanGoBackNodePalette() && NodePaletteBackBounds(paletteBounds).Contains(nodePalettePointerPoint))
            {
                return new NodePaletteTooltip(
                    "Back",
                    "Return to the previous palette level, or clear the active search. Shortcut: Backspace.",
                    "#aeb8c4");
            }

            if (NodePaletteSearchBounds(paletteBounds).Contains(nodePalettePointerPoint))
            {
                return new NodePaletteTooltip(
                    "Search nodes",
                    "Type by intent, label, category, parameters, synonyms, or simple typos.\nDelete clears the search.",
                    "#8fd0ff");
            }
        }

        return selected is null
            ? null
            : new NodePaletteTooltip(selected.TooltipTitle, selected.TooltipText, selected.IconAccentHex);
    }

    private static IReadOnlyList<string> NodePaletteTooltipLines(string body)
    {
        return body
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    private static TextLayout CreateNodePaletteTooltipLayout(
        string text,
        double size,
        IBrush brush,
        FontWeight weight,
        double maxWidth)
    {
        return new TextLayout(
            text,
            new Typeface("Inter", FontStyle.Normal, weight),
            size,
            brush,
            TextAlignment.Left,
            TextWrapping.Wrap,
            TextTrimming.None,
            textDecorations: null!,
            FlowDirection.LeftToRight,
            maxWidth,
            double.PositiveInfinity,
            lineHeight: size * 1.25,
            letterSpacing: 0,
            maxLines: int.MaxValue,
            fontFeatures: null!,
            textStyleOverrides: null!);
    }

    private Rect NodePaletteBounds(IReadOnlyList<NodePaletteBrowserRow> entries)
    {
        var width = IsNodePaletteSearchActive() ? 500 : 420;
        var height = NodePaletteHeight(entries);
        var x = Math.Clamp(nodePalettePoint.X, 8, Math.Max(8, Bounds.Width - width - 8));
        var y = Math.Clamp(nodePalettePoint.Y, 8, Math.Max(8, Bounds.Height - height - 8));
        return new Rect(x, y, width, height);
    }

    private double NodePaletteHeight(IReadOnlyList<NodePaletteBrowserRow> entries)
    {
        var rowCount = Math.Min(NodePaletteMaxVisibleEntries(entries.Count), Math.Max(1, entries.Count));
        return NodePaletteHeaderHeight + rowCount * CurrentNodePaletteRowHeight();
    }

    private IReadOnlyList<NodePaletteRow> NodePaletteRows(IReadOnlyList<NodePaletteBrowserRow> entries)
    {
        var bounds = NodePaletteBounds(entries);
        var rows = new List<NodePaletteRow>();
        var y = bounds.Y + NodePaletteHeaderHeight;
        var rowHeight = CurrentNodePaletteRowHeight();

        foreach (var entry in VisibleNodePaletteEntries(entries))
        {
            rows.Add(new NodePaletteRow(new Rect(bounds.X, y, bounds.Width, rowHeight), entry.Index, ""));
            y += rowHeight;
        }

        return rows;
    }

    private IReadOnlyList<NodePaletteBrowserRow> VisibleNodePaletteEntries(IReadOnlyList<NodePaletteBrowserRow> entries)
    {
        var maxVisible = NodePaletteMaxVisibleEntries(entries.Count);
        return entries
            .Skip(nodePaletteScrollIndex)
            .Take(maxVisible)
            .ToList();
    }

    private int NodePaletteMaxVisibleEntries(int entryCount)
    {
        var maxRows = IsNodePaletteSearchActive() ? 9 : 11;
        return Math.Min(maxRows, Math.Max(1, entryCount));
    }

    private Rect NodePaletteBackBounds(Rect bounds)
    {
        return new Rect(bounds.X + 8, bounds.Y + 67, 28, 26);
    }

    private Rect NodePaletteSearchBounds(Rect bounds)
    {
        return new Rect(bounds.X + 10, bounds.Y + 31, bounds.Width - 20, 28);
    }

    private Rect NodePaletteBreadcrumbBounds(Rect bounds)
    {
        return new Rect(bounds.X, bounds.Y + 66, bounds.Width, 28);
    }

    private Rect NodePaletteCompatibleToggleBounds(Rect bounds)
    {
        return new Rect(bounds.Right - 104, bounds.Y + 6, 92, 22);
    }

    private Rect NodePaletteApiSurfaceToggleBounds(Rect bounds)
    {
        return new Rect(bounds.Right - 204, bounds.Y + 6, 92, 22);
    }

    private string NodePaletteApiSurfaceFilterText()
    {
        return nodePaletteApiSurfaceFilter switch
        {
            NodePaletteApiSurfaceFilter.Creator => "Creator",
            NodePaletteApiSurfaceFilter.All => "All",
            _ => "Gameplay"
        };
    }

    private string NodePaletteApiSurfaceAccentHex()
    {
        return nodePaletteApiSurfaceFilter switch
        {
            NodePaletteApiSurfaceFilter.Creator => "#f59e0b",
            NodePaletteApiSurfaceFilter.All => "#aeb8c4",
            _ => "#60a5fa"
        };
    }

    private string NodePaletteBreadcrumbText(int resultCount)
    {
        if (!string.IsNullOrWhiteSpace(nodePaletteSearch))
        {
            return $"Search Results · {resultCount}";
        }

        if (string.IsNullOrWhiteSpace(nodePaletteCurrentIntentKey))
        {
            return "Node";
        }

        if (nodePaletteCurrentDomainPath.Count == 0)
        {
            return nodePaletteCurrentIntentKey;
        }

        return $"{nodePaletteCurrentIntentKey} / {string.Join(" / ", nodePaletteCurrentDomainPath)}";
    }

    private bool IsNodePaletteSearchActive()
    {
        return !string.IsNullOrWhiteSpace(nodePaletteSearch);
    }

    private double CurrentNodePaletteRowHeight()
    {
        return IsNodePaletteSearchActive() ? NodePaletteSearchRowHeight : NodePaletteRowHeight;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "";
    }

    private const double NodePaletteHeaderHeight = 96;
    private const double NodePaletteRowHeight = 28;
    private const double NodePaletteSearchRowHeight = 42;
}
