using Vrs.Core.Export;

namespace Vrs.Tests;

public sealed class LuauPreviewDiffServiceTests
{
    [Fact]
    public void Compare_DetectsAddedActionBlock()
    {
        const string before = """
            -- [VSR] TRIGGER START: On Timer Tick
            -- [VSR] FLOW OUT START: On Timer Tick
            -- [VSR] FLOW OUT END: On Timer Tick
            -- [VSR] TRIGGER END: On Timer Tick
            """;
        const string after = """
            -- [VSR] TRIGGER START: On Timer Tick
            -- [VSR] FLOW OUT START: On Timer Tick
            -- [VSR] ACTION START: Show Message
            print("Hello")
            -- [VSR] ACTION END: Show Message
            -- [VSR] FLOW OUT END: On Timer Tick
            -- [VSR] TRIGGER END: On Timer Tick
            """;

        var diff = LuauPreviewDiffService.Compare(before, after);

        Assert.True(diff.Changed);
        Assert.Equal(3, diff.FirstChangedLine);
        Assert.Equal(3, diff.AddedLineCount);
        Assert.Equal(0, diff.RemovedLineCount);
        Assert.Contains(3, diff.HighlightedLineNumbers);
        Assert.Equal("Export changed: +3 lines.", diff.StatusSuffix);
    }

    [Fact]
    public void Compare_DetectsRemovedActionBlockAndKeepsFocusInNewPreview()
    {
        const string before = """
            -- [VSR] TRIGGER START: On Timer Tick
            -- [VSR] FLOW OUT START: On Timer Tick
            -- [VSR] ACTION START: Show Message
            print("Hello")
            -- [VSR] ACTION END: Show Message
            -- [VSR] FLOW OUT END: On Timer Tick
            -- [VSR] TRIGGER END: On Timer Tick
            """;
        const string after = """
            -- [VSR] TRIGGER START: On Timer Tick
            -- [VSR] FLOW OUT START: On Timer Tick
            -- [VSR] FLOW OUT END: On Timer Tick
            -- [VSR] TRIGGER END: On Timer Tick
            """;

        var diff = LuauPreviewDiffService.Compare(before, after);

        Assert.True(diff.Changed);
        Assert.Equal(3, diff.FirstChangedLine);
        Assert.Equal(0, diff.AddedLineCount);
        Assert.Equal(3, diff.RemovedLineCount);
        Assert.Equal(new[] { 3 }, diff.HighlightedLineNumbers);
        Assert.Equal("Export changed: -3 lines.", diff.StatusSuffix);
    }

    [Fact]
    public void Compare_IdenticalPreviewHasNoDiff()
    {
        const string preview = "-- [VSR] same\nprint(\"Hello\")";

        var diff = LuauPreviewDiffService.Compare(preview, preview);

        Assert.False(diff.Changed);
        Assert.Empty(diff.HighlightedLineNumbers);
        Assert.Equal("", diff.StatusSuffix);
    }
}
