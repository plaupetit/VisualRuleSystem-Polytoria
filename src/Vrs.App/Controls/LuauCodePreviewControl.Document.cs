using Vrs.Core.Export;

namespace Vrs.App.Controls;

public sealed partial class LuauCodePreviewControl
{
    // Owns the lightweight document model used by rendering: normalized lines,
    // classifier tokens, and monospace measurements.
    private void RebuildDocument(string text)
    {
        lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (lines.Length == 0)
        {
            lines = [""];
        }

        tokens = LuauSyntaxClassifier.Classify(text);
        MeasureMetrics();
        ClampOffsets();
        InvalidateVisual();
    }

    private void MeasureMetrics()
    {
        var normalBrush = BrushFor(LuauSyntaxTokenKind.Identifier);
        var sample = CreateText("0000000000", normalBrush);
        charWidth = Math.Max(1, sample.Width / 10);
        lineHeight = Math.Max(18, sample.Height + 3);
        maxLineWidth = lines.Length == 0 ? 0 : lines.Max(line => line.Length * charWidth);
    }

    private int FindFirstTokenForLine(int lineIndex)
    {
        var offset = OffsetForLine(lineIndex);
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Start + tokens[i].Length >= offset)
            {
                return i;
            }
        }

        return tokens.Count;
    }

    private int OffsetForLine(int lineIndex)
    {
        var offset = 0;
        for (var i = 0; i < lineIndex && i < lines.Length; i++)
        {
            offset += lines[i].Length + 1;
        }

        return offset;
    }
}
