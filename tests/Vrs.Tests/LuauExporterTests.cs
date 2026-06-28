using Vrs.Core.Catalog;
using Vrs.Core.Export;
using Vrs.Core.RuntimeEvents;
using Vrs.Core.Samples;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.Tests;

public sealed class LuauExporterTests
{
    [Fact]
    public void ExportRuleToLuau_EmitsReadableTimerScript()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("-- [VSR] VRS_GRAPH_BEGIN base64-json", luau);
        Assert.DoesNotContain("function TimerMessage.Run(context)", luau);
        Assert.Contains("-- [VSR] USER CONFIGURATION", luau);
        Assert.DoesNotContain("-- [VSR] CONFIGURATION VARIABLES", luau);
        Assert.DoesNotContain("-- [VSR] TRIGGER VARIABLES", luau);
        Assert.DoesNotContain("-- [VSR] ACTION VARIABLES", luau);
        Assert.DoesNotContain("-- [VSR] CONDITION VARIABLES", luau);
        Assert.DoesNotContain("Configured as:", luau);
        Assert.DoesNotContain("This action", luau);
        Assert.DoesNotContain("This function", luau);
        Assert.DoesNotContain("Change this value", luau);
        Assert.DoesNotContain("-- [VSR] Local variables", luau);
        Assert.Contains("local VRS = { actions = {}, conditions = {}, vars = {}, states = {} }", luau);
        Assert.DoesNotContain("playerState", luau);
        Assert.DoesNotContain("vrsPlayerData", luau);
        Assert.DoesNotContain("local showMessage", luau);
        Assert.Contains("local TIMER_INTERVAL_SECONDS = 1", luau);
        Assert.Contains("local MESSAGE_TEXT = \"Hello from VisualRuleSystem.\"", luau);
        AssertOccursInOrder(
            luau,
            "-- [VSR] TRIGGER: ON TIMER TICK",
            "local TIMER_INTERVAL_SECONDS = 1",
            "-- [VSR] ACTION: SHOW MESSAGE",
            "local MESSAGE_TEXT = \"Hello from VisualRuleSystem.\"");
        Assert.Contains("local function onTimerTick()", luau);
        Assert.Contains("VRS.actions.showMessage(triggerObject, triggerContext)", luau);
        Assert.Contains("VRS.actions.showMessage = function(triggerObject, triggerContext)", luau);
        Assert.Contains("print(MESSAGE_TEXT)", luau);
        Assert.Contains("onTimerTick()", luau);
        Assert.EndsWith($"{Environment.NewLine}{Environment.NewLine}", luau);
    }

    [Fact]
    public void ExportRuleToLuauFiles_UsesRuleScriptKind()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        rule.ScriptKind = GraphScriptKind.Local;

        var localFile = new LuauExporter().ExportRuleToLuauFiles(rule, graph, catalog.Nodes).Single();
        rule.ScriptKind = GraphScriptKind.Module;
        var moduleFile = new LuauExporter().ExportRuleToLuauFiles(rule, graph, catalog.Nodes).Single();

        Assert.Equal(".client.luau", localFile.Suffix);
        Assert.Equal("Local", localFile.Role);
        Assert.DoesNotContain("Script kind selected in the visual graph", localFile.Content);
        Assert.Equal(".module.luau", moduleFile.Suffix);
        Assert.Equal("Module", moduleFile.Role);
        Assert.DoesNotContain("Script kind selected in the visual graph", moduleFile.Content);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsOnStartTriggerBeforeActionBlock()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var onStart = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var message = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(onStart, 100, 100, "TRG_OnStart");
        var messageNode = NodeCatalogService.CreateNode(message, 360, 100, "ACT_Message");

        var rule = new Rule
        {
            Id = "RULE_OnStartMessage",
            Name = "OnStartMessage",
            Nodes = [trigger, messageNode],
            Connections =
            [
                Flow("CONN_Start_Message", trigger.Id, GraphPortDefaults.FlowOut, messageNode.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph
        {
            Name = "OnStartMessageGraph",
            Rules = [rule]
        };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        AssertOccursInOrder(
            luau,
            "-- [VSR] TRIGGER: ON START",
            "local function onStart()",
            "VRS.actions.showMessage(triggerObject, triggerContext)",
            "-- [VSR] ACTION: SHOW MESSAGE",
            "VRS.actions.showMessage = function(triggerObject, triggerContext)",
            "print(MESSAGE_TEXT)",
            "-- [VSR] TRIGGER BOOTSTRAP",
            "onStart()",
            "-- [VSR] VRS_GRAPH_BEGIN base64-json");
    }

    [Fact]
    public void ExportRuleToLuau_EmitsReadableSetObjectColorAction()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var onStart = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var color = catalog.Nodes.Single(node => node.IdBase == "ACT_SetObjectColor");
        var trigger = NodeCatalogService.CreateNode(onStart, 100, 100, "TRG_OnStart");
        var colorNode = NodeCatalogService.CreateNode(color, 360, 100, "ACT_Color");
        SetConstant(colorNode, "target", "Self");
        SetConstant(colorNode, "r", "0");
        SetConstant(colorNode, "g", "0.65");
        SetConstant(colorNode, "b", "1");

        var rule = new Rule
        {
            Id = "RULE_OnStartColor",
            Name = "OnStartColor",
            Nodes = [trigger, colorNode],
            Connections =
            [
                Flow("CONN_Start_Color", trigger.Id, GraphPortDefaults.FlowOut, colorNode.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph
        {
            Name = "OnStartColorGraph",
            Rules = [rule]
        };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local TARGET_COLOR = Color.New(0, 0.65, 1, 1)", luau);
        Assert.DoesNotContain("local TARGET_NAME", luau);
        Assert.DoesNotContain("local function resolveTarget", luau);
        AssertOccursInOrder(
            luau,
            "-- [VSR] SCRIPT CONTEXT",
            "local VRS = { actions = {}, conditions = {}, vars = {}, states = {} }",
            "-- [VSR] TRIGGER: ON START",
            "VRS.actions.changeObjectColor(triggerObject, triggerContext)",
            "-- [VSR] ACTION: SET OBJECT COLOR",
            "local TARGET_COLOR = Color.New(0, 0.65, 1, 1)",
            "VRS.actions.changeObjectColor = function(triggerObject, triggerContext)",
            "local targetPart = triggerObject",
            "targetPart.Color = TARGET_COLOR",
            "-- [VSR] TRIGGER BOOTSTRAP",
            "onStart()",
            "-- [VSR] VRS_GRAPH_BEGIN base64-json");
        Assert.DoesNotContain("color changed by Set Object Color", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsNodeDebugSuccessLogsOnlyWhenEnabled()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var onStart = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var color = catalog.Nodes.Single(node => node.IdBase == "ACT_SetObjectColor");
        var trigger = NodeCatalogService.CreateNode(onStart, 100, 100, "TRG_OnStart");
        var colorNode = NodeCatalogService.CreateNode(color, 360, 100, "ACT_Color");
        colorNode.DebugEnabled = true;

        var rule = new Rule
        {
            Id = "RULE_DebugColor",
            Name = "DebugColor",
            Nodes = [trigger, colorNode],
            Connections =
            [
                Flow("CONN_Start_Color", trigger.Id, GraphPortDefaults.FlowOut, colorNode.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph
        {
            Name = "DebugColorGraph",
            Rules = [rule]
        };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("print(targetPart.Name .. \" color changed by Set Object Color.\")", luau);
        Assert.Contains("Set Object Color stopped: target Self was not found.", luau);
        Assert.Contains("is not a Part", luau);
    }

    [Fact]
    public void ExportRuleToLuau_ReportsDisconnectedHumanNodesWithoutCallingThem()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        rule.Connections.Clear();

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("print(\"On Timer Tick trigger stopped: no connected action or condition.\")", luau);
        Assert.DoesNotContain("-- [VSR] DISCONNECTED NODES", luau);
        Assert.DoesNotContain("-- [VSR] Not executed: Show Message", luau);
        Assert.DoesNotContain("VRS.actions.showMessage(triggerObject, triggerContext)", luau);
        Assert.Contains("VRS.actions.showMessage = function(triggerObject, triggerContext)", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsStableTimerWaitMessageFlow()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var timer = catalog.Nodes.Single(node => node.IdBase == "EV_OnTimerTick");
        var wait = catalog.Nodes.Single(node => node.IdBase == "ACT_WaitSeconds");
        var message = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(timer, 100, 100, "TRG_Timer");
        var waitNode = NodeCatalogService.CreateNode(wait, 360, 100, "ACT_Wait");
        var messageNode = NodeCatalogService.CreateNode(message, 620, 100, "ACT_Message");
        messageNode.Parameters.Single(parameter => parameter.Key == "message").Value = "Done after wait";

        var rule = new Rule
        {
            Id = "RULE_TimerWaitMessage",
            Name = "TimerWaitMessage",
            Nodes = [trigger, waitNode, messageNode],
            Connections =
            [
                Flow("CONN_Timer_Wait", trigger.Id, GraphPortDefaults.FlowOut, waitNode.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Wait_Message", waitNode.Id, GraphPortDefaults.FlowOut, messageNode.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph
        {
            Name = "TimerWaitMessageGraph",
            Rules = [rule]
        };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local WAIT_DURATION_SECONDS = 1", luau);
        Assert.Contains("wait(WAIT_DURATION_SECONDS)", luau);
        Assert.Contains("local MESSAGE_TEXT = \"Done after wait\"", luau);
        AssertOccursInOrder(
            luau,
            "-- [VSR] TRIGGER: ON TIMER TICK",
            "VRS.actions.waitSeconds(triggerObject, triggerContext)",
            "VRS.actions.showMessage(triggerObject, triggerContext)",
            "-- [VSR] ACTION: WAIT SECONDS",
            "VRS.actions.waitSeconds = function(triggerObject, triggerContext)",
            "wait(WAIT_DURATION_SECONDS)",
            "-- [VSR] ACTION: SHOW MESSAGE",
            "print(MESSAGE_TEXT)");
        Assert.DoesNotContain("${", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsAuthorNoteWithoutGeneratedSummaries()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        var action = rule.Nodes.Single(node => node.CatalogId == "ACT_ShowMessage");
        action.UserComment = "Keep this visible while testing timer startup.";

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.DoesNotContain("Configured as:", luau);
        Assert.DoesNotContain("This action", luau);
        Assert.Contains("-- [User] Keep this visible while testing timer startup.", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsNumberCompareConditionBranches()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = CreateNumberCompareBranchGraph(catalog.Nodes, operatorValue: ">");
        var rule = graph.Rules.Single();
        rule.Nodes.Single(node => node.Kind == NodeKind.Condition).UserComment = "Only show the true branch after the number check passes.";

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("VRS.conditions.checkNumberCompare = function(triggerObject, triggerContext)", luau);
        Assert.Contains("return 10 > 0", luau);
        AssertOccursInOrder(
            luau,
            "-- [VSR] TRIGGER: ON TIMER TICK",
            "if VRS.conditions.checkNumberCompare(triggerObject, triggerContext) then",
            "VRS.actions.showMessage(triggerObject, triggerContext)",
            "else",
            "VRS.actions.showMessage_2(triggerObject, triggerContext)",
            "end",
            "-- [VSR] CONDITION: NUMBER COMPARE",
            "-- [User] Only show the true branch after the number check passes.",
            "VRS.conditions.checkNumberCompare = function(triggerObject, triggerContext)",
            "return 10 > 0",
            "-- [VSR] ACTION: SHOW MESSAGE",
            "VRS.actions.showMessage = function(triggerObject, triggerContext)",
            "-- [VSR] ACTION: SHOW MESSAGE",
            "VRS.actions.showMessage_2 = function(triggerObject, triggerContext)");
    }

    [Fact]
    public void ExportRuleToLuau_EmitsInvalidNumberCompareFallback()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = CreateNumberCompareBranchGraph(catalog.Nodes, operatorValue: "??");
        var rule = graph.Rules.Single();

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("print(\"Condition Number Compare has invalid operator ??; returning false.\")", luau);
        Assert.Contains("return false", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsUnknownConditionFallback()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var timer = catalog.Nodes.Single(node => node.IdBase == "EV_OnTimerTick");
        var message = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(timer, 100, 100, "TRG_Timer");
        var condition = new RuleNode
        {
            Kind = NodeKind.Condition,
            Id = "COND_Custom_1",
            Type = "CustomCondition",
            Label = "Custom Gate",
            CatalogId = "COND_Custom",
            Enabled = true,
            Ports = GraphPortDefaults.CreateDefaultPorts(NodeKind.Condition)
        };
        var messageNode = NodeCatalogService.CreateNode(message, 620, 100, "ACT_Message");

        var rule = new Rule
        {
            Id = "RULE_CustomCondition",
            Name = "CustomCondition",
            Nodes = [trigger, condition, messageNode],
            Connections =
            [
                Flow("CONN_Timer_Condition", trigger.Id, GraphPortDefaults.FlowOut, condition.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Condition_Message", condition.Id, GraphPortDefaults.TrueOut, messageNode.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph
        {
            Name = "CustomConditionGraph",
            Rules = [rule]
        };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("VRS.conditions.checkCustomGate = function(triggerObject, triggerContext)", luau);
        Assert.Contains("print(\"Condition Custom Gate is not implemented yet; returning false.\")", luau);
        Assert.Contains("return false", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsRunLuauTriggerActionConditionAndProperty()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var triggerEntry = catalog.Nodes.Single(node => node.IdBase == "EV_RunLuauTrigger");
        var conditionEntry = catalog.Nodes.Single(node => node.IdBase == "COND_RunLuauCondition");
        var actionEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_RunLuauAction");
        var propertyEntry = catalog.Nodes.Single(node => node.IdBase == "PROP_RunLuauProperty");
        var printEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue");
        var trigger = NodeCatalogService.CreateNode(triggerEntry, 100, 100, "TRG_RunCode");
        var condition = NodeCatalogService.CreateNode(conditionEntry, 320, 100, "COND_RunCode");
        var action = NodeCatalogService.CreateNode(actionEntry, 540, 100, "ACT_RunCode");
        var property = NodeCatalogService.CreateNode(propertyEntry, 540, 220, "PROP_RunCodeValue");
        var print = NodeCatalogService.CreateNode(printEntry, 760, 100, "ACT_PrintRunCode");

        SetConstant(trigger, "code", "fire({ value = \"ready\" })");
        SetConstant(condition, "code", "return triggerContext.value == \"ready\"");
        SetConstant(action, "code", "VRS.vars[\"ran\"] = triggerContext.value");
        SetConstant(property, "code", "return VRS.vars[\"ran\"]");
        SetConstant(property, "resultType", "String");

        var rule = new Rule
        {
            Id = "RULE_RunCode",
            Name = "RunCode",
            Nodes = [trigger, condition, action, property, print],
            Connections =
            [
                Flow("CONN_RunCode_Trigger_Condition", trigger.Id, GraphPortDefaults.FlowOut, condition.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_RunCode_Condition_Action", condition.Id, GraphPortDefaults.TrueOut, action.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_RunCode_Action_Print", action.Id, GraphPortDefaults.FlowOut, print.Id, GraphPortDefaults.FlowIn),
                Value("CONN_RunCode_Property_Print", property.Id, GraphPortDefaults.ValueOut, print.Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph
        {
            Name = "RunCodeGraph",
            Rules = [rule]
        };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function runCodeTrigger()", luau);
        Assert.Contains("local function fire(customContext)", luau);
        Assert.Contains("local flowTriggerObject = triggerContext.object or triggerObject", luau);
        Assert.Contains("fire({ value = \"ready\" })", luau);
        Assert.Contains("if VRS.conditions.checkCodeCondition(flowTriggerObject, triggerContext) then", luau);
        Assert.Contains("VRS.actions.runCodeAction(flowTriggerObject, triggerContext)", luau);
        Assert.Contains("VRS.conditions.checkCodeCondition = function(triggerObject, triggerContext)", luau);
        Assert.Contains("local ok, result = pcall(function()", luau);
        Assert.Contains("return triggerContext.value == \"ready\"", luau);
        Assert.Contains("VRS.actions.runCodeAction = function(triggerObject, triggerContext)", luau);
        Assert.Contains("VRS.vars[\"ran\"] = triggerContext.value", luau);
        Assert.Contains("print(tostring((function()", luau);
        Assert.Contains("return VRS.vars[\"ran\"]", luau);
        Assert.Contains("Code Value failed", luau);
        Assert.Contains("local function resolveTarget", luau);
        Assert.Contains("local function makeVector3", luau);
        Assert.Contains("runCodeTrigger()", luau);
    }

    [Fact]
    public void ExportRuleToLuau_UsesConnectedPropertyValueForActionParameter()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var timer = catalog.Nodes.Single(node => node.IdBase == "EV_OnTimerTick");
        var manualText = catalog.Nodes.Single(node => node.IdBase == "PROP_ManualText");
        var message = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(timer, 100, 100, "TRG_Timer");
        var textNode = NodeCatalogService.CreateNode(manualText, 360, 80, "PROP_Text");
        var messageNode = NodeCatalogService.CreateNode(message, 620, 100, "ACT_Message");
        messageNode.Ports.Add(GraphPortDefaults.CreateInput(
            GraphPortDefaults.ParameterPortId("message"),
            "Message",
            NodePortKind.Value,
            "String",
            "#c084fc",
            10));
        textNode.Parameters.Single(parameter => parameter.Key == "value").Value = "Text from value node";
        messageNode.Parameters.Single(parameter => parameter.Key == "message").Value = "Fallback message";

        var rule = new Rule
        {
            Id = "RULE_PropertyPreview",
            Name = "PropertyPreview",
            Nodes = [trigger, textNode, messageNode],
            Connections =
            [
                Flow("CONN_Timer_Message", trigger.Id, GraphPortDefaults.FlowOut, messageNode.Id, GraphPortDefaults.FlowIn),
                new GraphConnection
                {
                    Id = "CONN_Text_Message",
                    From = new GraphEndpoint { NodeId = textNode.Id, PortId = GraphPortDefaults.ValueOut },
                    To = new GraphEndpoint { NodeId = messageNode.Id, PortId = GraphPortDefaults.ParameterPortId("message") },
                    ConnectionKind = GraphConnectionKind.Value
                }
            ]
        };
        var graph = new RuleGraph
        {
            Name = "PropertyPreviewGraph",
            Rules = [rule]
        };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local MESSAGE_TEXT = \"Text from value node\"", luau);
        Assert.DoesNotContain("Fallback message", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsCoreGeneralPropertyExpressions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var timer = catalog.Nodes.Single(node => node.IdBase == "EV_OnTimerTick");
        var addEntry = catalog.Nodes.Single(node => node.IdBase == "PROP_AddNumbers");
        var clampEntry = catalog.Nodes.Single(node => node.IdBase == "PROP_ClampNumber");
        var joinEntry = catalog.Nodes.Single(node => node.IdBase == "PROP_JoinText");
        var rangeEntry = catalog.Nodes.Single(node => node.IdBase == "COND_NumberInRange");
        var messageEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(timer, 100, 100, "TRG_Timer");
        var add = NodeCatalogService.CreateNode(addEntry, 260, 60, "PROP_Add");
        var clamp = NodeCatalogService.CreateNode(clampEntry, 420, 60, "PROP_Clamp");
        var join = NodeCatalogService.CreateNode(joinEntry, 580, 60, "PROP_Join");
        var range = NodeCatalogService.CreateNode(rangeEntry, 580, 160, "COND_Range");
        var message = NodeCatalogService.CreateNode(messageEntry, 820, 160, "ACT_Message");

        SetConstant(add, "left", "2");
        SetConstant(add, "right", "3");
        SetConstant(clamp, "min", "0");
        SetConstant(clamp, "max", "10");
        SetConstant(join, "first", "Total");
        SetConstant(join, "separator", ": ");
        SetConstant(range, "min", "0");
        SetConstant(range, "max", "10");

        var rule = new Rule
        {
            Id = "RULE_CoreGeneralExpressions",
            Name = "CoreGeneralExpressions",
            Nodes = [trigger, add, clamp, join, range, message],
            Connections =
            [
                Flow("CONN_Timer_Range", trigger.Id, GraphPortDefaults.FlowOut, range.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Range_Message", range.Id, GraphPortDefaults.TrueOut, message.Id, GraphPortDefaults.FlowIn),
                Value("CONN_Add_Clamp", add.Id, GraphPortDefaults.ValueOut, clamp.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Clamp_Range", clamp.Id, GraphPortDefaults.ValueOut, range.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Clamp_Join", clamp.Id, GraphPortDefaults.ValueOut, join.Id, GraphPortDefaults.ParameterPortId("second")),
                Value("CONN_Join_Message", join.Id, GraphPortDefaults.ValueOut, message.Id, GraphPortDefaults.ParameterPortId("message"))
            ]
        };
        var graph = new RuleGraph { Name = "CoreGeneralExpressionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local MESSAGE_TEXT = (tostring(\"Total\") .. \": \" .. tostring(math.max(0, math.min(10, (2 + 3)))))", luau);
        Assert.Contains("return math.max(0, math.min(10, (2 + 3))) >= 0 and math.max(0, math.min(10, (2 + 3))) <= 10", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsCoreGeneralScriptVariableNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var setVariable = catalog.Nodes.Single(node => node.IdBase == "ACT_SetScriptVariable");
        var increment = catalog.Nodes.Single(node => node.IdBase == "ACT_IncrementScriptNumber");
        var readVariable = catalog.Nodes.Single(node => node.IdBase == "PROP_ReadScriptVariable");
        var messageEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(start, 100, 100, "TRG_Start");
        var set = NodeCatalogService.CreateNode(setVariable, 320, 100, "ACT_SetScore");
        var add = NodeCatalogService.CreateNode(increment, 540, 100, "ACT_AddScore");
        var read = NodeCatalogService.CreateNode(readVariable, 760, 60, "PROP_ReadScore");
        var message = NodeCatalogService.CreateNode(messageEntry, 760, 140, "ACT_Message");

        SetConstant(set, "name", "Score");
        SetConstant(set, "value", "10");
        SetConstant(add, "name", "Score");
        SetConstant(add, "amount", "5");
        SetConstant(read, "name", "Score");

        var rule = new Rule
        {
            Id = "RULE_CoreGeneralVariables",
            Name = "CoreGeneralVariables",
            Nodes = [trigger, set, add, read, message],
            Connections =
            [
                Flow("CONN_Start_Set", trigger.Id, GraphPortDefaults.FlowOut, set.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Set_Add", set.Id, GraphPortDefaults.FlowOut, add.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Add_Message", add.Id, GraphPortDefaults.FlowOut, message.Id, GraphPortDefaults.FlowIn),
                Value("CONN_Read_Message", read.Id, GraphPortDefaults.ValueOut, message.Id, GraphPortDefaults.ParameterPortId("message"))
            ]
        };
        var graph = new RuleGraph { Name = "CoreGeneralVariablesGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local VRS = { actions = {}, conditions = {}, vars = {}, states = {} }", luau);
        Assert.Contains("VRS.vars[\"Score\"] = 10", luau);
        Assert.Contains("VRS.vars[\"Score\"] = (tonumber(VRS.vars[\"Score\"]) or 0) + 5", luau);
        Assert.DoesNotContain("local MESSAGE_TEXT = VRS.vars[\"Score\"]", luau);
        Assert.Contains("print(VRS.vars[\"Score\"])", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsScriptSharedTableNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartShared");
        var set = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetSharedValue"), stableId: "ACT_SetShared");
        var increment = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_IncrementSharedNumber"), stableId: "ACT_IncrementShared");
        var append = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_AppendSharedText"), stableId: "ACT_AppendShared");
        var exists = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_SharedValueExists"), stableId: "COND_SharedExists");
        var printValue = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintSharedValue");
        var atLeast = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_SharedNumberAtLeast"), stableId: "COND_SharedAtLeast");
        var printNumber = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintSharedNumber");
        var printText = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintSharedText");
        var remove = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_RemoveSharedValue"), stableId: "ACT_RemoveShared");
        var clearPrefix = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ClearSharedPrefix"), stableId: "ACT_ClearSharedPrefix");
        var clearSuffix = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ClearSharedSuffix"), stableId: "ACT_ClearSharedSuffix");
        var clearAll = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ClearSharedValues"), stableId: "ACT_ClearSharedAll");
        var readValue = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ReadSharedValue"), stableId: "PROP_ReadSharedValue");
        var readNumber = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ReadSharedNumber"), stableId: "PROP_ReadSharedNumber");
        var readText = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ReadSharedText"), stableId: "PROP_ReadSharedText");

        SetConstant(set, "key", "Score");
        SetConstant(set, "value", "10");
        SetConstant(increment, "key", "Score");
        SetConstant(increment, "amount", "5");
        SetConstant(append, "key", "Log");
        SetConstant(append, "text", "!");
        SetConstant(exists, "key", "Score");
        SetConstant(atLeast, "key", "Score");
        SetConstant(atLeast, "minimum", "10");
        SetConstant(remove, "key", "Old");
        SetConstant(clearPrefix, "prefix", "player:");
        SetConstant(clearSuffix, "suffix", ":temp");
        SetConstant(readValue, "key", "Score");
        SetConstant(readNumber, "key", "Score");
        SetConstant(readNumber, "fallback", "0");
        SetConstant(readText, "key", "Log");
        SetConstant(readText, "fallback", "");

        var rule = new Rule
        {
            Id = "RULE_ScriptSharedTable",
            Name = "ScriptSharedTable",
            Nodes =
            [
                start,
                set,
                increment,
                append,
                exists,
                printValue,
                atLeast,
                printNumber,
                printText,
                remove,
                clearPrefix,
                clearSuffix,
                clearAll,
                readValue,
                readNumber,
                readText
            ],
            Connections =
            [
                Flow("CONN_Shared_Start_Set", start.Id, GraphPortDefaults.FlowOut, set.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Shared_Set_Increment", set.Id, GraphPortDefaults.FlowOut, increment.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Shared_Increment_Append", increment.Id, GraphPortDefaults.FlowOut, append.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Shared_Append_Exists", append.Id, GraphPortDefaults.FlowOut, exists.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Shared_Exists_PrintValue", exists.Id, GraphPortDefaults.TrueOut, printValue.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Shared_PrintValue_AtLeast", printValue.Id, GraphPortDefaults.FlowOut, atLeast.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Shared_AtLeast_PrintNumber", atLeast.Id, GraphPortDefaults.TrueOut, printNumber.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Shared_PrintNumber_PrintText", printNumber.Id, GraphPortDefaults.FlowOut, printText.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Shared_PrintText_Remove", printText.Id, GraphPortDefaults.FlowOut, remove.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Shared_Remove_ClearPrefix", remove.Id, GraphPortDefaults.FlowOut, clearPrefix.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Shared_ClearPrefix_ClearSuffix", clearPrefix.Id, GraphPortDefaults.FlowOut, clearSuffix.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Shared_ClearSuffix_ClearAll", clearSuffix.Id, GraphPortDefaults.FlowOut, clearAll.Id, GraphPortDefaults.FlowIn),
                Value("CONN_Shared_ReadValue_Print", readValue.Id, GraphPortDefaults.ValueOut, printValue.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Shared_ReadNumber_Print", readNumber.Id, GraphPortDefaults.ValueOut, printNumber.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Shared_ReadText_Print", readText.Id, GraphPortDefaults.ValueOut, printText.Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "ScriptSharedTableGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("ScriptSharedTable[\"Score\"] = 10", luau);
        Assert.Contains("ScriptSharedTable[\"Score\"] = (tonumber(ScriptSharedTable[\"Score\"]) or 0) + 5", luau);
        Assert.Contains("ScriptSharedTable[\"Log\"] = tostring(ScriptSharedTable[\"Log\"] or \"\") .. tostring(\"!\")", luau);
        Assert.Contains("return ScriptSharedTable[\"Score\"] ~= nil", luau);
        Assert.Contains("return (tonumber(ScriptSharedTable[\"Score\"]) or 0) >= 10", luau);
        Assert.Contains("print(tostring((function() if ScriptSharedTable == nil then return nil end return ScriptSharedTable[\"Score\"] end)()))", luau);
        Assert.Contains("print(tostring((function() if ScriptSharedTable == nil then return 0 end return tonumber(ScriptSharedTable[\"Score\"]) or 0 end)()))", luau);
        Assert.Contains("print(tostring((function() if ScriptSharedTable == nil then return \"\" end local sharedValue = ScriptSharedTable[\"Log\"]; if sharedValue == nil then return \"\" end return tostring(sharedValue) end)()))", luau);
        Assert.Contains("ScriptSharedTable:Remove(tostring(\"Old\"))", luau);
        Assert.Contains("ScriptSharedTable:ClearPrefix(tostring(\"player:\"))", luau);
        Assert.Contains("ScriptSharedTable:ClearSuffix(tostring(\":temp\"))", luau);
        Assert.Contains("ScriptSharedTable:Clear()", luau);
        Assert.Contains("Set Shared Value stopped: ScriptSharedTable is not available.", luau);
        Assert.DoesNotContain("Set Shared Value is not implemented", luau);
        Assert.DoesNotContain("Read Shared Value is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsScriptSharedTableAdditionalConditions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartSharedConditions");
        var conditionIds = new[]
        {
            "COND_SharedValueMissing",
            "COND_SharedNumberEquals",
            "COND_SharedNumberAtMost",
            "COND_SharedTextEquals",
            "COND_SharedTextContains"
        };
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"COND_SharedExtra_{index}"))
            .ToList();
        var message = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_SharedConditionMessage");

        SetConstant(conditions[0], "key", "Missing");
        SetConstant(conditions[1], "key", "Score");
        SetConstant(conditions[1], "expected", "10");
        SetConstant(conditions[2], "key", "Score");
        SetConstant(conditions[2], "maximum", "5");
        SetConstant(conditions[3], "key", "Log");
        SetConstant(conditions[3], "text", "Ready");
        SetConstant(conditions[3], "caseSensitive", "false");
        SetConstant(conditions[4], "key", "Log");
        SetConstant(conditions[4], "search", "error");
        SetConstant(conditions[4], "caseSensitive", "false");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_SharedCondition_Start", start.Id, GraphPortDefaults.FlowOut, conditions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < conditions.Count; index++)
        {
            var nextNodeId = index + 1 < conditions.Count ? conditions[index + 1].Id : message.Id;
            connections.Add(Flow($"CONN_SharedCondition_{index}", conditions[index].Id, GraphPortDefaults.TrueOut, nextNodeId, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_ScriptSharedTableConditions",
            Name = "ScriptSharedTableConditions",
            Nodes = [start, .. conditions, message],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "ScriptSharedTableConditionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("return ScriptSharedTable[\"Missing\"] == nil", luau);
        Assert.Contains("return currentNumber == 10", luau);
        Assert.Contains("return currentNumber <= 5", luau);
        Assert.Contains("local expectedText = tostring(\"Ready\")", luau);
        Assert.Contains("return currentText == expectedText", luau);
        Assert.Contains("local expectedText = tostring(\"error\")", luau);
        Assert.Contains("return string.find(currentText, expectedText, 1, true) ~= nil", luau);
        Assert.DoesNotContain("Shared Value Missing is not implemented", luau);
        Assert.DoesNotContain("Shared Text Contains is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsScriptSharedTableWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var triggerIds = new[]
        {
            "EV_OnSharedValueChanged",
            "EV_OnSharedValueExists",
            "EV_OnSharedValueRemoved",
            "EV_OnSharedNumberReachedAtLeast",
            "EV_OnSharedNumberDroppedToAtMost",
            "EV_OnSharedTextBecame",
            "EV_OnSharedTextContains"
        };
        var triggers = triggerIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"TRG_SharedWatcher_{index}"))
            .ToList();
        var message = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_SharedWatcherMessage");

        foreach (var trigger in triggers.Take(5))
        {
            SetConstant(trigger, "key", "Score");
            SetConstant(trigger, "interval", "0.1");
        }

        SetConstant(triggers[3], "minimum", "10");
        SetConstant(triggers[4], "maximum", "0");
        SetConstant(triggers[5], "key", "Log");
        SetConstant(triggers[5], "text", "Ready");
        SetConstant(triggers[5], "caseSensitive", "false");
        SetConstant(triggers[5], "interval", "0.1");
        SetConstant(triggers[6], "key", "Log");
        SetConstant(triggers[6], "search", "error");
        SetConstant(triggers[6], "caseSensitive", "false");
        SetConstant(triggers[6], "interval", "0.1");

        var connections = triggers
            .Select((trigger, index) => Flow($"CONN_SharedWatcher_{index}", trigger.Id, GraphPortDefaults.FlowOut, message.Id, GraphPortDefaults.FlowIn))
            .ToList();

        var rule = new Rule
        {
            Id = "RULE_ScriptSharedTableWatchers",
            Name = "ScriptSharedTableWatchers",
            Nodes = [.. triggers, message],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "ScriptSharedTableWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local watchedSharedKey = tostring(\"Score\")", luau);
        Assert.Contains("return ScriptSharedTable[watchedSharedKey]", luau);
        Assert.Contains("return currentValue ~= nil, currentValue", luau);
        Assert.Contains("return currentValue == nil, currentValue", luau);
        Assert.Contains("local watchedLimit = tonumber(10) or 10", luau);
        Assert.Contains("return currentValue >= watchedLimit, currentValue", luau);
        Assert.Contains("local watchedLimit = tonumber(0) or 0", luau);
        Assert.Contains("return currentValue <= watchedLimit, currentValue", luau);
        Assert.Contains("local expectedText = tostring(\"Ready\")", luau);
        Assert.Contains("return currentText == expectedText, currentText", luau);
        Assert.Contains("local expectedText = tostring(\"error\")", luau);
        Assert.Contains("return string.find(currentText, expectedText, 1, true) ~= nil, currentText", luau);
        Assert.Contains("sharedKey = watchedSharedKey, sharedValue = currentValue", luau);
        Assert.DoesNotContain("On Shared Value Changed is not implemented", luau);
        Assert.DoesNotContain("On Shared Text Contains is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsScriptRuntimeAndMissingInstanceNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartScripts");
        var set = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetScriptEnabled"), stableId: "ACT_SetScriptEnabled");
        var enable = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_EnableScript"), stableId: "ACT_EnableScript");
        var disable = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_DisableScript"), stableId: "ACT_DisableScript");
        var toggle = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ToggleScriptEnabled"), stableId: "ACT_ToggleScript");
        var call = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_CallScriptFunction"), stableId: "ACT_CallScript");
        var callAsync = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_CallScriptFunctionAsync"), stableId: "ACT_CallScriptAsync");
        var scriptEnabledCondition = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_ScriptIsEnabled"), stableId: "COND_ScriptEnabled");
        var scriptDisabledCondition = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_ScriptIsDisabled"), stableId: "COND_ScriptDisabled");
        var scriptCanCallCondition = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_ScriptCanCallFunction"), stableId: "COND_ScriptCanCall");
        var scriptCanCallAsyncCondition = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_ScriptCanCallAsyncFunction"), stableId: "COND_ScriptCanCallAsync");
        var scriptTargetExistsCondition = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_ScriptTargetExists"), stableId: "COND_ScriptTargetExists");
        var scriptTargetMissingCondition = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_ScriptTargetMissing"), stableId: "COND_ScriptTargetMissing");
        var printScriptEnabled = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintScriptEnabled");
        var printScriptRuntimeExtra = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintScriptRuntimeExtra");
        var missingCondition = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_ObjectIsMissingInstance"), stableId: "COND_MissingInstance");
        var printMissing = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintMissingInstance");
        var scriptEnabledValue = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ScriptEnabled"), stableId: "PROP_ScriptEnabled");
        var missingValue = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ObjectIsMissingInstance"), stableId: "PROP_MissingInstance");

        foreach (var node in new[] { set, enable, disable, toggle, call, callAsync, scriptEnabledCondition, scriptDisabledCondition, scriptCanCallCondition, scriptCanCallAsyncCondition, scriptTargetExistsCondition, scriptEnabledValue })
        {
            SetConstant(node, "target", "World/Hidden/VRS Helper");
        }

        SetConstant(set, "enabled", "false");
        SetConstant(call, "functionName", "Run");
        SetConstant(call, "argument", "payload");
        SetConstant(callAsync, "functionName", "RunAsync");
        SetConstant(callAsync, "argument", "payload");
        SetConstant(missingCondition, "target", "World/BrokenReference");
        SetConstant(scriptTargetMissingCondition, "target", "World/BrokenReference");
        SetConstant(missingValue, "target", "World/BrokenReference");

        var rule = new Rule
        {
            Id = "RULE_ScriptRuntime",
            Name = "ScriptRuntime",
            Nodes =
            [
                start,
                set,
                enable,
                disable,
                toggle,
                call,
                callAsync,
                scriptEnabledCondition,
                scriptDisabledCondition,
                scriptCanCallCondition,
                scriptCanCallAsyncCondition,
                scriptTargetExistsCondition,
                scriptTargetMissingCondition,
                printScriptEnabled,
                printScriptRuntimeExtra,
                missingCondition,
                printMissing,
                scriptEnabledValue,
                missingValue
            ],
            Connections =
            [
                Flow("CONN_Script_Start_Set", start.Id, GraphPortDefaults.FlowOut, set.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Script_Set_Enable", set.Id, GraphPortDefaults.FlowOut, enable.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Script_Enable_Disable", enable.Id, GraphPortDefaults.FlowOut, disable.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Script_Disable_Toggle", disable.Id, GraphPortDefaults.FlowOut, toggle.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Script_Toggle_Call", toggle.Id, GraphPortDefaults.FlowOut, call.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Script_Call_Async", call.Id, GraphPortDefaults.FlowOut, callAsync.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Script_Async_Condition", callAsync.Id, GraphPortDefaults.FlowOut, scriptEnabledCondition.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Script_Condition_Print", scriptEnabledCondition.Id, GraphPortDefaults.TrueOut, printScriptEnabled.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Script_Print_MissingCondition", printScriptEnabled.Id, GraphPortDefaults.FlowOut, missingCondition.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Script_MissingCondition_Print", missingCondition.Id, GraphPortDefaults.TrueOut, printMissing.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Script_Print_DisabledCondition", printMissing.Id, GraphPortDefaults.FlowOut, scriptDisabledCondition.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Script_Disabled_CanCall", scriptDisabledCondition.Id, GraphPortDefaults.TrueOut, scriptCanCallCondition.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Script_CanCall_CanCallAsync", scriptCanCallCondition.Id, GraphPortDefaults.TrueOut, scriptCanCallAsyncCondition.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Script_CanCallAsync_TargetExists", scriptCanCallAsyncCondition.Id, GraphPortDefaults.TrueOut, scriptTargetExistsCondition.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Script_TargetExists_TargetMissing", scriptTargetExistsCondition.Id, GraphPortDefaults.TrueOut, scriptTargetMissingCondition.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Script_TargetMissing_Print", scriptTargetMissingCondition.Id, GraphPortDefaults.TrueOut, printScriptRuntimeExtra.Id, GraphPortDefaults.FlowIn),
                Value("CONN_Script_Value_Print", scriptEnabledValue.Id, GraphPortDefaults.ValueOut, printScriptEnabled.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Missing_Value_Print", missingValue.Id, GraphPortDefaults.ValueOut, printMissing.Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "ScriptRuntimeGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("targetObject.IsEnabled = false", luau);
        Assert.Contains("targetObject.IsEnabled = true", luau);
        Assert.Contains("targetObject.IsEnabled = not (targetObject.IsEnabled == true)", luau);
        Assert.Contains("scriptObject:Call(tostring(\"Run\"), \"payload\")", luau);
        Assert.Contains("scriptObject:CallAsync(tostring(\"RunAsync\"), \"payload\")", luau);
        Assert.Contains("return scriptObject.IsEnabled == true", luau);
        Assert.Contains("return scriptObject.IsEnabled == false", luau);
        Assert.Contains("return scriptObject.Call ~= nil", luau);
        Assert.Contains("return scriptObject.CallAsync ~= nil", luau);
        Assert.Contains("return scriptObject ~= nil and not (scriptObject ~= nil and ((scriptObject.ClassName == \"MissingInstance\")", luau);
        Assert.Contains("return scriptObject == nil or (scriptObject ~= nil and ((scriptObject.ClassName == \"MissingInstance\")", luau);
        Assert.Contains("return (targetObject ~= nil and ((targetObject.ClassName == \"MissingInstance\") or (targetObject.IsA ~= nil and targetObject:IsA(\"MissingInstance\"))))", luau);
        Assert.Contains("return targetObject.IsEnabled == true", luau);
        Assert.Contains("Script Enabled stopped: target does not expose IsEnabled.", luau);
        Assert.DoesNotContain("Set Script Enabled is not implemented", luau);
        Assert.DoesNotContain("Object Is Missing Instance is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsScriptRuntimeWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var triggerIds = new[]
        {
            "EV_OnScriptEnabled",
            "EV_OnScriptDisabled",
            "EV_OnScriptEnabledChanged",
            "EV_OnScriptCallAvailable",
            "EV_OnScriptCallAsyncAvailable",
            "EV_OnScriptTargetMissing"
        };

        var triggers = triggerIds
            .Select(id => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: id))
            .ToList();

        foreach (var trigger in triggers)
        {
            SetConstant(trigger, "target", "World/Hidden/VRS Helper");
            SetConstant(trigger, "interval", "0.1");
        }

        var rule = new Rule
        {
            Id = "RULE_ScriptWatchers",
            Name = "ScriptWatchers",
            Nodes = triggers
        };
        var graph = new RuleGraph { Name = "ScriptWatcherGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("return triggerObject.IsEnabled == true", luau);
        Assert.Contains("return currentValue == false, currentValue", luau);
        Assert.Contains("local previousValue = readWatchedValue()", luau);
        Assert.Contains("return triggerObject.Call ~= nil", luau);
        Assert.Contains("return triggerObject.CallAsync ~= nil", luau);
        Assert.Contains("local targetMissing = triggerObject == nil or (triggerObject ~= nil and ((triggerObject.ClassName == \"MissingInstance\")", luau);
        Assert.Contains("scriptTargetMissing = currentMatched", luau);
        Assert.DoesNotContain("On Script Enabled is not implemented", luau);
        Assert.DoesNotContain("On Script Target Missing is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsAssetMediaNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartAssets");
        var setImage = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetPTImageAssetId"), stableId: "ACT_SetImageAssetId");
        var setAudio = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetPTAudioAssetId"), stableId: "ACT_SetAudioAssetId");
        var setMesh = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetPTMeshAssetId"), stableId: "ACT_SetMeshAssetId");
        var setMeshAnimation = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetPTMeshAnimationAssetId"), stableId: "ACT_SetMeshAnimationAssetId");
        var setMeshAnimationType = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetMeshAnimationType"), stableId: "ACT_SetMeshAnimationType");
        var setBuiltInAudio = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetBuiltInAudioPreset"), stableId: "ACT_SetBuiltInAudioPreset");
        var setBuiltInFont = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetBuiltInFontSettings"), stableId: "ACT_SetBuiltInFontSettings");
        var setFileLink = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetFileLinkAssetId"), stableId: "ACT_SetFileLinkAssetId");
        var setGradientSize = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetGradientImageSize"), stableId: "ACT_SetGradientImageSize");

        var printAssetRef = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintAssetReference");
        var printImageId = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintImageId");
        var printAudioPreset = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintAudioPreset");
        var printFontPreset = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintFontPreset");
        var printFileLink = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintFileLink");
        var printGradientWidth = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintGradientWidth");
        var printAnimationInfoName = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintAnimationInfoName");

        var assetReference = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_AssetReference"), stableId: "PROP_AssetReference");
        var imageId = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_PTImageAssetId"), stableId: "PROP_ImageAssetId");
        var audioPreset = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_BuiltInAudioPreset"), stableId: "PROP_AudioPreset");
        var fontPreset = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_BuiltInFontPreset"), stableId: "PROP_FontPreset");
        var fileLink = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_FileLinkAssetId"), stableId: "PROP_FileLinkAssetId");
        var gradientWidth = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_GradientImageWidth"), stableId: "PROP_GradientWidth");
        var animationInfoName = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_MeshAnimationInfoName"), stableId: "PROP_AnimationInfoName");

        SetConstant(setImage, "target", "World/Hidden/VRS/Assets/TestImageAsset");
        SetConstant(setImage, "assetId", "123");
        SetConstant(setAudio, "target", "World/Hidden/VRS/Assets/TestAudioAsset");
        SetConstant(setAudio, "assetId", "456");
        SetConstant(setMesh, "target", "World/Hidden/VRS/Assets/TestMeshAsset");
        SetConstant(setMesh, "assetId", "789");
        SetConstant(setMeshAnimation, "target", "World/Hidden/VRS/Assets/TestMeshAnimationAsset");
        SetConstant(setMeshAnimation, "assetId", "987");
        SetConstant(setMeshAnimationType, "target", "World/Hidden/VRS/Assets/TestMeshAnimationAsset");
        SetConstant(setMeshAnimationType, "animationType", "Looped");
        SetConstant(setBuiltInAudio, "target", "World/Hidden/VRS/Assets/TestBuiltInAudio");
        SetConstant(setBuiltInAudio, "preset", "Explosion");
        SetConstant(setBuiltInFont, "target", "World/Hidden/VRS/Assets/TestFont");
        SetConstant(setBuiltInFont, "preset", "Montserrat");
        SetConstant(setBuiltInFont, "weight", "Bold");
        SetConstant(setBuiltInFont, "style", "Italic");
        SetConstant(setFileLink, "target", "World/Hidden/VRS/Assets/TestFileLink");
        SetConstant(setFileLink, "linkedId", "file-1");
        SetConstant(setGradientSize, "target", "World/Hidden/VRS/Assets/TestGradient");
        SetConstant(setGradientSize, "width", "512");
        SetConstant(setGradientSize, "height", "128");

        SetConstant(assetReference, "target", "World/Hidden/VRS/Assets/TestImageAsset");
        SetConstant(imageId, "target", "World/Hidden/VRS/Assets/TestImageAsset");
        SetConstant(audioPreset, "target", "World/Hidden/VRS/Assets/TestBuiltInAudio");
        SetConstant(fontPreset, "target", "World/Hidden/VRS/Assets/TestFont");
        SetConstant(fileLink, "target", "World/Hidden/VRS/Assets/TestFileLink");
        SetConstant(gradientWidth, "target", "World/Hidden/VRS/Assets/TestGradient");

        var rule = new Rule
        {
            Id = "RULE_AssetMedia",
            Name = "AssetMedia",
            Nodes =
            [
                start,
                setImage,
                setAudio,
                setMesh,
                setMeshAnimation,
                setMeshAnimationType,
                setBuiltInAudio,
                setBuiltInFont,
                setFileLink,
                setGradientSize,
                printAssetRef,
                printImageId,
                printAudioPreset,
                printFontPreset,
                printFileLink,
                printGradientWidth,
                printAnimationInfoName,
                assetReference,
                imageId,
                audioPreset,
                fontPreset,
                fileLink,
                gradientWidth,
                animationInfoName
            ],
            Connections =
            [
                Flow("CONN_Assets_Start_SetImage", start.Id, GraphPortDefaults.FlowOut, setImage.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Assets_SetImage_SetAudio", setImage.Id, GraphPortDefaults.FlowOut, setAudio.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Assets_SetAudio_SetMesh", setAudio.Id, GraphPortDefaults.FlowOut, setMesh.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Assets_SetMesh_SetMeshAnimation", setMesh.Id, GraphPortDefaults.FlowOut, setMeshAnimation.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Assets_SetMeshAnimation_SetType", setMeshAnimation.Id, GraphPortDefaults.FlowOut, setMeshAnimationType.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Assets_SetType_SetAudioPreset", setMeshAnimationType.Id, GraphPortDefaults.FlowOut, setBuiltInAudio.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Assets_SetAudioPreset_SetFont", setBuiltInAudio.Id, GraphPortDefaults.FlowOut, setBuiltInFont.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Assets_SetFont_SetFileLink", setBuiltInFont.Id, GraphPortDefaults.FlowOut, setFileLink.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Assets_SetFileLink_SetGradient", setFileLink.Id, GraphPortDefaults.FlowOut, setGradientSize.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Assets_SetGradient_PrintAsset", setGradientSize.Id, GraphPortDefaults.FlowOut, printAssetRef.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Assets_PrintAsset_PrintImage", printAssetRef.Id, GraphPortDefaults.FlowOut, printImageId.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Assets_PrintImage_PrintAudio", printImageId.Id, GraphPortDefaults.FlowOut, printAudioPreset.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Assets_PrintAudio_PrintFont", printAudioPreset.Id, GraphPortDefaults.FlowOut, printFontPreset.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Assets_PrintFont_PrintFile", printFontPreset.Id, GraphPortDefaults.FlowOut, printFileLink.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Assets_PrintFile_PrintGradient", printFileLink.Id, GraphPortDefaults.FlowOut, printGradientWidth.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Assets_PrintGradient_PrintInfo", printGradientWidth.Id, GraphPortDefaults.FlowOut, printAnimationInfoName.Id, GraphPortDefaults.FlowIn),
                Value("CONN_Assets_Value_Asset", assetReference.Id, GraphPortDefaults.ValueOut, printAssetRef.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Assets_Value_ImageId", imageId.Id, GraphPortDefaults.ValueOut, printImageId.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Assets_Value_AudioPreset", audioPreset.Id, GraphPortDefaults.ValueOut, printAudioPreset.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Assets_Value_FontPreset", fontPreset.Id, GraphPortDefaults.ValueOut, printFontPreset.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Assets_Value_FileLink", fileLink.Id, GraphPortDefaults.ValueOut, printFileLink.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Assets_Value_GradientWidth", gradientWidth.Id, GraphPortDefaults.ValueOut, printGradientWidth.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Assets_Value_AnimationInfoName", animationInfoName.Id, GraphPortDefaults.ValueOut, printAnimationInfoName.Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "AssetMediaGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("targetObject.ImageID = 123", luau);
        Assert.Contains("targetObject.AudioID = 456", luau);
        Assert.Contains("targetObject.AssetID = 789", luau);
        Assert.Contains("targetObject.AssetID = 987", luau);
        Assert.Contains("targetObject.AnimationType = \"Looped\"", luau);
        Assert.Contains("targetObject.AudioPreset = \"Explosion\"", luau);
        Assert.Contains("fontAsset.FontPreset = tostring(\"Montserrat\")", luau);
        Assert.Contains("fontAsset.FontWeight = tostring(\"Bold\")", luau);
        Assert.Contains("fontAsset.FontStyle = tostring(\"Italic\")", luau);
        Assert.Contains("targetObject.LinkedID = \"file-1\"", luau);
        Assert.Contains("assetObject.Width = 512", luau);
        Assert.Contains("assetObject.Height = 128", luau);
        Assert.Contains("return assetObject", luau);
        Assert.Contains("return tonumber(targetObject.ImageID) or 0", luau);
        Assert.Contains("return tostring(targetObject.AudioPreset or \"\")", luau);
        Assert.Contains("return tostring(targetObject.FontPreset or \"\")", luau);
        Assert.Contains("return tostring(targetObject.LinkedID or \"\")", luau);
        Assert.Contains("return tonumber(targetObject.Width) or 0", luau);
        Assert.Contains("animation info is not an object", luau);
        Assert.DoesNotContain("Set PT Image Asset ID is not implemented", luau);
        Assert.DoesNotContain("Mesh Animation Info Name is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsAssetMediaConditions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_AssetConditionsStart");
        var conditionIds = new[]
        {
            "COND_DecalImageIs",
            "COND_DecalHasImage",
            "COND_PTImageAssetIdIs",
            "COND_PTAudioAssetIdIs",
            "COND_PTMeshAssetIdIs",
            "COND_PTMeshAnimationAssetIdIs",
            "COND_BuiltInAudioPresetIs",
            "COND_BuiltInFontPresetIs",
            "COND_FileLinkAssetIdIs",
            "COND_MeshAnimationTypeIs",
            "COND_GradientImageWidthAtLeast",
            "COND_GradientImageHeightAtLeast"
        };
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"COND_AssetMedia_{index}"))
            .ToList();
        var message = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_AssetConditionMessage");

        SetConstant(conditions[0], "target", "World/Decals/Poster");
        SetConstant(conditions[0], "image", "images/poster.png");
        SetConstant(conditions[0], "caseSensitive", "true");
        SetConstant(conditions[1], "target", "World/Decals/Poster");
        SetConstant(conditions[2], "target", "World/Hidden/VRS/Assets/Image");
        SetConstant(conditions[2], "assetId", "123");
        SetConstant(conditions[3], "target", "World/Hidden/VRS/Assets/Audio");
        SetConstant(conditions[3], "assetId", "456");
        SetConstant(conditions[4], "target", "World/Hidden/VRS/Assets/Mesh");
        SetConstant(conditions[4], "assetId", "789");
        SetConstant(conditions[5], "target", "World/Hidden/VRS/Assets/Animation");
        SetConstant(conditions[5], "assetId", "987");
        SetConstant(conditions[6], "target", "World/Hidden/VRS/Assets/BuiltInAudio");
        SetConstant(conditions[6], "preset", "Explosion");
        SetConstant(conditions[7], "target", "World/Hidden/VRS/Assets/Font");
        SetConstant(conditions[7], "preset", "Montserrat");
        SetConstant(conditions[8], "target", "World/Hidden/VRS/Assets/File");
        SetConstant(conditions[8], "linkedId", "file-1");
        SetConstant(conditions[9], "target", "World/Hidden/VRS/Assets/Animation");
        SetConstant(conditions[9], "animationType", "Looped");
        SetConstant(conditions[10], "target", "World/Hidden/VRS/Assets/Gradient");
        SetConstant(conditions[10], "width", "512");
        SetConstant(conditions[11], "target", "World/Hidden/VRS/Assets/Gradient");
        SetConstant(conditions[11], "height", "128");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_AssetCondition0", start.Id, GraphPortDefaults.FlowOut, conditions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < conditions.Count; index++)
        {
            var toNodeId = index + 1 < conditions.Count ? conditions[index + 1].Id : message.Id;
            connections.Add(Flow($"CONN_AssetCondition_{index}", conditions[index].Id, GraphPortDefaults.TrueOut, toNodeId, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_AssetConditions",
            Name = "AssetConditions",
            Nodes = [start, .. conditions, message],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "AssetConditionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local currentText = tostring(targetObject.Image or \"\")", luau);
        Assert.Contains("local expectedText = tostring(\"images/poster.png\")", luau);
        Assert.Contains("return tostring(targetObject.Image or \"\") ~= \"\"", luau);
        Assert.Contains("local currentNumber = tonumber(targetObject.ImageID) or 0", luau);
        Assert.Contains("local expectedNumber = tonumber(123) or 0", luau);
        Assert.Contains("local currentNumber = tonumber(targetObject.AudioID) or 0", luau);
        Assert.Contains("local currentNumber = tonumber(targetObject.AssetID) or 0", luau);
        Assert.Contains("local currentText = tostring(targetObject.AudioPreset or \"\")", luau);
        Assert.Contains("local currentText = tostring(targetObject.FontPreset or \"\")", luau);
        Assert.Contains("local currentText = tostring(targetObject.LinkedID or \"\")", luau);
        Assert.Contains("local currentText = tostring(targetObject.AnimationType or \"\")", luau);
        Assert.Contains("local currentNumber = tonumber(targetObject.Width) or 0", luau);
        Assert.Contains("local currentNumber = tonumber(targetObject.Height) or 0", luau);
        Assert.Contains("return currentNumber == expectedNumber", luau);
        Assert.Contains("return currentNumber >= expectedNumber", luau);
        Assert.DoesNotContain("Decal Image Is is not implemented", luau);
        Assert.DoesNotContain("Gradient Image Height At Least is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsAssetMediaWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var triggerIds = new[]
        {
            "EV_OnDecalImageChanged",
            "EV_OnPTImageAssetIdChanged",
            "EV_OnPTAudioAssetIdChanged",
            "EV_OnPTMeshAssetIdChanged",
            "EV_OnPTMeshAnimationAssetIdChanged",
            "EV_OnBuiltInAudioPresetChanged",
            "EV_OnBuiltInFontPresetChanged",
            "EV_OnFileLinkAssetIdChanged",
            "EV_OnMeshAnimationTypeChanged",
            "EV_OnGradientImageWidthReached",
            "EV_OnGradientImageHeightReached"
        };
        var triggers = triggerIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"TRG_AssetWatcher_{index}"))
            .ToList();
        var message = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_AssetWatcherMessage");

        SetConstant(triggers[0], "target", "World/Decals/Poster");
        SetConstant(triggers[1], "target", "World/Hidden/VRS/Assets/Image");
        SetConstant(triggers[2], "target", "World/Hidden/VRS/Assets/Audio");
        SetConstant(triggers[3], "target", "World/Hidden/VRS/Assets/Mesh");
        SetConstant(triggers[4], "target", "World/Hidden/VRS/Assets/Animation");
        SetConstant(triggers[5], "target", "World/Hidden/VRS/Assets/BuiltInAudio");
        SetConstant(triggers[6], "target", "World/Hidden/VRS/Assets/Font");
        SetConstant(triggers[7], "target", "World/Hidden/VRS/Assets/File");
        SetConstant(triggers[8], "target", "World/Hidden/VRS/Assets/Animation");
        SetConstant(triggers[9], "target", "World/Hidden/VRS/Assets/Gradient");
        SetConstant(triggers[9], "width", "512");
        SetConstant(triggers[10], "target", "World/Hidden/VRS/Assets/Gradient");
        SetConstant(triggers[10], "height", "128");

        var connections = triggers
            .Select((trigger, index) => Flow($"CONN_AssetWatcher_{index}", trigger.Id, GraphPortDefaults.FlowOut, message.Id, GraphPortDefaults.FlowIn))
            .ToList();

        var rule = new Rule
        {
            Id = "RULE_AssetWatchers",
            Name = "AssetWatchers",
            Nodes = [.. triggers, message],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "AssetWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("return tostring(triggerObject.Image or \"\")", luau);
        Assert.Contains("return tonumber(triggerObject.ImageID) or 0", luau);
        Assert.Contains("return tonumber(triggerObject.AudioID) or 0", luau);
        Assert.Contains("return tonumber(triggerObject.AssetID) or 0", luau);
        Assert.Contains("return tostring(triggerObject.AudioPreset or \"\")", luau);
        Assert.Contains("return tostring(triggerObject.FontPreset or \"\")", luau);
        Assert.Contains("return tostring(triggerObject.LinkedID or \"\")", luau);
        Assert.Contains("return tostring(triggerObject.AnimationType or \"\")", luau);
        Assert.Contains("local watchedLimit = tonumber(512) or 256", luau);
        Assert.Contains("local watchedLimit = tonumber(128) or 256", luau);
        Assert.Contains("return tonumber(triggerObject.Width) or 0", luau);
        Assert.Contains("return tonumber(triggerObject.Height) or 0", luau);
        Assert.Contains("return currentValue >= watchedLimit, currentValue", luau);
        Assert.Contains("local previousValue = readWatchedValue()", luau);
        Assert.Contains("local previousMatched = readMatched() == true", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, assetImage = currentValue }", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, imageWidth = currentValue }", luau);
        Assert.DoesNotContain("On Decal Image Changed is not implemented", luau);
        Assert.DoesNotContain("On Gradient Image Height Reached is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_UsesCatalogValueRecipesInline()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var startEntry = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var setVariableEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_SetScriptVariable");
        var messageEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var addEntry = catalog.Nodes.Single(node => node.IdBase == "PROP_AddNumbers");
        var readEntry = catalog.Nodes.Single(node => node.IdBase == "PROP_ReadScriptVariable");
        var trigger = NodeCatalogService.CreateNode(startEntry, 100, 100, "TRG_Start");
        var set = NodeCatalogService.CreateNode(setVariableEntry, 320, 100, "ACT_SetScore");
        var message = NodeCatalogService.CreateNode(messageEntry, 540, 100, "ACT_Message");
        var addRecipe = NodeCatalogService.CreateNode(addEntry);
        var readRecipe = NodeCatalogService.CreateNode(readEntry);

        SetConstant(set, "name", "Score");
        SetConstant(addRecipe, "left", "2");
        SetConstant(addRecipe, "right", "3");
        SetCatalogRecipe(set, "value", addEntry, addRecipe.Parameters);
        SetConstant(readRecipe, "name", "Score");
        SetCatalogRecipe(message, "message", readEntry, readRecipe.Parameters);

        var rule = new Rule
        {
            Id = "RULE_CatalogValueRecipes",
            Name = "CatalogValueRecipes",
            Nodes = [trigger, set, message],
            Connections =
            [
                Flow("CONN_Start_Set", trigger.Id, GraphPortDefaults.FlowOut, set.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Set_Message", set.Id, GraphPortDefaults.FlowOut, message.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "CatalogValueRecipeGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("VRS.vars[\"Score\"] = (2 + 3)", luau);
        Assert.DoesNotContain("local MESSAGE_TEXT = VRS.vars[\"Score\"]", luau);
        Assert.Contains("print(VRS.vars[\"Score\"])", luau);
        Assert.DoesNotContain("PROP_AddNumbers", luau.Split("-- [VSR] GENERATED GRAPH METADATA")[0]);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsPlayerEventContextAndChatPlayerTarget()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var joined = catalog.Nodes.Single(node => node.IdBase == "EV_OnPlayerJoined");
        var triggeringPlayer = catalog.Nodes.Single(node => node.IdBase == "PROP_TriggeringPlayer");
        var sendChat = catalog.Nodes.Single(node => node.IdBase == "ACT_SendChatMessageToPlayer");
        var trigger = NodeCatalogService.CreateNode(joined, 100, 100, "TRG_PlayerJoined");
        var player = NodeCatalogService.CreateNode(triggeringPlayer, 320, 60, "PROP_Player");
        var action = NodeCatalogService.CreateNode(sendChat, 540, 100, "ACT_SendChat");
        SetConstant(action, "message", "Welcome!");

        var rule = new Rule
        {
            Id = "RULE_PlayerJoinedChat",
            Name = "PlayerJoinedChat",
            Nodes = [trigger, player, action],
            Connections =
            [
                Flow("CONN_Joined_Chat", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn),
                Value("CONN_Player_Chat", player.Id, GraphPortDefaults.ValueOut, action.Id, GraphPortDefaults.ParameterPortId("player"))
            ]
        };
        var graph = new RuleGraph { Name = "PlayerJoinedChatGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("Players.PlayerAdded:Connect(function(player)", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, player = player }", luau);
        Assert.Contains("local targetPlayer = ((triggerContext ~= nil and triggerContext.player) or nil)", luau);
        Assert.Contains("Chat:UnicastMessage(tostring(\"Welcome!\"), targetPlayer)", luau);
        Assert.DoesNotContain("Send Chat Message To Player is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsChatMessageContextValue()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var chat = catalog.Nodes.Single(node => node.IdBase == "EV_OnChatMessage");
        var chatMessage = catalog.Nodes.Single(node => node.IdBase == "PROP_TriggeringChatMessage");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(chat, 100, 100, "TRG_Chat");
        var messageValue = NodeCatalogService.CreateNode(chatMessage, 320, 60, "PROP_ChatMessage");
        var action = NodeCatalogService.CreateNode(showMessage, 540, 100, "ACT_PrintChat");

        var rule = new Rule
        {
            Id = "RULE_ChatMessage",
            Name = "ChatMessage",
            Nodes = [trigger, messageValue, action],
            Connections =
            [
                Flow("CONN_Chat_Print", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn),
                Value("CONN_ChatMessage_Print", messageValue.Id, GraphPortDefaults.ValueOut, action.Id, GraphPortDefaults.ParameterPortId("message"))
            ]
        };
        var graph = new RuleGraph { Name = "ChatMessageGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("Chat.NewChatMessage:Connect(function(sender, message)", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, player = sender, message = message }", luau);
        Assert.Contains("print(tostring((triggerContext ~= nil and triggerContext.message) or \"\"))", luau);
        Assert.DoesNotContain("local MESSAGE_TEXT", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsClientInputTriggerAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var input = catalog.Nodes.Single(node => node.IdBase == "EV_OnInputButtonDown");
        var inputAction = catalog.Nodes.Single(node => node.IdBase == "PROP_TriggeringInputAction");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(input, 100, 100, "TRG_Input");
        var actionName = NodeCatalogService.CreateNode(inputAction, 320, 60, "PROP_InputAction");
        var message = NodeCatalogService.CreateNode(showMessage, 540, 100, "ACT_PrintInput");
        SetConstant(trigger, "actionName", "Interact");
        var rule = new Rule
        {
            Id = "RULE_Input",
            Name = "Input",
            ScriptKind = GraphScriptKind.Local,
            Nodes = [trigger, actionName, message],
            Connections =
            [
                Flow("CONN_Input_Print", trigger.Id, GraphPortDefaults.FlowOut, message.Id, GraphPortDefaults.FlowIn),
                Value("CONN_InputAction_Print", actionName.Id, GraphPortDefaults.ValueOut, message.Id, GraphPortDefaults.ParameterPortId("message"))
            ]
        };
        var graph = new RuleGraph { Name = "InputGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local inputActionName = tostring(\"Interact\")", luau);
        Assert.Contains("local buttonAction = Input:GetButton(inputActionName)", luau);
        Assert.Contains("buttonAction.Pressed:Connect(function()", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, inputAction = inputActionName, inputValue = true }", luau);
        Assert.Contains("print(tostring((triggerContext ~= nil and triggerContext.inputAction) or \"\"))", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsInputButtonBindingNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var bindKey = catalog.Nodes.Single(node => node.IdBase == "ACT_BindInputButtonKey");
        var actionExists = catalog.Nodes.Single(node => node.IdBase == "COND_InputActionExists");
        var buttonFromKey = catalog.Nodes.Single(node => node.IdBase == "PROP_InputButtonFromKey");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(start, 100, 100, "TRG_Start");
        var bind = NodeCatalogService.CreateNode(bindKey, 320, 100, "ACT_BindKey");
        var condition = NodeCatalogService.CreateNode(actionExists, 540, 100, "COND_InputExists");
        var inputButton = NodeCatalogService.CreateNode(buttonFromKey, 540, 20, "PROP_InputButton");
        var message = NodeCatalogService.CreateNode(showMessage, 760, 100, "ACT_PrintButton");
        SetConstant(bind, "actionName", "Interact");
        SetConstant(bind, "keyCode", "Space");
        SetConstant(condition, "actionKind", "Axis");
        SetConstant(condition, "actionName", "Horizontal");
        SetConstant(inputButton, "keyCode", "MouseLeft");
        var rule = new Rule
        {
            Id = "RULE_InputBinding",
            Name = "InputBinding",
            ScriptKind = GraphScriptKind.Local,
            Nodes = [trigger, bind, condition, inputButton, message],
            Connections =
            [
                Flow("CONN_Start_Bind", trigger.Id, GraphPortDefaults.FlowOut, bind.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Bind_Check", bind.Id, GraphPortDefaults.FlowOut, condition.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Check_Message", condition.Id, GraphPortDefaults.FlowOut, message.Id, GraphPortDefaults.FlowIn),
                Value("CONN_InputButton_Message", inputButton.Id, GraphPortDefaults.ValueOut, message.Id, GraphPortDefaults.ParameterPortId("message"))
            ]
        };
        var graph = new RuleGraph { Name = "InputBindingGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local buttonAction = Input:BindButton(inputActionName)", luau);
        Assert.Contains("buttonAction.Buttons:AddButton(InputButton.New(KeyCode.Space))", luau);
        Assert.Contains("if Input == nil or Input.GetAxis == nil then", luau);
        Assert.Contains("return Input:GetAxis(tostring(\"Horizontal\")) ~= nil", luau);
        Assert.Contains("return InputButton.New(KeyCode.MouseLeft)", luau);
        Assert.DoesNotContain("Bind Input Button Key is not implemented", luau);
        Assert.DoesNotContain("Input Action Exists is not implemented", luau);
        Assert.DoesNotContain("Input Button From Key is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsClientInputNetworkEventSend()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var input = catalog.Nodes.Single(node => node.IdBase == "EV_OnInputButtonDown");
        var sendInput = catalog.Nodes.Single(node => node.IdBase == "ACT_SendInputEvent");
        var trigger = NodeCatalogService.CreateNode(input, 100, 100, "TRG_Input");
        var action = NodeCatalogService.CreateNode(sendInput, 340, 100, "ACT_SendInput");
        SetConstant(trigger, "actionName", "Jump");
        SetConstant(action, "inputAction", "Jump");
        var rule = new Rule
        {
            Id = "RULE_InputSend",
            Name = "InputSend",
            ScriptKind = GraphScriptKind.Local,
            Nodes = [trigger, action],
            Connections =
            [
                Flow("CONN_Input_Send", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "InputSendGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function vrsResolveInputNetworkEvent(actionName)", luau);
        Assert.Contains("\"User Input (NetworkEvent)\", \"Input Manager\", inputEventName", luau);
        Assert.Contains("\"User Input (NetworkEvent)\", inputEventName", luau);
        Assert.Contains("\"Input\", inputEventName", luau);
        Assert.Contains("local inputEvent = vrsResolveInputNetworkEvent(inputActionName)", luau);
        Assert.Contains("local inputMessage = NetMessage:New()", luau);
        Assert.Contains("inputEvent:InvokeServer(inputMessage)", luau);
        Assert.Contains("Run VRS Input Manager first", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsClientInputTextNetworkEventSend()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var input = catalog.Nodes.Single(node => node.IdBase == "EV_OnInputButtonDown");
        var sendInput = catalog.Nodes.Single(node => node.IdBase == "ACT_SendInputTextEvent");
        var trigger = NodeCatalogService.CreateNode(input, 100, 100, "TRG_InputText");
        var action = NodeCatalogService.CreateNode(sendInput, 340, 100, "ACT_SendInputText");
        SetConstant(trigger, "actionName", "Interact");
        SetConstant(action, "inputAction", "Interact");
        SetConstant(action, "payload", "OpenDoor");
        var rule = new Rule
        {
            Id = "RULE_InputTextSend",
            Name = "InputTextSend",
            ScriptKind = GraphScriptKind.Local,
            Nodes = [trigger, action],
            Connections =
            [
                Flow("CONN_Input_SendText", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "InputTextSendGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local inputMessage = NetMessage:New()", luau);
        Assert.Contains("inputMessage:AddString(tostring(\"OpenDoor\"))", luau);
        Assert.Contains("inputEvent:InvokeServer(inputMessage)", luau);
        Assert.Contains("Send Input Text Event warning", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsServerInputNetworkEventTrigger()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var inputEvent = catalog.Nodes.Single(node => node.IdBase == "EV_OnVrsInputEvent");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(inputEvent, 100, 100, "TRG_InputEvent");
        var action = NodeCatalogService.CreateNode(showMessage, 340, 100, "ACT_Message");
        SetConstant(trigger, "inputAction", "Jump");
        SetConstant(action, "message", "Jump received");
        var rule = new Rule
        {
            Id = "RULE_InputEvent",
            Name = "InputEvent",
            ScriptKind = GraphScriptKind.Server,
            Nodes = [trigger, action],
            Connections =
            [
                Flow("CONN_InputEvent_Message", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "InputEventGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function vrsResolveInputNetworkEvent(actionName)", luau);
        Assert.Contains("inputEvent.InvokedServer:Connect(function(player, inputMessage)", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, player = player, inputAction = inputActionName, inputMessage = inputMessage, message = inputMessage }", luau);
        Assert.Contains("VRS.actions.showMessage(triggerObject, triggerContext)", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsServerInputTextValue()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var inputEvent = catalog.Nodes.Single(node => node.IdBase == "EV_OnVrsInputEvent");
        var inputText = catalog.Nodes.Single(node => node.IdBase == "PROP_TriggeringInputText");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(inputEvent, 100, 100, "TRG_InputTextEvent");
        var property = NodeCatalogService.CreateNode(inputText, 320, 60, "PROP_InputText");
        var action = NodeCatalogService.CreateNode(showMessage, 540, 100, "ACT_InputTextMessage");
        SetConstant(trigger, "inputAction", "Interact");
        var rule = new Rule
        {
            Id = "RULE_InputTextEvent",
            Name = "InputTextEvent",
            ScriptKind = GraphScriptKind.Server,
            Nodes = [trigger, property, action],
            Connections =
            [
                Flow("CONN_InputTextEvent_Message", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn),
                Value("CONN_InputText_Message", property.Id, GraphPortDefaults.ValueOut, action.Id, GraphPortDefaults.ParameterPortId("message"))
            ]
        };
        var graph = new RuleGraph { Name = "InputTextEventGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("inputEvent.InvokedServer:Connect(function(player, inputMessage)", luau);
        Assert.Contains("inputMessage:GetString(1)", luau);
        Assert.Contains("VRS.actions.showMessage(triggerObject, triggerContext)", luau);
        Assert.DoesNotContain("Triggering Input Text is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsBindableEventTriggerActionAndPayload()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var bindableTrigger = catalog.Nodes.Single(node => node.IdBase == "EV_OnBindableEvent");
        var fireBindable = catalog.Nodes.Single(node => node.IdBase == "ACT_FireBindableEvent");
        var payloadValue = catalog.Nodes.Single(node => node.IdBase == "PROP_TriggeringBindablePayload");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(bindableTrigger, 100, 100, "TRG_Bindable");
        var fire = NodeCatalogService.CreateNode(fireBindable, 340, 100, "ACT_FireBindable");
        var payload = NodeCatalogService.CreateNode(payloadValue, 340, 260, "PROP_BindablePayload");
        var message = NodeCatalogService.CreateNode(showMessage, 580, 100, "ACT_BindableMessage");
        var bindablePath = $"{VrsRuntimeEventPaths.ServerScriptBindableEventsPath}/Opened";
        SetSceneObject(trigger, "target", bindablePath);
        SetSceneObject(fire, "target", bindablePath);
        SetConstant(fire, "payload", "DoorOpened");

        var rule = new Rule
        {
            Id = "RULE_BindableEvent",
            Name = "BindableEvent",
            ScriptKind = GraphScriptKind.Server,
            Nodes = [trigger, fire, payload, message],
            Connections =
            [
                Flow("CONN_Bindable_Fire", trigger.Id, GraphPortDefaults.FlowOut, fire.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Bindable_Fire_Message", fire.Id, GraphPortDefaults.FlowOut, message.Id, GraphPortDefaults.FlowIn),
                Value("CONN_BindablePayload_Message", payload.Id, GraphPortDefaults.ValueOut, message.Id, GraphPortDefaults.ParameterPortId("message"))
            ]
        };
        var graph = new RuleGraph { Name = "BindableEventGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function resolveTarget(triggerObject, targetName)", luau);
        Assert.Contains("bindableEvent.Invoked:Connect(function(payload)", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, bindableEvent = bindableEvent, payload = payload, message = payload }", luau);
        Assert.Contains($"local bindableEvent = resolveTarget(triggerObject, \"{bindablePath}\")", luau);
        Assert.Contains("bindableEvent:Invoke(\"DoorOpened\")", luau);
        Assert.Contains("triggerContext.payload", luau);
        Assert.DoesNotContain("Fire Bindable Event is not implemented", luau);
        Assert.DoesNotContain("Bindable Event Payload Text is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsApiCoveragePriorityPackNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartApiPacks");
        var setVisible = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetUIVisible"), stableId: "ACT_SetVisible");
        var setImage = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetUIImage"), stableId: "ACT_SetImage");
        var setFov = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetCameraFOV"), stableId: "ACT_SetFov");
        var setValue = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetValueObjectValue"), stableId: "ACT_SetValue");
        var setDecal = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetDecalImage"), stableId: "ACT_SetDecal");
        var propertyIds = new[]
        {
            "PROP_UIVisible",
            "PROP_UIImage",
            "PROP_CameraFOV",
            "PROP_ValueObjectValue",
            "PROP_DecalImage"
        };
        var properties = propertyIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_ApiPack_{index}"))
            .ToList();
        var prints = propertyIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintApiPack_{index}"))
            .ToList();

        SetSceneObject(setVisible, "target", "World/PlayerGUI/Hud");
        SetSceneObject(setImage, "target", "World/PlayerGUI/Hud/Icon");
        SetSceneObject(setFov, "target", "World/Camera");
        SetSceneObject(setValue, "target", "World/Hidden/VRS/Values/Score");
        SetSceneObject(setDecal, "target", "World/Environment/LogoDecal");
        SetConstant(setVisible, "visible", "true");
        SetConstant(setImage, "image", "ui://coin");
        SetConstant(setFov, "fov", "80");
        SetConstant(setValue, "value", "25");
        SetConstant(setDecal, "image", "image://logo");

        SetSceneObject(properties.Single(node => node.CatalogId == "PROP_UIVisible"), "target", "World/PlayerGUI/Hud");
        SetSceneObject(properties.Single(node => node.CatalogId == "PROP_UIImage"), "target", "World/PlayerGUI/Hud/Icon");
        SetSceneObject(properties.Single(node => node.CatalogId == "PROP_CameraFOV"), "target", "World/Camera");
        SetSceneObject(properties.Single(node => node.CatalogId == "PROP_ValueObjectValue"), "target", "World/Hidden/VRS/Values/Score");
        SetSceneObject(properties.Single(node => node.CatalogId == "PROP_DecalImage"), "target", "World/Environment/LogoDecal");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_Visible", start.Id, GraphPortDefaults.FlowOut, setVisible.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Visible_Image", setVisible.Id, GraphPortDefaults.FlowOut, setImage.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Image_Fov", setImage.Id, GraphPortDefaults.FlowOut, setFov.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Fov_Value", setFov.Id, GraphPortDefaults.FlowOut, setValue.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Value_Decal", setValue.Id, GraphPortDefaults.FlowOut, setDecal.Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < properties.Count; index++)
        {
            connections.Add(Value($"CONN_ApiPackValue_Print_{index}", properties[index].Id, GraphPortDefaults.ValueOut, prints[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < prints.Count)
            {
                connections.Add(Flow($"CONN_PrintApiPack_{index}_{index + 1}", prints[index].Id, GraphPortDefaults.FlowOut, prints[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_ApiCoveragePacks",
            Name = "ApiCoveragePacks",
            Nodes = [start, setVisible, setImage, setFov, setValue, setDecal, .. properties, .. prints],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "ApiCoveragePacksGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("uiObject.Visible = true", luau);
        Assert.Contains("imageObject.Image = tostring(\"ui://coin\")", luau);
        Assert.Contains("targetObject.FOV = 80", luau);
        Assert.Contains("targetObject.Value = 25", luau);
        Assert.Contains("targetObject.Image = \"image://logo\"", luau);
        Assert.Contains("return targetObject.Visible == true", luau);
        Assert.Contains("return tostring(targetObject.Image or \"\")", luau);
        Assert.Contains("return tonumber(targetObject.FOV) or 0", luau);
        Assert.Contains("return targetObject.Value", luau);
        Assert.DoesNotContain("Set UI Visible is not implemented", luau);
        Assert.DoesNotContain("Set Camera FOV is not implemented", luau);
        Assert.DoesNotContain("Set Value Object is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsIntegerAndInstanceValueObjectNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartValueObjects");
        var setInteger = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetIntegerValueObject"), stableId: "ACT_SetInteger");
        var setInstance = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetInstanceValueObject"), stableId: "ACT_SetInstance");
        var integerValue = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_IntegerValueObject"), stableId: "PROP_IntegerValue");
        var instanceValue = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_InstanceValueObject"), stableId: "PROP_InstanceValue");
        var printInteger = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintInteger");
        var printInstance = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintInstance");

        SetSceneObject(setInteger, "target", "World/Hidden/VRS/Values/ScoreInt");
        SetSceneObject(setInstance, "target", "World/Hidden/VRS/Values/TargetRef");
        SetSceneObject(setInstance, "value", "World/Environment/Kill Brick");
        SetSceneObject(integerValue, "target", "World/Hidden/VRS/Values/ScoreInt");
        SetSceneObject(instanceValue, "target", "World/Hidden/VRS/Values/TargetRef");
        SetConstant(setInteger, "value", "42.8");

        var rule = new Rule
        {
            Id = "RULE_ValueObjects",
            Name = "ValueObjects",
            ScriptKind = GraphScriptKind.Server,
            Nodes = [start, setInteger, setInstance, integerValue, instanceValue, printInteger, printInstance],
            Connections =
            [
                Flow("CONN_Start_SetInteger", start.Id, GraphPortDefaults.FlowOut, setInteger.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_SetInteger_SetInstance", setInteger.Id, GraphPortDefaults.FlowOut, setInstance.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_SetInstance_PrintInteger", setInstance.Id, GraphPortDefaults.FlowOut, printInteger.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PrintInteger_PrintInstance", printInteger.Id, GraphPortDefaults.FlowOut, printInstance.Id, GraphPortDefaults.FlowIn),
                Value("CONN_Integer_Print", integerValue.Id, GraphPortDefaults.ValueOut, printInteger.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Instance_Print", instanceValue.Id, GraphPortDefaults.ValueOut, printInstance.Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "ValueObjectsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local integerValue = math.floor(tonumber(42.8) or 0)", luau);
        Assert.Contains("targetObject.Value = integerValue", luau);
        Assert.Contains("local storedObject = resolveTarget(triggerObject, \"World/Environment/Kill Brick\")", luau);
        Assert.Contains("targetObject.Value = storedObject", luau);
        Assert.Contains("return math.floor(tonumber(targetObject.Value) or 0)", luau);
        Assert.Contains("return targetObject.Value", luau);
        Assert.DoesNotContain("Set Integer Value Object is not implemented", luau);
        Assert.DoesNotContain("Set Stored Object Reference is not implemented", luau);
        Assert.DoesNotContain("Integer Value Object is not implemented", luau);
        Assert.DoesNotContain("Stored Object Reference is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsWorldInfoValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartWorldInfo");
        var valueIds = new[]
        {
            "PROP_WorldIsLocalTest",
            "PROP_WorldIsOldFormat",
            "PROP_WorldIdentifier",
            "PROP_ServerIdentifier",
            "PROP_WorldUptime",
            "PROP_ServerTime",
            "PROP_WorldObjectCount"
        };
        var values = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_WorldInfo_{index}"))
            .ToList();
        var prints = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintWorldInfo_{index}"))
            .ToList();

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_PrintWorld0", start.Id, GraphPortDefaults.FlowOut, prints[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < values.Count; index++)
        {
            connections.Add(Value($"CONN_WorldInfo_{index}", values[index].Id, GraphPortDefaults.ValueOut, prints[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < prints.Count)
            {
                connections.Add(Flow($"CONN_PrintWorldInfo_{index}_{index + 1}", prints[index].Id, GraphPortDefaults.FlowOut, prints[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_WorldInfo",
            Name = "WorldInfo",
            ScriptKind = GraphScriptKind.Server,
            Nodes = [start, .. values, .. prints],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "WorldInfoGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("return World.IsLocalTest == true", luau);
        Assert.Contains("return World.IsLegacyWorld == true", luau);
        Assert.Contains("return tostring(World.WorldID or \"\")", luau);
        Assert.Contains("return tostring(World.ServerID or \"\")", luau);
        Assert.Contains("return tonumber(World.UpTime) or 0", luau);
        Assert.Contains("return tonumber(World.ServerTime) or 0", luau);
        Assert.Contains("return tonumber(World.InstanceCount) or 0", luau);
        Assert.DoesNotContain("Local Test Is Running is not implemented", luau);
        Assert.DoesNotContain("World Object Count is not implemented", luau);
    }

    [Theory]
    [InlineData("EV_OnStart", "local triggerContext = { object = triggerObject }")]
    [InlineData("EV_OnTimerTick", "local triggerContext = { object = triggerObject }")]
    [InlineData("EV_OnPlayerJoined", "local triggerContext = { object = triggerObject, player = player }")]
    [InlineData("EV_OnPlayerLeft", "local triggerContext = { object = triggerObject, player = player }")]
    [InlineData("EV_OnChatMessage", "local triggerContext = { object = triggerObject, player = sender, message = message }")]
    [InlineData("EV_OnInputButtonDown", "local triggerContext = { object = triggerObject, inputAction = inputActionName, inputValue = true }")]
    [InlineData("EV_OnVrsInputEvent", "local triggerContext = { object = triggerObject, player = player, inputAction = inputActionName, inputMessage = inputMessage, message = inputMessage }")]
    [InlineData("EV_OnBindableEvent", "local triggerContext = { object = triggerObject, bindableEvent = bindableEvent, payload = payload, message = payload }")]
    [InlineData("EV_OnPlayerRespawned", "local triggerContext = { object = triggerObject, player = player }")]
    public void ExportRuleToLuau_TriggerTargetsResolveConfiguredSceneObject(string triggerId, string expectedContext)
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var triggerEntry = catalog.Nodes.Single(node => node.IdBase == triggerId);
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(triggerEntry, 100, 100, $"TRG_{triggerEntry.Type}");
        var action = NodeCatalogService.CreateNode(showMessage, 360, 100, $"ACT_{triggerEntry.Type}_Message");
        SetSceneObject(trigger, "target", "World/Environment/TriggerOwner");

        var rule = new Rule
        {
            Id = $"RULE_{triggerEntry.Type}",
            Name = triggerEntry.Type,
            ScriptKind = triggerId == "EV_OnInputButtonDown" ? GraphScriptKind.Local : GraphScriptKind.Server,
            Nodes = [trigger, action],
            Connections =
            [
                Flow($"CONN_{triggerEntry.Type}_Message", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = $"{triggerEntry.Type}TargetGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function resolveTarget(triggerObject, targetName)", luau);
        Assert.Contains("local scriptParent = script.Parent", luau);
        Assert.Contains("local triggerObject = resolveTarget(scriptParent,", luau);
        Assert.Contains("World/Environment/TriggerOwner", luau);
        Assert.Contains(expectedContext, luau);
        Assert.Contains("VRS.actions.showMessage(triggerObject, triggerContext)", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsStateChangedPollingTrigger()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var stateChanged = catalog.Nodes.Single(node => node.IdBase == "EV_OnStateChanged");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(stateChanged, 100, 100, "TRG_StateChanged");
        var action = NodeCatalogService.CreateNode(showMessage, 360, 100, "ACT_StateChangedMessage");
        SetSceneObject(trigger, "target", "World/Environment/Watcher");
        SetConstant(trigger, "state", "DoorOpen");
        SetConstant(trigger, "interval", "0.5");

        var rule = new Rule
        {
            Id = "RULE_StateChanged",
            Name = "StateChanged",
            Nodes = [trigger, action],
            Connections =
            [
                Flow("CONN_StateChanged_Message", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "StateChangedGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local watchedStateName = tostring(\"DoorOpen\")", luau);
        Assert.Contains("local previousValue = VRS.states[watchedStateName] == true", luau);
        Assert.Contains("wait(0.5)", luau);
        Assert.Contains("local currentValue = VRS.states[watchedStateName] == true", luau);
        Assert.Contains("if currentValue ~= previousValue then", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, stateName = watchedStateName, stateValue = currentValue }", luau);
        Assert.Contains("local triggerObject = resolveTarget(scriptParent,", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsScriptVariableChangedPollingTrigger()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var variableChanged = catalog.Nodes.Single(node => node.IdBase == "EV_OnScriptVariableChanged");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(variableChanged, 100, 100, "TRG_VariableChanged");
        var action = NodeCatalogService.CreateNode(showMessage, 360, 100, "ACT_VariableChangedMessage");
        SetSceneObject(trigger, "target", "World/Environment/Watcher");
        SetConstant(trigger, "name", "Score");
        SetConstant(trigger, "interval", "0.25");

        var rule = new Rule
        {
            Id = "RULE_VariableChanged",
            Name = "VariableChanged",
            Nodes = [trigger, action],
            Connections =
            [
                Flow("CONN_VariableChanged_Message", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "VariableChangedGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local watchedVariableName = tostring(\"Score\")", luau);
        Assert.Contains("local previousValue = VRS.vars[watchedVariableName]", luau);
        Assert.Contains("wait(0.25)", luau);
        Assert.Contains("local currentValue = VRS.vars[watchedVariableName]", luau);
        Assert.Contains("if currentValue ~= previousValue then", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, variableName = watchedVariableName, variableValue = currentValue }", luau);
        Assert.Contains("local triggerObject = resolveTarget(scriptParent,", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsGenericTouchObjectTrigger()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var touch = catalog.Nodes.Single(node => node.IdBase == "EV_OnTouchObject");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(touch, 100, 100, "TRG_TouchObject");
        var action = NodeCatalogService.CreateNode(showMessage, 360, 100, "ACT_TouchMessage");
        SetSceneObject(trigger, "target", "World/Environment/TouchPad");

        var rule = new Rule
        {
            Id = "RULE_TouchObject",
            Name = "TouchObject",
            Nodes = [trigger, action],
            Connections =
            [
                Flow("CONN_Touch_Message", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "TouchObjectGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local listenObject = resolveTarget(scriptParent,", luau);
        Assert.Contains("listenObject.Touched:Connect(function(hit)", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, touchObject = hit, touchObjectSource = triggerObject }", luau);
        Assert.DoesNotContain("local touchingPlayer = vrsResolveTouchingPlayer(hit)", luau);
        Assert.Contains("VRS.actions.showMessage(triggerObject, triggerContext)", luau);
    }

    [Fact]
    public void ExportRuleToLuau_ObjectClickedConnectsConfirmedPhysicalClickEvent()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var click = catalog.Nodes.Single(node => node.IdBase == "EV_OnObjectClicked");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(click, 100, 100, "TRG_ObjectClicked");
        var action = NodeCatalogService.CreateNode(showMessage, 360, 100, "ACT_ClickMessage");
        SetSceneObject(trigger, "target", "World/Environment/Button");

        var rule = new Rule
        {
            Id = "RULE_ObjectClicked",
            Name = "ObjectClicked",
            Nodes = [trigger, action],
            Connections =
            [
                Flow("CONN_Click_Message", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "ObjectClickedGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("listenObject.Clicked:Connect(function(player)", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, player = player }", luau);
        Assert.Contains("VRS.actions.showMessage(triggerObject, triggerContext)", luau);
        Assert.DoesNotContain("object click events are not available in the confirmed Polytoria API yet", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsAfterDelayTrigger()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var afterDelay = catalog.Nodes.Single(node => node.IdBase == "EV_AfterDelay");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(afterDelay, 100, 100, "TRG_AfterDelay");
        var action = NodeCatalogService.CreateNode(showMessage, 360, 100, "ACT_AfterDelayMessage");
        SetSceneObject(trigger, "target", "World/Environment/TimerOwner");
        SetConstant(trigger, "duration", "2");

        var rule = new Rule
        {
            Id = "RULE_AfterDelay",
            Name = "AfterDelay",
            Nodes = [trigger, action],
            Connections =
            [
                Flow("CONN_AfterDelay_Message", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "AfterDelayGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function afterDelay()", luau);
        Assert.Contains("wait(2)", luau);
        Assert.Contains("local triggerObject = resolveTarget(scriptParent,", luau);
        Assert.Contains("local triggerContext = { object = triggerObject }", luau);
        Assert.Contains("VRS.actions.showMessage(triggerObject, triggerContext)", luau);
        Assert.DoesNotContain("local function onStart()", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsRoundAndGateTransitionTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var triggerIds = new[]
        {
            "EV_OnRoundStarted",
            "EV_OnRoundEnded",
            "EV_OnRoundTimeExpired",
            "EV_OnGateOpened",
            "EV_OnGateClosed"
        };
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var triggers = triggerIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), 100, 100 + index * 40, $"TRG_{id}"))
            .ToList();
        var actions = triggers
            .Select((trigger, index) => NodeCatalogService.CreateNode(showMessage, 360, 100 + index * 40, $"ACT_{trigger.CatalogId}_Message"))
            .ToList();

        foreach (var trigger in triggers.Where(node => node.CatalogId.Contains("Round", StringComparison.OrdinalIgnoreCase)))
        {
            SetConstant(trigger, "roundName", "Arena");
            SetConstant(trigger, "interval", "0.5");
        }

        foreach (var trigger in triggers.Where(node => node.CatalogId.Contains("Gate", StringComparison.OrdinalIgnoreCase)))
        {
            SetConstant(trigger, "gateName", "Door");
            SetConstant(trigger, "interval", "0.75");
        }

        var rule = new Rule
        {
            Id = "RULE_RoundGateTriggers",
            Name = "RoundGateTriggers",
            Nodes = [.. triggers, .. actions],
            Connections = triggers
                .Select((trigger, index) => Flow($"CONN_{trigger.CatalogId}_Message", trigger.Id, GraphPortDefaults.FlowOut, actions[index].Id, GraphPortDefaults.FlowIn))
                .ToList()
        };
        var graph = new RuleGraph { Name = "RoundGateTriggersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function vrsNow()", luau);
        Assert.Contains("local watchedName = tostring(\"Arena\")", luau);
        Assert.Contains("local stateKey = (\"round:\" .. watchedName)", luau);
        Assert.Contains("local previousValue = VRS.states[stateKey .. \":running\"] == true", luau);
        Assert.Contains("if currentValue == true then", luau);
        Assert.Contains("if currentValue == false then", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, roundName = watchedName, roundRunning = currentValue }", luau);
        Assert.Contains("local roundKey = (\"round:\" .. watchedName)", luau);
        Assert.Contains("return VRS.states[roundKey .. \":running\"] == true and endAt ~= nil and vrsNow() >= endAt", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, roundName = watchedName, roundTimeExpired = currentValue }", luau);
        Assert.Contains("local watchedName = tostring(\"Door\")", luau);
        Assert.Contains("local stateKey = (\"gate:\" .. watchedName)", luau);
        Assert.Contains("local previousValue = VRS.states[stateKey] == true", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, gateName = watchedName, gateOpen = currentValue }", luau);
        Assert.Contains("wait(0.5)", luau);
        Assert.Contains("wait(0.75)", luau);
        Assert.DoesNotContain("local function onStart()", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsObjectVisibilityTransitionTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var visible = catalog.Nodes.Single(node => node.IdBase == "EV_OnObjectBecameVisible");
        var hidden = catalog.Nodes.Single(node => node.IdBase == "EV_OnObjectBecameHidden");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var visibleTrigger = NodeCatalogService.CreateNode(visible, 100, 100, "TRG_ObjectVisible");
        var hiddenTrigger = NodeCatalogService.CreateNode(hidden, 100, 180, "TRG_ObjectHidden");
        var visibleAction = NodeCatalogService.CreateNode(showMessage, 360, 100, "ACT_ObjectVisibleMessage");
        var hiddenAction = NodeCatalogService.CreateNode(showMessage, 360, 180, "ACT_ObjectHiddenMessage");
        SetSceneObject(visibleTrigger, "target", "World/Environment/Door");
        SetSceneObject(hiddenTrigger, "target", "World/Environment/Door");
        SetConstant(visibleTrigger, "interval", "0.4");
        SetConstant(hiddenTrigger, "interval", "0.4");

        var rule = new Rule
        {
            Id = "RULE_ObjectVisibilityTriggers",
            Name = "ObjectVisibilityTriggers",
            Nodes = [visibleTrigger, hiddenTrigger, visibleAction, hiddenAction],
            Connections =
            [
                Flow("CONN_Visible_Message", visibleTrigger.Id, GraphPortDefaults.FlowOut, visibleAction.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Hidden_Message", hiddenTrigger.Id, GraphPortDefaults.FlowOut, hiddenAction.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "ObjectVisibilityTriggersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function readObjectVisible()", luau);
        Assert.Contains("if triggerObject.Visible ~= nil then", luau);
        Assert.Contains("return triggerObject.Visible == true", luau);
        Assert.Contains("if triggerObject.Transparency ~= nil then", luau);
        Assert.Contains("return triggerObject.Transparency < 1", luau);
        Assert.Contains("if currentValue == true then", luau);
        Assert.Contains("if currentValue == false then", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, objectVisible = currentValue }", luau);
        Assert.Contains("wait(0.4)", luau);
        Assert.Contains("World/Environment/Door", luau);
        Assert.DoesNotContain("local function onStart()", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsObjectPhysicsWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var triggerIds = new[]
        {
            "EV_OnObjectCollisionTurnedOn",
            "EV_OnObjectCollisionTurnedOff",
            "EV_OnObjectAnchored",
            "EV_OnObjectUnanchored"
        };
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var triggers = triggerIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), 100, 100 + index * 40, $"TRG_{id}"))
            .ToList();
        var actions = triggers
            .Select((trigger, index) => NodeCatalogService.CreateNode(showMessage, 360, 100 + index * 40, $"ACT_{trigger.CatalogId}_Message"))
            .ToList();

        foreach (var trigger in triggers)
        {
            SetSceneObject(trigger, "target", "World/Environment/MovingPart");
            SetConstant(trigger, "interval", "0.2");
        }

        var rule = new Rule
        {
            Id = "RULE_ObjectPhysicsWatchers",
            Name = "ObjectPhysicsWatchers",
            Nodes = [.. triggers, .. actions],
            Connections = triggers
                .Select((trigger, index) => Flow($"CONN_{trigger.CatalogId}_Message", trigger.Id, GraphPortDefaults.FlowOut, actions[index].Id, GraphPortDefaults.FlowIn))
                .ToList()
        };
        var graph = new RuleGraph { Name = "ObjectPhysicsWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function readWatchedObjectValue()", luau);
        Assert.Contains("if triggerObject.CanCollide == nil then", luau);
        Assert.Contains("return triggerObject.CanCollide == true", luau);
        Assert.Contains("if triggerObject.Anchored == nil then", luau);
        Assert.Contains("return triggerObject.Anchored == true", luau);
        Assert.Contains("local previousValue = readWatchedObjectValue()", luau);
        Assert.Contains("if currentValue == true then", luau);
        Assert.Contains("if currentValue == false then", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, objectCollisionOn = currentValue }", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, objectAnchored = currentValue }", luau);
        Assert.Contains("wait(0.2)", luau);
        Assert.Contains("World/Environment/MovingPart", luau);
        Assert.DoesNotContain("local function onStart()", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsObjectAnchoredConditions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var anchored = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_ObjectIsAnchored"), 100, 100, "COND_ObjectAnchored");
        var unanchored = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_ObjectIsUnanchored"), 100, 180, "COND_ObjectUnanchored");
        SetSceneObject(anchored, "target", "World/Environment/MovingPart");
        SetSceneObject(unanchored, "target", "World/Environment/MovingPart");

        var rule = new Rule
        {
            Id = "RULE_ObjectAnchoredConditions",
            Name = "ObjectAnchoredConditions",
            Nodes = [anchored, unanchored]
        };
        var graph = new RuleGraph { Name = "ObjectAnchoredConditionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("if targetObject.Anchored == nil then", luau);
        Assert.Contains("print(\"Object Is Anchored stopped: target does not expose Anchored.\")", luau);
        Assert.Contains("print(\"Object Is Unanchored stopped: target does not expose Anchored.\")", luau);
        Assert.Contains("return targetObject.Anchored == true", luau);
        Assert.Contains("return targetObject.Anchored == false", luau);
        Assert.DoesNotContain("Condition Object Is Anchored is not implemented yet", luau);
        Assert.DoesNotContain("Condition Object Is Unanchored is not implemented yet", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsObjectToggleActions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var toggleVisibility = catalog.Nodes.Single(node => node.IdBase == "ACT_ToggleObjectVisibility");
        var toggleCollision = catalog.Nodes.Single(node => node.IdBase == "ACT_ToggleObjectCollision");
        var toggleAnchored = catalog.Nodes.Single(node => node.IdBase == "ACT_ToggleObjectAnchored");
        var trigger = NodeCatalogService.CreateNode(start, 100, 100, "TRG_StartToggle");
        var visibility = NodeCatalogService.CreateNode(toggleVisibility, 320, 100, "ACT_ToggleVisibility");
        var collision = NodeCatalogService.CreateNode(toggleCollision, 540, 100, "ACT_ToggleCollision");
        var anchored = NodeCatalogService.CreateNode(toggleAnchored, 760, 100, "ACT_ToggleAnchored");
        SetSceneObject(visibility, "target", "World/Environment/Door");
        SetSceneObject(collision, "target", "World/Environment/Door");
        SetSceneObject(anchored, "target", "World/Environment/Door");

        var rule = new Rule
        {
            Id = "RULE_ObjectToggleActions",
            Name = "ObjectToggleActions",
            Nodes = [trigger, visibility, collision, anchored],
            Connections =
            [
                Flow("CONN_Start_Visibility", trigger.Id, GraphPortDefaults.FlowOut, visibility.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Visibility_Collision", visibility.Id, GraphPortDefaults.FlowOut, collision.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Collision_Anchored", collision.Id, GraphPortDefaults.FlowOut, anchored.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "ObjectToggleActionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("targetObject.Visible = not (targetObject.Visible == true)", luau);
        Assert.Contains("local isVisible = targetObject.Transparency < 1", luau);
        Assert.Contains("targetObject.Transparency = isVisible and 1 or 0", luau);
        Assert.Contains("targetObject.CanCollide = not (targetObject.CanCollide == true)", luau);
        Assert.Contains("targetObject.Anchored = not (targetObject.Anchored == true)", luau);
        Assert.Contains("resolveTarget(triggerObject,", luau);
        Assert.Contains("World/Environment/Door", luau);
        Assert.DoesNotContain("Action Toggle Object Visibility is not implemented", luau);
        Assert.DoesNotContain("Action Toggle Object Collision is not implemented", luau);
        Assert.DoesNotContain("Action Toggle Object Anchored is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsObjectProximityWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var close = catalog.Nodes.Single(node => node.IdBase == "EV_OnObjectsBecameClose");
        var far = catalog.Nodes.Single(node => node.IdBase == "EV_OnObjectsBecameFar");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var closeTrigger = NodeCatalogService.CreateNode(close, 100, 100, "TRG_ObjectsClose");
        var farTrigger = NodeCatalogService.CreateNode(far, 100, 180, "TRG_ObjectsFar");
        var closeAction = NodeCatalogService.CreateNode(showMessage, 360, 100, "ACT_ObjectsCloseMessage");
        var farAction = NodeCatalogService.CreateNode(showMessage, 360, 180, "ACT_ObjectsFarMessage");
        SetSceneObject(closeTrigger, "target", "World/Environment/Watcher");
        SetSceneObject(closeTrigger, "first", "World/Environment/A");
        SetSceneObject(closeTrigger, "second", "World/Environment/B");
        SetConstant(closeTrigger, "distance", "12");
        SetConstant(closeTrigger, "interval", "0.35");
        SetSceneObject(farTrigger, "target", "World/Environment/Watcher");
        SetSceneObject(farTrigger, "first", "World/Environment/A");
        SetSceneObject(farTrigger, "second", "World/Environment/C");
        SetConstant(farTrigger, "distance", "30");
        SetConstant(farTrigger, "interval", "0.45");

        var rule = new Rule
        {
            Id = "RULE_ObjectProximityTriggers",
            Name = "ObjectProximityTriggers",
            Nodes = [closeTrigger, farTrigger, closeAction, farAction],
            Connections =
            [
                Flow("CONN_Close_Message", closeTrigger.Id, GraphPortDefaults.FlowOut, closeAction.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Far_Message", farTrigger.Id, GraphPortDefaults.FlowOut, farAction.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "ObjectProximityTriggersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local firstObject = resolveTarget(triggerObject, \"World/Environment/A\")", luau);
        Assert.Contains("local secondObject = resolveTarget(triggerObject, \"World/Environment/B\")", luau);
        Assert.Contains("local secondObject = resolveTarget(triggerObject, \"World/Environment/C\")", luau);
        Assert.Contains("local distanceBetweenObjects = vrsDistanceBetweenPositions(firstObject.Position, secondObject.Position)", luau);
        Assert.Contains("return distanceBetweenObjects <= distanceLimit, distanceBetweenObjects", luau);
        Assert.Contains("return distanceBetweenObjects >= distanceLimit, distanceBetweenObjects", luau);
        Assert.Contains("local previousValue = readDistanceMatch()", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, firstObject = firstObject, secondObject = secondObject, distance = currentDistance }", luau);
        Assert.Contains("wait(0.35)", luau);
        Assert.Contains("wait(0.45)", luau);
        Assert.DoesNotContain("local function onStart()", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsTeamScoreWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var reached = catalog.Nodes.Single(node => node.IdBase == "EV_OnTeamScoreReached");
        var dropped = catalog.Nodes.Single(node => node.IdBase == "EV_OnTeamScoreDroppedTo");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var reachedTrigger = NodeCatalogService.CreateNode(reached, 100, 100, "TRG_TeamScoreReached");
        var droppedTrigger = NodeCatalogService.CreateNode(dropped, 100, 180, "TRG_TeamScoreDropped");
        var reachedAction = NodeCatalogService.CreateNode(showMessage, 360, 100, "ACT_TeamScoreReachedMessage");
        var droppedAction = NodeCatalogService.CreateNode(showMessage, 360, 180, "ACT_TeamScoreDroppedMessage");
        SetSceneObject(reachedTrigger, "target", "World/Environment/ScoreBoard");
        SetConstant(reachedTrigger, "teamName", "Blue");
        SetConstant(reachedTrigger, "score", "20");
        SetConstant(reachedTrigger, "interval", "0.5");
        SetSceneObject(droppedTrigger, "target", "World/Environment/ScoreBoard");
        SetConstant(droppedTrigger, "teamName", "Blue");
        SetConstant(droppedTrigger, "score", "5");
        SetConstant(droppedTrigger, "interval", "0.75");

        var rule = new Rule
        {
            Id = "RULE_TeamScoreTriggers",
            Name = "TeamScoreTriggers",
            Nodes = [reachedTrigger, droppedTrigger, reachedAction, droppedAction],
            Connections =
            [
                Flow("CONN_TeamReached_Message", reachedTrigger.Id, GraphPortDefaults.FlowOut, reachedAction.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_TeamDropped_Message", droppedTrigger.Id, GraphPortDefaults.FlowOut, droppedAction.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "TeamScoreTriggersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local watchedName = tostring(\"Blue\")", luau);
        Assert.Contains("local teamKey = (\"team:\" .. watchedName)", luau);
        Assert.Contains("local currentScore = tonumber(VRS.vars[teamKey .. \":score\"]) or 0", luau);
        Assert.Contains("return currentScore >= scoreLimit, currentScore", luau);
        Assert.Contains("return currentScore <= scoreLimit, currentScore", luau);
        Assert.Contains("local previousValue = readTeamScoreMatch()", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, teamName = watchedName, teamScore = currentScore }", luau);
        Assert.Contains("wait(0.5)", luau);
        Assert.Contains("wait(0.75)", luau);
        Assert.DoesNotContain("local function onStart()", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsAnyPlayerWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var score = catalog.Nodes.Single(node => node.IdBase == "EV_OnAnyPlayerScoreReached");
        var lives = catalog.Nodes.Single(node => node.IdBase == "EV_OnAnyPlayerHasNoLivesLeft");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var scoreTrigger = NodeCatalogService.CreateNode(score, 100, 100, "TRG_PlayerScoreReached");
        var livesTrigger = NodeCatalogService.CreateNode(lives, 100, 180, "TRG_PlayerLivesGone");
        var scoreAction = NodeCatalogService.CreateNode(showMessage, 360, 100, "ACT_PlayerScoreReachedMessage");
        var livesAction = NodeCatalogService.CreateNode(showMessage, 360, 180, "ACT_PlayerLivesGoneMessage");
        SetSceneObject(scoreTrigger, "target", "World/Environment/ScoreBoard");
        SetConstant(scoreTrigger, "score", "10");
        SetConstant(scoreTrigger, "interval", "0.25");
        SetSceneObject(livesTrigger, "target", "World/Environment/ScoreBoard");
        SetConstant(livesTrigger, "lives", "0");
        SetConstant(livesTrigger, "interval", "0.3");

        var rule = new Rule
        {
            Id = "RULE_AnyPlayerWatcherTriggers",
            Name = "AnyPlayerWatcherTriggers",
            Nodes = [scoreTrigger, livesTrigger, scoreAction, livesAction],
            Connections =
            [
                Flow("CONN_PlayerScore_Message", scoreTrigger.Id, GraphPortDefaults.FlowOut, scoreAction.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PlayerLives_Message", livesTrigger.Id, GraphPortDefaults.FlowOut, livesAction.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "AnyPlayerWatcherTriggersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("Players:GetPlayers()", luau);
        Assert.Contains("local previousMatches = {}", luau);
        Assert.Contains("local playerKey = vrsPlayerKey(player)", luau);
        Assert.Contains("local runtimeKey = \"player:\" .. playerKey .. \":score\"", luau);
        Assert.Contains("local runtimeKey = \"player:\" .. playerKey .. \":lives\"", luau);
        Assert.Contains("return currentValue >= valueLimit, currentValue, playerKey", luau);
        Assert.Contains("return currentValue <= valueLimit, currentValue, playerKey", luau);
        Assert.Contains("if previousMatch == nil then", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, player = player, playerScore = currentValue }", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, player = player, playerLives = currentValue }", luau);
        Assert.Contains("wait(0.25)", luau);
        Assert.Contains("wait(0.3)", luau);
        Assert.DoesNotContain("local function onStart()", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsGenericValueWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var triggerIds = new[]
        {
            "EV_OnNumberReachedAtLeast",
            "EV_OnNumberDroppedToAtMost",
            "EV_OnNumberEnteredRange",
            "EV_OnNumberLeftRange",
            "EV_OnTextBecame",
            "EV_OnTextContains",
            "EV_OnBooleanBecameTrue",
            "EV_OnBooleanBecameFalse"
        };
        var nodes = new List<RuleNode>();
        var connections = new List<GraphConnection>();

        for (var index = 0; index < triggerIds.Length; index++)
        {
            var id = triggerIds[index];
            var triggerEntry = catalog.Nodes.Single(node => node.IdBase == id);
            var trigger = NodeCatalogService.CreateNode(triggerEntry, 100, 100 + index * 80, $"TRG_ValueWatcher_{index}");
            var action = NodeCatalogService.CreateNode(showMessage, 360, 100 + index * 80, $"ACT_ValueWatcher_{index}");
            SetSceneObject(trigger, "target", "World/Environment/Watcher");
            SetConstant(trigger, "interval", (0.2 + index * 0.01).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            SetConstant(action, "message", id);

            switch (id)
            {
                case "EV_OnNumberReachedAtLeast":
                    SetConstant(trigger, "value", "7");
                    SetConstant(trigger, "minimum", "10");
                    break;
                case "EV_OnNumberDroppedToAtMost":
                    SetConstant(trigger, "value", "5");
                    SetConstant(trigger, "maximum", "2");
                    break;
                case "EV_OnNumberEnteredRange":
                case "EV_OnNumberLeftRange":
                    SetConstant(trigger, "value", "5");
                    SetConstant(trigger, "min", "1");
                    SetConstant(trigger, "max", "9");
                    break;
                case "EV_OnTextBecame":
                    SetConstant(trigger, "text", "Loading");
                    SetConstant(trigger, "expected", "Ready");
                    SetConstant(trigger, "caseSensitive", "false");
                    break;
                case "EV_OnTextContains":
                    SetConstant(trigger, "text", "Hello World");
                    SetConstant(trigger, "search", "World");
                    SetConstant(trigger, "caseSensitive", "false");
                    break;
                case "EV_OnBooleanBecameTrue":
                    SetConstant(trigger, "value", "false");
                    break;
                case "EV_OnBooleanBecameFalse":
                    SetConstant(trigger, "value", "true");
                    break;
            }

            nodes.Add(trigger);
            nodes.Add(action);
            connections.Add(Flow($"CONN_ValueWatcher_{index}", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_GenericValueWatchers",
            Name = "GenericValueWatchers",
            Nodes = nodes,
            Connections = connections
        };
        var graph = new RuleGraph { Name = "GenericValueWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local previousMatched = readMatched() == true", luau);
        Assert.Contains("if currentMatched and not previousMatched then", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, watchedValue = currentValue }", luau);
        Assert.Contains("return watchedValue >= limitValue, watchedValue", luau);
        Assert.Contains("return watchedValue <= limitValue, watchedValue", luau);
        Assert.Contains("local insideRange = watchedValue >= minValue and watchedValue <= maxValue", luau);
        Assert.Contains("return insideRange, watchedValue", luau);
        Assert.Contains("return not insideRange, watchedValue", luau);
        Assert.Contains("return watchedText == otherText, watchedText", luau);
        Assert.Contains("return string.find(watchedText, otherText, 1, true) ~= nil, watchedText", luau);
        Assert.Contains("return watchedValue == true, watchedValue", luau);
        Assert.Contains("return watchedValue == false, watchedValue", luau);
        Assert.Contains("wait(0.2)", luau);
        Assert.Contains("wait(0.27)", luau);
        Assert.DoesNotContain("local function onStart()", luau);
        Assert.DoesNotContain("is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsTemporaryVariableWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var triggerIds = new[]
        {
            "EV_OnVariableNumberReachedAtLeast",
            "EV_OnVariableNumberDroppedToAtMost",
            "EV_OnVariableTextBecame",
            "EV_OnVariableBooleanBecameTrue",
            "EV_OnVariableBooleanBecameFalse",
            "EV_OnVariableBecameEmpty",
            "EV_OnVariableBecameNotEmpty"
        };
        var nodes = new List<RuleNode>();
        var connections = new List<GraphConnection>();

        for (var index = 0; index < triggerIds.Length; index++)
        {
            var id = triggerIds[index];
            var triggerEntry = catalog.Nodes.Single(node => node.IdBase == id);
            var trigger = NodeCatalogService.CreateNode(triggerEntry, 100, 100 + index * 80, $"TRG_VariableWatcher_{index}");
            var action = NodeCatalogService.CreateNode(showMessage, 360, 100 + index * 80, $"ACT_VariableWatcher_{index}");
            SetSceneObject(trigger, "target", "World/Environment/Watcher");
            SetConstant(trigger, "interval", "0.25");
            SetConstant(action, "message", id);

            switch (id)
            {
                case "EV_OnVariableNumberReachedAtLeast":
                    SetConstant(trigger, "name", "Score");
                    SetConstant(trigger, "minimum", "10");
                    break;
                case "EV_OnVariableNumberDroppedToAtMost":
                    SetConstant(trigger, "name", "Lives");
                    SetConstant(trigger, "maximum", "0");
                    break;
                case "EV_OnVariableTextBecame":
                    SetConstant(trigger, "name", "RoundState");
                    SetConstant(trigger, "expected", "Ready");
                    SetConstant(trigger, "caseSensitive", "false");
                    break;
                case "EV_OnVariableBooleanBecameTrue":
                case "EV_OnVariableBooleanBecameFalse":
                    SetConstant(trigger, "name", "DoorOpen");
                    break;
                case "EV_OnVariableBecameEmpty":
                case "EV_OnVariableBecameNotEmpty":
                    SetConstant(trigger, "name", "State");
                    break;
            }

            nodes.Add(trigger);
            nodes.Add(action);
            connections.Add(Flow($"CONN_VariableWatcher_{index}", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_TemporaryVariableWatchers",
            Name = "TemporaryVariableWatchers",
            Nodes = nodes,
            Connections = connections
        };
        var graph = new RuleGraph { Name = "TemporaryVariableWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local watchedVariableName = tostring(\"Score\")", luau);
        Assert.Contains("local watchedVariableName = tostring(\"Lives\")", luau);
        Assert.Contains("local currentValue = tonumber(VRS.vars[watchedVariableName]) or 0", luau);
        Assert.Contains("return currentValue >= limitValue, currentValue", luau);
        Assert.Contains("return currentValue <= limitValue, currentValue", luau);
        Assert.Contains("local expectedText = tostring(\"Ready\" or \"\")", luau);
        Assert.Contains("return currentValue == expectedText, currentValue", luau);
        Assert.Contains("local currentValue = VRS.vars[watchedVariableName] == true", luau);
        Assert.Contains("return currentValue == true, currentValue", luau);
        Assert.Contains("return currentValue == false, currentValue", luau);
        Assert.Contains("local valueIsEmpty = currentValue == nil or tostring(currentValue) == \"\"", luau);
        Assert.Contains("return valueIsEmpty, currentValue", luau);
        Assert.Contains("return not valueIsEmpty, currentValue", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, variableName = watchedVariableName, variableValue = currentValue }", luau);
        Assert.DoesNotContain("local function onStart()", luau);
        Assert.DoesNotContain("is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsObjectPropertyWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var triggerIds = new[]
        {
            "EV_OnObjectXPositionReached",
            "EV_OnObjectHeightPositionReached",
            "EV_OnObjectHeightPositionDroppedTo",
            "EV_OnObjectTransparencyReached",
            "EV_OnObjectTransparencyDroppedTo",
            "EV_OnObjectTurnAngleReached",
            "EV_OnObjectTurnAngleDroppedTo",
            "EV_OnObjectWidthSizeReached",
            "EV_OnObjectWidthSizeDroppedTo",
            "EV_OnObjectHeightSizeReached",
            "EV_OnObjectHeightSizeDroppedTo",
            "EV_OnObjectCollisionChanged"
        };
        var nodes = new List<RuleNode>();
        var connections = new List<GraphConnection>();

        for (var index = 0; index < triggerIds.Length; index++)
        {
            var id = triggerIds[index];
            var triggerEntry = catalog.Nodes.Single(node => node.IdBase == id);
            var trigger = NodeCatalogService.CreateNode(triggerEntry, 100, 100 + index * 80, $"TRG_ObjectWatcher_{index}");
            var action = NodeCatalogService.CreateNode(showMessage, 360, 100 + index * 80, $"ACT_ObjectWatcher_{index}");
            SetSceneObject(trigger, "target", "World/Environment/MovingPart");
            SetConstant(trigger, "interval", "0.4");
            SetConstant(action, "message", id);

            switch (id)
            {
                case "EV_OnObjectXPositionReached":
                    SetConstant(trigger, "x", "12");
                    break;
                case "EV_OnObjectHeightPositionReached":
                case "EV_OnObjectHeightPositionDroppedTo":
                    SetConstant(trigger, "height", id.EndsWith("Reached", StringComparison.Ordinal) ? "20" : "0");
                    break;
                case "EV_OnObjectTransparencyReached":
                case "EV_OnObjectTransparencyDroppedTo":
                    SetConstant(trigger, "transparency", id.EndsWith("Reached", StringComparison.Ordinal) ? "1" : "0");
                    break;
                case "EV_OnObjectTurnAngleReached":
                case "EV_OnObjectTurnAngleDroppedTo":
                    SetConstant(trigger, "angle", id.EndsWith("Reached", StringComparison.Ordinal) ? "90" : "0");
                    break;
                case "EV_OnObjectWidthSizeReached":
                case "EV_OnObjectHeightSizeReached":
                    SetConstant(trigger, "size", "3");
                    break;
                case "EV_OnObjectWidthSizeDroppedTo":
                case "EV_OnObjectHeightSizeDroppedTo":
                    SetConstant(trigger, "size", "1");
                    break;
            }

            nodes.Add(trigger);
            nodes.Add(action);
            connections.Add(Flow($"CONN_ObjectWatcher_{index}", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_ObjectPropertyWatchers",
            Name = "ObjectPropertyWatchers",
            Nodes = nodes,
            Connections = connections
        };
        var graph = new RuleGraph { Name = "ObjectPropertyWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("vrsValueAxis(triggerObject.Position, \"X\", \"x\", 0)", luau);
        Assert.Contains("vrsValueAxis(triggerObject.Position, \"Y\", \"y\", 0)", luau);
        Assert.Contains("local currentValue = tonumber(triggerObject.Transparency) or 0", luau);
        Assert.Contains("vrsValueAxis(triggerObject.Rotation, \"Y\", \"y\", 0)", luau);
        Assert.Contains("vrsValueAxis(triggerObject.Scale, \"X\", \"x\", 1)", luau);
        Assert.Contains("vrsValueAxis(triggerObject.Scale, \"Y\", \"y\", 1)", luau);
        Assert.Contains("return currentValue >= limitValue, currentValue", luau);
        Assert.Contains("return currentValue <= limitValue, currentValue", luau);
        Assert.Contains("local function readWatchedObjectValue()", luau);
        Assert.Contains("return triggerObject.CanCollide == true", luau);
        Assert.Contains("if currentValue ~= previousValue then", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, watchedValue = currentValue }", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, objectCollisionOn = currentValue }", luau);
        Assert.Contains("wait(0.4)", luau);
        Assert.DoesNotContain("local function onStart()", luau);
        Assert.DoesNotContain("is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsObjectMovementWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var triggerIds = new[]
        {
            "EV_OnObjectStartedMoving",
            "EV_OnObjectStoppedMoving",
            "EV_OnObjectSpeedReached",
            "EV_OnObjectSpeedDroppedTo"
        };
        var nodes = new List<RuleNode>();
        var connections = new List<GraphConnection>();

        for (var index = 0; index < triggerIds.Length; index++)
        {
            var id = triggerIds[index];
            var triggerEntry = catalog.Nodes.Single(node => node.IdBase == id);
            var trigger = NodeCatalogService.CreateNode(triggerEntry, 100, 100 + index * 80, $"TRG_MovementWatcher_{index}");
            var action = NodeCatalogService.CreateNode(showMessage, 360, 100 + index * 80, $"ACT_MovementWatcher_{index}");
            SetSceneObject(trigger, "target", "World/Environment/MovingPart");
            SetConstant(trigger, "interval", "0.25");
            SetConstant(action, "message", id);

            if (id is "EV_OnObjectSpeedReached" or "EV_OnObjectSpeedDroppedTo")
            {
                SetConstant(trigger, "speed", id == "EV_OnObjectSpeedReached" ? "5" : "1");
            }
            else
            {
                SetConstant(trigger, "movement", "0.1");
            }

            nodes.Add(trigger);
            nodes.Add(action);
            connections.Add(Flow($"CONN_MovementWatcher_{index}", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_ObjectMovementWatchers",
            Name = "ObjectMovementWatchers",
            Nodes = nodes,
            Connections = connections
        };
        var graph = new RuleGraph { Name = "ObjectMovementWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function vrsDistanceBetweenPositions(first, second)", luau);
        Assert.Contains("local checkSeconds = math.max(tonumber(0.25) or 0.25, 0.001)", luau);
        Assert.Contains("local lastPosition = triggerObject.Position", luau);
        Assert.Contains("local movedDistance = vrsDistanceBetweenPositions(lastPosition, currentPosition)", luau);
        Assert.Contains("local currentSpeed = movedDistance / checkSeconds", luau);
        Assert.Contains("return movedDistance >= movementLimit, movedDistance", luau);
        Assert.Contains("return movedDistance <= movementLimit, movedDistance", luau);
        Assert.Contains("return currentSpeed >= movementLimit, currentSpeed", luau);
        Assert.Contains("return currentSpeed <= movementLimit, currentSpeed", luau);
        Assert.Contains("wait(checkSeconds)", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, objectMovement = currentValue }", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, objectSpeed = currentValue }", luau);
        Assert.DoesNotContain("local function onStart()", luau);
        Assert.DoesNotContain("is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsObjectAreaWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var triggerIds = new[]
        {
            "EV_OnObjectEnteredArea",
            "EV_OnObjectLeftArea",
            "EV_OnObjectEnteredBoxArea",
            "EV_OnObjectLeftBoxArea",
            "EV_OnObjectEnteredHeightBand",
            "EV_OnObjectLeftHeightBand"
        };
        var nodes = new List<RuleNode>();
        var connections = new List<GraphConnection>();

        for (var index = 0; index < triggerIds.Length; index++)
        {
            var id = triggerIds[index];
            var triggerEntry = catalog.Nodes.Single(node => node.IdBase == id);
            var trigger = NodeCatalogService.CreateNode(triggerEntry, 100, 100 + index * 80, $"TRG_AreaWatcher_{index}");
            var action = NodeCatalogService.CreateNode(showMessage, 360, 100 + index * 80, $"ACT_AreaWatcher_{index}");
            SetSceneObject(trigger, "target", "World/Environment/MovingPart");
            SetConstant(trigger, "interval", "0.25");
            SetConstant(action, "message", id);

            if (id.Contains("BoxArea", StringComparison.Ordinal))
            {
                SetSceneObject(trigger, "center", "World/Environment/Center");
                SetConstant(trigger, "width", "12");
                SetConstant(trigger, "height", "8");
                SetConstant(trigger, "depth", "6");
            }
            else if (id.Contains("HeightBand", StringComparison.Ordinal))
            {
                SetConstant(trigger, "minHeight", "2");
                SetConstant(trigger, "maxHeight", "12");
            }
            else
            {
                SetSceneObject(trigger, "center", "World/Environment/Center");
                SetConstant(trigger, "radius", "15");
            }

            nodes.Add(trigger);
            nodes.Add(action);
            connections.Add(Flow($"CONN_AreaWatcher_{index}", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_ObjectAreaWatchers",
            Name = "ObjectAreaWatchers",
            Nodes = nodes,
            Connections = connections
        };
        var graph = new RuleGraph { Name = "ObjectAreaWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local centerObject = resolveTarget(triggerObject, \"World/Environment/Center\")", luau);
        Assert.Contains("local areaRadius = math.max(0, tonumber(15) or 10)", luau);
        Assert.Contains("local currentDistance = vrsDistanceBetweenPositions(triggerObject.Position, centerObject.Position)", luau);
        Assert.Contains("local insideArea = currentDistance <= areaRadius", luau);
        Assert.Contains("return insideArea, currentDistance", luau);
        Assert.Contains("return not insideArea, currentDistance", luau);
        Assert.Contains("local halfWidth = math.max(0, tonumber(12) or 10) / 2", luau);
        Assert.Contains("local halfHeight = math.max(0, tonumber(8) or 10) / 2", luau);
        Assert.Contains("local halfDepth = math.max(0, tonumber(6) or 10) / 2", luau);
        Assert.Contains("local insideBox = dx <= halfWidth and dy <= halfHeight and dz <= halfDepth", luau);
        Assert.Contains("return insideBox, insideBox", luau);
        Assert.Contains("return not insideBox, insideBox", luau);
        Assert.Contains("local bandMin = tonumber(2) or 0", luau);
        Assert.Contains("local bandMax = tonumber(12) or 10", luau);
        Assert.Contains("local insideBand = currentHeight >= bandMin and currentHeight <= bandMax", luau);
        Assert.Contains("return insideBand, currentHeight", luau);
        Assert.Contains("return not insideBand, currentHeight", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, areaValue = currentValue, areaCenter = centerObject }", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, areaValue = currentValue }", luau);
        Assert.DoesNotContain("local function onStart()", luau);
        Assert.DoesNotContain("is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsStateMatchWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var triggerIds = new[]
        {
            "EV_OnStateBecameTrue",
            "EV_OnStateBecameFalse",
            "EV_OnStateBecameEmpty",
            "EV_OnStateBecameNotEmpty"
        };
        var nodes = new List<RuleNode>();
        var connections = new List<GraphConnection>();

        for (var index = 0; index < triggerIds.Length; index++)
        {
            var id = triggerIds[index];
            var triggerEntry = catalog.Nodes.Single(node => node.IdBase == id);
            var trigger = NodeCatalogService.CreateNode(triggerEntry, 100, 100 + index * 80, $"TRG_StateWatcher_{index}");
            var action = NodeCatalogService.CreateNode(showMessage, 360, 100 + index * 80, $"ACT_StateWatcher_{index}");
            SetSceneObject(trigger, "target", "World/Environment/Watcher");
            SetConstant(trigger, "state", index < 2 ? "DoorOpen" : "RoundState");
            SetConstant(trigger, "interval", "0.25");
            SetConstant(action, "message", id);
            nodes.Add(trigger);
            nodes.Add(action);
            connections.Add(Flow($"CONN_StateWatcher_{index}", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_StateWatchers",
            Name = "StateWatchers",
            Nodes = nodes,
            Connections = connections
        };
        var graph = new RuleGraph { Name = "StateWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local watchedStateName = tostring(\"DoorOpen\")", luau);
        Assert.Contains("local currentValue = VRS.states[watchedStateName]", luau);
        Assert.Contains("return currentValue == true, currentValue", luau);
        Assert.Contains("return currentValue == false, currentValue", luau);
        Assert.Contains("return currentValue == nil, currentValue", luau);
        Assert.Contains("return currentValue ~= nil, currentValue", luau);
        Assert.Contains("local previousMatched = readMatched() == true", luau);
        Assert.Contains("if currentMatched and not previousMatched then", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, stateName = watchedStateName, stateValue = currentValue }", luau);
        Assert.DoesNotContain("local function onStart()", luau);
        Assert.DoesNotContain("is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsChangedValueAndVariableWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var triggerIds = new[]
        {
            "EV_OnNumberChanged",
            "EV_OnTextChanged",
            "EV_OnBooleanChanged",
            "EV_OnVariableNumberChanged",
            "EV_OnVariableTextChanged",
            "EV_OnVariableBooleanChanged"
        };
        var nodes = new List<RuleNode>();
        var connections = new List<GraphConnection>();

        for (var index = 0; index < triggerIds.Length; index++)
        {
            var id = triggerIds[index];
            var triggerEntry = catalog.Nodes.Single(node => node.IdBase == id);
            var trigger = NodeCatalogService.CreateNode(triggerEntry, 100, 100 + index * 80, $"TRG_ChangedWatcher_{index}");
            var action = NodeCatalogService.CreateNode(showMessage, 360, 100 + index * 80, $"ACT_ChangedWatcher_{index}");
            SetSceneObject(trigger, "target", "World/Environment/Watcher");
            SetConstant(trigger, "interval", "0.25");
            SetConstant(action, "message", id);

            switch (id)
            {
                case "EV_OnNumberChanged":
                    SetConstant(trigger, "value", "5");
                    break;
                case "EV_OnTextChanged":
                    SetConstant(trigger, "text", "Ready");
                    break;
                case "EV_OnBooleanChanged":
                    SetConstant(trigger, "value", "false");
                    break;
                case "EV_OnVariableNumberChanged":
                    SetConstant(trigger, "name", "Score");
                    break;
                case "EV_OnVariableTextChanged":
                    SetConstant(trigger, "name", "RoundState");
                    break;
                case "EV_OnVariableBooleanChanged":
                    SetConstant(trigger, "name", "DoorOpen");
                    break;
            }

            nodes.Add(trigger);
            nodes.Add(action);
            connections.Add(Flow($"CONN_ChangedWatcher_{index}", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_ChangedWatchers",
            Name = "ChangedWatchers",
            Nodes = nodes,
            Connections = connections
        };
        var graph = new RuleGraph { Name = "ChangedWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local previousValue = readWatchedValue()", luau);
        Assert.Contains("if currentValue ~= previousValue then", luau);
        Assert.Contains("return tonumber(5) or 0", luau);
        Assert.Contains("return tostring(\"Ready\" or \"\")", luau);
        Assert.Contains("return false == true", luau);
        Assert.Contains("local watchedVariableName = tostring(\"Score\")", luau);
        Assert.Contains("return tonumber(VRS.vars[watchedVariableName]) or 0", luau);
        Assert.Contains("return tostring(VRS.vars[watchedVariableName] or \"\")", luau);
        Assert.Contains("return VRS.vars[watchedVariableName] == true", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, watchedValue = currentValue }", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, variableName = watchedVariableName, variableValue = currentValue }", luau);
        Assert.DoesNotContain("local function onStart()", luau);
        Assert.DoesNotContain("is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsPlayerCountWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var triggerIds = new[]
        {
            "EV_OnPlayerCountReached",
            "EV_OnPlayerCountDroppedTo",
            "EV_OnEnoughPlayers",
            "EV_OnNotEnoughPlayers"
        };
        var nodes = new List<RuleNode>();
        var connections = new List<GraphConnection>();

        for (var index = 0; index < triggerIds.Length; index++)
        {
            var id = triggerIds[index];
            var triggerEntry = catalog.Nodes.Single(node => node.IdBase == id);
            var trigger = NodeCatalogService.CreateNode(triggerEntry, 100, 100 + index * 80, $"TRG_PlayerCount_{index}");
            var action = NodeCatalogService.CreateNode(showMessage, 360, 100 + index * 80, $"ACT_PlayerCount_{index}");
            SetSceneObject(trigger, "target", "World/Environment/ScoreBoard");
            SetConstant(trigger, "interval", "0.25");
            SetConstant(action, "message", id);
            if (id is "EV_OnPlayerCountReached" or "EV_OnPlayerCountDroppedTo")
            {
                SetConstant(trigger, "count", id == "EV_OnPlayerCountReached" ? "4" : "1");
            }
            else
            {
                SetConstant(trigger, "minimum", "2");
            }

            nodes.Add(trigger);
            nodes.Add(action);
            connections.Add(Flow($"CONN_PlayerCount_{index}", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_PlayerCountWatchers",
            Name = "PlayerCountWatchers",
            Nodes = nodes,
            Connections = connections
        };
        var graph = new RuleGraph { Name = "PlayerCountWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("if Players == nil or Players.GetPlayers == nil then", luau);
        Assert.Contains("local playerLimit = tonumber(4) or 4", luau);
        Assert.Contains("local playerLimit = tonumber(1) or 1", luau);
        Assert.Contains("local playerLimit = tonumber(2) or 2", luau);
        Assert.Contains("local currentCount = #Players:GetPlayers()", luau);
        Assert.Contains("return currentCount >= playerLimit, currentCount", luau);
        Assert.Contains("return currentCount <= playerLimit, currentCount", luau);
        Assert.Contains("return currentCount < playerLimit, currentCount", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, playerCount = currentValue }", luau);
        Assert.DoesNotContain("local function onStart()", luau);
        Assert.DoesNotContain("is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsPlayerDefaultSettersAndReads()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var setWalkSpeed = catalog.Nodes.Single(node => node.IdBase == "ACT_SetWalkSpeed");
        var readWalkSpeed = catalog.Nodes.Single(node => node.IdBase == "PROP_WalkSpeedValue");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(start, 100, 100, "TRG_Start");
        var setter = NodeCatalogService.CreateNode(setWalkSpeed, 320, 100, "ACT_SetWalk");
        var value = NodeCatalogService.CreateNode(readWalkSpeed, 540, 60, "PROP_WalkSpeed");
        var message = NodeCatalogService.CreateNode(showMessage, 760, 100, "ACT_PrintWalk");
        SetConstant(setter, "value", "7");

        var rule = new Rule
        {
            Id = "RULE_PlayerDefaults",
            Name = "PlayerDefaults",
            Nodes = [trigger, setter, value, message],
            Connections =
            [
                Flow("CONN_Start_Set", trigger.Id, GraphPortDefaults.FlowOut, setter.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Set_Print", setter.Id, GraphPortDefaults.FlowOut, message.Id, GraphPortDefaults.FlowIn),
                Value("CONN_Walk_Print", value.Id, GraphPortDefaults.ValueOut, message.Id, GraphPortDefaults.ParameterPortId("message"))
            ]
        };
        var graph = new RuleGraph { Name = "PlayerDefaultsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("PlayerDefaults.WalkSpeed = 7", luau);
        Assert.Contains("print(((PlayerDefaults ~= nil and PlayerDefaults.WalkSpeed) or 4))", luau);
        Assert.DoesNotContain("Set Player Default is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsPlayerDefaultConditionsAndWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartPlayerDefaults");
        var conditionIds = new[]
        {
            "COND_WalkSpeedAtLeast",
            "COND_JumpPowerAtLeast",
            "COND_SprintSpeedAtLeast",
            "COND_MaxHealthAtLeast",
            "COND_RespawnTimeAtLeast",
            "COND_StaminaAtLeast"
        };
        var triggerIds = new[]
        {
            "EV_OnWalkSpeedReached",
            "EV_OnJumpPowerReached",
            "EV_OnSprintSpeedReached",
            "EV_OnMaxHealthReached",
            "EV_OnRespawnTimeReached",
            "EV_OnStaminaReached"
        };
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"COND_PlayerDefault_{index}"))
            .ToList();
        var triggers = triggerIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"TRG_PlayerDefault_{index}"))
            .ToList();
        var printAfterConditions = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintPlayerDefaultConditions");
        var triggerPrints = triggerIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintPlayerDefaultTrigger_{index}"))
            .ToList();
        var values = new[] { "7", "12", "10", "150", "6", "120" };

        for (var index = 0; index < conditions.Count; index++)
        {
            SetConstant(conditions[index], "value", values[index]);
            SetConstant(triggers[index], "value", values[index]);
            SetConstant(triggerPrints[index], "value", $"player default watcher {index}");
        }

        SetConstant(printAfterConditions, "value", "player defaults conditions passed");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_PlayerDefaults_Start_Condition0", start.Id, GraphPortDefaults.FlowOut, conditions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < conditions.Count - 1; index++)
        {
            connections.Add(Flow($"CONN_PlayerDefaults_Condition_{index}_{index + 1}", conditions[index].Id, GraphPortDefaults.FlowOut, conditions[index + 1].Id, GraphPortDefaults.FlowIn));
        }

        connections.Add(Flow("CONN_PlayerDefaults_ConditionLast_Print", conditions[^1].Id, GraphPortDefaults.FlowOut, printAfterConditions.Id, GraphPortDefaults.FlowIn));
        for (var index = 0; index < triggers.Count; index++)
        {
            connections.Add(Flow($"CONN_PlayerDefault_Trigger_{index}_Print", triggers[index].Id, GraphPortDefaults.FlowOut, triggerPrints[index].Id, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_PlayerDefaultsWatchers",
            Name = "Player Default Watchers",
            Nodes = [start, .. conditions, printAfterConditions, .. triggers, .. triggerPrints],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "PlayerDefaultsWatcherGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local currentDefaultValue = tonumber(PlayerDefaults.WalkSpeed) or 4", luau);
        Assert.Contains("local currentDefaultValue = tonumber(PlayerDefaults.JumpPower) or 8", luau);
        Assert.Contains("local currentDefaultValue = tonumber(PlayerDefaults.SprintSpeed) or 8", luau);
        Assert.Contains("local currentDefaultValue = tonumber(PlayerDefaults.MaxHealth) or 100", luau);
        Assert.Contains("local currentDefaultValue = tonumber(PlayerDefaults.RespawnTime) or 5", luau);
        Assert.Contains("local currentDefaultValue = tonumber(PlayerDefaults.Stamina) or 100", luau);
        Assert.Contains("local expectedDefaultValue = tonumber(7) or 4", luau);
        Assert.Contains("local expectedDefaultValue = tonumber(12) or 8", luau);
        Assert.Contains("local expectedDefaultValue = tonumber(150) or 100", luau);
        Assert.Contains("return currentDefaultValue >= expectedDefaultValue", luau);
        Assert.Contains("local watchedDefaultLimit = tonumber(7) or 4", luau);
        Assert.Contains("local watchedDefaultLimit = tonumber(12) or 8", luau);
        Assert.Contains("local watchedDefaultLimit = tonumber(150) or 100", luau);
        Assert.Contains("local currentValue = tonumber(PlayerDefaults.WalkSpeed) or 4", luau);
        Assert.Contains("local currentValue = tonumber(PlayerDefaults.Stamina) or 100", luau);
        Assert.Contains("return currentValue >= watchedDefaultLimit, currentValue", luau);
        Assert.Contains("playerDefaultName = \"WalkSpeed\", playerDefaultValue = currentValue", luau);
        Assert.Contains("playerDefaultName = \"Stamina\", playerDefaultValue = currentValue", luau);
        Assert.Contains("player defaults conditions passed", luau);
        Assert.Contains("player default watcher 5", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsInstanceAndTagChecks()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var hasTagEntry = catalog.Nodes.Single(node => node.IdBase == "COND_ObjectHasTag");
        var hasChildClassEntry = catalog.Nodes.Single(node => node.IdBase == "COND_ObjectHasChildClass");
        var hasChildrenEntry = catalog.Nodes.Single(node => node.IdBase == "COND_ObjectHasChildren");
        var hasNoChildrenEntry = catalog.Nodes.Single(node => node.IdBase == "COND_ObjectHasNoChildren");
        var childCountAtLeastEntry = catalog.Nodes.Single(node => node.IdBase == "COND_ObjectChildCountAtLeast");
        var childCountAtMostEntry = catalog.Nodes.Single(node => node.IdBase == "COND_ObjectChildCountAtMost");
        var findChildEntry = catalog.Nodes.Single(node => node.IdBase == "PROP_FindChild");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(start, 100, 100, "TRG_Start");
        var hasTag = NodeCatalogService.CreateNode(hasTagEntry, 320, 100, "COND_Tag");
        var hasChildClass = NodeCatalogService.CreateNode(hasChildClassEntry, 540, 100, "COND_ChildClass");
        var hasChildren = NodeCatalogService.CreateNode(hasChildrenEntry, 580, 160, "COND_HasChildren");
        var hasNoChildren = NodeCatalogService.CreateNode(hasNoChildrenEntry, 600, 220, "COND_HasNoChildren");
        var childCountAtLeast = NodeCatalogService.CreateNode(childCountAtLeastEntry, 620, 280, "COND_ChildCountAtLeast");
        var childCountAtMost = NodeCatalogService.CreateNode(childCountAtMostEntry, 640, 340, "COND_ChildCountAtMost");
        var childValue = NodeCatalogService.CreateNode(findChildEntry, 760, 60, "PROP_Child");
        var message = NodeCatalogService.CreateNode(showMessage, 980, 100, "ACT_PrintChild");
        SetConstant(hasTag, "tag", "Collectible");
        SetConstant(hasChildClass, "className", "Part");
        SetConstant(childCountAtLeast, "minimum", "2");
        SetConstant(childCountAtMost, "maximum", "5");
        SetConstant(childValue, "childName", "Handle");

        var rule = new Rule
        {
            Id = "RULE_InstanceChecks",
            Name = "InstanceChecks",
            Nodes = [trigger, hasTag, hasChildClass, hasChildren, hasNoChildren, childCountAtLeast, childCountAtMost, childValue, message],
            Connections =
            [
                Flow("CONN_Start_Tag", trigger.Id, GraphPortDefaults.FlowOut, hasTag.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Tag_ChildClass", hasTag.Id, GraphPortDefaults.TrueOut, hasChildClass.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_ChildClass_Print", hasChildClass.Id, GraphPortDefaults.TrueOut, message.Id, GraphPortDefaults.FlowIn),
                Value("CONN_Child_Print", childValue.Id, GraphPortDefaults.ValueOut, message.Id, GraphPortDefaults.ParameterPortId("message"))
            ]
        };
        var graph = new RuleGraph { Name = "InstanceChecksGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("return targetObject:HasTag(tostring(\"Collectible\"))", luau);
        Assert.Contains("return targetObject:FindChildByClass(tostring(\"Part\")) ~= nil", luau);
        Assert.Contains("local children = targetObject:GetChildren()", luau);
        Assert.Contains("local childCount = children == nil and 0 or #children", luau);
        Assert.Contains("return childCount > 0", luau);
        Assert.Contains("return childCount == 0", luau);
        Assert.Contains("return childCount >= 2", luau);
        Assert.Contains("return childCount <= 5", luau);
        Assert.Contains("return targetObject:FindChild(tostring(\"Handle\"))", luau);
        Assert.DoesNotContain("Object Has Tag is not implemented", luau);
        Assert.DoesNotContain("Object Has Child Type is not implemented", luau);
        Assert.DoesNotContain("Object Child Count At Least is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsSoundActionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var play = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PlaySound"), stableId: "ACT_PlaySound");
        var once = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PlaySoundOnce"), stableId: "ACT_PlayOnce");
        var volume = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetSoundVolume"), stableId: "ACT_SetVolume");
        var loop = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetSoundLoop"), stableId: "ACT_SetLoop");
        var audio = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetSoundAudio"), stableId: "ACT_SetSoundAudio");
        var pause = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PauseSound"), stableId: "ACT_PauseSound");
        var stop = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_StopSound"), stableId: "ACT_StopSound");
        var printAudio = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintAudio");
        var printVolume = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintVolume");
        var printPlaying = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintPlaying");
        var soundAudio = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_SoundAudio"), stableId: "PROP_SoundAudio");
        var soundVolume = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_SoundVolume"), stableId: "PROP_SoundVolume");
        var soundPlaying = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_SoundIsPlaying"), stableId: "PROP_SoundPlaying");

        foreach (var node in new[] { play, once, volume, loop, audio, pause, stop, soundAudio, soundVolume, soundPlaying })
        {
            SetSceneObject(node, "target", "World/Audio/Theme");
        }

        SetConstant(once, "volume", "0.75");
        SetConstant(volume, "volume", "0.5");
        SetConstant(loop, "enabled", "true");
        SetConstant(audio, "audio", "asset://theme");

        var rule = new Rule
        {
            Id = "RULE_Sound",
            Name = "Sound",
            Nodes = [start, play, once, volume, loop, audio, pause, stop, printAudio, printVolume, printPlaying, soundAudio, soundVolume, soundPlaying],
            Connections =
            [
                Flow("CONN_Start_Play", start.Id, GraphPortDefaults.FlowOut, play.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Play_Once", play.Id, GraphPortDefaults.FlowOut, once.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Once_Volume", once.Id, GraphPortDefaults.FlowOut, volume.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Volume_Loop", volume.Id, GraphPortDefaults.FlowOut, loop.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Loop_Audio", loop.Id, GraphPortDefaults.FlowOut, audio.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Audio_Pause", audio.Id, GraphPortDefaults.FlowOut, pause.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Pause_Stop", pause.Id, GraphPortDefaults.FlowOut, stop.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Stop_PrintAudio", stop.Id, GraphPortDefaults.FlowOut, printAudio.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PrintAudio_PrintVolume", printAudio.Id, GraphPortDefaults.FlowOut, printVolume.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PrintVolume_PrintPlaying", printVolume.Id, GraphPortDefaults.FlowOut, printPlaying.Id, GraphPortDefaults.FlowIn),
                Value("CONN_Audio_Print", soundAudio.Id, GraphPortDefaults.ValueOut, printAudio.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Volume_Print", soundVolume.Id, GraphPortDefaults.ValueOut, printVolume.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Playing_Print", soundPlaying.Id, GraphPortDefaults.ValueOut, printPlaying.Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "SoundGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local PLAY_SOUND_TARGET_NAME = \"World/Audio/Theme\"", luau);
        Assert.Contains("local soundObject = resolveTarget(triggerObject, PLAY_SOUND_TARGET_NAME)", luau);
        Assert.Contains("soundObject:Play()", luau);
        Assert.Contains("soundObject:PlayOneShot(0.75)", luau);
        Assert.Contains("soundObject.Volume = 0.5", luau);
        Assert.Contains("soundObject.Loop = true", luau);
        Assert.Contains("soundObject.Audio = \"asset://theme\"", luau);
        Assert.Contains("soundObject:Pause()", luau);
        Assert.Contains("soundObject:Stop()", luau);
        Assert.Contains("return targetObject.Audio", luau);
        Assert.Contains("return tonumber(targetObject.Volume) or 0", luau);
        Assert.Contains("return targetObject.Playing == true", luau);
        Assert.DoesNotContain("Play Sound is not implemented", luau);
        Assert.DoesNotContain("Set Sound Volume is not implemented", luau);
        Assert.DoesNotContain("Set Sound Audio is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsSoundLoadedTrigger()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var soundLoaded = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnSoundLoaded"), stableId: "TRG_SoundLoaded");
        var message = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_SoundLoadedMessage");
        SetSceneObject(soundLoaded, "target", "World/Audio/Theme");

        var rule = new Rule
        {
            Id = "RULE_SoundLoaded",
            Name = "SoundLoaded",
            Nodes = [soundLoaded, message],
            Connections =
            [
                Flow("CONN_SoundLoaded_Message", soundLoaded.Id, GraphPortDefaults.FlowOut, message.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "SoundLoadedGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("triggerObject.Loaded:Connect(function()", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, sound = triggerObject }", luau);
        Assert.Contains("target has no Loaded event", luau);
        Assert.DoesNotContain("local function onStart()", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsAudioLightingConditions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var soundPlaying = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_SoundIsPlaying"), stableId: "COND_SoundPlaying");
        var soundVolume = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_SoundVolumeAtLeast"), stableId: "COND_SoundVolume");
        var fogEnabled = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_FogIsEnabled"), stableId: "COND_FogEnabled");
        var fogEnd = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_FogEndDistanceAtMost"), stableId: "COND_FogEnd");
        var lightBrightness = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_LightBrightnessAtLeast"), stableId: "COND_LightBrightness");
        var sunShadows = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_SunLightShadowsEnabled"), stableId: "COND_SunShadows");
        var message = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_Message");

        SetSceneObject(soundPlaying, "target", "World/Audio/Theme");
        SetSceneObject(soundVolume, "target", "World/Audio/Theme");
        SetSceneObject(lightBrightness, "target", "World/Lighting/PointLamp");
        SetSceneObject(sunShadows, "target", "World/Lighting/Sun");
        SetConstant(soundVolume, "volume", "0.75");
        SetConstant(fogEnd, "distance", "120");
        SetConstant(lightBrightness, "brightness", "1.5");

        var rule = new Rule
        {
            Id = "RULE_AudioLightingConditions",
            Name = "AudioLightingConditions",
            Nodes = [start, soundPlaying, soundVolume, fogEnabled, fogEnd, lightBrightness, sunShadows, message],
            Connections =
            [
                Flow("CONN_Start_Playing", start.Id, GraphPortDefaults.FlowOut, soundPlaying.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Playing_Volume", soundPlaying.Id, GraphPortDefaults.TrueOut, soundVolume.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Volume_FogEnabled", soundVolume.Id, GraphPortDefaults.TrueOut, fogEnabled.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_FogEnabled_FogEnd", fogEnabled.Id, GraphPortDefaults.TrueOut, fogEnd.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_FogEnd_Light", fogEnd.Id, GraphPortDefaults.TrueOut, lightBrightness.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Light_Sun", lightBrightness.Id, GraphPortDefaults.TrueOut, sunShadows.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Sun_Message", sunShadows.Id, GraphPortDefaults.TrueOut, message.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "AudioLightingConditionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("return soundObject.Playing == true", luau);
        Assert.Contains("return (tonumber(soundObject.Volume) or 0) >= 0.75", luau);
        Assert.Contains("return Lighting.FogEnabled == true", luau);
        Assert.Contains("return (tonumber(Lighting.FogEndDistance) or 0) <= 120", luau);
        Assert.Contains("return (tonumber(lightObject.Brightness) or 0) >= 1.5", luau);
        Assert.Contains("return sunLightObject.Shadows == true", luau);
        Assert.DoesNotContain("Sound Is Playing is not implemented", luau);
        Assert.DoesNotContain("Light Brightness Is At Least is not implemented", luau);
        Assert.DoesNotContain("Sun Light Shadows Are Enabled is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsAudioLightingWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var soundVolume = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnSoundVolumeReached"), stableId: "TRG_SoundVolume");
        var fogEnabled = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnFogEnabled"), stableId: "TRG_FogEnabled");
        var lightDimmed = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnLightBrightnessDroppedTo"), stableId: "TRG_LightDimmed");
        var sunShadows = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnSunLightShadowsEnabled"), stableId: "TRG_SunShadows");
        var soundMessage = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_SoundMessage");
        var fogMessage = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_FogMessage");
        var lightMessage = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_LightMessage");
        var sunMessage = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_SunMessage");

        SetSceneObject(soundVolume, "target", "World/Audio/Theme");
        SetSceneObject(lightDimmed, "target", "World/Lighting/PointLamp");
        SetSceneObject(sunShadows, "target", "World/Lighting/Sun");
        SetConstant(soundVolume, "volume", "0.75");
        SetConstant(lightDimmed, "brightness", "0.2");

        var rule = new Rule
        {
            Id = "RULE_AudioLightingWatchers",
            Name = "AudioLightingWatchers",
            Nodes = [soundVolume, fogEnabled, lightDimmed, sunShadows, soundMessage, fogMessage, lightMessage, sunMessage],
            Connections =
            [
                Flow("CONN_Sound_Message", soundVolume.Id, GraphPortDefaults.FlowOut, soundMessage.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Fog_Message", fogEnabled.Id, GraphPortDefaults.FlowOut, fogMessage.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Light_Message", lightDimmed.Id, GraphPortDefaults.FlowOut, lightMessage.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Sun_Message", sunShadows.Id, GraphPortDefaults.FlowOut, sunMessage.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "AudioLightingWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("if triggerObject.Volume == nil then", luau);
        Assert.Contains("local watchedLimit = tonumber(0.75) or 1", luau);
        Assert.Contains("local currentValue = tonumber(triggerObject.Volume) or 0", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, soundVolume = currentValue }", luau);
        Assert.Contains("if Lighting == nil then", luau);
        Assert.Contains("local currentValue = Lighting.FogEnabled == true", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, fogEnabled = currentValue }", luau);
        Assert.Contains("if triggerObject.Brightness == nil then", luau);
        Assert.Contains("local watchedLimit = tonumber(0.2) or 0.25", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, lightBrightness = currentValue }", luau);
        Assert.Contains("if triggerObject.Shadows == nil then", luau);
        Assert.Contains("local currentValue = triggerObject.Shadows == true", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, sunLightShadows = currentValue }", luau);
        Assert.DoesNotContain("On Sound Volume Reached trigger stopped: no connected action or condition", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsLightingActionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var fogEnabled = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetFogEnabled"), stableId: "ACT_FogEnabled");
        var fogColor = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetFogColor"), stableId: "ACT_FogColor");
        var fogDistances = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetFogDistances"), stableId: "ACT_FogDistances");
        var ambient = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetAmbientColor"), stableId: "ACT_Ambient");
        var printFogStart = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintFogStart");
        var printAmbient = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintAmbient");
        var fogStart = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_FogStartDistance"), stableId: "PROP_FogStart");
        var ambientColor = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_AmbientColor"), stableId: "PROP_AmbientColor");

        SetConstant(fogEnabled, "enabled", "true");
        SetConstant(fogColor, "r", "0.2");
        SetConstant(fogColor, "g", "0.3");
        SetConstant(fogColor, "b", "0.4");
        SetConstant(fogDistances, "start", "25");
        SetConstant(fogDistances, "end", "150");
        SetConstant(ambient, "r", "0.8");
        SetConstant(ambient, "g", "0.9");
        SetConstant(ambient, "b", "1");

        var rule = new Rule
        {
            Id = "RULE_Lighting",
            Name = "Lighting",
            Nodes = [start, fogEnabled, fogColor, fogDistances, ambient, printFogStart, printAmbient, fogStart, ambientColor],
            Connections =
            [
                Flow("CONN_Start_FogEnabled", start.Id, GraphPortDefaults.FlowOut, fogEnabled.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_FogEnabled_FogColor", fogEnabled.Id, GraphPortDefaults.FlowOut, fogColor.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_FogColor_Distances", fogColor.Id, GraphPortDefaults.FlowOut, fogDistances.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Distances_Ambient", fogDistances.Id, GraphPortDefaults.FlowOut, ambient.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Ambient_PrintFogStart", ambient.Id, GraphPortDefaults.FlowOut, printFogStart.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PrintFogStart_PrintAmbient", printFogStart.Id, GraphPortDefaults.FlowOut, printAmbient.Id, GraphPortDefaults.FlowIn),
                Value("CONN_FogStart_Print", fogStart.Id, GraphPortDefaults.ValueOut, printFogStart.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Ambient_Print", ambientColor.Id, GraphPortDefaults.ValueOut, printAmbient.Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "LightingGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("Lighting.FogEnabled = true", luau);
        Assert.Contains("Lighting.FogColor = Color.New(0.2, 0.3, 0.4, 1)", luau);
        Assert.Contains("Lighting.FogStartDistance = 25", luau);
        Assert.Contains("Lighting.FogEndDistance = 150", luau);
        Assert.Contains("Lighting.AmbientColor = Color.New(0.8, 0.9, 1, 1)", luau);
        Assert.Contains("return tonumber(Lighting.FogStartDistance) or 0", luau);
        Assert.Contains("return Lighting.AmbientColor", luau);
        Assert.DoesNotContain("Set Fog Enabled is not implemented", luau);
        Assert.DoesNotContain("Ambient Color is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsObjectLightActionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var actionIds = new[]
        {
            "ACT_SetLightColor",
            "ACT_SetLightBrightness",
            "ACT_SetLightShine",
            "ACT_SetLightShadows",
            "ACT_SetPointLightRange",
            "ACT_SetSpotLightRange",
            "ACT_SetSpotLightAngle"
        };
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"ACT_ObjectLight_{index}"))
            .ToList();
        var valueIds = new[]
        {
            "PROP_LightColor",
            "PROP_LightBrightness",
            "PROP_LightShine",
            "PROP_LightShadows",
            "PROP_PointLightRange",
            "PROP_SpotLightRange",
            "PROP_SpotLightAngle"
        };
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_ObjectLight_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintObjectLight_{index}"))
            .ToList();

        foreach (var node in actions.Concat(valueNodes))
        {
            var targetPath = node.CatalogId?.Contains("SpotLight", StringComparison.OrdinalIgnoreCase) == true
                ? "World/Lighting/SpotLamp"
                : "World/Lighting/PointLamp";
            SetSceneObject(node, "target", targetPath);
        }

        SetConstant(actions[0], "r", "0.1");
        SetConstant(actions[0], "g", "0.2");
        SetConstant(actions[0], "b", "0.3");
        SetConstant(actions[1], "brightness", "2");
        SetConstant(actions[2], "shine", "0.25");
        SetConstant(actions[3], "shadows", "false");
        SetConstant(actions[4], "range", "35");
        SetConstant(actions[5], "range", "40");
        SetConstant(actions[6], "angle", "60");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_ObjectLight0", start.Id, GraphPortDefaults.FlowOut, actions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < actions.Count; index++)
        {
            if (index + 1 < actions.Count)
            {
                connections.Add(Flow($"CONN_ObjectLight_Action_{index}_{index + 1}", actions[index].Id, GraphPortDefaults.FlowOut, actions[index + 1].Id, GraphPortDefaults.FlowIn));
            }
            else
            {
                connections.Add(Flow("CONN_ObjectLight_Action_Print0", actions[index].Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn));
            }
        }

        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_ObjectLightValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_ObjectLight_Print_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_ObjectLights",
            Name = "ObjectLights",
            Nodes = [start, .. actions, .. valueNodes, .. printNodes],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "ObjectLightsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("lightObject.Color = Color.New(0.1, 0.2, 0.3, 1)", luau);
        Assert.Contains("lightObject.Brightness = 2", luau);
        Assert.Contains("lightObject.Specular = 0.25", luau);
        Assert.Contains("lightObject.Shadows = false", luau);
        Assert.Contains("lightObject.Range = 35", luau);
        Assert.Contains("lightObject.Range = 40", luau);
        Assert.Contains("lightObject.Angle = 60", luau);
        Assert.Contains("return targetObject.Color", luau);
        Assert.Contains("return tonumber(targetObject.Brightness) or 0", luau);
        Assert.Contains("return tonumber(targetObject.Specular) or 0", luau);
        Assert.Contains("return targetObject.Shadows == true", luau);
        Assert.Contains("return tonumber(targetObject.Range) or 0", luau);
        Assert.Contains("return tonumber(targetObject.Angle) or 0", luau);
        Assert.DoesNotContain("Set Light Color is not implemented", luau);
        Assert.DoesNotContain("Light Brightness is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsSunLightActionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var actionIds = new[]
        {
            "ACT_SetSunLightColor",
            "ACT_SetSunLightBrightness",
            "ACT_SetSunLightShine",
            "ACT_SetSunLightShadows"
        };
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"ACT_SunLight_{index}"))
            .ToList();
        var valueIds = new[]
        {
            "PROP_SunLightColor",
            "PROP_SunLightBrightness",
            "PROP_SunLightShine",
            "PROP_SunLightShadows"
        };
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_SunLight_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintSunLight_{index}"))
            .ToList();

        foreach (var node in actions.Concat(valueNodes))
        {
            SetSceneObject(node, "target", "World/Lighting/Sun");
        }

        SetConstant(actions[0], "r", "1");
        SetConstant(actions[0], "g", "0.85");
        SetConstant(actions[0], "b", "0.55");
        SetConstant(actions[1], "brightness", "1.75");
        SetConstant(actions[2], "shine", "0.35");
        SetConstant(actions[3], "shadows", "true");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_SunLight0", start.Id, GraphPortDefaults.FlowOut, actions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < actions.Count; index++)
        {
            if (index + 1 < actions.Count)
            {
                connections.Add(Flow($"CONN_SunLight_Action_{index}_{index + 1}", actions[index].Id, GraphPortDefaults.FlowOut, actions[index + 1].Id, GraphPortDefaults.FlowIn));
            }
            else
            {
                connections.Add(Flow("CONN_SunLight_Action_Print0", actions[index].Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn));
            }
        }

        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_SunLightValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_SunLight_Print_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_SunLight",
            Name = "SunLight",
            Nodes = [start, .. actions, .. valueNodes, .. printNodes],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "SunLightGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("lightObject.Color = Color.New(1, 0.85, 0.55, 1)", luau);
        Assert.Contains("lightObject.Brightness = 1.75", luau);
        Assert.Contains("lightObject.Specular = 0.35", luau);
        Assert.Contains("lightObject.Shadows = true", luau);
        Assert.Contains("return targetObject.Color", luau);
        Assert.Contains("return tonumber(targetObject.Brightness) or 0", luau);
        Assert.Contains("return tonumber(targetObject.Specular) or 0", luau);
        Assert.Contains("return targetObject.Shadows == true", luau);
        Assert.DoesNotContain("Set Sun Light Color is not implemented", luau);
        Assert.DoesNotContain("Sun Light Brightness is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsColorAdjustModifierActionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var actionIds = new[]
        {
            "ACT_SetColorAdjustBrightness",
            "ACT_SetColorAdjustContrast",
            "ACT_SetColorAdjustSaturation",
            "ACT_SetColorAdjustTint"
        };
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"ACT_ColorAdjust_{index}"))
            .ToList();
        var valueIds = new[]
        {
            "PROP_ColorAdjustBrightness",
            "PROP_ColorAdjustContrast",
            "PROP_ColorAdjustSaturation",
            "PROP_ColorAdjustTint"
        };
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_ColorAdjust_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintColorAdjust_{index}"))
            .ToList();

        foreach (var node in actions.Concat(valueNodes))
        {
            SetSceneObject(node, "target", "World/Lighting/ColorMood");
        }

        SetConstant(actions[0], "brightness", "0.15");
        SetConstant(actions[1], "contrast", "0.4");
        SetConstant(actions[2], "saturation", "-0.2");
        SetConstant(actions[3], "r", "0.9");
        SetConstant(actions[3], "g", "0.8");
        SetConstant(actions[3], "b", "0.7");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_ColorAdjust0", start.Id, GraphPortDefaults.FlowOut, actions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < actions.Count; index++)
        {
            if (index + 1 < actions.Count)
            {
                connections.Add(Flow($"CONN_ColorAdjust_Action_{index}_{index + 1}", actions[index].Id, GraphPortDefaults.FlowOut, actions[index + 1].Id, GraphPortDefaults.FlowIn));
            }
            else
            {
                connections.Add(Flow("CONN_ColorAdjust_Action_Print0", actions[index].Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn));
            }
        }

        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_ColorAdjustValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_ColorAdjust_Print_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_ColorAdjust",
            Name = "ColorAdjust",
            Nodes = [start, .. actions, .. valueNodes, .. printNodes],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "ColorAdjustGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("modifierObject.Brightness = 0.15", luau);
        Assert.Contains("modifierObject.Contrast = 0.4", luau);
        Assert.Contains("modifierObject.Saturation = -0.2", luau);
        Assert.Contains("modifierObject.TintColor = Color.New(0.9, 0.8, 0.7, 1)", luau);
        Assert.Contains("return tonumber(targetObject.Brightness) or 0", luau);
        Assert.Contains("return tonumber(targetObject.Contrast) or 0", luau);
        Assert.Contains("return tonumber(targetObject.Saturation) or 0", luau);
        Assert.Contains("return targetObject.TintColor", luau);
        Assert.DoesNotContain("Set Color Adjust Brightness is not implemented", luau);
        Assert.DoesNotContain("Color Adjust Tint is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsProceduralSkyActionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var actionIds = new[]
        {
            "ACT_SetProceduralSkySunSize",
            "ACT_SetProceduralSkyTint",
            "ACT_SetProceduralSkyHorizonColor",
            "ACT_SetProceduralSkyGroundColor",
            "ACT_SetProceduralSkyExposure"
        };
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"ACT_ProceduralSky_{index}"))
            .ToList();
        var valueIds = new[]
        {
            "PROP_ProceduralSkySunSize",
            "PROP_ProceduralSkyTint",
            "PROP_ProceduralSkyHorizonColor",
            "PROP_ProceduralSkyGroundColor",
            "PROP_ProceduralSkyExposure"
        };
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_ProceduralSky_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintProceduralSky_{index}"))
            .ToList();

        foreach (var node in actions.Concat(valueNodes))
        {
            SetSceneObject(node, "target", "World/Lighting/MorningSky");
        }

        SetConstant(actions[0], "size", "2");
        SetConstant(actions[1], "r", "0.4");
        SetConstant(actions[1], "g", "0.6");
        SetConstant(actions[1], "b", "0.9");
        SetConstant(actions[2], "r", "1");
        SetConstant(actions[2], "g", "0.5");
        SetConstant(actions[2], "b", "0.25");
        SetConstant(actions[3], "r", "0.1");
        SetConstant(actions[3], "g", "0.15");
        SetConstant(actions[3], "b", "0.2");
        SetConstant(actions[4], "exposure", "1.25");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_ProceduralSky0", start.Id, GraphPortDefaults.FlowOut, actions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < actions.Count; index++)
        {
            if (index + 1 < actions.Count)
            {
                connections.Add(Flow($"CONN_ProceduralSky_Action_{index}_{index + 1}", actions[index].Id, GraphPortDefaults.FlowOut, actions[index + 1].Id, GraphPortDefaults.FlowIn));
            }
            else
            {
                connections.Add(Flow("CONN_ProceduralSky_Action_Print0", actions[index].Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn));
            }
        }

        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_ProceduralSkyValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_ProceduralSky_Print_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_ProceduralSky",
            Name = "ProceduralSky",
            Nodes = [start, .. actions, .. valueNodes, .. printNodes],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "ProceduralSkyGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("skyObject.SunSize = 2", luau);
        Assert.Contains("skyObject.SkyTint = Color.New(0.4, 0.6, 0.9, 1)", luau);
        Assert.Contains("skyObject.HorizonColor = Color.New(1, 0.5, 0.25, 1)", luau);
        Assert.Contains("skyObject.GroundColor = Color.New(0.1, 0.15, 0.2, 1)", luau);
        Assert.Contains("skyObject.Exposure = 1.25", luau);
        Assert.Contains("return tonumber(targetObject.SunSize) or 0", luau);
        Assert.Contains("return targetObject.SkyTint", luau);
        Assert.Contains("return targetObject.HorizonColor", luau);
        Assert.Contains("return targetObject.GroundColor", luau);
        Assert.Contains("return tonumber(targetObject.Exposure) or 0", luau);
        Assert.DoesNotContain("Set Procedural Sky Sun Size is not implemented", luau);
        Assert.DoesNotContain("Procedural Sky Exposure is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsProceduralSkyConditionsAndTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var conditionIds = new[]
        {
            "COND_ProceduralSkySunSizeAtLeast",
            "COND_ProceduralSkyTintIs",
            "COND_ProceduralSkyHorizonColorIs",
            "COND_ProceduralSkyGroundColorIs",
            "COND_ProceduralSkyExposureAtLeast"
        };
        var triggerIds = new[]
        {
            "EV_OnProceduralSkySunSizeReached",
            "EV_OnProceduralSkyTintChanged",
            "EV_OnProceduralSkyHorizonColorChanged",
            "EV_OnProceduralSkyGroundColorChanged",
            "EV_OnProceduralSkyExposureReached"
        };
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"COND_ProceduralSky_{index}"))
            .ToList();
        var triggers = triggerIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"EV_ProceduralSky_{index}"))
            .ToList();

        foreach (var node in conditions.Concat(triggers))
        {
            SetSceneObject(node, "target", "World/Lighting/MorningSky");
        }

        SetConstant(conditions[0], "size", "2");
        SetConstant(conditions[1], "r", "0.4");
        SetConstant(conditions[1], "g", "0.6");
        SetConstant(conditions[1], "b", "0.9");
        SetConstant(conditions[2], "r", "1");
        SetConstant(conditions[2], "g", "0.5");
        SetConstant(conditions[2], "b", "0.25");
        SetConstant(conditions[3], "r", "0.1");
        SetConstant(conditions[3], "g", "0.15");
        SetConstant(conditions[3], "b", "0.2");
        SetConstant(conditions[4], "exposure", "1.25");
        SetConstant(triggers[0], "size", "2");
        SetConstant(triggers[4], "exposure", "1.25");

        var rule = new Rule
        {
            Id = "RULE_ProceduralSkyWatchers",
            Name = "ProceduralSkyWatchers",
            Nodes = [.. conditions, .. triggers]
        };
        var graph = new RuleGraph { Name = "ProceduralSkyWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local currentValue = tonumber(skyObject.SunSize) or 0", luau);
        Assert.Contains("local expectedValue = tonumber(2) or 1", luau);
        Assert.Contains("return skyObject.SkyTint == expectedColor", luau);
        Assert.Contains("return skyObject.HorizonColor == expectedColor", luau);
        Assert.Contains("return skyObject.GroundColor == expectedColor", luau);
        Assert.Contains("local currentValue = tonumber(skyObject.Exposure) or 0", luau);
        Assert.Contains("local watchedLimit = tonumber(2) or 1", luau);
        Assert.Contains("local currentValue = tonumber(triggerObject.SunSize) or 0", luau);
        Assert.Contains("return triggerObject.SkyTint", luau);
        Assert.Contains("return triggerObject.HorizonColor", luau);
        Assert.Contains("return triggerObject.GroundColor", luau);
        Assert.Contains("local watchedLimit = tonumber(1.25) or 1", luau);
        Assert.Contains("proceduralSkySunSize = currentValue", luau);
        Assert.Contains("proceduralSkyTint = currentValue", luau);
        Assert.Contains("proceduralSkyExposure = currentValue", luau);
        Assert.DoesNotContain("Procedural Sky Sun Size At Least is not implemented", luau);
        Assert.DoesNotContain("On Procedural Sky Exposure Reached is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsGradientSkyActionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var actionIds = new[]
        {
            "ACT_SetGradientSkyColors",
            "ACT_SetGradientSkySunDisc",
            "ACT_SetGradientSkySunHalo",
            "ACT_SetGradientSkyHorizonLine"
        };
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"ACT_GradientSky_{index}"))
            .ToList();
        var valueIds = new[]
        {
            "PROP_GradientSkyTopColor",
            "PROP_GradientSkyBottomColor",
            "PROP_GradientSkyExponent",
            "PROP_GradientSkySunDiscColor",
            "PROP_GradientSkySunDiscMultiplier",
            "PROP_GradientSkySunDiscExponent",
            "PROP_GradientSkySunHaloColor",
            "PROP_GradientSkySunHaloExponent",
            "PROP_GradientSkySunHaloContribution",
            "PROP_GradientSkyHorizonLineColor",
            "PROP_GradientSkyHorizonLineExponent",
            "PROP_GradientSkyHorizonLineContribution"
        };
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_GradientSky_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintGradientSky_{index}"))
            .ToList();

        foreach (var node in actions.Concat(valueNodes))
        {
            SetSceneObject(node, "target", "World/Lighting/SunsetGradient");
        }

        SetConstant(actions[0], "topR", "0.2");
        SetConstant(actions[0], "topG", "0.4");
        SetConstant(actions[0], "topB", "0.9");
        SetConstant(actions[0], "bottomR", "1");
        SetConstant(actions[0], "bottomG", "0.6");
        SetConstant(actions[0], "bottomB", "0.2");
        SetConstant(actions[0], "exponent", "1.5");
        SetConstant(actions[1], "r", "1");
        SetConstant(actions[1], "g", "0.9");
        SetConstant(actions[1], "b", "0.4");
        SetConstant(actions[1], "multiplier", "2");
        SetConstant(actions[1], "exponent", "3");
        SetConstant(actions[2], "r", "1");
        SetConstant(actions[2], "g", "0.7");
        SetConstant(actions[2], "b", "0.3");
        SetConstant(actions[2], "exponent", "4");
        SetConstant(actions[2], "contribution", "0.8");
        SetConstant(actions[3], "r", "0.9");
        SetConstant(actions[3], "g", "0.5");
        SetConstant(actions[3], "b", "0.2");
        SetConstant(actions[3], "exponent", "2.5");
        SetConstant(actions[3], "contribution", "0.6");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_GradientSky0", start.Id, GraphPortDefaults.FlowOut, actions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < actions.Count; index++)
        {
            if (index + 1 < actions.Count)
            {
                connections.Add(Flow($"CONN_GradientSky_Action_{index}_{index + 1}", actions[index].Id, GraphPortDefaults.FlowOut, actions[index + 1].Id, GraphPortDefaults.FlowIn));
            }
            else
            {
                connections.Add(Flow("CONN_GradientSky_Action_Print0", actions[index].Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn));
            }
        }

        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_GradientSkyValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_GradientSky_Print_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_GradientSky",
            Name = "GradientSky",
            Nodes = [start, .. actions, .. valueNodes, .. printNodes],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "GradientSkyGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("skyObject.SkyGradientTop = Color.New(0.2, 0.4, 0.9, 1)", luau);
        Assert.Contains("skyObject.SkyGradientBottom = Color.New(1, 0.6, 0.2, 1)", luau);
        Assert.Contains("skyObject.SkyGradientExponent = 1.5", luau);
        Assert.Contains("skyObject.SunDiscColor = Color.New(1, 0.9, 0.4, 1)", luau);
        Assert.Contains("skyObject.SunDiscMultiplier = 2", luau);
        Assert.Contains("skyObject.SunDiscExponent = 3", luau);
        Assert.Contains("skyObject.SunHaloColor = Color.New(1, 0.7, 0.3, 1)", luau);
        Assert.Contains("skyObject.SunHaloExponent = 4", luau);
        Assert.Contains("skyObject.SunHaloContribution = 0.8", luau);
        Assert.Contains("skyObject.HorizonLineColor = Color.New(0.9, 0.5, 0.2, 1)", luau);
        Assert.Contains("skyObject.HorizonLineExponent = 2.5", luau);
        Assert.Contains("skyObject.HorizonLineContribution = 0.6", luau);
        Assert.Contains("return targetObject.SkyGradientTop", luau);
        Assert.Contains("return targetObject.SkyGradientBottom", luau);
        Assert.Contains("return tonumber(targetObject.SkyGradientExponent) or 0", luau);
        Assert.Contains("return targetObject.SunDiscColor", luau);
        Assert.Contains("return tonumber(targetObject.SunDiscMultiplier) or 0", luau);
        Assert.Contains("return tonumber(targetObject.SunDiscExponent) or 0", luau);
        Assert.Contains("return targetObject.SunHaloColor", luau);
        Assert.Contains("return tonumber(targetObject.SunHaloExponent) or 0", luau);
        Assert.Contains("return tonumber(targetObject.SunHaloContribution) or 0", luau);
        Assert.Contains("return targetObject.HorizonLineColor", luau);
        Assert.Contains("return tonumber(targetObject.HorizonLineExponent) or 0", luau);
        Assert.Contains("return tonumber(targetObject.HorizonLineContribution) or 0", luau);
        Assert.DoesNotContain("Set Gradient Sky Colors is not implemented", luau);
        Assert.DoesNotContain("Gradient Sky Horizon Line Contribution is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsImageSkyActionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var allImages = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetImageSkyAllImages"), stableId: "ACT_ImageSky_All");
        var actionIds = new[]
        {
            "ACT_SetImageSkyTopImage",
            "ACT_SetImageSkyBottomImage",
            "ACT_SetImageSkyLeftImage",
            "ACT_SetImageSkyRightImage",
            "ACT_SetImageSkyFrontImage",
            "ACT_SetImageSkyBackImage"
        };
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"ACT_ImageSky_{index}"))
            .ToList();
        var valueIds = new[]
        {
            "PROP_ImageSkyTopImage",
            "PROP_ImageSkyBottomImage",
            "PROP_ImageSkyLeftImage",
            "PROP_ImageSkyRightImage",
            "PROP_ImageSkyFrontImage",
            "PROP_ImageSkyBackImage"
        };
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_ImageSky_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintImageSky_{index}"))
            .ToList();

        foreach (var node in new[] { allImages }.Concat(actions).Concat(valueNodes))
        {
            SetSceneObject(node, "target", "World/Lighting/EveningSkybox");
        }

        SetConstant(allImages, "topImage", "image://all-top");
        SetConstant(allImages, "bottomImage", "image://all-bottom");
        SetConstant(allImages, "leftImage", "image://all-left");
        SetConstant(allImages, "rightImage", "image://all-right");
        SetConstant(allImages, "frontImage", "image://all-front");
        SetConstant(allImages, "backImage", "image://all-back");

        var sideImages = new[]
        {
            "image://top",
            "image://bottom",
            "image://left",
            "image://right",
            "image://front",
            "image://back"
        };
        for (var index = 0; index < actions.Count; index++)
        {
            SetConstant(actions[index], "image", sideImages[index]);
        }

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_ImageSkyAll", start.Id, GraphPortDefaults.FlowOut, allImages.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_ImageSkyAll_Action0", allImages.Id, GraphPortDefaults.FlowOut, actions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < actions.Count; index++)
        {
            if (index + 1 < actions.Count)
            {
                connections.Add(Flow($"CONN_ImageSky_Action_{index}_{index + 1}", actions[index].Id, GraphPortDefaults.FlowOut, actions[index + 1].Id, GraphPortDefaults.FlowIn));
            }
            else
            {
                connections.Add(Flow("CONN_ImageSky_Action_Print0", actions[index].Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn));
            }
        }

        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_ImageSkyValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_ImageSky_Print_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_ImageSky",
            Name = "ImageSky",
            Nodes = [start, allImages, .. actions, .. valueNodes, .. printNodes],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "ImageSkyGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("skyObject.TopImage = \"image://all-top\"", luau);
        Assert.Contains("skyObject.BottomImage = \"image://all-bottom\"", luau);
        Assert.Contains("skyObject.LeftImage = \"image://all-left\"", luau);
        Assert.Contains("skyObject.RightImage = \"image://all-right\"", luau);
        Assert.Contains("skyObject.FrontImage = \"image://all-front\"", luau);
        Assert.Contains("skyObject.BackImage = \"image://all-back\"", luau);
        Assert.Contains("skyObject.TopImage = \"image://top\"", luau);
        Assert.Contains("skyObject.BottomImage = \"image://bottom\"", luau);
        Assert.Contains("skyObject.LeftImage = \"image://left\"", luau);
        Assert.Contains("skyObject.RightImage = \"image://right\"", luau);
        Assert.Contains("skyObject.FrontImage = \"image://front\"", luau);
        Assert.Contains("skyObject.BackImage = \"image://back\"", luau);
        Assert.Contains("return tostring(targetObject.TopImage or \"\")", luau);
        Assert.Contains("return tostring(targetObject.BottomImage or \"\")", luau);
        Assert.Contains("return tostring(targetObject.LeftImage or \"\")", luau);
        Assert.Contains("return tostring(targetObject.RightImage or \"\")", luau);
        Assert.Contains("return tostring(targetObject.FrontImage or \"\")", luau);
        Assert.Contains("return tostring(targetObject.BackImage or \"\")", luau);
        Assert.DoesNotContain("Set Image Sky Images is not implemented", luau);
        Assert.DoesNotContain("Image Sky Top Image is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsImageSkyConditionsAndWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_ImageSkyConditionStart");
        var conditionIds = new[]
        {
            "COND_ImageSkyTopImageIs",
            "COND_ImageSkyBottomImageIs",
            "COND_ImageSkyLeftImageIs",
            "COND_ImageSkyRightImageIs",
            "COND_ImageSkyFrontImageIs",
            "COND_ImageSkyBackImageIs"
        };
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"COND_ImageSky_{index}"))
            .ToList();
        var triggerIds = new[]
        {
            "EV_OnImageSkyTopImageChanged",
            "EV_OnImageSkyBottomImageChanged",
            "EV_OnImageSkyLeftImageChanged",
            "EV_OnImageSkyRightImageChanged",
            "EV_OnImageSkyFrontImageChanged",
            "EV_OnImageSkyBackImageChanged"
        };
        var triggers = triggerIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"TRG_ImageSkyWatcher_{index}"))
            .ToList();
        var message = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_ImageSkyConditionMessage");

        foreach (var node in conditions.Concat(triggers))
        {
            SetSceneObject(node, "target", "World/Lighting/EveningSkybox");
        }

        var sideImages = new[]
        {
            "image://top",
            "image://bottom",
            "image://left",
            "image://right",
            "image://front",
            "image://back"
        };
        for (var index = 0; index < conditions.Count; index++)
        {
            SetConstant(conditions[index], "image", sideImages[index]);
            SetConstant(conditions[index], "caseSensitive", "true");
        }

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_ImageSkyCondition0", start.Id, GraphPortDefaults.FlowOut, conditions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < conditions.Count; index++)
        {
            var toNodeId = index + 1 < conditions.Count ? conditions[index + 1].Id : message.Id;
            connections.Add(Flow($"CONN_ImageSkyCondition_{index}", conditions[index].Id, GraphPortDefaults.TrueOut, toNodeId, GraphPortDefaults.FlowIn));
        }

        connections.AddRange(triggers.Select((trigger, index) =>
            Flow($"CONN_ImageSkyWatcher_{index}", trigger.Id, GraphPortDefaults.FlowOut, message.Id, GraphPortDefaults.FlowIn)));

        var rule = new Rule
        {
            Id = "RULE_ImageSkyConditionsAndWatchers",
            Name = "ImageSkyConditionsAndWatchers",
            Nodes = [start, .. conditions, .. triggers, message],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "ImageSkyConditionsAndWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local currentText = tostring(targetObject.TopImage or \"\")", luau);
        Assert.Contains("local currentText = tostring(targetObject.BottomImage or \"\")", luau);
        Assert.Contains("local currentText = tostring(targetObject.LeftImage or \"\")", luau);
        Assert.Contains("local currentText = tostring(targetObject.RightImage or \"\")", luau);
        Assert.Contains("local currentText = tostring(targetObject.FrontImage or \"\")", luau);
        Assert.Contains("local currentText = tostring(targetObject.BackImage or \"\")", luau);
        Assert.Contains("local expectedText = tostring(\"image://top\")", luau);
        Assert.Contains("return currentText == expectedText", luau);
        Assert.Contains("return tostring(triggerObject.TopImage or \"\")", luau);
        Assert.Contains("return tostring(triggerObject.BottomImage or \"\")", luau);
        Assert.Contains("return tostring(triggerObject.LeftImage or \"\")", luau);
        Assert.Contains("return tostring(triggerObject.RightImage or \"\")", luau);
        Assert.Contains("return tostring(triggerObject.FrontImage or \"\")", luau);
        Assert.Contains("return tostring(triggerObject.BackImage or \"\")", luau);
        Assert.Contains("local previousValue = readWatchedValue()", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, imageSkyImage = currentValue }", luau);
        Assert.DoesNotContain("Image Sky Top Image Is is not implemented", luau);
        Assert.DoesNotContain("On Image Sky Back Image Changed is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsParticleActionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var startParticles = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_StartParticles"), stableId: "ACT_StartParticles");
        var burstParticles = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_BurstParticles"), stableId: "ACT_BurstParticles");
        var setAmount = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetParticleAmount"), stableId: "ACT_SetParticleAmount");
        var stopParticles = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_StopParticles"), stableId: "ACT_StopParticles");
        var printPlaying = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintParticlesPlaying");
        var printAmount = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintParticleAmount");
        var particlesPlaying = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ParticlesPlaying"), stableId: "PROP_ParticlesPlaying");
        var particleAmount = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ParticleAmount"), stableId: "PROP_ParticleAmount");

        foreach (var node in new[] { startParticles, burstParticles, setAmount, stopParticles, particlesPlaying, particleAmount })
        {
            SetSceneObject(node, "target", "World/Effects/Sparks");
        }

        SetConstant(burstParticles, "count", "12");
        SetConstant(setAmount, "amount", "30");

        var rule = new Rule
        {
            Id = "RULE_Particles",
            Name = "Particles",
            Nodes = [start, startParticles, burstParticles, setAmount, stopParticles, printPlaying, printAmount, particlesPlaying, particleAmount],
            Connections =
            [
                Flow("CONN_Start_StartParticles", start.Id, GraphPortDefaults.FlowOut, startParticles.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_StartParticles_Burst", startParticles.Id, GraphPortDefaults.FlowOut, burstParticles.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Burst_SetAmount", burstParticles.Id, GraphPortDefaults.FlowOut, setAmount.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_SetAmount_Stop", setAmount.Id, GraphPortDefaults.FlowOut, stopParticles.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Stop_PrintPlaying", stopParticles.Id, GraphPortDefaults.FlowOut, printPlaying.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PrintPlaying_PrintAmount", printPlaying.Id, GraphPortDefaults.FlowOut, printAmount.Id, GraphPortDefaults.FlowIn),
                Value("CONN_ParticlesPlaying_Print", particlesPlaying.Id, GraphPortDefaults.ValueOut, printPlaying.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_ParticleAmount_Print", particleAmount.Id, GraphPortDefaults.ValueOut, printAmount.Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "ParticlesGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("particleObject:Play()", luau);
        Assert.Contains("particleObject:Emit(12)", luau);
        Assert.Contains("particleObject.Amount = 30", luau);
        Assert.Contains("particleObject:Stop()", luau);
        Assert.Contains("return targetObject.Playing == true", luau);
        Assert.Contains("return tonumber(targetObject.Amount) or 0", luau);
        Assert.Contains("target particle effect was not found", luau);
        Assert.DoesNotContain("Start Particles is not implemented", luau);
        Assert.DoesNotContain("Particle Amount is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsMeshAnimationTriggerActionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var meshLoaded = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnMeshLoaded"), stableId: "TRG_MeshLoaded");
        var playAnimation = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PlayMeshAnimation"), stableId: "ACT_PlayMeshAnimation");
        var stopAnimation = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_StopMeshAnimation"), stableId: "ACT_StopMeshAnimation");
        var printCurrent = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintCurrentMeshAnimation");
        var printPlaying = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintMeshAnimationPlaying");
        var printLoading = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintMeshLoading");
        var currentAnimation = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_CurrentMeshAnimation"), stableId: "PROP_CurrentMeshAnimation");
        var animationPlaying = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_MeshAnimationPlaying"), stableId: "PROP_MeshAnimationPlaying");
        var meshLoading = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_MeshLoading"), stableId: "PROP_MeshLoading");

        foreach (var node in new[] { meshLoaded, playAnimation, stopAnimation, currentAnimation, animationPlaying, meshLoading })
        {
            SetSceneObject(node, "target", "World/Environment/AnimatedMesh");
        }

        SetConstant(playAnimation, "animationName", "Wave");
        SetConstant(playAnimation, "speed", "1.5");
        SetConstant(playAnimation, "loop", "false");
        SetConstant(stopAnimation, "animationName", "Wave");

        var rule = new Rule
        {
            Id = "RULE_MeshAnimation",
            Name = "Mesh Animation",
            Nodes = [meshLoaded, playAnimation, stopAnimation, printCurrent, printPlaying, printLoading, currentAnimation, animationPlaying, meshLoading],
            Connections =
            [
                Flow("CONN_Loaded_Play", meshLoaded.Id, GraphPortDefaults.FlowOut, playAnimation.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Play_Stop", playAnimation.Id, GraphPortDefaults.FlowOut, stopAnimation.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Stop_PrintCurrent", stopAnimation.Id, GraphPortDefaults.FlowOut, printCurrent.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PrintCurrent_PrintPlaying", printCurrent.Id, GraphPortDefaults.FlowOut, printPlaying.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PrintPlaying_PrintLoading", printPlaying.Id, GraphPortDefaults.FlowOut, printLoading.Id, GraphPortDefaults.FlowIn),
                Value("CONN_Current_Print", currentAnimation.Id, GraphPortDefaults.ValueOut, printCurrent.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Playing_Print", animationPlaying.Id, GraphPortDefaults.ValueOut, printPlaying.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Loading_Print", meshLoading.Id, GraphPortDefaults.ValueOut, printLoading.Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "MeshGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("triggerObject.Loaded:Connect(function()", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, mesh = triggerObject }", luau);
        Assert.Contains("meshObject:PlayAnimation(tostring(\"Wave\"), 1.5, false)", luau);
        Assert.Contains("meshObject:StopAnimation(tostring(\"Wave\"))", luau);
        Assert.Contains("return tostring(targetObject.CurrentAnimation or \"\")", luau);
        Assert.Contains("return targetObject.IsAnimationPlaying == true", luau);
        Assert.Contains("return targetObject.Loading == true", luau);
        Assert.Contains("target mesh was not found", luau);
        Assert.DoesNotContain("Play Mesh Animation is not implemented", luau);
        Assert.DoesNotContain("Current Mesh Animation is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsEnvironmentAndBoundsActionsConditionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var setGravity = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetWorldGravity"), stableId: "ACT_SetGravity");
        var setDestroyHeight = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetPartDestroyHeight"), stableId: "ACT_SetDestroyHeight");
        var setAutoNav = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetAutoGenerateNavMesh"), stableId: "ACT_SetAutoNav");
        var rebuildNav = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_RebuildNavMesh"), stableId: "ACT_RebuildNav");
        var containsPoint = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_ObjectBoundsContainsPoint"), stableId: "COND_BoundsContains");
        var printCenter = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintBoundsCenter");
        var printSize = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintBoundsSize");
        var printExtents = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintBoundsExtents");
        var printVolume = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintBoundsVolume");
        var printGravity = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintGravity");
        var printCamera = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintCamera");
        var boundsCenter = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ObjectBoundsCenter"), stableId: "PROP_BoundsCenter");
        var boundsSize = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ObjectBoundsSize"), stableId: "PROP_BoundsSize");
        var boundsExtents = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ObjectBoundsExtents"), stableId: "PROP_BoundsExtents");
        var boundsVolume = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ObjectBoundsVolume"), stableId: "PROP_BoundsVolume");
        var worldGravity = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_WorldGravity"), stableId: "PROP_WorldGravity");
        var currentCamera = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_CurrentCamera"), stableId: "PROP_CurrentCamera");

        foreach (var node in new[] { containsPoint, boundsCenter, boundsSize, boundsExtents, boundsVolume })
        {
            SetSceneObject(node, "target", "World/Environment/Test Part");
        }

        SetConstant(setGravity, "gravity", "6.5");
        SetConstant(setDestroyHeight, "height", "-250");
        SetConstant(setAutoNav, "enabled", "false");
        SetConstant(containsPoint, "x", "1");
        SetConstant(containsPoint, "y", "2");
        SetConstant(containsPoint, "z", "3");

        var rule = new Rule
        {
            Id = "RULE_EnvironmentBounds",
            Name = "Environment Bounds",
            Nodes =
            [
                start,
                setGravity,
                setDestroyHeight,
                setAutoNav,
                rebuildNav,
                containsPoint,
                printCenter,
                printSize,
                printExtents,
                printVolume,
                printGravity,
                printCamera,
                boundsCenter,
                boundsSize,
                boundsExtents,
                boundsVolume,
                worldGravity,
                currentCamera
            ],
            Connections =
            [
                Flow("CONN_Start_SetGravity", start.Id, GraphPortDefaults.FlowOut, setGravity.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_SetGravity_SetDestroyHeight", setGravity.Id, GraphPortDefaults.FlowOut, setDestroyHeight.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_SetDestroyHeight_SetAutoNav", setDestroyHeight.Id, GraphPortDefaults.FlowOut, setAutoNav.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_SetAutoNav_Rebuild", setAutoNav.Id, GraphPortDefaults.FlowOut, rebuildNav.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Rebuild_Contains", rebuildNav.Id, GraphPortDefaults.FlowOut, containsPoint.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Contains_PrintCenter", containsPoint.Id, GraphPortDefaults.FlowOut, printCenter.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PrintCenter_PrintSize", printCenter.Id, GraphPortDefaults.FlowOut, printSize.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PrintSize_PrintExtents", printSize.Id, GraphPortDefaults.FlowOut, printExtents.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PrintExtents_PrintVolume", printExtents.Id, GraphPortDefaults.FlowOut, printVolume.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PrintVolume_PrintGravity", printVolume.Id, GraphPortDefaults.FlowOut, printGravity.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PrintGravity_PrintCamera", printGravity.Id, GraphPortDefaults.FlowOut, printCamera.Id, GraphPortDefaults.FlowIn),
                Value("CONN_Center_Print", boundsCenter.Id, GraphPortDefaults.ValueOut, printCenter.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Size_Print", boundsSize.Id, GraphPortDefaults.ValueOut, printSize.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Extents_Print", boundsExtents.Id, GraphPortDefaults.ValueOut, printExtents.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Volume_Print", boundsVolume.Id, GraphPortDefaults.ValueOut, printVolume.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Gravity_Print", worldGravity.Id, GraphPortDefaults.ValueOut, printGravity.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Camera_Print", currentCamera.Id, GraphPortDefaults.ValueOut, printCamera.Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "EnvironmentBoundsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("Environment.Gravity = 6.5", luau);
        Assert.Contains("Environment.PartDestroyHeight = -250", luau);
        Assert.Contains("Environment.AutoGenerateNavMesh = false", luau);
        Assert.Contains("Environment:RebuildNavMesh()", luau);
        Assert.Contains("local bounds = targetObject:GetBounds()", luau);
        Assert.Contains("return bounds:Contains(makeVector3(1, 2, 3))", luau);
        Assert.Contains("return bounds.Center", luau);
        Assert.Contains("return bounds.Size", luau);
        Assert.Contains("return bounds.Extents", luau);
        Assert.Contains("return tonumber(bounds.Volume) or 0", luau);
        Assert.Contains("return tonumber(Environment.Gravity) or 0", luau);
        Assert.Contains("return Environment.CurrentCamera", luau);
        Assert.DoesNotContain("Set World Gravity is not implemented", luau);
        Assert.DoesNotContain("Object Bounds Center is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsVector2HelpersConditionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Vector2Start");
        var closeEnough = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_Vector2DistanceAtMost"), stableId: "COND_Vector2Close");
        var vector = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_Vector2FromXY"), stableId: "PROP_Vector2");
        var vectorX = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_Vector2X"), stableId: "PROP_Vector2X");
        var vectorY = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_Vector2Y"), stableId: "PROP_Vector2Y");
        var magnitude = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_Vector2Magnitude"), stableId: "PROP_Vector2Magnitude");
        var normalized = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_Vector2Normalized"), stableId: "PROP_Vector2Normalized");
        var distance = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_Vector2Distance"), stableId: "PROP_Vector2Distance");
        var lerp = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_Vector2Lerp"), stableId: "PROP_Vector2Lerp");
        var printNodes = Enumerable.Range(0, 6)
            .Select(index => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintVector2_{index}"))
            .ToList();

        SetConstant(vector, "x", "3");
        SetConstant(vector, "y", "4");
        SetConstant(closeEnough, "second", "6,8");
        SetConstant(closeEnough, "maximum", "5");
        SetConstant(distance, "second", "6,8");
        SetConstant(lerp, "to", "10,20");
        SetConstant(lerp, "amount", "0.25");

        var rule = new Rule
        {
            Id = "RULE_Vector2",
            Name = "Vector2",
            Nodes = [start, closeEnough, vector, vectorX, vectorY, magnitude, normalized, distance, lerp, .. printNodes],
            Connections =
            [
                Flow("CONN_Vector2_Start_Check", start.Id, GraphPortDefaults.FlowOut, closeEnough.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Vector2_Check_Print0", closeEnough.Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Vector2_Print0_Print1", printNodes[0].Id, GraphPortDefaults.FlowOut, printNodes[1].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Vector2_Print1_Print2", printNodes[1].Id, GraphPortDefaults.FlowOut, printNodes[2].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Vector2_Print2_Print3", printNodes[2].Id, GraphPortDefaults.FlowOut, printNodes[3].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Vector2_Print3_Print4", printNodes[3].Id, GraphPortDefaults.FlowOut, printNodes[4].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Vector2_Print4_Print5", printNodes[4].Id, GraphPortDefaults.FlowOut, printNodes[5].Id, GraphPortDefaults.FlowIn),
                Value("CONN_Vector2_Check_First", vector.Id, GraphPortDefaults.ValueOut, closeEnough.Id, GraphPortDefaults.ParameterPortId("first")),
                Value("CONN_Vector2_X_Source", vector.Id, GraphPortDefaults.ValueOut, vectorX.Id, GraphPortDefaults.ParameterPortId("vector")),
                Value("CONN_Vector2_Y_Source", vector.Id, GraphPortDefaults.ValueOut, vectorY.Id, GraphPortDefaults.ParameterPortId("vector")),
                Value("CONN_Vector2_Magnitude_Source", vector.Id, GraphPortDefaults.ValueOut, magnitude.Id, GraphPortDefaults.ParameterPortId("vector")),
                Value("CONN_Vector2_Normalized_Source", vector.Id, GraphPortDefaults.ValueOut, normalized.Id, GraphPortDefaults.ParameterPortId("vector")),
                Value("CONN_Vector2_Distance_Source", vector.Id, GraphPortDefaults.ValueOut, distance.Id, GraphPortDefaults.ParameterPortId("first")),
                Value("CONN_Vector2_Lerp_Source", vector.Id, GraphPortDefaults.ValueOut, lerp.Id, GraphPortDefaults.ParameterPortId("from")),
                Value("CONN_Vector2_X_Print", vectorX.Id, GraphPortDefaults.ValueOut, printNodes[0].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Vector2_Y_Print", vectorY.Id, GraphPortDefaults.ValueOut, printNodes[1].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Vector2_Magnitude_Print", magnitude.Id, GraphPortDefaults.ValueOut, printNodes[2].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Vector2_Normalized_Print", normalized.Id, GraphPortDefaults.ValueOut, printNodes[3].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Vector2_Distance_Print", distance.Id, GraphPortDefaults.ValueOut, printNodes[4].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Vector2_Lerp_Print", lerp.Id, GraphPortDefaults.ValueOut, printNodes[5].Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "Vector2Graph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function makeVector2(x, y)", luau);
        Assert.Contains("local function vrsVector2Distance(first, second)", luau);
        Assert.Contains("return vrsVector2Distance(makeVector2(3, 4), makeVector2(6, 8)) <= 5", luau);
        Assert.Contains("vrsVector2Axis(makeVector2(3, 4), \"X\", \"x\", 0)", luau);
        Assert.Contains("vrsVector2Axis(makeVector2(3, 4), \"Y\", \"y\", 0)", luau);
        Assert.Contains("vrsVector2Magnitude(makeVector2(3, 4))", luau);
        Assert.Contains("vrsVector2Normalized(makeVector2(3, 4))", luau);
        Assert.Contains("vrsVector2Distance(makeVector2(3, 4), makeVector2(6, 8))", luau);
        Assert.Contains("vrsVector2Lerp(makeVector2(3, 4), makeVector2(10, 20), 0.25)", luau);
        Assert.DoesNotContain("Vector2 Distance At Most is not implemented", luau);
        Assert.DoesNotContain("Vector2 From X Y is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsRaycastConditionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_RaycastStart");
        var hits = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_RaycastHits"), stableId: "COND_RaycastHits");
        var result = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_RaycastResult"), stableId: "PROP_RaycastResult");
        var hitObject = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_RaycastHitObject"), stableId: "PROP_RaycastObject");
        var hitPosition = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_RaycastHitPosition"), stableId: "PROP_RaycastPosition");
        var hitNormal = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_RaycastHitNormal"), stableId: "PROP_RaycastNormal");
        var hitDistance = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_RaycastHitDistance"), stableId: "PROP_RaycastDistance");
        var printNodes = Enumerable.Range(0, 5)
            .Select(index => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintRaycast_{index}"))
            .ToList();

        foreach (var node in new[] { hits, result, hitObject, hitPosition, hitNormal, hitDistance })
        {
            SetConstant(node, "origin", "0,10,0");
            SetConstant(node, "direction", "0,-1,0");
            SetConstant(node, "maxDistance", "50");
        }

        var rule = new Rule
        {
            Id = "RULE_Raycast",
            Name = "Raycast",
            Nodes = [start, hits, result, hitObject, hitPosition, hitNormal, hitDistance, .. printNodes],
            Connections =
            [
                Flow("CONN_Raycast_Start_Hits", start.Id, GraphPortDefaults.FlowOut, hits.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Raycast_Hits_Print0", hits.Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Raycast_Print0_Print1", printNodes[0].Id, GraphPortDefaults.FlowOut, printNodes[1].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Raycast_Print1_Print2", printNodes[1].Id, GraphPortDefaults.FlowOut, printNodes[2].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Raycast_Print2_Print3", printNodes[2].Id, GraphPortDefaults.FlowOut, printNodes[3].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Raycast_Print3_Print4", printNodes[3].Id, GraphPortDefaults.FlowOut, printNodes[4].Id, GraphPortDefaults.FlowIn),
                Value("CONN_Raycast_Result_Print", result.Id, GraphPortDefaults.ValueOut, printNodes[0].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Raycast_Object_Print", hitObject.Id, GraphPortDefaults.ValueOut, printNodes[1].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Raycast_Position_Print", hitPosition.Id, GraphPortDefaults.ValueOut, printNodes[2].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Raycast_Normal_Print", hitNormal.Id, GraphPortDefaults.ValueOut, printNodes[3].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Raycast_Distance_Print", hitDistance.Id, GraphPortDefaults.ValueOut, printNodes[4].Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "RaycastGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function makeVector3(x, y, z)", luau);
        Assert.Contains("local raycastResult = Environment:Raycast(makeVector3(0, 10, 0), makeVector3(0, -1, 0), 50)", luau);
        Assert.Contains("return raycastResult ~= nil and raycastResult.Instance ~= nil", luau);
        Assert.Contains("return raycastResult", luau);
        Assert.Contains("return raycastResult.Instance", luau);
        Assert.Contains("return raycastResult.Position", luau);
        Assert.Contains("return raycastResult.Normal", luau);
        Assert.Contains("return tonumber(raycastResult.Distance) or 0", luau);
        Assert.DoesNotContain("Raycast Hits is not implemented", luau);
        Assert.DoesNotContain("Raycast Hit Object is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsQuaternionHelpersConditionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_QuaternionStart");
        var closeEnough = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_QuaternionAngleAtMost"), stableId: "COND_QuaternionClose");
        var identity = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_QuaternionIdentity"), stableId: "PROP_QuaternionIdentity");
        var components = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_QuaternionFromComponents"), stableId: "PROP_QuaternionComponents");
        var fromEuler = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_QuaternionFromEuler"), stableId: "PROP_QuaternionEuler");
        var toEuler = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_QuaternionToEuler"), stableId: "PROP_QuaternionToEuler");
        var axisAngle = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_QuaternionFromAxisAngle"), stableId: "PROP_QuaternionAxis");
        var lookRotation = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_QuaternionLookRotation"), stableId: "PROP_QuaternionLook");
        var fromTo = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_QuaternionFromToRotation"), stableId: "PROP_QuaternionFromTo");
        var inverse = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_QuaternionInverse"), stableId: "PROP_QuaternionInverse");
        var normalize = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_QuaternionNormalize"), stableId: "PROP_QuaternionNormalize");
        var lerp = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_QuaternionLerp"), stableId: "PROP_QuaternionLerp");
        var slerp = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_QuaternionSlerp"), stableId: "PROP_QuaternionSlerp");
        var rotateTowards = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_QuaternionRotateTowards"), stableId: "PROP_QuaternionRotateTowards");
        var angle = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_QuaternionAngle"), stableId: "PROP_QuaternionAngle");
        var dot = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_QuaternionDot"), stableId: "PROP_QuaternionDot");
        var printNodes = Enumerable.Range(0, 14)
            .Select(index => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintQuaternion_{index}"))
            .ToList();

        SetConstant(components, "y", "0.25");
        SetConstant(components, "w", "0.97");
        SetConstant(fromEuler, "euler", "0,90,0");
        SetConstant(axisAngle, "axis", "0,1,0");
        SetConstant(axisAngle, "angle", "45");
        SetConstant(lookRotation, "forward", "0,0,1");
        SetConstant(lookRotation, "upwards", "0,1,0");
        SetConstant(fromTo, "fromDirection", "0,0,1");
        SetConstant(fromTo, "toDirection", "1,0,0");
        SetConstant(closeEnough, "maximum", "15");
        SetConstant(rotateTowards, "maxDegrees", "30");

        var rule = new Rule
        {
            Id = "RULE_Quaternion",
            Name = "Quaternion",
            Nodes = [start, closeEnough, identity, components, fromEuler, toEuler, axisAngle, lookRotation, fromTo, inverse, normalize, lerp, slerp, rotateTowards, angle, dot, .. printNodes],
            Connections =
            [
                Flow("CONN_Quaternion_Start_Check", start.Id, GraphPortDefaults.FlowOut, closeEnough.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Quaternion_Check_Print0", closeEnough.Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Quaternion_Print0_Print1", printNodes[0].Id, GraphPortDefaults.FlowOut, printNodes[1].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Quaternion_Print1_Print2", printNodes[1].Id, GraphPortDefaults.FlowOut, printNodes[2].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Quaternion_Print2_Print3", printNodes[2].Id, GraphPortDefaults.FlowOut, printNodes[3].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Quaternion_Print3_Print4", printNodes[3].Id, GraphPortDefaults.FlowOut, printNodes[4].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Quaternion_Print4_Print5", printNodes[4].Id, GraphPortDefaults.FlowOut, printNodes[5].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Quaternion_Print5_Print6", printNodes[5].Id, GraphPortDefaults.FlowOut, printNodes[6].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Quaternion_Print6_Print7", printNodes[6].Id, GraphPortDefaults.FlowOut, printNodes[7].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Quaternion_Print7_Print8", printNodes[7].Id, GraphPortDefaults.FlowOut, printNodes[8].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Quaternion_Print8_Print9", printNodes[8].Id, GraphPortDefaults.FlowOut, printNodes[9].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Quaternion_Print9_Print10", printNodes[9].Id, GraphPortDefaults.FlowOut, printNodes[10].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Quaternion_Print10_Print11", printNodes[10].Id, GraphPortDefaults.FlowOut, printNodes[11].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Quaternion_Print11_Print12", printNodes[11].Id, GraphPortDefaults.FlowOut, printNodes[12].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Quaternion_Print12_Print13", printNodes[12].Id, GraphPortDefaults.FlowOut, printNodes[13].Id, GraphPortDefaults.FlowIn),
                Value("CONN_Quaternion_Check_First", fromEuler.Id, GraphPortDefaults.ValueOut, closeEnough.Id, GraphPortDefaults.ParameterPortId("first")),
                Value("CONN_Quaternion_Check_Second", axisAngle.Id, GraphPortDefaults.ValueOut, closeEnough.Id, GraphPortDefaults.ParameterPortId("second")),
                Value("CONN_Quaternion_ToEuler_Source", fromEuler.Id, GraphPortDefaults.ValueOut, toEuler.Id, GraphPortDefaults.ParameterPortId("rotation")),
                Value("CONN_Quaternion_Inverse_Source", components.Id, GraphPortDefaults.ValueOut, inverse.Id, GraphPortDefaults.ParameterPortId("rotation")),
                Value("CONN_Quaternion_Normalize_Source", components.Id, GraphPortDefaults.ValueOut, normalize.Id, GraphPortDefaults.ParameterPortId("rotation")),
                Value("CONN_Quaternion_Lerp_From", fromEuler.Id, GraphPortDefaults.ValueOut, lerp.Id, GraphPortDefaults.ParameterPortId("from")),
                Value("CONN_Quaternion_Lerp_To", axisAngle.Id, GraphPortDefaults.ValueOut, lerp.Id, GraphPortDefaults.ParameterPortId("to")),
                Value("CONN_Quaternion_Slerp_From", fromEuler.Id, GraphPortDefaults.ValueOut, slerp.Id, GraphPortDefaults.ParameterPortId("from")),
                Value("CONN_Quaternion_Slerp_To", axisAngle.Id, GraphPortDefaults.ValueOut, slerp.Id, GraphPortDefaults.ParameterPortId("to")),
                Value("CONN_Quaternion_Rotate_From", fromEuler.Id, GraphPortDefaults.ValueOut, rotateTowards.Id, GraphPortDefaults.ParameterPortId("from")),
                Value("CONN_Quaternion_Rotate_To", axisAngle.Id, GraphPortDefaults.ValueOut, rotateTowards.Id, GraphPortDefaults.ParameterPortId("to")),
                Value("CONN_Quaternion_Angle_First", fromEuler.Id, GraphPortDefaults.ValueOut, angle.Id, GraphPortDefaults.ParameterPortId("first")),
                Value("CONN_Quaternion_Angle_Second", axisAngle.Id, GraphPortDefaults.ValueOut, angle.Id, GraphPortDefaults.ParameterPortId("second")),
                Value("CONN_Quaternion_Dot_First", fromEuler.Id, GraphPortDefaults.ValueOut, dot.Id, GraphPortDefaults.ParameterPortId("first")),
                Value("CONN_Quaternion_Dot_Second", axisAngle.Id, GraphPortDefaults.ValueOut, dot.Id, GraphPortDefaults.ParameterPortId("second")),
                Value("CONN_Quaternion_Identity_Print", identity.Id, GraphPortDefaults.ValueOut, printNodes[0].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Quaternion_Components_Print", components.Id, GraphPortDefaults.ValueOut, printNodes[1].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Quaternion_Euler_Print", fromEuler.Id, GraphPortDefaults.ValueOut, printNodes[2].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Quaternion_ToEuler_Print", toEuler.Id, GraphPortDefaults.ValueOut, printNodes[3].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Quaternion_Axis_Print", axisAngle.Id, GraphPortDefaults.ValueOut, printNodes[4].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Quaternion_Look_Print", lookRotation.Id, GraphPortDefaults.ValueOut, printNodes[5].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Quaternion_FromTo_Print", fromTo.Id, GraphPortDefaults.ValueOut, printNodes[6].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Quaternion_Inverse_Print", inverse.Id, GraphPortDefaults.ValueOut, printNodes[7].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Quaternion_Normalize_Print", normalize.Id, GraphPortDefaults.ValueOut, printNodes[8].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Quaternion_Lerp_Print", lerp.Id, GraphPortDefaults.ValueOut, printNodes[9].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Quaternion_Slerp_Print", slerp.Id, GraphPortDefaults.ValueOut, printNodes[10].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Quaternion_Rotate_Print", rotateTowards.Id, GraphPortDefaults.ValueOut, printNodes[11].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Quaternion_Angle_Print", angle.Id, GraphPortDefaults.ValueOut, printNodes[12].Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Quaternion_Dot_Print", dot.Id, GraphPortDefaults.ValueOut, printNodes[13].Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "QuaternionGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function makeQuaternion(x, y, z, w)", luau);
        Assert.Contains("local function vrsQuaternionFromEuler(euler)", luau);
        Assert.Contains("return vrsQuaternionAngle(vrsQuaternionFromEuler(makeVector3(0, 90, 0)), vrsQuaternionFromAxisAngle(makeVector3(0, 1, 0), 45)) <= 15", luau);
        Assert.Contains("vrsQuaternionIdentity()", luau);
        Assert.Contains("makeQuaternion(0, 0.25, 0, 0.97)", luau);
        Assert.Contains("vrsQuaternionToEuler(vrsQuaternionFromEuler(makeVector3(0, 90, 0)))", luau);
        Assert.Contains("vrsQuaternionLookRotation(makeVector3(0, 0, 1), makeVector3(0, 1, 0))", luau);
        Assert.Contains("vrsQuaternionFromToRotation(makeVector3(0, 0, 1), makeVector3(1, 0, 0))", luau);
        Assert.Contains("vrsQuaternionInverse(makeQuaternion(0, 0.25, 0, 0.97))", luau);
        Assert.Contains("vrsQuaternionNormalize(makeQuaternion(0, 0.25, 0, 0.97))", luau);
        Assert.Contains("vrsQuaternionLerp(vrsQuaternionFromEuler(makeVector3(0, 90, 0)), vrsQuaternionFromAxisAngle(makeVector3(0, 1, 0), 45), 0.5)", luau);
        Assert.Contains("vrsQuaternionSlerp(vrsQuaternionFromEuler(makeVector3(0, 90, 0)), vrsQuaternionFromAxisAngle(makeVector3(0, 1, 0), 45), 0.5)", luau);
        Assert.Contains("vrsQuaternionRotateTowards(vrsQuaternionFromEuler(makeVector3(0, 90, 0)), vrsQuaternionFromAxisAngle(makeVector3(0, 1, 0), 45), 30)", luau);
        Assert.Contains("vrsQuaternionDot(vrsQuaternionFromEuler(makeVector3(0, 90, 0)), vrsQuaternionFromAxisAngle(makeVector3(0, 1, 0), 45))", luau);
        Assert.DoesNotContain("Rotation Angle At Most is not implemented", luau);
        Assert.DoesNotContain("Rotation From Euler is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsSeatTriggerActionConditionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var seatSat = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnSeatSat"), stableId: "TRG_SeatSat");
        var seatVacated = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnSeatVacated"), stableId: "TRG_SeatVacated");
        var setAllows = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetSeatAllowsNPCs"), stableId: "ACT_SetSeatAllows");
        var occupied = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_SeatIsOccupied"), stableId: "COND_SeatOccupied");
        var allows = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_SeatAllowsNPCs"), stableId: "COND_SeatAllows");
        var occupantValue = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_SeatOccupant"), stableId: "PROP_SeatOccupant");
        var allowsValue = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_SeatAllowsNPCs"), stableId: "PROP_SeatAllows");
        var printOccupant = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintOccupant");
        var printAllows = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintAllows");
        var vacatedMessage = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_VacatedMessage");

        foreach (var node in new[] { seatSat, seatVacated, setAllows, occupied, allows, occupantValue, allowsValue })
        {
            SetSceneObject(node, "target", "World/Environment/BenchSeat");
        }

        SetConstant(setAllows, "enabled", "false");
        SetConstant(vacatedMessage, "message", "Seat left");

        var rule = new Rule
        {
            Id = "RULE_Seat",
            Name = "Seat",
            Nodes = [seatSat, seatVacated, setAllows, occupied, allows, occupantValue, allowsValue, printOccupant, printAllows, vacatedMessage],
            Connections =
            [
                Flow("CONN_SeatSat_SetAllows", seatSat.Id, GraphPortDefaults.FlowOut, setAllows.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_SetAllows_Occupied", setAllows.Id, GraphPortDefaults.FlowOut, occupied.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Occupied_PrintOccupant", occupied.Id, GraphPortDefaults.TrueOut, printOccupant.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PrintOccupant_Allows", printOccupant.Id, GraphPortDefaults.FlowOut, allows.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Allows_PrintAllows", allows.Id, GraphPortDefaults.TrueOut, printAllows.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_SeatVacated_Message", seatVacated.Id, GraphPortDefaults.FlowOut, vacatedMessage.Id, GraphPortDefaults.FlowIn),
                Value("CONN_Occupant_Print", occupantValue.Id, GraphPortDefaults.ValueOut, printOccupant.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_AllowsValue_Print", allowsValue.Id, GraphPortDefaults.ValueOut, printAllows.Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "SeatGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("triggerObject.Sat:Connect(function(occupant)", luau);
        Assert.Contains("triggerObject.Vacated:Connect(function(occupant)", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, seat = triggerObject, occupant = occupant }", luau);
        Assert.Contains("local SET_SEAT_ALLOWS_NPCS_TARGET_NAME = \"World/Environment/BenchSeat\"", luau);
        Assert.Contains("local seatObject = resolveTarget(triggerObject, SET_SEAT_ALLOWS_NPCS_TARGET_NAME)", luau);
        Assert.Contains("seatObject.CanNPCSit = false", luau);
        Assert.Contains("return targetObject.Occupant ~= nil", luau);
        Assert.Contains("return targetObject.CanNPCSit == true", luau);
        Assert.Contains("return targetObject.Occupant", luau);
        Assert.Contains("targetObject.CanNPCSit == true", luau);
        Assert.DoesNotContain("Seat Occupant stopped: target does not expose Occupant.", luau);
        Assert.DoesNotContain("On Seat Sat is not implemented", luau);
        Assert.DoesNotContain("Set Seat Allows NPCs is not implemented", luau);
        Assert.DoesNotContain("Seat Occupant is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsImage3DActionsConditionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartImage3D");
        var setColor = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_Set3DImageColor"), stableId: "ACT_Image3DColor");
        var setShadows = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_Set3DImageShadows"), stableId: "ACT_Image3DShadows");
        var setLighting = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_Set3DImageLighting"), stableId: "ACT_Image3DLighting");
        var setFaceCamera = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_Set3DImageFaceCamera"), stableId: "ACT_Image3DFaceCamera");
        var setTextureScale = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_Set3DImageTextureScale"), stableId: "ACT_Image3DTextureScale");
        var setTextureOffset = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_Set3DImageTextureOffset"), stableId: "ACT_Image3DTextureOffset");
        var castsShadows = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_3DImageCastsShadows"), stableId: "COND_Image3DShadows");
        var usesLighting = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_3DImageUsesLighting"), stableId: "COND_Image3DLighting");
        var facesCamera = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_3DImageFacesCamera"), stableId: "COND_Image3DFaceCamera");
        var valueIds = new[]
        {
            "PROP_3DImageColor",
            "PROP_3DImageCastsShadows",
            "PROP_3DImageUsesLighting",
            "PROP_3DImageFacesCamera"
        };
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_Image3D_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintImage3D_{index}"))
            .ToList();

        foreach (var node in new[] { setColor, setShadows, setLighting, setFaceCamera, setTextureScale, setTextureOffset, castsShadows, usesLighting, facesCamera }.Concat(valueNodes))
        {
            SetSceneObject(node, "target", "World/Environment/BillboardImage");
        }

        SetConstant(setColor, "r", "0.1");
        SetConstant(setColor, "g", "0.2");
        SetConstant(setColor, "b", "0.3");
        SetConstant(setShadows, "enabled", "false");
        SetConstant(setLighting, "enabled", "true");
        SetConstant(setFaceCamera, "enabled", "true");
        SetConstant(setTextureScale, "x", "2");
        SetConstant(setTextureScale, "y", "3");
        SetConstant(setTextureOffset, "x", "0.25");
        SetConstant(setTextureOffset, "y", "0.5");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Image3D_Start_Color", start.Id, GraphPortDefaults.FlowOut, setColor.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Image3D_Color_Shadows", setColor.Id, GraphPortDefaults.FlowOut, setShadows.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Image3D_Shadows_Lighting", setShadows.Id, GraphPortDefaults.FlowOut, setLighting.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Image3D_Lighting_Face", setLighting.Id, GraphPortDefaults.FlowOut, setFaceCamera.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Image3D_Face_Scale", setFaceCamera.Id, GraphPortDefaults.FlowOut, setTextureScale.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Image3D_Scale_Offset", setTextureScale.Id, GraphPortDefaults.FlowOut, setTextureOffset.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Image3D_Offset_ShadowCheck", setTextureOffset.Id, GraphPortDefaults.FlowOut, castsShadows.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Image3D_ShadowCheck_LightCheck", castsShadows.Id, GraphPortDefaults.FlowOut, usesLighting.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Image3D_LightCheck_FaceCheck", usesLighting.Id, GraphPortDefaults.FlowOut, facesCamera.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Image3D_FaceCheck_Print0", facesCamera.Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_Image3DValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_PrintImage3D_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_Image3D",
            Name = "Image3D",
            Nodes = [start, setColor, setShadows, setLighting, setFaceCamera, setTextureScale, setTextureOffset, castsShadows, usesLighting, facesCamera, .. valueNodes, .. printNodes],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "Image3DGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function makeVector2(x, y)", luau);
        Assert.Contains("imageObject.Color = Color.New(0.1, 0.2, 0.3, 1)", luau);
        Assert.Contains("imageObject.CastShadows = false", luau);
        Assert.Contains("imageObject.Shaded = true", luau);
        Assert.Contains("imageObject.FaceCamera = true", luau);
        Assert.Contains("imageObject.TextureScale = makeVector2(2, 3)", luau);
        Assert.Contains("imageObject.TextureOffset = makeVector2(0.25, 0.5)", luau);
        Assert.Contains("return imageObject.CastShadows == true", luau);
        Assert.Contains("return imageObject.Shaded == true", luau);
        Assert.Contains("return imageObject.FaceCamera == true", luau);
        Assert.Contains("return targetObject.Color", luau);
        Assert.Contains("return targetObject.CastShadows == true", luau);
        Assert.Contains("return targetObject.Shaded == true", luau);
        Assert.Contains("return targetObject.FaceCamera == true", luau);
        Assert.DoesNotContain("Set 3D Image Color is not implemented", luau);
        Assert.DoesNotContain("3D Image Color is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsImage3DAdditionalConditions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartImage3DConditions");
        var color = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_3DImageColorIs"), stableId: "COND_Image3DColorIs");
        var scale = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_3DImageTextureScaleIs"), stableId: "COND_Image3DScaleIs");
        var offset = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_3DImageTextureOffsetIs"), stableId: "COND_Image3DOffsetIs");
        var message = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_Image3DConditionMessage");

        foreach (var node in new[] { color, scale, offset })
        {
            SetSceneObject(node, "target", "World/Environment/BillboardImage");
        }

        SetConstant(color, "r", "0.1");
        SetConstant(color, "g", "0.2");
        SetConstant(color, "b", "0.3");
        SetConstant(scale, "x", "2");
        SetConstant(scale, "y", "3");
        SetConstant(offset, "x", "0.25");
        SetConstant(offset, "y", "0.5");

        var rule = new Rule
        {
            Id = "RULE_Image3DConditions",
            Name = "Image3DConditions",
            Nodes = [start, color, scale, offset, message],
            Connections =
            [
                Flow("CONN_Image3DCondition_Start_Color", start.Id, GraphPortDefaults.FlowOut, color.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Image3DCondition_Color_Scale", color.Id, GraphPortDefaults.TrueOut, scale.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Image3DCondition_Scale_Offset", scale.Id, GraphPortDefaults.TrueOut, offset.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Image3DCondition_Offset_Message", offset.Id, GraphPortDefaults.TrueOut, message.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "Image3DConditionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local expectedColor = Color.New(0.1, 0.2, 0.3, 1)", luau);
        Assert.Contains("return imageObject.Color == expectedColor", luau);
        Assert.Contains("local currentX = vrsVector2Axis(imageObject.TextureScale, \"X\", \"x\", 1)", luau);
        Assert.Contains("local currentY = vrsVector2Axis(imageObject.TextureScale, \"Y\", \"y\", 1)", luau);
        Assert.Contains("local expectedX = tonumber(2) or 1", luau);
        Assert.Contains("local expectedY = tonumber(3) or 1", luau);
        Assert.Contains("local currentX = vrsVector2Axis(imageObject.TextureOffset, \"X\", \"x\", 0)", luau);
        Assert.Contains("local currentY = vrsVector2Axis(imageObject.TextureOffset, \"Y\", \"y\", 0)", luau);
        Assert.Contains("local expectedX = tonumber(0.25) or 0", luau);
        Assert.Contains("local expectedY = tonumber(0.5) or 0", luau);
        Assert.Contains("return currentX == expectedX and currentY == expectedY", luau);
        Assert.DoesNotContain("3D Image Color Is is not implemented", luau);
        Assert.DoesNotContain("3D Image Texture Offset Is is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsImage3DWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var triggerIds = new[]
        {
            "EV_On3DImageColorChanged",
            "EV_On3DImageShadowsEnabled",
            "EV_On3DImageLightingEnabled",
            "EV_On3DImageFaceCameraEnabled",
            "EV_On3DImageTextureScaleChanged",
            "EV_On3DImageTextureOffsetChanged"
        };
        var triggers = triggerIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"TRG_Image3DWatcher_{index}"))
            .ToList();
        var message = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_Image3DWatcherMessage");

        foreach (var trigger in triggers)
        {
            SetSceneObject(trigger, "target", "World/Environment/BillboardImage");
            SetConstant(trigger, "interval", "0.1");
        }

        var connections = triggers
            .Select((trigger, index) => Flow($"CONN_Image3DWatcher_{index}", trigger.Id, GraphPortDefaults.FlowOut, message.Id, GraphPortDefaults.FlowIn))
            .ToList();

        var rule = new Rule
        {
            Id = "RULE_Image3DWatchers",
            Name = "Image3DWatchers",
            Nodes = [.. triggers, message],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "Image3DWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("if triggerObject.Color == nil then", luau);
        Assert.Contains("return triggerObject.Color", luau);
        Assert.Contains("if triggerObject.CastShadows == nil then", luau);
        Assert.Contains("return triggerObject.CastShadows == true", luau);
        Assert.Contains("if triggerObject.Shaded == nil then", luau);
        Assert.Contains("return triggerObject.Shaded == true", luau);
        Assert.Contains("if triggerObject.FaceCamera == nil then", luau);
        Assert.Contains("return triggerObject.FaceCamera == true", luau);
        Assert.Contains("if triggerObject.TextureScale == nil then", luau);
        Assert.Contains("return triggerObject.TextureScale", luau);
        Assert.Contains("if triggerObject.TextureOffset == nil then", luau);
        Assert.Contains("return triggerObject.TextureOffset", luau);
        Assert.Contains("local previousValue = readWatchedValue()", luau);
        Assert.Contains("local previousMatched = readMatched() == true", luau);
        Assert.Contains("image3DTextureOffset = currentValue", luau);
        Assert.DoesNotContain("On 3D Image Color Changed is not implemented", luau);
        Assert.DoesNotContain("On 3D Image Texture Offset Changed is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsText3DActionsConditionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartText3D");
        var actionIds = new[]
        {
            "ACT_Set3DText",
            "ACT_Set3DTextFontSize",
            "ACT_Set3DTextRichText",
            "ACT_Set3DTextColor",
            "ACT_Set3DTextOutlineWidth",
            "ACT_Set3DTextOutlineColor",
            "ACT_Set3DTextFaceCamera",
            "ACT_Set3DTextLighting"
        };
        var conditionIds = new[]
        {
            "COND_3DTextIs",
            "COND_3DTextIsEmpty",
            "COND_3DTextFacesCamera",
            "COND_3DTextUsesRichText",
            "COND_3DTextUsesLighting"
        };
        var valueIds = new[]
        {
            "PROP_3DText",
            "PROP_3DTextFontSize",
            "PROP_3DTextColor",
            "PROP_3DTextOutlineWidth",
            "PROP_3DTextOutlineColor",
            "PROP_3DTextFacesCamera",
            "PROP_3DTextUsesRichText",
            "PROP_3DTextUsesLighting"
        };
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"ACT_Text3D_{index}"))
            .ToList();
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"COND_Text3D_{index}"))
            .ToList();
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_Text3D_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintText3D_{index}"))
            .ToList();

        foreach (var node in actions.Concat(conditions).Concat(valueNodes))
        {
            SetSceneObject(node, "target", "World/Environment/FloatingLabel");
        }

        SetConstant(actions[0], "text", "Go");
        SetConstant(actions[1], "size", "24");
        SetConstant(actions[2], "enabled", "true");
        SetConstant(actions[3], "r", "0.9");
        SetConstant(actions[3], "g", "0.8");
        SetConstant(actions[3], "b", "0.1");
        SetConstant(actions[4], "width", "2");
        SetConstant(actions[5], "r", "0.1");
        SetConstant(actions[5], "g", "0.1");
        SetConstant(actions[5], "b", "0.1");
        SetConstant(actions[6], "enabled", "true");
        SetConstant(actions[7], "enabled", "false");
        SetConstant(conditions[0], "text", "Go");
        SetConstant(conditions[0], "caseSensitive", "true");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Text3D_Start_Action0", start.Id, GraphPortDefaults.FlowOut, actions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < actions.Count - 1; index++)
        {
            connections.Add(Flow($"CONN_Text3D_Action_{index}_{index + 1}", actions[index].Id, GraphPortDefaults.FlowOut, actions[index + 1].Id, GraphPortDefaults.FlowIn));
        }

        connections.Add(Flow("CONN_Text3D_Actions_Condition0", actions[^1].Id, GraphPortDefaults.FlowOut, conditions[0].Id, GraphPortDefaults.FlowIn));
        for (var index = 0; index < conditions.Count - 1; index++)
        {
            connections.Add(Flow($"CONN_Text3D_Condition_{index}_{index + 1}", conditions[index].Id, GraphPortDefaults.FlowOut, conditions[index + 1].Id, GraphPortDefaults.FlowIn));
        }

        connections.Add(Flow("CONN_Text3D_Conditions_Print0", conditions[^1].Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn));
        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_Text3DValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_PrintText3D_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_Text3D",
            Name = "Text3D",
            Nodes = [start, .. actions, .. conditions, .. valueNodes, .. printNodes],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "Text3DGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("textObject.Text = tostring(\"Go\")", luau);
        Assert.Contains("textObject.FontSize = 24", luau);
        Assert.Contains("textObject.UseRichText = true", luau);
        Assert.Contains("textObject.Color = Color.New(0.9, 0.8, 0.1, 1)", luau);
        Assert.Contains("textObject.OutlineWidth = 2", luau);
        Assert.Contains("textObject.OutlineColor = Color.New(0.1, 0.1, 0.1, 1)", luau);
        Assert.Contains("textObject.FaceCamera = true", luau);
        Assert.Contains("textObject.Shaded = false", luau);
        Assert.Contains("local currentText = tostring(textObject.Text or \"\")", luau);
        Assert.Contains("return currentText == expectedText", luau);
        Assert.Contains("return textObject.Text == nil or tostring(textObject.Text) == \"\"", luau);
        Assert.Contains("return textObject.FaceCamera == true", luau);
        Assert.Contains("return textObject.UseRichText == true", luau);
        Assert.Contains("return textObject.Shaded == true", luau);
        Assert.Contains("return tostring(targetObject.Text or \"\")", luau);
        Assert.Contains("return tonumber(targetObject.FontSize) or 0", luau);
        Assert.Contains("return targetObject.Color", luau);
        Assert.Contains("return tonumber(targetObject.OutlineWidth) or 0", luau);
        Assert.Contains("return targetObject.OutlineColor", luau);
        Assert.Contains("return targetObject.FaceCamera == true", luau);
        Assert.Contains("return targetObject.UseRichText == true", luau);
        Assert.Contains("return targetObject.Shaded == true", luau);
        Assert.DoesNotContain("Set 3D Text is not implemented", luau);
        Assert.DoesNotContain("3D Text is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsText3DAdditionalConditionsAndWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartText3DWatchers");
        var conditionIds = new[]
        {
            "COND_3DTextFontSizeAtLeast",
            "COND_3DTextColorIs",
            "COND_3DTextOutlineWidthAtLeast"
        };
        var triggerIds = new[]
        {
            "EV_On3DTextChanged",
            "EV_On3DTextSizeReached",
            "EV_On3DTextColorChanged",
            "EV_On3DTextOutlineWidthReached",
            "EV_On3DTextOutlineColorChanged",
            "EV_On3DTextFaceCameraEnabled",
            "EV_On3DTextRichTextEnabled",
            "EV_On3DTextLightingEnabled"
        };
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"COND_Text3DExtra_{index}"))
            .ToList();
        var triggers = triggerIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"TRG_Text3DWatcher_{index}"))
            .ToList();
        var printAfterConditions = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintText3DExtraConditions");
        var triggerPrints = triggerIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintText3DWatcher_{index}"))
            .ToList();

        foreach (var node in conditions.Concat(triggers))
        {
            SetSceneObject(node, "target", "World/Labels/ScoreText");
        }

        SetConstant(conditions[0], "size", "32");
        SetConstant(conditions[1], "r", "0.2");
        SetConstant(conditions[1], "g", "0.8");
        SetConstant(conditions[1], "b", "1");
        SetConstant(conditions[2], "width", "2");
        SetConstant(triggers[1], "size", "32");
        SetConstant(triggers[3], "width", "2");
        SetConstant(printAfterConditions, "value", "text3d extra conditions passed");
        for (var index = 0; index < triggerPrints.Count; index++)
        {
            SetConstant(triggerPrints[index], "value", $"text3d watcher {index}");
        }

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Text3DExtra_Start_Condition0", start.Id, GraphPortDefaults.FlowOut, conditions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < conditions.Count - 1; index++)
        {
            connections.Add(Flow($"CONN_Text3DExtra_Condition_{index}_{index + 1}", conditions[index].Id, GraphPortDefaults.FlowOut, conditions[index + 1].Id, GraphPortDefaults.FlowIn));
        }

        connections.Add(Flow("CONN_Text3DExtra_ConditionLast_Print", conditions[^1].Id, GraphPortDefaults.FlowOut, printAfterConditions.Id, GraphPortDefaults.FlowIn));
        for (var index = 0; index < triggers.Count; index++)
        {
            connections.Add(Flow($"CONN_Text3DWatcher_{index}_Print", triggers[index].Id, GraphPortDefaults.FlowOut, triggerPrints[index].Id, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_Text3DWatchers",
            Name = "Text3D Watchers",
            Nodes = [start, .. conditions, printAfterConditions, .. triggers, .. triggerPrints],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "Text3DWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local currentNumber = tonumber(textObject.FontSize) or 0", luau);
        Assert.Contains("local expectedNumber = tonumber(32) or 24", luau);
        Assert.Contains("local expectedColor = Color.New(0.2, 0.8, 1, 1)", luau);
        Assert.Contains("return textObject.Color == expectedColor", luau);
        Assert.Contains("local currentNumber = tonumber(textObject.OutlineWidth) or 0", luau);
        Assert.Contains("local expectedNumber = tonumber(2) or 1", luau);
        Assert.Contains("return currentNumber >= expectedNumber", luau);
        Assert.Contains("return tostring(triggerObject.Text or \"\")", luau);
        Assert.Contains("local watchedLimit = tonumber(", luau);
        Assert.Contains("or 24", luau);
        Assert.Contains("return tonumber(triggerObject.FontSize) or 0", luau);
        Assert.Contains("return triggerObject.Color", luau);
        Assert.Contains("or 1", luau);
        Assert.Contains("return tonumber(triggerObject.OutlineWidth) or 0", luau);
        Assert.Contains("return triggerObject.OutlineColor", luau);
        Assert.Contains("return triggerObject.FaceCamera == true", luau);
        Assert.Contains("return triggerObject.UseRichText == true", luau);
        Assert.Contains("return triggerObject.Shaded == true", luau);
        Assert.Contains("text3DText = currentValue", luau);
        Assert.Contains("text3DFontSize = currentValue", luau);
        Assert.Contains("text3DOutlineWidth = currentValue", luau);
        Assert.Contains("text3DFacesCamera = currentValue", luau);
        Assert.Contains("text3d extra conditions passed", luau);
        Assert.Contains("text3d watcher 7", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsNpcActionsConditionsValuesAndTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var died = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnNPCDied"), stableId: "TRG_NpcDied");
        var landed = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnNPCLanded"), stableId: "TRG_NpcLanded");
        var navFinished = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnNPCNavigationFinished"), stableId: "TRG_NpcNavFinished");
        var actionIds = new[]
        {
            "ACT_SetNPCHealth",
            "ACT_DamageNPC",
            "ACT_HealNPC",
            "ACT_KillNPC",
            "ACT_SetNPCWalkSpeed",
            "ACT_SetNPCJumpPower",
            "ACT_MakeNPCJump",
            "ACT_SetNPCNavigationTarget"
        };
        var conditionIds = new[]
        {
            "COND_NPCIsDead",
            "COND_NPCIsOnGround",
            "COND_NPCHealthAtMost",
            "COND_NPCReachedNavigationTarget"
        };
        var valueIds = new[]
        {
            "PROP_NPCHealth",
            "PROP_NPCWalkSpeed",
            "PROP_NPCJumpPower",
            "PROP_NPCIsDead",
            "PROP_NPCIsOnGround",
            "PROP_NPCNavigationDistance"
        };
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"ACT_Npc_{index}"))
            .ToList();
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"COND_Npc_{index}"))
            .ToList();
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_Npc_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintNpc_{index}"))
            .ToList();
        var landedPrint = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintNpcLanded");
        var navPrint = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintNpcNav");

        foreach (var node in new[] { died, landed, navFinished }.Concat(actions).Concat(conditions).Concat(valueNodes))
        {
            SetSceneObject(node, "target", "World/Characters/GuideNPC");
        }

        SetConstant(actions[0], "amount", "80");
        SetConstant(actions[1], "amount", "12");
        SetConstant(actions[2], "amount", "7");
        SetConstant(actions[4], "speed", "20");
        SetConstant(actions[5], "power", "60");
        SetConstant(actions[7], "x", "1");
        SetConstant(actions[7], "y", "2");
        SetConstant(actions[7], "z", "3");
        SetConstant(conditions[2], "amount", "25");
        SetConstant(landedPrint, "value", "landed");
        SetConstant(navPrint, "value", "navigation done");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Npc_Died_Action0", died.Id, GraphPortDefaults.FlowOut, actions[0].Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Npc_Landed_Print", landed.Id, GraphPortDefaults.FlowOut, landedPrint.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Npc_Nav_Print", navFinished.Id, GraphPortDefaults.FlowOut, navPrint.Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < actions.Count - 1; index++)
        {
            connections.Add(Flow($"CONN_Npc_Action_{index}_{index + 1}", actions[index].Id, GraphPortDefaults.FlowOut, actions[index + 1].Id, GraphPortDefaults.FlowIn));
        }

        connections.Add(Flow("CONN_Npc_Actions_Condition0", actions[^1].Id, GraphPortDefaults.FlowOut, conditions[0].Id, GraphPortDefaults.FlowIn));
        for (var index = 0; index < conditions.Count - 1; index++)
        {
            connections.Add(Flow($"CONN_Npc_Condition_{index}_{index + 1}", conditions[index].Id, GraphPortDefaults.FlowOut, conditions[index + 1].Id, GraphPortDefaults.FlowIn));
        }

        connections.Add(Flow("CONN_Npc_Conditions_Print0", conditions[^1].Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn));
        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_NpcValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_PrintNpc_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_NPC",
            Name = "NPC",
            Nodes = [died, landed, navFinished, .. actions, .. conditions, .. valueNodes, .. printNodes, landedPrint, navPrint],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "NpcGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("triggerObject.Died:Connect(function()", luau);
        Assert.Contains("triggerObject.Landed:Connect(function()", luau);
        Assert.Contains("triggerObject.NavFinished:Connect(function()", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, npc = triggerObject }", luau);
        Assert.Contains("npcObject.Health = 80", luau);
        Assert.Contains("npcObject:TakeDamage(12)", luau);
        Assert.Contains("npcObject:Heal(7)", luau);
        Assert.Contains("npcObject:Kill()", luau);
        Assert.Contains("npcObject.WalkSpeed = 20", luau);
        Assert.Contains("npcObject.JumpPower = 60", luau);
        Assert.Contains("npcObject:Jump()", luau);
        Assert.Contains("npcObject:SetNavDestination(makeVector3(1, 2, 3))", luau);
        Assert.Contains("return npcObject.IsDead == true", luau);
        Assert.Contains("return npcObject.IsOnGround == true", luau);
        Assert.Contains("return (tonumber(npcObject.Health) or 0) <= 25", luau);
        Assert.Contains("return npcObject.NavDestinationReached == true", luau);
        Assert.Contains("return tonumber(targetObject.Health) or 0", luau);
        Assert.Contains("return tonumber(targetObject.WalkSpeed) or 0", luau);
        Assert.Contains("return tonumber(targetObject.JumpPower) or 0", luau);
        Assert.Contains("return targetObject.IsDead == true", luau);
        Assert.Contains("return targetObject.IsOnGround == true", luau);
        Assert.Contains("return tonumber(targetObject.NavDestinationDistance) or 0", luau);
        Assert.DoesNotContain("NPC Health is not implemented", luau);
        Assert.DoesNotContain("On NPC Died is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsCoreUiActionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var setBuiltInUi = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetBuiltInUIVisible"), stableId: "ACT_SetBuiltInUi");
        var valueIds = new[]
        {
            "PROP_BuiltInChatVisible",
            "PROP_BuiltInLeaderboardVisible",
            "PROP_BuiltInHealthBarVisible",
            "PROP_BuiltInHotbarVisible",
            "PROP_BuiltInBackpackAvailable",
            "PROP_BuiltInMenuButtonVisible",
            "PROP_BuiltInEmoteWheelVisible",
            "PROP_BuiltInUserCardVisible",
            "PROP_PlayerCanRespawn"
        };
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_CoreUi_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintCoreUi_{index}"))
            .ToList();

        SetConstant(setBuiltInUi, "feature", "Health Bar");
        SetConstant(setBuiltInUi, "enabled", "false");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_SetBuiltInUi", start.Id, GraphPortDefaults.FlowOut, setBuiltInUi.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_SetBuiltInUi_Print0", setBuiltInUi.Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_CoreUiValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_PrintCoreUi_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_CoreUI",
            Name = "CoreUI",
            Nodes = [start, setBuiltInUi, .. valueNodes, .. printNodes],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "CoreUIGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("if CoreUI == nil then", luau);
        Assert.Contains("local builtInUiFeature = tostring(\"Health Bar\")", luau);
        Assert.Contains("[\"Health Bar\"] = \"UseHealthBar\"", luau);
        Assert.Contains("CoreUI[builtInUiProperty] = false", luau);
        Assert.Contains("return CoreUI.UseChat == true", luau);
        Assert.Contains("return CoreUI.UseLeaderboard == true", luau);
        Assert.Contains("return CoreUI.UseHealthBar == true", luau);
        Assert.Contains("return CoreUI.UseHotbar == true", luau);
        Assert.Contains("return CoreUI.UseBackpack == true", luau);
        Assert.Contains("return CoreUI.UseMenuButton == true", luau);
        Assert.Contains("return CoreUI.UseEmoteWheel == true", luau);
        Assert.Contains("return CoreUI.UseUserCard == true", luau);
        Assert.Contains("return CoreUI.CanRespawn == true", luau);
        Assert.DoesNotContain("Set Built-In UI Visible is not implemented", luau);
        Assert.DoesNotContain("Chat Is Visible is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsCustomUiTriggerActionsConditionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var buttonClicked = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnUIButtonClicked"), stableId: "TRG_UiButton");
        var setText = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetUIText"), stableId: "ACT_SetUiText");
        var setColor = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetUIColor"), stableId: "ACT_SetUiColor");
        var setWrapped = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetUITextWrapped"), stableId: "ACT_SetUiWrapped");
        var textIs = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_UITextIs"), stableId: "COND_UiTextIs");
        var textEmpty = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_UITextIsEmpty"), stableId: "COND_UiTextEmpty");
        var textWrapped = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_UITextWrapped"), stableId: "COND_UiTextWrapped");
        var valueIds = new[]
        {
            "PROP_UIText",
            "PROP_UIColor",
            "PROP_UIFontSize",
            "PROP_UITextWrapped"
        };
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_CustomUi_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintCustomUi_{index}"))
            .ToList();

        foreach (var node in new[] { buttonClicked, setText, setColor, setWrapped, textIs, textEmpty, textWrapped }.Concat(valueNodes))
        {
            SetSceneObject(node, "target", "World/PlayerGUI/MainMenu/PlayButton");
        }

        SetConstant(setText, "text", "Ready");
        SetConstant(setColor, "r", "0.2");
        SetConstant(setColor, "g", "0.4");
        SetConstant(setColor, "b", "0.6");
        SetConstant(setWrapped, "enabled", "true");
        SetConstant(textIs, "text", "Ready");
        SetConstant(textIs, "caseSensitive", "true");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_UiButton_SetText", buttonClicked.Id, GraphPortDefaults.FlowOut, setText.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_SetText_SetColor", setText.Id, GraphPortDefaults.FlowOut, setColor.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_SetColor_SetWrapped", setColor.Id, GraphPortDefaults.FlowOut, setWrapped.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_SetWrapped_TextIs", setWrapped.Id, GraphPortDefaults.FlowOut, textIs.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_TextIs_TextEmpty", textIs.Id, GraphPortDefaults.FlowOut, textEmpty.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_TextEmpty_TextWrapped", textEmpty.Id, GraphPortDefaults.FlowOut, textWrapped.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_TextWrapped_Print0", textWrapped.Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_CustomUiValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_PrintCustomUi_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_CustomUI",
            Name = "CustomUI",
            Nodes = [buttonClicked, setText, setColor, setWrapped, textIs, textEmpty, textWrapped, .. valueNodes, .. printNodes],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "CustomUIGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("\"World/PlayerGUI/MainMenu/PlayButton\"", luau);
        Assert.Contains("triggerObject.Clicked:Connect(function()", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, uiButton = triggerObject }", luau);
        Assert.Contains("labelObject.Text = tostring(\"Ready\")", luau);
        Assert.Contains("uiObject.Color = Color.New(0.2, 0.4, 0.6, 1)", luau);
        Assert.Contains("labelObject.TextWrapped = true", luau);
        Assert.Contains("return currentText == expectedText", luau);
        Assert.Contains("return targetObject.Text == nil or tostring(targetObject.Text) == \"\"", luau);
        Assert.Contains("return targetObject.TextWrapped == true", luau);
        Assert.Contains("return tostring(targetObject.Text or \"\")", luau);
        Assert.Contains("return targetObject.Color", luau);
        Assert.Contains("return tonumber(targetObject.FontSize) or 0", luau);
        Assert.DoesNotContain("On UI Button Clicked is not implemented", luau);
        Assert.DoesNotContain("Set UI Text is not implemented", luau);
        Assert.DoesNotContain("UI Text is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsExpandedCustomUiConditions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var conditionIds = new[]
        {
            "COND_UIVisible",
            "COND_UIHidden",
            "COND_UIImageIs",
            "COND_UIImageHasImage",
            "COND_TextInputTextIs",
            "COND_TextInputIsEmpty",
            "COND_TextInputReadOnly",
            "COND_TextInputEditable",
            "COND_UIFieldIgnoresMouse",
            "COND_UIFieldClipsChildren",
            "COND_Gui3DShaded",
            "COND_Gui3DFacesCamera",
            "COND_Gui3DTransparent",
            "COND_GridColumnsAtLeast",
            "COND_ScrollViewHorizontalModeIs"
        };
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"COND_ExpandedUi_{index}"))
            .ToList();
        var message = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_Message");

        foreach (var node in conditions.Take(4))
        {
            SetSceneObject(node, "target", "World/PlayerGUI/Hud/Icon");
        }

        foreach (var node in conditions.Skip(4).Take(4))
        {
            SetSceneObject(node, "target", "World/PlayerGUI/Menu/NameInput");
        }

        foreach (var node in conditions.Skip(8).Take(2))
        {
            SetSceneObject(node, "target", "World/PlayerGUI/Menu/Panel");
        }

        foreach (var node in conditions.Skip(10).Take(3))
        {
            SetSceneObject(node, "target", "World/Signs/QuestGui");
        }

        SetSceneObject(conditions[13], "target", "World/PlayerGUI/Menu/Grid");
        SetSceneObject(conditions[14], "target", "World/PlayerGUI/Menu/List");
        SetConstant(conditions[2], "image", "images/icon.png");
        SetConstant(conditions[2], "caseSensitive", "true");
        SetConstant(conditions[4], "text", "PlayerName");
        SetConstant(conditions[4], "caseSensitive", "false");
        SetConstant(conditions[13], "columns", "3");
        SetConstant(conditions[14], "mode", "Auto");
        SetConstant(conditions[14], "caseSensitive", "true");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_Condition0", start.Id, GraphPortDefaults.FlowOut, conditions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < conditions.Count; index++)
        {
            var toNodeId = index + 1 < conditions.Count ? conditions[index + 1].Id : message.Id;
            connections.Add(Flow($"CONN_ExpandedUiCondition_{index}", conditions[index].Id, GraphPortDefaults.TrueOut, toNodeId, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_ExpandedCustomUI",
            Name = "ExpandedCustomUI",
            Nodes = [start, .. conditions, message],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "ExpandedCustomUIGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("return (targetObject.Visible == true) == true", luau);
        Assert.Contains("return (targetObject.Visible == true) == false", luau);
        Assert.Contains("local currentText = tostring(targetObject.Image or \"\")", luau);
        Assert.Contains("local expectedText = tostring(\"images/icon.png\")", luau);
        Assert.Contains("return tostring(targetObject.Image or \"\") ~= \"\"", luau);
        Assert.Contains("local currentText = tostring(targetObject.Text or \"\")", luau);
        Assert.Contains("return tostring(targetObject.Text or \"\") == \"\"", luau);
        Assert.Contains("return (targetObject.ReadOnly == true) == true", luau);
        Assert.Contains("return (targetObject.ReadOnly == true) == false", luau);
        Assert.Contains("return (targetObject.IgnoreMouse == true) == true", luau);
        Assert.Contains("return (targetObject.ClipDescendants == true) == true", luau);
        Assert.Contains("return (targetObject.Shaded == true) == true", luau);
        Assert.Contains("return (targetObject.FaceCamera == true) == true", luau);
        Assert.Contains("return (targetObject.Transparent == true) == true", luau);
        Assert.Contains("local currentNumber = tonumber(targetObject.Columns) or 0", luau);
        Assert.Contains("return currentNumber >= expectedNumber", luau);
        Assert.Contains("local currentText = tostring(targetObject.HorizontalScrollMode or \"\")", luau);
        Assert.DoesNotContain("UI Is Visible is not implemented", luau);
        Assert.DoesNotContain("Horizontal Scroll Mode Is is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsCustomUiWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var triggerIds = new[]
        {
            "EV_OnUIBecameVisible",
            "EV_OnUIBecameHidden",
            "EV_OnUIImageChanged",
            "EV_OnTextInputBecameEmpty",
            "EV_OnTextInputBecameReadOnly",
            "EV_OnUIFieldStartedIgnoringMouse",
            "EV_OnGui3DShadedEnabled",
            "EV_OnGridColumnsReached",
            "EV_OnScrollViewHorizontalModeChanged"
        };
        var triggers = triggerIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"TRG_CustomUiWatcher_{index}"))
            .ToList();
        var message = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_Message");

        foreach (var node in triggers.Take(3))
        {
            SetSceneObject(node, "target", "World/PlayerGUI/Hud/Icon");
        }

        foreach (var node in triggers.Skip(3).Take(2))
        {
            SetSceneObject(node, "target", "World/PlayerGUI/Menu/NameInput");
        }

        SetSceneObject(triggers[5], "target", "World/PlayerGUI/Menu/Panel");
        SetSceneObject(triggers[6], "target", "World/Signs/QuestGui");
        SetSceneObject(triggers[7], "target", "World/PlayerGUI/Menu/Grid");
        SetSceneObject(triggers[8], "target", "World/PlayerGUI/Menu/List");
        SetConstant(triggers[7], "columns", "3");

        var connections = triggers
            .Select((trigger, index) => Flow($"CONN_CustomUiWatcher_{index}", trigger.Id, GraphPortDefaults.FlowOut, message.Id, GraphPortDefaults.FlowIn))
            .ToList();

        var rule = new Rule
        {
            Id = "RULE_CustomUIWatchers",
            Name = "CustomUIWatchers",
            Nodes = [.. triggers, message],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "CustomUIWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("return triggerObject.Visible == true", luau);
        Assert.Contains("return currentValue == true, currentValue", luau);
        Assert.Contains("return currentValue == false, currentValue", luau);
        Assert.Contains("return tostring(triggerObject.Image or \"\")", luau);
        Assert.Contains("return tostring(triggerObject.Text or \"\")", luau);
        Assert.Contains("return triggerObject.ReadOnly == true", luau);
        Assert.Contains("return triggerObject.IgnoreMouse == true", luau);
        Assert.Contains("return triggerObject.Shaded == true", luau);
        Assert.Contains("local watchedLimit = tonumber(3) or 2", luau);
        Assert.Contains("return tonumber(triggerObject.Columns) or 0", luau);
        Assert.Contains("return currentValue >= watchedLimit, currentValue", luau);
        Assert.Contains("return tostring(triggerObject.HorizontalScrollMode or \"\")", luau);
        Assert.Contains("local previousMatched = readMatched() == true", luau);
        Assert.Contains("local previousValue = readWatchedValue()", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, uiVisible = currentValue }", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, uiImage = currentValue }", luau);
        Assert.DoesNotContain("On UI Became Visible is not implemented", luau);
        Assert.DoesNotContain("On Grid Columns Reached is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsPlayerUiRootAndCreateUiContainer()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_CreateUiStart");
        var create = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_CreateUIContainer"), stableId: "ACT_CreateUiContainer");
        var playerUiRoot = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_PlayerUIRoot"), stableId: "PROP_PlayerUiRoot");
        var print = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintPlayerUiRoot");

        SetConstant(create, "uiKind", "Horizontal Flow");
        SetConstant(create, "objectName", "QuickMenuFlow");

        var rule = new Rule
        {
            Id = "RULE_CreatePlayerUi",
            Name = "CreatePlayerUi",
            ScriptKind = GraphScriptKind.Local,
            Nodes = [start, create, playerUiRoot, print],
            Connections =
            [
                Flow("CONN_Start_CreateUi", start.Id, GraphPortDefaults.FlowOut, create.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_CreateUi_Print", create.Id, GraphPortDefaults.FlowOut, print.Id, GraphPortDefaults.FlowIn),
                Value("CONN_PlayerUi_CreateParent", playerUiRoot.Id, GraphPortDefaults.ValueOut, create.Id, GraphPortDefaults.ParameterPortId("target")),
                Value("CONN_PlayerUi_Print", playerUiRoot.Id, GraphPortDefaults.ValueOut, print.Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "CreatePlayerUiGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local requestedUiParent = PlayerGUI", luau);
        Assert.Contains("parentUiObject = PlayerGUI", luau);
        Assert.Contains("local createdUiObject = Instance.New(\"UIHFlow\", parentUiObject)", luau);
        Assert.Contains("createdUiObject.Name = createdUiObjectName", luau);
        Assert.Contains("print(tostring(PlayerGUI))", luau);
        Assert.DoesNotContain("Create UI Container is not implemented", luau);
        Assert.DoesNotContain("Player UI Root is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsCreateSceneContainer()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_CreateContainerStart");
        var create = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_CreateSceneContainer"), stableId: "ACT_CreateSceneContainer");

        SetSceneObject(create, "target", "World/Environment");
        SetConstant(create, "containerKind", "Model");
        SetConstant(create, "objectName", "EnemyGroup");

        var rule = new Rule
        {
            Id = "RULE_CreateSceneContainer",
            Name = "CreateSceneContainer",
            ScriptKind = GraphScriptKind.Server,
            Nodes = [start, create],
            Connections =
            [
                Flow("CONN_Start_CreateContainer", start.Id, GraphPortDefaults.FlowOut, create.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "CreateSceneContainerGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local requestedParent = \"World/Environment\"", luau);
        Assert.Contains("parentObject = resolveTarget(triggerObject, requestedParent)", luau);
        Assert.Contains("local createdContainerObject = Instance.New(\"Model\", parentObject)", luau);
        Assert.Contains("createdContainerObject.Name = createdContainerName", luau);
        Assert.DoesNotContain("Create Scene Container is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsTextInputTriggersActionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var changed = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnTextInputChanged"), stableId: "TRG_TextInputChanged");
        var submitted = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnTextInputSubmitted"), stableId: "TRG_TextInputSubmitted");
        var setText = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetTextInputText"), stableId: "ACT_SetInputText");
        var setPlaceholder = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetTextInputPlaceholder"), stableId: "ACT_SetInputPlaceholder");
        var setReadOnly = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetTextInputReadOnly"), stableId: "ACT_SetInputReadOnly");
        var focus = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_FocusTextInput"), stableId: "ACT_FocusInput");
        var valueIds = new[]
        {
            "PROP_TextInputText",
            "PROP_TextInputPlaceholder",
            "PROP_TextInputReadOnly"
        };
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_TextInput_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintTextInput_{index}"))
            .ToList();

        foreach (var node in new[] { changed, submitted, setText, setPlaceholder, setReadOnly, focus }.Concat(valueNodes))
        {
            SetSceneObject(node, "target", "World/PlayerGUI/Profile/NameInput");
        }

        SetConstant(setText, "text", "Player");
        SetConstant(setPlaceholder, "placeholder", "Name");
        SetConstant(setReadOnly, "readOnly", "true");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_TextInputChanged_SetText", changed.Id, GraphPortDefaults.FlowOut, setText.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_TextInputSubmitted_SetPlaceholder", submitted.Id, GraphPortDefaults.FlowOut, setPlaceholder.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_SetText_SetReadOnly", setText.Id, GraphPortDefaults.FlowOut, setReadOnly.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_SetReadOnly_Focus", setReadOnly.Id, GraphPortDefaults.FlowOut, focus.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Focus_Print0", focus.Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_TextInputValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_PrintTextInput_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_TextInput",
            Name = "TextInput",
            ScriptKind = GraphScriptKind.Local,
            Nodes = [changed, submitted, setText, setPlaceholder, setReadOnly, focus, .. valueNodes, .. printNodes],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "TextInputGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("triggerObject.Changed:Connect(function(value)", luau);
        Assert.Contains("triggerObject.Submitted:Connect(function(value)", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, uiTextInput = triggerObject, text = tostring(inputText or \"\") }", luau);
        Assert.Contains("textInputObject.Text = tostring(\"Player\")", luau);
        Assert.Contains("textInputObject.Placeholder = tostring(\"Name\")", luau);
        Assert.Contains("textInputObject.ReadOnly = true", luau);
        Assert.Contains("textInputObject:Focus()", luau);
        Assert.Contains("return tostring(targetObject.Text or \"\")", luau);
        Assert.Contains("return tostring(targetObject.Placeholder or \"\")", luau);
        Assert.Contains("return targetObject.ReadOnly == true", luau);
        Assert.DoesNotContain("On Text Input Changed is not implemented", luau);
        Assert.DoesNotContain("Set Text Input Text is not implemented", luau);
        Assert.DoesNotContain("Text Input Text is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsUiFieldAndScrollViewActionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartUiField");
        var setLayer = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetUIFieldZIndex"), stableId: "ACT_SetUiLayer");
        var setIgnoreMouse = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetUIFieldIgnoresMouse"), stableId: "ACT_SetUiIgnoreMouse");
        var setClip = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetUIFieldClipDescendants"), stableId: "ACT_SetUiClip");
        var setRotation = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetUIFieldRotation"), stableId: "ACT_SetUiRotation");
        var setScale = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetUIFieldScale"), stableId: "ACT_SetUiScale");
        var setScroll = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetScrollViewMode"), stableId: "ACT_SetScrollMode");
        var valueIds = new[]
        {
            "PROP_UIFieldZIndex",
            "PROP_UIFieldIgnoresMouse",
            "PROP_UIFieldClipDescendants",
            "PROP_UIFieldRotation",
            "PROP_UIFieldScale",
            "PROP_ScrollViewHorizontalMode",
            "PROP_ScrollViewVerticalMode"
        };
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_UiField_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintUiField_{index}"))
            .ToList();

        foreach (var node in new[] { setLayer, setIgnoreMouse, setClip, setRotation, setScale }.Concat(valueNodes.Take(5)))
        {
            SetSceneObject(node, "target", "World/PlayerGUI/Hud/Panel");
        }

        SetSceneObject(setScroll, "target", "World/PlayerGUI/Hud/ScrollPanel");
        SetSceneObject(valueNodes.Single(node => node.CatalogId == "PROP_ScrollViewHorizontalMode"), "target", "World/PlayerGUI/Hud/ScrollPanel");
        SetSceneObject(valueNodes.Single(node => node.CatalogId == "PROP_ScrollViewVerticalMode"), "target", "World/PlayerGUI/Hud/ScrollPanel");
        SetConstant(setLayer, "zIndex", "5");
        SetConstant(setIgnoreMouse, "ignoreMouse", "true");
        SetConstant(setClip, "clipDescendants", "true");
        SetConstant(setRotation, "rotation", "15");
        SetConstant(setScale, "scale", "1.25");
        SetConstant(setScroll, "axis", "Both");
        SetConstant(setScroll, "mode", "AlwaysShow");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_SetLayer", start.Id, GraphPortDefaults.FlowOut, setLayer.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_SetLayer_IgnoreMouse", setLayer.Id, GraphPortDefaults.FlowOut, setIgnoreMouse.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_IgnoreMouse_Clip", setIgnoreMouse.Id, GraphPortDefaults.FlowOut, setClip.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Clip_Rotation", setClip.Id, GraphPortDefaults.FlowOut, setRotation.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Rotation_Scale", setRotation.Id, GraphPortDefaults.FlowOut, setScale.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Scale_Scroll", setScale.Id, GraphPortDefaults.FlowOut, setScroll.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Scroll_Print0", setScroll.Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_UiFieldValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_PrintUiField_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_UiField",
            Name = "UiField",
            ScriptKind = GraphScriptKind.Local,
            Nodes = [start, setLayer, setIgnoreMouse, setClip, setRotation, setScale, setScroll, .. valueNodes, .. printNodes],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "UiFieldGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("uiFieldObject.ZIndex = 5", luau);
        Assert.Contains("uiFieldObject.IgnoreMouse = true", luau);
        Assert.Contains("uiFieldObject.ClipDescendants = true", luau);
        Assert.Contains("uiFieldObject.Rotation = 15", luau);
        Assert.Contains("uiFieldObject.Scale = 1.25", luau);
        Assert.Contains("local scrollAxis = tostring(\"Both\")", luau);
        Assert.Contains("local scrollMode = tostring(\"AlwaysShow\")", luau);
        Assert.Contains("scrollViewObject.HorizontalScrollMode = scrollMode", luau);
        Assert.Contains("scrollViewObject.VerticalScrollMode = scrollMode", luau);
        Assert.Contains("return tonumber(targetObject.ZIndex) or 0", luau);
        Assert.Contains("return targetObject.IgnoreMouse == true", luau);
        Assert.Contains("return targetObject.ClipDescendants == true", luau);
        Assert.Contains("return tonumber(targetObject.Rotation) or 0", luau);
        Assert.Contains("return tonumber(targetObject.Scale) or 1", luau);
        Assert.Contains("return tostring(targetObject.HorizontalScrollMode or \"\")", luau);
        Assert.Contains("return tostring(targetObject.VerticalScrollMode or \"\")", luau);
        Assert.DoesNotContain("Set UI Layer is not implemented", luau);
        Assert.DoesNotContain("Set Scroll View Mode is not implemented", luau);
        Assert.DoesNotContain("UI Layer is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsUiLayoutAndGui3DActionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartUiLayout");
        var setGridColumns = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetGridLayoutColumns"), stableId: "ACT_SetGridColumns");
        var setGridSpacing = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetGridLayoutSpacing"), stableId: "ACT_SetGridSpacing");
        var setLayoutSpacing = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetLayoutSpacing"), stableId: "ACT_SetLayoutSpacing");
        var setAlignment = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetLayoutChildAlignment"), stableId: "ACT_SetAlignment");
        var setShaded = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetGui3DShaded"), stableId: "ACT_SetGui3DShaded");
        var setFaceCamera = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetGui3DFaceCamera"), stableId: "ACT_SetGui3DFaceCamera");
        var setTransparent = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetGui3DTransparent"), stableId: "ACT_SetGui3DTransparent");
        var valueIds = new[]
        {
            "PROP_GridLayoutColumns",
            "PROP_GridLayoutSpacing",
            "PROP_LayoutSpacing",
            "PROP_LayoutChildAlignment",
            "PROP_Gui3DShaded",
            "PROP_Gui3DFaceCamera",
            "PROP_Gui3DTransparent"
        };
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_UiLayout_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintUiLayout_{index}"))
            .ToList();

        foreach (var node in new[] { setGridColumns, setGridSpacing }.Concat(valueNodes.Take(2)))
        {
            SetSceneObject(node, "target", "World/PlayerGUI/Hud/InventoryGrid");
        }

        foreach (var node in new[] { setLayoutSpacing, setAlignment }.Concat(valueNodes.Skip(2).Take(2)))
        {
            SetSceneObject(node, "target", "World/PlayerGUI/Hud/MenuLayout");
        }

        foreach (var node in new[] { setShaded, setFaceCamera, setTransparent }.Concat(valueNodes.Skip(4)))
        {
            SetSceneObject(node, "target", "World/HudSign");
        }

        SetConstant(setGridColumns, "columns", "4");
        SetConstant(setGridSpacing, "spacing", "12");
        SetConstant(setLayoutSpacing, "spacing", "6");
        SetConstant(setAlignment, "alignment", "End");
        SetConstant(setShaded, "shaded", "false");
        SetConstant(setFaceCamera, "faceCamera", "true");
        SetConstant(setTransparent, "transparent", "false");

        var actionNodes = new[] { setGridColumns, setGridSpacing, setLayoutSpacing, setAlignment, setShaded, setFaceCamera, setTransparent };
        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_GridColumns", start.Id, GraphPortDefaults.FlowOut, actionNodes[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index + 1 < actionNodes.Length; index++)
        {
            connections.Add(Flow($"CONN_UiLayoutAction_{index}_{index + 1}", actionNodes[index].Id, GraphPortDefaults.FlowOut, actionNodes[index + 1].Id, GraphPortDefaults.FlowIn));
        }

        connections.Add(Flow("CONN_UiLayoutActions_Print0", actionNodes[^1].Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn));
        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_UiLayoutValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_PrintUiLayout_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_UiLayout",
            Name = "UiLayout",
            ScriptKind = GraphScriptKind.Local,
            Nodes = [start, .. actionNodes, .. valueNodes, .. printNodes],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "UiLayoutGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("layoutObject.Columns = 4", luau);
        Assert.Contains("layoutObject.Spacing = 12", luau);
        Assert.Contains("layoutObject.Spacing = 6", luau);
        Assert.Contains("layoutObject.ChildAlignment = tostring(\"End\")", luau);
        Assert.Contains("gui3DObject.Shaded = false", luau);
        Assert.Contains("gui3DObject.FaceCamera = true", luau);
        Assert.Contains("gui3DObject.Transparent = false", luau);
        Assert.Contains("return tonumber(targetObject.Columns) or 0", luau);
        Assert.Contains("return tonumber(targetObject.Spacing) or 0", luau);
        Assert.Contains("return tostring(targetObject.ChildAlignment or \"\")", luau);
        Assert.Contains("return targetObject.Shaded == true", luau);
        Assert.Contains("return targetObject.FaceCamera == true", luau);
        Assert.Contains("return targetObject.Transparent == true", luau);
        Assert.DoesNotContain("Set Grid Columns is not implemented", luau);
        Assert.DoesNotContain("Set Layout Child Alignment is not implemented", luau);
        Assert.DoesNotContain("3D UI Shaded is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsToolEventTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var triggerIds = new[]
        {
            "EV_OnToolEquipped",
            "EV_OnToolUnequipped",
            "EV_OnToolActivated",
            "EV_OnToolDeactivated"
        };
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var nodes = new List<RuleNode>();
        var connections = new List<GraphConnection>();

        for (var index = 0; index < triggerIds.Length; index++)
        {
            var trigger = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == triggerIds[index]), stableId: $"TRG_Tool_{index}");
            var message = NodeCatalogService.CreateNode(showMessage, stableId: $"ACT_ToolMessage_{index}");
            SetSceneObject(trigger, "target", "World/Tools/Sword");
            SetConstant(message, "message", triggerIds[index]);
            nodes.Add(trigger);
            nodes.Add(message);
            connections.Add(Flow($"CONN_ToolEvent_{index}", trigger.Id, GraphPortDefaults.FlowOut, message.Id, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_ToolEvents",
            Name = "ToolEvents",
            Nodes = nodes,
            Connections = connections
        };
        var graph = new RuleGraph { Name = "ToolEventsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("triggerObject.Equipped:Connect(function()", luau);
        Assert.Contains("triggerObject.Unequipped:Connect(function()", luau);
        Assert.Contains("triggerObject.Activated:Connect(function()", luau);
        Assert.Contains("triggerObject.Deactivated:Connect(function()", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, tool = triggerObject, holder = triggerObject.Holder }", luau);
        Assert.DoesNotContain("local function onStart()", luau);
        Assert.DoesNotContain("On Tool Activated is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsToolActionsConditionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var activate = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ActivateTool"), stableId: "ACT_ActivateTool");
        var deactivate = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_DeactivateTool"), stableId: "ACT_DeactivateTool");
        var playAnimation = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PlayToolAnimation"), stableId: "ACT_PlayAnimation");
        var setDroppable = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetToolDroppable"), stableId: "ACT_SetDroppable");
        var isHeld = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_ToolIsHeld"), stableId: "COND_ToolHeld");
        var canDrop = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_ToolCanBeDropped"), stableId: "COND_ToolDrop");
        var holderValue = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ToolHolder"), stableId: "PROP_ToolHolder");
        var dropValue = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ToolCanBeDropped"), stableId: "PROP_ToolDropValue");
        var printHolder = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintHolder");
        var printDrop = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintDrop");

        foreach (var node in new[] { activate, deactivate, playAnimation, setDroppable, isHeld, canDrop, holderValue, dropValue })
        {
            SetSceneObject(node, "target", "World/Tools/Sword");
        }

        SetConstant(playAnimation, "animationName", "Swing");
        SetConstant(setDroppable, "enabled", "false");

        var rule = new Rule
        {
            Id = "RULE_Tools",
            Name = "Tools",
            Nodes = [start, activate, deactivate, playAnimation, setDroppable, isHeld, canDrop, holderValue, dropValue, printHolder, printDrop],
            Connections =
            [
                Flow("CONN_Start_Activate", start.Id, GraphPortDefaults.FlowOut, activate.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Activate_Deactivate", activate.Id, GraphPortDefaults.FlowOut, deactivate.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Deactivate_Play", deactivate.Id, GraphPortDefaults.FlowOut, playAnimation.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Play_Droppable", playAnimation.Id, GraphPortDefaults.FlowOut, setDroppable.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Droppable_Held", setDroppable.Id, GraphPortDefaults.FlowOut, isHeld.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Held_CanDrop", isHeld.Id, GraphPortDefaults.TrueOut, canDrop.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_CanDrop_PrintHolder", canDrop.Id, GraphPortDefaults.TrueOut, printHolder.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PrintHolder_PrintDrop", printHolder.Id, GraphPortDefaults.FlowOut, printDrop.Id, GraphPortDefaults.FlowIn),
                Value("CONN_Holder_Print", holderValue.Id, GraphPortDefaults.ValueOut, printHolder.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_Drop_Print", dropValue.Id, GraphPortDefaults.ValueOut, printDrop.Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "ToolsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("toolObject:Activate()", luau);
        Assert.Contains("toolObject:Deactivate()", luau);
        Assert.Contains("toolObject:PlayAnimation(tostring(\"Swing\"))", luau);
        Assert.Contains("toolObject.Droppable = false", luau);
        Assert.Contains("return toolObject.Holder ~= nil", luau);
        Assert.Contains("return toolObject.Droppable == true", luau);
        Assert.Contains("return toolObject.Holder", luau);
        Assert.Contains("local toolObject = resolveTarget(triggerObject, \"World/Tools/Sword\")", luau);
        Assert.DoesNotContain("Activate Tool is not implemented", luau);
        Assert.DoesNotContain("Tool Holder is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsInventoryActionsConditionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var joined = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnPlayerJoined"), stableId: "TRG_PlayerJoined");
        var giveTool = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_GiveToolToPlayer"), stableId: "ACT_GiveTool");
        var hasTool = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_PlayerHasTool"), stableId: "COND_HasTool");
        var inventoryValue = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_PlayerInventory"), stableId: "PROP_PlayerInventory");
        var findTool = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_FindToolInInventory"), stableId: "PROP_FindTool");
        var printInventory = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintInventory");
        var printTool = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintTool");

        SetSceneObject(giveTool, "target", "World/Tools/KeyCard");
        SetConstant(hasTool, "toolName", "KeyCard");
        SetConstant(findTool, "toolName", "KeyCard");

        var rule = new Rule
        {
            Id = "RULE_Inventory",
            Name = "Inventory",
            Nodes = [joined, giveTool, hasTool, inventoryValue, findTool, printInventory, printTool],
            Connections =
            [
                Flow("CONN_Joined_Give", joined.Id, GraphPortDefaults.FlowOut, giveTool.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Give_Check", giveTool.Id, GraphPortDefaults.FlowOut, hasTool.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Check_PrintInventory", hasTool.Id, GraphPortDefaults.TrueOut, printInventory.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PrintInventory_PrintTool", printInventory.Id, GraphPortDefaults.FlowOut, printTool.Id, GraphPortDefaults.FlowIn),
                Value("CONN_Inventory_Print", inventoryValue.Id, GraphPortDefaults.ValueOut, printInventory.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_FindTool_Print", findTool.Id, GraphPortDefaults.ValueOut, printTool.Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "InventoryGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local inventory = player.Inventory", luau);
        Assert.Contains("\"World/Tools/KeyCard\"", luau);
        Assert.Contains("local toolObject = resolveTarget(triggerObject,", luau);
        Assert.Contains("toolObject.Parent = inventory", luau);
        Assert.Contains("return inventory:FindChild(tostring(\"KeyCard\")) ~= nil", luau);
        Assert.Contains("return player.Inventory", luau);
        Assert.Contains("return inventory:FindChild(tostring(\"KeyCard\"))", luau);
        Assert.DoesNotContain("Give Tool To Player is not implemented", luau);
        Assert.DoesNotContain("Player Has Tool is not implemented", luau);
        Assert.DoesNotContain("Player Inventory is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsGameTeamTriggerActionAndCondition()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var teamChanged = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnPlayerGameTeamChanged"), stableId: "TRG_TeamChanged");
        var playerInTeam = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_PlayerIsInGameTeam"), stableId: "COND_PlayerInTeam");
        var setTeam = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetPlayerGameTeam"), stableId: "ACT_SetGameTeam");
        var message = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_Message");

        SetSceneObject(teamChanged, "target", "World/Rules/TeamWatcher");
        SetSceneObject(playerInTeam, "target", "Teams/Blue");
        SetSceneObject(setTeam, "target", "Teams/Blue");
        SetConstant(message, "message", "Player team changed");

        var rule = new Rule
        {
            Id = "RULE_GameTeamFlow",
            Name = "GameTeamFlow",
            Nodes = [teamChanged, playerInTeam, setTeam, message],
            Connections =
            [
                Flow("CONN_TeamChanged_Check", teamChanged.Id, GraphPortDefaults.FlowOut, playerInTeam.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Check_Set", playerInTeam.Id, GraphPortDefaults.TrueOut, setTeam.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Set_Message", setTeam.Id, GraphPortDefaults.FlowOut, message.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "GameTeamFlowGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function connectPlayerTeamChanged(player)", luau);
        Assert.Contains("Players:GetPlayers()", luau);
        Assert.Contains("player.TeamChanged:Connect(function()", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, player = player, team = player.Team }", luau);
        Assert.Contains("Players.PlayerAdded:Connect(connectPlayerTeamChanged)", luau);
        Assert.Contains("local targetTeam = resolveTarget(triggerObject,", luau);
        Assert.Contains("return player.Team == targetTeam", luau);
        Assert.Contains("player.Team = targetTeam", luau);
        Assert.DoesNotContain("local function onStart()", luau);
        Assert.DoesNotContain("Set Player Game Team is not implemented", luau);
        Assert.DoesNotContain("Player Is In Game Team is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsGameTeamProperties()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var teamChanged = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnPlayerGameTeamChanged"), stableId: "TRG_TeamChanged");
        var valueIds = new[]
        {
            "PROP_TriggeringPlayerGameTeam",
            "PROP_PlayerGameTeamName",
            "PROP_PlayerGameTeamColor",
            "PROP_GameTeamName",
            "PROP_GameTeamColor",
            "PROP_GameTeamPlayerCount",
            "PROP_GameTeamPlayers",
            "PROP_GameTeamCount",
            "PROP_AllGameTeams"
        };
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_Team_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintTeam_{index}"))
            .ToList();

        SetSceneObject(teamChanged, "target", "World/Rules/TeamWatcher");
        foreach (var node in valueNodes.Where(node => node.Type.StartsWith("GameTeam", StringComparison.Ordinal) && !node.Type.Equals("GameTeamCount", StringComparison.Ordinal)))
        {
            SetSceneObject(node, "target", "Teams/Blue");
        }

        var connections = new List<GraphConnection>
        {
            Flow("CONN_TeamChanged_Print0", teamChanged.Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_Value_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_Print_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_GameTeamProperties",
            Name = "GameTeamProperties",
            Nodes = [teamChanged, .. valueNodes, .. printNodes],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "GameTeamPropertiesGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("triggerContext.player.Team", luau);
        Assert.Contains("team:GetDisplayName()", luau);
        Assert.Contains("return team.Color", luau);
        Assert.Contains("local targetTeam = resolveTarget(triggerObject, \"Teams/Blue\")", luau);
        Assert.Contains("targetTeam:GetDisplayName()", luau);
        Assert.Contains("return targetTeam.Color", luau);
        Assert.Contains("targetTeam:GetPlayers()", luau);
        Assert.Contains("return players", luau);
        Assert.Contains("Teams:GetTeams()", luau);
        Assert.Contains("return #teams", luau);
        Assert.Contains("return teams", luau);
        Assert.DoesNotContain("Game Team Name is not implemented", luau);
        Assert.DoesNotContain("Game Team Player Count is not implemented", luau);
        Assert.DoesNotContain("Game Team Players is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsPlayerStatActionsConditionAndProperties()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var trigger = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnPlayerGameTeamChanged"), stableId: "TRG_StatsPlayerContext");
        var setNumber = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetPlayerStatNumber"), stableId: "ACT_SetStatNumber");
        var setText = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetPlayerStatText"), stableId: "ACT_SetStatText");
        var condition = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_PlayerStatAtLeast"), stableId: "COND_StatAtLeast");
        var valueIds = new[]
        {
            "PROP_PlayerStatValue",
            "PROP_PlayerStatDisplayValue",
            "PROP_StatDisplayName",
            "PROP_TeamStatTotal",
            "PROP_AllPlayerStats"
        };
        var values = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_Stats_{index}"))
            .ToList();
        var prints = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintStats_{index}"))
            .ToList();

        SetSceneObject(trigger, "target", "World/Rules/StatsWatcher");
        SetSceneObject(setNumber, "target", "Stats/Score");
        SetSceneObject(setText, "target", "Stats/Rank");
        SetSceneObject(condition, "target", "Stats/Score");
        SetConstant(setNumber, "value", "15");
        SetConstant(setText, "value", "Champion");
        SetConstant(condition, "minimum", "10");
        foreach (var node in values.Where(node => node.CatalogId != "PROP_AllPlayerStats"))
        {
            SetSceneObject(node, "target", "Stats/Score");
        }

        SetSceneObject(values.Single(node => node.CatalogId == "PROP_TeamStatTotal"), "team", "Teams/Blue");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_StatsTrigger_SetNumber", trigger.Id, GraphPortDefaults.FlowOut, setNumber.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_SetNumber_SetText", setNumber.Id, GraphPortDefaults.FlowOut, setText.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_SetText_Check", setText.Id, GraphPortDefaults.FlowOut, condition.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Check_Print0", condition.Id, GraphPortDefaults.TrueOut, prints[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < prints.Count; index++)
        {
            connections.Add(Value($"CONN_StatsValue_Print_{index}", values[index].Id, GraphPortDefaults.ValueOut, prints[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < prints.Count)
            {
                connections.Add(Flow($"CONN_PrintStats_{index}_{index + 1}", prints[index].Id, GraphPortDefaults.FlowOut, prints[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_PlayerStats",
            Name = "PlayerStats",
            ScriptKind = GraphScriptKind.Server,
            Nodes = [trigger, setNumber, setText, condition, .. values, .. prints],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "PlayerStatsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("statObject:Set(player, tonumber(15) or 0)", luau);
        Assert.Contains("statObject:Set(player, tostring(\"Champion\"))", luau);
        Assert.Contains("return (tonumber(statObject:Get(player)) or 0) >= (tonumber(10) or 0)", luau);
        Assert.Contains("return statObject:Get(player)", luau);
        Assert.Contains("statObject:GetDisplayValue(player)", luau);
        Assert.Contains("statObject:GetDisplayName()", luau);
        Assert.Contains("statObject:GetTotalForTeam(teamObject)", luau);
        Assert.Contains("Stats:GetStats()", luau);
        Assert.DoesNotContain("Set Player Stat Number is not implemented", luau);
        Assert.DoesNotContain("Player Stat At Least is not implemented", luau);
        Assert.DoesNotContain("Player Stat Value is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsSavedDataActionsAndProperties()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var trigger = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_SavedDataStart");
        var save = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SaveDatastoreValue"), stableId: "ACT_SaveSavedValue");
        var remove = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_RemoveDatastoreValue"), stableId: "ACT_RemoveSavedValue");
        var savedValue = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_DatastoreValue"), stableId: "PROP_SavedValue");
        var savedStoreName = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_DatastoreKey"), stableId: "PROP_SavedStoreName");
        var printValue = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintSavedValue");
        var printStoreName = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintSavedStoreName");

        foreach (var node in new[] { save, remove, savedValue, savedStoreName })
        {
            SetConstant(node, "storeKey", "PlayerData");
        }

        SetConstant(save, "entryKey", "Coins");
        SetConstant(save, "value", "10");
        SetConstant(remove, "entryKey", "Coins");
        SetConstant(savedValue, "entryKey", "Coins");
        SetConstant(savedValue, "fallbackValue", "0");

        var rule = new Rule
        {
            Id = "RULE_SavedData",
            Name = "SavedData",
            ScriptKind = GraphScriptKind.Server,
            Nodes = [trigger, save, remove, savedValue, savedStoreName, printValue, printStoreName],
            Connections =
            [
                Flow("CONN_Start_Save", trigger.Id, GraphPortDefaults.FlowOut, save.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Save_Remove", save.Id, GraphPortDefaults.FlowOut, remove.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Remove_PrintValue", remove.Id, GraphPortDefaults.FlowOut, printValue.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PrintValue_PrintStoreName", printValue.Id, GraphPortDefaults.FlowOut, printStoreName.Id, GraphPortDefaults.FlowIn),
                Value("CONN_SavedValue_Print", savedValue.Id, GraphPortDefaults.ValueOut, printValue.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_StoreName_Print", savedStoreName.Id, GraphPortDefaults.ValueOut, printStoreName.Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "SavedDataGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("Datastore:GetDatastore(savedStoreName)", luau);
        Assert.Contains("savedStore:SetAsync(tostring(savedEntryName), \"10\")", luau);
        Assert.Contains("savedStore:RemoveAsync(tostring(savedEntryName))", luau);
        Assert.Contains("Datastore:GetDatastore(tostring(\"PlayerData\"))", luau);
        Assert.Contains("savedStore:GetAsync(tostring(\"Coins\"))", luau);
        Assert.Contains("local savedStoreKey = tostring(savedStore.Key or \"\")", luau);
        Assert.Contains("savedStore:Disconnect()", luau);
        Assert.DoesNotContain("Save Saved Value is not implemented", luau);
        Assert.DoesNotContain("Remove Saved Value is not implemented", luau);
        Assert.DoesNotContain("Saved Value is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsTweenApiActions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var tweenPosition = catalog.Nodes.Single(node => node.IdBase == "ACT_TweenObjectPosition");
        var tweenColor = catalog.Nodes.Single(node => node.IdBase == "ACT_TweenObjectColor");
        var tweenTransparency = catalog.Nodes.Single(node => node.IdBase == "ACT_TweenObjectTransparency");
        var trigger = NodeCatalogService.CreateNode(start, 100, 100, "TRG_Start");
        var position = NodeCatalogService.CreateNode(tweenPosition, 320, 100, "ACT_TweenPosition");
        var color = NodeCatalogService.CreateNode(tweenColor, 540, 100, "ACT_TweenColor");
        var transparency = NodeCatalogService.CreateNode(tweenTransparency, 760, 100, "ACT_TweenTransparency");
        SetConstant(position, "duration", "2");
        SetConstant(position, "vector", "1,2,3");
        SetConstant(position, "speedScale", "1.5");
        SetConstant(position, "looped", "true");
        SetConstant(position, "parallel", "true");
        SetConstant(color, "r", "0.25");
        SetConstant(color, "g", "0.5");
        SetConstant(color, "b", "0.75");
        SetConstant(transparency, "transparency", "0.5");
        SetConstant(transparency, "duration", "3");

        var rule = new Rule
        {
            Id = "RULE_Tweens",
            Name = "Tweens",
            Nodes = [trigger, position, color, transparency],
            Connections =
            [
                Flow("CONN_Start_Position", trigger.Id, GraphPortDefaults.FlowOut, position.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Position_Color", position.Id, GraphPortDefaults.FlowOut, color.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Color_Transparency", color.Id, GraphPortDefaults.FlowOut, transparency.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "TweenGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local tween = Tween:NewTween()", luau);
        Assert.Contains("local endValue = makeVector3(1, 2, 3)", luau);
        Assert.Contains("tween:TweenPosition(targetObject, endValue, 2)", luau);
        Assert.Contains("tween.SpeedScale = math.max(0.001, 1.5)", luau);
        Assert.Contains("tween.Looped = true", luau);
        Assert.Contains("tween.Parallel = true", luau);
        Assert.Contains("local endColor = Color.New(0.25, 0.5, 0.75, 1)", luau);
        Assert.Contains("tween:TweenColor(targetObject.Color, endColor, 1, function(color)", luau);
        Assert.Contains("local endValue = tonumber(0.5) or 0", luau);
        Assert.Contains("tween:TweenNumber(tonumber(targetObject.Transparency) or 0, endValue, 3, function(value)", luau);
        Assert.Contains("targetObject.Transparency = value", luau);
        Assert.DoesNotContain("Tween Object Position is not implemented", luau);
        Assert.DoesNotContain("Tween Object Color is not implemented", luau);
        Assert.DoesNotContain("Animate Object Transparency is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsTweenParityConditions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var conditionIds = new[]
        {
            "COND_TweenPositionReached",
            "COND_TweenRotationReached",
            "COND_TweenScaleReached",
            "COND_TweenColorReached",
            "COND_TweenTransparencyReached"
        };
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), 100 + index * 40, 100, id))
            .ToList();

        foreach (var condition in conditions)
        {
            SetConstant(condition, "target", "World/Environment/TweenTarget");
            if (condition.Parameters.Any(parameter => parameter.Key == "tolerance"))
            {
                SetConstant(condition, "tolerance", "0.02");
            }
        }

        SetConstant(conditions[0], "vector", "1,2,3");
        SetConstant(conditions[1], "vector", "0,90,0");
        SetConstant(conditions[2], "vector", "2,2,2");
        SetConstant(conditions[3], "r", "0.25");
        SetConstant(conditions[3], "g", "0.5");
        SetConstant(conditions[3], "b", "0.75");
        SetConstant(conditions[4], "transparency", "0.5");

        var rule = new Rule
        {
            Id = "RULE_TweenConditions",
            Name = "Tween Conditions",
            Nodes = conditions
        };
        var graph = new RuleGraph { Name = "TweenConditionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function vrsTweenVectorReached", luau);
        Assert.Contains("local expectedTweenValue = makeVector3(1, 2, 3)", luau);
        Assert.Contains("return vrsTweenVectorReached(targetObject.Position, expectedTweenValue, tweenTolerance)", luau);
        Assert.Contains("local expectedTweenValue = makeVector3(0, 90, 0)", luau);
        Assert.Contains("return vrsTweenVectorReached(targetObject.Rotation, expectedTweenValue, tweenTolerance)", luau);
        Assert.Contains("local expectedTweenValue = makeVector3(2, 2, 2)", luau);
        Assert.Contains("return vrsTweenVectorReached(targetObject.Scale, expectedTweenValue, tweenTolerance)", luau);
        Assert.Contains("local expectedTweenValue = Color.New(0.25, 0.5, 0.75, 1)", luau);
        Assert.Contains("return targetObject.Color == expectedTweenValue", luau);
        Assert.Contains("local expectedTweenValue = tonumber(0.5) or 0", luau);
        Assert.Contains("return math.abs(currentValue - expectedTweenValue) <= tweenTolerance", luau);
        Assert.DoesNotContain("Tween Position Reached is not implemented", luau);
        Assert.DoesNotContain("Tween Transparency Reached is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsTweenParityTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var triggerIds = new[]
        {
            "EV_OnTweenPositionReached",
            "EV_OnTweenRotationReached",
            "EV_OnTweenScaleReached",
            "EV_OnTweenColorReached",
            "EV_OnTweenTransparencyReached"
        };
        var triggers = triggerIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), 100 + index * 40, 100, id))
            .ToList();

        foreach (var trigger in triggers)
        {
            SetConstant(trigger, "target", "World/Environment/TweenTarget");
            if (trigger.Parameters.Any(parameter => parameter.Key == "tolerance"))
            {
                SetConstant(trigger, "tolerance", "0.02");
            }
            SetConstant(trigger, "interval", "0.1");
        }

        SetConstant(triggers[0], "vector", "1,2,3");
        SetConstant(triggers[1], "vector", "0,90,0");
        SetConstant(triggers[2], "vector", "2,2,2");
        SetConstant(triggers[3], "r", "0.25");
        SetConstant(triggers[3], "g", "0.5");
        SetConstant(triggers[3], "b", "0.75");
        SetConstant(triggers[4], "transparency", "0.5");

        var rule = new Rule
        {
            Id = "RULE_TweenTriggers",
            Name = "Tween Triggers",
            Nodes = triggers
        };
        var graph = new RuleGraph { Name = "TweenTriggersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function vrsTweenVectorReached", luau);
        Assert.Contains("return vrsTweenVectorReached(currentValue, expectedTweenValue, tweenTolerance), currentValue", luau);
        Assert.Contains("local currentValue = triggerObject.Color", luau);
        Assert.Contains("return currentValue == expectedTweenValue, currentValue", luau);
        Assert.Contains("local currentValue = tonumber(triggerObject.Transparency) or 0", luau);
        Assert.Contains("return math.abs(currentValue - expectedTweenValue) <= tweenTolerance, currentValue", luau);
        Assert.Contains("tweenPosition = currentValue", luau);
        Assert.Contains("tweenRotation = currentValue", luau);
        Assert.Contains("tweenScale = currentValue", luau);
        Assert.Contains("tweenColor = currentValue", luau);
        Assert.Contains("tweenTransparency = currentValue", luau);
        Assert.DoesNotContain("On Tween Position Reached is not implemented", luau);
        Assert.DoesNotContain("On Tween Transparency Reached is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsObbyCheckpointTouchRuntimeState()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var checkpointTrigger = catalog.Nodes.Single(node => node.IdBase == "EV_OnCheckpointTouched");
        var setCheckpoint = catalog.Nodes.Single(node => node.IdBase == "ACT_SetPlayerCheckpoint");
        var trigger = NodeCatalogService.CreateNode(checkpointTrigger, 100, 100, "TRG_Checkpoint");
        var action = NodeCatalogService.CreateNode(setCheckpoint, 360, 100, "ACT_SetCheckpoint");
        SetConstant(action, "checkpointName", "Stage 1");

        var rule = new Rule
        {
            Id = "RULE_ObbyCheckpoint",
            Name = "ObbyCheckpoint",
            Nodes = [trigger, action],
            Connections =
            [
                Flow("CONN_Checkpoint_Set", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "ObbyCheckpointGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local VRS = { actions = {}, conditions = {}, vars = {}, states = {}, playerState = {} }", luau);
        Assert.Contains("local function vrsResolveTouchingPlayer(hit)", luau);
        Assert.Contains("local function vrsObjectPosition(object)", luau);
        Assert.Contains("listenObject.Touched:Connect(function(hit)", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, player = touchingPlayer, touchObject = hit, checkpointObject = listenObject }", luau);
        Assert.Contains("local data = vrsPlayerData(player)", luau);
        Assert.Contains("data.checkpointName = checkpointName", luau);
        Assert.Contains("data.checkpointPosition = checkpointPosition", luau);
        Assert.Contains("data.checkpointSet = true", luau);
        Assert.DoesNotContain("Set Player Checkpoint is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsObbyHazardSendToCheckpointAndDeaths()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var hazardTrigger = catalog.Nodes.Single(node => node.IdBase == "EV_OnHazardTouched");
        var sendCheckpoint = catalog.Nodes.Single(node => node.IdBase == "ACT_SendPlayerToCheckpoint");
        var trigger = NodeCatalogService.CreateNode(hazardTrigger, 100, 100, "TRG_Hazard");
        var action = NodeCatalogService.CreateNode(sendCheckpoint, 360, 100, "ACT_SendCheckpoint");

        var rule = new Rule
        {
            Id = "RULE_ObbyHazard",
            Name = "ObbyHazard",
            Nodes = [trigger, action],
            Connections =
            [
                Flow("CONN_Hazard_Send", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "ObbyHazardGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("hazardObject = listenObject", luau);
        Assert.Contains("data.deaths = (tonumber(data.deaths) or 0) + 1", luau);
        Assert.Contains("player:MovePosition(checkpointPosition)", luau);
        Assert.Contains("player.Position = checkpointPosition", luau);
        Assert.Contains("player:Respawn()", luau);
        Assert.DoesNotContain("Send Player To Checkpoint is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsObbyHazardKillPlayer()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var hazardTrigger = catalog.Nodes.Single(node => node.IdBase == "EV_OnHazardTouched");
        var killPlayer = catalog.Nodes.Single(node => node.IdBase == "ACT_KillPlayer");
        var trigger = NodeCatalogService.CreateNode(hazardTrigger, 100, 100, "TRG_Hazard");
        var action = NodeCatalogService.CreateNode(killPlayer, 360, 100, "ACT_KillPlayer");

        var rule = new Rule
        {
            Id = "RULE_ObbyKillHazard",
            Name = "ObbyKillHazard",
            Nodes = [trigger, action],
            Connections =
            [
                Flow("CONN_Hazard_Kill", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "ObbyKillHazardGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("hazardObject = listenObject", luau);
        Assert.Contains("local player = ((triggerContext ~= nil and triggerContext.player) or nil)", luau);
        Assert.Contains("local killedPlayer = false", luau);
        Assert.Contains("player:Kill()", luau);
        Assert.Contains("player.Health = 0", luau);
        Assert.Contains("humanoid.Health = 0", luau);
        Assert.Contains("player:Respawn()", luau);
        Assert.DoesNotContain("Kill Player is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsObbyTouchDebugLogsOnlyWhenEnabled()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var touchTrigger = catalog.Nodes.Single(node => node.IdBase == "EV_OnPlayerTouchedObject");
        var killPlayer = catalog.Nodes.Single(node => node.IdBase == "ACT_KillPlayer");
        var trigger = NodeCatalogService.CreateNode(touchTrigger, 100, 100, "TRG_Touch");
        trigger.DebugEnabled = true;
        var action = NodeCatalogService.CreateNode(killPlayer, 360, 100, "ACT_KillPlayer");
        var rule = new Rule
        {
            Id = "RULE_DebugTouch",
            Name = "DebugTouch",
            Nodes = [trigger, action],
            Connections =
            [
                Flow("CONN_Touch_Kill", trigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "DebugTouchGraph", Rules = [rule] };

        var debugLuau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);
        trigger.DebugEnabled = false;
        var quietLuau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("debug: touch target resolved", debugLuau);
        Assert.Contains("debug: touch received", debugLuau);
        Assert.Contains("debug: no player resolved from touch hit", debugLuau);
        Assert.Contains("debug: player resolved", debugLuau);
        Assert.Contains("debug: launching connected flow", debugLuau);
        Assert.DoesNotContain("debug: touch received", quietLuau);
    }

    [Fact]
    public void ExportRuleToLuau_BootstrapsMultipleTouchTriggersAndSharedAction()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var touchTrigger = catalog.Nodes.Single(node => node.IdBase == "EV_OnPlayerTouchedObject");
        var killPlayer = catalog.Nodes.Single(node => node.IdBase == "ACT_KillPlayer");
        var firstTrigger = NodeCatalogService.CreateNode(touchTrigger, 100, 80, "TRG_TouchA");
        var secondTrigger = NodeCatalogService.CreateNode(touchTrigger, 100, 220, "TRG_TouchB");
        var action = NodeCatalogService.CreateNode(killPlayer, 360, 150, "ACT_KillPlayer");

        var rule = new Rule
        {
            Id = "RULE_MultiTouch",
            Name = "MultiTouch",
            Nodes = [firstTrigger, secondTrigger, action],
            Connections =
            [
                Flow("CONN_TouchA_Kill", firstTrigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_TouchB_Kill", secondTrigger.Id, GraphPortDefaults.FlowOut, action.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "MultiTouchGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Equal(2, CountOccurrences(luau, "listenObject.Touched:Connect(function(hit)"));
        Assert.Equal(2, CountOccurrences(luau, "VRS.actions.killPlayer(triggerObject, triggerContext)"));
        Assert.Equal(2, CountOccurrences(luau, "local function onPlayerTouchedObject"));
        Assert.Contains("onPlayerTouchedObject()", luau);
        Assert.Contains("onPlayerTouchedObject_2()", luau);
        Assert.DoesNotContain("no connected action or condition", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsObbyFinishCoinsCollectiblesAndRuntimeValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var finishTrigger = catalog.Nodes.Single(node => node.IdBase == "EV_OnFinishTouched");
        var finishTimer = catalog.Nodes.Single(node => node.IdBase == "ACT_FinishPlayerTimer");
        var addCoin = catalog.Nodes.Single(node => node.IdBase == "ACT_AddPlayerCoin");
        var markCollectible = catalog.Nodes.Single(node => node.IdBase == "ACT_MarkPlayerCollectible");
        var runTime = catalog.Nodes.Single(node => node.IdBase == "PROP_PlayerRunTime");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(finishTrigger, 100, 100, "TRG_Finish");
        var finish = NodeCatalogService.CreateNode(finishTimer, 360, 100, "ACT_FinishTimer");
        var coin = NodeCatalogService.CreateNode(addCoin, 580, 100, "ACT_AddCoin");
        var collectible = NodeCatalogService.CreateNode(markCollectible, 800, 100, "ACT_MarkCollectible");
        var timeValue = NodeCatalogService.CreateNode(runTime, 1020, 60, "PROP_RunTime");
        var message = NodeCatalogService.CreateNode(showMessage, 1020, 140, "ACT_PrintTime");
        SetConstant(coin, "amount", "5");
        SetConstant(collectible, "collectibleId", "Coin01");

        var rule = new Rule
        {
            Id = "RULE_ObbyFinish",
            Name = "ObbyFinish",
            Nodes = [trigger, finish, coin, collectible, timeValue, message],
            Connections =
            [
                Flow("CONN_Finish_Timer", trigger.Id, GraphPortDefaults.FlowOut, finish.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Timer_Coin", finish.Id, GraphPortDefaults.FlowOut, coin.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Coin_Collectible", coin.Id, GraphPortDefaults.FlowOut, collectible.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Collectible_Print", collectible.Id, GraphPortDefaults.FlowOut, message.Id, GraphPortDefaults.FlowIn),
                Value("CONN_Time_Print", timeValue.Id, GraphPortDefaults.ValueOut, message.Id, GraphPortDefaults.ParameterPortId("message"))
            ]
        };
        var graph = new RuleGraph { Name = "ObbyFinishGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("data.runFinish = vrsNow()", luau);
        Assert.Contains("data.runFinished = true", luau);
        Assert.Contains("data.coins = (tonumber(data.coins) or 0) + 5", luau);
        Assert.Contains("data.collectibles[collectibleId] = true", luau);
        Assert.Contains("local collectibleId = tostring(\"Coin01\")", luau);
        Assert.Contains("data.runStart == nil and 0 or math.max(0, ((data.runFinish or vrsNow()) - data.runStart))", luau);
        Assert.DoesNotContain("local MESSAGE_TEXT", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsObbyRuntimeConditions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var numberAtLeast = catalog.Nodes.Single(node => node.IdBase == "COND_PlayerNumberAtLeast");
        var flagTrue = catalog.Nodes.Single(node => node.IdBase == "COND_PlayerFlagIsTrue");
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(start, 100, 100, "TRG_Start");
        var number = NodeCatalogService.CreateNode(numberAtLeast, 360, 100, "COND_Number");
        var flag = NodeCatalogService.CreateNode(flagTrue, 580, 100, "COND_Flag");
        var message = NodeCatalogService.CreateNode(showMessage, 800, 100, "ACT_Message");
        SetConstant(number, "key", "Coins");
        SetConstant(number, "minimum", "10");
        SetConstant(flag, "key", "Unlocked");

        var rule = new Rule
        {
            Id = "RULE_ObbyConditions",
            Name = "ObbyConditions",
            Nodes = [trigger, number, flag, message],
            Connections =
            [
                Flow("CONN_Start_Number", trigger.Id, GraphPortDefaults.FlowOut, number.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Number_Flag", number.Id, GraphPortDefaults.TrueOut, flag.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Flag_Message", flag.Id, GraphPortDefaults.TrueOut, message.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "ObbyConditionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("return (tonumber(data.numbers[\"Coins\"]) or 0) >= 10", luau);
        Assert.Contains("return data.flags[\"Unlocked\"] == true", luau);
        Assert.DoesNotContain("Player Number At Least is not implemented", luau);
        Assert.DoesNotContain("Player Flag Is True is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsObbyMovingPlatformLoopWithoutPlayerState()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var platformLoop = catalog.Nodes.Single(node => node.IdBase == "ACT_StartMovingPlatformLoop");
        var trigger = NodeCatalogService.CreateNode(start, 100, 100, "TRG_Start");
        var platform = NodeCatalogService.CreateNode(platformLoop, 360, 100, "ACT_PlatformLoop");
        SetConstant(platform, "x", "3");
        SetConstant(platform, "y", "0");
        SetConstant(platform, "z", "0");
        SetConstant(platform, "duration", "1.5");

        var rule = new Rule
        {
            Id = "RULE_ObbyPlatform",
            Name = "ObbyPlatform",
            Nodes = [trigger, platform],
            Connections =
            [
                Flow("CONN_Start_Platform", trigger.Id, GraphPortDefaults.FlowOut, platform.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "ObbyPlatformGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local VRS = { actions = {}, conditions = {}, vars = {}, states = {} }", luau);
        Assert.DoesNotContain("playerState", luau);
        Assert.Contains("local function vrsRunVectorTween", luau);
        Assert.Contains("local function vrsObjectPosition(object)", luau);
        Assert.Contains("local offset = makeVector3(3, 0, 0)", luau);
        Assert.Contains("while true do", luau);
        Assert.Contains("spawn(runPlatformLoop)", luau);
        Assert.DoesNotContain("Start Moving Platform Loop is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsCoreAndSceneActions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var actionIds = new[]
        {
            "ACT_ShowWarning",
            "ACT_SetState",
            "ACT_ToggleState",
            "ACT_ClearScriptVariable",
            "ACT_SetObjectVisible",
            "ACT_SetObjectName",
            "ACT_DestroyObject",
            "ACT_MoveObject",
            "ACT_RotateObject",
            "ACT_SetObjectScale"
        };
        var trigger = NodeCatalogService.CreateNode(start, 100, 100, "TRG_Start");
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), 320 + index * 20, 100 + index * 20, $"{id}_{index}"))
            .ToList();

        SetConstant(actions.Single(node => node.CatalogId == "ACT_ShowWarning"), "message", "Careful");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetState"), "state", "IsActive");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetState"), "enabled", "true");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_ToggleState"), "state", "DoorOpen");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_ClearScriptVariable"), "name", "Score");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetObjectVisible"), "visible", "false");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetObjectName"), "name", "Beacon");

        var rule = new Rule
        {
            Id = "RULE_CoreSceneActions",
            Name = "CoreSceneActions",
            Nodes = [trigger, .. actions]
        };
        var graph = new RuleGraph { Name = "CoreSceneActionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("print(\"[VRS warning] \" .. tostring(\"Careful\"))", luau);
        Assert.Contains("VRS.states[\"IsActive\"] = true", luau);
        Assert.Contains("VRS.states[\"DoorOpen\"] = not (VRS.states[\"DoorOpen\"] == true)", luau);
        Assert.Contains("VRS.vars[\"Score\"] = nil", luau);
        Assert.Contains("targetObject.Transparency = (false) and 0 or 1", luau);
        Assert.Contains("targetObject.Name = tostring(\"Beacon\")", luau);
        Assert.Contains("targetObject:Destroy()", luau);
        Assert.Contains("local function makeVector3(x, y, z)", luau);
        Assert.Contains("targetObject.Position = targetObject.Position + makeVector3(0, 1, 0)", luau);
        Assert.Contains("targetObject.Rotation = targetObject.Rotation + makeVector3(0, 45, 0)", luau);
        Assert.Contains("targetObject.Scale = makeVector3(1, 1, 1)", luau);
        Assert.DoesNotContain("Action Show Warning is not implemented", luau);
        Assert.DoesNotContain("Action Set Object Visible is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsColorSeriesHelpersAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_ColorSeriesStart");
        var series = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ColorSeriesFromColors"), stableId: "PROP_ColorSeries");
        var sample = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ColorFromColorSeries"), stableId: "PROP_ColorSeriesSample");
        var count = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ColorSeriesPointCount"), stableId: "PROP_ColorSeriesCount");
        var pointColor = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ColorSeriesPointColor"), stableId: "PROP_ColorSeriesPointColor");
        var pointOffset = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ColorSeriesPointOffset"), stableId: "PROP_ColorSeriesPointOffset");
        var printSample = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintColorSeriesSample");
        var printCount = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintColorSeriesCount");
        var printPointColor = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintColorSeriesPointColor");
        var printPointOffset = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintColorSeriesPointOffset");

        SetConstant(series, "min", "1,0,0");
        SetConstant(series, "max", "0,0,1");
        SetConstant(sample, "amount", "0.25");
        SetConstant(pointColor, "point", "1");
        SetConstant(pointOffset, "point", "2");

        var rule = new Rule
        {
            Id = "RULE_ColorSeries",
            Name = "ColorSeries",
            Nodes = [start, series, sample, count, pointColor, pointOffset, printSample, printCount, printPointColor, printPointOffset],
            Connections =
            [
                Flow("CONN_ColorSeries_Start_PrintSample", start.Id, GraphPortDefaults.FlowOut, printSample.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_ColorSeries_PrintSample_Count", printSample.Id, GraphPortDefaults.FlowOut, printCount.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_ColorSeries_PrintCount_PointColor", printCount.Id, GraphPortDefaults.FlowOut, printPointColor.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_ColorSeries_PrintPointColor_PointOffset", printPointColor.Id, GraphPortDefaults.FlowOut, printPointOffset.Id, GraphPortDefaults.FlowIn),
                Value("CONN_ColorSeries_Series_Sample", series.Id, GraphPortDefaults.ValueOut, sample.Id, GraphPortDefaults.ParameterPortId("series")),
                Value("CONN_ColorSeries_Series_Count", series.Id, GraphPortDefaults.ValueOut, count.Id, GraphPortDefaults.ParameterPortId("series")),
                Value("CONN_ColorSeries_Series_PointColor", series.Id, GraphPortDefaults.ValueOut, pointColor.Id, GraphPortDefaults.ParameterPortId("series")),
                Value("CONN_ColorSeries_Series_PointOffset", series.Id, GraphPortDefaults.ValueOut, pointOffset.Id, GraphPortDefaults.ParameterPortId("series")),
                Value("CONN_ColorSeries_Sample_Print", sample.Id, GraphPortDefaults.ValueOut, printSample.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_ColorSeries_Count_Print", count.Id, GraphPortDefaults.ValueOut, printCount.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_ColorSeries_PointColor_Print", pointColor.Id, GraphPortDefaults.ValueOut, printPointColor.Id, GraphPortDefaults.ParameterPortId("value")),
                Value("CONN_ColorSeries_PointOffset_Print", pointOffset.Id, GraphPortDefaults.ValueOut, printPointOffset.Id, GraphPortDefaults.ParameterPortId("value"))
            ]
        };
        var graph = new RuleGraph { Name = "ColorSeriesGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function vrsColorSeriesFromColors", luau);
        Assert.Contains("return ColorSeries.New(minColor, maxColor)", luau);
        Assert.Contains("return series:Lerp(amount)", luau);
        Assert.Contains("return tonumber(series.PointCount) or 0", luau);
        Assert.Contains("return series:GetColor(point)", luau);
        Assert.Contains("return series:GetOffset(point)", luau);
        Assert.Contains("vrsColorSeriesFromColors(Color.New(1, 0, 0, 1), Color.New(0, 0, 1, 1))", luau);
        Assert.Contains("vrsColorSeriesLerp(vrsColorSeriesFromColors", luau);
        Assert.Contains("vrsColorSeriesPointCount(vrsColorSeriesFromColors", luau);
        Assert.Contains("vrsColorSeriesColorAt(vrsColorSeriesFromColors", luau);
        Assert.Contains("vrsColorSeriesOffsetAt(vrsColorSeriesFromColors", luau);
        Assert.DoesNotContain("Color Series From Colors is not implemented", luau);
        Assert.DoesNotContain("Color From Color Series is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsFusedTransformVariants()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var moveEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_MoveObject");
        var rotateEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_RotateObject");
        var scaleEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_SetObjectScale");
        var moveSet = NodeCatalogService.CreateNode(moveEntry, 100, 100, "ACT_Move_Set");
        var moveSmooth = NodeCatalogService.CreateNode(moveEntry, 120, 120, "ACT_Move_Smooth");
        var rotateSet = NodeCatalogService.CreateNode(rotateEntry, 140, 140, "ACT_Rotate_Set");
        var rotateSpin = NodeCatalogService.CreateNode(rotateEntry, 160, 160, "ACT_Rotate_Spin");
        var scaleSmooth = NodeCatalogService.CreateNode(scaleEntry, 180, 180, "ACT_Scale_Smooth");

        SetConstant(moveSet, "positionMode", "Set");
        SetConstant(moveSet, "vector", "2,3,4");
        SetConstant(moveSmooth, "motionMode", "Smooth");
        SetConstant(moveSmooth, "duration", "2");
        SetConstant(rotateSet, "rotationMode", "Set");
        SetConstant(rotateSet, "vector", "10,20,30");
        SetConstant(rotateSpin, "rotationMode", "Spin");
        SetConstant(rotateSpin, "vector", "0,5,0");
        SetConstant(rotateSpin, "stepSeconds", "0.2");
        SetConstant(scaleSmooth, "motionMode", "Smooth");
        SetConstant(scaleSmooth, "duration", "3");

        var rule = new Rule
        {
            Id = "RULE_FusedTransforms",
            Name = "FusedTransforms",
            Nodes = [moveSet, moveSmooth, rotateSet, rotateSpin, scaleSmooth]
        };
        var graph = new RuleGraph { Name = "FusedTransformGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function vrsRunVectorTween", luau);
        Assert.Contains("targetObject.Position = makeVector3(2, 3, 4)", luau);
        Assert.Contains("local function applyPositionTween()", luau);
        Assert.Contains("vrsRunVectorTween(function() return targetObject.Position end", luau);
        Assert.Contains("targetObject.Rotation = makeVector3(10, 20, 30)", luau);
        Assert.Contains("wait(0.2)", luau);
        Assert.Contains("targetObject.Rotation = targetObject.Rotation + makeVector3(0, 5, 0)", luau);
        Assert.Contains("local function applyScaleTween()", luau);
        Assert.Contains("vrsRunVectorTween(function() return targetObject.Scale end", luau);
    }

    [Fact]
    public void ExportRuleToLuau_TransformVectorFieldUsesCatalogPropertyRecipe()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var move = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_MoveObject"), 100, 100, "ACT_Move_With_Position");
        var objectPositionRecipe = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_ObjectPosition"), 100, 220, "PROP_ObjectPosition_Recipe");

        SetConstant(move, "positionMode", "Set");
        SetCatalogRecipe(move, "vector", catalog.Nodes.Single(node => node.IdBase == "PROP_ObjectPosition"), objectPositionRecipe.Parameters);

        var rule = new Rule
        {
            Id = "RULE_VectorRecipe",
            Name = "VectorRecipe",
            Nodes = [move]
        };
        var graph = new RuleGraph { Name = "VectorRecipeGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("targetObject.Position = (function() local targetObject = resolveTarget(triggerObject, \"Self\")", luau);
        Assert.Contains("return targetObject.Position", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsLookAtActions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var lookAtPosition = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_LookAtPosition"), 100, 100, "ACT_LookAtPosition");
        var lookAtObject = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_LookAtObject"), 220, 100, "ACT_LookAtObject");

        SetConstant(lookAtPosition, "lookPosition", "3,0,6");
        SetConstant(lookAtObject, "lookTarget", "World/Environment/Marker");

        var rule = new Rule
        {
            Id = "RULE_LookAt",
            Name = "LookAt",
            Nodes = [lookAtPosition, lookAtObject]
        };
        var graph = new RuleGraph { Name = "LookAtGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local lookPosition = makeVector3(3, 0, 6)", luau);
        Assert.Contains("local lookTargetObject = resolveTarget(triggerObject, \"World/Environment/Marker\")", luau);
        Assert.Contains("targetObject.Rotation = makeVector3(0, math.deg(angleRadians), 0)", luau);
        Assert.DoesNotContain("Action Look At Position is not implemented", luau);
        Assert.DoesNotContain("Action Look At Object is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_StillExportsLegacyTransformNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var legacyIds = new[]
        {
            "ACT_SetObjectPosition",
            "ACT_AddObjectPosition",
            "ACT_MoveObjectOverTime",
            "ACT_RotateObjectContinuously"
        };
        var actions = legacyIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), 320 + index * 20, 100 + index * 20, $"{id}_{index}"))
            .ToList();
        var rule = new Rule
        {
            Id = "RULE_LegacyTransforms",
            Name = "LegacyTransforms",
            Nodes = actions
        };
        var graph = new RuleGraph { Name = "LegacyTransformGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("targetObject.Position = makeVector3(0, 0, 0)", luau);
        Assert.Contains("targetObject.Position = targetObject.Position + makeVector3(0, 1, 0)", luau);
        Assert.Contains("local endValue = targetObject.Position + makeVector3(10, 0, 0)", luau);
        Assert.Contains("local function vrsRunVectorTween", luau);
        Assert.Contains("targetObject.Rotation = targetObject.Rotation + makeVector3(0, 5, 0)", luau);
        Assert.Contains("while true do", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsAdditionalCoreAndSceneConditions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var conditionIds = new[]
        {
            "COND_StateIsTrue",
            "COND_StateIsFalse",
            "COND_TextStartsWith",
            "COND_TextEndsWith",
            "COND_TextIsEmpty",
            "COND_TextIsNotEmpty",
            "COND_TextHasAtLeastCharacters",
            "COND_TextHasAtMostCharacters",
            "COND_ScriptVariableExists",
            "COND_ScriptNumberIsAtLeast",
            "COND_ScriptNumberIsAtMost",
            "COND_ScriptNumberEquals",
            "COND_NumberIsAtLeast",
            "COND_NumberIsAtMost",
            "COND_NumberEquals",
            "COND_ObjectIsNamed",
            "COND_ObjectIsType",
            "COND_ObjectIsVisible",
            "COND_ObjectIsAboveHeight",
            "COND_ObjectIsBelowHeight"
        };
        var trigger = NodeCatalogService.CreateNode(start, 100, 100, "TRG_Start");
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), 320 + index * 20, 100 + index * 20, $"{id}_{index}"))
            .ToList();

        SetConstant(conditions.Single(node => node.CatalogId == "COND_StateIsTrue"), "state", "DoorOpen");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_StateIsFalse"), "state", "RoundDone");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_TextStartsWith"), "text", "Hello World");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_TextStartsWith"), "prefix", "Hello");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_TextEndsWith"), "text", "Hello World");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_TextEndsWith"), "suffix", "World");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_TextIsEmpty"), "text", "   ");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_TextIsEmpty"), "ignoreSpaces", "true");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_TextIsNotEmpty"), "text", "Ready");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_TextIsNotEmpty"), "ignoreSpaces", "true");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_TextHasAtLeastCharacters"), "text", "Ready");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_TextHasAtLeastCharacters"), "count", "3");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_TextHasAtMostCharacters"), "text", "Ready");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_TextHasAtMostCharacters"), "count", "10");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ScriptVariableExists"), "name", "Score");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ScriptNumberIsAtLeast"), "name", "Score");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ScriptNumberIsAtLeast"), "minimum", "10");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ScriptNumberIsAtMost"), "name", "Lives");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ScriptNumberIsAtMost"), "maximum", "3");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ScriptNumberEquals"), "name", "Round");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ScriptNumberEquals"), "expected", "2");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_NumberIsAtLeast"), "value", "8");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_NumberIsAtLeast"), "minimum", "5");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_NumberIsAtMost"), "value", "2");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_NumberIsAtMost"), "maximum", "4");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_NumberEquals"), "left", "7");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_NumberEquals"), "right", "7");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ObjectIsNamed"), "name", "Beacon");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ObjectIsType"), "typeName", "Part");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ObjectIsAboveHeight"), "height", "5");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ObjectIsBelowHeight"), "height", "-20");

        var rule = new Rule
        {
            Id = "RULE_CoreSceneConditions",
            Name = "CoreSceneConditions",
            Nodes = [trigger, .. conditions]
        };
        var graph = new RuleGraph { Name = "CoreSceneConditionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("return VRS.states[\"DoorOpen\"] == true", luau);
        Assert.Contains("return VRS.states[\"RoundDone\"] == false", luau);
        Assert.Contains("return string.sub(textValue, 1, string.len(prefixValue)) == prefixValue", luau);
        Assert.Contains("return string.sub(textValue, -string.len(suffixValue)) == suffixValue", luau);
        Assert.Contains("textValue = string.gsub(textValue, \"^%s*(.-)%s*$\", \"%1\")", luau);
        Assert.Contains("return textValue == \"\"", luau);
        Assert.Contains("return textValue ~= \"\"", luau);
        Assert.Contains("local characterLimit = math.max(0, math.floor(3))", luau);
        Assert.Contains("return string.len(textValue) >= characterLimit", luau);
        Assert.Contains("local characterLimit = math.max(0, math.floor(10))", luau);
        Assert.Contains("return string.len(textValue) <= characterLimit", luau);
        Assert.Contains("return VRS.vars[\"Score\"] ~= nil", luau);
        Assert.Contains("return (tonumber(VRS.vars[\"Score\"]) or 0) >= 10", luau);
        Assert.Contains("return (tonumber(VRS.vars[\"Lives\"]) or 0) <= 3", luau);
        Assert.Contains("return (tonumber(VRS.vars[\"Round\"]) or 0) == 2", luau);
        Assert.Contains("return 8 >= 5", luau);
        Assert.Contains("return 2 <= 4", luau);
        Assert.Contains("return 7 == 7", luau);
        Assert.Contains("return tostring(targetObject.Name) == tostring(\"Beacon\")", luau);
        Assert.Contains("return targetObject:IsA(tostring(\"Part\"))", luau);
        Assert.Contains("return targetObject.Visible == true", luau);
        Assert.Contains("local objectHeight = vrsValueAxis(targetObject.Position, \"Y\", \"y\", 0)", luau);
        Assert.Contains("return objectHeight > 5", luau);
        Assert.Contains("return objectHeight < -20", luau);
        Assert.DoesNotContain("Condition State Is True is not implemented", luau);
        Assert.DoesNotContain("Condition State Is False is not implemented", luau);
        Assert.DoesNotContain("Condition Object Is Visible is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsAdditionalCatalogValueRecipesInline()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var startEntry = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var setVariableEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_SetScriptVariable");
        var recipeIds = new[]
        {
            "PROP_SubtractNumbers",
            "PROP_DivideNumbers",
            "PROP_AverageNumber",
            "PROP_SquareRootNumber",
            "PROP_RoundNumber",
            "PROP_TextLength",
            "PROP_NumberToText",
            "PROP_ReadState",
            "PROP_RGBColor",
            "PROP_RandomColor",
            "PROP_ObjectName",
            "PROP_ObjectTypeName",
            "PROP_ObjectNetworkKey",
            "PROP_ObjectSaveKey",
            "PROP_ObjectIsNetworked",
            "PROP_ObjectVisibleValue",
            "PROP_ObjectCollisionValue",
            "PROP_ObjectAnchoredValue",
            "PROP_ObjectTransparency",
            "PROP_ObjectXPosition",
            "PROP_ObjectHeightPosition",
            "PROP_ObjectZPosition",
            "PROP_ObjectTurnAngle",
            "PROP_ObjectWidthSize",
            "PROP_ObjectHeightSize",
            "PROP_ObjectDepthSize"
        };
        var trigger = NodeCatalogService.CreateNode(startEntry, 100, 100, "TRG_Start");
        var actions = new List<RuleNode>();

        foreach (var id in recipeIds)
        {
            var entry = catalog.Nodes.Single(node => node.IdBase == id);
            var recipe = NodeCatalogService.CreateNode(entry);
            var action = NodeCatalogService.CreateNode(setVariableEntry, 320 + actions.Count * 20, 100 + actions.Count * 20, $"ACT_Set_{actions.Count}");
            SetConstant(action, "name", id);
            PrimeRecipeParameters(recipe);
            SetCatalogRecipe(action, "value", entry, recipe.Parameters);
            actions.Add(action);
        }

        var rule = new Rule
        {
            Id = "RULE_AdditionalRecipes",
            Name = "AdditionalRecipes",
            Nodes = [trigger, .. actions]
        };
        var graph = new RuleGraph { Name = "AdditionalRecipesGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("VRS.vars[\"PROP_SubtractNumbers\"] = (9 - 4)", luau);
        Assert.Contains("division by zero", luau);
        Assert.Contains("VRS.vars[\"PROP_AverageNumber\"] = ((2 + 8) / 2)", luau);
        Assert.Contains("VRS.vars[\"PROP_SquareRootNumber\"] = math.sqrt(math.max(0, 16))", luau);
        Assert.Contains("math.floor(value + 0.5)", luau);
        Assert.Contains("string.len(tostring(\"Hello\"))", luau);
        Assert.Contains("tostring(42)", luau);
        Assert.Contains("VRS.states[\"DoorOpen\"] == true", luau);
        Assert.Contains("Color.New(0.25, 0.5, 0.75, 1)", luau);
        Assert.Contains("Color.New(math.random(), math.random(), math.random(), 1)", luau);
        Assert.Contains("local function resolveTarget(triggerObject, targetName)", luau);
        Assert.Contains("return tostring(targetObject.Name)", luau);
        Assert.Contains("return tostring(targetObject.ClassName)", luau);
        Assert.Contains("return tostring(targetObject.NetworkedObjectID)", luau);
        Assert.Contains("return tostring(targetObject.ObjectID)", luau);
        Assert.Contains("return targetObject.ExistInNetwork == true", luau);
        Assert.Contains("return targetObject.Visible == true", luau);
        Assert.Contains("return targetObject.CanCollide == true", luau);
        Assert.Contains("return targetObject.Anchored == true", luau);
        Assert.Contains("return tonumber(targetObject.Transparency) or 0", luau);
        Assert.Contains("return vrsValueAxis(targetObject.Position, \"X\", \"x\", 0)", luau);
        Assert.Contains("return vrsValueAxis(targetObject.Position, \"Y\", \"y\", 0)", luau);
        Assert.Contains("return vrsValueAxis(targetObject.Position, \"Z\", \"z\", 0)", luau);
        Assert.Contains("return vrsValueAxis(targetObject.Rotation, \"Y\", \"y\", 0)", luau);
        Assert.Contains("return vrsValueAxis(targetObject.Scale, \"X\", \"x\", 1)", luau);
        Assert.Contains("return vrsValueAxis(targetObject.Scale, \"Y\", \"y\", 1)", luau);
        Assert.Contains("return vrsValueAxis(targetObject.Scale, \"Z\", \"z\", 1)", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsCoreExtraActions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var actionIds = new[]
        {
            "ACT_PrintValue",
            "ACT_DecrementScriptNumber",
            "ACT_MultiplyScriptNumber",
            "ACT_AppendScriptText",
            "ACT_SetObjectTransparency",
            "ACT_SetObjectAnchored",
            "ACT_SetObjectCanCollide",
            "ACT_SetObjectParent"
        };
        var trigger = NodeCatalogService.CreateNode(start, 100, 100, "TRG_Start");
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), 320 + index * 20, 100 + index * 20, $"{id}_{index}"))
            .ToList();

        SetConstant(actions.Single(node => node.CatalogId == "ACT_PrintValue"), "value", "Hello");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_DecrementScriptNumber"), "name", "Score");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_DecrementScriptNumber"), "amount", "2");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_MultiplyScriptNumber"), "name", "Score");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_MultiplyScriptNumber"), "factor", "3");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_AppendScriptText"), "name", "Log");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_AppendScriptText"), "text", "!");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetObjectTransparency"), "transparency", "0.5");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetObjectAnchored"), "anchored", "true");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetObjectCanCollide"), "canCollide", "false");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetObjectParent"), "newParent", "Target");

        var rule = new Rule
        {
            Id = "RULE_CoreExtraActions",
            Name = "CoreExtraActions",
            Nodes = [trigger, .. actions]
        };
        var graph = new RuleGraph { Name = "CoreExtraActionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("print(tostring(\"Hello\"))", luau);
        Assert.Contains("VRS.vars[\"Score\"] = (tonumber(VRS.vars[\"Score\"]) or 0) - 2", luau);
        Assert.Contains("VRS.vars[\"Score\"] = (tonumber(VRS.vars[\"Score\"]) or 0) * 3", luau);
        Assert.Contains("VRS.vars[\"Log\"] = tostring(VRS.vars[\"Log\"] or \"\") .. tostring(\"!\")", luau);
        Assert.Contains("targetObject.Transparency = 0.5", luau);
        Assert.Contains("targetObject.Anchored = true", luau);
        Assert.Contains("targetObject.CanCollide = false", luau);
        Assert.Contains("local newParentObject = resolveTarget(triggerObject, \"Target\")", luau);
        Assert.Contains("targetObject.Parent = newParentObject", luau);
        Assert.DoesNotContain("Action Print Value is not implemented", luau);
        Assert.DoesNotContain("Action Set Object Transparency is not implemented", luau);
        Assert.DoesNotContain("Action Set Object Parent is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsCoreExtraConditions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var conditionIds = new[]
        {
            "COND_TextEquals",
            "COND_NumberIsEven",
            "COND_NumberIsOdd",
            "COND_NumberIsPositive",
            "COND_NumberIsNegative",
            "COND_NumberOutsideRange",
            "COND_RandomChance",
            "COND_ScriptTextEquals",
            "COND_ScriptBooleanIsTrue",
            "COND_ObjectTransparencyAtLeast",
            "COND_ObjectHasParent",
            "COND_ObjectParentIs",
            "COND_ObjectIsUnderObject"
        };
        var trigger = NodeCatalogService.CreateNode(start, 100, 100, "TRG_Start");
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), 320 + index * 20, 100 + index * 20, $"{id}_{index}"))
            .ToList();

        SetConstant(conditions.Single(node => node.CatalogId == "COND_TextEquals"), "left", "Hello");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_TextEquals"), "right", "hello");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_TextEquals"), "caseSensitive", "false");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_NumberIsEven"), "value", "4");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_NumberIsOdd"), "value", "5");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_NumberIsPositive"), "value", "5");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_NumberIsNegative"), "value", "-2");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_NumberOutsideRange"), "value", "12");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_NumberOutsideRange"), "min", "0");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_NumberOutsideRange"), "max", "10");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_RandomChance"), "percent", "25");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ScriptTextEquals"), "name", "Mode");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ScriptTextEquals"), "expected", "Active");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ScriptBooleanIsTrue"), "name", "Enabled");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ObjectTransparencyAtLeast"), "minimum", "0.5");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ObjectParentIs"), "parent", "Target");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ObjectIsUnderObject"), "ancestor", "Target");

        var rule = new Rule
        {
            Id = "RULE_CoreExtraConditions",
            Name = "CoreExtraConditions",
            Nodes = [trigger, .. conditions]
        };
        var graph = new RuleGraph { Name = "CoreExtraConditionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local leftValue = tostring(\"Hello\")", luau);
        Assert.Contains("return leftValue == rightValue", luau);
        Assert.Contains("return math.floor(value) == value and value % 2 == 0", luau);
        Assert.Contains("return math.floor(value) == value and math.abs(value % 2) == 1", luau);
        Assert.Contains("return 5 >= 0", luau);
        Assert.Contains("return -2 < 0", luau);
        Assert.Contains("return 12 < 0 or 12 > 10", luau);
        Assert.Contains("local chancePercent = math.max(0, math.min(100, 25))", luau);
        Assert.Contains("return math.random() * 100 < chancePercent", luau);
        Assert.Contains("local currentValue = tostring(VRS.vars[\"Mode\"] or \"\")", luau);
        Assert.Contains("return VRS.vars[\"Enabled\"] == true", luau);
        Assert.Contains("return targetObject.Transparency >= 0.5", luau);
        Assert.Contains("return targetObject ~= nil and targetObject.Parent ~= nil", luau);
        Assert.Contains("local expectedParent = resolveTarget(triggerObject, \"Target\")", luau);
        Assert.Contains("return targetObject.Parent == expectedParent", luau);
        Assert.Contains("local expectedAncestor = resolveTarget(triggerObject, \"Target\")", luau);
        Assert.Contains("while current ~= nil do", luau);
        Assert.Contains("current = current.Parent", luau);
        Assert.DoesNotContain("Condition Text Equals is not implemented", luau);
        Assert.DoesNotContain("Condition Object Has Parent is not implemented", luau);
        Assert.DoesNotContain("Condition Object Parent Is is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsCoreExtraCatalogValueRecipesInline()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var startEntry = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var setVariableEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_SetScriptVariable");
        var recipeIds = new[]
        {
            "PROP_MinNumber",
            "PROP_MaxNumber",
            "PROP_AverageNumber",
            "PROP_SquareRootNumber",
            "PROP_AbsoluteNumber",
            "PROP_FloorNumber",
            "PROP_CeilNumber",
            "PROP_PowerNumbers",
            "PROP_ModuloNumbers",
            "PROP_LerpNumber",
            "PROP_LowercaseText",
            "PROP_UppercaseText",
            "PROP_TrimText",
            "PROP_ReplaceText",
            "PROP_AndBoolean",
            "PROP_OrBoolean",
            "PROP_NotBoolean"
        };
        var trigger = NodeCatalogService.CreateNode(startEntry, 100, 100, "TRG_Start");
        var actions = new List<RuleNode>();

        foreach (var id in recipeIds)
        {
            var entry = catalog.Nodes.Single(node => node.IdBase == id);
            var recipe = NodeCatalogService.CreateNode(entry);
            var action = NodeCatalogService.CreateNode(setVariableEntry, 320 + actions.Count * 20, 100 + actions.Count * 20, $"ACT_SetExtra_{actions.Count}");
            SetConstant(action, "name", id);
            PrimeRecipeParameters(recipe);
            SetCatalogRecipe(action, "value", entry, recipe.Parameters);
            actions.Add(action);
        }

        var rule = new Rule
        {
            Id = "RULE_CoreExtraRecipes",
            Name = "CoreExtraRecipes",
            Nodes = [trigger, .. actions]
        };
        var graph = new RuleGraph { Name = "CoreExtraRecipesGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("VRS.vars[\"PROP_MinNumber\"] = math.min(0, 1)", luau);
        Assert.Contains("VRS.vars[\"PROP_MaxNumber\"] = math.max(0, 1)", luau);
        Assert.Contains("VRS.vars[\"PROP_AbsoluteNumber\"] = math.abs(-1)", luau);
        Assert.Contains("VRS.vars[\"PROP_FloorNumber\"] = math.floor(1.5)", luau);
        Assert.Contains("VRS.vars[\"PROP_CeilNumber\"] = math.ceil(1.5)", luau);
        Assert.Contains("VRS.vars[\"PROP_PowerNumbers\"] = (2 ^ 3)", luau);
        Assert.Contains("Modulo Numbers stopped: divisor was zero.", luau);
        Assert.Contains("VRS.vars[\"PROP_LerpNumber\"] = (0 + ((10 - 0) * 0.5))", luau);
        Assert.Contains("VRS.vars[\"PROP_LowercaseText\"] = string.lower(tostring(\"Hello\"))", luau);
        Assert.Contains("VRS.vars[\"PROP_UppercaseText\"] = string.upper(tostring(\"Hello\"))", luau);
        Assert.Contains("string.match(tostring(\" Hello \"), \"^%s*(.-)%s*$\")", luau);
        Assert.Contains("local pattern = string.gsub(searchValue", luau);
        Assert.Contains("VRS.vars[\"PROP_AndBoolean\"] = ((true) and (true))", luau);
        Assert.Contains("VRS.vars[\"PROP_OrBoolean\"] = ((true) or (false))", luau);
        Assert.Contains("VRS.vars[\"PROP_NotBoolean\"] = (not (true))", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsCoreGeneralConditions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var booleanEntry = catalog.Nodes.Single(node => node.IdBase == "COND_BooleanCheck");
        var textEntry = catalog.Nodes.Single(node => node.IdBase == "COND_TextContains");
        var emptyEntry = catalog.Nodes.Single(node => node.IdBase == "COND_ValueIsEmpty");
        var trigger = NodeCatalogService.CreateNode(start, 100, 100, "TRG_Start");
        var boolean = NodeCatalogService.CreateNode(booleanEntry, 320, 60, "COND_Boolean");
        var text = NodeCatalogService.CreateNode(textEntry, 320, 160, "COND_Text");
        var empty = NodeCatalogService.CreateNode(emptyEntry, 320, 260, "COND_Empty");

        SetConstant(boolean, "value", "true");
        SetConstant(boolean, "expected", "false");
        SetConstant(text, "text", "Hello World");
        SetConstant(text, "search", "world");
        SetConstant(text, "caseSensitive", "false");
        SetConstant(empty, "value", "");

        var rule = new Rule
        {
            Id = "RULE_CoreGeneralConditions",
            Name = "CoreGeneralConditions",
            Nodes = [trigger, boolean, text, empty]
        };
        var graph = new RuleGraph { Name = "CoreGeneralConditionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("return (true) == (false)", luau);
        Assert.Contains("return string.find(string.lower(tostring(\"Hello World\")), string.lower(tostring(\"world\")), 1, true) ~= nil", luau);
        Assert.Contains("local value = \"\"", luau);
        Assert.Contains("return value == nil or tostring(value) == \"\"", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsPolytoriaEssentialsRuntimeActionsAndConditions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var actionIds = new[]
        {
            "ACT_StartCooldown",
            "ACT_ResetCooldown",
            "ACT_OpenGate",
            "ACT_ToggleGate",
            "ACT_StartRound",
            "ACT_SetRoundTime",
            "ACT_EndRound",
            "ACT_AddPlayerScore",
            "ACT_SetPlayerLives",
            "ACT_SetPlayerTeam",
            "ACT_AddTeamScore"
        };
        var conditionIds = new[]
        {
            "COND_CooldownReady",
            "COND_GateIsOpen",
            "COND_RoundIsRunning",
            "COND_RoundTimeExpired",
            "COND_PlayerScoreAtLeast",
            "COND_PlayerScoreEquals",
            "COND_PlayerLivesAtLeast",
            "COND_PlayerLivesAtMost",
            "COND_PlayerLivesEquals",
            "COND_PlayerTeamIs",
            "COND_TeamScoreAtLeast"
        };
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), 100 + index * 20, 100 + index * 20, $"{id}_{index}"))
            .ToList();
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), 400 + index * 20, 100 + index * 20, $"{id}_{index}"))
            .ToList();

        SetConstant(actions.Single(node => node.CatalogId == "ACT_StartCooldown"), "cooldownName", "Dash");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_StartCooldown"), "duration", "5");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_ResetCooldown"), "cooldownName", "Dash");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_OpenGate"), "gateName", "Door");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_ToggleGate"), "gateName", "Door");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_StartRound"), "roundName", "Arena");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_StartRound"), "duration", "30");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetRoundTime"), "roundName", "Arena");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetRoundTime"), "duration", "15");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_EndRound"), "roundName", "Arena");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_AddPlayerScore"), "amount", "2");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetPlayerLives"), "lives", "3");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetPlayerTeam"), "teamName", "Blue");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_AddTeamScore"), "teamName", "Blue");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_AddTeamScore"), "amount", "4");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_CooldownReady"), "cooldownName", "Dash");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_GateIsOpen"), "gateName", "Door");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_RoundIsRunning"), "roundName", "Arena");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_RoundTimeExpired"), "roundName", "Arena");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_PlayerScoreAtLeast"), "minimum", "10");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_PlayerScoreEquals"), "score", "12");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_PlayerLivesAtLeast"), "minimum", "1");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_PlayerLivesAtMost"), "maximum", "2");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_PlayerLivesEquals"), "lives", "3");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_PlayerTeamIs"), "teamName", "Blue");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_TeamScoreAtLeast"), "teamName", "Blue");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_TeamScoreAtLeast"), "minimum", "20");

        var rule = new Rule
        {
            Id = "RULE_EssentialsRuntime",
            Name = "EssentialsRuntime",
            Nodes = [.. actions, .. conditions]
        };
        var graph = new RuleGraph { Name = "EssentialsRuntimeGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function vrsNow()", luau);
        Assert.Contains("local function vrsPlayerKey(player)", luau);
        Assert.Contains("local cooldownKey = (\"cooldown:\" .. tostring(\"Dash\"))", luau);
        Assert.Contains("VRS.vars[cooldownKey .. \":readyAt\"] = vrsNow() + math.max(0, 5)", luau);
        Assert.Contains("VRS.vars[cooldownKey .. \":readyAt\"] = nil", luau);
        Assert.Contains("VRS.states[gateKey] = true", luau);
        Assert.Contains("VRS.states[gateKey] = not (VRS.states[gateKey] == true)", luau);
        Assert.Contains("VRS.states[roundKey .. \":running\"] = true", luau);
        Assert.Contains("VRS.vars[roundKey .. \":endAt\"] = vrsNow() + VRS.vars[roundKey .. \":duration\"]", luau);
        Assert.Contains("VRS.states[roundKey .. \":running\"] = false", luau);
        Assert.Contains("local player = ((triggerContext ~= nil and triggerContext.player) or nil)", luau);
        Assert.Contains("VRS.vars[runtimeKey] = (tonumber(VRS.vars[runtimeKey]) or 0) + 2", luau);
        Assert.Contains("VRS.vars[\"player:\" .. vrsPlayerKey(player) .. \":team\"] = tostring(\"Blue\")", luau);
        Assert.Contains("VRS.vars[runtimeKey] = (tonumber(VRS.vars[runtimeKey]) or 0) + 4", luau);
        Assert.Contains("return vrsNow() >= readyAt", luau);
        Assert.Contains("return VRS.states[gateKey] == true", luau);
        Assert.Contains("return VRS.states[roundKey .. \":running\"] == true", luau);
        Assert.Contains("return VRS.states[roundKey .. \":running\"] == true and endAt ~= nil and vrsNow() >= endAt", luau);
        Assert.Contains("return (tonumber(VRS.vars[\"player:\" .. vrsPlayerKey(player) .. \":score\"]) or 0) >= 10", luau);
        Assert.Contains("return (tonumber(VRS.vars[\"player:\" .. vrsPlayerKey(player) .. \":score\"]) or 0) == 12", luau);
        Assert.Contains("return (tonumber(VRS.vars[\"player:\" .. vrsPlayerKey(player) .. \":lives\"]) or 0) <= 2", luau);
        Assert.Contains("return (tonumber(VRS.vars[\"player:\" .. vrsPlayerKey(player) .. \":lives\"]) or 0) == 3", luau);
        Assert.Contains("return tostring(VRS.vars[\"player:\" .. vrsPlayerKey(player) .. \":team\"] or \"\") == tostring(\"Blue\")", luau);
        Assert.Contains("return (tonumber(VRS.vars[teamKey .. \":score\"]) or 0) >= 20", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsPlayerCountAtMostCondition()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var condition = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_PlayerCountAtMost"), 100, 100, "COND_PlayerCountAtMost");
        SetConstant(condition, "maximum", "8");
        var rule = new Rule
        {
            Id = "RULE_PlayerCountAtMost",
            Name = "PlayerCountAtMost",
            Nodes = [condition]
        };
        var graph = new RuleGraph { Name = "PlayerCountAtMostGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("return ((Players ~= nil and Players.PlayersCount) or 0) <= 8", luau);
        Assert.DoesNotContain("Condition Player Count At Most is not implemented yet", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsPolytoriaEssentialsCatalogValueRecipesInline()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var setVariableEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_SetScriptVariable");
        var recipeIds = new[]
        {
            "PROP_CooldownRemainingSeconds",
            "PROP_RoundTimeRemaining",
            "PROP_PlayerScore",
            "PROP_PlayerLives",
            "PROP_PlayerTeam",
            "PROP_TeamScore",
            "PROP_DistanceBetweenObjects",
            "PROP_DistanceBetweenPositions",
            "PROP_PercentNumber",
            "PROP_MapNumberRange",
            "PROP_TimeNowSeconds"
        };
        var actions = new List<RuleNode>();

        foreach (var id in recipeIds)
        {
            var entry = catalog.Nodes.Single(node => node.IdBase == id);
            var recipe = NodeCatalogService.CreateNode(entry);
            PrimeRecipeParameters(recipe);
            var action = NodeCatalogService.CreateNode(setVariableEntry, 320 + actions.Count * 20, 100 + actions.Count * 20, $"ACT_SetEssentials_{actions.Count}");
            SetConstant(action, "name", id);
            SetCatalogRecipe(action, "value", entry, recipe.Parameters);
            actions.Add(action);
        }

        var rule = new Rule
        {
            Id = "RULE_EssentialsRecipes",
            Name = "EssentialsRecipes",
            Nodes = actions
        };
        var graph = new RuleGraph { Name = "EssentialsRecipesGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function vrsNow()", luau);
        Assert.Contains("local function vrsValueAxis(value, upperName, lowerName, fallback)", luau);
        Assert.Contains("local function resolveTarget(triggerObject, targetName)", luau);
        Assert.Contains("VRS.vars[\"PROP_CooldownRemainingSeconds\"] = (function() local readyAt", luau);
        Assert.Contains("VRS.vars[\"PROP_RoundTimeRemaining\"] = (function() local endAt", luau);
        Assert.Contains("VRS.vars[\"PROP_PlayerScore\"] = (function() local player", luau);
        Assert.Contains("VRS.vars[\"PROP_PlayerLives\"] = (function() local player", luau);
        Assert.Contains("VRS.vars[\"PROP_PlayerTeam\"] = (function() local player", luau);
        Assert.Contains("VRS.vars[\"PROP_TeamScore\"] = (tonumber(VRS.vars[(\"team:\" .. tostring(\"Blue\")) .. \":score\"]) or 0)", luau);
        Assert.Contains("return vrsDistanceBetweenPositions(firstObject.Position, secondObject.Position)", luau);
        Assert.Contains("vrsDistanceBetweenPositions(makeVector3(0, 0, 0), makeVector3(3, 4, 0))", luau);
        Assert.Contains("return (25 / wholeValue) * 100", luau);
        Assert.Contains("local inputSpan = 10 - 0", luau);
        Assert.Contains("VRS.vars[\"PROP_TimeNowSeconds\"] = vrsNow()", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsGc2InspiredBeginnerValueRecipes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var setVariableEntry = catalog.Nodes.Single(node => node.IdBase == "ACT_SetScriptVariable");
        var recipeIds = new[]
        {
            "PROP_ChooseNumber",
            "PROP_ChooseText",
            "PROP_ChooseObject",
            "PROP_RandomTrueOrFalse",
            "PROP_RandomWholeNumber",
            "PROP_RandomNumberChoice",
            "PROP_RandomTextChoice",
            "PROP_NumberFromText",
            "PROP_TextBefore",
            "PROP_TextAfter",
            "PROP_TextBetween",
            "PROP_FirstTextCharacters",
            "PROP_LastTextCharacters"
        };
        var actions = new List<RuleNode>();

        foreach (var id in recipeIds)
        {
            var entry = catalog.Nodes.Single(node => node.IdBase == id);
            var recipe = NodeCatalogService.CreateNode(entry);
            PrimeRecipeParameters(recipe);
            var action = NodeCatalogService.CreateNode(setVariableEntry, stableId: $"ACT_Set_{id}");
            SetConstant(action, "name", id);
            SetCatalogRecipe(action, "value", entry, recipe.Parameters);
            actions.Add(action);
        }

        var rule = new Rule
        {
            Id = "RULE_BeginnerRecipes",
            Name = "BeginnerRecipes",
            Nodes = actions
        };
        var graph = new RuleGraph { Name = "BeginnerRecipesGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("VRS.vars[\"PROP_ChooseNumber\"] = 7", luau);
        Assert.Contains("VRS.vars[\"PROP_ChooseText\"] = \"Hello player\"", luau);
        Assert.Contains("VRS.vars[\"PROP_ChooseObject\"] = \"World/Door\"", luau);
        Assert.Contains("VRS.vars[\"PROP_RandomTrueOrFalse\"] = (math.random() * 100 < math.max(0, math.min(100, 25)))", luau);
        Assert.Contains("VRS.vars[\"PROP_RandomWholeNumber\"] = (function() local first = math.floor(1); local second = math.floor(6); if first > second then first, second = second, first end; return math.random(first, second) end)()", luau);
        Assert.Contains("VRS.vars[\"PROP_RandomNumberChoice\"] = (function() local chancePercent = math.max(0, math.min(100, 40)); if math.random() * 100 < chancePercent then return 10 end; return 20 end)()", luau);
        Assert.Contains("VRS.vars[\"PROP_RandomTextChoice\"] = (function() local chancePercent = math.max(0, math.min(100, 60)); if math.random() * 100 < chancePercent then return tostring(\"Red\") end; return tostring(\"Blue\") end)()", luau);
        Assert.Contains("VRS.vars[\"PROP_NumberFromText\"] = (function() local parsed = tonumber(tostring(\"42\")); if parsed == nil then return -1 end; return parsed end)()", luau);
        Assert.Contains("VRS.vars[\"PROP_TextBefore\"] = (function() local textValue = tostring(\"hello:world\"); local markerValue = tostring(\":\");", luau);
        Assert.Contains("return string.sub(textValue, 1, startIndex - 1)", luau);
        Assert.Contains("VRS.vars[\"PROP_TextAfter\"] = (function() local textValue = tostring(\"hello:world\"); local markerValue = tostring(\":\");", luau);
        Assert.Contains("return string.sub(textValue, endIndex + 1)", luau);
        Assert.Contains("VRS.vars[\"PROP_TextBetween\"] = (function() local textValue = tostring(\"[red]\"); local startMarker = tostring(\"[\"); local endMarker = tostring(\"]\");", luau);
        Assert.Contains("return string.sub(textValue, searchFrom, endStart - 1)", luau);
        Assert.Contains("VRS.vars[\"PROP_FirstTextCharacters\"] = (function() local textValue = tostring(\"Hello\"); local keepCount = math.max(0, math.floor(3));", luau);
        Assert.Contains("return string.sub(textValue, 1, keepCount)", luau);
        Assert.Contains("VRS.vars[\"PROP_LastTextCharacters\"] = (function() local textValue = tostring(\"Hello\"); local keepCount = math.max(0, math.floor(2));", luau);
        Assert.Contains("return string.sub(textValue, -keepCount)", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsGc2InspiredBeginnerActionsAndConditions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var nodeIds = new[]
        {
            "ACT_ShowObject",
            "ACT_HideObject",
            "ACT_TurnObjectCollisionOn",
            "ACT_TurnObjectCollisionOff",
            "ACT_MoveObjectToAnotherObject",
            "ACT_MoveObjectUp",
            "ACT_MoveObjectDown",
            "ACT_SetObjectXPosition",
            "ACT_SetObjectHeightPosition",
            "ACT_SetObjectZPosition",
            "ACT_SetObjectTurnAngle",
            "ACT_TurnObjectByAngle",
            "ACT_SetObjectWidthSize",
            "ACT_SetObjectHeightSize",
            "ACT_SetObjectDepthSize",
            "ACT_ResetPlayerScore",
            "ACT_ResetPlayerLives",
            "ACT_ResetTeamScore",
            "COND_ObjectIsVisible",
            "COND_ObjectIsHidden",
            "COND_ObjectCollisionIsOn",
            "COND_ObjectCollisionIsOff",
            "COND_TextIsANumber",
            "COND_NumberIsZero",
            "COND_ObjectIsCloseToObject",
            "COND_ObjectIsFarFromObject",
            "COND_ObjectTurnAngleAtLeast",
            "COND_ObjectTurnAngleAtMost",
            "COND_ObjectSizeAtLeast",
            "COND_ObjectSizeAtMost",
            "COND_PlayerHasNoLivesLeft",
            "COND_PlayerScoreAtMost",
            "COND_TeamScoreAtMost",
            "COND_RoundHasTimeLeft"
        };
        var nodes = nodeIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(entry => entry.IdBase == id), stableId: $"{id}_{index}"))
            .ToList();

        SetConstant(nodes.Single(node => node.CatalogId == "ACT_MoveObjectToAnotherObject"), "destination", "World/TargetPad");
        SetConstant(nodes.Single(node => node.CatalogId == "ACT_MoveObjectUp"), "distance", "4");
        SetConstant(nodes.Single(node => node.CatalogId == "ACT_MoveObjectDown"), "distance", "2");
        SetConstant(nodes.Single(node => node.CatalogId == "ACT_SetObjectXPosition"), "x", "12");
        SetConstant(nodes.Single(node => node.CatalogId == "ACT_SetObjectHeightPosition"), "height", "8");
        SetConstant(nodes.Single(node => node.CatalogId == "ACT_SetObjectZPosition"), "z", "-4");
        SetConstant(nodes.Single(node => node.CatalogId == "ACT_SetObjectTurnAngle"), "angle", "90");
        SetConstant(nodes.Single(node => node.CatalogId == "ACT_TurnObjectByAngle"), "angle", "45");
        SetConstant(nodes.Single(node => node.CatalogId == "ACT_SetObjectWidthSize"), "size", "3");
        SetConstant(nodes.Single(node => node.CatalogId == "ACT_SetObjectHeightSize"), "size", "4");
        SetConstant(nodes.Single(node => node.CatalogId == "ACT_SetObjectDepthSize"), "size", "5");
        SetConstant(nodes.Single(node => node.CatalogId == "ACT_ResetPlayerLives"), "lives", "5");
        SetConstant(nodes.Single(node => node.CatalogId == "ACT_ResetTeamScore"), "teamName", "Blue");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_TextIsANumber"), "text", "42");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_NumberIsZero"), "value", "0");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_ObjectIsCloseToObject"), "first", "Self");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_ObjectIsCloseToObject"), "second", "World/Button");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_ObjectIsCloseToObject"), "maxDistance", "12");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_ObjectIsFarFromObject"), "first", "Self");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_ObjectIsFarFromObject"), "second", "World/Finish");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_ObjectIsFarFromObject"), "minDistance", "30");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_ObjectTurnAngleAtLeast"), "angle", "90");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_ObjectTurnAngleAtMost"), "angle", "180");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_ObjectSizeAtLeast"), "direction", "Height");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_ObjectSizeAtLeast"), "size", "4");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_ObjectSizeAtMost"), "direction", "Depth");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_ObjectSizeAtMost"), "size", "5");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_PlayerScoreAtMost"), "maximum", "9");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_TeamScoreAtMost"), "teamName", "Blue");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_TeamScoreAtMost"), "maximum", "15");
        SetConstant(nodes.Single(node => node.CatalogId == "COND_RoundHasTimeLeft"), "roundName", "Arena");

        var rule = new Rule
        {
            Id = "RULE_BeginnerActionsConditions",
            Name = "BeginnerActionsConditions",
            Nodes = nodes
        };
        var graph = new RuleGraph { Name = "BeginnerActionsConditionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function resolveTarget(triggerObject, targetName)", luau);
        Assert.Contains("local function vrsDistanceBetweenPositions(first, second)", luau);
        Assert.Contains("targetObject.Visible = true", luau);
        Assert.Contains("targetObject.Visible = false", luau);
        Assert.Contains("targetObject.CanCollide = true", luau);
        Assert.Contains("targetObject.CanCollide = false", luau);
        Assert.Contains("local destinationObject = resolveTarget(triggerObject, \"World/TargetPad\")", luau);
        Assert.Contains("targetObject.Position = destinationObject.Position", luau);
        Assert.Contains("local moveDistance = math.abs(4)", luau);
        Assert.Contains("targetObject.Position = targetObject.Position + makeVector3(0, moveDistance, 0)", luau);
        Assert.Contains("local moveDistance = math.abs(2)", luau);
        Assert.Contains("targetObject.Position = targetObject.Position + makeVector3(0, -moveDistance, 0)", luau);
        Assert.Contains("newX = 12", luau);
        Assert.Contains("newY = 8", luau);
        Assert.Contains("newZ = -4", luau);
        Assert.Contains("targetObject.Position = makeVector3(newX, newY, newZ)", luau);
        Assert.Contains("local currentRotation = targetObject.Rotation", luau);
        Assert.Contains("newY = 90", luau);
        Assert.Contains("newY = newY + 45", luau);
        Assert.Contains("targetObject.Rotation = makeVector3(newX, newY, newZ)", luau);
        Assert.Contains("local currentScale = targetObject.Scale", luau);
        Assert.Contains("newX = 3", luau);
        Assert.Contains("newY = 4", luau);
        Assert.Contains("newZ = 5", luau);
        Assert.Contains("targetObject.Scale = makeVector3(newX, newY, newZ)", luau);
        Assert.Contains("VRS.vars[runtimeKey] = 0", luau);
        Assert.Contains("VRS.vars[runtimeKey] = 5", luau);
        Assert.Contains("local teamKey = (\"team:\" .. tostring(\"Blue\"))", luau);
        Assert.Contains("return targetObject.Visible == true", luau);
        Assert.Contains("return targetObject.Visible == false", luau);
        Assert.Contains("return targetObject.CanCollide == true", luau);
        Assert.Contains("return targetObject.CanCollide == false", luau);
        Assert.Contains("return tonumber(tostring(\"42\")) ~= nil", luau);
        Assert.Contains("return 0 == 0", luau);
        Assert.Contains("return distance <= 12", luau);
        Assert.Contains("return distance >= 30", luau);
        Assert.Contains("local turnAngle = vrsValueAxis(targetObject.Rotation, \"Y\", \"y\", 0)", luau);
        Assert.Contains("return turnAngle >= 90", luau);
        Assert.Contains("return turnAngle <= 180", luau);
        Assert.Contains("local sizeDirection = tostring(\"Height\")", luau);
        Assert.Contains("local sizeDirection = tostring(\"Depth\")", luau);
        Assert.Contains("local currentSize = vrsValueAxis(targetObject.Scale, sizeAxisUpper, sizeAxisLower, 1)", luau);
        Assert.Contains("return currentSize >= 4", luau);
        Assert.Contains("return currentSize <= 5", luau);
        Assert.Contains("return (tonumber(VRS.vars[\"player:\" .. vrsPlayerKey(player) .. \":lives\"]) or 0) <= 0", luau);
        Assert.Contains("return (tonumber(VRS.vars[\"player:\" .. vrsPlayerKey(player) .. \":score\"]) or 0) <= 9", luau);
        Assert.Contains("return (tonumber(VRS.vars[teamKey .. \":score\"]) or 0) <= 15", luau);
        Assert.Contains("return VRS.states[roundKey .. \":running\"] == true and endAt ~= nil and vrsNow() < endAt", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsSingleAxisPositionActions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var actionIds = new[]
        {
            "ACT_SetObjectXPosition",
            "ACT_SetObjectHeightPosition",
            "ACT_SetObjectZPosition"
        };
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"{id}_{index}"))
            .ToList();

        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetObjectXPosition"), "x", "12");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetObjectHeightPosition"), "height", "8");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetObjectZPosition"), "z", "-4");

        var rule = new Rule
        {
            Id = "RULE_SingleAxisPositionActions",
            Name = "SingleAxisPositionActions",
            Nodes = [start, .. actions],
            Connections =
            [
                Flow("CONN_Start_X", start.Id, GraphPortDefaults.FlowOut, actions[0].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_X_Y", actions[0].Id, GraphPortDefaults.FlowOut, actions[1].Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Y_Z", actions[1].Id, GraphPortDefaults.FlowOut, actions[2].Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "SingleAxisPositionActionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function vrsValueAxis(value, upperName, lowerName, fallback)", luau);
        Assert.Contains("local function makeVector3(x, y, z)", luau);
        Assert.Contains("local currentPosition = targetObject.Position", luau);
        Assert.Contains("newX = 12", luau);
        Assert.Contains("newY = 8", luau);
        Assert.Contains("newZ = -4", luau);
        Assert.Contains("targetObject.Position = makeVector3(newX, newY, newZ)", luau);
        Assert.DoesNotContain("Action Set Object X Position is not implemented", luau);
        Assert.DoesNotContain("Action Set Object Height Position is not implemented", luau);
        Assert.DoesNotContain("Action Set Object Z Position is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsTurnAngleActionsAndConditions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var setTurn = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_SetObjectTurnAngle"), stableId: "ACT_SetTurn");
        var addTurn = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_TurnObjectByAngle"), stableId: "ACT_AddTurn");
        var atLeast = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_ObjectTurnAngleAtLeast"), stableId: "COND_TurnAtLeast");
        var atMost = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_ObjectTurnAngleAtMost"), stableId: "COND_TurnAtMost");

        SetConstant(setTurn, "angle", "90");
        SetConstant(addTurn, "angle", "-45");
        SetConstant(atLeast, "angle", "45");
        SetConstant(atMost, "angle", "180");

        var rule = new Rule
        {
            Id = "RULE_TurnAngle",
            Name = "TurnAngle",
            Nodes = [start, setTurn, addTurn, atLeast, atMost],
            Connections =
            [
                Flow("CONN_Start_Set", start.Id, GraphPortDefaults.FlowOut, setTurn.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Set_Add", setTurn.Id, GraphPortDefaults.FlowOut, addTurn.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Add_AtLeast", addTurn.Id, GraphPortDefaults.FlowOut, atLeast.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_AtLeast_AtMost", atLeast.Id, GraphPortDefaults.TrueOut, atMost.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "TurnAngleGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function vrsValueAxis(value, upperName, lowerName, fallback)", luau);
        Assert.Contains("local function makeVector3(x, y, z)", luau);
        Assert.Contains("local currentRotation = targetObject.Rotation", luau);
        Assert.Contains("newY = 90", luau);
        Assert.Contains("newY = newY + -45", luau);
        Assert.Contains("targetObject.Rotation = makeVector3(newX, newY, newZ)", luau);
        Assert.Contains("local turnAngle = vrsValueAxis(targetObject.Rotation, \"Y\", \"y\", 0)", luau);
        Assert.Contains("return turnAngle >= 45", luau);
        Assert.Contains("return turnAngle <= 180", luau);
        Assert.DoesNotContain("Action Set Object Turn Angle is not implemented", luau);
        Assert.DoesNotContain("Action Turn Object By Angle is not implemented", luau);
        Assert.DoesNotContain("Condition Object Turn Angle At Least is not implemented", luau);
        Assert.DoesNotContain("Condition Object Turn Angle At Most is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsSingleAxisSizeActionsAndConditions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var actionIds = new[]
        {
            "ACT_SetObjectWidthSize",
            "ACT_SetObjectHeightSize",
            "ACT_SetObjectDepthSize"
        };
        var conditionIds = new[]
        {
            "COND_ObjectSizeAtLeast",
            "COND_ObjectSizeAtMost"
        };
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"{id}_{index}"))
            .ToList();
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"{id}_{index}"))
            .ToList();

        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetObjectWidthSize"), "size", "3");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetObjectHeightSize"), "size", "4");
        SetConstant(actions.Single(node => node.CatalogId == "ACT_SetObjectDepthSize"), "size", "5");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ObjectSizeAtLeast"), "direction", "Height");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ObjectSizeAtLeast"), "size", "4");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ObjectSizeAtMost"), "direction", "Depth");
        SetConstant(conditions.Single(node => node.CatalogId == "COND_ObjectSizeAtMost"), "size", "5");

        var rule = new Rule
        {
            Id = "RULE_SingleAxisSize",
            Name = "SingleAxisSize",
            Nodes = [.. actions, .. conditions]
        };
        var graph = new RuleGraph { Name = "SingleAxisSizeGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local function vrsValueAxis(value, upperName, lowerName, fallback)", luau);
        Assert.Contains("local function makeVector3(x, y, z)", luau);
        Assert.Contains("local currentScale = targetObject.Scale", luau);
        Assert.Contains("newX = 3", luau);
        Assert.Contains("newY = 4", luau);
        Assert.Contains("newZ = 5", luau);
        Assert.Contains("targetObject.Scale = makeVector3(newX, newY, newZ)", luau);
        Assert.Contains("local sizeDirection = tostring(\"Height\")", luau);
        Assert.Contains("local sizeDirection = tostring(\"Depth\")", luau);
        Assert.Contains("local currentSize = vrsValueAxis(targetObject.Scale, sizeAxisUpper, sizeAxisLower, 1)", luau);
        Assert.Contains("return currentSize >= 4", luau);
        Assert.Contains("return currentSize <= 5", luau);
        Assert.DoesNotContain("Action Set Object Width Size is not implemented", luau);
        Assert.DoesNotContain("Action Set Object Height Size is not implemented", luau);
        Assert.DoesNotContain("Action Set Object Depth Size is not implemented", luau);
        Assert.DoesNotContain("Condition Object Size At Least is not implemented", luau);
        Assert.DoesNotContain("Condition Object Size At Most is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_PolytoriaEssentialsDemoSampleExportsRepresentativeRules()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreatePolytoriaEssentialsDemoGraph(catalog.Nodes);

        var exported = graph.Rules
            .Select(rule => new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes))
            .ToList();

        Assert.Contains(exported, luau => luau.Contains("VRS.states[roundKey .. \":running\"] = true", StringComparison.Ordinal));
        Assert.Contains(exported, luau => luau.Contains("VRS.vars[cooldownKey .. \":readyAt\"] = vrsNow() + math.max(0, 5)", StringComparison.Ordinal));
        Assert.Contains(exported, luau => luau.Contains("VRS.vars[runtimeKey] = (tonumber(VRS.vars[runtimeKey]) or 0) + 10", StringComparison.Ordinal));
    }

    [Fact]
    public void ExportRuleToLuau_UsesTaggedGeneratedAndUserComments()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();
        rule.Nodes.Single(node => node.CatalogId == "ACT_ShowMessage").UserComment = "Author context.";

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);
        var commentLines = luau
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("-- ", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(commentLines);
        Assert.Contains("-- [VSR] USER CONFIGURATION", commentLines);
        Assert.Contains("-- [VSR] SCRIPT CONTEXT", commentLines);
        Assert.Contains("-- [VSR] TRIGGER: ON TIMER TICK", commentLines);
        Assert.Contains("-- [VSR] ACTION: SHOW MESSAGE", commentLines);
        Assert.Contains("-- [VSR] TRIGGER BOOTSTRAP", commentLines);
        Assert.Contains("-- [User] Author context.", commentLines);
        Assert.DoesNotContain(commentLines, line => line.Contains("Configured as:", StringComparison.Ordinal));
        Assert.DoesNotContain(commentLines, line => line.Contains("This action", StringComparison.Ordinal));
        Assert.DoesNotContain(commentLines, line => line.Contains("Change this value", StringComparison.Ordinal));
        Assert.All(commentLines, line =>
            Assert.True(
                line.StartsWith("-- [VSR]", StringComparison.Ordinal) ||
                line.StartsWith("-- [User]", StringComparison.Ordinal),
                $"Comment is missing an owner tag: {line}"));
    }

    [Fact]
    public void TryExtractGraphMetadata_RoundTripsExportedGraph()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var graph = SampleGraphFactory.CreateTimerMessageGraph(catalog.Nodes);
        var rule = graph.Rules.Single();

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);
        var extracted = LuauExporter.TryExtractGraphMetadata(luau, out var restored);

        Assert.True(extracted);
        Assert.NotNull(restored);
        Assert.NotEmpty(graph.SceneObjects);
        Assert.Equal(graph.Name, restored!.Name);
        Assert.Equal(rule.Nodes.Count, restored.Rules.Single().Nodes.Count);
        Assert.Equal(rule.Connections.Count, restored.Rules.Single().Connections.Count);
        Assert.Empty(restored.SceneObjects);
    }

    private static RuleGraph CreateNumberCompareBranchGraph(IReadOnlyCollection<NodeCatalogEntry> catalog, string operatorValue)
    {
        var timer = catalog.Single(node => node.IdBase == "EV_OnTimerTick");
        var conditionEntry = catalog.Single(node => node.IdBase == "COND_NumberCompare");
        var message = catalog.Single(node => node.IdBase == "ACT_ShowMessage");
        var trigger = NodeCatalogService.CreateNode(timer, 100, 100, "TRG_Timer");
        var condition = NodeCatalogService.CreateNode(conditionEntry, 360, 100, "COND_NumberCompare_1");
        var trueAction = NodeCatalogService.CreateNode(message, 620, 60, "ACT_TrueMessage");
        var falseAction = NodeCatalogService.CreateNode(message, 620, 180, "ACT_FalseMessage");

        condition.Parameters.Single(parameter => parameter.Key == "left").Value = "10";
        condition.Parameters.Single(parameter => parameter.Key == "operator").Value = operatorValue;
        condition.Parameters.Single(parameter => parameter.Key == "right").Value = "0";
        trueAction.Parameters.Single(parameter => parameter.Key == "message").Value = "True branch";
        falseAction.Parameters.Single(parameter => parameter.Key == "message").Value = "False branch";

        var rule = new Rule
        {
            Id = "RULE_NumberCompareBranches",
            Name = "NumberCompareBranches",
            Nodes = [trigger, condition, trueAction, falseAction],
            Connections =
            [
                Flow("CONN_Timer_Condition", trigger.Id, GraphPortDefaults.FlowOut, condition.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Condition_True", condition.Id, GraphPortDefaults.TrueOut, trueAction.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Condition_False", condition.Id, GraphPortDefaults.FalseOut, falseAction.Id, GraphPortDefaults.FlowIn)
            ]
        };

        return new RuleGraph
        {
            Name = "NumberCompareBranchGraph",
            Rules = [rule]
        };
    }

    [Fact]
    public void ExportRuleToLuau_EmitsBodyPositionActionsConditionsValuesAndTrigger()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartBodyPosition");
        var reached = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnBodyPositionReachedTarget"), stableId: "TRG_BodyPositionReached");
        var actionIds = new[]
        {
            "ACT_SetBodyPositionTarget",
            "ACT_SetBodyPositionForce",
            "ACT_SetBodyPositionAcceptanceDistance"
        };
        var conditionIds = new[]
        {
            "COND_BodyPositionReachedTarget",
            "COND_BodyPositionForceAtLeast"
        };
        var valueIds = new[]
        {
            "PROP_BodyPositionTarget",
            "PROP_BodyPositionForce",
            "PROP_BodyPositionAcceptanceDistance",
            "PROP_BodyPositionDistanceToTarget"
        };
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"ACT_BodyPosition_{index}"))
            .ToList();
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"COND_BodyPosition_{index}"))
            .ToList();
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_BodyPosition_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintBodyPosition_{index}"))
            .ToList();
        var reachedPrint = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintBodyPositionReached");

        foreach (var node in new[] { reached }.Concat(actions).Concat(conditions).Concat(valueNodes))
        {
            SetSceneObject(node, "target", "World/Environment/BodyMover");
        }

        SetConstant(actions[0], "x", "1");
        SetConstant(actions[0], "y", "2");
        SetConstant(actions[0], "z", "3");
        SetConstant(actions[1], "amount", "250");
        SetConstant(actions[2], "distance", "2.5");
        SetConstant(conditions[1], "amount", "200");
        SetConstant(reachedPrint, "value", "body position reached");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_BodyPosition_Start_Action0", start.Id, GraphPortDefaults.FlowOut, actions[0].Id, GraphPortDefaults.FlowIn),
            Flow("CONN_BodyPosition_Trigger_Print", reached.Id, GraphPortDefaults.FlowOut, reachedPrint.Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < actions.Count - 1; index++)
        {
            connections.Add(Flow($"CONN_BodyPosition_Action_{index}_{index + 1}", actions[index].Id, GraphPortDefaults.FlowOut, actions[index + 1].Id, GraphPortDefaults.FlowIn));
        }

        connections.Add(Flow("CONN_BodyPosition_Actions_Condition0", actions[^1].Id, GraphPortDefaults.FlowOut, conditions[0].Id, GraphPortDefaults.FlowIn));
        connections.Add(Flow("CONN_BodyPosition_Condition0_Condition1", conditions[0].Id, GraphPortDefaults.FlowOut, conditions[1].Id, GraphPortDefaults.FlowIn));
        connections.Add(Flow("CONN_BodyPosition_Condition1_Print0", conditions[1].Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn));
        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_BodyPositionValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_PrintBodyPosition_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_BodyPosition",
            Name = "BodyPosition",
            Nodes = [start, reached, .. actions, .. conditions, .. valueNodes, .. printNodes, reachedPrint],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "BodyPositionGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("bodyPositionObject.TargetPosition = makeVector3(1, 2, 3)", luau);
        Assert.Contains("bodyPositionObject.Force = 250", luau);
        Assert.Contains("bodyPositionObject.AcceptanceDistance = 2.5", luau);
        Assert.Contains("local distanceToTarget = vrsDistanceBetweenPositions(parentObject.Position, bodyPositionObject.TargetPosition)", luau);
        Assert.Contains("return distanceToTarget <= stopDistance", luau);
        Assert.Contains("return (tonumber(bodyPositionObject.Force) or 0) >= 200", luau);
        Assert.Contains("return targetObject.TargetPosition", luau);
        Assert.Contains("return tonumber(targetObject.Force) or 0", luau);
        Assert.Contains("return tonumber(targetObject.AcceptanceDistance) or 0", luau);
        Assert.Contains("Body Position Distance To Target stopped", luau);
        Assert.Contains("local previousMatched = readMatched() == true", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, distance = currentValue }", luau);
        Assert.Contains("body position reached", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsPhysicalActionsConditionsValuesAndTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartPhysical");
        var triggerIds = new[]
        {
            "EV_OnObjectTouchEnded",
            "EV_OnObjectHoverStarted",
            "EV_OnObjectHoverEnded"
        };
        var actionIds = new[]
        {
            "ACT_MoveObjectWithPhysics",
            "ACT_TurnObjectWithPhysics",
            "ACT_SetObjectVelocity",
            "ACT_SetObjectSpinVelocity",
            "ACT_SetRigidBodyGravity",
            "ACT_SetRigidBodyMass",
            "ACT_SetRigidBodyFriction",
            "ACT_SetRigidBodyDrag",
            "ACT_SetRigidBodyAngularDrag",
            "ACT_SetRigidBodyBounciness"
        };
        var conditionIds = new[]
        {
            "COND_ObjectIsMoving",
            "COND_ObjectSpeedAtLeast"
        };
        var valueIds = new[]
        {
            "PROP_ObjectVelocity",
            "PROP_ObjectSpeed",
            "PROP_ObjectSpinVelocity",
            "PROP_TouchingObjectCount",
            "PROP_RigidBodyGravityEnabled",
            "PROP_RigidBodyMass",
            "PROP_RigidBodyFriction",
            "PROP_RigidBodyDrag",
            "PROP_RigidBodyAngularDrag",
            "PROP_RigidBodyBounciness"
        };
        var triggers = triggerIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"TRG_Physical_{index}"))
            .ToList();
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"ACT_Physical_{index}"))
            .ToList();
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"COND_Physical_{index}"))
            .ToList();
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_Physical_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintPhysical_{index}"))
            .ToList();
        var triggerPrints = triggerIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintPhysicalTrigger_{index}"))
            .ToList();

        foreach (var node in triggers.Concat(actions).Concat(conditions).Concat(valueNodes))
        {
            SetSceneObject(node, "target", "World/Environment/PhysicsBlock");
        }

        SetConstant(actions[0], "x", "1");
        SetConstant(actions[0], "y", "2");
        SetConstant(actions[0], "z", "3");
        SetConstant(actions[1], "x", "4");
        SetConstant(actions[1], "y", "5");
        SetConstant(actions[1], "z", "6");
        SetConstant(actions[2], "x", "7");
        SetConstant(actions[2], "y", "8");
        SetConstant(actions[2], "z", "9");
        SetConstant(actions[3], "x", "0");
        SetConstant(actions[3], "y", "3");
        SetConstant(actions[3], "z", "0");
        SetConstant(actions[4], "enabled", "false");
        SetConstant(actions[5], "mass", "12");
        SetConstant(actions[6], "friction", "0.25");
        SetConstant(actions[7], "drag", "0.4");
        SetConstant(actions[8], "angularDrag", "0.6");
        SetConstant(actions[9], "bounciness", "0.8");
        SetConstant(conditions[0], "minimumSpeed", "0.5");
        SetConstant(conditions[1], "speed", "10");
        SetConstant(triggerPrints[0], "value", "touch ended");
        SetConstant(triggerPrints[1], "value", "hover started");
        SetConstant(triggerPrints[2], "value", "hover ended");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Physical_Start_Action0", start.Id, GraphPortDefaults.FlowOut, actions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < actions.Count - 1; index++)
        {
            connections.Add(Flow($"CONN_Physical_Action_{index}_{index + 1}", actions[index].Id, GraphPortDefaults.FlowOut, actions[index + 1].Id, GraphPortDefaults.FlowIn));
        }

        connections.Add(Flow("CONN_Physical_Actions_Condition0", actions[^1].Id, GraphPortDefaults.FlowOut, conditions[0].Id, GraphPortDefaults.FlowIn));
        connections.Add(Flow("CONN_Physical_Condition0_Condition1", conditions[0].Id, GraphPortDefaults.FlowOut, conditions[1].Id, GraphPortDefaults.FlowIn));
        connections.Add(Flow("CONN_Physical_Condition1_Print0", conditions[1].Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn));
        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_PhysicalValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_PrintPhysical_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        for (var index = 0; index < triggers.Count; index++)
        {
            connections.Add(Flow($"CONN_PhysicalTrigger_{index}_Print", triggers[index].Id, GraphPortDefaults.FlowOut, triggerPrints[index].Id, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_Physical",
            Name = "Physical",
            Nodes = [start, .. triggers, .. actions, .. conditions, .. valueNodes, .. printNodes, .. triggerPrints],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "PhysicalGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("targetObject:MovePosition(makeVector3(1, 2, 3))", luau);
        Assert.Contains("targetObject:MoveRotation(makeVector3(4, 5, 6))", luau);
        Assert.Contains("targetObject.Velocity = makeVector3(7, 8, 9)", luau);
        Assert.Contains("targetObject.AngularVelocity = makeVector3(0, 3, 0)", luau);
        Assert.Contains("targetObject.UseGravity = false", luau);
        Assert.Contains("targetObject.Mass = 12", luau);
        Assert.Contains("targetObject.Friction = 0.25", luau);
        Assert.Contains("targetObject.Drag = 0.4", luau);
        Assert.Contains("targetObject.AngularDrag = 0.6", luau);
        Assert.Contains("targetObject.Bounciness = 0.8", luau);
        Assert.Contains("local currentSpeed = vrsDistanceBetweenPositions(targetObject.Velocity, makeVector3(0, 0, 0))", luau);
        Assert.Contains("return currentSpeed >= 0.5", luau);
        Assert.Contains("return currentSpeed >= 10", luau);
        Assert.Contains("return targetObject.Velocity", luau);
        Assert.Contains("Object Speed stopped", luau);
        Assert.Contains("return targetObject.AngularVelocity", luau);
        Assert.Contains("local touchingObjects = targetObject:GetTouching()", luau);
        Assert.Contains("return #touchingObjects", luau);
        Assert.Contains("return targetObject.UseGravity == true", luau);
        Assert.Contains("return tonumber(targetObject.Mass) or 0", luau);
        Assert.Contains("return tonumber(targetObject.Friction) or 0", luau);
        Assert.Contains("return tonumber(targetObject.Drag) or 0", luau);
        Assert.Contains("return tonumber(targetObject.AngularDrag) or 0", luau);
        Assert.Contains("return tonumber(targetObject.Bounciness) or 0", luau);
        Assert.Contains("listenObject.TouchEnded:Connect(function(hit)", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, touchObject = hit, touchObjectSource = triggerObject }", luau);
        Assert.Contains("listenObject.MouseEnter:Connect(function()", luau);
        Assert.Contains("listenObject.MouseExit:Connect(function()", luau);
        Assert.Contains("touch ended", luau);
        Assert.Contains("hover started", luau);
        Assert.Contains("hover ended", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsRigidBodyConditionsAndWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartRigidBody");
        var conditionIds = new[]
        {
            "COND_RigidBodyGravityEnabled",
            "COND_RigidBodyMassAtLeast",
            "COND_RigidBodyFrictionAtLeast",
            "COND_RigidBodyDragAtLeast",
            "COND_RigidBodyAngularDragAtLeast",
            "COND_RigidBodyBouncinessAtLeast"
        };
        var triggerIds = new[]
        {
            "EV_OnRigidBodyGravityEnabled",
            "EV_OnRigidBodyMassReached",
            "EV_OnRigidBodyFrictionReached",
            "EV_OnRigidBodyDragReached",
            "EV_OnRigidBodyAngularDragReached",
            "EV_OnRigidBodyBouncinessReached"
        };
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"COND_RigidBody_{index}"))
            .ToList();
        var triggers = triggerIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"TRG_RigidBody_{index}"))
            .ToList();
        var printAfterConditions = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintRigidBodyConditions");
        var triggerPrints = triggerIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintRigidBodyTrigger_{index}"))
            .ToList();

        foreach (var node in conditions.Concat(triggers))
        {
            SetSceneObject(node, "target", "World/Environment/RigidBodyBlock");
        }

        SetConstant(conditions[1], "mass", "2");
        SetConstant(conditions[2], "friction", "0.25");
        SetConstant(conditions[3], "drag", "0.4");
        SetConstant(conditions[4], "angularDrag", "0.6");
        SetConstant(conditions[5], "bounciness", "0.8");
        SetConstant(triggers[1], "mass", "2");
        SetConstant(triggers[2], "friction", "0.25");
        SetConstant(triggers[3], "drag", "0.4");
        SetConstant(triggers[4], "angularDrag", "0.6");
        SetConstant(triggers[5], "bounciness", "0.8");
        SetConstant(printAfterConditions, "value", "rigidbody conditions passed");
        for (var index = 0; index < triggerPrints.Count; index++)
        {
            SetConstant(triggerPrints[index], "value", $"rigidbody watcher {index}");
        }

        var connections = new List<GraphConnection>
        {
            Flow("CONN_RigidBody_Start_Condition0", start.Id, GraphPortDefaults.FlowOut, conditions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < conditions.Count - 1; index++)
        {
            connections.Add(Flow($"CONN_RigidBody_Condition_{index}_{index + 1}", conditions[index].Id, GraphPortDefaults.FlowOut, conditions[index + 1].Id, GraphPortDefaults.FlowIn));
        }

        connections.Add(Flow("CONN_RigidBody_ConditionLast_Print", conditions[^1].Id, GraphPortDefaults.FlowOut, printAfterConditions.Id, GraphPortDefaults.FlowIn));
        for (var index = 0; index < triggers.Count; index++)
        {
            connections.Add(Flow($"CONN_RigidBody_Trigger_{index}_Print", triggers[index].Id, GraphPortDefaults.FlowOut, triggerPrints[index].Id, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_RigidBody",
            Name = "Rigid Body Watchers",
            Nodes = [start, .. conditions, printAfterConditions, .. triggers, .. triggerPrints],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "RigidBodyGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("return (targetObject.UseGravity == true) == true", luau);
        Assert.Contains("local currentNumber = tonumber(targetObject.Mass) or 0", luau);
        Assert.Contains("local currentNumber = tonumber(targetObject.Friction) or 0", luau);
        Assert.Contains("local currentNumber = tonumber(targetObject.Drag) or 0", luau);
        Assert.Contains("local currentNumber = tonumber(targetObject.AngularDrag) or 0", luau);
        Assert.Contains("local currentNumber = tonumber(targetObject.Bounciness) or 0", luau);
        Assert.Contains("local expectedNumber = tonumber(2) or 0", luau);
        Assert.Contains("local expectedNumber = tonumber(0.25) or 0", luau);
        Assert.Contains("local expectedNumber = tonumber(0.4) or 0", luau);
        Assert.Contains("local expectedNumber = tonumber(0.6) or 0", luau);
        Assert.Contains("local expectedNumber = tonumber(0.8) or 0", luau);
        Assert.Contains("local previousMatched = readMatched() == true", luau);
        Assert.Contains("local watchedLimit = tonumber(2) or 1", luau);
        Assert.Contains("local watchedLimit = tonumber(0.25) or 0.5", luau);
        Assert.Contains("local watchedLimit = tonumber(0.4) or 0", luau);
        Assert.Contains("local watchedLimit = tonumber(0.6) or 0", luau);
        Assert.Contains("local watchedLimit = tonumber(0.8) or 0", luau);
        Assert.Contains("return currentValue >= watchedLimit, currentValue", luau);
        Assert.Contains("gravityEnabled = currentValue", luau);
        Assert.Contains("rigidBodyMass = currentValue", luau);
        Assert.Contains("rigidBodyFriction = currentValue", luau);
        Assert.Contains("rigidBodyDrag = currentValue", luau);
        Assert.Contains("rigidBodyAngularDrag = currentValue", luau);
        Assert.Contains("rigidBodyBounciness = currentValue", luau);
        Assert.Contains("rigidbody conditions passed", luau);
        Assert.Contains("rigidbody watcher 5", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsExplosionActionsValuesAndTouchedTrigger()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var touched = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnExplosionTouched"), stableId: "TRG_ExplosionTouched");
        var actionIds = new[]
        {
            "ACT_SetExplosionRadius",
            "ACT_SetExplosionForce",
            "ACT_SetExplosionDamage",
            "ACT_SetExplosionAffectAnchored"
        };
        var valueIds = new[]
        {
            "PROP_ExplosionRadius",
            "PROP_ExplosionForce",
            "PROP_ExplosionDamage",
            "PROP_ExplosionAffectAnchored"
        };
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"ACT_Explosion_{index}"))
            .ToList();
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_Explosion_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintExplosion_{index}"))
            .ToList();
        var touchedPrint = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintExplosionTouched");

        foreach (var node in new[] { touched }.Concat(actions).Concat(valueNodes))
        {
            SetSceneObject(node, "target", "World/Effects/Blast");
        }

        SetConstant(actions[0], "radius", "18");
        SetConstant(actions[1], "force", "650");
        SetConstant(actions[2], "damage", "80");
        SetConstant(actions[3], "enabled", "true");
        SetConstant(touchedPrint, "value", "explosion touched");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_Explosion0", start.Id, GraphPortDefaults.FlowOut, actions[0].Id, GraphPortDefaults.FlowIn),
            Flow("CONN_ExplosionTouched_Print", touched.Id, GraphPortDefaults.FlowOut, touchedPrint.Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < actions.Count; index++)
        {
            if (index + 1 < actions.Count)
            {
                connections.Add(Flow($"CONN_Explosion_Action_{index}_{index + 1}", actions[index].Id, GraphPortDefaults.FlowOut, actions[index + 1].Id, GraphPortDefaults.FlowIn));
            }
            else
            {
                connections.Add(Flow("CONN_Explosion_Action_Print0", actions[index].Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn));
            }
        }

        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_ExplosionValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_Explosion_Print_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_Explosion",
            Name = "Explosion",
            Nodes = [start, touched, .. actions, .. valueNodes, .. printNodes, touchedPrint],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "ExplosionGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("targetObject.Radius = 18", luau);
        Assert.Contains("targetObject.Force = 650", luau);
        Assert.Contains("targetObject.Damage = 80", luau);
        Assert.Contains("targetObject.AffectAnchored = true", luau);
        Assert.Contains("return tonumber(targetObject.Radius) or 0", luau);
        Assert.Contains("return tonumber(targetObject.Force) or 0", luau);
        Assert.Contains("return tonumber(targetObject.Damage) or 0", luau);
        Assert.Contains("return targetObject.AffectAnchored == true", luau);
        Assert.Contains("listenObject.Touched:Connect(function(hit)", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, explosion = triggerObject, touchObject = hit, affectedObject = hit, touchObjectSource = triggerObject }", luau);
        Assert.Contains("explosion touched", luau);
        Assert.DoesNotContain("Set Explosion Radius is not implemented", luau);
        Assert.DoesNotContain("Explosion Affect Anchored is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsGrabbableActionsValuesAndEvents()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var grabbed = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnObjectGrabbed"), stableId: "TRG_Grabbed");
        var released = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnObjectReleased"), stableId: "TRG_Released");
        var actionIds = new[]
        {
            "ACT_SetGrabForce",
            "ACT_SetGrabMaxRange",
            "ACT_SetGrabPickupRange",
            "ACT_SetGrabUsesDragForce",
            "ACT_SetGrabPermissionMode"
        };
        var conditionIds = new[]
        {
            "COND_GrabForceAtLeast",
            "COND_GrabMaxRangeAtLeast",
            "COND_GrabPickupRangeAtLeast",
            "COND_GrabUsesDragForce",
            "COND_GrabPermissionModeIs"
        };
        var watcherIds = new[]
        {
            "EV_OnGrabForceReached",
            "EV_OnGrabMaxRangeReached",
            "EV_OnGrabPickupRangeReached"
        };
        var valueIds = new[]
        {
            "PROP_GrabForce",
            "PROP_GrabMaxRange",
            "PROP_GrabPickupRange",
            "PROP_GrabUsesDragForce",
            "PROP_GrabPermissionMode",
            "PROP_CurrentGrabber"
        };
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"ACT_Grab_{index}"))
            .ToList();
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"COND_Grab_{index}"))
            .ToList();
        var watchers = watcherIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"TRG_GrabWatcher_{index}"))
            .ToList();
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_Grab_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintGrab_{index}"))
            .ToList();
        var grabbedPrint = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintGrabbed");
        var releasedPrint = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintReleased");
        var watcherPrints = watcherIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintGrabWatcher_{index}"))
            .ToList();
        var conditionPassedPrint = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintGrabConditions");

        foreach (var node in new[] { grabbed, released }.Concat(actions).Concat(conditions).Concat(watchers).Concat(valueNodes))
        {
            SetSceneObject(node, "target", "World/Environment/CrateGrabber");
        }

        SetConstant(actions[0], "force", "900");
        SetConstant(actions[1], "range", "40");
        SetConstant(actions[2], "range", "12");
        SetConstant(actions[3], "enabled", "false");
        SetConstant(actions[4], "mode", "Scripted");
        SetConstant(conditions[0], "force", "500");
        SetConstant(conditions[1], "range", "30");
        SetConstant(conditions[2], "range", "15");
        SetConstant(conditions[4], "mode", "Scripted");
        SetConstant(watchers[0], "force", "500");
        SetConstant(watchers[1], "range", "30");
        SetConstant(watchers[2], "range", "15");
        SetConstant(grabbedPrint, "value", "grabbed");
        SetConstant(releasedPrint, "value", "released");
        SetConstant(watcherPrints[0], "value", "grab force reached");
        SetConstant(watcherPrints[1], "value", "grab drag range reached");
        SetConstant(watcherPrints[2], "value", "grab pickup range reached");
        SetConstant(conditionPassedPrint, "value", "grab conditions passed");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_Grab0", start.Id, GraphPortDefaults.FlowOut, actions[0].Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Grabbed_Print", grabbed.Id, GraphPortDefaults.FlowOut, grabbedPrint.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_Released_Print", released.Id, GraphPortDefaults.FlowOut, releasedPrint.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_GrabWatcher_0_Print", watchers[0].Id, GraphPortDefaults.FlowOut, watcherPrints[0].Id, GraphPortDefaults.FlowIn),
            Flow("CONN_GrabWatcher_1_Print", watchers[1].Id, GraphPortDefaults.FlowOut, watcherPrints[1].Id, GraphPortDefaults.FlowIn),
            Flow("CONN_GrabWatcher_2_Print", watchers[2].Id, GraphPortDefaults.FlowOut, watcherPrints[2].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < actions.Count; index++)
        {
            if (index + 1 < actions.Count)
            {
                connections.Add(Flow($"CONN_Grab_Action_{index}_{index + 1}", actions[index].Id, GraphPortDefaults.FlowOut, actions[index + 1].Id, GraphPortDefaults.FlowIn));
            }
            else
            {
                connections.Add(Flow("CONN_Grab_Action_Print0", actions[index].Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn));
            }
        }

        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_GrabValue_Print_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_Grab_Print_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        connections.Add(Flow("CONN_Grab_Print_Conditions", printNodes[^1].Id, GraphPortDefaults.FlowOut, conditions[0].Id, GraphPortDefaults.FlowIn));
        for (var index = 0; index < conditions.Count; index++)
        {
            if (index + 1 < conditions.Count)
            {
                connections.Add(Flow($"CONN_Grab_Condition_{index}_{index + 1}", conditions[index].Id, GraphPortDefaults.FlowOut, conditions[index + 1].Id, GraphPortDefaults.FlowIn));
            }
            else
            {
                connections.Add(Flow("CONN_Grab_Conditions_Print", conditions[index].Id, GraphPortDefaults.FlowOut, conditionPassedPrint.Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_Grab",
            Name = "Grab",
            Nodes = [start, grabbed, released, .. watchers, .. actions, .. conditions, .. valueNodes, .. printNodes, grabbedPrint, releasedPrint, .. watcherPrints, conditionPassedPrint],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "GrabGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("targetObject.Force = 900", luau);
        Assert.Contains("targetObject.MaxRange = 40", luau);
        Assert.Contains("targetObject.MaxGrabbableRange = 12", luau);
        Assert.Contains("targetObject.UseDragForce = false", luau);
        Assert.Contains("local grabPermissionModeName = tostring(\"Scripted\" or \"Everyone\")", luau);
        Assert.Contains("targetObject.PermissionMode = Enums.GrabbablePermissionMode[grabPermissionModeName]", luau);
        Assert.Contains("return tonumber(targetObject.Force) or 0", luau);
        Assert.Contains("return tonumber(targetObject.MaxRange) or 0", luau);
        Assert.Contains("return tonumber(targetObject.MaxGrabbableRange) or 0", luau);
        Assert.Contains("return targetObject.UseDragForce == true", luau);
        Assert.Contains("return tostring(targetObject.PermissionMode or \"\")", luau);
        Assert.Contains("return targetObject.Dragger", luau);
        Assert.Contains("local currentNumber = tonumber(targetObject.Force) or 0", luau);
        Assert.Contains("local currentNumber = tonumber(targetObject.MaxRange) or 0", luau);
        Assert.Contains("local currentNumber = tonumber(targetObject.MaxGrabbableRange) or 0", luau);
        Assert.Contains("return (targetObject.UseDragForce == true) == true", luau);
        Assert.Contains("return tostring(targetObject.PermissionMode or \"\") == tostring(\"Scripted\")", luau);
        Assert.Contains("listenObject.Grabbed:Connect(function(player)", luau);
        Assert.Contains("listenObject.Released:Connect(function(player)", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, player = player, grabber = player, grabbable = triggerObject }", luau);
        Assert.Contains("local watchedLimit = tonumber(500) or 500", luau);
        Assert.Contains("local watchedLimit = tonumber(30) or 30", luau);
        Assert.Contains("local watchedLimit = tonumber(15) or 15", luau);
        Assert.Contains("triggerObject.Force", luau);
        Assert.Contains("triggerObject.MaxRange", luau);
        Assert.Contains("triggerObject.MaxGrabbableRange", luau);
        Assert.Contains("grabForce = currentValue", luau);
        Assert.Contains("grabMaxRange = currentValue", luau);
        Assert.Contains("grabPickupRange = currentValue", luau);
        Assert.Contains("grabbed", luau);
        Assert.Contains("released", luau);
        Assert.Contains("grab conditions passed", luau);
        Assert.Contains("grab force reached", luau);
        Assert.DoesNotContain("Set Grab Force is not implemented", luau);
        Assert.DoesNotContain("Current Grabber is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsCharacterAnimationActionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var actionIds = new[]
        {
            "ACT_PlayCharacterAnimation",
            "ACT_PlayCharacterOneShotAnimation",
            "ACT_StopCharacterAnimation",
            "ACT_StopCharacterOneShotAnimation",
            "ACT_SetCharacterState",
            "ACT_SetCharacterAnimationSpeed"
        };
        var valueIds = new[]
        {
            "PROP_CurrentCharacterAnimation",
            "PROP_CharacterAnimator",
            "PROP_CharacterState",
            "PROP_CharacterAnimationSpeed",
            "PROP_CharacterAttachment"
        };
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"ACT_CharacterAnimation_{index}"))
            .ToList();
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_CharacterAnimation_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintCharacterAnimation_{index}"))
            .ToList();

        foreach (var node in actions.Concat(valueNodes))
        {
            SetSceneObject(node, "target", "World/Characters/Hero");
        }

        SetConstant(actions[0], "animation", "Run");
        SetConstant(actions[1], "animation", "Wave");
        SetConstant(actions[4], "state", "Running");
        SetConstant(actions[5], "speed", "1.25");
        SetConstant(valueNodes[4], "attachment", "HandRight");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_PlayCharacterAnimation", start.Id, GraphPortDefaults.FlowOut, actions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < actions.Count; index++)
        {
            if (index + 1 < actions.Count)
            {
                connections.Add(Flow($"CONN_CharacterAnimation_Action_{index}_{index + 1}", actions[index].Id, GraphPortDefaults.FlowOut, actions[index + 1].Id, GraphPortDefaults.FlowIn));
            }
            else
            {
                connections.Add(Flow("CONN_CharacterAnimation_Print0", actions[index].Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn));
            }
        }

        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_CharacterAnimation_Value_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_CharacterAnimation_Print_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_CharacterAnimation",
            Name = "Character Animation",
            Nodes = [start, .. actions, .. valueNodes, .. printNodes],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "CharacterAnimationGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local animatorObject = characterObject.Animator", luau);
        Assert.Contains("animatorObject:PlayAnimation(tostring(\"Run\"))", luau);
        Assert.Contains("animatorObject:PlayOneShotAnimation(tostring(\"Wave\"))", luau);
        Assert.Contains("animatorObject:StopAnimation()", luau);
        Assert.Contains("animatorObject:StopOneShotAnimation()", luau);
        Assert.Contains("local characterStateName = tostring(\"Running\" or \"Idle\")", luau);
        Assert.Contains("characterObject.CurrentState = Enums.CharacterModelState[characterStateName]", luau);
        Assert.Contains("targetObject.CurrentSpeed = 1.25", luau);
        Assert.Contains("return tostring(animatorObject.CurrentAnimation or \"\")", luau);
        Assert.Contains("return targetObject.Animator", luau);
        Assert.Contains("return tostring(targetObject.CurrentState or \"\")", luau);
        Assert.Contains("return tonumber(targetObject.CurrentSpeed) or 0", luau);
        Assert.Contains("local attachmentName = tostring(\"HandRight\" or \"Head\")", luau);
        Assert.Contains("return characterObject:GetAttachment(attachmentValue)", luau);
        Assert.DoesNotContain("Play Character Animation is not implemented", luau);
        Assert.DoesNotContain("Character Attachment is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsCharacterAnimationConditionsAndWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartCharacterAnimationConditions");
        var conditionIds = new[]
        {
            "COND_CurrentCharacterAnimationIs",
            "COND_CharacterHasAnimator",
            "COND_CharacterStateIs",
            "COND_CharacterAnimationSpeedAtLeast",
            "COND_CharacterAnimationSpeedAtMost",
            "COND_CharacterHasAttachment"
        };
        var triggerIds = new[]
        {
            "EV_OnCharacterAnimationChanged",
            "EV_OnCharacterAnimationBecame",
            "EV_OnCharacterStateChanged",
            "EV_OnCharacterStateBecame",
            "EV_OnCharacterAnimationSpeedReached",
            "EV_OnCharacterAttachmentAvailable"
        };
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"COND_CharacterAnimation_{index}"))
            .ToList();
        var triggers = triggerIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"TRG_CharacterAnimation_{index}"))
            .ToList();
        var printAfterConditions = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintCharacterAnimationConditions");
        var triggerPrints = triggerIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintCharacterAnimationTrigger_{index}"))
            .ToList();

        foreach (var node in conditions.Concat(triggers))
        {
            SetSceneObject(node, "target", "World/Characters/Hero");
        }

        SetConstant(conditions[0], "animation", "Run");
        SetConstant(conditions[2], "state", "Running");
        SetConstant(conditions[3], "speed", "1.25");
        SetConstant(conditions[4], "speed", "2");
        SetConstant(conditions[5], "attachment", "HandRight");
        SetConstant(triggers[1], "animation", "Run");
        SetConstant(triggers[3], "state", "Running");
        SetConstant(triggers[4], "speed", "1.25");
        SetConstant(triggers[5], "attachment", "HandRight");
        SetConstant(printAfterConditions, "value", "character animation conditions passed");
        for (var index = 0; index < triggerPrints.Count; index++)
        {
            SetConstant(triggerPrints[index], "value", $"character animation watcher {index}");
        }

        var connections = new List<GraphConnection>
        {
            Flow("CONN_CharacterAnimation_Start_Condition0", start.Id, GraphPortDefaults.FlowOut, conditions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < conditions.Count - 1; index++)
        {
            connections.Add(Flow($"CONN_CharacterAnimation_Condition_{index}_{index + 1}", conditions[index].Id, GraphPortDefaults.FlowOut, conditions[index + 1].Id, GraphPortDefaults.FlowIn));
        }

        connections.Add(Flow("CONN_CharacterAnimation_ConditionLast_Print", conditions[^1].Id, GraphPortDefaults.FlowOut, printAfterConditions.Id, GraphPortDefaults.FlowIn));
        for (var index = 0; index < triggers.Count; index++)
        {
            connections.Add(Flow($"CONN_CharacterAnimation_Trigger_{index}_Print", triggers[index].Id, GraphPortDefaults.FlowOut, triggerPrints[index].Id, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_CharacterAnimationConditions",
            Name = "Character Animation Conditions",
            Nodes = [start, .. conditions, printAfterConditions, .. triggers, .. triggerPrints],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "CharacterAnimationConditionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local currentText = tostring(characterObject.Animator.CurrentAnimation or \"\")", luau);
        Assert.Contains("local expectedText = tostring(\"Run\" or \"Idle\")", luau);
        Assert.Contains("return characterObject ~= nil and characterObject.Animator ~= nil", luau);
        Assert.Contains("local currentText = tostring(characterObject.CurrentState or \"\")", luau);
        Assert.Contains("local expectedText = tostring(\"Running\" or \"Idle\")", luau);
        Assert.Contains("local currentNumber = tonumber(characterObject.CurrentSpeed) or 0", luau);
        Assert.Contains("local expectedNumber = tonumber(1.25) or 1", luau);
        Assert.Contains("local expectedNumber = tonumber(2) or 1", luau);
        Assert.Contains("return currentNumber >= expectedNumber", luau);
        Assert.Contains("return currentNumber <= expectedNumber", luau);
        Assert.Contains("return characterObject:GetAttachment(attachmentValue) ~= nil", luau);
        Assert.Contains("return tostring(triggerObject.Animator.CurrentAnimation or \"\")", luau);
        Assert.Contains("local expectedAnimation = tostring(\"Run\" or \"Idle\")", luau);
        Assert.Contains("return currentValue == expectedAnimation, currentValue", luau);
        Assert.Contains("return tostring(triggerObject.CurrentState or \"\")", luau);
        Assert.Contains("local expectedState = tostring(\"Running\" or \"Idle\")", luau);
        Assert.Contains("return currentValue == expectedState, currentValue", luau);
        Assert.Contains("local watchedSpeed = tonumber(1.25) or 1", luau);
        Assert.Contains("return currentValue >= watchedSpeed, currentValue", luau);
        Assert.Contains("return triggerObject:GetAttachment(attachmentValue)", luau);
        Assert.Contains("characterAnimation = currentValue", luau);
        Assert.Contains("characterAnimationSpeed = currentValue", luau);
        Assert.Contains("characterAttachment = currentValue", luau);
        Assert.Contains("character animation conditions passed", luau);
        Assert.Contains("character animation watcher 5", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsCharacterAppearanceConditions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var accessoryAttachment = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_AccessoryAttachmentIs"), stableId: "COND_AccessoryAttachment");
        var clothingHasImage = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_ClothingHasImage"), stableId: "COND_ClothingImage");
        var faceImage = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_CharacterFaceImageIs"), stableId: "COND_FaceImage");
        var notRagdolling = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "COND_CharacterIsNotRagdolling"), stableId: "COND_NotRagdolling");
        var message = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_Message");

        SetSceneObject(accessoryAttachment, "target", "World/Players/NPC/Hat");
        SetSceneObject(clothingHasImage, "target", "World/Players/NPC/Shirt");
        SetSceneObject(faceImage, "target", "World/Players/NPC");
        SetSceneObject(notRagdolling, "target", "World/Players/NPC");
        SetConstant(accessoryAttachment, "attachment", "Head");
        SetConstant(faceImage, "image", "images/face.png");

        var rule = new Rule
        {
            Id = "RULE_CharacterAppearanceConditions",
            Name = "Character Appearance Conditions",
            Nodes = [start, accessoryAttachment, clothingHasImage, faceImage, notRagdolling, message],
            Connections =
            [
                Flow("CONN_Start_Attachment", start.Id, GraphPortDefaults.FlowOut, accessoryAttachment.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Attachment_Clothing", accessoryAttachment.Id, GraphPortDefaults.TrueOut, clothingHasImage.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Clothing_Face", clothingHasImage.Id, GraphPortDefaults.TrueOut, faceImage.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Face_Ragdoll", faceImage.Id, GraphPortDefaults.TrueOut, notRagdolling.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Ragdoll_Message", notRagdolling.Id, GraphPortDefaults.TrueOut, message.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "CharacterAppearanceConditionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local currentText = tostring(targetObject.TargetAttachment or \"\")", luau);
        Assert.Contains("local expectedText = tostring(\"Head\" or \"\")", luau);
        Assert.Contains("return tostring(targetObject.Image or \"\") ~= \"\"", luau);
        Assert.Contains("local currentText = tostring(targetObject.FaceImage or \"\")", luau);
        Assert.Contains("local expectedText = tostring(\"images/face.png\" or \"\")", luau);
        Assert.Contains("return (targetObject.Ragdolling == true) == false", luau);
        Assert.DoesNotContain("Accessory Attachment Is is not implemented", luau);
        Assert.DoesNotContain("Character Is Not Ragdolling is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsCharacterAppearanceActionsValuesAndEvents()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var ragdollStarted = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnCharacterRagdollStarted"), stableId: "TRG_RagdollStarted");
        var ragdollStopped = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnCharacterRagdollStopped"), stableId: "TRG_RagdollStopped");
        var actionIds = new[]
        {
            "ACT_SetAccessoryAttachment",
            "ACT_SetClothingImage",
            "ACT_SetCharacterFaceImage",
            "ACT_SetCharacterBodyMesh",
            "ACT_SetCharacterBodyColor",
            "ACT_LoadCharacterAppearance",
            "ACT_ClearCharacterAppearance",
            "ACT_StartCharacterRagdoll",
            "ACT_StopCharacterRagdoll"
        };
        var valueIds = new[]
        {
            "PROP_AccessoryAttachment",
            "PROP_ClothingImage",
            "PROP_CharacterFaceImage",
            "PROP_CharacterBodyMesh",
            "PROP_CharacterBodyColor",
            "PROP_CharacterRagdolling",
            "PROP_CharacterRagdollPosition",
            "PROP_CharacterRagdollRotation"
        };
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"ACT_CharacterAppearance_{index}"))
            .ToList();
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_CharacterAppearance_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintCharacterAppearance_{index}"))
            .ToList();
        var startedPrint = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintRagdollStarted");
        var stoppedPrint = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintRagdollStopped");

        SetSceneObject(actions[0], "target", "World/Characters/Hero/Hat");
        SetSceneObject(actions[1], "target", "World/Characters/Hero/Shirt");
        foreach (var node in actions.Skip(2).Concat(new[] { ragdollStarted, ragdollStopped }))
        {
            SetSceneObject(node, "target", "World/Characters/Hero");
        }

        SetSceneObject(valueNodes[0], "target", "World/Characters/Hero/Hat");
        SetSceneObject(valueNodes[1], "target", "World/Characters/Hero/Shirt");
        foreach (var node in valueNodes.Skip(2))
        {
            SetSceneObject(node, "target", "World/Characters/Hero");
        }

        SetConstant(actions[0], "attachment", "HandRight");
        SetConstant(actions[1], "image", "images/shirt.png");
        SetConstant(actions[2], "image", "images/face.png");
        SetConstant(actions[3], "mesh", "meshes/body.mesh");
        SetConstant(actions[4], "bodyPart", "Left Arm");
        SetConstant(actions[4], "r", "0.2");
        SetConstant(actions[4], "g", "0.4");
        SetConstant(actions[4], "b", "0.6");
        SetConstant(actions[5], "userId", "12345");
        SetConstant(actions[5], "loadTools", "false");
        SetConstant(actions[7], "x", "1");
        SetConstant(actions[7], "y", "2");
        SetConstant(actions[7], "z", "3");
        SetConstant(valueNodes[4], "bodyPart", "Left Arm");
        SetConstant(startedPrint, "value", "started");
        SetConstant(stoppedPrint, "value", "stopped");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_Appearance0", start.Id, GraphPortDefaults.FlowOut, actions[0].Id, GraphPortDefaults.FlowIn),
            Flow("CONN_RagdollStarted_Print", ragdollStarted.Id, GraphPortDefaults.FlowOut, startedPrint.Id, GraphPortDefaults.FlowIn),
            Flow("CONN_RagdollStopped_Print", ragdollStopped.Id, GraphPortDefaults.FlowOut, stoppedPrint.Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < actions.Count; index++)
        {
            if (index + 1 < actions.Count)
            {
                connections.Add(Flow($"CONN_CharacterAppearance_Action_{index}_{index + 1}", actions[index].Id, GraphPortDefaults.FlowOut, actions[index + 1].Id, GraphPortDefaults.FlowIn));
            }
            else
            {
                connections.Add(Flow("CONN_CharacterAppearance_Print0", actions[index].Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn));
            }
        }

        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_CharacterAppearance_Value_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_CharacterAppearance_Print_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_CharacterAppearance",
            Name = "Character Appearance",
            Nodes = [start, ragdollStarted, ragdollStopped, .. actions, .. valueNodes, .. printNodes, startedPrint, stoppedPrint],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "CharacterAppearanceGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("targetObject.TargetAttachment = attachmentValue", luau);
        Assert.Contains("targetObject.Image = tostring(\"images/shirt.png\")", luau);
        Assert.Contains("targetObject.FaceImage = tostring(\"images/face.png\")", luau);
        Assert.Contains("targetObject.BodyMesh = tostring(\"meshes/body.mesh\")", luau);
        Assert.Contains("local bodyPartName = tostring(\"Left Arm\" or \"Head\")", luau);
        Assert.Contains("characterObject.LeftArmColor = bodyColor", luau);
        Assert.Contains("characterObject:LoadAppearance(math.floor(tonumber(12345) or 0), false)", luau);
        Assert.Contains("characterObject:ClearAppearance()", luau);
        Assert.Contains("characterObject:StartRagdoll(makeVector3(1, 2, 3))", luau);
        Assert.Contains("characterObject:StopRagdoll()", luau);
        Assert.Contains("return tostring(targetObject.TargetAttachment or \"\")", luau);
        Assert.Contains("return tostring(targetObject.Image or \"\")", luau);
        Assert.Contains("return tostring(targetObject.FaceImage or \"\")", luau);
        Assert.Contains("return tostring(targetObject.BodyMesh or \"\")", luau);
        Assert.Contains("return characterObject.LeftArmColor or Color.New(1, 1, 1, 1)", luau);
        Assert.Contains("return targetObject.Ragdolling == true", luau);
        Assert.Contains("return targetObject.RagdollPosition", luau);
        Assert.Contains("return targetObject.RagdollRotation", luau);
        Assert.Contains("triggerObject.RagdollStarted:Connect(function()", luau);
        Assert.Contains("triggerObject.RagdollStopped:Connect(function()", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, character = triggerObject }", luau);
        Assert.DoesNotContain("Set Accessory Attachment is not implemented", luau);
        Assert.DoesNotContain("Character Ragdoll Position is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsWorldMarkerTrussAndEntityActionsAndValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_Start");
        var actionIds = new[]
        {
            "ACT_SetMarkerLength",
            "ACT_SetMarkerAppearsOnTop",
            "ACT_SetMarkerVisibleInDev",
            "ACT_SetTrussClimbSpeed",
            "ACT_SetEntityCastsShadows",
            "ACT_SetEntityIsSpawn",
            "ACT_SetEntityColor"
        };
        var valueIds = new[]
        {
            "PROP_MarkerLength",
            "PROP_MarkerAppearsOnTop",
            "PROP_MarkerVisibleInDev",
            "PROP_TrussClimbSpeed",
            "PROP_EntityCastsShadows",
            "PROP_EntityIsSpawn",
            "PROP_EntityColor"
        };
        var actions = actionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"ACT_WorldMarker_{index}"))
            .ToList();
        var valueNodes = valueIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"PROP_WorldMarker_{index}"))
            .ToList();
        var printNodes = valueIds
            .Select((_, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: $"ACT_PrintWorldMarker_{index}"))
            .ToList();

        foreach (var node in actions.Take(3).Concat(valueNodes.Take(3)))
        {
            SetSceneObject(node, "target", "World/Environment/Quest Marker");
        }

        SetSceneObject(actions[3], "target", "World/Environment/Ladder");
        SetSceneObject(valueNodes[3], "target", "World/Environment/Ladder");
        foreach (var node in actions.Skip(4).Concat(valueNodes.Skip(4)))
        {
            SetSceneObject(node, "target", "World/Environment/Spawn Block");
        }

        SetConstant(actions[0], "length", "7.5");
        SetConstant(actions[1], "enabled", "false");
        SetConstant(actions[2], "enabled", "false");
        SetConstant(actions[3], "speed", "12");
        SetConstant(actions[4], "enabled", "false");
        SetConstant(actions[5], "enabled", "true");
        SetConstant(actions[6], "r", "0.25");
        SetConstant(actions[6], "g", "0.5");
        SetConstant(actions[6], "b", "0.75");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_WorldMarker0", start.Id, GraphPortDefaults.FlowOut, actions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < actions.Count; index++)
        {
            if (index + 1 < actions.Count)
            {
                connections.Add(Flow($"CONN_WorldMarker_Action_{index}_{index + 1}", actions[index].Id, GraphPortDefaults.FlowOut, actions[index + 1].Id, GraphPortDefaults.FlowIn));
            }
            else
            {
                connections.Add(Flow("CONN_WorldMarker_Print0", actions[index].Id, GraphPortDefaults.FlowOut, printNodes[0].Id, GraphPortDefaults.FlowIn));
            }
        }

        for (var index = 0; index < printNodes.Count; index++)
        {
            connections.Add(Value($"CONN_WorldMarker_Value_{index}", valueNodes[index].Id, GraphPortDefaults.ValueOut, printNodes[index].Id, GraphPortDefaults.ParameterPortId("value")));
            if (index + 1 < printNodes.Count)
            {
                connections.Add(Flow($"CONN_WorldMarker_Print_{index}_{index + 1}", printNodes[index].Id, GraphPortDefaults.FlowOut, printNodes[index + 1].Id, GraphPortDefaults.FlowIn));
            }
        }

        var rule = new Rule
        {
            Id = "RULE_WorldMarker",
            Name = "World Marker",
            Nodes = [start, .. actions, .. valueNodes, .. printNodes],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "WorldMarkerGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("targetObject.Length = 7.5", luau);
        Assert.Contains("targetObject.AppearOnTop = false", luau);
        Assert.Contains("targetObject.VisibleInDev = false", luau);
        Assert.Contains("targetObject.ClimbSpeed = 12", luau);
        Assert.Contains("targetObject.CastShadows = false", luau);
        Assert.Contains("targetObject.IsSpawn = true", luau);
        Assert.Contains("targetObject.Color = Color.New(0.25, 0.5, 0.75, 1)", luau);
        Assert.Contains("return tonumber(targetObject.Length) or 0", luau);
        Assert.Contains("return targetObject.AppearOnTop == true", luau);
        Assert.Contains("return targetObject.VisibleInDev == true", luau);
        Assert.Contains("return tonumber(targetObject.ClimbSpeed) or 0", luau);
        Assert.Contains("return targetObject.CastShadows == true", luau);
        Assert.Contains("return targetObject.IsSpawn == true", luau);
        Assert.Contains("return targetObject.Color", luau);
        Assert.DoesNotContain("Set Marker Length is not implemented", luau);
        Assert.DoesNotContain("Entity Color is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsWorldMarkerTrussAndEntityConditions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "EV_OnStart"), stableId: "TRG_StartWorldMarkerConditions");
        var conditionIds = new[]
        {
            "COND_MarkerLengthAtLeast",
            "COND_MarkerAppearsOnTop",
            "COND_MarkerVisibleInDev",
            "COND_TrussClimbSpeedAtLeast",
            "COND_EntityCastsShadows",
            "COND_EntityIsSpawn",
            "COND_EntityColorIs"
        };
        var conditions = conditionIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"COND_WorldMarker_{index}"))
            .ToList();
        var message = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_WorldMarkerConditionMessage");

        foreach (var node in conditions.Take(3))
        {
            SetSceneObject(node, "target", "World/Environment/Quest Marker");
        }

        SetSceneObject(conditions[3], "target", "World/Environment/Ladder");
        foreach (var node in conditions.Skip(4))
        {
            SetSceneObject(node, "target", "World/Environment/Spawn Block");
        }

        SetConstant(conditions[0], "length", "7.5");
        SetConstant(conditions[3], "speed", "12");
        SetConstant(conditions[6], "r", "0.25");
        SetConstant(conditions[6], "g", "0.5");
        SetConstant(conditions[6], "b", "0.75");

        var connections = new List<GraphConnection>
        {
            Flow("CONN_Start_WorldMarkerCondition0", start.Id, GraphPortDefaults.FlowOut, conditions[0].Id, GraphPortDefaults.FlowIn)
        };
        for (var index = 0; index < conditions.Count; index++)
        {
            var toNodeId = index + 1 < conditions.Count ? conditions[index + 1].Id : message.Id;
            connections.Add(Flow($"CONN_WorldMarkerCondition_{index}", conditions[index].Id, GraphPortDefaults.TrueOut, toNodeId, GraphPortDefaults.FlowIn));
        }

        var rule = new Rule
        {
            Id = "RULE_WorldMarkerConditions",
            Name = "World Marker Conditions",
            Nodes = [start, .. conditions, message],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "WorldMarkerConditionsGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local currentNumber = tonumber(targetObject.Length) or 0", luau);
        Assert.Contains("local expectedNumber = tonumber(7.5) or 0", luau);
        Assert.Contains("return (targetObject.AppearOnTop == true) == true", luau);
        Assert.Contains("return (targetObject.VisibleInDev == true) == true", luau);
        Assert.Contains("local currentNumber = tonumber(targetObject.ClimbSpeed) or 0", luau);
        Assert.Contains("return (targetObject.CastShadows == true) == true", luau);
        Assert.Contains("return (targetObject.IsSpawn == true) == true", luau);
        Assert.Contains("local expectedColor = Color.New(0.25, 0.5, 0.75, 1)", luau);
        Assert.Contains("return targetObject.Color == expectedColor", luau);
        Assert.DoesNotContain("Marker Length At Least is not implemented", luau);
        Assert.DoesNotContain("Entity Color Is is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsWorldMarkerTrussAndEntityWatcherTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var triggerIds = new[]
        {
            "EV_OnMarkerLengthReached",
            "EV_OnMarkerAppearsOnTopEnabled",
            "EV_OnMarkerVisibleInDevEnabled",
            "EV_OnTrussClimbSpeedReached",
            "EV_OnEntityStartedCastingShadows",
            "EV_OnEntityBecameSpawn",
            "EV_OnEntityColorChanged"
        };
        var triggers = triggerIds
            .Select((id, index) => NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == id), stableId: $"TRG_WorldMarkerWatcher_{index}"))
            .ToList();
        var message = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage"), stableId: "ACT_WorldMarkerWatcherMessage");

        foreach (var node in triggers.Take(3))
        {
            SetSceneObject(node, "target", "World/Environment/Quest Marker");
        }

        SetSceneObject(triggers[3], "target", "World/Environment/Ladder");
        foreach (var node in triggers.Skip(4))
        {
            SetSceneObject(node, "target", "World/Environment/Spawn Block");
        }

        SetConstant(triggers[0], "length", "7.5");
        SetConstant(triggers[3], "speed", "12");

        var connections = triggers
            .Select((trigger, index) => Flow($"CONN_WorldMarkerWatcher_{index}", trigger.Id, GraphPortDefaults.FlowOut, message.Id, GraphPortDefaults.FlowIn))
            .ToList();

        var rule = new Rule
        {
            Id = "RULE_WorldMarkerWatchers",
            Name = "World Marker Watchers",
            Nodes = [.. triggers, message],
            Connections = connections
        };
        var graph = new RuleGraph { Name = "WorldMarkerWatchersGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local watchedLimit = tonumber(7.5) or 10", luau);
        Assert.Contains("return tonumber(triggerObject.Length) or 0", luau);
        Assert.Contains("return triggerObject.AppearOnTop == true", luau);
        Assert.Contains("return triggerObject.VisibleInDev == true", luau);
        Assert.Contains("local watchedLimit = tonumber(12) or 12", luau);
        Assert.Contains("return tonumber(triggerObject.ClimbSpeed) or 0", luau);
        Assert.Contains("return triggerObject.CastShadows == true", luau);
        Assert.Contains("return triggerObject.IsSpawn == true", luau);
        Assert.Contains("return triggerObject.Color", luau);
        Assert.Contains("local previousMatched = readMatched() == true", luau);
        Assert.Contains("local previousValue = readWatchedValue()", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, markerLength = currentValue }", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, entityColor = currentValue }", luau);
        Assert.DoesNotContain("On Marker Length Reached is not implemented", luau);
        Assert.DoesNotContain("On Entity Color Changed is not implemented", luau);
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

    private static GraphConnection Value(string id, string fromNodeId, string fromPortId, string toNodeId, string toPortId)
    {
        return new GraphConnection
        {
            Id = id,
            From = new GraphEndpoint { NodeId = fromNodeId, PortId = fromPortId },
            To = new GraphEndpoint { NodeId = toNodeId, PortId = toPortId },
            ConnectionKind = GraphConnectionKind.Value
        };
    }

    private static void SetConstant(RuleNode node, string parameterKey, string value)
    {
        var parameter = node.Parameters.Single(item => item.Key == parameterKey);
        parameter.Value = value;
        parameter.Binding.SourceKind = GraphValueSourceKind.Constant;
        parameter.Binding.ConstantValue = value;
        parameter.Binding.DisplayText = value;
    }

    private static void SetSceneObject(RuleNode node, string parameterKey, string path)
    {
        var parameter = node.Parameters.Single(item => item.Key == parameterKey);
        parameter.Value = path;
        parameter.Binding.SourceKind = GraphValueSourceKind.SceneObject;
        parameter.Binding.SceneObjectPath = path;
        parameter.Binding.DisplayText = path;
    }

    private static void SetCatalogRecipe(
        RuleNode node,
        string parameterKey,
        NodeCatalogEntry recipeEntry,
        IEnumerable<RuleParameter> recipeParameters)
    {
        var parameter = node.Parameters.Single(item => item.Key == parameterKey);
        parameter.Value = recipeEntry.IdBase;
        parameter.SourceCatalogId = recipeEntry.IdBase;
        parameter.Binding.SourceKind = GraphValueSourceKind.CatalogValue;
        parameter.Binding.CatalogId = recipeEntry.IdBase;
        parameter.Binding.CatalogType = recipeEntry.Type;
        parameter.Binding.CatalogParameters = recipeParameters.ToList();
        parameter.Binding.DisplayText = recipeEntry.Label;
    }

    private static int CountOccurrences(string text, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static void PrimeRecipeParameters(RuleNode recipe)
    {
        switch (recipe.Type)
        {
            case "SubtractNumbers":
                SetConstant(recipe, "left", "9");
                SetConstant(recipe, "right", "4");
                break;
            case "DivideNumbers":
                SetConstant(recipe, "numerator", "9");
                SetConstant(recipe, "divisor", "0");
                break;
            case "AverageNumber":
                SetConstant(recipe, "first", "2");
                SetConstant(recipe, "second", "8");
                break;
            case "SquareRootNumber":
                SetConstant(recipe, "value", "16");
                break;
            case "RoundNumber":
                SetConstant(recipe, "value", "4.6");
                break;
            case "TextLength":
                SetConstant(recipe, "text", "Hello");
                break;
            case "NumberToText":
                SetConstant(recipe, "value", "42");
                break;
            case "ReadState":
                SetConstant(recipe, "state", "DoorOpen");
                break;
            case "RGBColor":
                SetConstant(recipe, "r", "0.25");
                SetConstant(recipe, "g", "0.5");
                SetConstant(recipe, "b", "0.75");
                break;
            case "ObjectName":
            case "ObjectTypeName":
            case "ObjectNetworkKey":
            case "ObjectSaveKey":
            case "ObjectIsNetworkedValue":
                SetConstant(recipe, "target", "Self");
                break;
            case "ObjectXPosition":
            case "ObjectHeightPosition":
            case "ObjectZPosition":
            case "ObjectTurnAngle":
            case "ObjectWidthSize":
            case "ObjectHeightSize":
            case "ObjectDepthSize":
                SetConstant(recipe, "target", "Self");
                break;
            case "ModuloNumbers":
                SetConstant(recipe, "value", "10");
                SetConstant(recipe, "divisor", "0");
                break;
            case "TeamScore":
                SetConstant(recipe, "teamName", "Blue");
                break;
            case "DistanceBetweenObjects":
                SetConstant(recipe, "from", "Self");
                SetConstant(recipe, "to", "World/Target");
                break;
            case "DistanceBetweenPositions":
                SetConstant(recipe, "first", "0,0,0");
                SetConstant(recipe, "second", "3,4,0");
                break;
            case "PercentNumber":
                SetConstant(recipe, "part", "25");
                SetConstant(recipe, "whole", "50");
                break;
            case "MapNumberRange":
                SetConstant(recipe, "value", "5");
                SetConstant(recipe, "inMin", "0");
                SetConstant(recipe, "inMax", "10");
                SetConstant(recipe, "outMin", "0");
                SetConstant(recipe, "outMax", "100");
                break;
            case "ChooseNumber":
                SetConstant(recipe, "value", "7");
                break;
            case "ChooseText":
                SetConstant(recipe, "value", "Hello player");
                break;
            case "ChooseObject":
                SetConstant(recipe, "target", "World/Door");
                break;
            case "RandomTrueOrFalse":
                SetConstant(recipe, "trueChance", "25");
                break;
            case "RandomWholeNumber":
                SetConstant(recipe, "min", "1");
                SetConstant(recipe, "max", "6");
                break;
            case "RandomNumberChoice":
                SetConstant(recipe, "first", "10");
                SetConstant(recipe, "second", "20");
                SetConstant(recipe, "firstChance", "40");
                break;
            case "RandomTextChoice":
                SetConstant(recipe, "first", "Red");
                SetConstant(recipe, "second", "Blue");
                SetConstant(recipe, "firstChance", "60");
                break;
            case "NumberFromText":
                SetConstant(recipe, "text", "42");
                SetConstant(recipe, "fallback", "-1");
                break;
            case "TextBefore":
                SetConstant(recipe, "text", "hello:world");
                SetConstant(recipe, "marker", ":");
                break;
            case "TextAfter":
                SetConstant(recipe, "text", "hello:world");
                SetConstant(recipe, "marker", ":");
                break;
            case "TextBetween":
                SetConstant(recipe, "text", "[red]");
                SetConstant(recipe, "startMarker", "[");
                SetConstant(recipe, "endMarker", "]");
                break;
            case "FirstTextCharacters":
                SetConstant(recipe, "text", "Hello");
                SetConstant(recipe, "count", "3");
                break;
            case "LastTextCharacters":
                SetConstant(recipe, "text", "Hello");
                SetConstant(recipe, "count", "2");
                break;
        }
    }

    private static void AssertOccursInOrder(string text, params string[] markers)
    {
        var searchStart = 0;
        foreach (var marker in markers)
        {
            var index = text.IndexOf(marker, searchStart, StringComparison.Ordinal);
            Assert.True(index >= 0, $"Expected marker after index {searchStart}: {marker}");
            searchStart = index + marker.Length;
        }
    }
}
