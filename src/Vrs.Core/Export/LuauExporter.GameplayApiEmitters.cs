using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static readonly HashSet<string> PlayerDefaultProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "WalkSpeed",
        "JumpPower",
        "SprintSpeed",
        "MaxHealth",
        "RespawnTime",
        "Stamina"
    };

    private static readonly HashSet<string> TweenTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Linear",
        "Sine",
        "Quad",
        "Cubic",
        "Quart",
        "Quint",
        "Expo",
        "Circ",
        "Back",
        "Elastic",
        "Bounce"
    };

    private static readonly HashSet<string> TweenDirections = new(StringComparer.OrdinalIgnoreCase)
    {
        "In",
        "Out",
        "InOut"
    };

    private static readonly HashSet<string> InputActionKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Button",
        "Axis",
        "Vector2"
    };

    private static bool TryAppendReadableGameplayApiConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (TryAppendReadableAudioLightingConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableTeamApiConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableStatsConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableInventoryConditionBody(builder, rule, condition, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableSharedTableConditionBody(builder, rule, condition, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableScriptRuntimeConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableEnvironmentBoundsConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableRaycastConditionBody(builder, rule, condition, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableQuaternionConditionBody(builder, rule, condition, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableVector2ConditionBody(builder, rule, condition, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableToolApiConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableSeatConditionBody(builder, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableNpcConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableBodyPositionConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadablePhysicalConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableImage3DConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableText3DConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableCharacterAnimationConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableCharacterAppearanceConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableCustomUiConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableWorldMarkerConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableAssetMediaConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableTweenConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (condition.Type.Equals("PlayerExists", StringComparison.OrdinalIgnoreCase))
        {
            var username = ParameterExpression(rule, condition, nodesById, "username", "String", "");
            builder.AppendLine($"{indent}if Players == nil or Players.GetPlayer == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return Players:GetPlayer(tostring({username.Code})) ~= nil");
            return true;
        }

        if (condition.Type.Equals("PlayerCountAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            var minimum = ParameterExpression(rule, condition, nodesById, "minimum", "Number", "1");
            builder.AppendLine($"{indent}return ((Players ~= nil and Players.PlayersCount) or 0) >= {minimum.Code}");
            return true;
        }

        if (condition.Type.Equals("PlayerCountAtMost", StringComparison.OrdinalIgnoreCase))
        {
            var maximum = ParameterExpression(rule, condition, nodesById, "maximum", "Number", "8");
            builder.AppendLine($"{indent}return ((Players ~= nil and Players.PlayersCount) or 0) <= {maximum.Code}");
            return true;
        }

        if (condition.Type.Equals("PlayerDefaultAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            var property = SanitizedPlayerDefaultProperty(condition, BuildEffectiveParameterValues(rule, condition, nodesById), "WalkSpeed");
            var fallback = PlayerDefaultFallback(property);
            var minimum = ParameterExpression(rule, condition, nodesById, "value", "Number", fallback);
            builder.AppendLine($"{indent}if PlayerDefaults == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local currentDefaultValue = tonumber(PlayerDefaults.{property}) or {fallback}");
            builder.AppendLine($"{indent}local expectedDefaultValue = tonumber({minimum.Code}) or {fallback}");
            builder.AppendLine($"{indent}return currentDefaultValue >= expectedDefaultValue");
            return true;
        }

        if (condition.Type.Equals("InputButtonDown", StringComparison.OrdinalIgnoreCase))
        {
            var actionName = ParameterExpression(rule, condition, nodesById, "actionName", "String", "Interact");
            builder.AppendLine($"{indent}if Input == nil or Input.GetButton == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local buttonAction = Input:GetButton(tostring({actionName.Code}))");
            builder.AppendLine($"{indent}return buttonAction ~= nil and buttonAction.IsPressed == true");
            return true;
        }

        if (condition.Type.Equals("InputActionExists", StringComparison.OrdinalIgnoreCase))
        {
            var actionKind = SanitizedInputActionKind(condition, BuildEffectiveParameterValues(rule, condition, nodesById), "Button");
            var getter = InputActionGetter(actionKind);
            var actionName = ParameterExpression(rule, condition, nodesById, "actionName", "String", "Interact");
            builder.AppendLine($"{indent}if Input == nil or Input.{getter} == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return Input:{getter}(tostring({actionName.Code})) ~= nil");
            return true;
        }

        if (condition.Type.Equals("ObjectHasTag", StringComparison.OrdinalIgnoreCase))
        {
            var tagName = ParameterExpression(rule, condition, nodesById, "tag", "String", "");
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil or targetObject.HasTag == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return targetObject:HasTag(tostring({tagName.Code}))");
            return true;
        }

        if (condition.Type.Equals("ObjectIsA", StringComparison.OrdinalIgnoreCase))
        {
            var className = ParameterExpression(rule, condition, nodesById, "className", "String", "Part");
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil or targetObject.IsA == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return targetObject:IsA(tostring({className.Code}))");
            return true;
        }

        if (condition.Type.Equals("ObjectHasChild", StringComparison.OrdinalIgnoreCase))
        {
            var childName = ParameterExpression(rule, condition, nodesById, "childName", "String", "");
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil or targetObject.FindChild == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return targetObject:FindChild(tostring({childName.Code})) ~= nil");
            return true;
        }

        if (condition.Type.Equals("ObjectHasChildClass", StringComparison.OrdinalIgnoreCase))
        {
            var className = ParameterExpression(rule, condition, nodesById, "className", "String", "Part");
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil or targetObject.FindChildByClass == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return targetObject:FindChildByClass(tostring({className.Code})) ~= nil");
            return true;
        }

        if (condition.Type.Equals("ObjectHasChildren", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("ObjectHasNoChildren", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("ObjectChildCountAtLeast", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("ObjectChildCountAtMost", StringComparison.OrdinalIgnoreCase))
        {
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil or targetObject.GetChildren == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local children = targetObject:GetChildren()");
            builder.AppendLine($"{indent}local childCount = children == nil and 0 or #children");
            if (condition.Type.Equals("ObjectHasChildren", StringComparison.OrdinalIgnoreCase))
            {
                builder.AppendLine($"{indent}return childCount > 0");
                return true;
            }

            if (condition.Type.Equals("ObjectHasNoChildren", StringComparison.OrdinalIgnoreCase))
            {
                builder.AppendLine($"{indent}return childCount == 0");
                return true;
            }

            if (condition.Type.Equals("ObjectChildCountAtLeast", StringComparison.OrdinalIgnoreCase))
            {
                var minimum = ParameterExpression(rule, condition, nodesById, "minimum", "Number", "1");
                builder.AppendLine($"{indent}return childCount >= {minimum.Code}");
                return true;
            }

            var maximum = ParameterExpression(rule, condition, nodesById, "maximum", "Number", "5");
            builder.AppendLine($"{indent}return childCount <= {maximum.Code}");
            return true;
        }

        return false;
    }

    private static bool TryAppendReadableGameplayApiActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (TryAppendReadableTeamApiActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableStatsActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableDatastoreActionBody(builder, rule, action, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableToolApiActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableInventoryActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableSharedTableActionBody(builder, rule, action, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableScriptRuntimeActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableAssetMediaActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableAudioLightingActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableEffectsActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableMeshActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableCharacterAnimationActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableCharacterAppearanceActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableWorldMarkerActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableSeatActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableNpcActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableBodyPositionActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadablePhysicalActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableImage3DActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableText3DActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableCoreUiActionBody(builder, rule, action, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableCustomUiActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableWorldContainerActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableEnvironmentBoundsActionBody(builder, rule, action, nodesById, indentLevel))
        {
            return true;
        }

        if (action.Type.Equals("SetCameraFOV", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "FOV", "fov", "Number", "70", "Set Camera FOV");
            return true;
        }

        if (action.Type.Equals("SetValueObjectValue", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Value", "value", "Any", "", "Set Value Object");
            return true;
        }

        if (action.Type.Equals("SetIntegerValueObject", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, action, nodesById, "value", "Number", "0");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    print(\"Set Integer Value Object stopped: target was not found.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if targetObject.Value == nil then");
            builder.AppendLine($"{indent}    print(\"Set Integer Value Object stopped: target does not expose Value.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local integerValue = math.floor(tonumber({value.Code}) or 0)");
            builder.AppendLine($"{indent}targetObject.Value = integerValue");
            return true;
        }

        if (action.Type.Equals("SetInstanceValueObject", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, action, nodesById, "value", "String", "Self");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    print(\"Set Stored Object Reference stopped: target was not found.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if targetObject.Value == nil then");
            builder.AppendLine($"{indent}    print(\"Set Stored Object Reference stopped: target does not expose Value.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local storedObject = resolveTarget(triggerObject, {value.Code})");
            builder.AppendLine($"{indent}if storedObject == nil then");
            builder.AppendLine($"{indent}    print(\"Set Stored Object Reference stopped: stored object was not found.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}targetObject.Value = storedObject");
            return true;
        }

        if (action.Type.Equals("SetDecalImage", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Image", "image", "String", "", "Set Decal Image");
            return true;
        }

        if (action.Type.Equals("BroadcastChatMessage", StringComparison.OrdinalIgnoreCase))
        {
            var message = ParameterExpression(rule, action, nodesById, "message", "String", "");
            builder.AppendLine($"{indent}if Chat == nil or Chat.BroadcastMessage == nil then");
            builder.AppendLine($"{indent}    print(\"Broadcast Chat Message stopped: Chat:BroadcastMessage is not available.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}Chat:BroadcastMessage(tostring({message.Code}))");
            return true;
        }

        if (action.Type.Equals("SendChatMessageToPlayer", StringComparison.OrdinalIgnoreCase))
        {
            var message = ParameterExpression(rule, action, nodesById, "message", "String", "");
            var player = ParameterExpression(rule, action, nodesById, "player", "Any", "");
            var playerName = ParameterExpression(rule, action, nodesById, "playerName", "String", "");
            builder.AppendLine($"{indent}if Chat == nil or Chat.UnicastMessage == nil then");
            builder.AppendLine($"{indent}    print(\"Send Chat Message To Player stopped: Chat:UnicastMessage is not available.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local targetPlayer = {player.Code}");
            builder.AppendLine($"{indent}if targetPlayer == nil or targetPlayer == \"\" then");
            builder.AppendLine($"{indent}    if Players ~= nil and Players.GetPlayer ~= nil then");
            builder.AppendLine($"{indent}        targetPlayer = Players:GetPlayer(tostring({playerName.Code}))");
            builder.AppendLine($"{indent}    end");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if targetPlayer == nil then");
            builder.AppendLine($"{indent}    print(\"Send Chat Message To Player stopped: target player was not found.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}Chat:UnicastMessage(tostring({message.Code}), targetPlayer)");
            return true;
        }

        if (action.Type.Equals("BindInputButtonKey", StringComparison.OrdinalIgnoreCase))
        {
            var actionName = ParameterExpression(rule, action, nodesById, "actionName", "String", "Interact");
            var keyCode = KeyCodeLiteral(action, BuildEffectiveParameterValues(rule, action, nodesById));
            builder.AppendLine($"{indent}if Input == nil or Input.BindButton == nil then");
            builder.AppendLine($"{indent}    print(\"Bind Input Button Key stopped: Input:BindButton is not available.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if InputButton == nil or InputButton.New == nil or KeyCode == nil then");
            builder.AppendLine($"{indent}    print(\"Bind Input Button Key stopped: InputButton.New or KeyCode is not available.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local inputActionName = tostring({actionName.Code})");
            builder.AppendLine($"{indent}local buttonAction = Input:BindButton(inputActionName)");
            builder.AppendLine($"{indent}if buttonAction == nil or buttonAction.Buttons == nil or buttonAction.Buttons.AddButton == nil then");
            builder.AppendLine($"{indent}    print(\"Bind Input Button Key stopped: input action \" .. inputActionName .. \" has no button collection.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}buttonAction.Buttons:AddButton(InputButton.New({keyCode}))");
            return true;
        }

        if (action.Type.Equals("SetPlayerDefaultValue", StringComparison.OrdinalIgnoreCase))
        {
            var property = SanitizedPlayerDefaultProperty(action, BuildEffectiveParameterValues(rule, action, nodesById), "WalkSpeed");
            var fallback = PlayerDefaultFallback(property);
            var value = ParameterExpression(rule, action, nodesById, "value", "Number", fallback);
            builder.AppendLine($"{indent}if PlayerDefaults == nil then");
            builder.AppendLine($"{indent}    print(\"Set Player Default stopped: PlayerDefaults is not available.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}PlayerDefaults.{property} = {value.Code}");
            return true;
        }

        if (action.Type.Equals("TweenObjectPosition", StringComparison.OrdinalIgnoreCase))
        {
            AppendTweenVectorAction(builder, rule, action, plan, nodesById, indentLevel, "Position", "TweenPosition", "Tween Object Position", scaleDefaults: false);
            return true;
        }

        if (action.Type.Equals("TweenObjectRotation", StringComparison.OrdinalIgnoreCase))
        {
            AppendTweenVectorAction(builder, rule, action, plan, nodesById, indentLevel, "Rotation", "TweenRotation", "Tween Object Rotation", scaleDefaults: false);
            return true;
        }

        if (action.Type.Equals("TweenObjectScale", StringComparison.OrdinalIgnoreCase))
        {
            AppendTweenVectorAction(builder, rule, action, plan, nodesById, indentLevel, "Scale", "TweenSize", "Tween Object Scale", scaleDefaults: true);
            return true;
        }

        if (action.Type.Equals("TweenObjectColor", StringComparison.OrdinalIgnoreCase))
        {
            AppendTweenColorAction(builder, rule, action, plan, nodesById, indentLevel);
            return true;
        }

        if (action.Type.Equals("TweenObjectTransparency", StringComparison.OrdinalIgnoreCase))
        {
            AppendTweenNumberPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Transparency", "transparency", "Animate Object Transparency");
            return true;
        }

        return false;
    }

    private static void AppendReadablePlayerDefaultTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var property = SanitizedPlayerDefaultProperty(trigger, BuildEffectiveParameterValues(rule, trigger, nodesById), "WalkSpeed");
        var fallback = PlayerDefaultFallback(property);
        var limit = ParameterExpression(rule, trigger, nodesById, "value", "Number", fallback);
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine("    if PlayerDefaults == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: PlayerDefaults is not available.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine($"    local watchedDefaultLimit = tonumber({limit.Code}) or {fallback}");
        builder.AppendLine("    local function readMatched()");
        builder.AppendLine($"        local currentValue = tonumber(PlayerDefaults.{property}) or {fallback}");
        builder.AppendLine("        return currentValue >= watchedDefaultLimit, currentValue");
        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, $", playerDefaultName = \"{property}\", playerDefaultValue = currentValue", 1);
        builder.AppendLine("end");
    }

    private static LuauExpression? TryResolveReadableGameplayApiPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        if (TryResolveReadableObbyPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } obbyExpression)
        {
            return obbyExpression;
        }

        if (TryResolveReadableAudioLightingPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } audioLightingExpression)
        {
            return audioLightingExpression;
        }

        if (TryResolveReadableEffectsPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } effectsExpression)
        {
            return effectsExpression;
        }

        if (TryResolveReadableMeshPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } meshExpression)
        {
            return meshExpression;
        }

        if (TryResolveReadableCharacterAnimationPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } characterExpression)
        {
            return characterExpression;
        }

        if (TryResolveReadableCharacterAppearancePropertyExpression(rule, node, nodesById, visitedNodeIds) is { } characterAppearanceExpression)
        {
            return characterAppearanceExpression;
        }

        if (TryResolveReadableWorldMarkerPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } worldMarkerExpression)
        {
            return worldMarkerExpression;
        }

        if (TryResolveReadableEnvironmentBoundsPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } environmentBoundsExpression)
        {
            return environmentBoundsExpression;
        }

        if (TryResolveReadableRaycastPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } raycastExpression)
        {
            return raycastExpression;
        }

        if (TryResolveReadableQuaternionPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } quaternionExpression)
        {
            return quaternionExpression;
        }

        if (TryResolveReadableColorSeriesPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } colorSeriesExpression)
        {
            return colorSeriesExpression;
        }

        if (TryResolveReadableVector2PropertyExpression(rule, node, nodesById, visitedNodeIds) is { } vector2Expression)
        {
            return vector2Expression;
        }

        if (TryResolveReadableSeatPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } seatExpression)
        {
            return seatExpression;
        }

        if (TryResolveReadableNpcPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } npcExpression)
        {
            return npcExpression;
        }

        if (TryResolveReadableBodyPositionPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } bodyPositionExpression)
        {
            return bodyPositionExpression;
        }

        if (TryResolveReadablePhysicalPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } physicalExpression)
        {
            return physicalExpression;
        }

        if (TryResolveReadableImage3DPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } image3DExpression)
        {
            return image3DExpression;
        }

        if (TryResolveReadableText3DPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } text3DExpression)
        {
            return text3DExpression;
        }

        if (TryResolveReadableTeamApiPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } teamExpression)
        {
            return teamExpression;
        }

        if (TryResolveReadableStatsPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } statsExpression)
        {
            return statsExpression;
        }

        if (TryResolveReadableDatastorePropertyExpression(rule, node, nodesById, visitedNodeIds) is { } datastoreExpression)
        {
            return datastoreExpression;
        }

        if (TryResolveReadableToolApiPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } toolExpression)
        {
            return toolExpression;
        }

        if (TryResolveReadableInventoryPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } inventoryExpression)
        {
            return inventoryExpression;
        }

        if (TryResolveReadableSharedTablePropertyExpression(rule, node, nodesById, visitedNodeIds) is { } sharedTableExpression)
        {
            return sharedTableExpression;
        }

        if (TryResolveReadableScriptRuntimePropertyExpression(rule, node, nodesById, visitedNodeIds) is { } scriptRuntimeExpression)
        {
            return scriptRuntimeExpression;
        }

        if (TryResolveReadableAssetMediaPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } assetMediaExpression)
        {
            return assetMediaExpression;
        }

        if (TryResolveReadableCoreUiPropertyExpression(node) is { } coreUiExpression)
        {
            return coreUiExpression;
        }

        if (TryResolveReadableCustomUiPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } customUiExpression)
        {
            return customUiExpression;
        }

        if (node.Type.Equals("TriggeringPlayer", StringComparison.OrdinalIgnoreCase) ||
            node.Type.Equals("TriggeringChatPlayer", StringComparison.OrdinalIgnoreCase))
        {
            return new LuauExpression("((triggerContext ~= nil and triggerContext.player) or nil)", "Any");
        }

        if (node.Type.Equals("TriggeringChatMessage", StringComparison.OrdinalIgnoreCase))
        {
            return new LuauExpression("tostring((triggerContext ~= nil and triggerContext.message) or \"\")", "String");
        }

        if (node.Type.Equals("TriggeringInputAction", StringComparison.OrdinalIgnoreCase))
        {
            return new LuauExpression("tostring((triggerContext ~= nil and triggerContext.inputAction) or \"\")", "String");
        }

        if (node.Type.Equals("TriggeringInputValue", StringComparison.OrdinalIgnoreCase))
        {
            return new LuauExpression("((triggerContext ~= nil and triggerContext.inputValue) or nil)", "Any");
        }

        if (node.Type.Equals("TriggeringInputText", StringComparison.OrdinalIgnoreCase))
        {
            return new LuauExpression("(function() local inputMessage = ((triggerContext ~= nil and triggerContext.inputMessage) or nil); if inputMessage == nil or inputMessage.GetString == nil then return \"\" end; local ok, value = pcall(function() return inputMessage:GetString(1) end); if ok and value ~= nil then return tostring(value) end; return \"\" end)()", "String");
        }

        if (node.Type.Equals("TriggeringBindablePayload", StringComparison.OrdinalIgnoreCase))
        {
            return new LuauExpression("tostring((triggerContext ~= nil and triggerContext.payload) or \"\")", "String");
        }

        if (node.Type.Equals("LocalPlayer", StringComparison.OrdinalIgnoreCase))
        {
            return new LuauExpression("((Players ~= nil and Players.LocalPlayer) or nil)", "Any");
        }

        if (node.Type.Equals("PlayerCount", StringComparison.OrdinalIgnoreCase))
        {
            return new LuauExpression("((Players ~= nil and Players.PlayersCount) or 0)", "Number");
        }

        if (node.Type.Equals("FindPlayerByName", StringComparison.OrdinalIgnoreCase))
        {
            var username = PropertyParameterExpression(rule, node, nodesById, "username", "String", "", visitedNodeIds);
            return new LuauExpression($"(function() if Players == nil or Players.GetPlayer == nil then return nil end return Players:GetPlayer(tostring({username.Code})) end)()", "Any");
        }

        if (node.Type.Equals("FindPlayerByID", StringComparison.OrdinalIgnoreCase))
        {
            var userId = PropertyParameterExpression(rule, node, nodesById, "userId", "Number", "0", visitedNodeIds);
            return new LuauExpression($"(function() if Players == nil or Players.GetPlayerByID == nil then return nil end return Players:GetPlayerByID({userId.Code}) end)()", "Any");
        }

        if (node.Type.Equals("InputAxisValue", StringComparison.OrdinalIgnoreCase))
        {
            var actionName = PropertyParameterExpression(rule, node, nodesById, "actionName", "String", "Move", visitedNodeIds);
            return new LuauExpression($"(function() if Input == nil or Input.GetAxis == nil then return 0 end local axis = Input:GetAxis(tostring({actionName.Code})); if type(axis) == \"number\" then return axis end if axis ~= nil and axis.Value ~= nil then return axis.Value end return 0 end)()", "Number");
        }

        if (node.Type.Equals("InputVectorX", StringComparison.OrdinalIgnoreCase))
        {
            return InputVectorAxisExpression(rule, node, nodesById, visitedNodeIds, "X", "x");
        }

        if (node.Type.Equals("InputVectorY", StringComparison.OrdinalIgnoreCase))
        {
            return InputVectorAxisExpression(rule, node, nodesById, visitedNodeIds, "Y", "y");
        }

        if (node.Type.Equals("InputButtonFromKey", StringComparison.OrdinalIgnoreCase))
        {
            var keyCode = KeyCodeLiteral(node, BuildEffectiveParameterValues(rule, node, nodesById));
            return new LuauExpression($"(function() if InputButton == nil or InputButton.New == nil or KeyCode == nil then return nil end return InputButton.New({keyCode}) end)()", "Any");
        }

        if (node.Type.Equals("PlayerDefaultValue", StringComparison.OrdinalIgnoreCase))
        {
            var property = SanitizedPlayerDefaultProperty(node, BuildEffectiveParameterValues(rule, node, nodesById), "WalkSpeed");
            var fallback = PlayerDefaultFallback(property);
            return new LuauExpression($"((PlayerDefaults ~= nil and PlayerDefaults.{property}) or {fallback})", "Number");
        }

        if (node.Type.Equals("FindChild", StringComparison.OrdinalIgnoreCase))
        {
            var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Self", visitedNodeIds);
            var childName = PropertyParameterExpression(rule, node, nodesById, "childName", "String", "", visitedNodeIds);
            return new LuauExpression($"(function() local targetObject = resolveTarget(triggerObject, {target.Code}); if targetObject == nil or targetObject.FindChild == nil then return nil end return targetObject:FindChild(tostring({childName.Code})) end)()", "Any");
        }

        if (node.Type.Equals("FindChildByClass", StringComparison.OrdinalIgnoreCase))
        {
            var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Self", visitedNodeIds);
            var className = PropertyParameterExpression(rule, node, nodesById, "className", "String", "Part", visitedNodeIds);
            return new LuauExpression($"(function() local targetObject = resolveTarget(triggerObject, {target.Code}); if targetObject == nil or targetObject.FindChildByClass == nil then return nil end return targetObject:FindChildByClass(tostring({className.Code})) end)()", "Any");
        }

        if (node.Type.Equals("ObjectChildCount", StringComparison.OrdinalIgnoreCase))
        {
            var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Self", visitedNodeIds);
            return new LuauExpression($"(function() local targetObject = resolveTarget(triggerObject, {target.Code}); if targetObject == nil or targetObject.GetChildren == nil then return 0 end local children = targetObject:GetChildren(); if children == nil then return 0 end return #children end)()", "Number");
        }

        if (node.Type.Equals("CameraFOVValue", StringComparison.OrdinalIgnoreCase))
        {
            return ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "FOV", "Number", "Camera FOV", "return tonumber(targetObject.FOV) or 0");
        }

        if (node.Type.Equals("ValueObjectValue", StringComparison.OrdinalIgnoreCase))
        {
            return ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Value", "Any", "Value Object", "return targetObject.Value");
        }

        if (node.Type.Equals("IntegerValueObjectValue", StringComparison.OrdinalIgnoreCase))
        {
            return ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Value", "Number", "Integer Value Object", "return math.floor(tonumber(targetObject.Value) or 0)");
        }

        if (node.Type.Equals("InstanceValueObjectValue", StringComparison.OrdinalIgnoreCase))
        {
            return ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Value", "Any", "Stored Object Reference", "return targetObject.Value");
        }

        if (node.Type.Equals("WorldIsLocalTestValue", StringComparison.OrdinalIgnoreCase))
        {
            return WorldValueExpression("IsLocalTest", "Boolean", "Local Test Is Running", "return World.IsLocalTest == true");
        }

        if (node.Type.Equals("WorldIsOldFormatValue", StringComparison.OrdinalIgnoreCase))
        {
            return WorldValueExpression("IsLegacyWorld", "Boolean", "Old World Format", "return World.IsLegacyWorld == true");
        }

        if (node.Type.Equals("WorldIdentifierValue", StringComparison.OrdinalIgnoreCase))
        {
            return WorldValueExpression("WorldID", "String", "World Identifier", "return tostring(World.WorldID or \"\")");
        }

        if (node.Type.Equals("ServerIdentifierValue", StringComparison.OrdinalIgnoreCase))
        {
            return WorldValueExpression("ServerID", "String", "Server Identifier", "return tostring(World.ServerID or \"\")");
        }

        if (node.Type.Equals("WorldUptimeValue", StringComparison.OrdinalIgnoreCase))
        {
            return WorldValueExpression("UpTime", "Number", "World Uptime", "return tonumber(World.UpTime) or 0");
        }

        if (node.Type.Equals("ServerTimeValue", StringComparison.OrdinalIgnoreCase))
        {
            return WorldValueExpression("ServerTime", "Number", "Server Time", "return tonumber(World.ServerTime) or 0");
        }

        if (node.Type.Equals("WorldObjectCountValue", StringComparison.OrdinalIgnoreCase))
        {
            return WorldValueExpression("InstanceCount", "Number", "World Object Count", "return tonumber(World.InstanceCount) or 0");
        }

        if (node.Type.Equals("DecalImageValue", StringComparison.OrdinalIgnoreCase))
        {
            return ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Image", "String", "Decal Image", "return tostring(targetObject.Image or \"\")");
        }

        return null;
    }

    private static LuauExpression InputVectorAxisExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string upperAxis,
        string lowerAxis)
    {
        var actionName = PropertyParameterExpression(rule, node, nodesById, "actionName", "String", "Move", visitedNodeIds);
        var code = $"(function() if Input == nil or Input.GetVector2 == nil then return 0 end local vector = Input:GetVector2(tostring({actionName.Code})); if vector == nil then return 0 end if vector.Value ~= nil then vector = vector.Value end if vector.{upperAxis} ~= nil then return vector.{upperAxis} end if vector.{lowerAxis} ~= nil then return vector.{lowerAxis} end return 0 end)()";
        return new LuauExpression(code, "Number");
    }

    private static LuauExpression WorldValueExpression(
        string propertyName,
        string dataType,
        string readableName,
        string returnStatement)
    {
        var fallback = NormalizeExpressionDataType(dataType) switch
        {
            "String" => "\"\"",
            "Number" => "0",
            "Boolean" => "false",
            _ => "nil"
        };
        var code = $"(function() if World == nil then print(\"{readableName} stopped: World is not available.\"); return {fallback} end if World.{propertyName} == nil then print(\"{readableName} stopped: World does not expose {propertyName}.\"); return {fallback} end {returnStatement} end)()";
        return new LuauExpression(code, dataType);
    }

    private static void AppendTweenVectorAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string tweenMethodName,
        string readableName,
        bool scaleDefaults)
    {
        var indent = IndentText(indentLevel);
        var defaultValue = scaleDefaults ? "1" : "0";
        var endValue = ParameterVectorExpression(rule, action, nodesById, "vector", defaultValue, defaultValue, defaultValue);
        var duration = ParameterExpression(rule, action, nodesById, "duration", "Number", "1");
        var waitToComplete = ParameterExpression(rule, action, nodesById, "waitToComplete", "Boolean", "true");
        builder.AppendLine($"{indent}local targetObject = {TargetExpression(plan, action)}");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if Tween == nil or Tween.NewTween == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: Tween service is not available.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local endValue = {endValue.Code}");
        builder.AppendLine($"{indent}local function runTween()");
        builder.AppendLine($"{indent}    local tween = Tween:NewTween()");
        AppendTweenOptionSetters(builder, action, indentLevel + 1);
        builder.AppendLine($"{indent}    if tween.{tweenMethodName} == nil then");
        builder.AppendLine($"{indent}        print(\"{readableName} stopped: tween does not support {tweenMethodName}.\")");
        builder.AppendLine($"{indent}        return");
        builder.AppendLine($"{indent}    end");
        builder.AppendLine($"{indent}    tween:{tweenMethodName}(targetObject, endValue, {duration.Code})");
        builder.AppendLine($"{indent}    if tween.Finished ~= nil and tween.Finished.Wait ~= nil then");
        builder.AppendLine($"{indent}        tween.Finished:Wait()");
        builder.AppendLine($"{indent}    end");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if {waitToComplete.Code} then");
        builder.AppendLine($"{indent}    runTween()");
        builder.AppendLine($"{indent}elseif spawn ~= nil then");
        builder.AppendLine($"{indent}    spawn(runTween)");
        builder.AppendLine($"{indent}else");
        builder.AppendLine($"{indent}    runTween()");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendTweenColorAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var duration = ParameterExpression(rule, action, nodesById, "duration", "Number", "1");
        var waitToComplete = ParameterExpression(rule, action, nodesById, "waitToComplete", "Boolean", "true");
        var endColor = ColorExpression(rule, action, nodesById);
        builder.AppendLine($"{indent}local targetObject = {TargetExpression(plan, action)}");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"Tween Object Color stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.Color == nil then");
        builder.AppendLine($"{indent}    print(\"Tween Object Color stopped: target does not expose Color.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if Tween == nil or Tween.NewTween == nil then");
        builder.AppendLine($"{indent}    print(\"Tween Object Color stopped: Tween service is not available.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local endColor = {endColor.Code}");
        builder.AppendLine($"{indent}local function runTween()");
        builder.AppendLine($"{indent}    local tween = Tween:NewTween()");
        AppendTweenOptionSetters(builder, action, indentLevel + 1);
        builder.AppendLine($"{indent}    if tween.TweenColor == nil then");
        builder.AppendLine($"{indent}        print(\"Tween Object Color stopped: tween does not support TweenColor.\")");
        builder.AppendLine($"{indent}        return");
        builder.AppendLine($"{indent}    end");
        builder.AppendLine($"{indent}    tween:TweenColor(targetObject.Color, endColor, {duration.Code}, function(color)");
        builder.AppendLine($"{indent}        targetObject.Color = color");
        builder.AppendLine($"{indent}    end)");
        builder.AppendLine($"{indent}    if tween.Finished ~= nil and tween.Finished.Wait ~= nil then");
        builder.AppendLine($"{indent}        tween.Finished:Wait()");
        builder.AppendLine($"{indent}    end");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if {waitToComplete.Code} then");
        builder.AppendLine($"{indent}    runTween()");
        builder.AppendLine($"{indent}elseif spawn ~= nil then");
        builder.AppendLine($"{indent}    spawn(runTween)");
        builder.AppendLine($"{indent}else");
        builder.AppendLine($"{indent}    runTween()");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendTweenNumberPropertyAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string parameterKey,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        var duration = ParameterExpression(rule, action, nodesById, "duration", "Number", "1");
        var waitToComplete = ParameterExpression(rule, action, nodesById, "waitToComplete", "Boolean", "true");
        var endValue = ParameterExpression(rule, action, nodesById, parameterKey, "Number", "0");
        builder.AppendLine($"{indent}local targetObject = {TargetExpression(plan, action)}");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if Tween == nil or Tween.NewTween == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: Tween service is not available.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local endValue = tonumber({endValue.Code}) or 0");
        builder.AppendLine($"{indent}local function runTween()");
        builder.AppendLine($"{indent}    local tween = Tween:NewTween()");
        AppendTweenOptionSetters(builder, action, indentLevel + 1);
        builder.AppendLine($"{indent}    if tween.TweenNumber == nil then");
        builder.AppendLine($"{indent}        print(\"{readableName} stopped: tween does not support TweenNumber.\")");
        builder.AppendLine($"{indent}        return");
        builder.AppendLine($"{indent}    end");
        builder.AppendLine($"{indent}    tween:TweenNumber(tonumber(targetObject.{propertyName}) or 0, endValue, {duration.Code}, function(value)");
        builder.AppendLine($"{indent}        targetObject.{propertyName} = value");
        builder.AppendLine($"{indent}    end)");
        builder.AppendLine($"{indent}    if tween.Finished ~= nil and tween.Finished.Wait ~= nil then");
        builder.AppendLine($"{indent}        tween.Finished:Wait()");
        builder.AppendLine($"{indent}    end");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if {waitToComplete.Code} then");
        builder.AppendLine($"{indent}    runTween()");
        builder.AppendLine($"{indent}elseif spawn ~= nil then");
        builder.AppendLine($"{indent}    spawn(runTween)");
        builder.AppendLine($"{indent}else");
        builder.AppendLine($"{indent}    runTween()");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendTweenOptionSetters(StringBuilder builder, RuleNode action, int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var parameterValues = action.Parameters.ToDictionary(parameter => parameter.Key, EffectiveParameterValue, StringComparer.OrdinalIgnoreCase);
        var transition = SanitizedChoice(ParameterValue(action, parameterValues, "transition"), "Sine", TweenTransitions);
        var direction = SanitizedChoice(ParameterValue(action, parameterValues, "direction"), "InOut", TweenDirections);
        var speedScale = NumericLiteral(ParameterValue(action, parameterValues, "speedScale"), "1");
        var looped = BooleanValue(ParameterValue(action, parameterValues, "looped"), fallback: false) ? "true" : "false";
        var parallel = BooleanValue(ParameterValue(action, parameterValues, "parallel"), fallback: false) ? "true" : "false";
        builder.AppendLine($"{indent}if tween.SetTrans ~= nil and Enums ~= nil and Enums.TweenTransition ~= nil and Enums.TweenTransition.{transition} ~= nil then");
        builder.AppendLine($"{indent}    tween:SetTrans(Enums.TweenTransition.{transition})");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if tween.SetDirection ~= nil and Enums ~= nil and Enums.TweenDirection ~= nil and Enums.TweenDirection.{direction} ~= nil then");
        builder.AppendLine($"{indent}    tween:SetDirection(Enums.TweenDirection.{direction})");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if tween.SpeedScale ~= nil then");
        builder.AppendLine($"{indent}    tween.SpeedScale = math.max(0.001, {speedScale})");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if tween.Looped ~= nil then");
        builder.AppendLine($"{indent}    tween.Looped = {looped}");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if tween.Parallel ~= nil then");
        builder.AppendLine($"{indent}    tween.Parallel = {parallel}");
        builder.AppendLine($"{indent}end");
    }

    private static string SanitizedPlayerDefaultProperty(RuleNode node, IReadOnlyDictionary<string, string> parameterValues, string fallback)
    {
        var property = ParameterValue(node, parameterValues, "propertyName");
        return SanitizedChoice(property, fallback, PlayerDefaultProperties);
    }

    private static string SanitizedInputActionKind(RuleNode node, IReadOnlyDictionary<string, string> parameterValues, string fallback)
    {
        var kind = ParameterValue(node, parameterValues, "actionKind");
        return SanitizedChoice(kind, fallback, InputActionKinds);
    }

    private static string InputActionGetter(string actionKind)
    {
        return actionKind switch
        {
            "Axis" => "GetAxis",
            "Vector2" => "GetVector2",
            _ => "GetButton"
        };
    }

    private static string KeyCodeLiteral(RuleNode node, IReadOnlyDictionary<string, string> parameterValues)
    {
        return $"KeyCode.{SanitizedKeyCodeName(ParameterValue(node, parameterValues, "keyCode"))}";
    }

    private static string SanitizedKeyCodeName(string value)
    {
        var keyCode = (value ?? string.Empty).Trim();
        if (keyCode.StartsWith("KeyCode.", StringComparison.OrdinalIgnoreCase))
        {
            keyCode = keyCode["KeyCode.".Length..].Trim();
        }

        if (keyCode.Length == 0 || !char.IsLetter(keyCode[0]))
        {
            return "E";
        }

        foreach (var character in keyCode)
        {
            if (!char.IsLetterOrDigit(character) && character != '_')
            {
                return "E";
            }
        }

        return keyCode;
    }

    private static string SanitizedChoice(string value, string fallback, HashSet<string> allowedValues)
    {
        var match = allowedValues.FirstOrDefault(item => item.Equals(value, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(match) ? fallback : match;
    }

    private static string PlayerDefaultFallback(string property)
    {
        return property switch
        {
            "JumpPower" => "8",
            "SprintSpeed" => "8",
            "MaxHealth" => "100",
            "RespawnTime" => "5",
            "Stamina" => "100",
            _ => "4"
        };
    }
}
