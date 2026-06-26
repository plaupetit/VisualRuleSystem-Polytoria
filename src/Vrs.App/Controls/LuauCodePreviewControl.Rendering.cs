using Avalonia;
using Avalonia.Media;
using Vrs.Core.Export;

namespace Vrs.App.Controls;

public sealed partial class LuauCodePreviewControl
{
    // Rendering stays deliberately immediate-mode: the preview is read-only and
    // avoids editor template dependencies that were fragile in this prototype.
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds;
        context.FillRectangle(backgroundBrush, bounds);
        context.FillRectangle(gutterBrush, new Rect(0, 0, LineNumberWidth, Math.Max(0, bounds.Height - ScrollbarSize)));

        if (string.IsNullOrWhiteSpace(codeText))
        {
            DrawEmptyState(context);
            DrawScrollbars(context);
            return;
        }

        var firstLine = Math.Max(0, (int)Math.Floor(verticalOffset / lineHeight));
        var y = PaddingTop - (verticalOffset - firstLine * lineHeight);
        var maxVisibleLine = Math.Min(lines.Length, firstLine + (int)Math.Ceiling(bounds.Height / lineHeight) + 2);
        var tokenIndex = FindFirstTokenForLine(firstLine);

        for (var lineIndex = firstLine; lineIndex < maxVisibleLine; lineIndex++)
        {
            DrawLineHighlight(context, lineIndex, y);
            DrawLineNumber(context, lineIndex, y);
            tokenIndex = DrawLineText(context, lineIndex, tokenIndex, y);
            y += lineHeight;
        }

        context.DrawLine(new Pen(borderBrush, 1), new Point(LineNumberWidth, 0), new Point(LineNumberWidth, bounds.Height - ScrollbarSize));
        DrawScrollbars(context);
    }

    private int DrawLineText(DrawingContext context, int lineIndex, int tokenIndex, double y)
    {
        var line = lines[lineIndex];
        var lineStart = OffsetForLine(lineIndex);
        var lineEnd = lineStart + line.Length;
        var x = LineNumberWidth + PaddingLeft - horizontalOffset;

        if (line.Length == 0)
        {
            return tokenIndex;
        }

        while (tokenIndex < tokens.Count && tokens[tokenIndex].Start + tokens[tokenIndex].Length <= lineStart)
        {
            tokenIndex++;
        }

        var cursor = 0;
        var scanIndex = tokenIndex;
        while (scanIndex < tokens.Count)
        {
            var token = tokens[scanIndex];
            if (token.Start >= lineEnd)
            {
                break;
            }

            var localStart = Math.Max(0, token.Start - lineStart);
            var localEnd = Math.Min(line.Length, token.Start + token.Length - lineStart);
            if (localStart > cursor)
            {
                DrawSegment(context, line[cursor..localStart], x + cursor * charWidth, y, BrushFor(LuauSyntaxTokenKind.Identifier));
            }

            if (localEnd > localStart)
            {
                DrawSegment(context, line[localStart..localEnd], x + localStart * charWidth, y, BrushFor(token.Kind));
            }

            cursor = Math.Max(cursor, localEnd);
            scanIndex++;
        }

        if (cursor < line.Length)
        {
            DrawSegment(context, line[cursor..], x + cursor * charWidth, y, BrushFor(LuauSyntaxTokenKind.Identifier));
        }

        return tokenIndex;
    }

    private void DrawLineNumber(DrawingContext context, int lineIndex, double y)
    {
        var text = CreateText((lineIndex + 1).ToString(), lineNumberBrush);
        context.DrawText(text, new Point(LineNumberWidth - text.Width - 10, y));
    }

    private void DrawLineHighlight(DrawingContext context, int lineIndex, double y)
    {
        if (!highlightedLineNumberSet.Contains(lineIndex + 1))
        {
            return;
        }

        var width = Math.Max(0, Bounds.Width - LineNumberWidth - ScrollbarSize);
        context.FillRectangle(changedLineBrush, new Rect(LineNumberWidth, y - 1, width, lineHeight));
    }

    private void DrawSegment(DrawingContext context, string text, double x, double y, IBrush brush)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        context.DrawText(CreateText(text, brush), new Point(x, y));
    }

    private void DrawEmptyState(DrawingContext context)
    {
        context.DrawText(CreateText("No script preview generated yet.", emptyBrush), new Point(LineNumberWidth + PaddingLeft, PaddingTop));
    }

    private void DrawScrollbars(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= ScrollbarSize || height <= ScrollbarSize)
        {
            return;
        }

        var viewportWidth = Math.Max(1, width - LineNumberWidth - ScrollbarSize);
        var viewportHeight = Math.Max(1, height - ScrollbarSize);
        var contentWidth = Math.Max(viewportWidth, maxLineWidth + PaddingLeft * 2);
        var contentHeight = Math.Max(viewportHeight, lines.Length * lineHeight + PaddingTop * 2);

        context.FillRectangle(scrollbarTrackBrush, new Rect(width - ScrollbarSize, 0, ScrollbarSize, height - ScrollbarSize));
        context.FillRectangle(scrollbarTrackBrush, new Rect(LineNumberWidth, height - ScrollbarSize, width - LineNumberWidth - ScrollbarSize, ScrollbarSize));

        var verticalThumbHeight = Math.Max(28, viewportHeight * viewportHeight / contentHeight);
        var verticalRange = Math.Max(1, viewportHeight - verticalThumbHeight);
        var verticalThumbY = (verticalOffset / Math.Max(1, contentHeight - viewportHeight)) * verticalRange;
        context.FillRectangle(scrollbarThumbBrush, new Rect(width - ScrollbarSize + 2, verticalThumbY + 2, ScrollbarSize - 4, verticalThumbHeight - 4));

        var horizontalThumbWidth = Math.Max(36, viewportWidth * viewportWidth / contentWidth);
        var horizontalRange = Math.Max(1, viewportWidth - horizontalThumbWidth);
        var horizontalThumbX = LineNumberWidth + (horizontalOffset / Math.Max(1, contentWidth - viewportWidth)) * horizontalRange;
        context.FillRectangle(scrollbarThumbBrush, new Rect(horizontalThumbX + 2, height - ScrollbarSize + 2, horizontalThumbWidth - 4, ScrollbarSize - 4));
    }

    private FormattedText CreateText(string text, IBrush brush)
    {
        return new FormattedText(
            text,
            Thread.CurrentThread.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            13,
            brush);
    }

    private IBrush BrushFor(LuauSyntaxTokenKind kind)
    {
        if (kind == LuauSyntaxTokenKind.Whitespace)
        {
            kind = LuauSyntaxTokenKind.Identifier;
        }

        if (tokenBrushes.TryGetValue(kind, out var brush))
        {
            return brush;
        }

        brush = new SolidColorBrush(Color.Parse(theme.ColorFor(kind)));
        tokenBrushes[kind] = brush;
        return brush;
    }
}
