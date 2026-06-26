using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Text3D nodes cover documented world-text properties only. Font assets
    // and alignment enums need dedicated pickers before they become safe nodes.
    private static bool TryAppendReadableText3DActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (action.Type.Equals("Set3DText", StringComparison.OrdinalIgnoreCase))
        {
            var text = ParameterExpression(rule, action, nodesById, "text", "String", "");
            AppendText3DTargetVariable(builder, plan, action, indentLevel, "Text", "Set 3D Text");
            builder.AppendLine($"{indent}textObject.Text = tostring({text.Code})");
            return true;
        }

        if (action.Type.Equals("Set3DTextFontSize", StringComparison.OrdinalIgnoreCase))
        {
            var size = ParameterExpression(rule, action, nodesById, "size", "Number", "24");
            AppendText3DTargetVariable(builder, plan, action, indentLevel, "FontSize", "Set 3D Text Size");
            builder.AppendLine($"{indent}textObject.FontSize = {size.Code}");
            return true;
        }

        if (action.Type.Equals("Set3DTextRichText", StringComparison.OrdinalIgnoreCase))
        {
            var enabled = ParameterExpression(rule, action, nodesById, "enabled", "Boolean", "true");
            AppendText3DTargetVariable(builder, plan, action, indentLevel, "UseRichText", "Set 3D Text Rich Text");
            builder.AppendLine($"{indent}textObject.UseRichText = {enabled.Code}");
            return true;
        }

        if (action.Type.Equals("Set3DTextColor", StringComparison.OrdinalIgnoreCase))
        {
            var red = ParameterExpression(rule, action, nodesById, "r", "Number", "1");
            var green = ParameterExpression(rule, action, nodesById, "g", "Number", "1");
            var blue = ParameterExpression(rule, action, nodesById, "b", "Number", "1");
            AppendText3DTargetVariable(builder, plan, action, indentLevel, "Color", "Set 3D Text Color");
            builder.AppendLine($"{indent}textObject.Color = Color.New({red.Code}, {green.Code}, {blue.Code}, 1)");
            return true;
        }

        if (action.Type.Equals("Set3DTextOutlineWidth", StringComparison.OrdinalIgnoreCase))
        {
            var width = ParameterExpression(rule, action, nodesById, "width", "Number", "1");
            AppendText3DTargetVariable(builder, plan, action, indentLevel, "OutlineWidth", "Set 3D Text Outline Width");
            builder.AppendLine($"{indent}textObject.OutlineWidth = {width.Code}");
            return true;
        }

        if (action.Type.Equals("Set3DTextOutlineColor", StringComparison.OrdinalIgnoreCase))
        {
            var red = ParameterExpression(rule, action, nodesById, "r", "Number", "0");
            var green = ParameterExpression(rule, action, nodesById, "g", "Number", "0");
            var blue = ParameterExpression(rule, action, nodesById, "b", "Number", "0");
            AppendText3DTargetVariable(builder, plan, action, indentLevel, "OutlineColor", "Set 3D Text Outline Color");
            builder.AppendLine($"{indent}textObject.OutlineColor = Color.New({red.Code}, {green.Code}, {blue.Code}, 1)");
            return true;
        }

        if (action.Type.Equals("Set3DTextFaceCamera", StringComparison.OrdinalIgnoreCase))
        {
            var enabled = ParameterExpression(rule, action, nodesById, "enabled", "Boolean", "true");
            AppendText3DTargetVariable(builder, plan, action, indentLevel, "FaceCamera", "Set 3D Text Face Camera");
            builder.AppendLine($"{indent}textObject.FaceCamera = {enabled.Code}");
            return true;
        }

        if (action.Type.Equals("Set3DTextLighting", StringComparison.OrdinalIgnoreCase))
        {
            var enabled = ParameterExpression(rule, action, nodesById, "enabled", "Boolean", "true");
            AppendText3DTargetVariable(builder, plan, action, indentLevel, "Shaded", "Set 3D Text Lighting");
            builder.AppendLine($"{indent}textObject.Shaded = {enabled.Code}");
            return true;
        }

        return false;
    }

    private static bool TryAppendReadableText3DConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (condition.Type.Equals("Text3DIs", StringComparison.OrdinalIgnoreCase))
        {
            var expected = ParameterExpression(rule, condition, nodesById, "text", "String", "");
            var caseSensitive = ParameterExpression(rule, condition, nodesById, "caseSensitive", "Boolean", "true");
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "textObject");
            builder.AppendLine($"{indent}if textObject == nil or textObject.Text == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local currentText = tostring(textObject.Text or \"\")");
            builder.AppendLine($"{indent}local expectedText = tostring({expected.Code})");
            builder.AppendLine($"{indent}if ({caseSensitive.Code}) == true then");
            builder.AppendLine($"{indent}    return currentText == expectedText");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return string.lower(currentText) == string.lower(expectedText)");
            return true;
        }

        if (condition.Type.Equals("Text3DIsEmpty", StringComparison.OrdinalIgnoreCase))
        {
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "textObject");
            builder.AppendLine($"{indent}if textObject == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return textObject.Text == nil or tostring(textObject.Text) == \"\"");
            return true;
        }

        if (condition.Type.Equals("Text3DFacesCamera", StringComparison.OrdinalIgnoreCase))
        {
            AppendText3DBooleanCondition(builder, condition, plan, indentLevel, "FaceCamera");
            return true;
        }

        if (condition.Type.Equals("Text3DUsesRichText", StringComparison.OrdinalIgnoreCase))
        {
            AppendText3DBooleanCondition(builder, condition, plan, indentLevel, "UseRichText");
            return true;
        }

        if (condition.Type.Equals("Text3DUsesLighting", StringComparison.OrdinalIgnoreCase))
        {
            AppendText3DBooleanCondition(builder, condition, plan, indentLevel, "Shaded");
            return true;
        }

        return false;
    }

    private static LuauExpression? TryResolveReadableText3DPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "Text3DValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Text", "String", "3D Text", "return tostring(targetObject.Text or \"\")"),
            "Text3DFontSizeValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "FontSize", "Number", "3D Text Size", "return tonumber(targetObject.FontSize) or 0"),
            "Text3DColorValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Color", "Color", "3D Text Color", "return targetObject.Color"),
            "Text3DOutlineWidthValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "OutlineWidth", "Number", "3D Text Outline Width", "return tonumber(targetObject.OutlineWidth) or 0"),
            "Text3DOutlineColorValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "OutlineColor", "Color", "3D Text Outline Color", "return targetObject.OutlineColor"),
            "Text3DFacesCameraValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "FaceCamera", "Boolean", "3D Text Faces Camera", "return targetObject.FaceCamera == true"),
            "Text3DUsesRichTextValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "UseRichText", "Boolean", "3D Text Uses Rich Text", "return targetObject.UseRichText == true"),
            "Text3DUsesLightingValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Shaded", "Boolean", "3D Text Uses Lighting", "return targetObject.Shaded == true"),
            _ => null
        };
    }

    private static void AppendText3DBooleanCondition(
        StringBuilder builder,
        RuleNode condition,
        ReadableExportPlan plan,
        int indentLevel,
        string propertyName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "textObject");
        builder.AppendLine($"{indent}if textObject == nil or textObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return textObject.{propertyName} == true");
    }

    private static void AppendText3DTargetVariable(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode node,
        int indentLevel,
        string propertyName,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, node, indentLevel, "textObject");
        builder.AppendLine($"{indent}if textObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target 3D text was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if textObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target 3D text does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }
}
