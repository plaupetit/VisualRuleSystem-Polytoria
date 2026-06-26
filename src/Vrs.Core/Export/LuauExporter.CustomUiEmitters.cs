using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Custom UI object nodes use the official UI object properties/events
    // directly. Keep them separate from CoreUI, which controls Polytoria's
    // built-in interface service rather than user-created UI objects.
    private static bool TryAppendReadableCustomUiActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (action.Type.Equals("SetUIText", StringComparison.OrdinalIgnoreCase))
        {
            var text = ParameterExpression(rule, action, nodesById, "text", "String", "");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "labelObject");
            AppendCustomUiPropertyGuard(builder, indentLevel, "labelObject", "Text", "Set UI Text");
            builder.AppendLine($"{indent}labelObject.Text = tostring({text.Code})");
            return true;
        }

        if (action.Type.Equals("SetUIColor", StringComparison.OrdinalIgnoreCase))
        {
            var red = ParameterExpression(rule, action, nodesById, "r", "Number", "1");
            var green = ParameterExpression(rule, action, nodesById, "g", "Number", "1");
            var blue = ParameterExpression(rule, action, nodesById, "b", "Number", "1");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "uiObject");
            AppendCustomUiPropertyGuard(builder, indentLevel, "uiObject", "Color", "Set UI Color");
            builder.AppendLine($"{indent}uiObject.Color = Color.New({red.Code}, {green.Code}, {blue.Code}, 1)");
            return true;
        }

        if (action.Type.Equals("SetUITextWrapped", StringComparison.OrdinalIgnoreCase))
        {
            var enabled = ParameterExpression(rule, action, nodesById, "enabled", "Boolean", "true");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "labelObject");
            AppendCustomUiPropertyGuard(builder, indentLevel, "labelObject", "TextWrapped", "Set UI Text Wrapping");
            builder.AppendLine($"{indent}labelObject.TextWrapped = {enabled.Code}");
            return true;
        }

        return false;
    }

    private static bool TryAppendReadableCustomUiConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (condition.Type.Equals("UITextIs", StringComparison.OrdinalIgnoreCase))
        {
            var expectedText = ParameterExpression(rule, condition, nodesById, "text", "String", "");
            var caseSensitive = ParameterExpression(rule, condition, nodesById, "caseSensitive", "Boolean", "false");
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil or targetObject.Text == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local currentText = tostring(targetObject.Text or \"\")");
            builder.AppendLine($"{indent}local expectedText = tostring({expectedText.Code})");
            builder.AppendLine($"{indent}if {caseSensitive.Code} then");
            builder.AppendLine($"{indent}    return currentText == expectedText");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return string.lower(currentText) == string.lower(expectedText)");
            return true;
        }

        if (condition.Type.Equals("UITextIsEmpty", StringComparison.OrdinalIgnoreCase))
        {
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return targetObject.Text == nil or tostring(targetObject.Text) == \"\"");
            return true;
        }

        if (condition.Type.Equals("UITextWrapped", StringComparison.OrdinalIgnoreCase))
        {
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return targetObject.TextWrapped == true");
            return true;
        }

        return false;
    }

    private static LuauExpression? TryResolveReadableCustomUiPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "UITextValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Text", "String", "UI Text", "return tostring(targetObject.Text or \"\")"),
            "UIColorValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Color", "Color", "UI Color", "return targetObject.Color"),
            "UIFontSizeValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "FontSize", "Number", "UI Font Size", "return tonumber(targetObject.FontSize) or 0"),
            "UITextWrappedValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "TextWrapped", "Boolean", "UI Text Wraps", "return targetObject.TextWrapped == true"),
            _ => null
        };
    }

    private static void AppendReadableUiButtonClickedTrigger(
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
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine("    if triggerObject.Clicked == nil or triggerObject.Clicked.Connect == nil then");
        builder.AppendLine("        print(\"On UI Button Clicked trigger stopped: target button has no Clicked event.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    triggerObject.Clicked:Connect(function()");
        builder.AppendLine("        local triggerContext = { object = triggerObject, uiButton = triggerObject }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
        if (!emitted)
        {
            builder.AppendLine("        print(\"On UI Button Clicked trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("    end)");
        builder.AppendLine("end");
    }

    private static void AppendCustomUiPropertyGuard(
        StringBuilder builder,
        int indentLevel,
        string variableName,
        string propertyName,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if {variableName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target UI object was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if {variableName}.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target UI object does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }
}
