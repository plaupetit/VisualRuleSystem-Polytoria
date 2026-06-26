using Avalonia;
using Avalonia.Media;
using Vrs.App.Services;
using Vrs.Graph.Model;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    private void DrawGroups(DrawingContext context, IReadOnlyList<RuleNodeGroup> groups)
    {
        foreach (var group in GroupsInDrawOrder(groups))
        {
            DrawGroup(context, group);
        }
    }

    private void DrawGroup(DrawingContext context, RuleNodeGroup group)
    {
        var colors = NodeGroupColorPalette.Resolve(group.Color);
        var rect = ToScreenRect(GroupRect(group));
        var selected = string.Equals(SelectedGroupId, group.Id, StringComparison.OrdinalIgnoreCase);
        var fill = new SolidColorBrush(Color.Parse(colors.FillHex), 0.72);
        var border = selected ? new Pen(BrushFromHex(colors.AccentHex), 2.4) : new Pen(BrushFromHex(colors.BorderHex), 1.3);
        var headerHeight = Math.Max(18.0, 32.0 * Zoom);

        context.DrawRectangle(fill, border, rect, 7, 7);
        context.DrawRectangle(BrushFromHex(colors.BorderHex), null, new Rect(rect.X, rect.Y, rect.Width, headerHeight), 7, 7);

        var textLeft = rect.X + (12.0 * Zoom);
        var textWidth = Math.Max(48.0, rect.Width - (82.0 * Zoom));
        DrawText(context, string.IsNullOrWhiteSpace(group.Name) ? "Group" : group.Name, textLeft, rect.Y + (8.0 * Zoom), 13.0 * Zoom, BrushFromHex(colors.TextHex), FontWeight.SemiBold, textWidth);

        var countText = group.MemberRerouteIds.Count == 0
            ? $"{group.MemberNodeIds.Count} node(s)"
            : $"{group.MemberNodeIds.Count} node / {group.MemberRerouteIds.Count} reroute";
        var countWidth = group.MemberRerouteIds.Count == 0 ? 62.0 : 116.0;
        DrawText(context, countText, rect.Right - ((countWidth + 8.0) * Zoom), rect.Y + (9.0 * Zoom), 10.0 * Zoom, BrushFromHex(colors.TextHex), FontWeight.Normal, countWidth * Zoom);

        if (!string.IsNullOrWhiteSpace(group.ParentGroupId) && Zoom >= 0.55)
        {
            DrawText(context, "nested", textLeft, rect.Y + headerHeight + (8.0 * Zoom), 10.5 * Zoom, BrushFromHex(colors.TextHex), FontWeight.SemiBold, 72.0 * Zoom);
        }

        if (selected && Zoom >= 0.5)
        {
            var handle = ToScreenRect(GroupResizeHandleRect(group));
            context.DrawRectangle(BrushFromHex(colors.AccentHex), new Pen(BrushFromHex(colors.TextHex), 1.0), handle, 2, 2);
        }
    }

    private void DrawSelectionRectangle(DrawingContext context)
    {
        if (!selectingNodes)
        {
            return;
        }

        var rect = RectFromPoints(selectionStartPoint, selectionCurrentPoint);
        context.DrawRectangle(
            new SolidColorBrush(Color.Parse("#2f80ed"), 0.16),
            new Pen(new SolidColorBrush(Color.Parse("#80c7ff")), 1.2),
            rect);
    }
}
