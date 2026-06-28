using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static readonly HashSet<string> CharacterAnimationWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnCharacterAnimationChanged",
        "OnCharacterAnimationBecame",
        "OnCharacterStateChanged",
        "OnCharacterStateBecame",
        "OnCharacterAnimationSpeedReached",
        "OnCharacterAttachmentAvailable"
    };

    private static bool IsCharacterAnimationWatcherTrigger(string triggerType)
        => CharacterAnimationWatcherTriggerTypes.Contains(triggerType);

    private static bool TryAppendReadableCharacterAnimationActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (action.Type.Equals("PlayCharacterAnimation", StringComparison.OrdinalIgnoreCase))
        {
            var animation = ParameterExpression(rule, action, nodesById, "animation", "String", "Idle");
            AppendCharacterAnimatorMethodAction(builder, plan, action, indentLevel, "PlayAnimation", $"tostring({animation.Code})", "Play Character Animation");
            return true;
        }

        if (action.Type.Equals("PlayCharacterOneShotAnimation", StringComparison.OrdinalIgnoreCase))
        {
            var animation = ParameterExpression(rule, action, nodesById, "animation", "String", "Wave");
            AppendCharacterAnimatorMethodAction(builder, plan, action, indentLevel, "PlayOneShotAnimation", $"tostring({animation.Code})", "Play Character One Shot");
            return true;
        }

        if (action.Type.Equals("StopCharacterAnimation", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterAnimatorMethodAction(builder, plan, action, indentLevel, "StopAnimation", "", "Stop Character Animation");
            return true;
        }

        if (action.Type.Equals("StopCharacterOneShotAnimation", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterAnimatorMethodAction(builder, plan, action, indentLevel, "StopOneShotAnimation", "", "Stop Character One Shot");
            return true;
        }

        if (action.Type.Equals("SetCharacterState", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterStateAction(builder, rule, action, plan, nodesById, indentLevel);
            return true;
        }

        if (action.Type.Equals("SetCharacterAnimationSpeed", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "CurrentSpeed", "speed", "Number", "1", "Set Character Animation Speed");
            return true;
        }

        return false;
    }

    private static bool TryAppendReadableCharacterAnimationConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (condition.Type.Equals("CurrentCharacterAnimationIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterAnimatorStringCondition(builder, rule, condition, plan, nodesById, indentLevel, "CurrentAnimation", "animation", "Idle");
            return true;
        }

        if (condition.Type.Equals("CharacterHasAnimator", StringComparison.OrdinalIgnoreCase))
        {
            var indent = IndentText(indentLevel);
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "characterObject");
            builder.AppendLine($"{indent}return characterObject ~= nil and characterObject.Animator ~= nil");
            return true;
        }

        if (condition.Type.Equals("CharacterStateIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterStringPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "CurrentState", "state", "Idle");
            return true;
        }

        if (condition.Type.Equals("CharacterAnimationSpeedAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "CurrentSpeed", "speed", "1", ">=");
            return true;
        }

        if (condition.Type.Equals("CharacterAnimationSpeedAtMost", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "CurrentSpeed", "speed", "1", "<=");
            return true;
        }

        if (condition.Type.Equals("CharacterHasAttachment", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterAttachmentCondition(builder, rule, condition, plan, nodesById, indentLevel);
            return true;
        }

        return false;
    }

    private static void AppendReadableCharacterAnimationWatcherTransitionTrigger(
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

        if (trigger.Type.Equals("OnCharacterAnimationChanged", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterAnimatorWatchedValue(builder, "CurrentAnimation", "currentAnimation", "Current Character Animation", readsNumber: false);
            AppendReadableAnyChangeLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, ", characterAnimation = currentValue", 1);
            builder.AppendLine("end");
            return;
        }

        if (trigger.Type.Equals("OnCharacterAnimationBecame", StringComparison.OrdinalIgnoreCase))
        {
            var animation = ParameterExpression(rule, trigger, nodesById, "animation", "String", "Idle");
            builder.AppendLine($"    local expectedAnimation = tostring({animation.Code} or \"Idle\")");
            AppendCharacterAnimatorWatchedValue(builder, "CurrentAnimation", "currentAnimation", "Current Character Animation", readsNumber: false);
            builder.AppendLine("    local function readMatched()");
            builder.AppendLine("        local currentValue = readWatchedValue()");
            builder.AppendLine("        if currentValue == nil then");
            builder.AppendLine("            return false, nil");
            builder.AppendLine("        end");
            builder.AppendLine("        return currentValue == expectedAnimation, currentValue");
            builder.AppendLine("    end");
            AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, ", characterAnimation = currentValue", 1);
            builder.AppendLine("end");
            return;
        }

        if (trigger.Type.Equals("OnCharacterStateChanged", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterPropertyWatchedValue(builder, "CurrentState", "characterState", readsNumber: false);
            AppendReadableAnyChangeLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, ", characterState = currentValue", 1);
            builder.AppendLine("end");
            return;
        }

        if (trigger.Type.Equals("OnCharacterStateBecame", StringComparison.OrdinalIgnoreCase))
        {
            var state = ParameterExpression(rule, trigger, nodesById, "state", "String", "Idle");
            builder.AppendLine($"    local expectedState = tostring({state.Code} or \"Idle\")");
            AppendCharacterPropertyWatchedValue(builder, "CurrentState", "characterState", readsNumber: false);
            builder.AppendLine("    local function readMatched()");
            builder.AppendLine("        local currentValue = readWatchedValue()");
            builder.AppendLine("        if currentValue == nil then");
            builder.AppendLine("            return false, nil");
            builder.AppendLine("        end");
            builder.AppendLine("        return currentValue == expectedState, currentValue");
            builder.AppendLine("    end");
            AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, ", characterState = currentValue", 1);
            builder.AppendLine("end");
            return;
        }

        if (trigger.Type.Equals("OnCharacterAnimationSpeedReached", StringComparison.OrdinalIgnoreCase))
        {
            var speed = ParameterExpression(rule, trigger, nodesById, "speed", "Number", "1");
            builder.AppendLine($"    local watchedSpeed = tonumber({speed.Code}) or 1");
            AppendCharacterPropertyWatchedValue(builder, "CurrentSpeed", "characterSpeed", readsNumber: true);
            builder.AppendLine("    local function readMatched()");
            builder.AppendLine("        local currentValue = readWatchedValue()");
            builder.AppendLine("        if currentValue == nil then");
            builder.AppendLine("            return false, nil");
            builder.AppendLine("        end");
            builder.AppendLine("        return currentValue >= watchedSpeed, currentValue");
            builder.AppendLine("    end");
            AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, ", characterAnimationSpeed = currentValue", 1);
            builder.AppendLine("end");
            return;
        }

        var attachment = ParameterExpression(rule, trigger, nodesById, "attachment", "String", "Head");
        builder.AppendLine($"    local watchedAttachmentName = tostring({attachment.Code} or \"Head\")");
        AppendCharacterAttachmentWatchedValue(builder);
        builder.AppendLine("    local function readMatched()");
        builder.AppendLine("        local currentValue = readWatchedValue()");
        builder.AppendLine("        return currentValue ~= nil, currentValue");
        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, ", characterAttachment = currentValue", 1);
        builder.AppendLine("end");
    }

    private static LuauExpression? TryResolveReadableCharacterAnimationPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "CurrentCharacterAnimation" => CharacterAnimatorValueExpression(rule, node, nodesById, visitedNodeIds, "CurrentAnimation", "String", "Current Character Animation", "return tostring(animatorObject.CurrentAnimation or \"\")"),
            "CharacterAnimator" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Animator", "SceneObject", "Character Animation Controller", "return targetObject.Animator"),
            "CharacterStateValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "CurrentState", "String", "Character State", "return tostring(targetObject.CurrentState or \"\")"),
            "CharacterAnimationSpeedValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "CurrentSpeed", "Number", "Character Animation Speed", "return tonumber(targetObject.CurrentSpeed) or 0"),
            "CharacterAttachment" => CharacterAttachmentExpression(rule, node, nodesById, visitedNodeIds),
            _ => null
        };
    }

    private static void AppendCharacterAnimatorMethodAction(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode action,
        int indentLevel,
        string methodName,
        string argumentCode,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "characterObject");
        builder.AppendLine($"{indent}if characterObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: character was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local animatorObject = characterObject.Animator");
        builder.AppendLine($"{indent}if animatorObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: character has no animation controller.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if animatorObject.{methodName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: animation controller does not support {methodName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine(string.IsNullOrWhiteSpace(argumentCode)
            ? $"{indent}animatorObject:{methodName}()"
            : $"{indent}animatorObject:{methodName}({argumentCode})");
    }

    private static void AppendCharacterStateAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var state = ParameterExpression(rule, action, nodesById, "state", "String", "Idle");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "characterObject");
        builder.AppendLine($"{indent}if characterObject == nil then");
        builder.AppendLine($"{indent}    print(\"Set Character State stopped: character was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if characterObject.CurrentState == nil then");
        builder.AppendLine($"{indent}    print(\"Set Character State stopped: character does not expose CurrentState.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local characterStateName = tostring({state.Code} or \"Idle\")");
        builder.AppendLine($"{indent}if Enums ~= nil and Enums.CharacterModelState ~= nil and Enums.CharacterModelState[characterStateName] ~= nil then");
        builder.AppendLine($"{indent}    characterObject.CurrentState = Enums.CharacterModelState[characterStateName]");
        builder.AppendLine($"{indent}else");
        builder.AppendLine($"{indent}    characterObject.CurrentState = characterStateName");
        builder.AppendLine($"{indent}end");
    }

    private static LuauExpression CharacterAnimatorValueExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string propertyName,
        string dataType,
        string readableName,
        string returnStatement)
    {
        var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Self", visitedNodeIds);
        var code = $"(function() local characterObject = resolveTarget(triggerObject, {target.Code}); if characterObject == nil then print(\"{readableName} stopped: character was not found.\"); return nil end; local animatorObject = characterObject.Animator; if animatorObject == nil then print(\"{readableName} stopped: character has no animation controller.\"); return nil end; if animatorObject.{propertyName} == nil then print(\"{readableName} stopped: animation controller does not expose {propertyName}.\"); return nil end; {returnStatement} end)()";
        return new LuauExpression(code, dataType);
    }

    private static LuauExpression CharacterAttachmentExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Self", visitedNodeIds);
        var attachment = PropertyParameterExpression(rule, node, nodesById, "attachment", "String", "Head", visitedNodeIds);
        var code = $"(function() local characterObject = resolveTarget(triggerObject, {target.Code}); if characterObject == nil then print(\"Character Attachment stopped: character was not found.\"); return nil end; if characterObject.GetAttachment == nil then print(\"Character Attachment stopped: character does not support GetAttachment.\"); return nil end; local attachmentName = tostring({attachment.Code} or \"Head\"); local attachmentValue = attachmentName; if Enums ~= nil and Enums.CharacterAttachment ~= nil and Enums.CharacterAttachment[attachmentName] ~= nil then attachmentValue = Enums.CharacterAttachment[attachmentName] end; return characterObject:GetAttachment(attachmentValue) end)()";
        return new LuauExpression(code, "SceneObject");
    }

    private static void AppendCharacterAnimatorStringCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string parameterKey,
        string fallback)
    {
        var indent = IndentText(indentLevel);
        var expected = ParameterExpression(rule, condition, nodesById, parameterKey, "String", fallback);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "characterObject");
        builder.AppendLine($"{indent}if characterObject == nil or characterObject.Animator == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local currentText = tostring(characterObject.Animator.{propertyName} or \"\")");
        builder.AppendLine($"{indent}local expectedText = tostring({expected.Code} or \"{fallback}\")");
        builder.AppendLine($"{indent}return currentText == expectedText");
    }

    private static void AppendCharacterStringPropertyCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string parameterKey,
        string fallback)
    {
        var indent = IndentText(indentLevel);
        var expected = ParameterExpression(rule, condition, nodesById, parameterKey, "String", fallback);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "characterObject");
        builder.AppendLine($"{indent}if characterObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local currentText = tostring(characterObject.{propertyName} or \"\")");
        builder.AppendLine($"{indent}local expectedText = tostring({expected.Code} or \"{fallback}\")");
        builder.AppendLine($"{indent}return currentText == expectedText");
    }

    private static void AppendCharacterNumberPropertyCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string parameterKey,
        string fallback,
        string comparison)
    {
        var indent = IndentText(indentLevel);
        var threshold = ParameterExpression(rule, condition, nodesById, parameterKey, "Number", fallback);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "characterObject");
        builder.AppendLine($"{indent}if characterObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local currentNumber = tonumber(characterObject.{propertyName}) or 0");
        builder.AppendLine($"{indent}local expectedNumber = tonumber({threshold.Code}) or {fallback}");
        builder.AppendLine($"{indent}return currentNumber {comparison} expectedNumber");
    }

    private static void AppendCharacterAttachmentCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var attachment = ParameterExpression(rule, condition, nodesById, "attachment", "String", "Head");
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "characterObject");
        builder.AppendLine($"{indent}if characterObject == nil or characterObject.GetAttachment == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local attachmentName = tostring({attachment.Code} or \"Head\")");
        builder.AppendLine($"{indent}local attachmentValue = attachmentName");
        builder.AppendLine($"{indent}if Enums ~= nil and Enums.CharacterAttachment ~= nil and Enums.CharacterAttachment[attachmentName] ~= nil then");
        builder.AppendLine($"{indent}    attachmentValue = Enums.CharacterAttachment[attachmentName]");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return characterObject:GetAttachment(attachmentValue) ~= nil");
    }

    private static void AppendCharacterAnimatorWatchedValue(StringBuilder builder, string propertyName, string contextName, string readableName, bool readsNumber)
    {
        builder.AppendLine("    local function readWatchedValue()");
        builder.AppendLine("        if triggerObject == nil or triggerObject.Animator == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        builder.AppendLine($"        if triggerObject.Animator.{propertyName} == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        builder.AppendLine(readsNumber
            ? $"        return tonumber(triggerObject.Animator.{propertyName}) or 0"
            : $"        return tostring(triggerObject.Animator.{propertyName} or \"\")");
        builder.AppendLine("    end");
    }

    private static void AppendCharacterPropertyWatchedValue(StringBuilder builder, string propertyName, string contextName, bool readsNumber)
    {
        builder.AppendLine("    local function readWatchedValue()");
        builder.AppendLine("        if triggerObject == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        builder.AppendLine($"        if triggerObject.{propertyName} == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        builder.AppendLine(readsNumber
            ? $"        return tonumber(triggerObject.{propertyName}) or 0"
            : $"        return tostring(triggerObject.{propertyName} or \"\")");
        builder.AppendLine("    end");
    }

    private static void AppendCharacterAttachmentWatchedValue(StringBuilder builder)
    {
        builder.AppendLine("    local function readWatchedValue()");
        builder.AppendLine("        if triggerObject == nil or triggerObject.GetAttachment == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        builder.AppendLine("        local attachmentValue = watchedAttachmentName");
        builder.AppendLine("        if Enums ~= nil and Enums.CharacterAttachment ~= nil and Enums.CharacterAttachment[watchedAttachmentName] ~= nil then");
        builder.AppendLine("            attachmentValue = Enums.CharacterAttachment[watchedAttachmentName]");
        builder.AppendLine("        end");
        builder.AppendLine("        return triggerObject:GetAttachment(attachmentValue)");
        builder.AppendLine("    end");
    }
}
