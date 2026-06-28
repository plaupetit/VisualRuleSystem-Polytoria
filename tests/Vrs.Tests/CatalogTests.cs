using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.Tests;

public sealed class CatalogTests
{
    [Fact]
    public void LoadCatalog_LoadsTimerAndMessagePackages()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);

        Assert.Contains(catalog.Nodes, node => node.IdBase == "EV_OnStart");
        Assert.Contains(catalog.Nodes, node => node.IdBase == "EV_OnTimerTick");
        Assert.Contains(catalog.Nodes, node => node.IdBase == "ACT_ShowMessage");
        Assert.Contains(catalog.Nodes, node => node.IdBase == "ACT_WaitSeconds");
        Assert.Contains(catalog.Nodes, node => node.IdBase == "ACT_MoveObjectOverTime");
        Assert.Contains(catalog.Nodes, node => node.IdBase == "COND_NumberCompare");
        Assert.Contains(catalog.Nodes, node => node.IdBase == "PROP_AddNumbers");
        Assert.Contains(catalog.Nodes, node => node.IdBase == "PROP_JoinText");
        Assert.Contains(catalog.Nodes, node => node.IdBase == "PROP_ReadScriptVariable");
        Assert.Contains(catalog.Nodes, node => node.IdBase == "COND_TextContains");
        Assert.Contains(catalog.Nodes, node => node.IdBase == "ACT_IncrementScriptNumber");
        Assert.Contains(catalog.Nodes, node => node.IdBase == "EV_RunLuauTrigger");
        Assert.Contains(catalog.Nodes, node => node.IdBase == "ACT_RunLuauAction");
        Assert.Contains(catalog.Nodes, node => node.IdBase == "COND_RunLuauCondition");
        Assert.Contains(catalog.Nodes, node => node.IdBase == "PROP_RunLuauProperty");
        Assert.Empty(catalog.Warnings);
    }

    [Fact]
    public void LoadCatalog_ValueNodesAreAddableForAdvancedPins()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var manualText = catalog.Nodes.Single(node => node.IdBase == "PROP_ManualText");
        var addNumbers = catalog.Nodes.Single(node => node.IdBase == "PROP_AddNumbers");

        Assert.True(NodeCatalogService.IsAddable(manualText));
        Assert.True(NodeCatalogService.IsAddable(addNumbers));
    }

    [Fact]
    public void CreateNode_AddsTypedValuePortsForPrimitiveParameters()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");
        var addNumbers = catalog.Nodes.Single(node => node.IdBase == "PROP_AddNumbers");

        var action = NodeCatalogService.CreateNode(showMessage);
        var property = NodeCatalogService.CreateNode(addNumbers);

        Assert.Contains(action.Ports, port =>
            port.Id == "param_message" &&
            port.PortKind == NodePortKind.Value &&
            port.DataType == "String");
        Assert.Contains(property.Ports, port =>
            port.Id == "valueOut" &&
            port.Direction == NodePortDirection.Output &&
            port.DataType == "Number");
        Assert.Contains(property.Ports, port =>
            port.Id == "param_left" &&
            port.Direction == NodePortDirection.Input &&
            port.DataType == "Number");
    }

    [Fact]
    public void RunLuauCatalog_ExposesAdvancedNodesWithoutCodeValuePorts()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var runTrigger = catalog.Nodes.Single(node => node.IdBase == "EV_RunLuauTrigger");
        var runAction = catalog.Nodes.Single(node => node.IdBase == "ACT_RunLuauAction");
        var runCondition = catalog.Nodes.Single(node => node.IdBase == "COND_RunLuauCondition");
        var runProperty = catalog.Nodes.Single(node => node.IdBase == "PROP_RunLuauProperty");

        Assert.All(new[] { runTrigger, runAction, runCondition, runProperty }, node =>
        {
            Assert.Equal("Ready", node.Status);
            Assert.Equal("Advanced", node.Surface);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.Contains("luau", node.SearchKeywords);
            Assert.Contains(node.Parameters, parameter => parameter.Key == "code" && parameter.Control == "Code");
        });

        Assert.Contains("target=Self", runTrigger.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Object Path", runTrigger.Parameters.Single(parameter => parameter.Key == "target").Type);

        var action = NodeCatalogService.CreateNode(runAction);
        var condition = NodeCatalogService.CreateNode(runCondition);
        var property = NodeCatalogService.CreateNode(runProperty);

        Assert.DoesNotContain(action.Ports, port => port.Id == GraphPortDefaults.ParameterPortId("code"));
        Assert.DoesNotContain(condition.Ports, port => port.Id == GraphPortDefaults.ParameterPortId("code"));
        Assert.DoesNotContain(property.Ports, port => port.Id == GraphPortDefaults.ParameterPortId("code"));
        Assert.DoesNotContain(property.Ports, port => port.Id == GraphPortDefaults.ParameterPortId("resultType"));
        Assert.Contains(property.Ports, port =>
            port.Id == GraphPortDefaults.ValueOut &&
            port.Direction == NodePortDirection.Output &&
            port.DataType == "Any");
    }

    [Fact]
    public void LoadCatalog_ReadsGc2InspiredMetadataWithoutRebuild()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var showMessage = catalog.Nodes.Single(node => node.IdBase == "ACT_ShowMessage");

        Assert.Equal("core.debug", showMessage.ModuleId);
        Assert.Contains("print", showMessage.SearchKeywords);
        Assert.Contains("${message}", showMessage.PreviewTemplate);
        Assert.NotEmpty(showMessage.SelectorHints);
        Assert.NotEmpty(showMessage.DebugHints);
    }

    [Fact]
    public void TriggerCatalog_DeclaresTargetParameterForEveryTrigger()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var triggers = catalog.Nodes.Where(node => node.Kind == NodeKind.Trigger).ToList();

        Assert.NotEmpty(triggers);
        Assert.Equal(110, triggers.Count);
        foreach (var trigger in triggers)
        {
            Assert.Contains("target=Self", trigger.Value, StringComparison.OrdinalIgnoreCase);
            var target = Assert.Single(trigger.Parameters, parameter => parameter.Key.Equals("target", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("Object Path", target.Type);
            Assert.Equal("SceneObject", target.Control);
            Assert.True(target.Required);
            Assert.Equal("Self", target.Default);
            Assert.Equal("Target Context", target.ValueSource);

            var hint = Assert.Single(trigger.SelectorHints, item => item.Key.Equals("target", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("SceneObject", hint.DataType);
            Assert.Contains(GraphValueSourceKind.Self, hint.AllowedSources);
            Assert.Contains(GraphValueSourceKind.Target, hint.AllowedSources);
            Assert.Contains(GraphValueSourceKind.SceneObject, hint.AllowedSources);
            Assert.Contains(GraphVariableScope.Script, hint.AllowedScopes);
            Assert.Contains(GraphVariableScope.State, hint.AllowedScopes);
            Assert.Contains(GraphVariableScope.Graph, hint.AllowedScopes);
        }
    }

    [Fact]
    public void BackfillMissingParameters_AppendsCatalogDefaultsWithoutOverwritingAuthoredValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var inputTrigger = catalog.Nodes.Single(node => node.IdBase == "EV_OnInputButtonDown");
        var oldNode = new RuleNode
        {
            Kind = NodeKind.Trigger,
            Id = "TRG_Input",
            Type = inputTrigger.Type,
            Label = inputTrigger.Label,
            CatalogId = inputTrigger.IdBase,
            Parameters =
            [
                new RuleParameter
                {
                    Key = "actionName",
                    Value = "Dash",
                    Binding = new GraphValueBinding
                    {
                        SourceKind = GraphValueSourceKind.Constant,
                        ConstantValue = "Dash",
                        DisplayText = "Dash"
                    }
                }
            ]
        };

        var changed = NodeCatalogService.BackfillMissingParameters(oldNode, inputTrigger);

        Assert.True(changed);
        Assert.Equal("Dash", oldNode.Parameters.Single(parameter => parameter.Key == "actionName").Value);
        var target = oldNode.Parameters.Single(parameter => parameter.Key == "target");
        Assert.Equal("Self", target.Value);
        Assert.Equal(GraphValueSourceKind.Self, target.Binding.SourceKind);
    }

    [Fact]
    public void CreateNode_KillPlayerDefaultsPlayerToTriggeringPlayer()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var killPlayer = catalog.Nodes.Single(node => node.IdBase == "ACT_KillPlayer");

        var node = NodeCatalogService.CreateNode(killPlayer);
        var player = node.Parameters.Single(parameter => parameter.Key == "player");

        Assert.Equal(GraphValueSourceKind.TriggeringPlayer, player.Binding.SourceKind);
        Assert.Equal("Triggering Player", player.Value);
        Assert.Equal("Trigger Context", player.ValueSource);
        Assert.Equal("", player.Binding.ConstantValue);
        Assert.Equal("Triggering Player", player.Binding.DisplayText);
    }

    [Fact]
    public void CatalogIndex_SearchesKeywordsAndLazyReloads()
    {
        var index = new CatalogIndexService();
        var catalog = index.GetCatalog(TestPaths.CatalogRoot);

        var results = index.Search(catalog.Nodes, "console");

        Assert.Contains(results, node => node.IdBase == "ACT_ShowMessage");
        Assert.Contains(index.Search(catalog.Nodes, "Show Message"), node => node.IdBase == "ACT_ShowMessage");
        Assert.Contains(index.Search(catalog.Nodes, "Timing"), node => node.IdBase == "EV_OnTimerTick");
        Assert.Contains(index.Search(catalog.Nodes, "startup"), node => node.IdBase == "EV_OnStart");
        Assert.Same(catalog, index.GetCatalog(TestPaths.CatalogRoot));
    }

    [Fact]
    public void CatalogSearch_UsesBeginnerIntentDomainRuntimeAndParameterText()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);

        Assert.Contains(catalog.Nodes.Where(node => NodeCatalogService.Matches(node, "when")), node => node.IdBase == "EV_OnStart");
        Assert.Contains(catalog.Nodes.Where(node => NodeCatalogService.Matches(node, "value")), node => node.IdBase == "PROP_AddNumbers");
        Assert.Contains(catalog.Nodes.Where(node => NodeCatalogService.Matches(node, "server")), node => node.IdBase == "EV_OnTimerTick");
        Assert.Contains(catalog.Nodes.Where(node => NodeCatalogService.Matches(node, "Polytoria output")), node => node.IdBase == "ACT_ShowMessage");
        Assert.Contains(catalog.Nodes.Where(node => NodeCatalogService.Matches(node, "script parent")), node => node.IdBase == "EV_OnTimerTick");
    }

    [Theory]
    [InlineData("kil player", "ACT_KillPlayer")]
    [InlineData("kll player", "ACT_KillPlayer")]
    [InlineData("death", "ACT_KillPlayer")]
    [InlineData("die", "ACT_KillPlayer")]
    [InlineData("respawn", "ACT_RespawnPlayer")]
    public void CatalogSearch_RanksPlayerHazardVocabulary(string search, string expectedId)
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var topIds = RankedCatalogMatches(catalog, search)
            .Take(8)
            .Select(item => item.Entry.IdBase)
            .ToList();

        Assert.Contains(expectedId, topIds);
    }

    [Theory]
    [InlineData("move")]
    [InlineData("translate")]
    public void CatalogSearch_RanksMovementActionsForMovementVocabulary(string search)
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var topIds = RankedCatalogMatches(catalog, search)
            .Take(8)
            .Select(item => item.Entry.IdBase)
            .ToList();

        Assert.Contains(topIds, id =>
            id.StartsWith("ACT_Move", StringComparison.OrdinalIgnoreCase) ||
            (id.StartsWith("ACT_", StringComparison.OrdinalIgnoreCase) && id.Contains("Position", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void CatalogSearch_FindsMovementMetadataForIdleVocabulary()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var topIds = RankedCatalogMatches(catalog, "idle")
            .Take(12)
            .Select(item => item.Entry.IdBase)
            .ToList();

        Assert.Contains(topIds, id => id is "COND_ObjectIsMoving" or "EV_OnObjectStoppedMoving");
    }

    [Fact]
    public void CatalogSearch_NormalizesCaseAccentsAndPunctuation()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var top = RankedCatalogMatches(catalog, "KÍLL-player").First();

        Assert.Equal("ACT_KillPlayer", top.Entry.IdBase);
        Assert.NotEmpty(top.Search.MatchSummary);
    }

    [Theory]
    [InlineData("do")]
    [InlineData("is")]
    [InlineData("a")]
    public void CatalogSearch_DoesNotFuzzyMatchShortNoiseWords(string search)
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);

        Assert.Empty(RankedCatalogMatches(catalog, search));
    }

    [Fact]
    public void CoreSceneV1_UserFacingReadyNodesHaveBeginnerMetadata()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedReadyIds = new[]
        {
            "ACT_ShowWarning",
            "ACT_ToggleState",
            "ACT_ClearScriptVariable",
            "ACT_SetObjectVisible",
            "ACT_SetObjectName",
            "ACT_DestroyObject",
            "ACT_MoveObject",
            "ACT_RotateObject",
            "ACT_SetObjectScale",
            "COND_StateIsTrue",
            "COND_TextStartsWith",
            "COND_TextEndsWith",
            "COND_ScriptVariableExists",
            "COND_ScriptNumberIsAtLeast",
            "COND_ObjectIsNamed",
            "COND_ObjectIsType",
            "COND_ObjectIsVisible",
            "PROP_SubtractNumbers",
            "PROP_DivideNumbers",
            "PROP_RoundNumber",
            "PROP_TextLength",
            "PROP_NumberToText",
            "PROP_ObjectName",
            "PROP_ObjectPosition",
            "PROP_ObjectColor",
            "PROP_ObjectParent",
            "PROP_ReadState",
            "PROP_RGBColor",
            "PROP_RandomColor"
        };

        foreach (var id in expectedReadyIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.PalettePath);
            Assert.NotEmpty(node.SearchKeywords);
        }
    }

    [Fact]
    public void CoreExtraPack_LoadsManyReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedExtraIds = new[]
        {
            "ACT_PrintValue",
            "ACT_DecrementScriptNumber",
            "ACT_MultiplyScriptNumber",
            "ACT_AppendScriptText",
            "ACT_SetObjectTransparency",
            "ACT_SetObjectAnchored",
            "ACT_SetObjectCanCollide",
            "COND_TextEquals",
            "COND_NumberIsEven",
            "COND_NumberIsPositive",
            "COND_RandomChance",
            "COND_ScriptTextEquals",
            "COND_ScriptBooleanIsTrue",
            "COND_ObjectTransparencyAtLeast",
            "COND_ObjectHasParent",
            "PROP_MinNumber",
            "PROP_MaxNumber",
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

        Assert.True(catalog.Nodes.Count >= 94);
        foreach (var id in expectedExtraIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.PalettePath);
            Assert.NotEmpty(node.SearchKeywords);
        }
    }

    [Fact]
    public void GameplayApiPack_LoadsFirstRunnableBatch()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedGameplayIds = new[]
        {
            "EV_OnPlayerJoined",
            "EV_OnPlayerLeft",
            "EV_OnChatMessage",
            "EV_OnInputButtonDown",
            "ACT_BroadcastChatMessage",
            "ACT_SendChatMessageToPlayer",
            "ACT_SetWalkSpeed",
            "ACT_SetJumpPower",
            "ACT_SetSprintSpeed",
            "ACT_SetMaxHealth",
            "ACT_SetRespawnTime",
            "ACT_SetStamina",
            "COND_WalkSpeedAtLeast",
            "COND_JumpPowerAtLeast",
            "COND_SprintSpeedAtLeast",
            "COND_MaxHealthAtLeast",
            "COND_RespawnTimeAtLeast",
            "COND_StaminaAtLeast",
            "EV_OnWalkSpeedReached",
            "EV_OnJumpPowerReached",
            "EV_OnSprintSpeedReached",
            "EV_OnMaxHealthReached",
            "EV_OnRespawnTimeReached",
            "EV_OnStaminaReached",
            "ACT_BindInputButtonKey",
            "ACT_SaveDatastoreValue",
            "ACT_RemoveDatastoreValue",
            "ACT_TweenObjectPosition",
            "ACT_TweenObjectRotation",
            "ACT_TweenObjectScale",
            "ACT_TweenObjectColor",
            "ACT_TweenObjectTransparency",
            "COND_TweenPositionReached",
            "COND_TweenRotationReached",
            "COND_TweenScaleReached",
            "COND_TweenColorReached",
            "COND_TweenTransparencyReached",
            "EV_OnTweenPositionReached",
            "EV_OnTweenRotationReached",
            "EV_OnTweenScaleReached",
            "EV_OnTweenColorReached",
            "EV_OnTweenTransparencyReached",
            "COND_PlayerExists",
            "COND_PlayerCountAtLeast",
            "COND_InputButtonDown",
            "COND_InputActionExists",
            "COND_ObjectHasTag",
            "COND_ObjectIsA",
            "COND_ObjectHasChild",
            "COND_ObjectHasChildClass",
            "PROP_TriggeringPlayer",
            "PROP_LocalPlayer",
            "PROP_PlayerCount",
            "PROP_FindPlayerByName",
            "PROP_FindPlayerByID",
            "PROP_TriggeringChatMessage",
            "PROP_TriggeringChatPlayer",
            "PROP_TriggeringInputAction",
            "PROP_TriggeringInputValue",
            "PROP_InputAxisValue",
            "PROP_InputVectorX",
            "PROP_InputVectorY",
            "PROP_InputButtonFromKey",
            "PROP_DatastoreValue",
            "PROP_DatastoreKey",
            "PROP_WalkSpeedValue",
            "PROP_JumpPowerValue",
            "PROP_SprintSpeedValue",
            "PROP_MaxHealthValue",
            "PROP_RespawnTimeValue",
            "PROP_StaminaValue",
            "PROP_FindChild",
            "PROP_FindChildByClass",
            "PROP_ObjectChildCount"
        };

        foreach (var id in expectedGameplayIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.PalettePath);
            Assert.NotEmpty(node.SearchKeywords);
        }
    }

    [Fact]
    public void InputNetworkEventNodes_LoadAsClientAndServerWorkflow()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var sendInput = catalog.Nodes.Single(node => node.IdBase == "ACT_SendInputEvent");
        var sendInputText = catalog.Nodes.Single(node => node.IdBase == "ACT_SendInputTextEvent");
        var receiveInput = catalog.Nodes.Single(node => node.IdBase == "EV_OnVrsInputEvent");
        var triggeringInputText = catalog.Nodes.Single(node => node.IdBase == "PROP_TriggeringInputText");

        Assert.Equal("Ready", sendInput.Status);
        Assert.Equal("Client", sendInput.RuntimeFamily);
        Assert.Equal("UserFacing", sendInput.Surface);
        Assert.True(NodeCatalogService.IsAddable(sendInput));
        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(sendInput, GraphScriptKind.Local));
        Assert.False(NodeCatalogService.IsCompatibleWithScriptKind(sendInput, GraphScriptKind.Server));
        Assert.NotEmpty(sendInput.ApiReferences);
        Assert.Contains(sendInput.ApiReferences, reference => reference.Type == "Hidden" && reference.Coverage == "Indirect");

        Assert.Equal("Ready", sendInputText.Status);
        Assert.Equal("Client", sendInputText.RuntimeFamily);
        Assert.Equal("UserFacing", sendInputText.Surface);
        Assert.True(NodeCatalogService.IsAddable(sendInputText));
        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(sendInputText, GraphScriptKind.Local));
        Assert.False(NodeCatalogService.IsCompatibleWithScriptKind(sendInputText, GraphScriptKind.Server));
        Assert.Contains(sendInputText.Parameters, parameter => parameter.Key == "payload" && parameter.Type == "String");
        Assert.Contains(sendInputText.ApiReferences, reference => reference.Type == "NetMessage" && reference.Member == "AddString");
        Assert.Contains(sendInputText.ApiReferences, reference => reference.Type == "HiddenBase" && reference.Coverage == "Indirect");

        Assert.Equal("Ready", receiveInput.Status);
        Assert.Equal("Server", receiveInput.RuntimeFamily);
        Assert.Equal("UserFacing", receiveInput.Surface);
        Assert.True(NodeCatalogService.IsAddable(receiveInput));
        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(receiveInput, GraphScriptKind.Server));
        Assert.False(NodeCatalogService.IsCompatibleWithScriptKind(receiveInput, GraphScriptKind.Local));
        Assert.Contains(receiveInput.Parameters, parameter => parameter.Key == "inputAction" && parameter.Control == "Choice");
        Assert.Contains(receiveInput.ApiReferences, reference => reference.Type == "Hidden" && reference.Coverage == "Indirect");

        Assert.Equal("Ready", triggeringInputText.Status);
        Assert.Equal("Server", triggeringInputText.RuntimeFamily);
        Assert.Equal("UserFacing", triggeringInputText.Surface);
        Assert.True(NodeCatalogService.IsAddable(triggeringInputText));
        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(triggeringInputText, GraphScriptKind.Server));
        Assert.False(NodeCatalogService.IsCompatibleWithScriptKind(triggeringInputText, GraphScriptKind.Local));
        Assert.Contains(triggeringInputText.ApiReferences, reference => reference.Type == "NetMessage" && reference.Member == "GetString");
    }

    [Fact]
    public void NetworkedObjectValueNodes_LoadReadyAnnotatedGameplayProperties()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedMembers = new Dictionary<string, string>
        {
            ["PROP_ObjectTypeName"] = "ClassName",
            ["PROP_ObjectNetworkKey"] = "NetworkedObjectID",
            ["PROP_ObjectSaveKey"] = "ObjectID",
            ["PROP_ObjectIsNetworked"] = "ExistInNetwork"
        };

        foreach (var (id, member) in expectedMembers)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.Equal(["Scene Object", "Network"], node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Server));
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Local));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
            Assert.Contains(node.ApiReferences, reference =>
                reference.Type == "NetworkedObject" &&
                reference.MemberKind == "Property" &&
                reference.Member == member);

            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal(["Any"], target.AcceptedObjectGroups);
        }
    }

    [Fact]
    public void ApiCoveragePriorityPacks_LoadReadyAnnotatedNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new[]
        {
            "ACT_SetUIVisible",
            "ACT_SetUIImage",
            "PROP_UIVisible",
            "PROP_UIImage",
            "EV_OnTextInputChanged",
            "EV_OnTextInputSubmitted",
            "ACT_SetTextInputText",
            "ACT_SetTextInputPlaceholder",
            "ACT_SetTextInputReadOnly",
            "ACT_FocusTextInput",
            "PROP_TextInputText",
            "PROP_TextInputPlaceholder",
            "PROP_TextInputReadOnly",
            "ACT_SetUIFieldZIndex",
            "ACT_SetUIFieldIgnoresMouse",
            "ACT_SetUIFieldClipDescendants",
            "ACT_SetUIFieldRotation",
            "ACT_SetUIFieldScale",
            "ACT_SetScrollViewMode",
            "ACT_SetGridLayoutColumns",
            "ACT_SetGridLayoutSpacing",
            "ACT_SetLayoutSpacing",
            "ACT_SetLayoutChildAlignment",
            "ACT_SetGui3DShaded",
            "ACT_SetGui3DFaceCamera",
            "ACT_SetGui3DTransparent",
            "PROP_UIFieldZIndex",
            "PROP_UIFieldIgnoresMouse",
            "PROP_UIFieldClipDescendants",
            "PROP_UIFieldRotation",
            "PROP_UIFieldScale",
            "PROP_ScrollViewHorizontalMode",
            "PROP_ScrollViewVerticalMode",
            "PROP_GridLayoutColumns",
            "PROP_GridLayoutSpacing",
            "PROP_LayoutSpacing",
            "PROP_LayoutChildAlignment",
            "PROP_Gui3DShaded",
            "PROP_Gui3DFaceCamera",
            "PROP_Gui3DTransparent",
            "ACT_SetCameraFOV",
            "PROP_CameraFOV",
            "ACT_SetValueObjectValue",
            "PROP_ValueObjectValue",
            "ACT_SetIntegerValueObject",
            "PROP_IntegerValueObject",
            "ACT_SetInstanceValueObject",
            "PROP_InstanceValueObject",
            "PROP_WorldIsLocalTest",
            "PROP_WorldIsOldFormat",
            "PROP_WorldIdentifier",
            "PROP_ServerIdentifier",
            "PROP_WorldUptime",
            "PROP_ServerTime",
            "PROP_WorldObjectCount",
            "ACT_SetDecalImage",
            "PROP_DecalImage"
        };

        foreach (var id in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.ApiReferences);
            Assert.NotEmpty(node.SearchKeywords);
            Assert.NotEmpty(node.PalettePath);
        }
    }

    [Fact]
    public void AudioAndLightingPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, string[]>
        {
            ["ACT_PlaySound"] = ["Audio", "Playback"],
            ["ACT_PlaySoundOnce"] = ["Audio", "Playback"],
            ["ACT_PauseSound"] = ["Audio", "Playback"],
            ["ACT_StopSound"] = ["Audio", "Playback"],
            ["ACT_SetSoundVolume"] = ["Audio", "Settings"],
            ["ACT_SetSoundLoop"] = ["Audio", "Settings"],
            ["ACT_SetSoundAudio"] = ["Audio", "Settings"],
            ["PROP_SoundVolume"] = ["Audio", "Settings"],
            ["PROP_SoundAudio"] = ["Audio", "Settings"],
            ["PROP_SoundIsPlaying"] = ["Audio", "Status"],
            ["PROP_SoundLength"] = ["Audio", "Status"],
            ["PROP_SoundTime"] = ["Audio", "Status"],
            ["COND_SoundIsPlaying"] = ["Audio", "Status"],
            ["COND_SoundIsLooping"] = ["Audio", "Settings"],
            ["COND_SoundVolumeAtLeast"] = ["Audio", "Settings"],
            ["COND_SoundVolumeAtMost"] = ["Audio", "Settings"],
            ["EV_OnSoundLoaded"] = ["Audio", "Playback"],
            ["EV_OnSoundStarted"] = ["Audio", "Playback"],
            ["EV_OnSoundStopped"] = ["Audio", "Playback"],
            ["EV_OnSoundVolumeReached"] = ["Audio", "Settings"],
            ["EV_OnSoundVolumeDroppedTo"] = ["Audio", "Settings"],
            ["ACT_SetFogEnabled"] = ["Lighting", "Fog"],
            ["ACT_SetFogColor"] = ["Lighting", "Fog"],
            ["ACT_SetFogDistances"] = ["Lighting", "Fog"],
            ["ACT_SetAmbientColor"] = ["Lighting", "Ambient"],
            ["COND_FogIsEnabled"] = ["Lighting", "Fog"],
            ["COND_FogStartDistanceAtLeast"] = ["Lighting", "Fog"],
            ["COND_FogStartDistanceAtMost"] = ["Lighting", "Fog"],
            ["COND_FogEndDistanceAtLeast"] = ["Lighting", "Fog"],
            ["COND_FogEndDistanceAtMost"] = ["Lighting", "Fog"],
            ["EV_OnFogEnabled"] = ["Lighting", "Fog Changes"],
            ["EV_OnFogDisabled"] = ["Lighting", "Fog Changes"],
            ["EV_OnFogStartDistanceReached"] = ["Lighting", "Fog Changes"],
            ["EV_OnFogStartDistanceDroppedTo"] = ["Lighting", "Fog Changes"],
            ["EV_OnFogEndDistanceReached"] = ["Lighting", "Fog Changes"],
            ["EV_OnFogEndDistanceDroppedTo"] = ["Lighting", "Fog Changes"],
            ["ACT_SetLightColor"] = ["Lighting", "Light Settings"],
            ["ACT_SetLightBrightness"] = ["Lighting", "Light Settings"],
            ["ACT_SetLightShine"] = ["Lighting", "Light Settings"],
            ["ACT_SetLightShadows"] = ["Lighting", "Light Settings"],
            ["COND_LightBrightnessAtLeast"] = ["Lighting", "Light Settings"],
            ["COND_LightBrightnessAtMost"] = ["Lighting", "Light Settings"],
            ["COND_LightShadowsEnabled"] = ["Lighting", "Light Settings"],
            ["EV_OnLightBrightnessReached"] = ["Lighting", "Light Changes"],
            ["EV_OnLightBrightnessDroppedTo"] = ["Lighting", "Light Changes"],
            ["EV_OnLightShadowsEnabled"] = ["Lighting", "Light Changes"],
            ["EV_OnLightShadowsDisabled"] = ["Lighting", "Light Changes"],
            ["ACT_SetSunLightColor"] = ["Lighting", "Sun Light"],
            ["ACT_SetSunLightBrightness"] = ["Lighting", "Sun Light"],
            ["ACT_SetSunLightShine"] = ["Lighting", "Sun Light"],
            ["ACT_SetSunLightShadows"] = ["Lighting", "Sun Light"],
            ["COND_SunLightBrightnessAtLeast"] = ["Lighting", "Sun Light"],
            ["COND_SunLightBrightnessAtMost"] = ["Lighting", "Sun Light"],
            ["COND_SunLightShadowsEnabled"] = ["Lighting", "Sun Light"],
            ["EV_OnSunLightBrightnessReached"] = ["Lighting", "Sun Light Changes"],
            ["EV_OnSunLightBrightnessDroppedTo"] = ["Lighting", "Sun Light Changes"],
            ["EV_OnSunLightShadowsEnabled"] = ["Lighting", "Sun Light Changes"],
            ["EV_OnSunLightShadowsDisabled"] = ["Lighting", "Sun Light Changes"],
            ["ACT_SetPointLightRange"] = ["Lighting", "Light Reach"],
            ["ACT_SetSpotLightRange"] = ["Lighting", "Light Reach"],
            ["ACT_SetSpotLightAngle"] = ["Lighting", "Light Reach"],
            ["ACT_SetColorAdjustBrightness"] = ["Lighting", "Color Adjust"],
            ["ACT_SetColorAdjustContrast"] = ["Lighting", "Color Adjust"],
            ["ACT_SetColorAdjustSaturation"] = ["Lighting", "Color Adjust"],
            ["ACT_SetColorAdjustTint"] = ["Lighting", "Color Adjust"],
            ["ACT_SetProceduralSkySunSize"] = ["Lighting", "Procedural Sky"],
            ["ACT_SetProceduralSkyTint"] = ["Lighting", "Procedural Sky"],
            ["ACT_SetProceduralSkyHorizonColor"] = ["Lighting", "Procedural Sky"],
            ["ACT_SetProceduralSkyGroundColor"] = ["Lighting", "Procedural Sky"],
            ["ACT_SetProceduralSkyExposure"] = ["Lighting", "Procedural Sky"],
            ["COND_ProceduralSkySunSizeAtLeast"] = ["Lighting", "Procedural Sky"],
            ["COND_ProceduralSkyTintIs"] = ["Lighting", "Procedural Sky"],
            ["COND_ProceduralSkyHorizonColorIs"] = ["Lighting", "Procedural Sky"],
            ["COND_ProceduralSkyGroundColorIs"] = ["Lighting", "Procedural Sky"],
            ["COND_ProceduralSkyExposureAtLeast"] = ["Lighting", "Procedural Sky"],
            ["EV_OnProceduralSkySunSizeReached"] = ["Lighting", "Procedural Sky Changes"],
            ["EV_OnProceduralSkyTintChanged"] = ["Lighting", "Procedural Sky Changes"],
            ["EV_OnProceduralSkyHorizonColorChanged"] = ["Lighting", "Procedural Sky Changes"],
            ["EV_OnProceduralSkyGroundColorChanged"] = ["Lighting", "Procedural Sky Changes"],
            ["EV_OnProceduralSkyExposureReached"] = ["Lighting", "Procedural Sky Changes"],
            ["ACT_SetGradientSkyColors"] = ["Lighting", "Sky Gradient"],
            ["ACT_SetGradientSkySunDisc"] = ["Lighting", "Sky Sun Disc"],
            ["ACT_SetGradientSkySunHalo"] = ["Lighting", "Sky Sun Halo"],
            ["ACT_SetGradientSkyHorizonLine"] = ["Lighting", "Sky Horizon"],
            ["ACT_SetImageSkyAllImages"] = ["Lighting", "Image Sky Setup"],
            ["ACT_SetImageSkyTopImage"] = ["Lighting", "Image Sky Sides"],
            ["ACT_SetImageSkyBottomImage"] = ["Lighting", "Image Sky Sides"],
            ["ACT_SetImageSkyLeftImage"] = ["Lighting", "Image Sky Sides"],
            ["ACT_SetImageSkyRightImage"] = ["Lighting", "Image Sky Sides"],
            ["ACT_SetImageSkyFrontImage"] = ["Lighting", "Image Sky Sides"],
            ["ACT_SetImageSkyBackImage"] = ["Lighting", "Image Sky Sides"],
            ["COND_ImageSkyTopImageIs"] = ["Lighting", "Image Sky Sides"],
            ["COND_ImageSkyBottomImageIs"] = ["Lighting", "Image Sky Sides"],
            ["COND_ImageSkyLeftImageIs"] = ["Lighting", "Image Sky Sides"],
            ["COND_ImageSkyRightImageIs"] = ["Lighting", "Image Sky Sides"],
            ["COND_ImageSkyFrontImageIs"] = ["Lighting", "Image Sky Sides"],
            ["COND_ImageSkyBackImageIs"] = ["Lighting", "Image Sky Sides"],
            ["EV_OnImageSkyTopImageChanged"] = ["Lighting", "Image Sky Changes"],
            ["EV_OnImageSkyBottomImageChanged"] = ["Lighting", "Image Sky Changes"],
            ["EV_OnImageSkyLeftImageChanged"] = ["Lighting", "Image Sky Changes"],
            ["EV_OnImageSkyRightImageChanged"] = ["Lighting", "Image Sky Changes"],
            ["EV_OnImageSkyFrontImageChanged"] = ["Lighting", "Image Sky Changes"],
            ["EV_OnImageSkyBackImageChanged"] = ["Lighting", "Image Sky Changes"],
            ["PROP_FogEnabled"] = ["Lighting", "Fog"],
            ["PROP_FogStartDistance"] = ["Lighting", "Fog"],
            ["PROP_FogEndDistance"] = ["Lighting", "Fog"],
            ["PROP_AmbientColor"] = ["Lighting", "Ambient"],
            ["PROP_LightColor"] = ["Lighting", "Light Settings"],
            ["PROP_LightBrightness"] = ["Lighting", "Light Settings"],
            ["PROP_LightShine"] = ["Lighting", "Light Settings"],
            ["PROP_LightShadows"] = ["Lighting", "Light Settings"],
            ["PROP_SunLightColor"] = ["Lighting", "Sun Light"],
            ["PROP_SunLightBrightness"] = ["Lighting", "Sun Light"],
            ["PROP_SunLightShine"] = ["Lighting", "Sun Light"],
            ["PROP_SunLightShadows"] = ["Lighting", "Sun Light"],
            ["PROP_PointLightRange"] = ["Lighting", "Light Reach"],
            ["PROP_SpotLightRange"] = ["Lighting", "Light Reach"],
            ["PROP_SpotLightAngle"] = ["Lighting", "Light Reach"],
            ["PROP_ColorAdjustBrightness"] = ["Lighting", "Color Adjust"],
            ["PROP_ColorAdjustContrast"] = ["Lighting", "Color Adjust"],
            ["PROP_ColorAdjustSaturation"] = ["Lighting", "Color Adjust"],
            ["PROP_ColorAdjustTint"] = ["Lighting", "Color Adjust"],
            ["PROP_ProceduralSkySunSize"] = ["Lighting", "Procedural Sky"],
            ["PROP_ProceduralSkyTint"] = ["Lighting", "Procedural Sky"],
            ["PROP_ProceduralSkyHorizonColor"] = ["Lighting", "Procedural Sky"],
            ["PROP_ProceduralSkyGroundColor"] = ["Lighting", "Procedural Sky"],
            ["PROP_ProceduralSkyExposure"] = ["Lighting", "Procedural Sky"],
            ["PROP_GradientSkyTopColor"] = ["Lighting", "Sky Gradient"],
            ["PROP_GradientSkyBottomColor"] = ["Lighting", "Sky Gradient"],
            ["PROP_GradientSkyExponent"] = ["Lighting", "Sky Gradient"],
            ["PROP_GradientSkySunDiscColor"] = ["Lighting", "Sky Sun Disc"],
            ["PROP_GradientSkySunDiscMultiplier"] = ["Lighting", "Sky Sun Disc"],
            ["PROP_GradientSkySunDiscExponent"] = ["Lighting", "Sky Sun Disc"],
            ["PROP_GradientSkySunHaloColor"] = ["Lighting", "Sky Sun Halo"],
            ["PROP_GradientSkySunHaloExponent"] = ["Lighting", "Sky Sun Halo"],
            ["PROP_GradientSkySunHaloContribution"] = ["Lighting", "Sky Sun Halo"],
            ["PROP_GradientSkyHorizonLineColor"] = ["Lighting", "Sky Horizon"],
            ["PROP_GradientSkyHorizonLineExponent"] = ["Lighting", "Sky Horizon"],
            ["PROP_GradientSkyHorizonLineContribution"] = ["Lighting", "Sky Horizon"],
            ["PROP_ImageSkyTopImage"] = ["Lighting", "Image Sky Sides"],
            ["PROP_ImageSkyBottomImage"] = ["Lighting", "Image Sky Sides"],
            ["PROP_ImageSkyLeftImage"] = ["Lighting", "Image Sky Sides"],
            ["PROP_ImageSkyRightImage"] = ["Lighting", "Image Sky Sides"],
            ["PROP_ImageSkyFrontImage"] = ["Lighting", "Image Sky Sides"],
            ["PROP_ImageSkyBackImage"] = ["Lighting", "Image Sky Sides"]
        };

        foreach (var (id, expectedPath) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(expectedPath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
        }
    }

    [Fact]
    public void EffectsApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, (string[] Path, string TargetGroup)>
        {
            ["ACT_StartParticles"] = (["Effects", "Particles"], "ParticleObject"),
            ["ACT_StopParticles"] = (["Effects", "Particles"], "ParticleObject"),
            ["ACT_BurstParticles"] = (["Effects", "Particles"], "ParticleObject"),
            ["ACT_SetParticleAmount"] = (["Effects", "Particles"], "ParticleObject"),
            ["PROP_ParticlesPlaying"] = (["Effects", "Particles"], "ParticleObject"),
            ["PROP_ParticleAmount"] = (["Effects", "Particles"], "ParticleObject"),
            ["ACT_SetExplosionRadius"] = (["Effects", "Explosion"], "ExplosionObject"),
            ["ACT_SetExplosionForce"] = (["Effects", "Explosion"], "ExplosionObject"),
            ["ACT_SetExplosionDamage"] = (["Effects", "Explosion"], "ExplosionObject"),
            ["ACT_SetExplosionAffectAnchored"] = (["Effects", "Explosion"], "ExplosionObject"),
            ["PROP_ExplosionRadius"] = (["Effects", "Explosion"], "ExplosionObject"),
            ["PROP_ExplosionForce"] = (["Effects", "Explosion"], "ExplosionObject"),
            ["PROP_ExplosionDamage"] = (["Effects", "Explosion"], "ExplosionObject"),
            ["PROP_ExplosionAffectAnchored"] = (["Effects", "Explosion"], "ExplosionObject"),
            ["EV_OnExplosionTouched"] = (["Effects", "Explosion"], "ExplosionObject")
        };

        foreach (var (id, expected) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(expected.Path, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal([expected.TargetGroup], target.AcceptedObjectGroups);
        }
    }

    [Fact]
    public void GrabbableApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, string[]>
        {
            ["ACT_SetGrabForce"] = ["Scene Object", "Grab"],
            ["ACT_SetGrabMaxRange"] = ["Scene Object", "Grab"],
            ["ACT_SetGrabPickupRange"] = ["Scene Object", "Grab"],
            ["ACT_SetGrabUsesDragForce"] = ["Scene Object", "Grab"],
            ["ACT_SetGrabPermissionMode"] = ["Scene Object", "Grab"],
            ["PROP_GrabForce"] = ["Scene Object", "Grab"],
            ["PROP_GrabMaxRange"] = ["Scene Object", "Grab"],
            ["PROP_GrabPickupRange"] = ["Scene Object", "Grab"],
            ["PROP_GrabUsesDragForce"] = ["Scene Object", "Grab"],
            ["PROP_GrabPermissionMode"] = ["Scene Object", "Grab"],
            ["PROP_CurrentGrabber"] = ["Scene Object", "Grab"],
            ["EV_OnObjectGrabbed"] = ["Scene Object", "Grab"],
            ["EV_OnObjectReleased"] = ["Scene Object", "Grab"],
            ["COND_GrabForceAtLeast"] = ["Scene Object", "Grab"],
            ["COND_GrabMaxRangeAtLeast"] = ["Scene Object", "Grab"],
            ["COND_GrabPickupRangeAtLeast"] = ["Scene Object", "Grab"],
            ["COND_GrabUsesDragForce"] = ["Scene Object", "Grab"],
            ["COND_GrabPermissionModeIs"] = ["Scene Object", "Grab"],
            ["EV_OnGrabForceReached"] = ["Scene Object", "Grab Changes"],
            ["EV_OnGrabMaxRangeReached"] = ["Scene Object", "Grab Changes"],
            ["EV_OnGrabPickupRangeReached"] = ["Scene Object", "Grab Changes"]
        };

        foreach (var (id, expectedPath) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(expectedPath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal(["GrabbableObject"], target.AcceptedObjectGroups);
        }

        var permission = catalog.Nodes.Single(entry => entry.IdBase == "ACT_SetGrabPermissionMode");
        var mode = Assert.Single(permission.Parameters, parameter => parameter.Key == "mode");
        Assert.Equal("Choice", mode.Control);
        Assert.Equal(["None", "Everyone", "Scripted"], mode.Options);

        var permissionCondition = catalog.Nodes.Single(entry => entry.IdBase == "COND_GrabPermissionModeIs");
        var expectedMode = Assert.Single(permissionCondition.Parameters, parameter => parameter.Key == "mode");
        Assert.Equal("Choice", expectedMode.Control);
        Assert.Equal(["None", "Everyone", "Scripted"], expectedMode.Options);
    }

    [Fact]
    public void MeshApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, string[]>
        {
            ["EV_OnMeshLoaded"] = ["Scene Object", "Mesh Animation"],
            ["ACT_PlayMeshAnimation"] = ["Scene Object", "Mesh Animation"],
            ["ACT_StopMeshAnimation"] = ["Scene Object", "Mesh Animation"],
            ["PROP_CurrentMeshAnimation"] = ["Scene Object", "Mesh Animation"],
            ["PROP_MeshAnimationPlaying"] = ["Scene Object", "Mesh Animation"],
            ["PROP_MeshLoading"] = ["Scene Object", "Mesh Animation"]
        };

        foreach (var (id, expectedPath) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(expectedPath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal(["MeshObject"], target.AcceptedObjectGroups);
        }
    }

    [Fact]
    public void CharacterAnimationApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, string[]>
        {
            ["ACT_PlayCharacterAnimation"] = ["Players", "Character Animation"],
            ["ACT_PlayCharacterOneShotAnimation"] = ["Players", "Character Animation"],
            ["ACT_StopCharacterAnimation"] = ["Players", "Character Animation"],
            ["ACT_StopCharacterOneShotAnimation"] = ["Players", "Character Animation"],
            ["ACT_SetCharacterState"] = ["Players", "Character Animation"],
            ["ACT_SetCharacterAnimationSpeed"] = ["Players", "Character Animation"],
            ["COND_CurrentCharacterAnimationIs"] = ["Players", "Character Animation"],
            ["COND_CharacterHasAnimator"] = ["Players", "Character Animation"],
            ["COND_CharacterStateIs"] = ["Players", "Character Animation"],
            ["COND_CharacterAnimationSpeedAtLeast"] = ["Players", "Character Animation"],
            ["COND_CharacterAnimationSpeedAtMost"] = ["Players", "Character Animation"],
            ["COND_CharacterHasAttachment"] = ["Players", "Character Animation"],
            ["EV_OnCharacterAnimationChanged"] = ["Players", "Character Animation Changes"],
            ["EV_OnCharacterAnimationBecame"] = ["Players", "Character Animation Changes"],
            ["EV_OnCharacterStateChanged"] = ["Players", "Character Animation Changes"],
            ["EV_OnCharacterStateBecame"] = ["Players", "Character Animation Changes"],
            ["EV_OnCharacterAnimationSpeedReached"] = ["Players", "Character Animation Changes"],
            ["EV_OnCharacterAttachmentAvailable"] = ["Players", "Character Animation Changes"],
            ["PROP_CurrentCharacterAnimation"] = ["Players", "Character Animation"],
            ["PROP_CharacterAnimator"] = ["Players", "Character Animation"],
            ["PROP_CharacterState"] = ["Players", "Character Animation"],
            ["PROP_CharacterAnimationSpeed"] = ["Players", "Character Animation"],
            ["PROP_CharacterAttachment"] = ["Players", "Character Animation"]
        };

        foreach (var (id, expectedPath) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(expectedPath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal(["CharacterModelObject"], target.AcceptedObjectGroups);
        }

        var setState = catalog.Nodes.Single(entry => entry.IdBase == "ACT_SetCharacterState");
        var state = Assert.Single(setState.Parameters, parameter => parameter.Key == "state");
        Assert.Equal("Choice", state.Control);
        Assert.Equal(["Idle", "Walking", "Running", "Jumping", "Climbing"], state.Options);

        var attachmentValue = catalog.Nodes.Single(entry => entry.IdBase == "PROP_CharacterAttachment");
        var attachment = Assert.Single(attachmentValue.Parameters, parameter => parameter.Key == "attachment");
        Assert.Equal("Choice", attachment.Control);
        Assert.Contains("Head", attachment.Options);
        Assert.Contains("HandLeft", attachment.Options);
        Assert.Contains("FootRight", attachment.Options);
    }

    [Fact]
    public void CharacterAppearanceApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, (string[] Path, string TargetGroup)>
        {
            ["ACT_SetAccessoryAttachment"] = (["Players", "Character Appearance"], "AccessoryObject"),
            ["PROP_AccessoryAttachment"] = (["Players", "Character Appearance"], "AccessoryObject"),
            ["COND_AccessoryAttachmentIs"] = (["Players", "Character Appearance"], "AccessoryObject"),
            ["ACT_SetClothingImage"] = (["Players", "Character Appearance"], "ClothingObject"),
            ["PROP_ClothingImage"] = (["Players", "Character Appearance"], "ClothingObject"),
            ["COND_ClothingImageIs"] = (["Players", "Character Appearance"], "ClothingObject"),
            ["COND_ClothingHasImage"] = (["Players", "Character Appearance"], "ClothingObject"),
            ["ACT_SetCharacterFaceImage"] = (["Players", "Character Appearance"], "PolytorianModelObject"),
            ["ACT_SetCharacterBodyMesh"] = (["Players", "Character Appearance"], "PolytorianModelObject"),
            ["ACT_SetCharacterBodyColor"] = (["Players", "Character Appearance"], "PolytorianModelObject"),
            ["ACT_LoadCharacterAppearance"] = (["Players", "Character Appearance"], "PolytorianModelObject"),
            ["ACT_ClearCharacterAppearance"] = (["Players", "Character Appearance"], "PolytorianModelObject"),
            ["ACT_StartCharacterRagdoll"] = (["Players", "Character Ragdoll"], "PolytorianModelObject"),
            ["ACT_StopCharacterRagdoll"] = (["Players", "Character Ragdoll"], "PolytorianModelObject"),
            ["PROP_CharacterFaceImage"] = (["Players", "Character Appearance"], "PolytorianModelObject"),
            ["PROP_CharacterBodyMesh"] = (["Players", "Character Appearance"], "PolytorianModelObject"),
            ["PROP_CharacterBodyColor"] = (["Players", "Character Appearance"], "PolytorianModelObject"),
            ["COND_CharacterFaceImageIs"] = (["Players", "Character Appearance"], "PolytorianModelObject"),
            ["COND_CharacterHasFaceImage"] = (["Players", "Character Appearance"], "PolytorianModelObject"),
            ["COND_CharacterBodyMeshIs"] = (["Players", "Character Appearance"], "PolytorianModelObject"),
            ["COND_CharacterHasBodyMesh"] = (["Players", "Character Appearance"], "PolytorianModelObject"),
            ["PROP_CharacterRagdolling"] = (["Players", "Character Ragdoll"], "PolytorianModelObject"),
            ["PROP_CharacterRagdollPosition"] = (["Players", "Character Ragdoll"], "PolytorianModelObject"),
            ["PROP_CharacterRagdollRotation"] = (["Players", "Character Ragdoll"], "PolytorianModelObject"),
            ["COND_CharacterIsRagdolling"] = (["Players", "Character Ragdoll"], "PolytorianModelObject"),
            ["COND_CharacterIsNotRagdolling"] = (["Players", "Character Ragdoll"], "PolytorianModelObject"),
            ["EV_OnCharacterRagdollStarted"] = (["Players", "Character Ragdoll"], "PolytorianModelObject"),
            ["EV_OnCharacterRagdollStopped"] = (["Players", "Character Ragdoll"], "PolytorianModelObject")
        };

        foreach (var (id, expected) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(expected.Path, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal([expected.TargetGroup], target.AcceptedObjectGroups);
        }

        var accessory = catalog.Nodes.Single(entry => entry.IdBase == "ACT_SetAccessoryAttachment");
        var accessoryAttachment = Assert.Single(accessory.Parameters, parameter => parameter.Key == "attachment");
        Assert.Equal("Choice", accessoryAttachment.Control);
        Assert.Contains("Head", accessoryAttachment.Options);
        Assert.Contains("HandRight", accessoryAttachment.Options);
        Assert.Contains("FootLeft", accessoryAttachment.Options);

        var colorAction = catalog.Nodes.Single(entry => entry.IdBase == "ACT_SetCharacterBodyColor");
        var bodyPart = Assert.Single(colorAction.Parameters, parameter => parameter.Key == "bodyPart");
        Assert.Equal("Choice", bodyPart.Control);
        Assert.Equal(["Head", "Torso", "Left Arm", "Right Arm", "Left Leg", "Right Leg"], bodyPart.Options);
    }

    [Fact]
    public void WorldMarkerTrussEntityApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, (string[] Path, string TargetGroup)>
        {
            ["ACT_SetMarkerLength"] = (["Scene Object", "Markers"], "Marker3DObject"),
            ["ACT_SetMarkerAppearsOnTop"] = (["Scene Object", "Markers"], "Marker3DObject"),
            ["ACT_SetMarkerVisibleInDev"] = (["Scene Object", "Markers"], "Marker3DObject"),
            ["PROP_MarkerLength"] = (["Scene Object", "Markers"], "Marker3DObject"),
            ["PROP_MarkerAppearsOnTop"] = (["Scene Object", "Markers"], "Marker3DObject"),
            ["PROP_MarkerVisibleInDev"] = (["Scene Object", "Markers"], "Marker3DObject"),
            ["COND_MarkerLengthAtLeast"] = (["Scene Object", "Markers"], "Marker3DObject"),
            ["COND_MarkerAppearsOnTop"] = (["Scene Object", "Markers"], "Marker3DObject"),
            ["COND_MarkerVisibleInDev"] = (["Scene Object", "Markers"], "Marker3DObject"),
            ["EV_OnMarkerLengthReached"] = (["Scene Object", "Marker Changes"], "Marker3DObject"),
            ["EV_OnMarkerAppearsOnTopEnabled"] = (["Scene Object", "Marker Changes"], "Marker3DObject"),
            ["EV_OnMarkerVisibleInDevEnabled"] = (["Scene Object", "Marker Changes"], "Marker3DObject"),
            ["ACT_SetTrussClimbSpeed"] = (["Scene Object", "Climb"], "TrussObject"),
            ["PROP_TrussClimbSpeed"] = (["Scene Object", "Climb"], "TrussObject"),
            ["COND_TrussClimbSpeedAtLeast"] = (["Scene Object", "Climb"], "TrussObject"),
            ["EV_OnTrussClimbSpeedReached"] = (["Scene Object", "Climb Changes"], "TrussObject"),
            ["ACT_SetEntityCastsShadows"] = (["Scene Object", "Entity"], "EntityObject"),
            ["ACT_SetEntityIsSpawn"] = (["Scene Object", "Entity"], "EntityObject"),
            ["ACT_SetEntityColor"] = (["Scene Object", "Entity"], "EntityObject"),
            ["PROP_EntityCastsShadows"] = (["Scene Object", "Entity"], "EntityObject"),
            ["PROP_EntityIsSpawn"] = (["Scene Object", "Entity"], "EntityObject"),
            ["PROP_EntityColor"] = (["Scene Object", "Entity"], "EntityObject"),
            ["COND_EntityCastsShadows"] = (["Scene Object", "Entity"], "EntityObject"),
            ["COND_EntityIsSpawn"] = (["Scene Object", "Entity"], "EntityObject"),
            ["COND_EntityColorIs"] = (["Scene Object", "Entity"], "EntityObject"),
            ["EV_OnEntityStartedCastingShadows"] = (["Scene Object", "Entity Changes"], "EntityObject"),
            ["EV_OnEntityBecameSpawn"] = (["Scene Object", "Entity Changes"], "EntityObject"),
            ["EV_OnEntityColorChanged"] = (["Scene Object", "Entity Changes"], "EntityObject")
        };

        foreach (var (id, expected) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(expected.Path, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
            Assert.NotEmpty(node.ApiReferences);
            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal([expected.TargetGroup], target.AcceptedObjectGroups);
        }
    }

    [Fact]
    public void SeatApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, string[]>
        {
            ["EV_OnSeatSat"] = ["Scene Object", "Seats"],
            ["EV_OnSeatVacated"] = ["Scene Object", "Seats"],
            ["ACT_SetSeatAllowsNPCs"] = ["Scene Object", "Seats"],
            ["COND_SeatIsOccupied"] = ["Scene Object", "Seats"],
            ["COND_SeatAllowsNPCs"] = ["Scene Object", "Seats"],
            ["PROP_SeatOccupant"] = ["Scene Object", "Seats"],
            ["PROP_SeatAllowsNPCs"] = ["Scene Object", "Seats"]
        };

        foreach (var (id, expectedPath) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(expectedPath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal(["SeatObject"], target.AcceptedObjectGroups);
            if (node.Kind == NodeKind.Trigger)
            {
                Assert.Equal("Self", target.Default);
                Assert.Equal("Target Context", target.ValueSource);
            }
        }
    }

    [Fact]
    public void Image3DApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, string[]>
        {
            ["ACT_Set3DImageColor"] = ["Scene Object", "3D Image Display"],
            ["ACT_Set3DImageShadows"] = ["Scene Object", "3D Image Display"],
            ["ACT_Set3DImageLighting"] = ["Scene Object", "3D Image Display"],
            ["ACT_Set3DImageFaceCamera"] = ["Scene Object", "3D Image Display"],
            ["ACT_Set3DImageTextureScale"] = ["Scene Object", "3D Image Texture"],
            ["ACT_Set3DImageTextureOffset"] = ["Scene Object", "3D Image Texture"],
            ["COND_3DImageCastsShadows"] = ["Scene Object", "3D Image Display"],
            ["COND_3DImageUsesLighting"] = ["Scene Object", "3D Image Display"],
            ["COND_3DImageFacesCamera"] = ["Scene Object", "3D Image Display"],
            ["COND_3DImageColorIs"] = ["Scene Object", "3D Image Display"],
            ["COND_3DImageTextureScaleIs"] = ["Scene Object", "3D Image Texture"],
            ["COND_3DImageTextureOffsetIs"] = ["Scene Object", "3D Image Texture"],
            ["EV_On3DImageColorChanged"] = ["Scene Object", "3D Image Changes"],
            ["EV_On3DImageShadowsEnabled"] = ["Scene Object", "3D Image Changes"],
            ["EV_On3DImageLightingEnabled"] = ["Scene Object", "3D Image Changes"],
            ["EV_On3DImageFaceCameraEnabled"] = ["Scene Object", "3D Image Changes"],
            ["EV_On3DImageTextureScaleChanged"] = ["Scene Object", "3D Image Changes"],
            ["EV_On3DImageTextureOffsetChanged"] = ["Scene Object", "3D Image Changes"],
            ["PROP_3DImageColor"] = ["Scene Object", "3D Image Display"],
            ["PROP_3DImageCastsShadows"] = ["Scene Object", "3D Image Display"],
            ["PROP_3DImageUsesLighting"] = ["Scene Object", "3D Image Display"],
            ["PROP_3DImageFacesCamera"] = ["Scene Object", "3D Image Display"]
        };

        foreach (var (id, expectedPath) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(expectedPath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);

            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal(["Image3DObject"], target.AcceptedObjectGroups);
            Assert.Equal(["World"], target.AcceptedSceneRoots);
        }
    }

    [Fact]
    public void Text3DApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, string[]>
        {
            ["ACT_Set3DText"] = ["Scene Object", "3D Text Content"],
            ["ACT_Set3DTextFontSize"] = ["Scene Object", "3D Text Content"],
            ["ACT_Set3DTextRichText"] = ["Scene Object", "3D Text Content"],
            ["ACT_Set3DTextColor"] = ["Scene Object", "3D Text Display"],
            ["ACT_Set3DTextOutlineWidth"] = ["Scene Object", "3D Text Display"],
            ["ACT_Set3DTextOutlineColor"] = ["Scene Object", "3D Text Display"],
            ["ACT_Set3DTextFaceCamera"] = ["Scene Object", "3D Text Display"],
            ["ACT_Set3DTextLighting"] = ["Scene Object", "3D Text Display"],
            ["COND_3DTextIs"] = ["Scene Object", "3D Text Content"],
            ["COND_3DTextIsEmpty"] = ["Scene Object", "3D Text Content"],
            ["COND_3DTextFontSizeAtLeast"] = ["Scene Object", "3D Text Content"],
            ["COND_3DTextColorIs"] = ["Scene Object", "3D Text Display"],
            ["COND_3DTextOutlineWidthAtLeast"] = ["Scene Object", "3D Text Display"],
            ["COND_3DTextFacesCamera"] = ["Scene Object", "3D Text Display"],
            ["COND_3DTextUsesRichText"] = ["Scene Object", "3D Text Content"],
            ["COND_3DTextUsesLighting"] = ["Scene Object", "3D Text Display"],
            ["EV_On3DTextChanged"] = ["Scene Object", "3D Text Changes"],
            ["EV_On3DTextSizeReached"] = ["Scene Object", "3D Text Changes"],
            ["EV_On3DTextColorChanged"] = ["Scene Object", "3D Text Changes"],
            ["EV_On3DTextOutlineWidthReached"] = ["Scene Object", "3D Text Changes"],
            ["EV_On3DTextOutlineColorChanged"] = ["Scene Object", "3D Text Changes"],
            ["EV_On3DTextFaceCameraEnabled"] = ["Scene Object", "3D Text Changes"],
            ["EV_On3DTextRichTextEnabled"] = ["Scene Object", "3D Text Changes"],
            ["EV_On3DTextLightingEnabled"] = ["Scene Object", "3D Text Changes"],
            ["PROP_3DText"] = ["Scene Object", "3D Text Content"],
            ["PROP_3DTextFontSize"] = ["Scene Object", "3D Text Content"],
            ["PROP_3DTextColor"] = ["Scene Object", "3D Text Display"],
            ["PROP_3DTextOutlineWidth"] = ["Scene Object", "3D Text Display"],
            ["PROP_3DTextOutlineColor"] = ["Scene Object", "3D Text Display"],
            ["PROP_3DTextFacesCamera"] = ["Scene Object", "3D Text Display"],
            ["PROP_3DTextUsesRichText"] = ["Scene Object", "3D Text Content"],
            ["PROP_3DTextUsesLighting"] = ["Scene Object", "3D Text Display"]
        };

        foreach (var (id, expectedPath) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(expectedPath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);

            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal(["Text3DObject"], target.AcceptedObjectGroups);
            Assert.Equal(["World"], target.AcceptedSceneRoots);
        }
    }

    [Fact]
    public void NpcApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, string[]>
        {
            ["EV_OnNPCDied"] = ["Scene Object", "NPC Health"],
            ["EV_OnNPCLanded"] = ["Scene Object", "NPC Movement"],
            ["EV_OnNPCNavigationFinished"] = ["Scene Object", "NPC Movement"],
            ["ACT_SetNPCHealth"] = ["Scene Object", "NPC Health"],
            ["ACT_DamageNPC"] = ["Scene Object", "NPC Health"],
            ["ACT_HealNPC"] = ["Scene Object", "NPC Health"],
            ["ACT_KillNPC"] = ["Scene Object", "NPC Health"],
            ["ACT_SetNPCWalkSpeed"] = ["Scene Object", "NPC Movement"],
            ["ACT_SetNPCJumpPower"] = ["Scene Object", "NPC Movement"],
            ["ACT_MakeNPCJump"] = ["Scene Object", "NPC Movement"],
            ["ACT_SetNPCNavigationTarget"] = ["Scene Object", "NPC Movement"],
            ["COND_NPCIsDead"] = ["Scene Object", "NPC Health"],
            ["COND_NPCIsOnGround"] = ["Scene Object", "NPC Movement"],
            ["COND_NPCHealthAtMost"] = ["Scene Object", "NPC Health"],
            ["COND_NPCReachedNavigationTarget"] = ["Scene Object", "NPC Movement"],
            ["PROP_NPCHealth"] = ["Scene Object", "NPC Health"],
            ["PROP_NPCWalkSpeed"] = ["Scene Object", "NPC Movement"],
            ["PROP_NPCJumpPower"] = ["Scene Object", "NPC Movement"],
            ["PROP_NPCIsDead"] = ["Scene Object", "NPC Health"],
            ["PROP_NPCIsOnGround"] = ["Scene Object", "NPC Movement"],
            ["PROP_NPCNavigationDistance"] = ["Scene Object", "NPC Movement"]
        };

        foreach (var (id, expectedPath) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(expectedPath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);

            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal(["NPCObject"], target.AcceptedObjectGroups);
            Assert.Equal(["World"], target.AcceptedSceneRoots);
            if (node.Kind == NodeKind.Trigger)
            {
                Assert.Equal("Self", target.Default);
                Assert.Equal("Target Context", target.ValueSource);
            }
        }
    }

    [Fact]
    public void BodyPositionApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, string[]>
        {
            ["EV_OnBodyPositionReachedTarget"] = ["Scene Object", "Body Position"],
            ["ACT_SetBodyPositionTarget"] = ["Scene Object", "Body Position"],
            ["ACT_SetBodyPositionForce"] = ["Scene Object", "Body Position"],
            ["ACT_SetBodyPositionAcceptanceDistance"] = ["Scene Object", "Body Position"],
            ["COND_BodyPositionReachedTarget"] = ["Scene Object", "Body Position"],
            ["COND_BodyPositionForceAtLeast"] = ["Scene Object", "Body Position"],
            ["PROP_BodyPositionTarget"] = ["Scene Object", "Body Position"],
            ["PROP_BodyPositionForce"] = ["Scene Object", "Body Position"],
            ["PROP_BodyPositionAcceptanceDistance"] = ["Scene Object", "Body Position"],
            ["PROP_BodyPositionDistanceToTarget"] = ["Scene Object", "Body Position"]
        };

        foreach (var (id, expectedPath) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(expectedPath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);

            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal(["BodyPositionObject"], target.AcceptedObjectGroups);
            Assert.Equal(["World"], target.AcceptedSceneRoots);
        }
    }

    [Fact]
    public void PhysicalApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, string[]>
        {
            ["EV_OnTouchObject"] = ["Scene Object", "Touch"],
            ["EV_OnObjectTouchEnded"] = ["Scene Object", "Touch"],
            ["EV_OnObjectClicked"] = ["Scene Object", "Input"],
            ["EV_OnObjectHoverStarted"] = ["Scene Object", "Input"],
            ["EV_OnObjectHoverEnded"] = ["Scene Object", "Input"],
            ["ACT_MoveObjectWithPhysics"] = ["Scene Object", "Physics Motion"],
            ["ACT_TurnObjectWithPhysics"] = ["Scene Object", "Physics Motion"],
            ["ACT_SetObjectVelocity"] = ["Scene Object", "Physics Motion"],
            ["ACT_SetObjectSpinVelocity"] = ["Scene Object", "Physics Motion"],
            ["COND_ObjectIsMoving"] = ["Scene Object", "Physics Motion"],
            ["COND_ObjectSpeedAtLeast"] = ["Scene Object", "Physics Motion"],
            ["PROP_ObjectVelocity"] = ["Scene Object", "Physics Motion"],
            ["PROP_ObjectSpeed"] = ["Scene Object", "Physics Motion"],
            ["PROP_ObjectSpinVelocity"] = ["Scene Object", "Physics Motion"],
            ["PROP_TouchingObjectCount"] = ["Scene Object", "Touch"]
        };

        foreach (var (id, expectedPath) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(expectedPath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);

            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal(["PhysicsBody"], target.AcceptedObjectGroups);
        }
    }

    [Fact]
    public void RigidBodyApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, string[]>
        {
            ["ACT_SetRigidBodyGravity"] = ["Scene Object", "Rigid Body"],
            ["ACT_SetRigidBodyMass"] = ["Scene Object", "Rigid Body"],
            ["ACT_SetRigidBodyFriction"] = ["Scene Object", "Rigid Body"],
            ["ACT_SetRigidBodyDrag"] = ["Scene Object", "Rigid Body"],
            ["ACT_SetRigidBodyAngularDrag"] = ["Scene Object", "Rigid Body"],
            ["ACT_SetRigidBodyBounciness"] = ["Scene Object", "Rigid Body"],
            ["COND_RigidBodyGravityEnabled"] = ["Scene Object", "Rigid Body"],
            ["COND_RigidBodyMassAtLeast"] = ["Scene Object", "Rigid Body"],
            ["COND_RigidBodyFrictionAtLeast"] = ["Scene Object", "Rigid Body"],
            ["COND_RigidBodyDragAtLeast"] = ["Scene Object", "Rigid Body"],
            ["COND_RigidBodyAngularDragAtLeast"] = ["Scene Object", "Rigid Body"],
            ["COND_RigidBodyBouncinessAtLeast"] = ["Scene Object", "Rigid Body"],
            ["EV_OnRigidBodyGravityEnabled"] = ["Scene Object", "Rigid Body Changes"],
            ["EV_OnRigidBodyMassReached"] = ["Scene Object", "Rigid Body Changes"],
            ["EV_OnRigidBodyFrictionReached"] = ["Scene Object", "Rigid Body Changes"],
            ["EV_OnRigidBodyDragReached"] = ["Scene Object", "Rigid Body Changes"],
            ["EV_OnRigidBodyAngularDragReached"] = ["Scene Object", "Rigid Body Changes"],
            ["EV_OnRigidBodyBouncinessReached"] = ["Scene Object", "Rigid Body Changes"],
            ["PROP_RigidBodyGravityEnabled"] = ["Scene Object", "Rigid Body"],
            ["PROP_RigidBodyMass"] = ["Scene Object", "Rigid Body"],
            ["PROP_RigidBodyFriction"] = ["Scene Object", "Rigid Body"],
            ["PROP_RigidBodyDrag"] = ["Scene Object", "Rigid Body"],
            ["PROP_RigidBodyAngularDrag"] = ["Scene Object", "Rigid Body"],
            ["PROP_RigidBodyBounciness"] = ["Scene Object", "Rigid Body"]
        };

        foreach (var (id, expectedPath) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, node.ApiSurface);
            Assert.NotEmpty(node.ApiReferences);
            Assert.Equal(expectedPath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);

            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal(["RigidBodyObject"], target.AcceptedObjectGroups);
        }
    }

    [Fact]
    public void TeamApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, string[]>
        {
            ["ACT_SetPlayerGameTeam"] = ["Players", "Game Team"],
            ["COND_PlayerIsInGameTeam"] = ["Players", "Game Team"],
            ["EV_OnPlayerGameTeamChanged"] = ["Players", "Game Team"],
            ["PROP_TriggeringPlayerGameTeam"] = ["Players", "Game Team"],
            ["PROP_PlayerGameTeamName"] = ["Players", "Game Team"],
            ["PROP_PlayerGameTeamColor"] = ["Players", "Game Team"],
            ["PROP_GameTeamName"] = ["Game Rules", "Game Teams"],
            ["PROP_GameTeamColor"] = ["Game Rules", "Game Teams"],
            ["PROP_GameTeamPlayerCount"] = ["Game Rules", "Game Teams"],
            ["PROP_GameTeamPlayers"] = ["Game Rules", "Game Teams"],
            ["PROP_GameTeamCount"] = ["Game Rules", "Game Teams"],
            ["PROP_AllGameTeams"] = ["Game Rules", "Game Teams"]
        };

        foreach (var (id, expectedPath) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, node.ApiSurface);
            Assert.NotEmpty(node.ApiReferences);
            Assert.Equal(expectedPath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
        }
    }

    [Fact]
    public void StatsApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, string[]>
        {
            ["ACT_SetPlayerStatNumber"] = ["Game Rules", "Player Stats"],
            ["ACT_SetPlayerStatText"] = ["Game Rules", "Player Stats"],
            ["COND_PlayerStatAtLeast"] = ["Game Rules", "Player Stats"],
            ["PROP_PlayerStatValue"] = ["Game Rules", "Player Stats"],
            ["PROP_PlayerStatDisplayValue"] = ["Game Rules", "Player Stats"],
            ["PROP_StatDisplayName"] = ["Game Rules", "Player Stats"],
            ["PROP_TeamStatTotal"] = ["Game Rules", "Player Stats"],
            ["PROP_AllPlayerStats"] = ["Game Rules", "Player Stats"]
        };

        foreach (var (id, expectedPath) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, node.ApiSurface);
            Assert.NotEmpty(node.ApiReferences);
            Assert.Equal(expectedPath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
        }

        var statTargetNodes = expectedIds.Keys
            .Where(id => id != "PROP_AllPlayerStats")
            .Select(id => catalog.Nodes.Single(entry => entry.IdBase == id));
        foreach (var node in statTargetNodes)
        {
            var statTarget = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal(["StatObject"], statTarget.AcceptedObjectGroups);
        }

        var teamTotal = catalog.Nodes.Single(entry => entry.IdBase == "PROP_TeamStatTotal");
        var team = Assert.Single(teamTotal.Parameters, parameter => parameter.Key == "team");
        Assert.Equal(["TeamObject"], team.AcceptedObjectGroups);
    }

    [Fact]
    public void SavedDataApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, string[]>
        {
            ["ACT_SaveDatastoreValue"] = ["Game Rules", "Saved Data"],
            ["ACT_RemoveDatastoreValue"] = ["Game Rules", "Saved Data"],
            ["PROP_DatastoreValue"] = ["Game Rules", "Saved Data"],
            ["PROP_DatastoreKey"] = ["Game Rules", "Saved Data"]
        };

        foreach (var (id, expectedPath) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, node.ApiSurface);
            Assert.NotEmpty(node.ApiReferences);
            Assert.Equal(expectedPath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
        }
    }

    [Fact]
    public void CoreUiApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new[]
        {
            "ACT_SetBuiltInUIVisible",
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

        foreach (var id in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.Equal(["UI & Feedback", "Built-In UI"], node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Local));
            Assert.False(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Server));
            Assert.NotEmpty(node.ApiReferences);
            Assert.All(node.ApiReferences, reference => Assert.Equal("CoreUIService", reference.Type));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
        }
    }

    [Fact]
    public void CustomUiApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, string>
        {
            ["EV_OnUIButtonClicked"] = "UIButton2D",
            ["ACT_SetUIText"] = "UILabelObject",
            ["ACT_SetUIColor"] = "UIObject",
            ["ACT_SetUITextWrapped"] = "UILabelObject",
            ["COND_UITextIs"] = "UILabelObject",
            ["COND_UITextIsEmpty"] = "UILabelObject",
            ["COND_UITextWrapped"] = "UILabelObject",
            ["PROP_UIText"] = "UILabelObject",
            ["PROP_UIColor"] = "UIObject",
            ["PROP_UIFontSize"] = "UILabelObject",
            ["PROP_UITextWrapped"] = "UILabelObject",
            ["PROP_PlayerUIRoot"] = ""
        };

        foreach (var (id, expectedGroup) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(["UI & Feedback", "Custom UI"], node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);

            if (id == "PROP_PlayerUIRoot")
            {
                Assert.Empty(node.Parameters);
                continue;
            }

            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal([expectedGroup], target.AcceptedObjectGroups);
            Assert.Equal(["PlayerGUI", "CoreUI"], target.AcceptedSceneRoots);
            if (node.Kind == NodeKind.Trigger)
            {
                Assert.Equal("Self", target.Default);
                Assert.Equal("Target Context", target.ValueSource);
            }
        }
    }

    [Fact]
    public void CustomUiConditionExpansion_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, (string[] PalettePath, string ExpectedGroup)>
        {
            ["COND_UIVisible"] = (["UI & Feedback", "UI"], "UIObject"),
            ["COND_UIHidden"] = (["UI & Feedback", "UI"], "UIObject"),
            ["COND_UIImageIs"] = (["UI & Feedback", "UI"], "UIObject"),
            ["COND_UIImageHasImage"] = (["UI & Feedback", "UI"], "UIObject"),
            ["COND_TextInputTextIs"] = (["UI & Feedback", "Text Input"], "UITextInputObject"),
            ["COND_TextInputIsEmpty"] = (["UI & Feedback", "Text Input"], "UITextInputObject"),
            ["COND_TextInputReadOnly"] = (["UI & Feedback", "Text Input"], "UITextInputObject"),
            ["COND_TextInputEditable"] = (["UI & Feedback", "Text Input"], "UITextInputObject"),
            ["COND_UIFieldIgnoresMouse"] = (["UI & Feedback", "UI Field"], "UIFieldObject"),
            ["COND_UIFieldClipsChildren"] = (["UI & Feedback", "UI Field"], "UIFieldObject"),
            ["COND_Gui3DShaded"] = (["UI & Feedback", "3D UI"], "GUI3DObject"),
            ["COND_Gui3DFacesCamera"] = (["UI & Feedback", "3D UI"], "GUI3DObject"),
            ["COND_Gui3DTransparent"] = (["UI & Feedback", "3D UI"], "GUI3DObject"),
            ["COND_GridColumnsAtLeast"] = (["UI & Feedback", "UI Layout"], "UIGridLayoutObject"),
            ["COND_ScrollViewHorizontalModeIs"] = (["UI & Feedback", "Scroll View"], "UIScrollViewObject")
        };

        foreach (var (id, expected) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal(NodeKind.Condition, node.Kind);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(expected.PalettePath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.ApiReferences);
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);

            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Contains(expected.ExpectedGroup, target.AcceptedObjectGroups);
        }
    }

    [Fact]
    public void CustomUiTriggerExpansion_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, (string[] PalettePath, string ExpectedGroup)>
        {
            ["EV_OnUIBecameVisible"] = (["UI & Feedback", "UI Changes"], "UIObject"),
            ["EV_OnUIBecameHidden"] = (["UI & Feedback", "UI Changes"], "UIObject"),
            ["EV_OnUIImageChanged"] = (["UI & Feedback", "UI Changes"], "UIObject"),
            ["EV_OnTextInputBecameEmpty"] = (["UI & Feedback", "Text Input Changes"], "UITextInputObject"),
            ["EV_OnTextInputNoLongerEmpty"] = (["UI & Feedback", "Text Input Changes"], "UITextInputObject"),
            ["EV_OnTextInputBecameReadOnly"] = (["UI & Feedback", "Text Input Changes"], "UITextInputObject"),
            ["EV_OnTextInputBecameEditable"] = (["UI & Feedback", "Text Input Changes"], "UITextInputObject"),
            ["EV_OnUIFieldStartedIgnoringMouse"] = (["UI & Feedback", "UI Field Changes"], "UIFieldObject"),
            ["EV_OnUIFieldStoppedIgnoringMouse"] = (["UI & Feedback", "UI Field Changes"], "UIFieldObject"),
            ["EV_OnUIFieldStartedClippingChildren"] = (["UI & Feedback", "UI Field Changes"], "UIFieldObject"),
            ["EV_OnUIFieldStoppedClippingChildren"] = (["UI & Feedback", "UI Field Changes"], "UIFieldObject"),
            ["EV_OnGui3DShadedEnabled"] = (["UI & Feedback", "3D UI Changes"], "GUI3DObject"),
            ["EV_OnGui3DShadedDisabled"] = (["UI & Feedback", "3D UI Changes"], "GUI3DObject"),
            ["EV_OnGui3DFaceCameraEnabled"] = (["UI & Feedback", "3D UI Changes"], "GUI3DObject"),
            ["EV_OnGui3DFaceCameraDisabled"] = (["UI & Feedback", "3D UI Changes"], "GUI3DObject"),
            ["EV_OnGui3DTransparentEnabled"] = (["UI & Feedback", "3D UI Changes"], "GUI3DObject"),
            ["EV_OnGui3DTransparentDisabled"] = (["UI & Feedback", "3D UI Changes"], "GUI3DObject"),
            ["EV_OnGridColumnsReached"] = (["UI & Feedback", "UI Layout Changes"], "UIGridLayoutObject"),
            ["EV_OnScrollViewHorizontalModeChanged"] = (["UI & Feedback", "Scroll View Changes"], "UIScrollViewObject")
        };

        foreach (var (id, expected) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal(NodeKind.Trigger, node.Kind);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(expected.PalettePath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.ApiReferences);
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);

            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Contains(expected.ExpectedGroup, target.AcceptedObjectGroups);
            Assert.Contains(node.Parameters, parameter => parameter.Key == "interval");
        }
    }

    [Fact]
    public void TextInputApiPack_LoadsReadyAnnotatedGameplayNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new[]
        {
            "EV_OnTextInputChanged",
            "EV_OnTextInputSubmitted",
            "ACT_SetTextInputText",
            "ACT_SetTextInputPlaceholder",
            "ACT_SetTextInputReadOnly",
            "ACT_FocusTextInput",
            "PROP_TextInputText",
            "PROP_TextInputPlaceholder",
            "PROP_TextInputReadOnly"
        };

        foreach (var id in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(["UI & Feedback", "Text Input"], node.PalettePath);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Local));
            Assert.False(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Server));
            Assert.NotEmpty(node.ApiReferences);
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);

            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal(["UITextInputObject"], target.AcceptedObjectGroups);
            Assert.Equal(["PlayerGUI", "CoreUI"], target.AcceptedSceneRoots);
        }
    }

    [Fact]
    public void UiFieldAndScrollViewApiPack_LoadsReadyAnnotatedGameplayNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedGroups = new Dictionary<string, string>
        {
            ["ACT_SetUIFieldZIndex"] = "UIFieldObject",
            ["ACT_SetUIFieldIgnoresMouse"] = "UIFieldObject",
            ["ACT_SetUIFieldClipDescendants"] = "UIFieldObject",
            ["ACT_SetUIFieldRotation"] = "UIFieldObject",
            ["ACT_SetUIFieldScale"] = "UIFieldObject",
            ["PROP_UIFieldZIndex"] = "UIFieldObject",
            ["PROP_UIFieldIgnoresMouse"] = "UIFieldObject",
            ["PROP_UIFieldClipDescendants"] = "UIFieldObject",
            ["PROP_UIFieldRotation"] = "UIFieldObject",
            ["PROP_UIFieldScale"] = "UIFieldObject",
            ["ACT_SetScrollViewMode"] = "UIScrollViewObject",
            ["PROP_ScrollViewHorizontalMode"] = "UIScrollViewObject",
            ["PROP_ScrollViewVerticalMode"] = "UIScrollViewObject"
        };

        foreach (var (id, expectedGroup) in expectedGroups)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Local));
            Assert.False(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Server));
            Assert.NotEmpty(node.ApiReferences);
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);

            var expectedPath = expectedGroup == "UIScrollViewObject"
                ? new[] { "UI & Feedback", "Scroll View" }
                : new[] { "UI & Feedback", "UI Field" };
            Assert.Equal(expectedPath, node.PalettePath);
            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal([expectedGroup], target.AcceptedObjectGroups);
            Assert.Equal(["PlayerGUI", "CoreUI"], target.AcceptedSceneRoots);
        }
    }

    [Fact]
    public void UiLayoutAndGui3DApiPack_LoadsReadyAnnotatedGameplayNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedGroups = new Dictionary<string, string>
        {
            ["ACT_CreateUIContainer"] = "Any",
            ["ACT_SetGridLayoutColumns"] = "UIGridLayoutObject",
            ["ACT_SetGridLayoutSpacing"] = "UIGridLayoutObject",
            ["PROP_GridLayoutColumns"] = "UIGridLayoutObject",
            ["PROP_GridLayoutSpacing"] = "UIGridLayoutObject",
            ["ACT_SetLayoutSpacing"] = "UIHVLayoutObject",
            ["ACT_SetLayoutChildAlignment"] = "UIHVLayoutObject",
            ["PROP_LayoutSpacing"] = "UIHVLayoutObject",
            ["PROP_LayoutChildAlignment"] = "UIHVLayoutObject",
            ["ACT_SetGui3DShaded"] = "GUI3DObject",
            ["ACT_SetGui3DFaceCamera"] = "GUI3DObject",
            ["ACT_SetGui3DTransparent"] = "GUI3DObject",
            ["PROP_Gui3DShaded"] = "GUI3DObject",
            ["PROP_Gui3DFaceCamera"] = "GUI3DObject",
            ["PROP_Gui3DTransparent"] = "GUI3DObject"
        };

        foreach (var (id, expectedGroup) in expectedGroups)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Local));
            Assert.False(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Server));
            Assert.NotEmpty(node.ApiReferences);
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);

            var expectedPath = expectedGroup == "GUI3DObject"
                ? new[] { "UI & Feedback", "3D UI" }
                : new[] { "UI & Feedback", "UI Layout" };
            Assert.Equal(expectedPath, node.PalettePath);
            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            if (id == "ACT_CreateUIContainer")
            {
                Assert.Equal(["UIFieldObject", "Any"], target.AcceptedObjectGroups);
                Assert.Contains(node.ApiReferences, reference => reference.Type == "UIHLayout");
                Assert.Contains(node.ApiReferences, reference => reference.Type == "UIVLayout");
                Assert.Contains(node.ApiReferences, reference => reference.Type == "UIViewport");
            }
            else
            {
                Assert.Equal([expectedGroup], target.AcceptedObjectGroups);
            }

            if (expectedGroup == "GUI3DObject")
            {
                Assert.Equal(["World", "PlayerGUI", "CoreUI"], target.AcceptedSceneRoots);
            }
            else
            {
                Assert.Equal(["PlayerGUI", "CoreUI"], target.AcceptedSceneRoots);
            }
        }
    }

    [Fact]
    public void IntegerAndInstanceValueApiPack_LoadsReadyAnnotatedGameplayNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedGroups = new Dictionary<string, string>
        {
            ["ACT_SetIntegerValueObject"] = "IntValueObject",
            ["PROP_IntegerValueObject"] = "IntValueObject",
            ["ACT_SetInstanceValueObject"] = "InstanceValueObject",
            ["PROP_InstanceValueObject"] = "InstanceValueObject"
        };

        foreach (var (id, expectedGroup) in expectedGroups)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Server));
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Local));
            Assert.NotEmpty(node.ApiReferences);
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
            Assert.Equal(["Game Rules", "Stored Values"], node.PalettePath);

            var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
            Assert.Equal([expectedGroup], target.AcceptedObjectGroups);
        }

        var setInstance = catalog.Nodes.Single(entry => entry.IdBase == "ACT_SetInstanceValueObject");
        var storedObject = Assert.Single(setInstance.Parameters, parameter => parameter.Key == "value");
        Assert.Equal(["Any"], storedObject.AcceptedObjectGroups);
    }

    [Fact]
    public void WorldInfoApiPack_LoadsReadyAnnotatedGameplayNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new[]
        {
            "PROP_WorldIsLocalTest",
            "PROP_WorldIsOldFormat",
            "PROP_WorldIdentifier",
            "PROP_ServerIdentifier",
            "PROP_WorldUptime",
            "PROP_ServerTime",
            "PROP_WorldObjectCount"
        };

        foreach (var id in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.Equal(["Game Rules", "World Info"], node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Server));
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Local));
            Assert.Empty(node.Parameters);
            Assert.NotEmpty(node.ApiReferences);
            Assert.All(node.ApiReferences, reference => Assert.Equal("World", reference.Type));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
        }
    }

    [Fact]
    public void EnvironmentBoundsApiPack_LoadsReadyAnnotatedGameplayNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, (string[] Path, string? TargetGroup)>
        {
            ["ACT_SetWorldGravity"] = (["Game Rules", "Environment"], null),
            ["ACT_SetPartDestroyHeight"] = (["Game Rules", "Environment"], null),
            ["ACT_SetAutoGenerateNavMesh"] = (["Game Rules", "Environment"], null),
            ["ACT_RebuildNavMesh"] = (["Game Rules", "Environment"], null),
            ["PROP_WorldGravity"] = (["Game Rules", "Environment"], null),
            ["PROP_PartDestroyHeight"] = (["Game Rules", "Environment"], null),
            ["PROP_AutoGenerateNavMesh"] = (["Game Rules", "Environment"], null),
            ["PROP_CurrentCamera"] = (["Game Rules", "Environment"], null),
            ["PROP_ObjectBoundsCenter"] = (["Scene Object", "Bounds"], "PartLike"),
            ["PROP_ObjectBoundsSize"] = (["Scene Object", "Bounds"], "PartLike"),
            ["PROP_ObjectBoundsExtents"] = (["Scene Object", "Bounds"], "PartLike"),
            ["PROP_ObjectBoundsVolume"] = (["Scene Object", "Bounds"], "PartLike"),
            ["COND_ObjectBoundsContainsPoint"] = (["Scene Object", "Bounds"], "PartLike")
        };

        foreach (var (id, expected) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.Equal(expected.Path, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.ApiReferences);
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);

            if (expected.TargetGroup is not null)
            {
                var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
                Assert.Equal([expected.TargetGroup], target.AcceptedObjectGroups);
            }
        }
    }

    [Fact]
    public void Vector2ApiPack_LoadsReadyAnnotatedGameplayNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new[]
        {
            "PROP_Vector2FromXY",
            "PROP_Vector2X",
            "PROP_Vector2Y",
            "PROP_Vector2Magnitude",
            "PROP_Vector2Normalized",
            "PROP_Vector2Distance",
            "PROP_Vector2Lerp",
            "COND_Vector2DistanceAtMost"
        };

        foreach (var id in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.Equal(["Math", "Vector"], node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.ApiReferences);
            Assert.All(node.ApiReferences, reference => Assert.Equal("Vector2", reference.Type));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
        }
    }

    [Fact]
    public void RaycastApiPack_LoadsReadyAnnotatedGameplayNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new[]
        {
            "COND_RaycastHits",
            "PROP_RaycastResult",
            "PROP_RaycastHitObject",
            "PROP_RaycastHitPosition",
            "PROP_RaycastHitNormal",
            "PROP_RaycastHitDistance"
        };

        foreach (var id in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.Equal(["Scene Object", "Raycast"], node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.ApiReferences);
            Assert.Contains(node.ApiReferences, reference => reference.Type == "Environment" && reference.Member == "Raycast");
            Assert.Contains(node.ApiReferences, reference => reference.Type == "RayResult");
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Server));
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Local));
        }
    }

    [Fact]
    public void QuaternionApiPack_LoadsReadyAnnotatedGameplayNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new[]
        {
            "PROP_QuaternionIdentity",
            "PROP_QuaternionFromComponents",
            "PROP_QuaternionFromEuler",
            "PROP_QuaternionToEuler",
            "PROP_QuaternionFromAxisAngle",
            "PROP_QuaternionLookRotation",
            "PROP_QuaternionFromToRotation",
            "PROP_QuaternionInverse",
            "PROP_QuaternionNormalize",
            "PROP_QuaternionLerp",
            "PROP_QuaternionSlerp",
            "PROP_QuaternionRotateTowards",
            "PROP_QuaternionAngle",
            "PROP_QuaternionDot",
            "COND_QuaternionAngleAtMost"
        };

        foreach (var id in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.Equal(["Math", "Rotation"], node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.ApiReferences);
            Assert.All(node.ApiReferences, reference => Assert.Equal("Quaternion", reference.Type));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Server));
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Local));
        }
    }

    [Fact]
    public void ColorSeriesApiPack_LoadsReadyAnnotatedGameplayNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new[]
        {
            "PROP_ColorSeriesFromColors",
            "PROP_ColorFromColorSeries",
            "PROP_ColorSeriesPointCount",
            "PROP_ColorSeriesPointColor",
            "PROP_ColorSeriesPointOffset"
        };

        foreach (var id in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.Equal(["Math", "Color Series"], node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.ApiReferences);
            Assert.All(node.ApiReferences, reference => Assert.Equal("ColorSeries", reference.Type));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Server));
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Local));
        }
    }

    [Fact]
    public void BindableEventApiPack_LoadsReadyAnnotatedGameplayNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new[]
        {
            "EV_OnBindableEvent",
            "ACT_FireBindableEvent",
            "PROP_TriggeringBindablePayload"
        };

        foreach (var id in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.Equal(["Flow & Timing", "Local Events"], node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.ApiReferences);
            Assert.All(node.ApiReferences, reference => Assert.Equal("BindableEvent", reference.Type));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Server));
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Local));
        }
    }

    [Fact]
    public void WorldContainerApiPack_LoadsReadyAnnotatedGameplayNode()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var node = catalog.Nodes.Single(entry => entry.IdBase == "ACT_CreateSceneContainer");

        Assert.Equal("Ready", node.Status);
        Assert.Equal("UserFacing", node.Surface);
        Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
        Assert.Equal(["Scene Object", "Hierarchy"], node.PalettePath);
        Assert.True(NodeCatalogService.IsAddable(node));
        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Server));
        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Local));
        Assert.NotEmpty(node.BeginnerSummary);
        Assert.NotEmpty(node.SearchKeywords);
        Assert.Contains(node.ApiReferences, reference => reference.Type == "Instance" && reference.Member == "New");
        Assert.Contains(node.ApiReferences, reference => reference.Type == "Folder");
        Assert.Contains(node.ApiReferences, reference => reference.Type == "Model");

        var target = Assert.Single(node.Parameters, parameter => parameter.Key == "target");
        Assert.Equal(["Any"], target.AcceptedObjectGroups);
        Assert.Equal(["World"], target.AcceptedSceneRoots);
    }

    [Fact]
    public void InventoryApiPack_LoadsReadyAnnotatedGameplayNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new[]
        {
            "PROP_PlayerInventory",
            "PROP_FindToolInInventory",
            "COND_PlayerHasTool",
            "ACT_GiveToolToPlayer"
        };

        foreach (var id in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.Equal(["Players", "Inventory"], node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.ApiReferences);
            Assert.Contains(node.ApiReferences, reference => reference.Type == "Inventory");
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
        }

        var inventoryValue = catalog.Nodes.Single(entry => entry.IdBase == "PROP_PlayerInventory");
        Assert.Equal("SceneObject", NodeCatalogService.CreateNode(inventoryValue).Ports.Single(port => port.Id == GraphPortDefaults.ValueOut).DataType);
    }

    [Fact]
    public void ScriptSharedTableApiPack_LoadsReadyAnnotatedGameplayNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new[]
        {
            "ACT_SetSharedValue",
            "ACT_IncrementSharedNumber",
            "ACT_AppendSharedText",
            "ACT_RemoveSharedValue",
            "ACT_ClearSharedValues",
            "ACT_ClearSharedPrefix",
            "ACT_ClearSharedSuffix",
            "COND_SharedValueExists",
            "COND_SharedNumberAtLeast",
            "COND_SharedValueMissing",
            "COND_SharedNumberEquals",
            "COND_SharedNumberAtMost",
            "COND_SharedTextEquals",
            "COND_SharedTextContains",
            "EV_OnSharedValueChanged",
            "EV_OnSharedValueExists",
            "EV_OnSharedValueRemoved",
            "EV_OnSharedNumberReachedAtLeast",
            "EV_OnSharedNumberDroppedToAtMost",
            "EV_OnSharedTextBecame",
            "EV_OnSharedTextContains",
            "PROP_ReadSharedValue",
            "PROP_ReadSharedNumber",
            "PROP_ReadSharedText"
        };

        foreach (var id in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.Equal(["Variables", "Shared"], node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.ApiReferences);
            Assert.Contains(node.ApiReferences, reference => reference.Type == "ScriptSharedTable");
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Server));
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Local));
        }

        var readValue = catalog.Nodes.Single(entry => entry.IdBase == "PROP_ReadSharedValue");
        var readNumber = catalog.Nodes.Single(entry => entry.IdBase == "PROP_ReadSharedNumber");
        var readText = catalog.Nodes.Single(entry => entry.IdBase == "PROP_ReadSharedText");
        Assert.Equal("Any", NodeCatalogService.CreateNode(readValue).Ports.Single(port => port.Id == GraphPortDefaults.ValueOut).DataType);
        Assert.Equal("Number", NodeCatalogService.CreateNode(readNumber).Ports.Single(port => port.Id == GraphPortDefaults.ValueOut).DataType);
        Assert.Equal("String", NodeCatalogService.CreateNode(readText).Ports.Single(port => port.Id == GraphPortDefaults.ValueOut).DataType);
    }

    [Fact]
    public void ScriptRuntimeApiPack_LoadsReadyAnnotatedGameplayNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var scriptIds = new[]
        {
            "ACT_SetScriptEnabled",
            "ACT_EnableScript",
            "ACT_DisableScript",
            "ACT_ToggleScriptEnabled",
            "ACT_CallScriptFunction",
            "ACT_CallScriptFunctionAsync",
            "COND_ScriptIsEnabled",
            "COND_ScriptIsDisabled",
            "COND_ScriptCanCallFunction",
            "COND_ScriptCanCallAsyncFunction",
            "COND_ScriptTargetExists",
            "COND_ScriptTargetMissing",
            "EV_OnScriptEnabled",
            "EV_OnScriptDisabled",
            "EV_OnScriptEnabledChanged",
            "EV_OnScriptCallAvailable",
            "EV_OnScriptCallAsyncAvailable",
            "EV_OnScriptTargetMissing",
            "PROP_ScriptEnabled"
        };

        foreach (var id in scriptIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.Equal(["Scene Object", "Scripts"], node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.Contains(node.ApiReferences, reference => reference.Type == "Script");
            Assert.Contains(node.ApiReferences, reference => reference.Type == "ServerScript");
            Assert.Contains(node.ApiReferences, reference => reference.Type == "ClientScript");
            Assert.Contains(node.ApiReferences, reference => reference.Type == "ModuleScript");
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Server));
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Local));

            var target = node.Parameters.FirstOrDefault(parameter => parameter.Key == "target");
            Assert.NotNull(target);
            Assert.Contains("ScriptObject", target.AcceptedObjectGroups);
        }

        var scriptEnabled = catalog.Nodes.Single(entry => entry.IdBase == "PROP_ScriptEnabled");
        Assert.Equal("Boolean", NodeCatalogService.CreateNode(scriptEnabled).Ports.Single(port => port.Id == GraphPortDefaults.ValueOut).DataType);
    }

    [Fact]
    public void MissingInstanceValidationPack_LoadsReadyAnnotatedGameplayNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new[] { "COND_ObjectIsMissingInstance", "PROP_ObjectIsMissingInstance" };

        foreach (var id in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.Equal(["Scene Object", "Validation"], node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.Contains(node.ApiReferences, reference => reference.Type == "MissingInstance");
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);

            var target = node.Parameters.Single(parameter => parameter.Key == "target");
            Assert.Contains("Any", target.AcceptedObjectGroups);
        }

        var missingValue = catalog.Nodes.Single(entry => entry.IdBase == "PROP_ObjectIsMissingInstance");
        Assert.Equal("Boolean", NodeCatalogService.CreateNode(missingValue).Ports.Single(port => port.Id == GraphPortDefaults.ValueOut).DataType);
    }

    [Fact]
    public void AssetMediaApiPack_LoadsReadyAnnotatedGameplayNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new[]
        {
            "PROP_AssetReference",
            "PROP_ResourceAssetReference",
            "PROP_FontAssetReference",
            "ACT_SetPTImageAssetId",
            "PROP_PTImageAssetId",
            "ACT_SetPTAudioAssetId",
            "PROP_PTAudioAssetId",
            "ACT_SetPTMeshAssetId",
            "PROP_PTMeshAssetId",
            "ACT_SetPTMeshAnimationAssetId",
            "PROP_PTMeshAnimationAssetId",
            "ACT_SetMeshAnimationType",
            "PROP_MeshAnimationInfoName",
            "ACT_SetBuiltInAudioPreset",
            "PROP_BuiltInAudioPreset",
            "ACT_SetBuiltInFontSettings",
            "PROP_BuiltInFontPreset",
            "ACT_SetFileLinkAssetId",
            "PROP_FileLinkAssetId",
            "ACT_SetGradientImageSize",
            "PROP_GradientImageWidth",
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
            "COND_GradientImageHeightAtLeast",
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

        var coveredTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(NodeApiSurface.Gameplay, NodeCatalogApiSurfaceService.GetEntrySurface(node));
            Assert.Equal("Assets", node.PalettePath.First());
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.ApiReferences);
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Server));
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Local));

            foreach (var reference in node.ApiReferences)
            {
                coveredTypes.Add(reference.Type);
            }
        }

        foreach (var requiredType in new[]
        {
            "BaseAsset",
            "BuiltInAudioAsset",
            "BuiltInFontAsset",
            "FileLinkAsset",
            "FontAsset",
            "GradientImageAsset",
            "MeshAnimationAsset",
            "MeshAnimationInfo",
            "PTAudioAsset",
            "PTImageAsset",
            "PTMeshAnimationAsset",
            "PTMeshAsset",
            "ResourceAsset"
        })
        {
            Assert.Contains(requiredType, coveredTypes);
        }

        var assetReference = catalog.Nodes.Single(entry => entry.IdBase == "PROP_AssetReference");
        var imageId = catalog.Nodes.Single(entry => entry.IdBase == "PROP_PTImageAssetId");
        var audioPreset = catalog.Nodes.Single(entry => entry.IdBase == "PROP_BuiltInAudioPreset");
        Assert.Equal("BaseAsset", NodeCatalogService.CreateNode(assetReference).Ports.Single(port => port.Id == GraphPortDefaults.ValueOut).DataType);
        Assert.Equal("Number", NodeCatalogService.CreateNode(imageId).Ports.Single(port => port.Id == GraphPortDefaults.ValueOut).DataType);
        Assert.Equal("String", NodeCatalogService.CreateNode(audioPreset).Ports.Single(port => port.Id == GraphPortDefaults.ValueOut).DataType);

        var meshType = catalog.Nodes.Single(entry => entry.IdBase == "ACT_SetMeshAnimationType");
        Assert.Contains("OneShotImpluse", meshType.Parameters.Single(parameter => parameter.Key == "animationType").Options);
    }

    [Fact]
    public void ToolApiPack_LoadsReadyBeginnerNodes()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new[]
        {
            "EV_OnToolEquipped",
            "EV_OnToolUnequipped",
            "EV_OnToolActivated",
            "EV_OnToolDeactivated",
            "ACT_ActivateTool",
            "ACT_DeactivateTool",
            "ACT_PlayToolAnimation",
            "ACT_SetToolDroppable",
            "COND_ToolCanBeDropped",
            "COND_ToolIsHeld",
            "PROP_ToolHolder",
            "PROP_ToolCanBeDropped"
        };

        foreach (var id in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(["Players", "Tools"], node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
        }
    }

    [Fact]
    public void PolytoriaEssentialsPack_LoadsRunnableNodeBatchWithAliases()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedEssentialsIds = new[]
        {
            "ACT_StartCooldown",
            "ACT_ResetCooldown",
            "COND_CooldownReady",
            "PROP_CooldownRemainingSeconds",
            "ACT_OpenGate",
            "ACT_CloseGate",
            "ACT_ToggleGate",
            "COND_GateIsOpen",
            "ACT_StartRound",
            "ACT_EndRound",
            "ACT_SetRoundTime",
            "COND_RoundIsRunning",
            "COND_RoundTimeExpired",
            "PROP_RoundTimeRemaining",
            "ACT_SetPlayerScore",
            "ACT_AddPlayerScore",
            "PROP_PlayerScore",
            "COND_PlayerScoreAtLeast",
            "ACT_SetPlayerLives",
            "ACT_AddPlayerLives",
            "PROP_PlayerLives",
            "COND_PlayerLivesAtLeast",
            "ACT_SetPlayerTeam",
            "PROP_PlayerTeam",
            "COND_PlayerTeamIs",
            "ACT_SetTeamScore",
            "ACT_AddTeamScore",
            "PROP_TeamScore",
            "COND_TeamScoreAtLeast",
            "PROP_DistanceBetweenObjects",
            "PROP_DistanceBetweenPositions",
            "PROP_PercentNumber",
            "PROP_MapNumberRange",
            "PROP_TimeNowSeconds"
        };

        foreach (var id in expectedEssentialsIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal("Essentials", node.ApiGroup);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.PalettePath);
            Assert.NotEmpty(node.PaletteAliases);
            Assert.Contains(node.PaletteAliases, alias =>
                alias.Count >= 2 &&
                alias[0].Equals("Essentials", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Gc2InspiredBeginnerPack_LoadsSmallReadyNodeBatch()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, string[]>
        {
            ["PROP_ChooseNumber"] = ["Math", "Choice"],
            ["PROP_ChooseText"] = ["Text", "Convert"],
            ["PROP_ChooseObject"] = ["Scene Object", "Lookup"],
            ["PROP_RandomTrueOrFalse"] = ["Logic", "Boolean"],
            ["PROP_RandomWholeNumber"] = ["Math", "Random"],
            ["PROP_RandomNumberChoice"] = ["Math", "Random"],
            ["PROP_RandomTextChoice"] = ["Text", "Choice"],
            ["PROP_AverageNumber"] = ["Math", "Arithmetic"],
            ["PROP_SquareRootNumber"] = ["Math", "Arithmetic"],
            ["PROP_NumberFromText"] = ["Text", "Convert"],
            ["PROP_TextBefore"] = ["Text", "Extract"],
            ["PROP_TextAfter"] = ["Text", "Extract"],
            ["PROP_TextBetween"] = ["Text", "Extract"],
            ["PROP_FirstTextCharacters"] = ["Text", "Extract"],
            ["PROP_LastTextCharacters"] = ["Text", "Extract"],
            ["PROP_ObjectVisibleValue"] = ["Scene Object", "Visibility"],
            ["PROP_ObjectCollisionValue"] = ["Scene Object", "Collision"],
            ["PROP_ObjectAnchoredValue"] = ["Scene Object", "Physics"],
            ["PROP_ObjectTransparency"] = ["Scene Object", "Transparency"],
            ["PROP_ObjectXPosition"] = ["Scene Object", "Position"],
            ["PROP_ObjectHeightPosition"] = ["Scene Object", "Height"],
            ["PROP_ObjectZPosition"] = ["Scene Object", "Position"],
            ["PROP_ObjectTurnAngle"] = ["Scene Object", "Rotation"],
            ["PROP_ObjectWidthSize"] = ["Scene Object", "Dimensions"],
            ["PROP_ObjectHeightSize"] = ["Scene Object", "Dimensions"],
            ["PROP_ObjectDepthSize"] = ["Scene Object", "Dimensions"],
            ["COND_ScriptNumberIsAtMost"] = ["Variables", "Script Numbers"],
            ["COND_ScriptNumberEquals"] = ["Variables", "Script Numbers"],
            ["COND_StateIsFalse"] = ["Variables", "State"],
            ["COND_NumberIsAtLeast"] = ["Math", "Comparison"],
            ["COND_NumberIsAtMost"] = ["Math", "Comparison"],
            ["COND_NumberEquals"] = ["Math", "Comparison"],
            ["COND_NumberIsOdd"] = ["Math", "Comparison"],
            ["COND_NumberIsNegative"] = ["Math", "Comparison"],
            ["COND_NumberOutsideRange"] = ["Math", "Range"],
            ["COND_TextIsEmpty"] = ["Text", "Empty"],
            ["COND_TextIsNotEmpty"] = ["Text", "Empty"],
            ["COND_ObjectHasChildren"] = ["Scene Object", "Children"],
            ["COND_ObjectHasNoChildren"] = ["Scene Object", "Children"],
            ["COND_ObjectChildCountAtLeast"] = ["Scene Object", "Children"],
            ["COND_ObjectChildCountAtMost"] = ["Scene Object", "Children"],
            ["COND_ObjectParentIs"] = ["Scene Object", "Parent"],
            ["COND_ObjectIsUnderObject"] = ["Scene Object", "Parent"],
            ["COND_ObjectIsCloseToObject"] = ["Math", "Geometry"],
            ["COND_ObjectIsFarFromObject"] = ["Math", "Geometry"],
            ["COND_ObjectIsVisible"] = ["Scene Object", "Visibility"],
            ["COND_ObjectIsHidden"] = ["Scene Object", "Visibility"],
            ["COND_ObjectCollisionIsOn"] = ["Scene Object", "Collision"],
            ["COND_ObjectCollisionIsOff"] = ["Scene Object", "Collision"],
            ["COND_ObjectIsAnchored"] = ["Scene Object", "Physics"],
            ["COND_ObjectIsUnanchored"] = ["Scene Object", "Physics"],
            ["COND_ObjectIsAboveHeight"] = ["Scene Object", "Height"],
            ["COND_ObjectIsBelowHeight"] = ["Scene Object", "Height"],
            ["COND_ObjectTurnAngleAtLeast"] = ["Scene Object", "Rotation"],
            ["COND_ObjectTurnAngleAtMost"] = ["Scene Object", "Rotation"],
            ["COND_ObjectSizeAtLeast"] = ["Scene Object", "Dimensions"],
            ["COND_ObjectSizeAtMost"] = ["Scene Object", "Dimensions"],
            ["COND_TextIsANumber"] = ["Text", "Convert"],
            ["COND_TextHasAtLeastCharacters"] = ["Text", "Length"],
            ["COND_TextHasAtMostCharacters"] = ["Text", "Length"],
            ["COND_NumberIsZero"] = ["Math", "Comparison"],
            ["COND_PlayerHasNoLivesLeft"] = ["Players", "Lives"],
            ["COND_PlayerScoreAtMost"] = ["Players", "Score"],
            ["COND_PlayerScoreEquals"] = ["Players", "Score"],
            ["COND_PlayerLivesAtMost"] = ["Players", "Lives"],
            ["COND_PlayerLivesEquals"] = ["Players", "Lives"],
            ["COND_PlayerCountAtMost"] = ["Players", "Count"],
            ["COND_RoundHasTimeLeft"] = ["Game Rules", "Round"],
            ["COND_TeamScoreAtMost"] = ["Game Rules", "Team Score"],
            ["ACT_ShowObject"] = ["Scene Object", "Visibility"],
            ["ACT_HideObject"] = ["Scene Object", "Visibility"],
            ["ACT_ToggleObjectVisibility"] = ["Scene Object", "Visibility"],
            ["ACT_TurnObjectCollisionOn"] = ["Scene Object", "Collision"],
            ["ACT_TurnObjectCollisionOff"] = ["Scene Object", "Collision"],
            ["ACT_ToggleObjectCollision"] = ["Scene Object", "Collision"],
            ["ACT_ToggleObjectAnchored"] = ["Scene Object", "Physics"],
            ["ACT_SetObjectXPosition"] = ["Scene Object", "Position"],
            ["ACT_SetObjectHeightPosition"] = ["Scene Object", "Height"],
            ["ACT_SetObjectZPosition"] = ["Scene Object", "Position"],
            ["ACT_SetObjectTurnAngle"] = ["Scene Object", "Rotation"],
            ["ACT_TurnObjectByAngle"] = ["Scene Object", "Rotation"],
            ["ACT_SetObjectWidthSize"] = ["Scene Object", "Dimensions"],
            ["ACT_SetObjectHeightSize"] = ["Scene Object", "Dimensions"],
            ["ACT_SetObjectDepthSize"] = ["Scene Object", "Dimensions"],
            ["ACT_MoveObjectToAnotherObject"] = ["Scene Object", "Movement"],
            ["ACT_MoveObjectUp"] = ["Scene Object", "Movement"],
            ["ACT_MoveObjectDown"] = ["Scene Object", "Movement"],
            ["ACT_SetObjectParent"] = ["Scene Object", "Parent"],
            ["ACT_ResetPlayerScore"] = ["Players", "Score"],
            ["ACT_ResetPlayerLives"] = ["Players", "Lives"],
            ["ACT_ResetTeamScore"] = ["Game Rules", "Team Score"]
        };

        foreach (var (id, palettePath) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(palettePath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);
        }
    }

    [Fact]
    public void Gc2InspiredBeginnerTriggerPack_LoadsSafeReadyTriggers()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedIds = new Dictionary<string, string[]>
        {
            ["EV_AfterDelay"] = ["Flow & Timing", "Timing"],
            ["EV_OnRoundStarted"] = ["Game Rules", "Round"],
            ["EV_OnRoundEnded"] = ["Game Rules", "Round"],
            ["EV_OnRoundTimeExpired"] = ["Game Rules", "Round"],
            ["EV_OnGateOpened"] = ["Flow & Timing", "Gate"],
            ["EV_OnGateClosed"] = ["Flow & Timing", "Gate"],
            ["EV_OnObjectBecameVisible"] = ["Scene Object", "Visibility"],
            ["EV_OnObjectBecameHidden"] = ["Scene Object", "Visibility"],
            ["EV_OnObjectCollisionTurnedOn"] = ["Scene Object", "Collision"],
            ["EV_OnObjectCollisionTurnedOff"] = ["Scene Object", "Collision"],
            ["EV_OnObjectAnchored"] = ["Scene Object", "Physics"],
            ["EV_OnObjectUnanchored"] = ["Scene Object", "Physics"],
            ["EV_OnObjectsBecameClose"] = ["Scene Object", "Proximity"],
            ["EV_OnObjectsBecameFar"] = ["Scene Object", "Proximity"],
            ["EV_OnTeamScoreReached"] = ["Game Rules", "Team Score"],
            ["EV_OnTeamScoreDroppedTo"] = ["Game Rules", "Team Score"],
            ["EV_OnAnyPlayerScoreReached"] = ["Players", "Score"],
            ["EV_OnAnyPlayerHasNoLivesLeft"] = ["Players", "Lives"],
            ["EV_OnNumberReachedAtLeast"] = ["Math", "Comparison"],
            ["EV_OnNumberDroppedToAtMost"] = ["Math", "Comparison"],
            ["EV_OnNumberEnteredRange"] = ["Math", "Range"],
            ["EV_OnNumberLeftRange"] = ["Math", "Range"],
            ["EV_OnTextBecame"] = ["Text", "Changes"],
            ["EV_OnTextContains"] = ["Text", "Changes"],
            ["EV_OnBooleanBecameTrue"] = ["Logic", "Boolean"],
            ["EV_OnBooleanBecameFalse"] = ["Logic", "Boolean"],
            ["EV_OnVariableNumberReachedAtLeast"] = ["Variables", "Changes"],
            ["EV_OnVariableNumberDroppedToAtMost"] = ["Variables", "Changes"],
            ["EV_OnVariableTextBecame"] = ["Variables", "Changes"],
            ["EV_OnVariableBooleanBecameTrue"] = ["Variables", "Changes"],
            ["EV_OnVariableBooleanBecameFalse"] = ["Variables", "Changes"],
            ["EV_OnVariableBecameEmpty"] = ["Variables", "Changes"],
            ["EV_OnVariableBecameNotEmpty"] = ["Variables", "Changes"],
            ["EV_OnStateBecameTrue"] = ["Variables", "State"],
            ["EV_OnStateBecameFalse"] = ["Variables", "State"],
            ["EV_OnStateBecameEmpty"] = ["Variables", "State"],
            ["EV_OnStateBecameNotEmpty"] = ["Variables", "State"],
            ["EV_OnNumberChanged"] = ["Math", "Comparison"],
            ["EV_OnTextChanged"] = ["Text", "Changes"],
            ["EV_OnBooleanChanged"] = ["Logic", "Boolean"],
            ["EV_OnVariableNumberChanged"] = ["Variables", "Changes"],
            ["EV_OnVariableTextChanged"] = ["Variables", "Changes"],
            ["EV_OnVariableBooleanChanged"] = ["Variables", "Changes"],
            ["EV_OnPlayerCountReached"] = ["Players", "Count"],
            ["EV_OnPlayerCountDroppedTo"] = ["Players", "Count"],
            ["EV_OnEnoughPlayers"] = ["Players", "Count"],
            ["EV_OnNotEnoughPlayers"] = ["Players", "Count"],
            ["EV_OnObjectXPositionReached"] = ["Scene Object", "Position"],
            ["EV_OnObjectHeightPositionReached"] = ["Scene Object", "Height"],
            ["EV_OnObjectHeightPositionDroppedTo"] = ["Scene Object", "Height"],
            ["EV_OnObjectTransparencyReached"] = ["Scene Object", "Transparency"],
            ["EV_OnObjectTransparencyDroppedTo"] = ["Scene Object", "Transparency"],
            ["EV_OnObjectTurnAngleReached"] = ["Scene Object", "Rotation"],
            ["EV_OnObjectTurnAngleDroppedTo"] = ["Scene Object", "Rotation"],
            ["EV_OnObjectWidthSizeReached"] = ["Scene Object", "Dimensions"],
            ["EV_OnObjectWidthSizeDroppedTo"] = ["Scene Object", "Dimensions"],
            ["EV_OnObjectHeightSizeReached"] = ["Scene Object", "Dimensions"],
            ["EV_OnObjectHeightSizeDroppedTo"] = ["Scene Object", "Dimensions"],
            ["EV_OnObjectCollisionChanged"] = ["Scene Object", "Collision"],
            ["EV_OnObjectStartedMoving"] = ["Scene Object", "Movement"],
            ["EV_OnObjectStoppedMoving"] = ["Scene Object", "Movement"],
            ["EV_OnObjectSpeedReached"] = ["Scene Object", "Movement"],
            ["EV_OnObjectSpeedDroppedTo"] = ["Scene Object", "Movement"],
            ["EV_OnObjectEnteredArea"] = ["Scene Object", "Zones"],
            ["EV_OnObjectLeftArea"] = ["Scene Object", "Zones"],
            ["EV_OnObjectEnteredBoxArea"] = ["Scene Object", "Zones"],
            ["EV_OnObjectLeftBoxArea"] = ["Scene Object", "Zones"],
            ["EV_OnObjectEnteredHeightBand"] = ["Scene Object", "Zones"],
            ["EV_OnObjectLeftHeightBand"] = ["Scene Object", "Zones"]
        };

        foreach (var (id, palettePath) in expectedIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Trigger", node.Kind.ToString());
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.Equal(palettePath, node.PalettePath);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.SearchKeywords);

            var target = node.Parameters.Single(parameter => parameter.Key == "target");
            Assert.Equal("SceneObject", target.Control);
            Assert.NotEmpty(target.AcceptedObjectGroups);
            if (id is "EV_OnObjectCollisionTurnedOn" or "EV_OnObjectCollisionTurnedOff" or "EV_OnObjectAnchored" or "EV_OnObjectUnanchored" or "EV_OnObjectCollisionChanged")
            {
                Assert.Equal(["PhysicsBody"], target.AcceptedObjectGroups);
            }
            else if (id.StartsWith("EV_OnObject", StringComparison.OrdinalIgnoreCase) &&
                !id.StartsWith("EV_OnObjects", StringComparison.OrdinalIgnoreCase) &&
                id is not "EV_OnObjectBecameVisible" and not "EV_OnObjectBecameHidden")
            {
                Assert.Equal(["PartLike"], target.AcceptedObjectGroups);
            }

            if (!id.Equals("EV_AfterDelay", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Contains(node.Parameters, parameter => parameter.Key == "interval");
            }
        }
    }

    [Fact]
    public void CatalogPaletteAliases_AreValidNonEmptyPaths()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var aliasedNodes = catalog.Nodes.Where(node => node.PaletteAliases.Count > 0).ToList();

        Assert.NotEmpty(aliasedNodes);
        foreach (var node in aliasedNodes)
        {
            Assert.All(node.PaletteAliases, alias =>
            {
                Assert.NotEmpty(alias);
                Assert.All(alias, part => Assert.False(string.IsNullOrWhiteSpace(part)));
            });
        }
    }

    [Fact]
    public void ObbyPack_LoadsRunnableNodeBatch()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var expectedObbyIds = new[]
        {
            "EV_OnPlayerTouchedObject",
            "EV_OnCheckpointTouched",
            "EV_OnHazardTouched",
            "EV_OnFinishTouched",
            "EV_OnPlayerRespawned",
            "ACT_SetPlayerCheckpoint",
            "ACT_SendPlayerToCheckpoint",
            "ACT_RespawnPlayer",
            "ACT_KillPlayer",
            "ACT_StartPlayerTimer",
            "ACT_FinishPlayerTimer",
            "ACT_ResetPlayerRun",
            "ACT_AddPlayerCoin",
            "ACT_MarkPlayerCollectible",
            "ACT_ClearPlayerCollectibles",
            "ACT_SetPlayerNumber",
            "ACT_AddPlayerNumber",
            "ACT_SetPlayerText",
            "ACT_SetPlayerFlag",
            "ACT_SetObjectSpawnEnabled",
            "ACT_SetPlayerCanMove",
            "ACT_MakePlayerJump",
            "ACT_StartMovingPlatformLoop",
            "COND_PlayerHasCheckpoint",
            "COND_PlayerReachedCheckpoint",
            "COND_PlayerHasCollectible",
            "COND_PlayerNumberAtLeast",
            "COND_PlayerFlagIsTrue",
            "COND_RunIsActive",
            "COND_RunIsFinished",
            "PROP_TriggeringTouchObject",
            "PROP_PlayerCheckpointName",
            "PROP_PlayerCheckpointPosition",
            "PROP_PlayerRunTime",
            "PROP_PlayerDeathCount",
            "PROP_PlayerCoinCount",
            "PROP_PlayerRuntimeNumber",
            "PROP_PlayerRuntimeText",
            "PROP_PlayerRuntimeFlag"
        };

        foreach (var id in expectedObbyIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("UserFacing", node.Surface);
            Assert.True(NodeCatalogService.IsAddable(node));
            Assert.Equal("Obby", node.ApiGroup);
            Assert.NotEmpty(node.BeginnerSummary);
            Assert.NotEmpty(node.PalettePath);
            Assert.NotEmpty(node.SearchKeywords);
        }
    }

    [Theory]
    [InlineData("obby", "EV_OnCheckpointTouched")]
    [InlineData("parcours", "ACT_StartMovingPlatformLoop")]
    [InlineData("checkpoint", "ACT_SetPlayerCheckpoint")]
    [InlineData("hazard", "EV_OnHazardTouched")]
    [InlineData("kill", "ACT_KillPlayer")]
    [InlineData("finish", "EV_OnFinishTouched")]
    [InlineData("coin", "ACT_AddPlayerCoin")]
    [InlineData("platform", "ACT_StartMovingPlatformLoop")]
    public void CatalogSearch_FindsObbyPackTerms(string search, string expectedId)
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);

        Assert.Contains(catalog.Nodes.Where(node => NodeCatalogService.Matches(node, search)), node => node.IdBase == expectedId);
    }

    [Fact]
    public void CatalogSearch_KilPlayerRanksKillPlayerWithoutWillNoise()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);

        var ranked = catalog.Nodes
            .Select(node => new { Node = node, Result = NodeCatalogSearchService.Match(node, "kil player") })
            .Where(item => item.Result.IsMatch)
            .OrderByDescending(item => item.Result.Score)
            .ToList();

        Assert.Equal("ACT_KillPlayer", ranked.First().Node.IdBase);
        Assert.DoesNotContain(ranked.Take(10), item => item.Result.MatchSummary.Contains("kil -> will", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ObbyPackRuntimeCompatibility_IsServerOnlyForV1()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var obbyNodes = catalog.Nodes.Where(node => node.ApiGroup.Equals("Obby", StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.Equal(39, obbyNodes.Count);
        Assert.All(obbyNodes, node =>
        {
            Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Server));
            Assert.False(NodeCatalogService.IsCompatibleWithScriptKind(node, GraphScriptKind.Local));
        });
    }

    [Theory]
    [InlineData("players", "EV_OnPlayerJoined")]
    [InlineData("chat", "EV_OnChatMessage")]
    [InlineData("input", "EV_OnInputButtonDown")]
    [InlineData("player defaults", "ACT_SetWalkSpeed")]
    [InlineData("tags", "COND_ObjectHasTag")]
    [InlineData("instance", "PROP_FindChild")]
    [InlineData("tween", "ACT_TweenObjectPosition")]
    public void CatalogSearch_FindsGameplayApiDomains(string search, string expectedId)
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);

        Assert.Contains(catalog.Nodes.Where(node => NodeCatalogService.Matches(node, search)), node => node.IdBase == expectedId);
    }

    [Fact]
    public void GameplayApiRuntimeCompatibility_IsStrictForInputAndPlayerDefaults()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var inputTrigger = catalog.Nodes.Single(node => node.IdBase == "EV_OnInputButtonDown");
        var inputCondition = catalog.Nodes.Single(node => node.IdBase == "COND_InputButtonDown");
        var localPlayer = catalog.Nodes.Single(node => node.IdBase == "PROP_LocalPlayer");
        var setWalkSpeed = catalog.Nodes.Single(node => node.IdBase == "ACT_SetWalkSpeed");
        var walkSpeedValue = catalog.Nodes.Single(node => node.IdBase == "PROP_WalkSpeedValue");

        Assert.False(NodeCatalogService.IsCompatibleWithScriptKind(inputTrigger, GraphScriptKind.Server));
        Assert.False(NodeCatalogService.IsCompatibleWithScriptKind(inputCondition, GraphScriptKind.Server));
        Assert.False(NodeCatalogService.IsCompatibleWithScriptKind(localPlayer, GraphScriptKind.Server));
        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(inputTrigger, GraphScriptKind.Local));
        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(inputCondition, GraphScriptKind.Local));
        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(localPlayer, GraphScriptKind.Local));

        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(setWalkSpeed, GraphScriptKind.Server));
        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(walkSpeedValue, GraphScriptKind.Server));
        Assert.False(NodeCatalogService.IsCompatibleWithScriptKind(setWalkSpeed, GraphScriptKind.Local));
        Assert.False(NodeCatalogService.IsCompatibleWithScriptKind(walkSpeedValue, GraphScriptKind.Local));
    }

    [Fact]
    public void FusedTransformCatalog_HidesLegacyTransformNodesButKeepsThemLoadable()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var legacyIds = new[]
        {
            "ACT_SetObjectPosition",
            "ACT_AddObjectPosition",
            "ACT_MoveObjectOverTime",
            "ACT_RotateObjectContinuously"
        };

        foreach (var id in legacyIds)
        {
            var node = catalog.Nodes.Single(entry => entry.IdBase == id);
            Assert.Equal("Ready", node.Status);
            Assert.Equal("Support", node.Surface);
        }
    }

    [Fact]
    public void TypedPropertyCatalog_ExposesReusableVectorAndColorValues()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var objectPosition = catalog.Nodes.Single(node => node.IdBase == "PROP_ObjectPosition");
        var checkpointPosition = catalog.Nodes.Single(node => node.IdBase == "PROP_PlayerCheckpointPosition");
        var rgbColor = catalog.Nodes.Single(node => node.IdBase == "PROP_RGBColor");
        var moveObject = catalog.Nodes.Single(node => node.IdBase == "ACT_MoveObject");
        var lookAtPosition = catalog.Nodes.Single(node => node.IdBase == "ACT_LookAtPosition");

        Assert.Equal("Vector3", NodeCatalogService.CreateNode(objectPosition).Ports.Single(port => port.Id == GraphPortDefaults.ValueOut).DataType);
        Assert.Equal("Vector3", NodeCatalogService.CreateNode(checkpointPosition).Ports.Single(port => port.Id == GraphPortDefaults.ValueOut).DataType);
        Assert.Equal("Color", NodeCatalogService.CreateNode(rgbColor).Ports.Single(port => port.Id == GraphPortDefaults.ValueOut).DataType);
        Assert.Contains(moveObject.Parameters, parameter => parameter.Key == "vector" && parameter.Type == "Vector3");
        Assert.Contains(lookAtPosition.Parameters, parameter => parameter.Key == "lookPosition" && parameter.Type == "Vector3");
        Assert.Contains(catalog.Nodes, node => node.IdBase == "PROP_VectorAdd");
        Assert.Contains(catalog.Nodes, node => node.IdBase == "PROP_DirectionToObject");
        Assert.Contains(catalog.Nodes, node => node.IdBase == "ACT_LookAtObject");
    }

    [Theory]
    [InlineData("spin", "ACT_RotateObject")]
    [InlineData("hide", "ACT_SetObjectVisible")]
    [InlineData("score", "ACT_IncrementScriptNumber")]
    [InlineData("append", "ACT_AppendScriptText")]
    [InlineData("anchored", "ACT_SetObjectAnchored")]
    [InlineData("transparency", "ACT_SetObjectTransparency")]
    [InlineData("chance", "COND_RandomChance")]
    [InlineData("lowercase", "PROP_LowercaseText")]
    [InlineData("part", "ACT_SetObjectColor")]
    [InlineData("random color", "PROP_RandomColor")]
    [InlineData("touch", "EV_OnTouchObject")]
    public void CatalogSearch_FindsCoreSceneBeginnerTerms(string search, string expectedId)
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);

        Assert.Contains(catalog.Nodes.Where(node => NodeCatalogService.Matches(node, search)), node => node.IdBase == expectedId);
    }

    [Fact]
    public void IsCompatibleWithScriptKind_FiltersRuntimeFamily()
    {
        var server = CatalogEntry("Server");
        var local = CatalogEntry("Local");
        var client = CatalogEntry("Client");
        var module = CatalogEntry("Module");
        var shared = CatalogEntry("Shared");

        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(server, GraphScriptKind.Server));
        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(shared, GraphScriptKind.Server));
        Assert.False(NodeCatalogService.IsCompatibleWithScriptKind(local, GraphScriptKind.Server));

        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(local, GraphScriptKind.Local));
        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(client, GraphScriptKind.Local));
        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(shared, GraphScriptKind.Local));
        Assert.False(NodeCatalogService.IsCompatibleWithScriptKind(server, GraphScriptKind.Local));

        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(module, GraphScriptKind.Module));
        Assert.True(NodeCatalogService.IsCompatibleWithScriptKind(shared, GraphScriptKind.Module));
        Assert.False(NodeCatalogService.IsCompatibleWithScriptKind(server, GraphScriptKind.Module));
    }

    [Fact]
    public void LoadCatalog_ReadsOptionDetailsWhileKeepingSimpleOptions()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-catalog-options-{Guid.NewGuid():N}");
        var nodeRoot = Path.Combine(tempRoot, "TestNode");
        Directory.CreateDirectory(nodeRoot);

        try
        {
            File.WriteAllText(Path.Combine(nodeRoot, "manifest.vrs-node.json"), """
            {
              "kind": "Action",
              "apiGroup": "Tests",
              "runtimeFamily": "Shared",
              "idBase": "ACT_TestOptions",
              "type": "TestOptions",
              "label": "Test Options",
              "parameters": [
                {
                  "key": "mode",
                  "label": "Mode",
                  "type": "String",
                  "control": "Choice",
                  "options": [ "Fast", "Safe" ],
                  "optionDetails": [
                    {
                      "value": "Safe",
                      "label": "Safe Mode",
                      "category": "Recommended",
                      "description": "Use this when reliability matters more than speed.",
                      "searchKeywords": [ "reliable", "careful" ]
                    }
                  ]
                }
              ]
            }
            """);

            var catalog = new NodeCatalogService().LoadCatalog(tempRoot);

            Assert.Empty(catalog.Warnings);
            var parameter = catalog.Nodes.Single().Parameters.Single();
            Assert.Equal(["Fast", "Safe"], parameter.Options);
            var detail = parameter.OptionDetails.Single();
            Assert.Equal("Safe", detail.Value);
            Assert.Equal("Safe Mode", detail.Label);
            Assert.Equal("Recommended", detail.Category);
            Assert.Contains("reliable", detail.SearchKeywords);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void LoadCatalog_ReadsOptionalBeginnerPresentationFields()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-catalog-presentation-{Guid.NewGuid():N}");
        var nodeRoot = Path.Combine(tempRoot, "TestNode");
        Directory.CreateDirectory(nodeRoot);

        try
        {
            File.WriteAllText(Path.Combine(nodeRoot, "manifest.vrs-node.json"), """
            {
              "kind": "Action",
              "runtimeFamily": "Shared",
              "idBase": "ACT_TestPresentation",
              "type": "TestPresentation",
              "label": "Test Presentation",
              "description": "Technical fallback description.",
              "beginnerSummary": "Do a beginner-friendly test action.",
              "surface": "Advanced",
              "palettePath": [ "Math", "Arithmetic" ]
            }
            """);

            var catalog = new NodeCatalogService().LoadCatalog(tempRoot);

            Assert.Empty(catalog.Warnings);
            var node = catalog.Nodes.Single();
            Assert.Equal("Do a beginner-friendly test action.", node.BeginnerSummary);
            Assert.Equal("Advanced", node.Surface);
            Assert.Equal(["Math", "Arithmetic"], node.PalettePath);
            Assert.Equal("Do a beginner-friendly test action.", NodeCatalogPresentationService.GetBeginnerSummary(node));
            Assert.True(NodeCatalogPresentationService.IsDefaultPaletteSurface(node));
            Assert.Equal(["Math", "Arithmetic"], NodeCatalogPresentationService.GetPalettePath(node));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Presentation_FallsBackToSubcategoryWhenPalettePathIsMissing()
    {
        var entry = new NodeCatalogEntry
        {
            IdBase = "ACT_Legacy",
            Kind = NodeKind.Action,
            Label = "Legacy Action",
            Subcategory = "Scene Object"
        };

        Assert.Equal(["Scene Object"], NodeCatalogPresentationService.GetPalettePath(entry));
    }

    private static NodeCatalogEntry CatalogEntry(string runtimeFamily)
    {
        return new NodeCatalogEntry
        {
            IdBase = $"TEST_{runtimeFamily}",
            Kind = NodeKind.Action,
            RuntimeFamily = runtimeFamily,
            Label = runtimeFamily
        };
    }

    private static IReadOnlyList<(NodeCatalogEntry Entry, NodeCatalogSearchResult Search)> RankedCatalogMatches(
        NodeCatalogData catalog,
        string search)
    {
        // Tests use the same ranking contract as UI surfaces without depending
        // on a specific Avalonia palette implementation.
        return catalog.Nodes
            .Select(entry => (Entry: entry, Search: NodeCatalogSearchService.Match(entry, search)))
            .Where(item => item.Search.IsMatch)
            .OrderByDescending(item => item.Search.Score)
            .ThenBy(item => item.Entry.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
