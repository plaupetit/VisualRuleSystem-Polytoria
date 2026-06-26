using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Body Position nodes use the documented physics object surface:
    // TargetPosition, Force, and AcceptanceDistance.
    private static bool TryAppendReadableBodyPositionActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (action.Type.Equals("SetBodyPositionTarget", StringComparison.OrdinalIgnoreCase))
        {
            var x = ParameterExpression(rule, action, nodesById, "x", "Number", "0");
            var y = ParameterExpression(rule, action, nodesById, "y", "Number", "0");
            var z = ParameterExpression(rule, action, nodesById, "z", "Number", "0");
            AppendBodyPositionPropertyGuard(builder, plan, action, indentLevel, "TargetPosition", "Set Body Position Target");
            builder.AppendLine($"{indent}bodyPositionObject.TargetPosition = makeVector3({x.Code}, {y.Code}, {z.Code})");
            return true;
        }

        if (action.Type.Equals("SetBodyPositionForce", StringComparison.OrdinalIgnoreCase))
        {
            var amount = ParameterExpression(rule, action, nodesById, "amount", "Number", "100");
            AppendBodyPositionPropertyGuard(builder, plan, action, indentLevel, "Force", "Set Body Position Force");
            builder.AppendLine($"{indent}bodyPositionObject.Force = {amount.Code}");
            return true;
        }

        if (action.Type.Equals("SetBodyPositionAcceptanceDistance", StringComparison.OrdinalIgnoreCase))
        {
            var distance = ParameterExpression(rule, action, nodesById, "distance", "Number", "1");
            AppendBodyPositionPropertyGuard(builder, plan, action, indentLevel, "AcceptanceDistance", "Set Body Position Stop Distance");
            builder.AppendLine($"{indent}bodyPositionObject.AcceptanceDistance = {distance.Code}");
            return true;
        }

        return false;
    }

    private static bool TryAppendReadableBodyPositionConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (condition.Type.Equals("BodyPositionReachedTarget", StringComparison.OrdinalIgnoreCase))
        {
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "bodyPositionObject");
            builder.AppendLine($"{indent}if bodyPositionObject == nil or bodyPositionObject.TargetPosition == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local parentObject = bodyPositionObject.Parent");
            builder.AppendLine($"{indent}if parentObject == nil or parentObject.Position == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local stopDistance = tonumber(bodyPositionObject.AcceptanceDistance) or 0");
            builder.AppendLine($"{indent}local distanceToTarget = vrsDistanceBetweenPositions(parentObject.Position, bodyPositionObject.TargetPosition)");
            builder.AppendLine($"{indent}return distanceToTarget <= stopDistance");
            return true;
        }

        if (condition.Type.Equals("BodyPositionForceAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            var amount = ParameterExpression(rule, condition, nodesById, "amount", "Number", "100");
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "bodyPositionObject");
            builder.AppendLine($"{indent}if bodyPositionObject == nil or bodyPositionObject.Force == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return (tonumber(bodyPositionObject.Force) or 0) >= {amount.Code}");
            return true;
        }

        return false;
    }

    private static LuauExpression? TryResolveReadableBodyPositionPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "BodyPositionTarget" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "TargetPosition", "Vector3", "Body Position Target", "return targetObject.TargetPosition"),
            "BodyPositionForce" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Force", "Number", "Body Position Force", "return tonumber(targetObject.Force) or 0"),
            "BodyPositionAcceptanceDistance" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "AcceptanceDistance", "Number", "Body Position Stop Distance", "return tonumber(targetObject.AcceptanceDistance) or 0"),
            "BodyPositionDistanceToTarget" => BodyPositionDistanceExpression(rule, node, nodesById, visitedNodeIds),
            _ => null
        };
    }

    private static LuauExpression BodyPositionDistanceExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Self", visitedNodeIds);
        var code = $"(function() local bodyPositionObject = resolveTarget(triggerObject, {target.Code}); if bodyPositionObject == nil or bodyPositionObject.TargetPosition == nil then print(\"Body Position Distance To Target stopped: target Body Position was not found or has no target position.\"); return 0 end; local parentObject = bodyPositionObject.Parent; if parentObject == nil or parentObject.Position == nil then print(\"Body Position Distance To Target stopped: parent has no Position.\"); return 0 end; return vrsDistanceBetweenPositions(parentObject.Position, bodyPositionObject.TargetPosition) end)()";
        return new LuauExpression(code, "Number");
    }

    private static void AppendReadableBodyPositionReachedTargetTrigger(
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
        builder.AppendLine("    if triggerObject.TargetPosition == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target Body Position has no target position.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    local function readMatched()");
        builder.AppendLine("        if triggerObject.TargetPosition == nil then");
        builder.AppendLine($"            print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target Body Position has no target position.\")");
        builder.AppendLine("            return false, 0");
        builder.AppendLine("        end");
        builder.AppendLine("        local parentObject = triggerObject.Parent");
        builder.AppendLine("        if parentObject == nil or parentObject.Position == nil then");
        builder.AppendLine($"            print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: parent has no Position.\")");
        builder.AppendLine("            return false, 0");
        builder.AppendLine("        end");
        builder.AppendLine("        local stopDistance = tonumber(triggerObject.AcceptanceDistance) or 0");
        builder.AppendLine("        local distanceToTarget = vrsDistanceBetweenPositions(parentObject.Position, triggerObject.TargetPosition)");
        builder.AppendLine("        return distanceToTarget <= stopDistance, distanceToTarget");
        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, ", distance = currentValue", 1);
        builder.AppendLine("end");
    }

    private static void AppendBodyPositionPropertyGuard(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode node,
        int indentLevel,
        string propertyName,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, node, indentLevel, "bodyPositionObject");
        builder.AppendLine($"{indent}if bodyPositionObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target Body Position was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if bodyPositionObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target Body Position does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }
}
