using System.Text;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Readable trigger and flow traversal output, including condition branch calls.
    private static void AppendReadableTriggerBlock(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds)
    {
        reachedNodeIds.Add(trigger.Id);
        var functionName = FunctionName(plan, trigger);

        builder.AppendLine(LuauCommentTags.VsrComment($"TRIGGER: {HumanBlockName(trigger.Label)}"));
        if (AppendReadableNodeLocalVariables(builder, rule, trigger, nodesById, plan))
        {
            builder.AppendLine();
        }

        AppendReadableNodeSummary(builder, trigger, catalog);
        if (trigger.Type.Equals("RunLuauTrigger", StringComparison.OrdinalIgnoreCase))
        {
            AppendRunLuauTriggerBlock(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (trigger.Type.Equals("OnTimerTick", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"local function {functionName}()");
            builder.AppendLine("    while true do");
            builder.AppendLine($"        wait({ConfigName(plan, trigger, "interval")})");
            AppendReadableTriggerObjectResolution(builder, plan, trigger, 2);
            builder.AppendLine("        local triggerContext = { object = triggerObject }");
            var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
            if (!emitted)
            {
                builder.AppendLine("        print(\"On Timer Tick trigger stopped: no connected action or condition.\")");
            }

            builder.AppendLine("    end");
            builder.AppendLine("end");
        }
        else if (trigger.Type.Equals("AfterDelay", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableAfterDelayTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (trigger.Type.Equals("OnSoundLoaded", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableSoundLoadedTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (IsAudioLightingWatcherTrigger(trigger.Type))
        {
            AppendReadableAudioLightingWatcherTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (IsImageSkyWatcherTrigger(trigger.Type))
        {
            AppendReadableImageSkyWatcherTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (IsProceduralSkyWatcherTrigger(trigger.Type))
        {
            AppendReadableProceduralSkyWatcherTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (trigger.Type.Equals("OnMeshLoaded", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableMeshLoadedTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (IsSeatEventTrigger(trigger.Type))
        {
            AppendReadableSeatEventTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (IsNpcEventTrigger(trigger.Type))
        {
            AppendReadableNpcEventTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (IsCharacterRagdollEventTrigger(trigger.Type))
        {
            AppendReadableCharacterRagdollEventTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (IsCharacterAnimationWatcherTrigger(trigger.Type))
        {
            AppendReadableCharacterAnimationWatcherTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (trigger.Type.Equals("OnBodyPositionReachedTarget", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableBodyPositionReachedTargetTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (trigger.Type.Equals("OnUIButtonClicked", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableUiButtonClickedTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (trigger.Type.Equals("OnTextInputChanged", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableTextInputEventTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName, "Changed", "On Text Input Changed");
        }
        else if (trigger.Type.Equals("OnTextInputSubmitted", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableTextInputEventTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName, "Submitted", "On Text Input Submitted");
        }
        else if (IsCustomUiWatcherTrigger(trigger.Type))
        {
            AppendReadableCustomUiWatcherTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (IsText3DWatcherTrigger(trigger.Type))
        {
            AppendReadableText3DWatcherTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (IsImage3DWatcherTrigger(trigger.Type))
        {
            AppendReadableImage3DWatcherTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (IsAssetMediaWatcherTrigger(trigger.Type))
        {
            AppendReadableAssetMediaWatcherTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (IsWorldMarkerWatcherTrigger(trigger.Type))
        {
            AppendReadableWorldMarkerWatcherTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (IsSharedTableWatcherTrigger(trigger.Type))
        {
            AppendReadableSharedTableWatcherTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (IsScriptRuntimeWatcherTrigger(trigger.Type))
        {
            AppendReadableScriptRuntimeWatcherTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (IsTweenTargetWatcherTrigger(trigger.Type))
        {
            AppendReadableTweenTargetWatcherTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (trigger.Type.Equals("OnPlayerGameTeamChanged", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadablePlayerGameTeamChangedTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (IsToolEventTrigger(trigger.Type))
        {
            AppendReadableToolEventTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (trigger.Type.Equals("OnPlayerJoined", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnPlayerLeft", StringComparison.OrdinalIgnoreCase))
        {
            var signalName = trigger.Type.Equals("OnPlayerLeft", StringComparison.OrdinalIgnoreCase) ? "PlayerRemoved" : "PlayerAdded";
            builder.AppendLine($"local function {functionName}()");
            builder.AppendLine($"    if Players == nil or Players.{signalName} == nil or Players.{signalName}.Connect == nil then");
            builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: Players.{signalName} is not available.\")");
            builder.AppendLine("        return");
            builder.AppendLine("    end");
            builder.AppendLine($"    Players.{signalName}:Connect(function(player)");
            AppendReadableTriggerObjectResolution(builder, plan, trigger, 2);
            builder.AppendLine("        local triggerContext = { object = triggerObject, player = player }");
            var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
            if (!emitted)
            {
                builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
            }

            builder.AppendLine("    end)");
            builder.AppendLine("end");
        }
        else if (trigger.Type.Equals("OnChatMessage", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"local function {functionName}()");
            builder.AppendLine("    if Chat == nil or Chat.NewChatMessage == nil or Chat.NewChatMessage.Connect == nil then");
            builder.AppendLine("        print(\"On Chat Message trigger stopped: Chat.NewChatMessage is not available.\")");
            builder.AppendLine("        return");
            builder.AppendLine("    end");
            builder.AppendLine("    Chat.NewChatMessage:Connect(function(sender, message)");
            AppendReadableTriggerObjectResolution(builder, plan, trigger, 2);
            builder.AppendLine("        local triggerContext = { object = triggerObject, player = sender, message = message }");
            var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
            if (!emitted)
            {
                builder.AppendLine("        print(\"On Chat Message trigger stopped: no connected action or condition.\")");
            }

            builder.AppendLine("    end)");
            builder.AppendLine("end");
        }
        else if (trigger.Type.Equals("OnInputButtonDown", StringComparison.OrdinalIgnoreCase))
        {
            var actionName = ParameterExpression(rule, trigger, nodesById, "actionName", "String", "Interact");
            builder.AppendLine($"local function {functionName}()");
            builder.AppendLine("    if Input == nil or Input.GetButton == nil then");
            builder.AppendLine("        print(\"On Input Button Down trigger stopped: Input:GetButton is not available.\")");
            builder.AppendLine("        return");
            builder.AppendLine("    end");
            builder.AppendLine($"    local inputActionName = tostring({actionName.Code})");
            builder.AppendLine("    local buttonAction = Input:GetButton(inputActionName)");
            builder.AppendLine("    if buttonAction == nil or buttonAction.Pressed == nil or buttonAction.Pressed.Connect == nil then");
            builder.AppendLine("        print(\"On Input Button Down trigger stopped: input action \" .. inputActionName .. \" has no Pressed event.\")");
            builder.AppendLine("        return");
            builder.AppendLine("    end");
            builder.AppendLine("    buttonAction.Pressed:Connect(function()");
            AppendReadableTriggerObjectResolution(builder, plan, trigger, 2);
            builder.AppendLine("        local triggerContext = { object = triggerObject, inputAction = inputActionName, inputValue = true }");
            var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
            if (!emitted)
            {
                builder.AppendLine("        print(\"On Input Button Down trigger stopped: no connected action or condition.\")");
            }

            builder.AppendLine("    end)");
            builder.AppendLine("end");
        }
        else if (trigger.Type.Equals("OnVrsInputEvent", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableVrsInputEventTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (trigger.Type.Equals("OnBindableEvent", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableBindableEventTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (trigger.Type.Equals("OnStateChanged", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableStateChangedTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (trigger.Type.Equals("OnScriptVariableChanged", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableScriptVariableChangedTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (PhysicalEventTriggerTypes.Contains(trigger.Type))
        {
            AppendReadablePhysicalEventTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (IsRigidBodyWatcherTrigger(trigger.Type))
        {
            AppendReadableRigidBodyWatcherTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (IsGrabbableWatcherTrigger(trigger.Type))
        {
            AppendReadableGrabbableWatcherTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (trigger.Type.Equals("OnRoundStarted", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnRoundEnded", StringComparison.OrdinalIgnoreCase))
        {
            var expected = trigger.Type.Equals("OnRoundStarted", StringComparison.OrdinalIgnoreCase);
            AppendReadableNamedBooleanTransitionTrigger(
                builder,
                rule,
                trigger,
                plan,
                nodesById,
                visited,
                reachedNodeIds,
                functionName,
                "roundName",
                "Main Round",
                "round",
                "stateKey .. \":running\"",
                expected,
                "roundName = watchedName, roundRunning = currentValue");
        }
        else if (trigger.Type.Equals("OnRoundTimeExpired", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableRoundTimeExpiredTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (trigger.Type.Equals("OnGateOpened", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnGateClosed", StringComparison.OrdinalIgnoreCase))
        {
            var expected = trigger.Type.Equals("OnGateOpened", StringComparison.OrdinalIgnoreCase);
            AppendReadableNamedBooleanTransitionTrigger(
                builder,
                rule,
                trigger,
                plan,
                nodesById,
                visited,
                reachedNodeIds,
                functionName,
                "gateName",
                "Main Gate",
                "gate",
                "stateKey",
                expected,
                "gateName = watchedName, gateOpen = currentValue");
        }
        else if (trigger.Type.Equals("OnObjectBecameVisible", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnObjectBecameHidden", StringComparison.OrdinalIgnoreCase))
        {
            var expected = trigger.Type.Equals("OnObjectBecameVisible", StringComparison.OrdinalIgnoreCase);
            AppendReadableObjectVisibilityTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName, expected);
        }
        else if (trigger.Type.Equals("OnObjectCollisionTurnedOn", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnObjectCollisionTurnedOff", StringComparison.OrdinalIgnoreCase))
        {
            var expected = trigger.Type.Equals("OnObjectCollisionTurnedOn", StringComparison.OrdinalIgnoreCase);
            AppendReadableObjectBooleanPropertyTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName, "CanCollide", expected, "objectCollisionOn");
        }
        else if (trigger.Type.Equals("OnObjectAnchored", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnObjectUnanchored", StringComparison.OrdinalIgnoreCase))
        {
            var expected = trigger.Type.Equals("OnObjectAnchored", StringComparison.OrdinalIgnoreCase);
            AppendReadableObjectBooleanPropertyTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName, "Anchored", expected, "objectAnchored");
        }
        else if (trigger.Type.Equals("OnObjectsBecameClose", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnObjectsBecameFar", StringComparison.OrdinalIgnoreCase))
        {
            var expectedClose = trigger.Type.Equals("OnObjectsBecameClose", StringComparison.OrdinalIgnoreCase);
            AppendReadableObjectDistanceTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName, expectedClose);
        }
        else if (trigger.Type.Equals("OnTeamScoreReached", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnTeamScoreDroppedTo", StringComparison.OrdinalIgnoreCase))
        {
            var reached = trigger.Type.Equals("OnTeamScoreReached", StringComparison.OrdinalIgnoreCase);
            AppendReadableTeamScoreTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName, reached);
        }
        else if (trigger.Type.Equals("OnAnyPlayerScoreReached", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnAnyPlayerHasNoLivesLeft", StringComparison.OrdinalIgnoreCase))
        {
            var watchesScore = trigger.Type.Equals("OnAnyPlayerScoreReached", StringComparison.OrdinalIgnoreCase);
            AppendReadableAnyPlayerNumberTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName, watchesScore);
        }
        else if (ValueWatcherTriggerTypes.Contains(trigger.Type))
        {
            AppendReadableValueWatcherTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (VariableWatcherTriggerTypes.Contains(trigger.Type))
        {
            AppendReadableVariableWatcherTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (StateMatchWatcherTriggerTypes.Contains(trigger.Type))
        {
            AppendReadableStateMatchTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (ChangedValueWatcherTriggerTypes.Contains(trigger.Type))
        {
            AppendReadableChangedValueWatcherTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (ChangedVariableWatcherTriggerTypes.Contains(trigger.Type))
        {
            AppendReadableChangedVariableWatcherTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (PlayerCountWatcherTriggerTypes.Contains(trigger.Type))
        {
            AppendReadablePlayerCountTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (trigger.Type.Equals("OnPlayerDefaultReached", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadablePlayerDefaultTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (ObjectMovementWatcherTriggerTypes.Contains(trigger.Type))
        {
            AppendReadableObjectMovementWatcherTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (ObjectAreaWatcherTriggerTypes.Contains(trigger.Type))
        {
            AppendReadableObjectAreaWatcherTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (ObjectNumericWatcherTriggerTypes.Contains(trigger.Type))
        {
            AppendReadableObjectNumericWatcherTransitionTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (trigger.Type.Equals("OnObjectCollisionChanged", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableObjectCollisionChangedTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (ObbyTouchTriggerTypes.Contains(trigger.Type))
        {
            AppendReadableObbyTouchTrigger(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, functionName);
        }
        else if (trigger.Type.Equals("OnPlayerRespawned", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"local function {functionName}()");
            builder.AppendLine("    if Players == nil then");
            builder.AppendLine("        print(\"On Player Respawned trigger stopped: Players is not available.\")");
            builder.AppendLine("        return");
            builder.AppendLine("    end");
            builder.AppendLine("    local function connectPlayerRespawned(player)");
            builder.AppendLine("        if player == nil or player.Respawned == nil or player.Respawned.Connect == nil then");
            builder.AppendLine("            return");
            builder.AppendLine("        end");
            builder.AppendLine("        player.Respawned:Connect(function()");
            AppendReadableTriggerObjectResolution(builder, plan, trigger, 3);
            builder.AppendLine("            local triggerContext = { object = triggerObject, player = player }");
            var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 3, 0);
            if (!emitted)
            {
                builder.AppendLine("            print(\"On Player Respawned trigger stopped: no connected action or condition.\")");
            }

            builder.AppendLine("        end)");
            builder.AppendLine("    end");
            builder.AppendLine("    if Players.GetPlayers ~= nil then");
            builder.AppendLine("        for _, player in ipairs(Players:GetPlayers()) do");
            builder.AppendLine("            connectPlayerRespawned(player)");
            builder.AppendLine("        end");
            builder.AppendLine("    end");
            builder.AppendLine("    if Players.PlayerAdded ~= nil and Players.PlayerAdded.Connect ~= nil then");
            builder.AppendLine("        Players.PlayerAdded:Connect(connectPlayerRespawned)");
            builder.AppendLine("    end");
            builder.AppendLine("end");
        }
        else
        {
            builder.AppendLine($"local function {functionName}()");
            AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
            builder.AppendLine("    local triggerContext = { object = triggerObject }");
            var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 1, 0);
            if (!emitted)
            {
                builder.AppendLine($"    print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
            }

            builder.AppendLine("end");
        }
    }

    private static void AppendReadableTriggerObjectResolution(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode trigger,
        int indentLevel)
    {
        // Trigger targets are rule context, not event filters: event payloads
        // still travel through triggerContext while triggerObject follows target.
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}local scriptParent = script.Parent");
        builder.AppendLine($"{indent}local triggerObject = {ReadableTriggerTargetExpression(plan, trigger, "scriptParent")}");
        builder.AppendLine($"{indent}if triggerObject == nil then");
        if (HasConfigName(plan, trigger, "target"))
        {
            builder.AppendLine($"{indent}    print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target \" .. tostring({ConfigName(plan, trigger, "target")}) .. \" was not found.\")");
        }
        else
        {
            builder.AppendLine($"{indent}    print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: this script has no parent object.\")");
        }

        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private static string ReadableTriggerTargetExpression(ReadableExportPlan plan, RuleNode trigger, string baseObjectName)
    {
        return HasConfigName(plan, trigger, "target")
            ? $"resolveTarget({baseObjectName}, {ConfigName(plan, trigger, "target")})"
            : baseObjectName;
    }

    private static void AppendReadableStateChangedTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var state = ParameterExpression(rule, trigger, nodesById, "state", "String", "DoorOpen");
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    local watchedStateName = tostring({state.Code})");
        builder.AppendLine("    local previousValue = VRS.states[watchedStateName] == true");
        builder.AppendLine("    while true do");
        builder.AppendLine($"        wait({interval.Code})");
        builder.AppendLine("        local currentValue = VRS.states[watchedStateName] == true");
        builder.AppendLine("        if currentValue ~= previousValue then");
        builder.AppendLine("            previousValue = currentValue");
        builder.AppendLine("            local triggerContext = { object = triggerObject, stateName = watchedStateName, stateValue = currentValue }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 3, 0);
        if (!emitted)
        {
            builder.AppendLine("            print(\"On State Changed trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("        end");
        builder.AppendLine("    end");
        builder.AppendLine("end");
    }

    private static void AppendReadableAfterDelayTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var duration = ParameterExpression(rule, trigger, nodesById, "duration", "Number", "1");

        builder.AppendLine($"local function {functionName}()");
        builder.AppendLine($"    wait({duration.Code})");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine("    local triggerContext = { object = triggerObject }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 1, 0);
        if (!emitted)
        {
            builder.AppendLine("    print(\"After Delay trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("end");
    }

    private static void AppendReadableNamedBooleanTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName,
        string nameParameterKey,
        string fallbackName,
        string keyPrefix,
        string stateKeyExpression,
        bool expectedValue,
        string contextFields)
    {
        var name = ParameterExpression(rule, trigger, nodesById, nameParameterKey, "String", fallbackName);
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");
        var expected = expectedValue ? "true" : "false";

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    local watchedName = tostring({name.Code})");
        builder.AppendLine($"    local stateKey = (\"{keyPrefix}:\" .. watchedName)");
        builder.AppendLine($"    local previousValue = VRS.states[{stateKeyExpression}] == true");
        builder.AppendLine("    while true do");
        builder.AppendLine($"        wait({interval.Code})");
        builder.AppendLine($"        local currentValue = VRS.states[{stateKeyExpression}] == true");
        builder.AppendLine("        if currentValue ~= previousValue then");
        builder.AppendLine("            previousValue = currentValue");
        builder.AppendLine($"            if currentValue == {expected} then");
        builder.AppendLine($"                local triggerContext = {{ object = triggerObject, {contextFields} }}");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 4, 0);
        if (!emitted)
        {
            builder.AppendLine($"                print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("            end");
        builder.AppendLine("        end");
        builder.AppendLine("    end");
        builder.AppendLine("end");
    }

    private static void AppendReadableRoundTimeExpiredTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var roundName = ParameterExpression(rule, trigger, nodesById, "roundName", "String", "Main Round");
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    local watchedName = tostring({roundName.Code})");
        builder.AppendLine("    local roundKey = (\"round:\" .. watchedName)");
        builder.AppendLine("    local function isRoundTimeExpired()");
        builder.AppendLine("        local endAt = tonumber(VRS.vars[roundKey .. \":endAt\"])");
        builder.AppendLine("        return VRS.states[roundKey .. \":running\"] == true and endAt ~= nil and vrsNow() >= endAt");
        builder.AppendLine("    end");
        builder.AppendLine("    local previousValue = isRoundTimeExpired()");
        builder.AppendLine("    while true do");
        builder.AppendLine($"        wait({interval.Code})");
        builder.AppendLine("        local currentValue = isRoundTimeExpired()");
        builder.AppendLine("        if currentValue ~= previousValue then");
        builder.AppendLine("            previousValue = currentValue");
        builder.AppendLine("            if currentValue == true then");
        builder.AppendLine("                local triggerContext = { object = triggerObject, roundName = watchedName, roundTimeExpired = currentValue }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 4, 0);
        if (!emitted)
        {
            builder.AppendLine("                print(\"On Round Time Expired trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("            end");
        builder.AppendLine("        end");
        builder.AppendLine("    end");
        builder.AppendLine("end");
    }

    private static void AppendReadableObjectVisibilityTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName,
        bool expectedValue)
    {
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");
        var expected = expectedValue ? "true" : "false";

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine("    local function readObjectVisible()");
        builder.AppendLine("        if triggerObject.Visible ~= nil then");
        builder.AppendLine("            return triggerObject.Visible == true");
        builder.AppendLine("        end");
        builder.AppendLine("        if triggerObject.Transparency ~= nil then");
        builder.AppendLine("            return triggerObject.Transparency < 1");
        builder.AppendLine("        end");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target does not expose Visible or Transparency.\")");
        builder.AppendLine("        return nil");
        builder.AppendLine("    end");
        builder.AppendLine("    local previousValue = readObjectVisible()");
        builder.AppendLine("    if previousValue == nil then");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    while true do");
        builder.AppendLine($"        wait({interval.Code})");
        builder.AppendLine("        local currentValue = readObjectVisible()");
        builder.AppendLine("        if currentValue == nil then");
        builder.AppendLine("            return");
        builder.AppendLine("        end");
        builder.AppendLine("        if currentValue ~= previousValue then");
        builder.AppendLine("            previousValue = currentValue");
        builder.AppendLine($"            if currentValue == {expected} then");
        builder.AppendLine("                local triggerContext = { object = triggerObject, objectVisible = currentValue }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 4, 0);
        if (!emitted)
        {
            builder.AppendLine($"                print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("            end");
        builder.AppendLine("        end");
        builder.AppendLine("    end");
        builder.AppendLine("end");
    }

    private static void AppendReadableObjectBooleanPropertyTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName,
        string propertyName,
        bool expectedValue,
        string contextField)
    {
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");
        var expected = expectedValue ? "true" : "false";

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine("    local function readWatchedObjectValue()");
        builder.AppendLine($"        if triggerObject.{propertyName} == nil then");
        builder.AppendLine($"            print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target does not expose {propertyName}.\")");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        builder.AppendLine($"        return triggerObject.{propertyName} == true");
        builder.AppendLine("    end");
        // Snapshot first so an already-matching object does not fire when the script starts.
        builder.AppendLine("    local previousValue = readWatchedObjectValue()");
        builder.AppendLine("    if previousValue == nil then");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    while true do");
        builder.AppendLine($"        wait({interval.Code})");
        builder.AppendLine("        local currentValue = readWatchedObjectValue()");
        builder.AppendLine("        if currentValue == nil then");
        builder.AppendLine("            return");
        builder.AppendLine("        end");
        builder.AppendLine("        if currentValue ~= previousValue then");
        builder.AppendLine("            previousValue = currentValue");
        builder.AppendLine($"            if currentValue == {expected} then");
        builder.AppendLine($"                local triggerContext = {{ object = triggerObject, {contextField} = currentValue }}");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 4, 0);
        if (!emitted)
        {
            builder.AppendLine($"                print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("            end");
        builder.AppendLine("        end");
        builder.AppendLine("    end");
        builder.AppendLine("end");
    }

    private static void AppendReadableObjectDistanceTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName,
        bool expectedClose)
    {
        var first = ParameterExpression(rule, trigger, nodesById, "first", "String", "Self");
        var second = ParameterExpression(rule, trigger, nodesById, "second", "String", "Target");
        var distance = ParameterExpression(rule, trigger, nodesById, "distance", "Number", expectedClose ? "10" : "25");
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");
        var comparison = expectedClose ? "<=" : ">=";

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    local firstObject = resolveTarget(triggerObject, {first.Code})");
        builder.AppendLine($"    local secondObject = resolveTarget(triggerObject, {second.Code})");
        builder.AppendLine("    if firstObject == nil or secondObject == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: one of the watched objects was not found.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    if firstObject.Position == nil or secondObject.Position == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: a watched object has no Position.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine($"    local distanceLimit = {distance.Code}");
        builder.AppendLine("    local function readDistanceMatch()");
        builder.AppendLine("        local distanceBetweenObjects = vrsDistanceBetweenPositions(firstObject.Position, secondObject.Position)");
        builder.AppendLine($"        return distanceBetweenObjects {comparison} distanceLimit, distanceBetweenObjects");
        builder.AppendLine("    end");
        builder.AppendLine("    local previousValue = readDistanceMatch()");
        builder.AppendLine("    while true do");
        builder.AppendLine($"        wait({interval.Code})");
        builder.AppendLine("        local currentValue, currentDistance = readDistanceMatch()");
        builder.AppendLine("        if currentValue ~= previousValue then");
        builder.AppendLine("            previousValue = currentValue");
        builder.AppendLine("            if currentValue == true then");
        builder.AppendLine("                local triggerContext = { object = triggerObject, firstObject = firstObject, secondObject = secondObject, distance = currentDistance }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 4, 0);
        if (!emitted)
        {
            builder.AppendLine($"                print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("            end");
        builder.AppendLine("        end");
        builder.AppendLine("    end");
        builder.AppendLine("end");
    }

    private static void AppendReadableTeamScoreTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName,
        bool reached)
    {
        var teamName = ParameterExpression(rule, trigger, nodesById, "teamName", "String", "Blue");
        var score = ParameterExpression(rule, trigger, nodesById, "score", "Number", "10");
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");
        var comparison = reached ? ">=" : "<=";

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    local watchedName = tostring({teamName.Code})");
        builder.AppendLine("    local teamKey = (\"team:\" .. watchedName)");
        builder.AppendLine($"    local scoreLimit = {score.Code}");
        builder.AppendLine("    local function readTeamScoreMatch()");
        builder.AppendLine("        local currentScore = tonumber(VRS.vars[teamKey .. \":score\"]) or 0");
        builder.AppendLine($"        return currentScore {comparison} scoreLimit, currentScore");
        builder.AppendLine("    end");
        builder.AppendLine("    local previousValue = readTeamScoreMatch()");
        builder.AppendLine("    while true do");
        builder.AppendLine($"        wait({interval.Code})");
        builder.AppendLine("        local currentValue, currentScore = readTeamScoreMatch()");
        builder.AppendLine("        if currentValue ~= previousValue then");
        builder.AppendLine("            previousValue = currentValue");
        builder.AppendLine("            if currentValue == true then");
        builder.AppendLine("                local triggerContext = { object = triggerObject, teamName = watchedName, teamScore = currentScore }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 4, 0);
        if (!emitted)
        {
            builder.AppendLine($"                print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("            end");
        builder.AppendLine("        end");
        builder.AppendLine("    end");
        builder.AppendLine("end");
    }

    private static void AppendReadableAnyPlayerNumberTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName,
        bool watchesScore)
    {
        var parameterKey = watchesScore ? "score" : "lives";
        var fallbackValue = watchesScore ? "10" : "0";
        var limit = ParameterExpression(rule, trigger, nodesById, parameterKey, "Number", fallbackValue);
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");
        var valueName = watchesScore ? "score" : "lives";
        var comparison = watchesScore ? ">=" : "<=";
        var contextField = watchesScore ? "playerScore" : "playerLives";

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine("    if Players == nil or Players.GetPlayers == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: Players:GetPlayers is not available.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine($"    local valueLimit = {limit.Code}");
        builder.AppendLine("    local previousMatches = {}");
        builder.AppendLine("    local function readPlayerMatch(player)");
        builder.AppendLine("        local playerKey = vrsPlayerKey(player)");
        builder.AppendLine($"        local runtimeKey = \"player:\" .. playerKey .. \":{valueName}\"");
        builder.AppendLine("        local currentValue = tonumber(VRS.vars[runtimeKey]) or 0");
        builder.AppendLine($"        return currentValue {comparison} valueLimit, currentValue, playerKey");
        builder.AppendLine("    end");
        builder.AppendLine("    for _, player in ipairs(Players:GetPlayers()) do");
        builder.AppendLine("        local currentMatch, _, playerKey = readPlayerMatch(player)");
        builder.AppendLine("        previousMatches[playerKey] = currentMatch == true");
        builder.AppendLine("    end");
        builder.AppendLine("    while true do");
        builder.AppendLine($"        wait({interval.Code})");
        builder.AppendLine("        for _, player in ipairs(Players:GetPlayers()) do");
        builder.AppendLine("            local currentMatch, currentValue, playerKey = readPlayerMatch(player)");
        builder.AppendLine("            local previousMatch = previousMatches[playerKey]");
        builder.AppendLine("            if previousMatch == nil then");
        builder.AppendLine("                previousMatches[playerKey] = currentMatch == true");
        builder.AppendLine("            elseif currentMatch ~= previousMatch then");
        builder.AppendLine("                previousMatches[playerKey] = currentMatch == true");
        builder.AppendLine("                if currentMatch == true then");
        builder.AppendLine($"                    local triggerContext = {{ object = triggerObject, player = player, {contextField} = currentValue }}");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 5, 0);
        if (!emitted)
        {
            builder.AppendLine($"                    print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("                end");
        builder.AppendLine("            end");
        builder.AppendLine("        end");
        builder.AppendLine("    end");
        builder.AppendLine("end");
    }

    private static readonly HashSet<string> ValueWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnNumberReachedAtLeast",
        "OnNumberDroppedToAtMost",
        "OnNumberEnteredRange",
        "OnNumberLeftRange",
        "OnTextBecame",
        "OnTextContains",
        "OnBooleanBecameTrue",
        "OnBooleanBecameFalse"
    };

    private static readonly HashSet<string> VariableWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnVariableNumberReachedAtLeast",
        "OnVariableNumberDroppedToAtMost",
        "OnVariableTextBecame",
        "OnVariableBooleanBecameTrue",
        "OnVariableBooleanBecameFalse",
        "OnVariableBecameEmpty",
        "OnVariableBecameNotEmpty"
    };

    private static readonly HashSet<string> StateMatchWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnStateBecameTrue",
        "OnStateBecameFalse",
        "OnStateBecameEmpty",
        "OnStateBecameNotEmpty"
    };

    private static readonly HashSet<string> ChangedValueWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnNumberChanged",
        "OnTextChanged",
        "OnBooleanChanged"
    };

    private static readonly HashSet<string> ChangedVariableWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnVariableNumberChanged",
        "OnVariableTextChanged",
        "OnVariableBooleanChanged"
    };

    private static readonly HashSet<string> PlayerCountWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnPlayerCountReached",
        "OnPlayerCountDroppedTo",
        "OnEnoughPlayers",
        "OnNotEnoughPlayers"
    };

    private static readonly HashSet<string> ObjectMovementWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnObjectStartedMoving",
        "OnObjectStoppedMoving",
        "OnObjectSpeedReached",
        "OnObjectSpeedDroppedTo"
    };

    private static readonly HashSet<string> ObjectAreaWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnObjectEnteredArea",
        "OnObjectLeftArea",
        "OnObjectEnteredBoxArea",
        "OnObjectLeftBoxArea",
        "OnObjectEnteredHeightBand",
        "OnObjectLeftHeightBand"
    };

    private static readonly HashSet<string> ObjectNumericWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnObjectXPositionReached",
        "OnObjectHeightPositionReached",
        "OnObjectHeightPositionDroppedTo",
        "OnObjectTransparencyReached",
        "OnObjectTransparencyDroppedTo",
        "OnObjectTurnAngleReached",
        "OnObjectTurnAngleDroppedTo",
        "OnObjectWidthSizeReached",
        "OnObjectWidthSizeDroppedTo",
        "OnObjectHeightSizeReached",
        "OnObjectHeightSizeDroppedTo"
    };

    private static void AppendReadableValueWatcherTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        AppendReadableValueWatcherReadFunction(builder, rule, trigger, nodesById, 1);
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, ", watchedValue = currentValue", 1);
        builder.AppendLine("end");
    }

    private static void AppendReadableValueWatcherReadFunction(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);

        if (trigger.Type.Equals("OnNumberReachedAtLeast", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnNumberDroppedToAtMost", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, trigger, nodesById, "value", "Number", "0");
            var limitKey = trigger.Type.Equals("OnNumberReachedAtLeast", StringComparison.OrdinalIgnoreCase) ? "minimum" : "maximum";
            var limit = ParameterExpression(rule, trigger, nodesById, limitKey, "Number", trigger.Type.Equals("OnNumberReachedAtLeast", StringComparison.OrdinalIgnoreCase) ? "10" : "0");
            var comparison = trigger.Type.Equals("OnNumberReachedAtLeast", StringComparison.OrdinalIgnoreCase) ? ">=" : "<=";

            builder.AppendLine($"{indent}local function readMatched()");
            builder.AppendLine($"{indent}    local watchedValue = tonumber({value.Code}) or 0");
            builder.AppendLine($"{indent}    local limitValue = tonumber({limit.Code}) or 0");
            builder.AppendLine($"{indent}    return watchedValue {comparison} limitValue, watchedValue");
            builder.AppendLine($"{indent}end");
            return;
        }

        if (trigger.Type.Equals("OnNumberEnteredRange", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnNumberLeftRange", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, trigger, nodesById, "value", "Number", "0");
            var min = ParameterExpression(rule, trigger, nodesById, "min", "Number", "0");
            var max = ParameterExpression(rule, trigger, nodesById, "max", "Number", "10");
            var expectsInside = trigger.Type.Equals("OnNumberEnteredRange", StringComparison.OrdinalIgnoreCase);

            builder.AppendLine($"{indent}local function readMatched()");
            builder.AppendLine($"{indent}    local watchedValue = tonumber({value.Code}) or 0");
            builder.AppendLine($"{indent}    local minValue = tonumber({min.Code}) or 0");
            builder.AppendLine($"{indent}    local maxValue = tonumber({max.Code}) or minValue");
            builder.AppendLine($"{indent}    if minValue > maxValue then");
            builder.AppendLine($"{indent}        minValue, maxValue = maxValue, minValue");
            builder.AppendLine($"{indent}    end");
            builder.AppendLine($"{indent}    local insideRange = watchedValue >= minValue and watchedValue <= maxValue");
            builder.AppendLine($"{indent}    return {(expectsInside ? "insideRange" : "not insideRange")}, watchedValue");
            builder.AppendLine($"{indent}end");
            return;
        }

        if (trigger.Type.Equals("OnTextBecame", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnTextContains", StringComparison.OrdinalIgnoreCase))
        {
            var text = ParameterExpression(rule, trigger, nodesById, "text", "String", "");
            var otherKey = trigger.Type.Equals("OnTextBecame", StringComparison.OrdinalIgnoreCase) ? "expected" : "search";
            var other = ParameterExpression(rule, trigger, nodesById, otherKey, "String", "");
            var caseSensitive = ParameterExpression(rule, trigger, nodesById, "caseSensitive", "Boolean", "false");
            var comparisonExpression = trigger.Type.Equals("OnTextBecame", StringComparison.OrdinalIgnoreCase)
                ? "watchedText == otherText"
                : "string.find(watchedText, otherText, 1, true) ~= nil";

            builder.AppendLine($"{indent}local function readMatched()");
            builder.AppendLine($"{indent}    local watchedText = tostring({text.Code} or \"\")");
            builder.AppendLine($"{indent}    local otherText = tostring({other.Code} or \"\")");
            builder.AppendLine($"{indent}    if {caseSensitive.Code} ~= true then");
            builder.AppendLine($"{indent}        watchedText = string.lower(watchedText)");
            builder.AppendLine($"{indent}        otherText = string.lower(otherText)");
            builder.AppendLine($"{indent}    end");
            builder.AppendLine($"{indent}    return {comparisonExpression}, watchedText");
            builder.AppendLine($"{indent}end");
            return;
        }

        var valueExpression = ParameterExpression(rule, trigger, nodesById, "value", "Boolean", "false");
        var expectedBoolean = trigger.Type.Equals("OnBooleanBecameTrue", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
        builder.AppendLine($"{indent}local function readMatched()");
        builder.AppendLine($"{indent}    local watchedValue = {valueExpression.Code} == true");
        builder.AppendLine($"{indent}    return watchedValue == {expectedBoolean}, watchedValue");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendReadableVariableWatcherTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var name = ParameterExpression(rule, trigger, nodesById, "name", "String", "Score");
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    local watchedVariableName = tostring({name.Code})");
        AppendReadableVariableWatcherReadFunction(builder, rule, trigger, nodesById, 1);
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, ", variableName = watchedVariableName, variableValue = currentValue", 1);
        builder.AppendLine("end");
    }

    private static void AppendReadableVariableWatcherReadFunction(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);

        if (trigger.Type.Equals("OnVariableNumberReachedAtLeast", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnVariableNumberDroppedToAtMost", StringComparison.OrdinalIgnoreCase))
        {
            var limitKey = trigger.Type.Equals("OnVariableNumberReachedAtLeast", StringComparison.OrdinalIgnoreCase) ? "minimum" : "maximum";
            var limit = ParameterExpression(rule, trigger, nodesById, limitKey, "Number", trigger.Type.Equals("OnVariableNumberReachedAtLeast", StringComparison.OrdinalIgnoreCase) ? "10" : "0");
            var comparison = trigger.Type.Equals("OnVariableNumberReachedAtLeast", StringComparison.OrdinalIgnoreCase) ? ">=" : "<=";

            builder.AppendLine($"{indent}local function readMatched()");
            builder.AppendLine($"{indent}    local currentValue = tonumber(VRS.vars[watchedVariableName]) or 0");
            builder.AppendLine($"{indent}    local limitValue = tonumber({limit.Code}) or 0");
            builder.AppendLine($"{indent}    return currentValue {comparison} limitValue, currentValue");
            builder.AppendLine($"{indent}end");
            return;
        }

        if (trigger.Type.Equals("OnVariableTextBecame", StringComparison.OrdinalIgnoreCase))
        {
            var expected = ParameterExpression(rule, trigger, nodesById, "expected", "String", "");
            var caseSensitive = ParameterExpression(rule, trigger, nodesById, "caseSensitive", "Boolean", "false");

            builder.AppendLine($"{indent}local function readMatched()");
            builder.AppendLine($"{indent}    local currentValue = tostring(VRS.vars[watchedVariableName] or \"\")");
            builder.AppendLine($"{indent}    local expectedText = tostring({expected.Code} or \"\")");
            builder.AppendLine($"{indent}    if {caseSensitive.Code} ~= true then");
            builder.AppendLine($"{indent}        currentValue = string.lower(currentValue)");
            builder.AppendLine($"{indent}        expectedText = string.lower(expectedText)");
            builder.AppendLine($"{indent}    end");
            builder.AppendLine($"{indent}    return currentValue == expectedText, currentValue");
            builder.AppendLine($"{indent}end");
            return;
        }

        if (trigger.Type.Equals("OnVariableBooleanBecameTrue", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnVariableBooleanBecameFalse", StringComparison.OrdinalIgnoreCase))
        {
            var expectedBoolean = trigger.Type.Equals("OnVariableBooleanBecameTrue", StringComparison.OrdinalIgnoreCase) ? "true" : "false";

            builder.AppendLine($"{indent}local function readMatched()");
            builder.AppendLine($"{indent}    local currentValue = VRS.vars[watchedVariableName] == true");
            builder.AppendLine($"{indent}    return currentValue == {expectedBoolean}, currentValue");
            builder.AppendLine($"{indent}end");
            return;
        }

        var expectsEmpty = trigger.Type.Equals("OnVariableBecameEmpty", StringComparison.OrdinalIgnoreCase);
        builder.AppendLine($"{indent}local function readMatched()");
        builder.AppendLine($"{indent}    local currentValue = VRS.vars[watchedVariableName]");
        builder.AppendLine($"{indent}    local valueIsEmpty = currentValue == nil or tostring(currentValue) == \"\"");
        builder.AppendLine($"{indent}    return {(expectsEmpty ? "valueIsEmpty" : "not valueIsEmpty")}, currentValue");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendReadableStateMatchTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var state = ParameterExpression(rule, trigger, nodesById, "state", "String", "DoorOpen");
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    local watchedStateName = tostring({state.Code})");
        builder.AppendLine("    local function readMatched()");
        builder.AppendLine("        local currentValue = VRS.states[watchedStateName]");
        if (trigger.Type.Equals("OnStateBecameTrue", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("        return currentValue == true, currentValue");
        }
        else if (trigger.Type.Equals("OnStateBecameFalse", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("        return currentValue == false, currentValue");
        }
        else if (trigger.Type.Equals("OnStateBecameEmpty", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("        return currentValue == nil, currentValue");
        }
        else
        {
            builder.AppendLine("        return currentValue ~= nil, currentValue");
        }

        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, ", stateName = watchedStateName, stateValue = currentValue", 1);
        builder.AppendLine("end");
    }

    private static void AppendReadableChangedValueWatcherTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine("    local function readWatchedValue()");
        if (trigger.Type.Equals("OnNumberChanged", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, trigger, nodesById, "value", "Number", "0");
            builder.AppendLine($"        return tonumber({value.Code}) or 0");
        }
        else if (trigger.Type.Equals("OnTextChanged", StringComparison.OrdinalIgnoreCase))
        {
            var text = ParameterExpression(rule, trigger, nodesById, "text", "String", "");
            builder.AppendLine($"        return tostring({text.Code} or \"\")");
        }
        else
        {
            var value = ParameterExpression(rule, trigger, nodesById, "value", "Boolean", "false");
            builder.AppendLine($"        return {value.Code} == true");
        }

        builder.AppendLine("    end");
        AppendReadableAnyChangeLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, ", watchedValue = currentValue", 1);
        builder.AppendLine("end");
    }

    private static void AppendReadableChangedVariableWatcherTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var name = ParameterExpression(rule, trigger, nodesById, "name", "String", "Score");
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    local watchedVariableName = tostring({name.Code})");
        builder.AppendLine("    local function readWatchedValue()");
        if (trigger.Type.Equals("OnVariableNumberChanged", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("        return tonumber(VRS.vars[watchedVariableName]) or 0");
        }
        else if (trigger.Type.Equals("OnVariableTextChanged", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("        return tostring(VRS.vars[watchedVariableName] or \"\")");
        }
        else
        {
            builder.AppendLine("        return VRS.vars[watchedVariableName] == true");
        }

        builder.AppendLine("    end");
        AppendReadableAnyChangeLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, ", variableName = watchedVariableName, variableValue = currentValue", 1);
        builder.AppendLine("end");
    }

    private static void AppendReadablePlayerCountTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var parameterKey = trigger.Type.Equals("OnEnoughPlayers", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnNotEnoughPlayers", StringComparison.OrdinalIgnoreCase)
            ? "minimum"
            : "count";
        var fallbackLimit = parameterKey == "minimum" ? "2" : trigger.Type.Equals("OnPlayerCountReached", StringComparison.OrdinalIgnoreCase) ? "4" : "1";
        var limit = ParameterExpression(rule, trigger, nodesById, parameterKey, "Number", fallbackLimit);
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");
        var comparison = trigger.Type.Equals("OnPlayerCountReached", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnEnoughPlayers", StringComparison.OrdinalIgnoreCase)
            ? ">="
            : trigger.Type.Equals("OnPlayerCountDroppedTo", StringComparison.OrdinalIgnoreCase)
                ? "<="
                : "<";

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine("    if Players == nil or Players.GetPlayers == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: Players:GetPlayers is not available.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine($"    local playerLimit = tonumber({limit.Code}) or {fallbackLimit}");
        builder.AppendLine("    local function readMatched()");
        builder.AppendLine("        local currentCount = #Players:GetPlayers()");
        builder.AppendLine($"        return currentCount {comparison} playerLimit, currentCount");
        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, ", playerCount = currentValue", 1);
        builder.AppendLine("end");
    }

    private static void AppendReadableObjectMovementWatcherTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");
        var watchesSpeed = trigger.Type.Equals("OnObjectSpeedReached", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnObjectSpeedDroppedTo", StringComparison.OrdinalIgnoreCase);
        var limitKey = watchesSpeed ? "speed" : "movement";
        var fallbackLimit = watchesSpeed
            ? trigger.Type.Equals("OnObjectSpeedReached", StringComparison.OrdinalIgnoreCase) ? "5" : "1"
            : "0.1";
        var limit = ParameterExpression(rule, trigger, nodesById, limitKey, "Number", fallbackLimit);
        var comparison = trigger.Type.Equals("OnObjectStartedMoving", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnObjectSpeedReached", StringComparison.OrdinalIgnoreCase)
            ? ">="
            : "<=";
        var contextField = watchesSpeed ? "objectSpeed" : "objectMovement";

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine("    if triggerObject.Position == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target has no Position.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine($"    local checkSeconds = math.max(tonumber({interval.Code}) or 0.25, 0.001)");
        builder.AppendLine($"    local movementLimit = tonumber({limit.Code}) or {fallbackLimit}");
        builder.AppendLine("    local lastPosition = triggerObject.Position");
        builder.AppendLine("    local function readMatched()");
        builder.AppendLine("        local currentPosition = triggerObject.Position");
        builder.AppendLine("        local movedDistance = vrsDistanceBetweenPositions(lastPosition, currentPosition)");
        builder.AppendLine("        lastPosition = currentPosition");
        if (watchesSpeed)
        {
            builder.AppendLine("        local currentSpeed = movedDistance / checkSeconds");
            builder.AppendLine($"        return currentSpeed {comparison} movementLimit, currentSpeed");
        }
        else
        {
            builder.AppendLine($"        return movedDistance {comparison} movementLimit, movedDistance");
        }

        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, "checkSeconds", $", {contextField} = currentValue", 1);
        builder.AppendLine("end");
    }

    private static void AppendReadableObjectAreaWatcherTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);

        if (trigger.Type.Equals("OnObjectEnteredArea", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnObjectLeftArea", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableRoundAreaWatcherBody(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code);
        }
        else if (trigger.Type.Equals("OnObjectEnteredBoxArea", StringComparison.OrdinalIgnoreCase) ||
            trigger.Type.Equals("OnObjectLeftBoxArea", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableBoxAreaWatcherBody(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code);
        }
        else
        {
            AppendReadableHeightBandWatcherBody(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code);
        }

        builder.AppendLine("end");
    }

    private static void AppendReadableRoundAreaWatcherBody(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string intervalCode)
    {
        var center = ParameterExpression(rule, trigger, nodesById, "center", "String", "Target");
        var radius = ParameterExpression(rule, trigger, nodesById, "radius", "Number", "10");
        var expectsInside = trigger.Type.Equals("OnObjectEnteredArea", StringComparison.OrdinalIgnoreCase);

        builder.AppendLine($"    local centerObject = resolveTarget(triggerObject, {center.Code})");
        builder.AppendLine("    if centerObject == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: area center was not found.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    if triggerObject.Position == nil or centerObject.Position == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: a watched object has no Position.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine($"    local areaRadius = math.max(0, tonumber({radius.Code}) or 10)");
        builder.AppendLine("    local function readMatched()");
        builder.AppendLine("        local currentDistance = vrsDistanceBetweenPositions(triggerObject.Position, centerObject.Position)");
        builder.AppendLine("        local insideArea = currentDistance <= areaRadius");
        builder.AppendLine($"        return {(expectsInside ? "insideArea" : "not insideArea")}, currentDistance");
        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, intervalCode, ", areaValue = currentValue, areaCenter = centerObject", 1);
    }

    private static void AppendReadableBoxAreaWatcherBody(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string intervalCode)
    {
        var center = ParameterExpression(rule, trigger, nodesById, "center", "String", "Target");
        var width = ParameterExpression(rule, trigger, nodesById, "width", "Number", "10");
        var height = ParameterExpression(rule, trigger, nodesById, "height", "Number", "10");
        var depth = ParameterExpression(rule, trigger, nodesById, "depth", "Number", "10");
        var expectsInside = trigger.Type.Equals("OnObjectEnteredBoxArea", StringComparison.OrdinalIgnoreCase);

        builder.AppendLine($"    local centerObject = resolveTarget(triggerObject, {center.Code})");
        builder.AppendLine("    if centerObject == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: area center was not found.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    if triggerObject.Position == nil or centerObject.Position == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: a watched object has no Position.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine($"    local halfWidth = math.max(0, tonumber({width.Code}) or 10) / 2");
        builder.AppendLine($"    local halfHeight = math.max(0, tonumber({height.Code}) or 10) / 2");
        builder.AppendLine($"    local halfDepth = math.max(0, tonumber({depth.Code}) or 10) / 2");
        builder.AppendLine("    local function readMatched()");
        builder.AppendLine("        local dx = math.abs(vrsValueAxis(triggerObject.Position, \"X\", \"x\", 0) - vrsValueAxis(centerObject.Position, \"X\", \"x\", 0))");
        builder.AppendLine("        local dy = math.abs(vrsValueAxis(triggerObject.Position, \"Y\", \"y\", 0) - vrsValueAxis(centerObject.Position, \"Y\", \"y\", 0))");
        builder.AppendLine("        local dz = math.abs(vrsValueAxis(triggerObject.Position, \"Z\", \"z\", 0) - vrsValueAxis(centerObject.Position, \"Z\", \"z\", 0))");
        builder.AppendLine("        local insideBox = dx <= halfWidth and dy <= halfHeight and dz <= halfDepth");
        builder.AppendLine($"        return {(expectsInside ? "insideBox" : "not insideBox")}, insideBox");
        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, intervalCode, ", areaValue = currentValue, areaCenter = centerObject", 1);
    }

    private static void AppendReadableHeightBandWatcherBody(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string intervalCode)
    {
        var minHeight = ParameterExpression(rule, trigger, nodesById, "minHeight", "Number", "0");
        var maxHeight = ParameterExpression(rule, trigger, nodesById, "maxHeight", "Number", "10");
        var expectsInside = trigger.Type.Equals("OnObjectEnteredHeightBand", StringComparison.OrdinalIgnoreCase);

        builder.AppendLine("    if triggerObject.Position == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target has no Position.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine($"    local bandMin = tonumber({minHeight.Code}) or 0");
        builder.AppendLine($"    local bandMax = tonumber({maxHeight.Code}) or 10");
        builder.AppendLine("    if bandMin > bandMax then");
        builder.AppendLine("        bandMin, bandMax = bandMax, bandMin");
        builder.AppendLine("    end");
        builder.AppendLine("    local function readMatched()");
        builder.AppendLine("        local currentHeight = vrsValueAxis(triggerObject.Position, \"Y\", \"y\", 0)");
        builder.AppendLine("        local insideBand = currentHeight >= bandMin and currentHeight <= bandMax");
        builder.AppendLine($"        return {(expectsInside ? "insideBand" : "not insideBand")}, currentHeight");
        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, intervalCode, ", areaValue = currentValue", 1);
    }

    private static void AppendReadableObjectNumericWatcherTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");
        var definition = ObjectNumericWatcherDefinition.For(trigger.Type);
        var limit = ParameterExpression(rule, trigger, nodesById, definition.ParameterKey, "Number", definition.FallbackLimit);

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    if triggerObject.{definition.ObjectProperty} == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target does not expose {definition.ObjectProperty}.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    local function readMatched()");
        if (definition.AxisUpper is null)
        {
            builder.AppendLine($"        local currentValue = tonumber(triggerObject.{definition.ObjectProperty}) or {definition.FallbackCurrent}");
        }
        else
        {
            builder.AppendLine($"        local currentValue = vrsValueAxis(triggerObject.{definition.ObjectProperty}, \"{definition.AxisUpper}\", \"{definition.AxisLower}\", {definition.FallbackCurrent})");
        }

        builder.AppendLine($"        local limitValue = tonumber({limit.Code}) or {definition.FallbackLimit}");
        builder.AppendLine($"        return currentValue {definition.Comparison} limitValue, currentValue");
        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, ", watchedValue = currentValue", 1);
        builder.AppendLine("end");
    }

    private static void AppendReadableObjectCollisionChangedTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine("    local function readWatchedObjectValue()");
        builder.AppendLine("        if triggerObject.CanCollide == nil then");
        builder.AppendLine($"            print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target does not expose CanCollide.\")");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        builder.AppendLine("        return triggerObject.CanCollide == true");
        builder.AppendLine("    end");
        // Changed watchers still snapshot first: they fire only after the first observed change.
        builder.AppendLine("    local previousValue = readWatchedObjectValue()");
        builder.AppendLine("    if previousValue == nil then");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    while true do");
        builder.AppendLine($"        wait({interval.Code})");
        builder.AppendLine("        local currentValue = readWatchedObjectValue()");
        builder.AppendLine("        if currentValue == nil then");
        builder.AppendLine("            return");
        builder.AppendLine("        end");
        builder.AppendLine("        if currentValue ~= previousValue then");
        builder.AppendLine("            previousValue = currentValue");
        builder.AppendLine("            local triggerContext = { object = triggerObject, objectCollisionOn = currentValue }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 3, 0);
        if (!emitted)
        {
            builder.AppendLine($"            print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("        end");
        builder.AppendLine("    end");
        builder.AppendLine("end");
    }

    private static void AppendReadableTrueTransitionLoop(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string intervalCode,
        string contextFields,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);

        // The first read is a baseline snapshot, so a value that already matches
        // at startup does not fire until it leaves and re-enters the watched state.
        builder.AppendLine($"{indent}local previousMatched = readMatched() == true");
        builder.AppendLine($"{indent}while true do");
        builder.AppendLine($"{indent}    wait({intervalCode})");
        builder.AppendLine($"{indent}    local currentMatched, currentValue = readMatched()");
        builder.AppendLine($"{indent}    currentMatched = currentMatched == true");
        builder.AppendLine($"{indent}    if currentMatched and not previousMatched then");
        builder.AppendLine($"{indent}        local triggerContext = {{ object = triggerObject{contextFields} }}");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", indentLevel + 2, 0);
        if (!emitted)
        {
            builder.AppendLine($"{indent}        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine($"{indent}    end");
        builder.AppendLine($"{indent}    previousMatched = currentMatched");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendReadableAnyChangeLoop(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string intervalCode,
        string contextFields,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);

        // Changed watchers also snapshot first; initial values are baseline, not events.
        builder.AppendLine($"{indent}local previousValue = readWatchedValue()");
        builder.AppendLine($"{indent}while true do");
        builder.AppendLine($"{indent}    wait({intervalCode})");
        builder.AppendLine($"{indent}    local currentValue = readWatchedValue()");
        builder.AppendLine($"{indent}    if currentValue ~= previousValue then");
        builder.AppendLine($"{indent}        previousValue = currentValue");
        builder.AppendLine($"{indent}        local triggerContext = {{ object = triggerObject{contextFields} }}");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", indentLevel + 2, 0);
        if (!emitted)
        {
            builder.AppendLine($"{indent}        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine($"{indent}    end");
        builder.AppendLine($"{indent}end");
    }

    private sealed record ObjectNumericWatcherDefinition(
        string ObjectProperty,
        string? AxisUpper,
        string? AxisLower,
        string ParameterKey,
        string FallbackLimit,
        string FallbackCurrent,
        string Comparison)
    {
        public static ObjectNumericWatcherDefinition For(string triggerType)
        {
            return triggerType switch
            {
                "OnObjectXPositionReached" => new("Position", "X", "x", "x", "10", "0", ">="),
                "OnObjectHeightPositionReached" => new("Position", "Y", "y", "height", "10", "0", ">="),
                "OnObjectHeightPositionDroppedTo" => new("Position", "Y", "y", "height", "0", "0", "<="),
                "OnObjectTransparencyReached" => new("Transparency", null, null, "transparency", "1", "0", ">="),
                "OnObjectTransparencyDroppedTo" => new("Transparency", null, null, "transparency", "0", "0", "<="),
                "OnObjectTurnAngleReached" => new("Rotation", "Y", "y", "angle", "90", "0", ">="),
                "OnObjectTurnAngleDroppedTo" => new("Rotation", "Y", "y", "angle", "0", "0", "<="),
                "OnObjectWidthSizeReached" => new("Scale", "X", "x", "size", "2", "1", ">="),
                "OnObjectWidthSizeDroppedTo" => new("Scale", "X", "x", "size", "1", "1", "<="),
                "OnObjectHeightSizeReached" => new("Scale", "Y", "y", "size", "2", "1", ">="),
                "OnObjectHeightSizeDroppedTo" => new("Scale", "Y", "y", "size", "1", "1", "<="),
                _ => throw new ArgumentOutOfRangeException(nameof(triggerType), triggerType, "Unknown object numeric watcher trigger.")
            };
        }
    }

    private static void AppendReadableScriptVariableChangedTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var name = ParameterExpression(rule, trigger, nodesById, "name", "String", "Score");
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    local watchedVariableName = tostring({name.Code})");
        builder.AppendLine("    local previousValue = VRS.vars[watchedVariableName]");
        builder.AppendLine("    while true do");
        builder.AppendLine($"        wait({interval.Code})");
        builder.AppendLine("        local currentValue = VRS.vars[watchedVariableName]");
        builder.AppendLine("        if currentValue ~= previousValue then");
        builder.AppendLine("            previousValue = currentValue");
        builder.AppendLine("            local triggerContext = { object = triggerObject, variableName = watchedVariableName, variableValue = currentValue }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 3, 0);
        if (!emitted)
        {
            builder.AppendLine("            print(\"On Script Variable Changed trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("        end");
        builder.AppendLine("    end");
        builder.AppendLine("end");
    }

    private static void AppendReadableSceneTouchTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        builder.AppendLine($"local function {functionName}()");
        builder.AppendLine("    local scriptParent = script.Parent");
        builder.AppendLine($"    local listenObject = {ReadableTriggerTargetExpression(plan, trigger, "scriptParent")}");
        builder.AppendLine("    if listenObject == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: touch target was not found.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    if listenObject.Touched == nil or listenObject.Touched.Connect == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target has no Touched event.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    listenObject.Touched:Connect(function(hit)");
        builder.AppendLine("        local triggerObject = listenObject");
        builder.AppendLine("        local triggerContext = { object = triggerObject, touchObject = hit, touchObjectSource = listenObject }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
        if (!emitted)
        {
            builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("    end)");
        builder.AppendLine("end");
    }

    private static void AppendReadableObbyTouchTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var contextKey = trigger.Type.Equals("OnCheckpointTouched", StringComparison.OrdinalIgnoreCase)
            ? "checkpointObject"
            : trigger.Type.Equals("OnHazardTouched", StringComparison.OrdinalIgnoreCase)
                ? "hazardObject"
                : trigger.Type.Equals("OnFinishTouched", StringComparison.OrdinalIgnoreCase)
                    ? "finishObject"
                    : "touchObjectSource";

        builder.AppendLine($"local function {functionName}()");
        builder.AppendLine("    local scriptParent = script.Parent");
        builder.AppendLine($"    local listenObject = {ReadableTriggerTargetExpression(plan, trigger, "scriptParent")}");
        builder.AppendLine("    if listenObject == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: touch target was not found.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    if listenObject.Touched == nil or listenObject.Touched.Connect == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target has no Touched event.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        if (trigger.DebugEnabled)
        {
            builder.AppendLine($"    print(\"{EscapeForDoubleQuotedString(trigger.Label)} debug: touch target resolved: \" .. tostring((listenObject ~= nil and listenObject.Name) or listenObject))");
        }

        builder.AppendLine("    listenObject.Touched:Connect(function(hit)");
        if (trigger.DebugEnabled)
        {
            builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} debug: touch received: \" .. tostring(hit))");
        }

        builder.AppendLine("        local touchingPlayer = vrsResolveTouchingPlayer(hit)");
        builder.AppendLine("        if touchingPlayer == nil then");
        if (trigger.DebugEnabled)
        {
            builder.AppendLine($"            print(\"{EscapeForDoubleQuotedString(trigger.Label)} debug: no player resolved from touch hit.\")");
        }

        builder.AppendLine("            return");
        builder.AppendLine("        end");
        if (trigger.DebugEnabled)
        {
            builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} debug: player resolved: \" .. tostring((touchingPlayer ~= nil and touchingPlayer.Name) or touchingPlayer))");
        }

        builder.AppendLine("        local triggerObject = listenObject");
        builder.AppendLine($"        local triggerContext = {{ object = triggerObject, player = touchingPlayer, touchObject = hit, {contextKey} = listenObject }}");
        if (trigger.DebugEnabled)
        {
            builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} debug: launching connected flow.\")");
        }

        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
        if (!emitted)
        {
            builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("    end)");
        builder.AppendLine("end");
    }

    private static bool AppendReadableFlowFromNode(
        StringBuilder builder,
        Rule rule,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        string fromNodeId,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string triggerObjectName,
        string triggerContextName,
        int indentLevel,
        int depth)
    {
        var indent = IndentText(indentLevel);
        if (depth > 128)
        {
            builder.AppendLine($"{indent}print(\"Flow stopped after 128 steps to avoid an infinite loop.\")");
            return true;
        }

        var outgoing = rule.Connections
            .Where(connection =>
                connection.ConnectionKind == GraphConnectionKind.Flow &&
                string.Equals(connection.From.NodeId, fromNodeId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(connection => FlowPortOrder(connection.From.PortId))
            .ThenBy(connection => connection.To.NodeId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var emittedAnyNode = false;
        foreach (var connection in outgoing)
        {
            if (!nodesById.TryGetValue(connection.To.NodeId, out var node) || !node.Enabled)
            {
                continue;
            }

            if (!visited.Add($"{connection.From.NodeId}:{connection.From.PortId}->{connection.To.NodeId}:{connection.To.PortId}"))
            {
                builder.AppendLine($"{indent}print(\"Skipped repeated flow into {EscapeForDoubleQuotedString(node.Label)}.\")");
                emittedAnyNode = true;
                continue;
            }

            reachedNodeIds.Add(node.Id);
            emittedAnyNode = true;
            switch (node.Kind)
            {
                case NodeKind.Action:
                    builder.AppendLine($"{indent}{RegistryFunctionReference(plan, node)}({triggerObjectName}, {triggerContextName})");
                    AppendReadableFlowFromNode(builder, rule, plan, nodesById, node.Id, visited, reachedNodeIds, triggerObjectName, triggerContextName, indentLevel, depth + 1);
                    break;
                case NodeKind.Condition:
                    AppendReadableConditionCall(builder, rule, node, plan, nodesById, visited, reachedNodeIds, triggerObjectName, triggerContextName, indentLevel, depth + 1);
                    break;
                case NodeKind.Trigger:
                    builder.AppendLine($"{indent}print(\"Skipped nested Trigger {EscapeForDoubleQuotedString(node.Label)}.\")");
                    break;
            }
        }

        return emittedAnyNode;
    }

    private static void AppendReadableConditionCall(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string triggerObjectName,
        string triggerContextName,
        int indentLevel,
        int depth)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if {RegistryFunctionReference(plan, condition)}({triggerObjectName}, {triggerContextName}) then");
        var trueEmitted = AppendReadableBranch(builder, rule, plan, nodesById, condition, GraphPortDefaults.TrueOut, visited, reachedNodeIds, triggerObjectName, triggerContextName, indentLevel + 1, depth);
        if (!trueEmitted)
        {
            builder.AppendLine($"{IndentText(indentLevel + 1)}print(\"{EscapeForDoubleQuotedString(condition.Label)} TRUE branch has no connected action.\")");
        }

        builder.AppendLine($"{indent}else");
        var falseEmitted = AppendReadableBranch(builder, rule, plan, nodesById, condition, GraphPortDefaults.FalseOut, visited, reachedNodeIds, triggerObjectName, triggerContextName, indentLevel + 1, depth);
        if (!falseEmitted)
        {
            builder.AppendLine($"{IndentText(indentLevel + 1)}print(\"{EscapeForDoubleQuotedString(condition.Label)} FALSE branch has no connected action.\")");
        }

        builder.AppendLine($"{indent}end");
    }

    private static bool AppendReadableBranch(
        StringBuilder builder,
        Rule rule,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        RuleNode condition,
        string portId,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string triggerObjectName,
        string triggerContextName,
        int indentLevel,
        int depth)
    {
        var branchConnections = rule.Connections
            .Where(connection =>
                connection.ConnectionKind == GraphConnectionKind.Flow &&
                string.Equals(connection.From.NodeId, condition.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(connection.From.PortId, portId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(connection => connection.To.NodeId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var emitted = false;
        foreach (var connection in branchConnections)
        {
            if (!nodesById.TryGetValue(connection.To.NodeId, out var node) || !node.Enabled)
            {
                continue;
            }

            emitted = true;
            if (!visited.Add($"{connection.From.NodeId}:{connection.From.PortId}->{connection.To.NodeId}:{connection.To.PortId}"))
            {
                builder.AppendLine($"{IndentText(indentLevel)}print(\"Skipped repeated flow into {EscapeForDoubleQuotedString(node.Label)}.\")");
                continue;
            }

            reachedNodeIds.Add(node.Id);
            if (node.Kind == NodeKind.Action)
            {
                builder.AppendLine($"{IndentText(indentLevel)}{RegistryFunctionReference(plan, node)}({triggerObjectName}, {triggerContextName})");
                AppendReadableFlowFromNode(builder, rule, plan, nodesById, node.Id, visited, reachedNodeIds, triggerObjectName, triggerContextName, indentLevel, depth + 1);
            }
            else if (node.Kind == NodeKind.Condition)
            {
                AppendReadableConditionCall(builder, rule, node, plan, nodesById, visited, reachedNodeIds, triggerObjectName, triggerContextName, indentLevel, depth + 1);
            }
        }

        return emitted;
    }

}
