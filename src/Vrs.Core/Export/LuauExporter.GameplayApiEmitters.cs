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

    private static bool TryAppendReadableGameplayApiConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (TryAppendReadableTeamApiConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
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

        if (TryAppendReadableImage3DConditionBody(builder, condition, plan, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableText3DConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return true;
        }

        if (TryAppendReadableCustomUiConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
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

        if (TryAppendReadableToolApiActionBody(builder, rule, action, plan, nodesById, indentLevel))
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

        return false;
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

        if (TryResolveReadableToolApiPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } toolExpression)
        {
            return toolExpression;
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

    private static void AppendTweenOptionSetters(StringBuilder builder, RuleNode action, int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var parameterValues = action.Parameters.ToDictionary(parameter => parameter.Key, EffectiveParameterValue, StringComparer.OrdinalIgnoreCase);
        var transition = SanitizedChoice(ParameterValue(action, parameterValues, "transition"), "Sine", TweenTransitions);
        var direction = SanitizedChoice(ParameterValue(action, parameterValues, "direction"), "InOut", TweenDirections);
        builder.AppendLine($"{indent}if tween.SetTrans ~= nil and Enums ~= nil and Enums.TweenTransition ~= nil and Enums.TweenTransition.{transition} ~= nil then");
        builder.AppendLine($"{indent}    tween:SetTrans(Enums.TweenTransition.{transition})");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if tween.SetDirection ~= nil and Enums ~= nil and Enums.TweenDirection ~= nil and Enums.TweenDirection.{direction} ~= nil then");
        builder.AppendLine($"{indent}    tween:SetDirection(Enums.TweenDirection.{direction})");
        builder.AppendLine($"{indent}end");
    }

    private static string SanitizedPlayerDefaultProperty(RuleNode node, IReadOnlyDictionary<string, string> parameterValues, string fallback)
    {
        var property = ParameterValue(node, parameterValues, "propertyName");
        return SanitizedChoice(property, fallback, PlayerDefaultProperties);
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
