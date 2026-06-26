using Vrs.Core.Catalog;
using Vrs.Core.Samples;
using Vrs.Core.Validation;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.Tests;

public sealed class ValidationTests
{
    [Fact]
    public void Validate_SampleGraph_HasNoErrors()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);

        var result = new RuleGraphValidator().Validate(graph, catalog.Nodes);

        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public void Validate_MissingTriggerAndRequiredParameter_ReportsErrors()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var actionEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var action = NodeCatalogService.CreateNode(actionEntry, stableId: "ACT_BrokenMessage");
        action.Parameters.Single(parameter => parameter.Key == "message").Value = "";

        var graph = new RuleGraph
        {
            Name = "BrokenGraph",
            Rules =
            [
                new Rule
                {
                    Id = "RULE_Broken",
                    Name = "BrokenRule",
                    Nodes = [action]
                }
            ]
        };

        var result = new RuleGraphValidator().Validate(graph, catalog.Nodes);

        Assert.True(result.ErrorCount >= 2);
        Assert.Contains(result.Messages, message => message.Message.Contains("no enabled Trigger", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Messages, message => message.Message.Contains("Required parameter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RequiredParameterSuppliedByValueWire_HasNoRequiredParameterError()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var timer = catalog.Nodes.Single(node => node.IdBase == "EV_OnTimerTick");
        var textEntry = catalog.Nodes.Single(node => node.IdBase == "PROP_ManualText");
        var actionEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(timer, 80, 100, "TRG_Timer");
        var text = NodeCatalogService.CreateNode(textEntry, 320, 80, "PROP_Text");
        var action = NodeCatalogService.CreateNode(actionEntry, 560, 100, "ACT_Message");
        action.Ports.Add(GraphPortDefaults.CreateInput(
            GraphPortDefaults.ParameterPortId("message"),
            "Message",
            NodePortKind.Value,
            "String",
            "#c084fc",
            10));
        action.Parameters.Single(parameter => parameter.Key == "message").Value = "";

        var graph = new RuleGraph
        {
            Name = "ValidatedValueWireGraph",
            Rules =
            [
                new Rule
                {
                    Id = "RULE_ValidatedValueWire",
                    Name = "ValidatedValueWire",
                    Nodes = [trigger, text, action],
                    Connections =
                    [
                        new GraphConnection
                        {
                            Id = "CONN_Timer_Message",
                            From = new GraphEndpoint { NodeId = trigger.Id, PortId = GraphPortDefaults.FlowOut },
                            To = new GraphEndpoint { NodeId = action.Id, PortId = GraphPortDefaults.FlowIn },
                            ConnectionKind = GraphConnectionKind.Flow
                        },
                        new GraphConnection
                        {
                            Id = "CONN_Text_Message",
                            From = new GraphEndpoint { NodeId = text.Id, PortId = GraphPortDefaults.ValueOut },
                            To = new GraphEndpoint { NodeId = action.Id, PortId = GraphPortDefaults.ParameterPortId("message") },
                            ConnectionKind = GraphConnectionKind.Value
                        }
                    ]
                }
            ]
        };

        var result = new RuleGraphValidator().Validate(graph, catalog.Nodes);

        Assert.DoesNotContain(result.Messages, message => message.Severity == ValidationSeverity.Error && message.Message.Contains("Required parameter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_EnabledTriggerWithoutFlow_ReportsWarning()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var timer = catalog.Nodes.Single(node => node.IdBase == "EV_OnTimerTick");
        var actionEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(timer, 80, 100, "TRG_Timer");
        var action = NodeCatalogService.CreateNode(actionEntry, 320, 100, "ACT_Message");

        var graph = new RuleGraph
        {
            Name = "DisconnectedTriggerGraph",
            Rules =
            [
                new Rule
                {
                    Id = "RULE_DisconnectedTrigger",
                    Name = "DisconnectedTrigger",
                    Nodes = [trigger, action]
                }
            ]
        };

        var result = new RuleGraphValidator().Validate(graph, catalog.Nodes);

        Assert.Contains(result.Messages, message =>
            message.Severity == ValidationSeverity.Warning &&
            message.Message.Contains("no connected flow", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MultipleConnectedTriggers_DoNotReportTriggerFlowWarning()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var touchEntry = catalog.Nodes.Single(node => node.IdBase == "EV_OnPlayerTouchedObject");
        var actionEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_KillPlayer");
        var firstTrigger = NodeCatalogService.CreateNode(touchEntry, 80, 80, "TRG_TouchA");
        var secondTrigger = NodeCatalogService.CreateNode(touchEntry, 80, 220, "TRG_TouchB");
        var action = NodeCatalogService.CreateNode(actionEntry, 360, 140, "ACT_Kill");

        var graph = new RuleGraph
        {
            Name = "MultiTriggerGraph",
            Rules =
            [
                new Rule
                {
                    Id = "RULE_MultiTrigger",
                    Name = "MultiTrigger",
                    Nodes = [firstTrigger, secondTrigger, action],
                    Connections =
                    [
                        new GraphConnection
                        {
                            Id = "CONN_TouchA_Kill",
                            From = new GraphEndpoint { NodeId = firstTrigger.Id, PortId = GraphPortDefaults.FlowOut },
                            To = new GraphEndpoint { NodeId = action.Id, PortId = GraphPortDefaults.FlowIn },
                            ConnectionKind = GraphConnectionKind.Flow
                        },
                        new GraphConnection
                        {
                            Id = "CONN_TouchB_Kill",
                            From = new GraphEndpoint { NodeId = secondTrigger.Id, PortId = GraphPortDefaults.FlowOut },
                            To = new GraphEndpoint { NodeId = action.Id, PortId = GraphPortDefaults.FlowIn },
                            ConnectionKind = GraphConnectionKind.Flow
                        }
                    ]
                }
            ]
        };

        var result = new RuleGraphValidator().Validate(graph, catalog.Nodes);

        Assert.DoesNotContain(result.Messages, message =>
            message.Message.Contains("no connected flow", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_UnusedPropertyNode_ReportsRecipeGuidanceWarning()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var timer = catalog.Nodes.Single(node => node.IdBase == "EV_OnTimerTick");
        var addEntry = catalog.Nodes.Single(node => node.IdBase == "PROP_AddNumbers");
        var trigger = NodeCatalogService.CreateNode(timer, 80, 100, "TRG_Timer");
        var add = NodeCatalogService.CreateNode(addEntry, 320, 80, "PROP_Add");
        var graph = new RuleGraph
        {
            Name = "UnusedPropertyGraph",
            Rules =
            [
                new Rule
                {
                    Id = "RULE_UnusedProperty",
                    Name = "UnusedProperty",
                    Nodes = [trigger, add]
                }
            ]
        };

        var result = new RuleGraphValidator().Validate(graph, catalog.Nodes);

        Assert.Contains(result.Messages, message =>
            message.Severity == ValidationSeverity.Warning &&
            message.Message.Contains("Choose it from a parameter instead", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_IncompatibleTypedSceneObject_ReportsWarning()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var timerEntry = catalog.Nodes.Single(node => node.IdBase == "EV_OnTimerTick");
        var moveEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_SetObjectPosition");
        var trigger = NodeCatalogService.CreateNode(timerEntry, stableId: "TRG_Timer");
        var move = NodeCatalogService.CreateNode(moveEntry, stableId: "ACT_MoveGui");
        SetSceneObject(move, "target", "World/PlayerGUI/MainHud");

        var graph = new RuleGraph
        {
            Name = "TypedSceneObjectValidation",
            SceneObjects =
            [
                new SceneObject { Id = "MainHud", Name = "MainHud", Kind = "ScreenGui", Path = "World/PlayerGUI/MainHud" },
                new SceneObject { Id = "MovingPart", Name = "MovingPart", Kind = "Part", Path = "World/Environment/MovingPart" }
            ],
            Rules =
            [
                new Rule
                {
                    Id = "RULE_TypedSceneObjectValidation",
                    Name = "TypedSceneObjectValidation",
                    Nodes = [trigger, move],
                    Connections =
                    [
                        new GraphConnection
                        {
                            Id = "CONN_Timer_Move",
                            From = new GraphEndpoint { NodeId = trigger.Id, PortId = GraphPortDefaults.FlowOut },
                            To = new GraphEndpoint { NodeId = move.Id, PortId = GraphPortDefaults.FlowIn },
                            ConnectionKind = GraphConnectionKind.Flow
                        }
                    ]
                }
            ]
        };

        var result = new RuleGraphValidator().Validate(graph, catalog.Nodes);

        Assert.Contains(result.Messages, message =>
            message.Severity == ValidationSeverity.Warning &&
            message.Message.Contains("expects Part-like objects", StringComparison.OrdinalIgnoreCase) &&
            message.Message.Contains("ScreenGui", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_StateFragmentWithoutTrigger_ReportsWarning()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var actionEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var action = NodeCatalogService.CreateNode(actionEntry, stableId: "ACT_StateOnly");
        action.FragmentId = "FRAG_State_NoTrigger";
        var graph = new RuleGraph
        {
            Name = "FragmentValidation",
            Rules =
            [
                new Rule
                {
                    Id = "RULE_FragmentValidation",
                    Name = "FragmentValidation",
                    Nodes = [action],
                    Fragments =
                    [
                        new GraphFragment
                        {
                            Id = "FRAG_State_NoTrigger",
                            Name = "No Trigger State",
                            Kind = GraphFragmentKind.State,
                            NodeIds = [action.Id],
                            Collapsed = true
                        }
                    ]
                }
            ]
        };

        var result = new RuleGraphValidator().Validate(graph, catalog.Nodes);

        Assert.Contains(result.Messages, message =>
            message.Severity == ValidationSeverity.Warning &&
            message.Message.Contains("State fragment has no enabled Trigger", StringComparison.OrdinalIgnoreCase));
    }

    private static void SetSceneObject(RuleNode node, string key, string path)
    {
        var parameter = node.Parameters.Single(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        parameter.Value = path;
        parameter.Binding.SourceKind = GraphValueSourceKind.SceneObject;
        parameter.Binding.SceneObjectPath = path;
        parameter.Binding.DisplayText = path;
    }

    [Fact]
    public void Validate_NodeGroups_ReportBrokenReferencesAndCycles()
    {
        var graph = new RuleGraph
        {
            Name = "GroupValidation",
            Rules =
            [
                new Rule
                {
                    Id = "RULE_GroupValidation",
                    Name = "GroupValidation",
                    Nodes =
                    [
                        new RuleNode { Id = "TRIG_Start", Kind = NodeKind.Trigger, Enabled = true },
                        new RuleNode { Id = "ACT_Show", Kind = NodeKind.Action }
                    ],
                    NodeGroups =
                    [
                        new RuleNodeGroup
                        {
                            Id = "GROUP_A",
                            Name = "A",
                            ParentGroupId = "GROUP_B",
                            MemberNodeIds = ["TRIG_Start", "MissingNode"]
                        },
                        new RuleNodeGroup
                        {
                            Id = "GROUP_B",
                            Name = "B",
                            ParentGroupId = "GROUP_A",
                            MemberNodeIds = ["TRIG_Start"]
                        },
                        new RuleNodeGroup
                        {
                            Id = "GROUP_C",
                            Name = "C",
                            ParentGroupId = "MissingGroup"
                        }
                    ]
                }
            ]
        };

        var result = new RuleGraphValidator().Validate(graph);

        Assert.Contains(result.Messages, message => message.Message.Contains("missing node", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Messages, message => message.Message.Contains("missing parent group", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Messages, message => message.Message.Contains("more than one direct group", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Messages, message => message.Message.Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WireReroutes_ReportBrokenReferencesAndInvalidDirections()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        rule.WireReroutes.Add(new RuleWireReroute
        {
            Id = "REROUTE_Broken",
            GraphX = 240,
            GraphY = 140,
            InputDirection = "Diagonal",
            OutputDirection = WireRerouteDirection.Right
        });
        rule.Connections.Single().RerouteIds.Add("MissingReroute");
        rule.NodeGroups.Add(new RuleNodeGroup
        {
            Id = "GROUP_Reroute",
            Name = "Reroute Group",
            MemberRerouteIds = ["MissingReroute"]
        });

        var result = new RuleGraphValidator().Validate(graph, catalog.Nodes);

        Assert.Contains(result.Messages, message => message.Message.Contains("invalid input direction", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Messages, message => message.Message.Contains("missing wire reroute", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Messages, message => message.Message.Contains("not attached", StringComparison.OrdinalIgnoreCase));
    }
}
