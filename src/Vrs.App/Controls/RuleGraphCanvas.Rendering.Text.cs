using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    private void DrawText(
        DrawingContext context,
        string text,
        double x,
        double y,
        double size,
        IBrush brush,
        FontWeight weight = default,
        double maxWidth = 0,
        int maxLineCount = 1)
    {
        if (string.IsNullOrWhiteSpace(text) || size < 7)
        {
            return;
        }

        var layout = new TextLayout(
            text,
            new Typeface("Inter", FontStyle.Normal, weight == default ? FontWeight.Normal : weight),
            size,
            brush,
            TextAlignment.Left,
            TextWrapping.NoWrap,
            maxWidth > 0 ? TextTrimming.CharacterEllipsis : TextTrimming.None,
            textDecorations: null!,
            FlowDirection.LeftToRight,
            maxWidth > 0 ? maxWidth : double.PositiveInfinity,
            size * 1.35 * Math.Max(1, maxLineCount),
            lineHeight: size * 1.22,
            letterSpacing: 0,
            Math.Max(1, maxLineCount),
            fontFeatures: null!,
            textStyleOverrides: null!);

        layout.Draw(context, new Point(x, y));
    }
}
