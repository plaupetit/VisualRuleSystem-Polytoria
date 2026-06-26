using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // NPC nodes cover the documented character-control surface that does not
    // need asset pickers or external persistence.
    private static bool TryAppendReadableNpcActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (action.Type.Equals("SetNPCHealth", StringComparison.OrdinalIgnoreCase))
        {
            var amount = ParameterExpression(rule, action, nodesById, "amount", "Number", "100");
            AppendNpcTargetVariable(builder, plan, action, indentLevel, "Health", "Set NPC Health");
            builder.AppendLine($"{indent}npcObject.Health = {amount.Code}");
            return true;
        }

        if (action.Type.Equals("DamageNPC", StringComparison.OrdinalIgnoreCase))
        {
            var amount = ParameterExpression(rule, action, nodesById, "amount", "Number", "10");
            AppendNpcTargetGuard(builder, plan, action, indentLevel, "Damage NPC");
            builder.AppendLine($"{indent}if npcObject.TakeDamage ~= nil then");
            builder.AppendLine($"{indent}    npcObject:TakeDamage({amount.Code})");
            builder.AppendLine($"{indent}elseif npcObject.Health ~= nil then");
            builder.AppendLine($"{indent}    npcObject.Health = math.max(0, (tonumber(npcObject.Health) or 0) - ({amount.Code}))");
            builder.AppendLine($"{indent}else");
            builder.AppendLine($"{indent}    print(\"Damage NPC stopped: target NPC has no TakeDamage method or Health property.\")");
            builder.AppendLine($"{indent}end");
            return true;
        }

        if (action.Type.Equals("HealNPC", StringComparison.OrdinalIgnoreCase))
        {
            var amount = ParameterExpression(rule, action, nodesById, "amount", "Number", "10");
            AppendNpcTargetGuard(builder, plan, action, indentLevel, "Heal NPC");
            builder.AppendLine($"{indent}if npcObject.Heal ~= nil then");
            builder.AppendLine($"{indent}    npcObject:Heal({amount.Code})");
            builder.AppendLine($"{indent}elseif npcObject.Health ~= nil then");
            builder.AppendLine($"{indent}    npcObject.Health = (tonumber(npcObject.Health) or 0) + ({amount.Code})");
            builder.AppendLine($"{indent}else");
            builder.AppendLine($"{indent}    print(\"Heal NPC stopped: target NPC has no Heal method or Health property.\")");
            builder.AppendLine($"{indent}end");
            return true;
        }

        if (action.Type.Equals("KillNPC", StringComparison.OrdinalIgnoreCase))
        {
            AppendNpcTargetGuard(builder, plan, action, indentLevel, "Kill NPC");
            builder.AppendLine($"{indent}if npcObject.Kill ~= nil then");
            builder.AppendLine($"{indent}    npcObject:Kill()");
            builder.AppendLine($"{indent}elseif npcObject.Health ~= nil then");
            builder.AppendLine($"{indent}    npcObject.Health = 0");
            builder.AppendLine($"{indent}else");
            builder.AppendLine($"{indent}    print(\"Kill NPC stopped: target NPC has no Kill method or Health property.\")");
            builder.AppendLine($"{indent}end");
            return true;
        }

        if (action.Type.Equals("SetNPCWalkSpeed", StringComparison.OrdinalIgnoreCase))
        {
            var speed = ParameterExpression(rule, action, nodesById, "speed", "Number", "16");
            AppendNpcTargetVariable(builder, plan, action, indentLevel, "WalkSpeed", "Set NPC Walk Speed");
            builder.AppendLine($"{indent}npcObject.WalkSpeed = {speed.Code}");
            return true;
        }

        if (action.Type.Equals("SetNPCJumpPower", StringComparison.OrdinalIgnoreCase))
        {
            var power = ParameterExpression(rule, action, nodesById, "power", "Number", "50");
            AppendNpcTargetVariable(builder, plan, action, indentLevel, "JumpPower", "Set NPC Jump Power");
            builder.AppendLine($"{indent}npcObject.JumpPower = {power.Code}");
            return true;
        }

        if (action.Type.Equals("MakeNPCJump", StringComparison.OrdinalIgnoreCase))
        {
            AppendNpcMethodGuard(builder, plan, action, indentLevel, "Jump", "Make NPC Jump");
            builder.AppendLine($"{indent}npcObject:Jump()");
            return true;
        }

        if (action.Type.Equals("SetNPCNavigationTarget", StringComparison.OrdinalIgnoreCase))
        {
            var x = ParameterExpression(rule, action, nodesById, "x", "Number", "0");
            var y = ParameterExpression(rule, action, nodesById, "y", "Number", "0");
            var z = ParameterExpression(rule, action, nodesById, "z", "Number", "0");
            AppendNpcMethodGuard(builder, plan, action, indentLevel, "SetNavDestination", "Set NPC Navigation Target");
            builder.AppendLine($"{indent}npcObject:SetNavDestination(makeVector3({x.Code}, {y.Code}, {z.Code}))");
            return true;
        }

        return false;
    }

    private static bool TryAppendReadableNpcConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (condition.Type.Equals("NPCIsDead", StringComparison.OrdinalIgnoreCase))
        {
            AppendNpcBooleanCondition(builder, condition, plan, indentLevel, "IsDead");
            return true;
        }

        if (condition.Type.Equals("NPCIsOnGround", StringComparison.OrdinalIgnoreCase))
        {
            AppendNpcBooleanCondition(builder, condition, plan, indentLevel, "IsOnGround");
            return true;
        }

        if (condition.Type.Equals("NPCReachedNavigationTarget", StringComparison.OrdinalIgnoreCase))
        {
            AppendNpcBooleanCondition(builder, condition, plan, indentLevel, "NavDestinationReached");
            return true;
        }

        if (condition.Type.Equals("NPCHealthAtMost", StringComparison.OrdinalIgnoreCase))
        {
            var amount = ParameterExpression(rule, condition, nodesById, "amount", "Number", "0");
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "npcObject");
            builder.AppendLine($"{indent}if npcObject == nil or npcObject.Health == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return (tonumber(npcObject.Health) or 0) <= {amount.Code}");
            return true;
        }

        return false;
    }

    private static LuauExpression? TryResolveReadableNpcPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "NPCHealth" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Health", "Number", "NPC Health", "return tonumber(targetObject.Health) or 0"),
            "NPCWalkSpeed" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "WalkSpeed", "Number", "NPC Walk Speed", "return tonumber(targetObject.WalkSpeed) or 0"),
            "NPCJumpPower" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "JumpPower", "Number", "NPC Jump Power", "return tonumber(targetObject.JumpPower) or 0"),
            "NPCIsDeadValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "IsDead", "Boolean", "NPC Is Dead", "return targetObject.IsDead == true"),
            "NPCIsOnGroundValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "IsOnGround", "Boolean", "NPC Is On Ground", "return targetObject.IsOnGround == true"),
            "NPCNavigationDistance" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "NavDestinationDistance", "Number", "NPC Navigation Distance", "return tonumber(targetObject.NavDestinationDistance) or 0"),
            _ => null
        };
    }

    private static void AppendReadableNpcEventTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var eventName = trigger.Type switch
        {
            "OnNPCDied" => "Died",
            "OnNPCLanded" => "Landed",
            _ => "NavFinished"
        };
        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    if triggerObject.{eventName} == nil or triggerObject.{eventName}.Connect == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target NPC has no {eventName} event.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine($"    triggerObject.{eventName}:Connect(function()");
        builder.AppendLine("        local triggerContext = { object = triggerObject, npc = triggerObject }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
        if (!emitted)
        {
            builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("    end)");
        builder.AppendLine("end");
    }

    private static bool IsNpcEventTrigger(string nodeType)
    {
        return nodeType.Equals("OnNPCDied", StringComparison.OrdinalIgnoreCase) ||
            nodeType.Equals("OnNPCLanded", StringComparison.OrdinalIgnoreCase) ||
            nodeType.Equals("OnNPCNavigationFinished", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendNpcBooleanCondition(
        StringBuilder builder,
        RuleNode condition,
        ReadableExportPlan plan,
        int indentLevel,
        string propertyName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "npcObject");
        builder.AppendLine($"{indent}if npcObject == nil or npcObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return npcObject.{propertyName} == true");
    }

    private static void AppendNpcTargetVariable(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode node,
        int indentLevel,
        string propertyName,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        AppendNpcTargetGuard(builder, plan, node, indentLevel, readableName);
        builder.AppendLine($"{indent}if npcObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target NPC does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendNpcMethodGuard(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode node,
        int indentLevel,
        string methodName,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        AppendNpcTargetGuard(builder, plan, node, indentLevel, readableName);
        builder.AppendLine($"{indent}if npcObject.{methodName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target NPC does not expose {methodName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendNpcTargetGuard(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode node,
        int indentLevel,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, node, indentLevel, "npcObject");
        builder.AppendLine($"{indent}if npcObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target NPC was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }
}
