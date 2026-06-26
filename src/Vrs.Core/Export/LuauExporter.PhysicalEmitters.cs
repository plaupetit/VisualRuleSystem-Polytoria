using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static readonly HashSet<string> PhysicalEventTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnTouchObject",
        "OnObjectTouchEnded",
        "OnObjectHoverStarted",
        "OnObjectHoverEnded",
        "OnObjectClicked"
    };

    private static bool TryAppendReadablePhysicalActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (action.Type.Equals("MoveObjectWithPhysics", StringComparison.OrdinalIgnoreCase))
        {
            AppendPhysicalVectorMethodAction(builder, rule, action, plan, nodesById, indentLevel, "MovePosition", "Move Object With Physics");
            return true;
        }

        if (action.Type.Equals("TurnObjectWithPhysics", StringComparison.OrdinalIgnoreCase))
        {
            AppendPhysicalVectorMethodAction(builder, rule, action, plan, nodesById, indentLevel, "MoveRotation", "Turn Object With Physics");
            return true;
        }

        if (action.Type.Equals("SetObjectVelocity", StringComparison.OrdinalIgnoreCase))
        {
            AppendPhysicalVectorPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Velocity", "Set Object Velocity");
            return true;
        }

        if (action.Type.Equals("SetObjectSpinVelocity", StringComparison.OrdinalIgnoreCase))
        {
            AppendPhysicalVectorPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "AngularVelocity", "Set Object Spin Velocity");
            return true;
        }

        return false;
    }

    private static bool TryAppendReadablePhysicalConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (!condition.Type.Equals("ObjectIsMoving", StringComparison.OrdinalIgnoreCase) &&
            !condition.Type.Equals("ObjectSpeedAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indent = IndentText(indentLevel);
        var parameterKey = condition.Type.Equals("ObjectIsMoving", StringComparison.OrdinalIgnoreCase)
            ? "minimumSpeed"
            : "speed";
        var fallback = condition.Type.Equals("ObjectIsMoving", StringComparison.OrdinalIgnoreCase) ? "0.1" : "10";
        var speed = ParameterExpression(rule, condition, nodesById, parameterKey, "Number", fallback);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.Velocity == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local currentSpeed = vrsDistanceBetweenPositions(targetObject.Velocity, makeVector3(0, 0, 0))");
        builder.AppendLine($"{indent}return currentSpeed >= {speed.Code}");
        return true;
    }

    private static LuauExpression? TryResolveReadablePhysicalPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "ObjectVelocity" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Velocity", "Vector3", "Object Velocity", "return targetObject.Velocity"),
            "ObjectSpinVelocity" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "AngularVelocity", "Vector3", "Object Spin Velocity", "return targetObject.AngularVelocity"),
            "ObjectSpeed" => ObjectSpeedExpression(rule, node, nodesById, visitedNodeIds),
            "TouchingObjectCount" => TouchingObjectCountExpression(rule, node, nodesById, visitedNodeIds),
            _ => null
        };
    }

    private static void AppendReadablePhysicalEventTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var definition = PhysicalEventDefinition.For(trigger.Type);
        builder.AppendLine($"local function {functionName}()");
        builder.AppendLine("    local scriptParent = script.Parent");
        builder.AppendLine($"    local listenObject = {ReadableTriggerTargetExpression(plan, trigger, "scriptParent")}");
        builder.AppendLine("    if listenObject == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target was not found.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine($"    if listenObject.{definition.EventName} == nil or listenObject.{definition.EventName}.Connect == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target has no {definition.EventName} event.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine($"    listenObject.{definition.EventName}:Connect(function({definition.ParameterList})");
        builder.AppendLine("        local triggerObject = listenObject");
        builder.AppendLine($"        local triggerContext = {{ object = triggerObject{definition.ContextFields} }}");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
        if (!emitted)
        {
            builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("    end)");
        builder.AppendLine("end");
    }

    private static void AppendPhysicalVectorMethodAction(
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
        var value = ParameterVectorExpression(rule, action, nodesById, "vector", "0", "0", "0");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.{methodName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not support {methodName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}targetObject:{methodName}({value.Code})");
    }

    private static void AppendPhysicalVectorPropertyAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        var value = ParameterVectorExpression(rule, action, nodesById, "vector", "0", "0", "0");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}targetObject.{propertyName} = {value.Code}");
    }

    private static LuauExpression ObjectSpeedExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Self", visitedNodeIds);
        var code = $"(function() local targetObject = resolveTarget(triggerObject, {target.Code}); if targetObject == nil then print(\"Object Speed stopped: target was not found.\"); return 0 end; if targetObject.Velocity == nil then print(\"Object Speed stopped: target does not expose Velocity.\"); return 0 end; return vrsDistanceBetweenPositions(targetObject.Velocity, makeVector3(0, 0, 0)) end)()";
        return new LuauExpression(code, "Number");
    }

    private static LuauExpression TouchingObjectCountExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Self", visitedNodeIds);
        var code = $"(function() local targetObject = resolveTarget(triggerObject, {target.Code}); if targetObject == nil then print(\"Touching Object Count stopped: target was not found.\"); return 0 end; if targetObject.GetTouching == nil then print(\"Touching Object Count stopped: target does not support GetTouching.\"); return 0 end; local touchingObjects = targetObject:GetTouching(); if touchingObjects == nil then return 0 end; return #touchingObjects end)()";
        return new LuauExpression(code, "Number");
    }

    private sealed record PhysicalEventDefinition(string EventName, string ParameterList, string ContextFields)
    {
        public static PhysicalEventDefinition For(string triggerType)
        {
            return triggerType switch
            {
                "OnTouchObject" => new("Touched", "hit", ", touchObject = hit, touchObjectSource = triggerObject"),
                "OnObjectTouchEnded" => new("TouchEnded", "hit", ", touchObject = hit, touchObjectSource = triggerObject"),
                "OnObjectHoverStarted" => new("MouseEnter", "", ""),
                "OnObjectHoverEnded" => new("MouseExit", "", ""),
                "OnObjectClicked" => new("Clicked", "player", ", player = player"),
                _ => throw new ArgumentOutOfRangeException(nameof(triggerType), triggerType, "Unknown physical event trigger.")
            };
        }
    }
}
