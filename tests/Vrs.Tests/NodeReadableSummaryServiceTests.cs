using Vrs.Core.Authoring;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.Tests;

public sealed class NodeReadableSummaryServiceTests
{
    [Fact]
    public void BuildNodeSummary_UsesCatalogPreviewTemplateForTimer()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var entry = catalog.Nodes.Single(node => node.IdBase == "EV_OnTimerTick");
        var node = NodeCatalogService.CreateNode(entry);

        var summary = NodeReadableSummaryService.BuildNodeSummary(node, entry);

        Assert.Equal("On Timer Tick every 1s on Self", summary);
    }

    [Fact]
    public void BuildNodeSummary_ReflectsConfiguredMessage()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var entry = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var node = NodeCatalogService.CreateNode(entry);
        var message = node.Parameters.Single(parameter => parameter.Key == "message");
        message.Value = "Hello player";
        message.Binding.ConstantValue = "Hello player";
        message.Binding.DisplayText = "Hello player";

        var summary = NodeReadableSummaryService.BuildNodeSummary(node, entry);

        Assert.Equal("Do Show Message: Hello player", summary);
    }

    [Fact]
    public void BuildNodeSummary_PrefersVisualDisplayText()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var entry = catalog.Nodes.Single(node => node.IdBase == "EV_OnTimerTick");
        var node = NodeCatalogService.CreateNode(entry);
        var interval = node.Parameters.Single(parameter => parameter.Key == "interval");
        interval.Value = "1";
        interval.Binding.ConstantValue = "1";
        interval.Binding.DisplayText = "Constant: 2";

        var summary = NodeReadableSummaryService.BuildNodeSummary(node, entry);

        Assert.Equal("On Timer Tick every 2s on Self", summary);
    }

    [Fact]
    public void BuildNodeSummary_UsesReadableFallbackWithoutTemplate()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var entry = catalog.Nodes.Single(node => node.IdBase == "ACT_WaitSeconds");
        var node = NodeCatalogService.CreateNode(entry);

        var summary = NodeReadableSummaryService.BuildNodeSummary(node, entry);

        Assert.Equal("Do Wait Seconds: Duration Seconds: 1, Wait To Complete: true", summary);
    }

    [Fact]
    public void BuildParameterSummary_UsesDisplayTextAsReadableSource()
    {
        var parameter = new RuleParameter
        {
            Key = "value",
            Value = "fallback",
            Binding = new GraphValueBinding
            {
                SourceKind = GraphValueSourceKind.GlobalVariable,
                VariableName = "RawVariable",
                DisplayText = "Global variable: PlayerScore"
            }
        };

        var summary = NodeReadableSummaryService.BuildParameterSummary(parameter, null);

        Assert.Equal("PlayerScore", summary);
    }

    [Fact]
    public void BuildParameterSummary_DoesNotLetStaleDisplayTextOverrideSelf()
    {
        var parameter = new RuleParameter
        {
            Key = "target",
            Value = "Part",
            Binding = new GraphValueBinding
            {
                SourceKind = GraphValueSourceKind.Self,
                DisplayText = "Part"
            }
        };

        var summary = NodeReadableSummaryService.BuildParameterSummary(parameter, null);

        Assert.Equal("Self", summary);
    }

    [Theory]
    [InlineData("ACT_MoveObject", "positionMode", "Set", "(Set) Move Object")]
    [InlineData("ACT_MoveObject", "positionMode", "Add", "(Add) Move Object")]
    [InlineData("ACT_RotateObject", "rotationMode", "Set", "(Set) Rotate Object")]
    [InlineData("ACT_RotateObject", "rotationMode", "Add", "(Add) Rotate Object")]
    [InlineData("ACT_RotateObject", "rotationMode", "Spin", "(Spin) Rotate Object")]
    [InlineData("ACT_SetObjectScale", "", "", "(Set) Scale Object")]
    public void BuildNodeDisplayName_ShowsFusedTransformVariant(
        string catalogId,
        string parameterKey,
        string value,
        string expected)
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var entry = catalog.Nodes.Single(node => node.IdBase == catalogId);
        var node = NodeCatalogService.CreateNode(entry);
        if (!string.IsNullOrWhiteSpace(parameterKey))
        {
            var parameter = node.Parameters.Single(item => item.Key == parameterKey);
            parameter.Value = value;
            parameter.Binding.ConstantValue = value;
            parameter.Binding.DisplayText = value;
        }

        var displayName = NodeReadableSummaryService.BuildNodeDisplayName(node);

        Assert.Equal(expected, displayName);
    }
}
