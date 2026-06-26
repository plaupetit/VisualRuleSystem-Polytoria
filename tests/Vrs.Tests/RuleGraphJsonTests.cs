using System.Text.Json;
using Vrs.Core.Catalog;
using Vrs.Core.Persistence;
using Vrs.Core.Samples;
using Vrs.Graph.Model;

namespace Vrs.Tests;

public sealed class RuleGraphJsonTests
{
    [Fact]
    public void SerializeDeserialize_PreservesGraphV3Content()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);

        var json = RuleGraphJson.Serialize(graph);
        var restored = RuleGraphJson.Deserialize(json);

        Assert.Equal(3, restored.Version);
        Assert.Equal(graph.Name, restored.Name);
        Assert.Equal(graph.Rules[0].Connections[0].From.NodeId, restored.Rules[0].Connections[0].From.NodeId);
        Assert.Equal(graph.Rules[0].Nodes[1].Parameters[0].Value, restored.Rules[0].Nodes[1].Parameters[0].Value);
        Assert.Equal(GraphValueSourceKind.Constant, restored.Rules[0].Nodes[1].Parameters[0].Binding.SourceKind);
        Assert.NotEmpty(restored.Rules[0].Nodes[0].Ports);
        Assert.Equal(GraphAuthoringMode.PolyCreatorLessDraft, restored.AuthoringMode);
        Assert.Equal(GraphScriptKind.Server, restored.Script.ScriptKind);
        Assert.True(restored.Script.IsScriptKindLocked);
    }

    [Fact]
    public void NormalizeScriptBinding_MigratesEmptyKillPlayerManualPlayerToTriggeringPlayer()
    {
        var graph = new RuleGraph
        {
            Name = "LegacyKillPlayerGraph",
            Rules =
            [
                new Rule
                {
                    Id = "RULE_LegacyKill",
                    Name = "LegacyKill",
                    Nodes =
                    [
                        new RuleNode
                        {
                            Id = "ACT_KillPlayer",
                            Kind = NodeKind.Action,
                            Type = "KillPlayer",
                            Label = "Kill Player",
                            CatalogId = "ACT_KillPlayer",
                            Parameters =
                            [
                                new RuleParameter
                                {
                                    Key = "player",
                                    Value = "",
                                    ValueSource = "Manual Value",
                                    Binding = new GraphValueBinding
                                    {
                                        SourceKind = GraphValueSourceKind.Constant,
                                        ConstantValue = "",
                                        DisplayText = ""
                                    }
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var changed = RuleGraphDocumentNormalizer.NormalizeScriptBinding(graph);
        var player = graph.Rules.Single().Nodes.Single().Parameters.Single();

        Assert.True(changed);
        Assert.Equal(GraphValueSourceKind.TriggeringPlayer, player.Binding.SourceKind);
        Assert.Equal("Triggering Player", player.Value);
        Assert.Equal("Trigger Context", player.ValueSource);
    }

    [Fact]
    public void NormalizeScriptBinding_KeepsNonEmptyKillPlayerManualPlayer()
    {
        var graph = new RuleGraph
        {
            Name = "ManualKillPlayerGraph",
            Rules =
            [
                new Rule
                {
                    Id = "RULE_ManualKill",
                    Name = "ManualKill",
                    Nodes =
                    [
                        new RuleNode
                        {
                            Id = "ACT_KillPlayer",
                            Kind = NodeKind.Action,
                            Type = "KillPlayer",
                            Label = "Kill Player",
                            CatalogId = "ACT_KillPlayer",
                            Parameters =
                            [
                                new RuleParameter
                                {
                                    Key = "player",
                                    Value = "NamedPlayer",
                                    ValueSource = "Manual Value",
                                    Binding = new GraphValueBinding
                                    {
                                        SourceKind = GraphValueSourceKind.Constant,
                                        ConstantValue = "NamedPlayer",
                                        DisplayText = "NamedPlayer"
                                    }
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        RuleGraphDocumentNormalizer.NormalizeScriptBinding(graph);
        var player = graph.Rules.Single().Nodes.Single().Parameters.Single();

        Assert.Equal(GraphValueSourceKind.Constant, player.Binding.SourceKind);
        Assert.Equal("NamedPlayer", player.Value);
    }

    [Fact]
    public void SerializeDeserialize_PreservesGraphFragments()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        rule.Fragments.Add(new GraphFragment
        {
            Id = "FRAG_State_Test",
            Name = "Gameplay State",
            Kind = GraphFragmentKind.State,
            NodeIds = rule.Nodes.Select(node => node.Id).ToList(),
            ConnectionIds = rule.Connections.Select(connection => connection.Id).ToList(),
            Collapsed = true,
            GraphX = 42,
            GraphY = 84,
            Comment = "Readable authoring state."
        });
        foreach (var node in rule.Nodes)
        {
            node.FragmentId = "FRAG_State_Test";
        }

        var restored = RuleGraphJson.Deserialize(RuleGraphJson.Serialize(graph));
        var fragment = restored.Rules.Single().Fragments.Single();

        Assert.Equal(GraphFragmentKind.State, fragment.Kind);
        Assert.True(fragment.Collapsed);
        Assert.Equal(42, fragment.GraphX);
        Assert.Equal(rule.Nodes.Count, fragment.NodeIds.Count);
        Assert.All(restored.Rules.Single().Nodes, node => Assert.Equal("FRAG_State_Test", node.FragmentId));
    }

    [Fact]
    public void SerializeDeserialize_PreservesNestedNodeGroups()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        var nodeIds = rule.Nodes.Select(node => node.Id).ToList();
        rule.NodeGroups.Add(new RuleNodeGroup
        {
            Id = "GROUP_Parent",
            Name = "Gameplay",
            Color = "Amber",
            MemberNodeIds = [nodeIds[0]],
            GraphX = 12,
            GraphY = 24,
            Width = 640,
            Height = 420
        });
        rule.NodeGroups.Add(new RuleNodeGroup
        {
            Id = "GROUP_Child",
            Name = "Timer Flow",
            Color = "Teal",
            ParentGroupId = "GROUP_Parent",
            MemberNodeIds = [nodeIds[1]],
            MemberRerouteIds = ["REROUTE_Main"],
            GraphX = 48,
            GraphY = 96,
            Width = 360,
            Height = 220
        });
        rule.WireReroutes.Add(new RuleWireReroute
        {
            Id = "REROUTE_Main",
            GraphX = 300,
            GraphY = 180,
            InputDirection = WireRerouteDirection.Up,
            OutputDirection = WireRerouteDirection.Down
        });
        rule.Connections.Single().RerouteIds.Add("REROUTE_Main");

        var restored = RuleGraphJson.Deserialize(RuleGraphJson.Serialize(graph));
        var groups = restored.Rules.Single().NodeGroups;
        var restoredRule = restored.Rules.Single();

        Assert.Equal(2, groups.Count);
        Assert.Equal("Amber", groups[0].Color);
        Assert.Equal("GROUP_Parent", groups[1].ParentGroupId);
        Assert.Equal(360, groups[1].Width);
        Assert.Equal(nodeIds[1], Assert.Single(groups[1].MemberNodeIds));
        Assert.Equal("REROUTE_Main", Assert.Single(groups[1].MemberRerouteIds));
        Assert.Equal("REROUTE_Main", Assert.Single(restoredRule.Connections.Single().RerouteIds));
        var reroute = Assert.Single(restoredRule.WireReroutes);
        Assert.Equal(WireRerouteDirection.Up, reroute.InputDirection);
        Assert.Equal(WireRerouteDirection.Down, reroute.OutputDirection);
    }

    [Fact]
    public void Deserialize_RejectsOldGraphVersions()
    {
        const string json = """
        {
          "schema": "VisualRuleSystem.RuleGraph",
          "version": 2,
          "name": "OldGraph",
          "rules": []
        }
        """;

        Assert.Throws<JsonException>(() => RuleGraphJson.Deserialize(json));
    }

    [Fact]
    public void PortableScriptGraph_RoundTripsReadableGraphOrganization()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        rule.NodeGroups.Add(new RuleNodeGroup
        {
            Id = "GROUP_DebugFlow",
            Name = "Debug Flow",
            MemberNodeIds = rule.Nodes.Select(node => node.Id).ToList(),
            GraphX = 12,
            GraphY = 24
        });
        rule.WireReroutes.Add(new RuleWireReroute
        {
            Id = "REROUTE_DebugFlow",
            GraphX = 320,
            GraphY = 180
        });
        rule.Connections.Single().RerouteIds.Add("REROUTE_DebugFlow");

        var json = PortableScriptGraphService.Serialize(graph);
        var restored = PortableScriptGraphService.Deserialize(json);

        Assert.Empty(restored.Warnings);
        Assert.Contains("VisualRuleSystem.Polytoria.ScriptGraph", json, StringComparison.Ordinal);
        Assert.Contains("Debug Flow", json, StringComparison.Ordinal);
        Assert.Contains("REROUTE_DebugFlow", json, StringComparison.Ordinal);
        Assert.Equal("Debug Flow", restored.Graph.Rules.Single().NodeGroups.Single().Name);
        Assert.Equal("REROUTE_DebugFlow", Assert.Single(restored.Graph.Rules.Single().WireReroutes).Id);
    }

    [Fact]
    public void PortableScriptGraph_WarnsButLoadsDifferentVersion()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var json = PortableScriptGraphService.Serialize(graph)
            .Replace("\"formatVersion\": 1", "\"formatVersion\": 99", StringComparison.Ordinal);

        var restored = PortableScriptGraphService.Deserialize(json);

        Assert.NotEmpty(restored.Warnings);
        Assert.NotEmpty(restored.Graph.Rules.Single().Nodes);
    }
}
