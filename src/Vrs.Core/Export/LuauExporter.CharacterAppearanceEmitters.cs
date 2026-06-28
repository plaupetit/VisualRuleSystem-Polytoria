using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static bool TryAppendReadableCharacterAppearanceActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (action.Type.Equals("SetAccessoryAttachment", StringComparison.OrdinalIgnoreCase))
        {
            AppendSetAccessoryAttachmentAction(builder, rule, action, plan, nodesById, indentLevel);
            return true;
        }

        if (action.Type.Equals("SetClothingImage", StringComparison.OrdinalIgnoreCase))
        {
            AppendPcallSetObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Image", "image", "String", "", "Set Clothing Image");
            return true;
        }

        if (action.Type.Equals("SetCharacterFaceImage", StringComparison.OrdinalIgnoreCase))
        {
            AppendPcallSetObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "FaceImage", "image", "String", "", "Set Character Face Image");
            return true;
        }

        if (action.Type.Equals("SetCharacterBodyMesh", StringComparison.OrdinalIgnoreCase))
        {
            AppendPcallSetObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "BodyMesh", "mesh", "String", "", "Set Character Body Mesh");
            return true;
        }

        if (action.Type.Equals("SetCharacterBodyColor", StringComparison.OrdinalIgnoreCase))
        {
            AppendSetCharacterBodyColorAction(builder, rule, action, plan, nodesById, indentLevel);
            return true;
        }

        if (action.Type.Equals("LoadCharacterAppearance", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterAppearanceMethodAction(builder, rule, action, plan, nodesById, indentLevel, "LoadAppearance", "Load Character Appearance");
            return true;
        }

        if (action.Type.Equals("ClearCharacterAppearance", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterAppearanceMethodAction(builder, rule, action, plan, nodesById, indentLevel, "ClearAppearance", "Clear Character Appearance");
            return true;
        }

        if (action.Type.Equals("StartCharacterRagdoll", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterAppearanceMethodAction(builder, rule, action, plan, nodesById, indentLevel, "StartRagdoll", "Start Character Ragdoll");
            return true;
        }

        if (action.Type.Equals("StopCharacterRagdoll", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterAppearanceMethodAction(builder, rule, action, plan, nodesById, indentLevel, "StopRagdoll", "Stop Character Ragdoll");
            return true;
        }

        return false;
    }

    private static bool TryAppendReadableCharacterAppearanceConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (condition.Type.Equals("AccessoryAttachmentIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterStringPropertyEqualsCondition(builder, rule, condition, plan, nodesById, indentLevel, "TargetAttachment", "attachment", "Head");
            return true;
        }

        if (condition.Type.Equals("ClothingImageIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterStringPropertyEqualsCondition(builder, rule, condition, plan, nodesById, indentLevel, "Image", "image", "");
            return true;
        }

        if (condition.Type.Equals("CharacterFaceImageIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterStringPropertyEqualsCondition(builder, rule, condition, plan, nodesById, indentLevel, "FaceImage", "image", "");
            return true;
        }

        if (condition.Type.Equals("CharacterBodyMeshIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterStringPropertyEqualsCondition(builder, rule, condition, plan, nodesById, indentLevel, "BodyMesh", "mesh", "");
            return true;
        }

        if (condition.Type.Equals("ClothingHasImage", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterStringPropertyNotEmptyCondition(builder, condition, plan, indentLevel, "Image");
            return true;
        }

        if (condition.Type.Equals("CharacterHasFaceImage", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterStringPropertyNotEmptyCondition(builder, condition, plan, indentLevel, "FaceImage");
            return true;
        }

        if (condition.Type.Equals("CharacterHasBodyMesh", StringComparison.OrdinalIgnoreCase))
        {
            AppendCharacterStringPropertyNotEmptyCondition(builder, condition, plan, indentLevel, "BodyMesh");
            return true;
        }

        if (condition.Type.Equals("CharacterIsRagdolling", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("CharacterIsNotRagdolling", StringComparison.OrdinalIgnoreCase))
        {
            var expected = condition.Type.Equals("CharacterIsRagdolling", StringComparison.OrdinalIgnoreCase);
            AppendCharacterBooleanPropertyCondition(builder, condition, plan, indentLevel, "Ragdolling", expected);
            return true;
        }

        return false;
    }

    private static LuauExpression? TryResolveReadableCharacterAppearancePropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "AccessoryAttachmentValue" => NullableObjectPropertyExpression(rule, node, nodesById, visitedNodeIds, "TargetAttachment", "String", "Accessory Attachment", "return tostring(targetObject.TargetAttachment or \"\")"),
            "ClothingImageValue" => NullableObjectPropertyExpression(rule, node, nodesById, visitedNodeIds, "Image", "String", "Clothing Image", "return tostring(targetObject.Image or \"\")"),
            "CharacterFaceImageValue" => NullableObjectPropertyExpression(rule, node, nodesById, visitedNodeIds, "FaceImage", "String", "Character Face Image", "return tostring(targetObject.FaceImage or \"\")"),
            "CharacterBodyMeshValue" => NullableObjectPropertyExpression(rule, node, nodesById, visitedNodeIds, "BodyMesh", "String", "Character Body Mesh", "return tostring(targetObject.BodyMesh or \"\")"),
            "CharacterBodyColorValue" => CharacterBodyColorExpression(rule, node, nodesById, visitedNodeIds),
            "CharacterRagdollingValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Ragdolling", "Boolean", "Character Is Ragdolling", "return targetObject.Ragdolling == true"),
            "CharacterRagdollPosition" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "RagdollPosition", "Vector3", "Character Ragdoll Position", "return targetObject.RagdollPosition"),
            "CharacterRagdollRotation" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "RagdollRotation", "Vector3", "Character Ragdoll Rotation", "return targetObject.RagdollRotation"),
            _ => null
        };
    }

    private static void AppendReadableCharacterRagdollEventTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var eventName = trigger.Type.Equals("OnCharacterRagdollStarted", StringComparison.OrdinalIgnoreCase)
            ? "RagdollStarted"
            : "RagdollStopped";
        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    if triggerObject.{eventName} == nil or triggerObject.{eventName}.Connect == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target character has no {eventName} event.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine($"    triggerObject.{eventName}:Connect(function()");
        builder.AppendLine("        local triggerContext = { object = triggerObject, character = triggerObject }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
        if (!emitted)
        {
            builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("    end)");
        builder.AppendLine("end");
    }

    private static void AppendCharacterStringPropertyEqualsCondition(
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
        var caseSensitive = ParameterExpression(rule, condition, nodesById, "caseSensitive", "Boolean", "false");
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil or targetObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local currentText = tostring(targetObject.{propertyName} or \"\")");
        builder.AppendLine($"{indent}local expectedText = tostring({expected.Code} or \"\")");
        builder.AppendLine($"{indent}if {caseSensitive.Code} ~= true then");
        builder.AppendLine($"{indent}    currentText = string.lower(currentText)");
        builder.AppendLine($"{indent}    expectedText = string.lower(expectedText)");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return currentText == expectedText");
    }

    private static void AppendCharacterStringPropertyNotEmptyCondition(
        StringBuilder builder,
        RuleNode condition,
        ReadableExportPlan plan,
        int indentLevel,
        string propertyName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil or targetObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return tostring(targetObject.{propertyName} or \"\") ~= \"\"");
    }

    private static void AppendCharacterBooleanPropertyCondition(
        StringBuilder builder,
        RuleNode condition,
        ReadableExportPlan plan,
        int indentLevel,
        string propertyName,
        bool expected)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil or targetObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return (targetObject.{propertyName} == true) == {expected.ToString().ToLowerInvariant()}");
    }

    private static bool IsCharacterRagdollEventTrigger(string nodeType)
    {
        return nodeType.Equals("OnCharacterRagdollStarted", StringComparison.OrdinalIgnoreCase) ||
            nodeType.Equals("OnCharacterRagdollStopped", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendSetAccessoryAttachmentAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var attachment = ParameterExpression(rule, action, nodesById, "attachment", "String", "Head");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"Set Accessory Attachment stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local attachmentName = tostring({attachment.Code} or \"Head\")");
        builder.AppendLine($"{indent}local attachmentValue = attachmentName");
        builder.AppendLine($"{indent}if Enums ~= nil and Enums.CharacterAttachment ~= nil and Enums.CharacterAttachment[attachmentName] ~= nil then");
        builder.AppendLine($"{indent}    attachmentValue = Enums.CharacterAttachment[attachmentName]");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local setOk, setError = pcall(function()");
        builder.AppendLine($"{indent}    targetObject.TargetAttachment = attachmentValue");
        builder.AppendLine($"{indent}end)");
        builder.AppendLine($"{indent}if not setOk then");
        builder.AppendLine($"{indent}    print(\"Set Accessory Attachment stopped: \" .. tostring(setError))");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendPcallSetObjectPropertyAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string parameterKey,
        string dataType,
        string fallback,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        var value = ParameterExpression(rule, action, nodesById, parameterKey, dataType, fallback);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local setOk, setError = pcall(function()");
        builder.AppendLine($"{indent}    targetObject.{propertyName} = tostring({value.Code})");
        builder.AppendLine($"{indent}end)");
        builder.AppendLine($"{indent}if not setOk then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: \" .. tostring(setError))");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendSetCharacterBodyColorAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var bodyPart = ParameterExpression(rule, action, nodesById, "bodyPart", "String", "Head");
        var color = ColorExpression(rule, action, nodesById);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "characterObject");
        builder.AppendLine($"{indent}if characterObject == nil then");
        builder.AppendLine($"{indent}    print(\"Set Character Body Color stopped: character was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local bodyPartName = tostring({bodyPart.Code} or \"Head\")");
        builder.AppendLine($"{indent}local bodyColor = {color.Code}");
        AppendBodyPartSetBranch(builder, indentLevel, "Head", "HeadColor", first: true);
        AppendBodyPartSetBranch(builder, indentLevel, "Torso", "TorsoColor", first: false);
        AppendBodyPartSetBranch(builder, indentLevel, "Left Arm", "LeftArmColor", first: false);
        AppendBodyPartSetBranch(builder, indentLevel, "Right Arm", "RightArmColor", first: false);
        AppendBodyPartSetBranch(builder, indentLevel, "Left Leg", "LeftLegColor", first: false);
        AppendBodyPartSetBranch(builder, indentLevel, "Right Leg", "RightLegColor", first: false);
        builder.AppendLine($"{indent}else");
        builder.AppendLine($"{indent}    print(\"Set Character Body Color stopped: unknown body part \" .. bodyPartName)");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendBodyPartSetBranch(StringBuilder builder, int indentLevel, string option, string propertyName, bool first)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine(first
            ? $"{indent}if bodyPartName == \"{option}\" then"
            : $"{indent}elseif bodyPartName == \"{option}\" then");
        builder.AppendLine($"{indent}    characterObject.{propertyName} = bodyColor");
    }

    private static void AppendCharacterAppearanceMethodAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string methodName,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "characterObject");
        builder.AppendLine($"{indent}if characterObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: character was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if characterObject.{methodName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: character does not support {methodName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}characterObject:{methodName}({CharacterAppearanceMethodArguments(rule, action, nodesById, methodName)})");
    }

    private static string CharacterAppearanceMethodArguments(
        Rule rule,
        RuleNode action,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        string methodName)
    {
        if (methodName.Equals("LoadAppearance", StringComparison.OrdinalIgnoreCase))
        {
            var userId = ParameterExpression(rule, action, nodesById, "userId", "Number", "0");
            var loadTools = ParameterExpression(rule, action, nodesById, "loadTools", "Boolean", "true");
            return $"math.floor(tonumber({userId.Code}) or 0), {loadTools.Code}";
        }

        if (methodName.Equals("StartRagdoll", StringComparison.OrdinalIgnoreCase))
        {
            var x = ParameterExpression(rule, action, nodesById, "x", "Number", "0");
            var y = ParameterExpression(rule, action, nodesById, "y", "Number", "0");
            var z = ParameterExpression(rule, action, nodesById, "z", "Number", "0");
            return $"makeVector3({x.Code}, {y.Code}, {z.Code})";
        }

        return "";
    }

    private static LuauExpression NullableObjectPropertyExpression(
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
        var code = $"(function() local targetObject = resolveTarget(triggerObject, {target.Code}); if targetObject == nil then print(\"{readableName} stopped: target was not found.\"); return nil end; {returnStatement} end)()";
        return new LuauExpression(code, dataType);
    }

    private static LuauExpression CharacterBodyColorExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Self", visitedNodeIds);
        var bodyPart = PropertyParameterExpression(rule, node, nodesById, "bodyPart", "String", "Head", visitedNodeIds);
        var code = $"(function() local characterObject = resolveTarget(triggerObject, {target.Code}); if characterObject == nil then print(\"Character Body Color stopped: character was not found.\"); return Color.New(1, 1, 1, 1) end; local bodyPartName = tostring({bodyPart.Code} or \"Head\"); if bodyPartName == \"Head\" then return characterObject.HeadColor or Color.New(1, 1, 1, 1) elseif bodyPartName == \"Torso\" then return characterObject.TorsoColor or Color.New(1, 1, 1, 1) elseif bodyPartName == \"Left Arm\" then return characterObject.LeftArmColor or Color.New(1, 1, 1, 1) elseif bodyPartName == \"Right Arm\" then return characterObject.RightArmColor or Color.New(1, 1, 1, 1) elseif bodyPartName == \"Left Leg\" then return characterObject.LeftLegColor or Color.New(1, 1, 1, 1) elseif bodyPartName == \"Right Leg\" then return characterObject.RightLegColor or Color.New(1, 1, 1, 1) end; return Color.New(1, 1, 1, 1) end)()";
        return new LuauExpression(code, "Color");
    }
}
