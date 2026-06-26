using Vrs.Graph.Model;
using Vrs.Graph.Modeling;
using Vrs.Graph.Theming;

namespace Vrs.Tests;

public sealed class GraphArchitectureTests
{
    [Fact]
    public void VrsGraph_HasNoContainerOrCoreReferences()
    {
        var references = typeof(RuleGraph).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Avalonia", references);
        Assert.DoesNotContain("Avalonia.Base", references);
        Assert.DoesNotContain("Vrs.Core", references);
        Assert.DoesNotContain("Vrs.App", references);
    }

    [Fact]
    public void DefaultTheme_UsesRequestedTriggerActionConditionPalette()
    {
        var theme = GraphTheme.Default;

        Assert.Equal("Trigger", theme.StyleFor(NodeKind.Trigger).DisplayName);
        Assert.Equal("#d7a72f", theme.StyleFor(NodeKind.Trigger).AccentHex);
        Assert.Equal("#2f8fd7", theme.StyleFor(NodeKind.Action).AccentHex);
        Assert.Equal("#30a66a", theme.StyleFor(NodeKind.Condition).AccentHex);
    }

    [Fact]
    public void DefaultPorts_PreserveNodeFamilyFlowContracts()
    {
        var triggerPorts = GraphPortDefaults.CreateDefaultPorts(NodeKind.Trigger);
        Assert.Single(triggerPorts);
        Assert.Equal(NodePortDirection.Output, triggerPorts.Single().Direction);
        Assert.Equal(GraphPortDefaults.FlowOut, triggerPorts.Single().Id);

        var actionPorts = GraphPortDefaults.CreateDefaultPorts(NodeKind.Action);
        Assert.Contains(actionPorts, port => port.Direction == NodePortDirection.Input && port.Id == GraphPortDefaults.FlowIn);
        Assert.Contains(actionPorts, port => port.Direction == NodePortDirection.Output && port.Id == GraphPortDefaults.FlowOut);

        var conditionPorts = GraphPortDefaults.CreateDefaultPorts(NodeKind.Condition);
        Assert.Contains(conditionPorts, port => port.Direction == NodePortDirection.Input && port.Id == GraphPortDefaults.FlowIn);
        Assert.Contains(conditionPorts, port => port.Direction == NodePortDirection.Output && port.Id == GraphPortDefaults.TrueOut);
        Assert.Contains(conditionPorts, port => port.Direction == NodePortDirection.Output && port.Id == GraphPortDefaults.FalseOut);

        var propertyPorts = GraphPortDefaults.CreateDefaultPorts(NodeKind.Property);
        Assert.Single(propertyPorts);
        Assert.Equal(NodePortKind.Value, propertyPorts.Single().PortKind);
        Assert.Equal(GraphPortDefaults.ValueOut, propertyPorts.Single().Id);
    }

    [Fact]
    public void GraphColor_InterpolatesWireGradientColors()
    {
        Assert.Equal("#839B83", GraphColor.InterpolateHex("#d7a72f", "#2f8fd7", 0.5));
        Assert.Equal("#309BA1", GraphColor.InterpolateHex("#2f8fd7", "#30a66a", 0.5));
    }
}
