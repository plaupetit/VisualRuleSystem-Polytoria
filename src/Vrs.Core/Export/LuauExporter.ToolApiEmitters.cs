using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static bool TryAppendReadableToolApiActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (action.Type.Equals("ActivateTool", StringComparison.OrdinalIgnoreCase))
        {
            AppendToolMethodAction(builder, plan, action, indentLevel, "Activate", null, "Activate Tool");
            return true;
        }

        if (action.Type.Equals("DeactivateTool", StringComparison.OrdinalIgnoreCase))
        {
            AppendToolMethodAction(builder, plan, action, indentLevel, "Deactivate", null, "Deactivate Tool");
            return true;
        }

        if (action.Type.Equals("PlayToolAnimation", StringComparison.OrdinalIgnoreCase))
        {
            var animationName = ParameterExpression(rule, action, nodesById, "animationName", "String", "Swing");
            AppendToolMethodAction(builder, plan, action, indentLevel, "PlayAnimation", $"tostring({animationName.Code})", "Play Tool Animation");
            return true;
        }

        if (action.Type.Equals("SetToolDroppable", StringComparison.OrdinalIgnoreCase))
        {
            var indent = IndentText(indentLevel);
            var enabled = ParameterExpression(rule, action, nodesById, "enabled", "Boolean", "true");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "toolObject");
            AppendToolObjectGuard(builder, indentLevel, "Set Tool Droppable");
            builder.AppendLine($"{indent}if toolObject.Droppable == nil then");
            builder.AppendLine($"{indent}    print(\"Set Tool Droppable stopped: target tool does not expose Droppable.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}toolObject.Droppable = {enabled.Code}");
            return true;
        }

        return false;
    }

    private static bool TryAppendReadableToolApiConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (!condition.Type.Equals("ToolCanBeDropped", StringComparison.OrdinalIgnoreCase) &&
            !condition.Type.Equals("ToolIsHeld", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "toolObject");
        builder.AppendLine($"{indent}if toolObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        if (condition.Type.Equals("ToolCanBeDropped", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"{indent}return toolObject.Droppable == true");
            return true;
        }

        builder.AppendLine($"{indent}return toolObject.Holder ~= nil");
        return true;
    }

    private static LuauExpression? TryResolveReadableToolApiPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "ToolHolder" => ToolTargetExpression(rule, node, nodesById, visitedNodeIds, "SceneObject", "Tool Holder", "return toolObject.Holder"),
            "ToolCanBeDroppedValue" => ToolTargetExpression(rule, node, nodesById, visitedNodeIds, "Boolean", "Tool Can Be Dropped", "return toolObject.Droppable == true"),
            _ => null
        };
    }

    private static void AppendReadableToolEventTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var eventName = ToolEventName(trigger.Type);
        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    if triggerObject.{eventName} == nil or triggerObject.{eventName}.Connect == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target tool has no {eventName} event.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine($"    triggerObject.{eventName}:Connect(function()");
        builder.AppendLine("        local triggerContext = { object = triggerObject, tool = triggerObject, holder = triggerObject.Holder }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
        if (!emitted)
        {
            builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("    end)");
        builder.AppendLine("end");
    }

    private static bool IsToolEventTrigger(string nodeType)
    {
        return nodeType.Equals("OnToolEquipped", StringComparison.OrdinalIgnoreCase) ||
            nodeType.Equals("OnToolUnequipped", StringComparison.OrdinalIgnoreCase) ||
            nodeType.Equals("OnToolActivated", StringComparison.OrdinalIgnoreCase) ||
            nodeType.Equals("OnToolDeactivated", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToolEventName(string nodeType)
    {
        if (nodeType.Equals("OnToolEquipped", StringComparison.OrdinalIgnoreCase))
        {
            return "Equipped";
        }

        if (nodeType.Equals("OnToolUnequipped", StringComparison.OrdinalIgnoreCase))
        {
            return "Unequipped";
        }

        if (nodeType.Equals("OnToolActivated", StringComparison.OrdinalIgnoreCase))
        {
            return "Activated";
        }

        return "Deactivated";
    }

    private static void AppendToolMethodAction(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode action,
        int indentLevel,
        string methodName,
        string? argumentCode,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "toolObject");
        AppendToolObjectGuard(builder, indentLevel, readableName);
        builder.AppendLine($"{indent}if toolObject.{methodName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target tool does not support {methodName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        if (string.IsNullOrWhiteSpace(argumentCode))
        {
            builder.AppendLine($"{indent}toolObject:{methodName}()");
        }
        else
        {
            builder.AppendLine($"{indent}toolObject:{methodName}({argumentCode})");
        }
    }

    private static void AppendToolObjectGuard(StringBuilder builder, int indentLevel, string readableName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if toolObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target tool was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private static LuauExpression ToolTargetExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string dataType,
        string readableName,
        string returnStatement)
    {
        var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Target", visitedNodeIds);
        var fallback = NormalizeExpressionDataType(dataType) switch
        {
            "Boolean" => "false",
            _ => "nil"
        };
        var code = $"(function() local toolObject = resolveTarget(triggerObject, {target.Code}); if toolObject == nil then print(\"{readableName} stopped: target tool was not found.\"); return {fallback} end; {returnStatement} end)()";
        return new LuauExpression(code, dataType);
    }
}
