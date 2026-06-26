namespace Vrs.Core.Export;

public sealed record LuauPreviewDiffResult(
    bool HasPreviousPreview,
    bool Changed,
    int FirstChangedLine,
    int AddedLineCount,
    int RemovedLineCount,
    IReadOnlyList<int> HighlightedLineNumbers)
{
    public string StatusSuffix
    {
        get
        {
            if (!HasPreviousPreview || !Changed)
            {
                return "";
            }

            if (AddedLineCount > 0 && RemovedLineCount > 0)
            {
                return $"Export changed: +{AddedLineCount}/-{RemovedLineCount} lines.";
            }

            if (AddedLineCount > 0)
            {
                return $"Export changed: +{AddedLineCount} lines.";
            }

            if (RemovedLineCount > 0)
            {
                return $"Export changed: -{RemovedLineCount} lines.";
            }

            return "Export changed.";
        }
    }
}

/// <summary>
/// Produces a small line-level diff for editor feedback without turning the
/// script preview into a full source-control diff tool.
/// </summary>
public static class LuauPreviewDiffService
{
    public static LuauPreviewDiffResult Compare(string? previousText, string? nextText)
    {
        previousText ??= "";
        nextText ??= "";

        var hasPreviousPreview = !string.IsNullOrEmpty(previousText);
        if (string.Equals(previousText, nextText, StringComparison.Ordinal))
        {
            return new LuauPreviewDiffResult(hasPreviousPreview, false, 0, 0, 0, []);
        }

        var previousLines = SplitLines(previousText);
        var nextLines = SplitLines(nextText);
        var prefix = CommonPrefixLength(previousLines, nextLines);
        var suffix = CommonSuffixLength(previousLines, nextLines, prefix);
        var removedLineCount = Math.Max(0, previousLines.Length - prefix - suffix);
        var addedLineCount = Math.Max(0, nextLines.Length - prefix - suffix);
        var firstChangedLine = Math.Clamp(prefix + 1, 1, Math.Max(1, nextLines.Length));
        var highlightCount = Math.Max(1, addedLineCount);
        var highlightedLines = Enumerable
            .Range(firstChangedLine, highlightCount)
            .Where(line => line >= 1 && line <= nextLines.Length)
            .ToList();

        if (highlightedLines.Count == 0 && nextLines.Length > 0)
        {
            highlightedLines.Add(Math.Min(firstChangedLine, nextLines.Length));
        }

        return new LuauPreviewDiffResult(
            hasPreviousPreview,
            true,
            firstChangedLine,
            addedLineCount,
            removedLineCount,
            highlightedLines);
    }

    private static string[] SplitLines(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
    }

    private static int CommonPrefixLength(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        var max = Math.Min(left.Count, right.Count);
        var index = 0;
        while (index < max && string.Equals(left[index], right[index], StringComparison.Ordinal))
        {
            index++;
        }

        return index;
    }

    private static int CommonSuffixLength(IReadOnlyList<string> left, IReadOnlyList<string> right, int prefixLength)
    {
        var max = Math.Min(left.Count, right.Count) - prefixLength;
        var suffix = 0;
        while (suffix < max &&
            string.Equals(left[left.Count - 1 - suffix], right[right.Count - 1 - suffix], StringComparison.Ordinal))
        {
            suffix++;
        }

        return suffix;
    }
}
