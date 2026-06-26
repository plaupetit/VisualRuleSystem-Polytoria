using Vrs.Core.Catalog;
using Vrs.Core.Persistence;
using Vrs.Core.Samples;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.Tests;

public sealed class RuleGraphEditServiceTests
{
    [Fact]
    public void AddAndRemoveConnection_ValidTriggerToAction()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        rule.Connections.Clear();
        var trigger = rule.Nodes.Single(node => node.Kind == NodeKind.Trigger);
        var action = rule.Nodes.Single(node => node.Kind == NodeKind.Action);

        var service = new RuleGraphEditService();
        var result = service.AddConnection(rule, trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn);

        Assert.True(result.Success);
        Assert.Single(rule.Connections);

        var remove = service.RemoveConnection(rule, 0);

        Assert.True(remove.Success);
        Assert.Empty(rule.Connections);
    }

    [Fact]
    public void WireReroute_AddMoveDirectionAndConnectionCleanup_UpdateVisualMetadataOnly()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        var service = new RuleGraphEditService();

        var add = service.AddWireReroute(rule, 0, 320, 180);
        var reroute = Assert.Single(rule.WireReroutes);
        var attachedRerouteId = Assert.Single(rule.Connections.Single().RerouteIds);
        var directions = service.SetWireRerouteDirections(rule, reroute.Id, WireRerouteDirection.Up, WireRerouteDirection.Down);
        var move = service.MoveWireReroute(rule, reroute.Id, 360, 220);
        var removeConnection = service.RemoveConnection(rule, 0);

        Assert.True(add.Success);
        Assert.Equal(reroute.Id, attachedRerouteId);
        Assert.True(directions.Success);
        Assert.True(move.Success);
        Assert.Equal(360, reroute.GraphX);
        Assert.Equal(220, reroute.GraphY);
        Assert.Equal(WireRerouteDirection.Up, reroute.InputDirection);
        Assert.Equal(WireRerouteDirection.Down, reroute.OutputDirection);
        Assert.True(removeConnection.Success);
        Assert.Empty(rule.WireReroutes);
    }

    [Fact]
    public void AddConnection_RejectsInvalidEndpoints()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        var service = new RuleGraphEditService();
        var action = rule.Nodes.Single(node => node.Kind == NodeKind.Action);
        var trigger = rule.Nodes.Single(node => node.Kind == NodeKind.Trigger);

        Assert.False(service.AddConnection(rule, action.Id, GraphPortDefaults.FlowOut, trigger.Id, GraphPortDefaults.FlowIn).Success);
        Assert.False(service.AddConnection(rule, action.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn).Success);
        Assert.False(service.AddConnection(rule, trigger.Id, "missing", action.Id, GraphPortDefaults.FlowIn).Success);
    }

    [Fact]
    public void ConditionTrueFalsePorts_CanConnectToActions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var conditionEntry = catalog.Nodes.Single(node => node.IdBase == "COND_ValueEquals");
        var actionEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var condition = NodeCatalogService.CreateNode(conditionEntry, 100, 100, "COND_Test");
        var trueAction = NodeCatalogService.CreateNode(actionEntry, 360, 80, "ACT_True");
        var falseAction = NodeCatalogService.CreateNode(actionEntry, 360, 180, "ACT_False");
        var rule = new Rule
        {
            Id = "RULE_Condition",
            Name = "Condition",
            Nodes = [condition, trueAction, falseAction]
        };

        var service = new RuleGraphEditService();
        var trueResult = service.AddConnection(rule, condition.Id, GraphPortDefaults.TrueOut, trueAction.Id, GraphPortDefaults.FlowIn);
        var falseResult = service.AddConnection(rule, condition.Id, GraphPortDefaults.FalseOut, falseAction.Id, GraphPortDefaults.FlowIn);

        Assert.True(trueResult.Success);
        Assert.True(falseResult.Success);
        Assert.Equal(2, rule.Connections.Count);
    }

    [Fact]
    public void PropertyValuePort_CanConnectToActionParameterPort()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var textEntry = catalog.Nodes.Single(node => node.IdBase == "PROP_ManualText");
        var actionEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var text = NodeCatalogService.CreateNode(textEntry, 100, 100, "PROP_Text");
        var action = NodeCatalogService.CreateNode(actionEntry, 360, 100, "ACT_Message");
        action.Ports.Add(GraphPortDefaults.CreateInput(
            GraphPortDefaults.ParameterPortId("message"),
            "Message",
            NodePortKind.Value,
            "String",
            "#c084fc",
            10));
        var rule = new Rule
        {
            Id = "RULE_ValueWire",
            Name = "ValueWire",
            Nodes = [text, action]
        };

        var service = new RuleGraphEditService();
        var result = service.AddConnection(rule, text.Id, GraphPortDefaults.ValueOut, action.Id, GraphPortDefaults.ParameterPortId("message"));

        Assert.True(result.Success);
        Assert.Single(rule.Connections);
        Assert.Equal(GraphConnectionKind.Value, rule.Connections.Single().ConnectionKind);
    }

    [Fact]
    public void MoveNodeAndRoundTrip_PreservesPositionPortsAndConnections()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        var action = rule.Nodes.Single(node => node.Kind == NodeKind.Action);
        var service = new RuleGraphEditService();

        var move = service.MoveNode(rule, action.Id, 700, 320);
        var json = RuleGraphJson.Serialize(graph);
        var restored = RuleGraphJson.Deserialize(json);
        var restoredAction = restored.Rules.Single().Nodes.Single(node => node.Id == action.Id);

        Assert.True(move.Success);
        Assert.Equal(3, restored.Version);
        Assert.Equal(700, restoredAction.GraphX);
        Assert.Equal(320, restoredAction.GraphY);
        Assert.True(restoredAction.GraphPositionSet);
        Assert.NotEmpty(restoredAction.Ports);
        Assert.Equal(rule.Connections.Single().From.NodeId, restored.Rules.Single().Connections.Single().From.NodeId);
    }

    [Fact]
    public void DuplicateNode_CreatesFreshNodeWithPortsAndNoConnections()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        var action = rule.Nodes.Single(node => node.Kind == NodeKind.Action);
        var service = new RuleGraphEditService();

        var result = service.DuplicateNode(rule, action.Id);
        var duplicate = rule.Nodes.Single(node => node.Id.StartsWith($"{action.Id}_Copy", StringComparison.Ordinal));

        Assert.True(result.Success);
        Assert.Equal(action.Ports.Count, duplicate.Ports.Count);
        Assert.Equal(rule.Connections.Count, rule.Connections.Count(connection => connection.From.NodeId != duplicate.Id && connection.To.NodeId != duplicate.Id));
    }

    [Fact]
    public void CreateFragmentFromSelection_AssignsNodesAndCanExpand()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        var service = new RuleGraphEditService();

        var create = service.CreateFragmentFromSelection(rule, rule.Nodes.Select(node => node.Id), GraphFragmentKind.State, "Gameplay State");
        var fragment = rule.Fragments.Single();
        var expand = service.SetFragmentCollapsed(rule, fragment.Id, collapsed: false);

        Assert.True(create.Success);
        Assert.Equal(GraphFragmentKind.State, fragment.Kind);
        Assert.Equal(rule.Nodes.Count, fragment.NodeIds.Count);
        Assert.All(rule.Nodes, node => Assert.Equal(fragment.Id, node.FragmentId));
        Assert.True(expand.Success);
        Assert.False(fragment.Collapsed);
    }

    [Fact]
    public void CreateGroupFromSelection_ComputesBoundsAndClaimsNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        var service = new RuleGraphEditService();

        var result = service.CreateGroupFromSelection(rule, rule.Nodes.Select(node => node.Id), "Main Flow", "Amber");
        var group = Assert.Single(rule.NodeGroups);

        Assert.True(result.Success);
        Assert.Equal("Main Flow", group.Name);
        Assert.Equal("Amber", group.Color);
        Assert.Equal(rule.Nodes.Count, group.MemberNodeIds.Count);
        Assert.True(group.GraphX < rule.Nodes.Min(node => node.GraphX));
        Assert.True(group.GraphY < rule.Nodes.Min(node => node.GraphY));
        Assert.True(group.Width > RuleGraphGeometryService.NodeWidth);
        Assert.True(group.Height > RuleGraphGeometryService.NodeHeight);
    }

    [Fact]
    public void CreateGroupFromSelection_CanClaimWireReroutes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        var service = new RuleGraphEditService();
        service.AddWireReroute(rule, 0, 320, 180);
        var reroute = rule.WireReroutes.Single();

        var result = service.CreateGroupFromSelection(rule, [], [reroute.Id], "Wire Group", "Blue");
        var group = Assert.Single(rule.NodeGroups);

        Assert.True(result.Success);
        Assert.Empty(group.MemberNodeIds);
        Assert.Contains(reroute.Id, group.MemberRerouteIds);
        Assert.True(group.GraphX < reroute.GraphX);
        Assert.True(group.GraphY < reroute.GraphY);
    }

    [Fact]
    public void CreateEmptyGroup_UsesRequestedPositionAndHasNoMembers()
    {
        var rule = new Rule { Id = "RULE_Empty", Name = "Empty" };
        var service = new RuleGraphEditService();

        var result = service.CreateEmptyGroup(rule, 240, 160, "Layout Zone", "Purple");
        var group = Assert.Single(rule.NodeGroups);

        Assert.True(result.Success);
        Assert.Equal("Layout Zone", group.Name);
        Assert.Equal("Purple", group.Color);
        Assert.Equal(240, group.GraphX);
        Assert.Equal(160, group.GraphY);
        Assert.Empty(group.MemberNodeIds);
        Assert.Empty(group.MemberRerouteIds);
    }

    [Fact]
    public void MoveNode_GroupedNodeCanLeaveGroupAndRemainingContentsAutoFit()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        var service = new RuleGraphEditService();
        var moving = rule.Nodes.First();
        var staying = rule.Nodes.Last();
        service.CreateGroupFromSelection(rule, [moving.Id, staying.Id], "Flexible Group");
        var group = rule.NodeGroups.Single();
        var targetX = group.GraphX - 500;
        var targetY = group.GraphY - 500;

        var move = service.MoveNode(rule, moving.Id, targetX, targetY);

        Assert.True(move.Success);
        Assert.Equal(targetX, moving.GraphX);
        Assert.Equal(targetY, moving.GraphY);
        Assert.DoesNotContain(moving.Id, group.MemberNodeIds);
        Assert.Contains(staying.Id, group.MemberNodeIds);
        Assert.True(group.GraphX < staying.GraphX);
        Assert.True(group.GraphY < staying.GraphY);
    }

    [Fact]
    public void MoveNode_AddsUngroupedNodeDroppedInsideGroup()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        var service = new RuleGraphEditService();
        var grouped = rule.Nodes.First();
        var ungrouped = rule.Nodes.Last();
        service.CreateGroupFromSelection(rule, [grouped.Id], "Drop Target");
        var group = rule.NodeGroups.Single();

        var move = service.MoveNode(rule, ungrouped.Id, group.GraphX + 30, group.GraphY + 50);

        Assert.True(move.Success);
        Assert.Contains(ungrouped.Id, group.MemberNodeIds);
    }

    [Fact]
    public void MoveWireReroute_GroupedRerouteCanLeaveGroup()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        var service = new RuleGraphEditService();
        service.AddWireReroute(rule, 0, 320, 180);
        var reroute = rule.WireReroutes.Single();
        service.CreateGroupFromSelection(rule, [], [reroute.Id], "Wire Group");
        var group = rule.NodeGroups.Single();
        var targetX = group.GraphX - 500;
        var targetY = group.GraphY - 500;

        var move = service.MoveWireReroute(rule, reroute.Id, targetX, targetY);

        Assert.True(move.Success);
        Assert.Equal(targetX, reroute.GraphX);
        Assert.Equal(targetY, reroute.GraphY);
        Assert.DoesNotContain(reroute.Id, group.MemberRerouteIds);
    }

    [Fact]
    public void MoveWireReroute_UngroupedRerouteDroppedInsideGroupIsClaimed()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        var service = new RuleGraphEditService();
        var node = rule.Nodes.First();
        service.CreateGroupFromSelection(rule, [node.Id], "Wire Target");
        var group = rule.NodeGroups.Single();
        service.AddWireReroute(rule, 0, group.GraphX - 240, group.GraphY - 120);
        var reroute = rule.WireReroutes.Single();

        var move = service.MoveWireReroute(rule, reroute.Id, group.GraphX + 60, group.GraphY + 70);

        Assert.True(move.Success);
        Assert.Contains(reroute.Id, group.MemberRerouteIds);
    }

    [Fact]
    public void MoveResizeRenameAndColorGroup_UpdateOnlyVisualMetadata()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        var service = new RuleGraphEditService();
        service.CreateGroupFromSelection(rule, rule.Nodes.Select(node => node.Id), "Main Flow");
        var group = rule.NodeGroups.Single();
        var trigger = rule.Nodes.Single(node => node.Kind == NodeKind.Trigger);

        rule.WireReroutes.Add(new RuleWireReroute { Id = "REROUTE_Grouped", GraphX = 250, GraphY = 160 });
        group.MemberRerouteIds.Add("REROUTE_Grouped");

        var move = service.MoveGroup(
            rule,
            group.Id,
            100,
            200,
            [new GraphNodeMove(trigger.Id, 320, 240)],
            [new GraphWireRerouteMove("REROUTE_Grouped", 340, 260)]);
        var resize = service.ResizeGroup(rule, group.Id, 420, 260);
        var rename = service.RenameGroup(rule, group.Id, "Timer Group");
        var color = service.SetGroupColor(rule, group.Id, "Rose");

        Assert.True(move.Success);
        Assert.True(resize.Success);
        Assert.True(rename.Success);
        Assert.True(color.Success);
        Assert.Equal(100, group.GraphX);
        Assert.Equal(200, group.GraphY);
        Assert.Equal(420, group.Width);
        Assert.Equal(260, group.Height);
        Assert.Equal("Timer Group", group.Name);
        Assert.Equal("Rose", group.Color);
        Assert.Equal(320, trigger.GraphX);
        Assert.Equal(240, trigger.GraphY);
        Assert.Equal(340, rule.WireReroutes.Single().GraphX);
        Assert.Equal(260, rule.WireReroutes.Single().GraphY);
    }

    [Fact]
    public void Clipboard_CopyPasteNode_PreservesParametersAndBinding()
    {
        var rule = new Rule
        {
            Nodes =
            [
                new RuleNode
                {
                    Id = "ACT_KillPlayer",
                    Kind = NodeKind.Action,
                    Label = "Kill Player",
                    GraphX = 100,
                    GraphY = 200,
                    Parameters =
                    [
                        new RuleParameter
                        {
                            Key = "player",
                            Value = "Triggering Player",
                            ValueSource = "Triggering Player",
                            Binding = new GraphValueBinding
                            {
                                SourceKind = GraphValueSourceKind.TriggeringPlayer,
                                DataType = "Player",
                                DisplayText = "Triggering Player"
                            }
                        }
                    ]
                }
            ]
        };
        var clipboard = new RuleGraphClipboardService();

        var copy = clipboard.Copy(rule, new GraphClipboardSelection(["ACT_KillPlayer"]));
        var paste = clipboard.Paste(rule, 400, 500);
        var pasted = rule.Nodes.Single(node => node.Id != "ACT_KillPlayer");
        var pastedParameter = pasted.Parameters.Single(parameter => parameter.Key == "player");

        Assert.True(copy.Success);
        Assert.True(paste.Success);
        Assert.NotEqual("ACT_KillPlayer", pasted.Id);
        Assert.Equal("Kill Player", pasted.Label);
        Assert.Equal(GraphValueSourceKind.TriggeringPlayer, pastedParameter.Binding.SourceKind);
        Assert.Equal("Triggering Player", pastedParameter.Binding.DisplayText);
    }

    [Fact]
    public void Clipboard_CopyPasteConnectedNodes_RemapsConnectionAndReroute()
    {
        var rule = new Rule
        {
            Nodes =
            [
                new RuleNode { Id = "TRG_Start", Kind = NodeKind.Trigger, Label = "Start", GraphX = 100, GraphY = 100 },
                new RuleNode { Id = "ACT_Show", Kind = NodeKind.Action, Label = "Show", GraphX = 460, GraphY = 100 }
            ],
            Connections =
            [
                new GraphConnection
                {
                    Id = "CONN_Start_Show",
                    From = new GraphEndpoint { NodeId = "TRG_Start", PortId = GraphPortDefaults.FlowOut },
                    To = new GraphEndpoint { NodeId = "ACT_Show", PortId = GraphPortDefaults.FlowIn },
                    ConnectionKind = GraphConnectionKind.Flow,
                    RerouteIds = ["WIRE_1"]
                }
            ],
            WireReroutes =
            [
                new RuleWireReroute { Id = "WIRE_1", GraphX = 300, GraphY = 120 }
            ]
        };
        var clipboard = new RuleGraphClipboardService();

        clipboard.Copy(rule, new GraphClipboardSelection(["TRG_Start", "ACT_Show"]));
        var paste = clipboard.Paste(rule, 800, 600);
        var pastedConnection = rule.Connections.Single(connection => connection.Id != "CONN_Start_Show");
        var pastedReroute = rule.WireReroutes.Single(reroute => reroute.Id != "WIRE_1");

        Assert.True(paste.Success);
        Assert.Equal(4, rule.Nodes.Count);
        Assert.Contains(pastedConnection.From.NodeId, paste.NodeIds);
        Assert.Contains(pastedConnection.To.NodeId, paste.NodeIds);
        Assert.Equal(pastedReroute.Id, Assert.Single(pastedConnection.RerouteIds));
        Assert.DoesNotContain("WIRE_1", pastedConnection.RerouteIds);
    }

    [Fact]
    public void Clipboard_CopyPasteSingleNode_DoesNotCopyExternalConnection()
    {
        var rule = new Rule
        {
            Nodes =
            [
                new RuleNode { Id = "TRG_Start", Kind = NodeKind.Trigger, Label = "Start" },
                new RuleNode { Id = "ACT_Show", Kind = NodeKind.Action, Label = "Show" }
            ],
            Connections =
            [
                new GraphConnection
                {
                    Id = "CONN_External",
                    From = new GraphEndpoint { NodeId = "TRG_Start", PortId = GraphPortDefaults.FlowOut },
                    To = new GraphEndpoint { NodeId = "ACT_Show", PortId = GraphPortDefaults.FlowIn },
                    ConnectionKind = GraphConnectionKind.Flow
                }
            ]
        };
        var clipboard = new RuleGraphClipboardService();

        clipboard.Copy(rule, new GraphClipboardSelection(["TRG_Start"]));
        clipboard.Paste(rule, 200, 200);

        Assert.Equal(3, rule.Nodes.Count);
        Assert.Single(rule.Connections);
        Assert.Equal("CONN_External", rule.Connections.Single().Id);
    }

    [Fact]
    public void Clipboard_CopyPasteGroup_RemapsMembers()
    {
        var rule = new Rule
        {
            Nodes =
            [
                new RuleNode { Id = "TRG_Start", Kind = NodeKind.Trigger, GraphX = 100, GraphY = 100 },
                new RuleNode { Id = "ACT_Show", Kind = NodeKind.Action, GraphX = 460, GraphY = 100 }
            ],
            WireReroutes =
            [
                new RuleWireReroute { Id = "WIRE_1", GraphX = 300, GraphY = 140 }
            ],
            NodeGroups =
            [
                new RuleNodeGroup
                {
                    Id = "GROUP_Main",
                    Name = "Main",
                    MemberNodeIds = ["TRG_Start", "ACT_Show"],
                    MemberRerouteIds = ["WIRE_1"],
                    GraphX = 60,
                    GraphY = 60,
                    Width = 600,
                    Height = 220
                }
            ]
        };
        var clipboard = new RuleGraphClipboardService();

        clipboard.Copy(rule, new GraphClipboardSelection([], GroupId: "GROUP_Main"));
        var paste = clipboard.Paste(rule, 900, 400);
        var pastedGroup = rule.NodeGroups.Single(group => group.Id != "GROUP_Main");

        Assert.True(paste.Success);
        Assert.Equal(2, pastedGroup.MemberNodeIds.Count);
        Assert.All(pastedGroup.MemberNodeIds, id => Assert.Contains(id, paste.NodeIds));
        Assert.DoesNotContain("TRG_Start", pastedGroup.MemberNodeIds);
        Assert.DoesNotContain("ACT_Show", pastedGroup.MemberNodeIds);
        Assert.DoesNotContain("WIRE_1", pastedGroup.MemberRerouteIds);
        Assert.Single(pastedGroup.MemberRerouteIds);
    }

    [Fact]
    public void GroupParenting_PreventsCyclesAndRemoveReparentsChildren()
    {
        var rule = new Rule
        {
            Nodes =
            [
                new RuleNode { Id = "TRIG_Start", Kind = NodeKind.Trigger, GraphX = 100, GraphY = 100 },
                new RuleNode { Id = "ACT_Show", Kind = NodeKind.Action, GraphX = 480, GraphY = 120 }
            ]
        };
        var service = new RuleGraphEditService();
        service.CreateGroupFromSelection(rule, ["TRIG_Start"], "Parent");
        service.CreateGroupFromSelection(rule, ["ACT_Show"], "Child");
        var parent = rule.NodeGroups[0];
        var child = rule.NodeGroups[1];

        var setParent = service.SetGroupParent(rule, child.Id, parent.Id);
        var cycle = service.SetGroupParent(rule, parent.Id, child.Id);
        var removeParent = service.RemoveGroup(rule, parent.Id);

        Assert.True(setParent.Success);
        Assert.False(cycle.Success);
        Assert.True(removeParent.Success);
        Assert.Single(rule.NodeGroups);
        Assert.Equal("", child.ParentGroupId);
        Assert.Contains("ACT_Show", child.MemberNodeIds);
    }

    [Fact]
    public void AutoParentGroupByBounds_CanNestAndUnnestDraggedGroups()
    {
        var rule = new Rule
        {
            Nodes =
            [
                new RuleNode { Id = "TRIG_Start", Kind = NodeKind.Trigger, GraphX = 120, GraphY = 130 },
                new RuleNode { Id = "ACT_Show", Kind = NodeKind.Action, GraphX = 520, GraphY = 130 }
            ]
        };
        var service = new RuleGraphEditService();
        service.CreateEmptyGroup(rule, 40, 40, "Parent");
        service.CreateGroupFromSelection(rule, ["ACT_Show"], "Child");
        var parent = rule.NodeGroups.Single(group => group.Name == "Parent");
        var child = rule.NodeGroups.Single(group => group.Name == "Child");

        service.MoveGroup(rule, child.Id, parent.GraphX + 60, parent.GraphY + 70);
        var nest = service.AutoParentGroupByBounds(rule, child.Id);
        Assert.Equal(parent.Id, child.ParentGroupId);

        service.MoveGroup(rule, child.Id, parent.GraphX + parent.Width + 360, parent.GraphY + parent.Height + 360);
        var unnest = service.AutoParentGroupByBounds(rule, child.Id);

        Assert.True(nest.Success);
        Assert.True(unnest.Success);
        Assert.Equal("", child.ParentGroupId);
    }

    [Fact]
    public void RemoveNode_CleansGroupMembership()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        var service = new RuleGraphEditService();
        service.CreateGroupFromSelection(rule, rule.Nodes.Select(node => node.Id), "Main Flow");
        var removedNodeId = rule.Nodes[0].Id;

        var remove = service.RemoveNode(rule, removedNodeId);

        Assert.True(remove.Success);
        Assert.DoesNotContain(rule.NodeGroups.Single().MemberNodeIds, id => id == removedNodeId);
    }

    [Fact]
    public void AddNode_DoesNotAutoConnectWhenGraphHasNoConnections()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var timer = catalog.Nodes.Single(node => node.IdBase == "EV_OnTimerTick");
        var message = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var rule = new Rule
        {
            Id = "RULE_Auto",
            Name = "Auto"
        };

        var service = new RuleGraphEditService();
        var trigger = NodeCatalogService.CreateNode(timer, 100, 100, "TRG_Auto");
        var action = NodeCatalogService.CreateNode(message, 420, 100, "ACT_Auto");

        Assert.True(service.AddNode(rule, trigger).Success);
        Assert.True(service.AddNode(rule, action).Success);
        Assert.Empty(rule.Connections);
    }

    [Fact]
    public void CreateNode_PreservesFlowPortsAndAddsAdvancedParameterPorts()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var timer = catalog.Nodes.Single(node => node.IdBase == "EV_OnTimerTick");
        var message = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var conditionEntry = catalog.Nodes.Single(node => node.IdBase == "COND_ValueEquals");

        var trigger = NodeCatalogService.CreateNode(timer);
        var action = NodeCatalogService.CreateNode(message);
        var condition = NodeCatalogService.CreateNode(conditionEntry);

        Assert.Contains(trigger.Ports, port =>
            port.Id == GraphPortDefaults.FlowOut &&
            port.Direction == NodePortDirection.Output &&
            port.ColorHex == GraphPortDefaults.TriggerFlowColor);
        Assert.Contains(trigger.Ports, port =>
            port.Id == GraphPortDefaults.ParameterPortId("interval") &&
            port.PortKind == NodePortKind.Value &&
            port.DataType == "Number");

        Assert.Contains(action.Ports, port =>
            port.Id == GraphPortDefaults.FlowIn &&
            port.Direction == NodePortDirection.Input &&
            port.ColorHex == GraphPortDefaults.ActionFlowColor);
        Assert.Contains(action.Ports, port =>
            port.Id == GraphPortDefaults.FlowOut &&
            port.Direction == NodePortDirection.Output &&
            port.ColorHex == GraphPortDefaults.ActionFlowColor);
        Assert.Contains(action.Ports, port =>
            port.Id == GraphPortDefaults.ParameterPortId("message") &&
            port.PortKind == NodePortKind.Value &&
            port.DataType == "String");

        Assert.Contains(condition.Ports, port =>
            port.Id == GraphPortDefaults.FlowIn &&
            port.Direction == NodePortDirection.Input &&
            port.ColorHex == GraphPortDefaults.ConditionFlowColor);
        Assert.Contains(condition.Ports, port =>
            port.Id == GraphPortDefaults.TrueOut &&
            port.Direction == NodePortDirection.Output &&
            port.ColorHex == GraphPortDefaults.ConditionFlowColor);
        Assert.Contains(condition.Ports, port =>
            port.Id == GraphPortDefaults.FalseOut &&
            port.Direction == NodePortDirection.Output &&
            port.ColorHex == GraphPortDefaults.ConditionFalseColor);
        Assert.Contains(condition.Ports, port =>
            port.Id == GraphPortDefaults.ParameterPortId("left") &&
            port.PortKind == NodePortKind.Value &&
            port.DataType == "String");
    }
}
