using Avalonia;
using Avalonia.Media;
using Vrs.Graph.Model;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    private void DrawCollapsedFragments(DrawingContext context, IReadOnlyList<GraphFragment> fragments)
    {
        if (ViewMode != GraphViewMode.Simple)
        {
            return;
        }

        foreach (var fragment in fragments.Where(fragment => fragment.Collapsed))
        {
            var rect = ToScreenRect(FragmentRect(fragment));
            var selected = string.Equals(SelectedFragmentId, fragment.Id, StringComparison.OrdinalIgnoreCase);
            var fill = fragment.Kind == GraphFragmentKind.State ? "#1d2d24" : "#202637";
            var accent = fragment.Kind == GraphFragmentKind.State ? "#30a66a" : "#7a6be0";
            var border = selected ? new Pen(BrushFromHex(accent), 2.5) : new Pen(new SolidColorBrush(Color.Parse("#415166")), 1.2);

            context.DrawRectangle(BrushFromHex(fill), border, rect, 8, 8);
            context.DrawRectangle(BrushFromHex(accent), null, new Rect(rect.X, rect.Y, rect.Width, 26.0 * Zoom), 8, 8);

            var textLeft = rect.X + (12.0 * Zoom);
            var textWidth = Math.Max(32.0, rect.Width - (24.0 * Zoom));
            DrawText(context, fragment.Name, textLeft, rect.Y + (7.0 * Zoom), 13.5 * Zoom, Brushes.White, FontWeight.SemiBold, textWidth);
            DrawText(context, fragment.Kind.ToString(), textLeft, rect.Y + (40.0 * Zoom), 12.0 * Zoom, new SolidColorBrush(Color.Parse("#c7d7e8")), maxWidth: textWidth);
            DrawText(context, $"{fragment.NodeIds.Count} node(s)", textLeft, rect.Y + (62.0 * Zoom), 11.5 * Zoom, new SolidColorBrush(Color.Parse("#9aa8b5")), maxWidth: textWidth);
        }
    }
}
