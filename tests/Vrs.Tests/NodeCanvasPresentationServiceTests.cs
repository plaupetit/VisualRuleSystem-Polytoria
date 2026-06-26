using Vrs.App.Services;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.Tests;

public sealed class NodeCanvasPresentationServiceTests
{
    [Fact]
    public void BuildTooltip_IncludesDescriptionConfiguredSummaryAndAuthorNote()
    {
        var service = new NodeCanvasPresentationService();
        var entry = ShowMessageEntry();
        var node = NodeCatalogService.CreateNode(entry);
        node.UserComment = "Keep this visible while testing.";
        var message = node.Parameters.Single(parameter => parameter.Key == "message");
        message.Value = "Hello player";
        message.Binding.ConstantValue = "Hello player";
        message.Binding.DisplayText = "Hello player";

        var tooltip = service.BuildTooltip(node, entry);

        Assert.Equal("Show Message", tooltip.Title);
        Assert.Contains("Type: Action / ShowMessage", tooltip.Lines);
        Assert.Contains("What it does: Prints a message to the output console.", tooltip.Lines);
        Assert.Contains("Configured as: Do Show Message: Hello player", tooltip.Lines);
        Assert.Contains("Author note: Keep this visible while testing.", tooltip.Lines);
    }

    [Fact]
    public void BuildTooltip_LeavesAuthorNoteOutWhenNodeHasNoUserComment()
    {
        var service = new NodeCanvasPresentationService();
        var entry = ShowMessageEntry();
        var node = NodeCatalogService.CreateNode(entry);

        var tooltip = service.BuildTooltip(node, entry);

        Assert.DoesNotContain(tooltip.Lines, line => line.StartsWith("Author note:", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildTooltip_IncludesImportantNodeStatuses()
    {
        var service = new NodeCanvasPresentationService();
        var node = new RuleNode
        {
            Id = "ACT_Debug",
            Kind = NodeKind.Action,
            Type = "ShowMessage",
            Label = "Show Message",
            Description = "Prints a message.",
            Enabled = false,
            DebugEnabled = true,
            Breakpoint = true
        };

        var tooltip = service.BuildTooltip(node, null);

        var status = Assert.Single(tooltip.Lines, line => line.StartsWith("Status:", StringComparison.Ordinal));
        Assert.Contains("Disabled", status, StringComparison.Ordinal);
        Assert.Contains("Debug logs enabled", status, StringComparison.Ordinal);
        Assert.Contains("Breakpoint", status, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildStatusBadges_ShowsNoteOnlyWhenUserCommentExists()
    {
        var service = new NodeCanvasPresentationService();
        var withoutNote = new RuleNode { Id = "ACT_A", Kind = NodeKind.Action };
        var withNote = new RuleNode { Id = "ACT_B", Kind = NodeKind.Action, UserComment = "Author context." };

        Assert.DoesNotContain(service.BuildStatusBadges(withoutNote), badge => badge.Label == "NOTE");
        var note = Assert.Single(service.BuildStatusBadges(withNote), badge => badge.Label == "NOTE");
        Assert.Equal("#5ee6a8", note.BorderHex);
    }

    [Fact]
    public void BuildStatusBadges_KeepsDebugBreakpointAndNoteBadgesTogether()
    {
        var service = new NodeCanvasPresentationService();
        var node = new RuleNode
        {
            Id = "ACT_DebugNote",
            Kind = NodeKind.Action,
            DebugEnabled = true,
            Breakpoint = true,
            UserComment = "Author context."
        };

        var labels = service.BuildStatusBadges(node).Select(badge => badge.Label).ToList();

        Assert.Equal(["DBG", "BRK", "NOTE"], labels);
    }

    private static NodeCatalogEntry ShowMessageEntry()
    {
        return new NodeCatalogEntry
        {
            IdBase = "ACT_ShowMessage",
            Kind = NodeKind.Action,
            Type = "ShowMessage",
            Label = "Show Message",
            Description = "Prints a message to the output console.",
            PreviewTemplate = "Do Show Message: ${message}",
            Parameters =
            [
                new NodeCatalogParameterDefinition
                {
                    Key = "message",
                    Label = "Message",
                    Default = "Hello"
                }
            ]
        };
    }
}
