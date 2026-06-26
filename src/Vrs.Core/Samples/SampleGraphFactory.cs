using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.Core.Samples;

public static class SampleGraphFactory
{
    public static RuleGraph CreateEmptyDraftGraph(GraphScriptKind scriptKind = GraphScriptKind.Server, string scriptName = "NewVisualScript")
    {
        var normalizedName = string.IsNullOrWhiteSpace(scriptName) ? "NewVisualScript" : scriptName.Trim();
        return new RuleGraph
        {
            Name = "PolytoriaVisualScriptingDraft",
            AuthoringMode = GraphAuthoringMode.PolyCreatorLessDraft,
            Script = new GraphScriptBinding
            {
                ScriptName = normalizedName,
                ScriptKind = scriptKind,
                Source = "Draft",
                IsScriptKindLocked = false,
                AutosaveEnabled = true
            },
            Rules =
            [
                new Rule
                {
                    Id = "RULE_Default",
                    Name = normalizedName,
                    Description = "Empty script graph.",
                    ScriptKind = scriptKind
                }
            ]
        };
    }

    public static RuleGraph CreateTimerMessageGraph(IEnumerable<NodeCatalogEntry>? catalogEntries = null)
    {
        var catalog = catalogEntries?.ToList() ?? [];
        var timerEntry = NodeCatalogService.FindByCatalogId(catalog, "EV_OnTimerTick");
        var messageEntry = NodeCatalogService.FindByCatalogId(catalog, "ACT_ShowMessage");

        var trigger = timerEntry is null
            ? FallbackNode(NodeKind.Trigger, "TRG_OnTimerTick_1", "OnTimerTick", "On Timer Tick", 160, 120)
            : NodeCatalogService.CreateNode(timerEntry, 160, 120, "TRG_OnTimerTick_1");

        var action = messageEntry is null
            ? FallbackNode(NodeKind.Action, "ACT_ShowMessage_1", "ShowMessage", "Show Message", 460, 120)
            : NodeCatalogService.CreateNode(messageEntry, 460, 120, "ACT_ShowMessage_1");

        var rule = new Rule
        {
            Id = "RULE_TimerMessage",
            Name = "TimerMessage",
            Description = "Every second, print a visible debug message.",
            Comment = "Sample graph used for smoke tests and first launch.",
            Nodes = [trigger, action],
            Connections =
            [
                new GraphConnection
                {
                    Id = "CONN_Timer_Message",
                    From = new GraphEndpoint { NodeId = trigger.Id, PortId = GraphPortDefaults.FlowOut },
                    To = new GraphEndpoint { NodeId = action.Id, PortId = GraphPortDefaults.FlowIn },
                    ConnectionKind = GraphConnectionKind.Flow
                }
            ]
        };

        var graph = new RuleGraph
        {
            Name = "PolytoriaVisualScriptingDraft",
            AuthoringMode = GraphAuthoringMode.PolyCreatorLessDraft,
            Script = new GraphScriptBinding
            {
                ScriptName = "TimerMessage",
                ScriptKind = GraphScriptKind.Server,
                Source = "Sample",
                IsScriptKindLocked = true,
                AutosaveEnabled = true
            },
            SceneObjects =
            [
                new SceneObject { Id = "Root", Name = "Root", Kind = "Root", Path = "Root" },
                new SceneObject { Id = "Hidden", Name = "Hidden", Kind = "Folder", Path = "Hidden" },
                new SceneObject { Id = "ScriptService", Name = "ScriptService", Kind = "Service", Path = "ScriptService" }
            ],
            Rules = [rule]
        };
        rule.ScriptKind = graph.Script.ScriptKind;
        return graph;
    }

    public static RuleGraph CreatePolytoriaEssentialsDemoGraph(IEnumerable<NodeCatalogEntry> catalogEntries)
    {
        var catalog = catalogEntries.ToList();
        var start = CatalogNode(catalog, "EV_OnStart", NodeKind.Trigger, "TRG_EssentialsStart", "OnStart", "On Start", 120, 80);
        var timer = CatalogNode(catalog, "EV_OnTimerTick", NodeKind.Trigger, "TRG_EssentialsTimer", "OnTimerTick", "On Timer Tick", 120, 260);
        var touch = CatalogNode(catalog, "EV_OnTouchObject", NodeKind.Trigger, "TRG_EssentialsTouch", "OnTouchObject", "On Touch Object", 120, 440);
        var startRound = CatalogNode(catalog, "ACT_StartRound", NodeKind.Action, "ACT_EssentialsStartRound", "StartRound", "Start Round", 380, 80);
        var cooldownReady = CatalogNode(catalog, "COND_CooldownReady", NodeKind.Condition, "COND_EssentialsCooldownReady", "CooldownReady", "Cooldown Ready", 380, 260);
        var startCooldown = CatalogNode(catalog, "ACT_StartCooldown", NodeKind.Action, "ACT_EssentialsStartCooldown", "StartCooldown", "Start Cooldown", 660, 220);
        var addTeamScore = CatalogNode(catalog, "ACT_AddTeamScore", NodeKind.Action, "ACT_EssentialsAddTeamScore", "AddTeamScore", "Add Team Score", 900, 220);
        var showMessage = CatalogNode(catalog, "ACT_ShowMessage", NodeKind.Action, "ACT_EssentialsShowMessage", "ShowMessage", "Show Message", 1140, 220);
        var addPlayerScore = CatalogNode(catalog, "ACT_AddPlayerScore", NodeKind.Action, "ACT_EssentialsAddPlayerScore", "AddPlayerScore", "Add Player Score", 380, 440);

        SetParameter(startRound, "roundName", "Main Round");
        SetParameter(startRound, "duration", "60");
        SetParameter(cooldownReady, "cooldownName", "Score Tick");
        SetParameter(startCooldown, "cooldownName", "Score Tick");
        SetParameter(startCooldown, "duration", "5");
        SetParameter(addTeamScore, "teamName", "Players");
        SetParameter(addTeamScore, "amount", "1");
        SetParameter(showMessage, "message", "Essentials tick scored.");
        SetParameter(addPlayerScore, "amount", "10");

        var graph = new RuleGraph
        {
            Name = "PolytoriaEssentialsDemo",
            AuthoringMode = GraphAuthoringMode.PolyCreatorLessDraft,
            Script = new GraphScriptBinding
            {
                ScriptName = "PolytoriaEssentialsDemo",
                ScriptKind = GraphScriptKind.Server,
                Source = "Sample",
                IsScriptKindLocked = true,
                AutosaveEnabled = true
            },
            SceneObjects =
            [
                new SceneObject { Id = "Root", Name = "Root", Kind = "Root", Path = "Root" },
                new SceneObject { Id = "World", Name = "World", Kind = "Service", Path = "World" },
                new SceneObject { Id = "Environment", Name = "Environment", Kind = "Folder", Path = "World/Environment" }
            ],
            Rules =
            [
                new Rule
                {
                    Id = "RULE_EssentialsRoundStart",
                    Name = "EssentialsRoundStart",
                    Description = "Start one reusable runtime round.",
                    Nodes = [start, startRound],
                    Connections = [Flow("CONN_Essentials_Start_Round", start.Id, GraphPortDefaults.FlowOut, startRound.Id, GraphPortDefaults.FlowIn)]
                },
                new Rule
                {
                    Id = "RULE_EssentialsCooldownTick",
                    Name = "EssentialsCooldownTick",
                    Description = "Every timer tick, score once when the cooldown is ready.",
                    Nodes = [timer, cooldownReady, startCooldown, addTeamScore, showMessage],
                    Connections =
                    [
                        Flow("CONN_Essentials_Timer_Cooldown", timer.Id, GraphPortDefaults.FlowOut, cooldownReady.Id, GraphPortDefaults.FlowIn),
                        Flow("CONN_Essentials_Cooldown_Start", cooldownReady.Id, GraphPortDefaults.TrueOut, startCooldown.Id, GraphPortDefaults.FlowIn),
                        Flow("CONN_Essentials_Start_TeamScore", startCooldown.Id, GraphPortDefaults.FlowOut, addTeamScore.Id, GraphPortDefaults.FlowIn),
                        Flow("CONN_Essentials_TeamScore_Message", addTeamScore.Id, GraphPortDefaults.FlowOut, showMessage.Id, GraphPortDefaults.FlowIn)
                    ]
                },
                new Rule
                {
                    Id = "RULE_EssentialsTouchScore",
                    Name = "EssentialsTouchScore",
                    Description = "Touch-triggered rule that adds runtime player score.",
                    Nodes = [touch, addPlayerScore],
                    Connections = [Flow("CONN_Essentials_Touch_PlayerScore", touch.Id, GraphPortDefaults.FlowOut, addPlayerScore.Id, GraphPortDefaults.FlowIn)]
                }
            ]
        };
        foreach (var rule in graph.Rules)
        {
            rule.ScriptKind = graph.Script.ScriptKind;
        }

        return graph;
    }

    private static RuleNode FallbackNode(NodeKind kind, string id, string type, string label, float graphX, float graphY)
    {
        return new RuleNode
        {
            Kind = kind,
            Id = id,
            Type = type,
            Label = label,
            CatalogId = id.Split('_').Length > 2 ? string.Join('_', id.Split('_').Take(2)) : id,
            Ports = GraphPortDefaults.CreateDefaultPorts(kind),
            GraphX = graphX,
            GraphY = graphY,
            GraphPositionSet = true
        };
    }

    private static RuleNode CatalogNode(
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        string catalogId,
        NodeKind kind,
        string id,
        string type,
        string label,
        float graphX,
        float graphY)
    {
        var entry = NodeCatalogService.FindByCatalogId(catalog, catalogId);
        return entry is null
            ? FallbackNode(kind, id, type, label, graphX, graphY)
            : NodeCatalogService.CreateNode(entry, graphX, graphY, id);
    }

    private static void SetParameter(RuleNode node, string key, string value)
    {
        var parameter = node.Parameters.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (parameter is null)
        {
            return;
        }

        parameter.Value = value;
        parameter.Binding.SourceKind = GraphValueSourceKind.Constant;
        parameter.Binding.ConstantValue = value;
        parameter.Binding.DisplayText = value;
    }

    private static GraphConnection Flow(string id, string fromNodeId, string fromPortId, string toNodeId, string toPortId)
    {
        return new GraphConnection
        {
            Id = id,
            From = new GraphEndpoint { NodeId = fromNodeId, PortId = fromPortId },
            To = new GraphEndpoint { NodeId = toNodeId, PortId = toPortId },
            ConnectionKind = GraphConnectionKind.Flow
        };
    }
}
