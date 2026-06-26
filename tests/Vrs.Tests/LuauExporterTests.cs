using Vrs.Core.Catalog;
using Vrs.Core.Export;
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
        Assert.Contains("local inputEvent = vrsResolveInputNetworkEvent(inputActionName)", luau);
        Assert.Contains("local inputMessage = NetMessage:New()", luau);
        Assert.Contains("inputEvent:InvokeServer(inputMessage)", luau);
        Assert.Contains("Run VRS Input Manager first", luau);
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

    [Theory]
    [InlineData("EV_OnStart", "local triggerContext = { object = triggerObject }")]
    [InlineData("EV_OnTimerTick", "local triggerContext = { object = triggerObject }")]
    [InlineData("EV_OnPlayerJoined", "local triggerContext = { object = triggerObject, player = player }")]
    [InlineData("EV_OnPlayerLeft", "local triggerContext = { object = triggerObject, player = player }")]
    [InlineData("EV_OnChatMessage", "local triggerContext = { object = triggerObject, player = sender, message = message }")]
    [InlineData("EV_OnInputButtonDown", "local triggerContext = { object = triggerObject, inputAction = inputActionName, inputValue = true }")]
    [InlineData("EV_OnVrsInputEvent", "local triggerContext = { object = triggerObject, player = player, inputAction = inputActionName, inputMessage = inputMessage, message = inputMessage }")]
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
        var pause = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PauseSound"), stableId: "ACT_PauseSound");
        var stop = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_StopSound"), stableId: "ACT_StopSound");
        var printVolume = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintVolume");
        var printPlaying = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "ACT_PrintValue"), stableId: "ACT_PrintPlaying");
        var soundVolume = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_SoundVolume"), stableId: "PROP_SoundVolume");
        var soundPlaying = NodeCatalogService.CreateNode(catalog.Nodes.Single(node => node.IdBase == "PROP_SoundIsPlaying"), stableId: "PROP_SoundPlaying");

        foreach (var node in new[] { play, once, volume, loop, pause, stop, soundVolume, soundPlaying })
        {
            SetSceneObject(node, "target", "World/Audio/Theme");
        }

        SetConstant(once, "volume", "0.75");
        SetConstant(volume, "volume", "0.5");
        SetConstant(loop, "enabled", "true");

        var rule = new Rule
        {
            Id = "RULE_Sound",
            Name = "Sound",
            Nodes = [start, play, once, volume, loop, pause, stop, printVolume, printPlaying, soundVolume, soundPlaying],
            Connections =
            [
                Flow("CONN_Start_Play", start.Id, GraphPortDefaults.FlowOut, play.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Play_Once", play.Id, GraphPortDefaults.FlowOut, once.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Once_Volume", once.Id, GraphPortDefaults.FlowOut, volume.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Volume_Loop", volume.Id, GraphPortDefaults.FlowOut, loop.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Loop_Pause", loop.Id, GraphPortDefaults.FlowOut, pause.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Pause_Stop", pause.Id, GraphPortDefaults.FlowOut, stop.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Stop_PrintVolume", stop.Id, GraphPortDefaults.FlowOut, printVolume.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_PrintVolume_PrintPlaying", printVolume.Id, GraphPortDefaults.FlowOut, printPlaying.Id, GraphPortDefaults.FlowIn),
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
        Assert.Contains("soundObject:Pause()", luau);
        Assert.Contains("soundObject:Stop()", luau);
        Assert.Contains("return tonumber(targetObject.Volume) or 0", luau);
        Assert.Contains("return targetObject.Playing == true", luau);
        Assert.DoesNotContain("Play Sound is not implemented", luau);
        Assert.DoesNotContain("Set Sound Volume is not implemented", luau);
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
            "PROP_GameTeamCount"
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
        Assert.Contains("Teams:GetTeams()", luau);
        Assert.Contains("return #teams", luau);
        Assert.DoesNotContain("Game Team Name is not implemented", luau);
        Assert.DoesNotContain("Game Team Player Count is not implemented", luau);
    }

    [Fact]
    public void ExportRuleToLuau_EmitsTweenApiActions()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var start = catalog.Nodes.Single(node => node.IdBase == "EV_OnStart");
        var tweenPosition = catalog.Nodes.Single(node => node.IdBase == "ACT_TweenObjectPosition");
        var tweenColor = catalog.Nodes.Single(node => node.IdBase == "ACT_TweenObjectColor");
        var trigger = NodeCatalogService.CreateNode(start, 100, 100, "TRG_Start");
        var position = NodeCatalogService.CreateNode(tweenPosition, 320, 100, "ACT_TweenPosition");
        var color = NodeCatalogService.CreateNode(tweenColor, 540, 100, "ACT_TweenColor");
        SetConstant(position, "duration", "2");
        SetConstant(position, "vector", "1,2,3");
        SetConstant(color, "r", "0.25");
        SetConstant(color, "g", "0.5");
        SetConstant(color, "b", "0.75");

        var rule = new Rule
        {
            Id = "RULE_Tweens",
            Name = "Tweens",
            Nodes = [trigger, position, color],
            Connections =
            [
                Flow("CONN_Start_Position", trigger.Id, GraphPortDefaults.FlowOut, position.Id, GraphPortDefaults.FlowIn),
                Flow("CONN_Position_Color", position.Id, GraphPortDefaults.FlowOut, color.Id, GraphPortDefaults.FlowIn)
            ]
        };
        var graph = new RuleGraph { Name = "TweenGraph", Rules = [rule] };

        var luau = new LuauExporter().ExportRuleToLuau(rule, graph, catalog.Nodes);

        Assert.Contains("local tween = Tween:NewTween()", luau);
        Assert.Contains("local endValue = makeVector3(1, 2, 3)", luau);
        Assert.Contains("tween:TweenPosition(targetObject, endValue, 2)", luau);
        Assert.Contains("local endColor = Color.New(0.25, 0.5, 0.75, 1)", luau);
        Assert.Contains("tween:TweenColor(targetObject.Color, endColor, 1, function(color)", luau);
        Assert.DoesNotContain("Tween Object Position is not implemented", luau);
        Assert.DoesNotContain("Tween Object Color is not implemented", luau);
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
            "ACT_SetObjectSpinVelocity"
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
            "PROP_TouchingObjectCount"
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
        Assert.Contains("local currentSpeed = vrsDistanceBetweenPositions(targetObject.Velocity, makeVector3(0, 0, 0))", luau);
        Assert.Contains("return currentSpeed >= 0.5", luau);
        Assert.Contains("return currentSpeed >= 10", luau);
        Assert.Contains("return targetObject.Velocity", luau);
        Assert.Contains("Object Speed stopped", luau);
        Assert.Contains("return targetObject.AngularVelocity", luau);
        Assert.Contains("local touchingObjects = targetObject:GetTouching()", luau);
        Assert.Contains("return #touchingObjects", luau);
        Assert.Contains("listenObject.TouchEnded:Connect(function(hit)", luau);
        Assert.Contains("local triggerContext = { object = triggerObject, touchObject = hit, touchObjectSource = triggerObject }", luau);
        Assert.Contains("listenObject.MouseEnter:Connect(function()", luau);
        Assert.Contains("listenObject.MouseExit:Connect(function()", luau);
        Assert.Contains("touch ended", luau);
        Assert.Contains("hover started", luau);
        Assert.Contains("hover ended", luau);
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
