using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Seat nodes stay isolated because they use a small Polytoria API surface:
    // occupancy events plus the CanNPCSit and Occupant properties.
    private static bool TryAppendReadableSeatActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (!action.Type.Equals("SetSeatAllowsNPCs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indent = IndentText(indentLevel);
        var enabled = ParameterExpression(rule, action, nodesById, "enabled", "Boolean", "true");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "seatObject");
        AppendSeatObjectGuard(builder, indentLevel, "Set Seat Allows NPCs");
        builder.AppendLine($"{indent}if seatObject.CanNPCSit == nil then");
        builder.AppendLine($"{indent}    print(\"Set Seat Allows NPCs stopped: target seat does not expose CanNPCSit.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}seatObject.CanNPCSit = {enabled.Code}");
        return true;
    }

    private static bool TryAppendReadableSeatConditionBody(
        StringBuilder builder,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (!condition.Type.Equals("SeatIsOccupied", StringComparison.OrdinalIgnoreCase) &&
            !condition.Type.Equals("SeatAllowsNPCs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        if (condition.Type.Equals("SeatIsOccupied", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"{indent}return targetObject.Occupant ~= nil");
            return true;
        }

        builder.AppendLine($"{indent}return targetObject.CanNPCSit == true");
        return true;
    }

    private static LuauExpression? TryResolveReadableSeatPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        if (node.Type.Equals("SeatOccupant", StringComparison.OrdinalIgnoreCase))
        {
            var target = ParameterExpression(rule, node, nodesById, "target", "Object Path", "Self");
            var code = $"(function() local targetObject = resolveTarget(triggerObject, {target.Code}); if targetObject == nil then print(\"Seat Occupant stopped: target was not found.\"); return nil end; return targetObject.Occupant end)()";
            return new LuauExpression(code, "Any");
        }

        return node.Type switch
        {
            "SeatAllowsNPCsValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "CanNPCSit", "Boolean", "Seat Allows NPCs", "return targetObject.CanNPCSit == true"),
            _ => null
        };
    }

    private static void AppendReadableSeatEventTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var eventName = trigger.Type.Equals("OnSeatSat", StringComparison.OrdinalIgnoreCase)
            ? "Sat"
            : "Vacated";
        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    if triggerObject.{eventName} == nil or triggerObject.{eventName}.Connect == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target seat has no {eventName} event.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine($"    triggerObject.{eventName}:Connect(function(occupant)");
        builder.AppendLine("        local triggerContext = { object = triggerObject, seat = triggerObject, occupant = occupant }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
        if (!emitted)
        {
            builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("    end)");
        builder.AppendLine("end");
    }

    private static bool IsSeatEventTrigger(string nodeType)
    {
        return nodeType.Equals("OnSeatSat", StringComparison.OrdinalIgnoreCase) ||
            nodeType.Equals("OnSeatVacated", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendSeatObjectGuard(StringBuilder builder, int indentLevel, string readableName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if seatObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target seat was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }
}
