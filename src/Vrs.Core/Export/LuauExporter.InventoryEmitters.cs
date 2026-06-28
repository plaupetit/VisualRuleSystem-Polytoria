using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static bool TryAppendReadableInventoryActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (!action.Type.Equals("GiveToolToPlayer", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indent = IndentText(indentLevel);
        var player = ObbyPlayerExpression(rule, action, nodesById);
        builder.AppendLine($"{indent}local player = {player.Code}");
        builder.AppendLine($"{indent}if player == nil or player == \"\" then");
        builder.AppendLine($"{indent}    print(\"Give Tool To Player stopped: no player was available.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local inventory = player.Inventory");
        builder.AppendLine($"{indent}if inventory == nil then");
        builder.AppendLine($"{indent}    print(\"Give Tool To Player stopped: player inventory was not available.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "toolObject");
        builder.AppendLine($"{indent}if toolObject == nil then");
        builder.AppendLine($"{indent}    print(\"Give Tool To Player stopped: target tool was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if toolObject.IsA ~= nil and not toolObject:IsA(\"Tool\") then");
        builder.AppendLine($"{indent}    print(\"Give Tool To Player stopped: target object is not a Tool.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}toolObject.Parent = inventory");
        return true;
    }

    private static bool TryAppendReadableInventoryConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (!condition.Type.Equals("PlayerHasTool", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indent = IndentText(indentLevel);
        var player = ObbyPlayerExpression(rule, condition, nodesById);
        var toolName = ParameterExpression(rule, condition, nodesById, "toolName", "String", "");
        builder.AppendLine($"{indent}local player = {player.Code}");
        builder.AppendLine($"{indent}if player == nil or player == \"\" then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local inventory = player.Inventory");
        builder.AppendLine($"{indent}if inventory == nil or inventory.FindChild == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return inventory:FindChild(tostring({toolName.Code})) ~= nil");
        return true;
    }

    private static LuauExpression? TryResolveReadableInventoryPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        if (node.Type.Equals("PlayerInventory", StringComparison.OrdinalIgnoreCase))
        {
            var player = ObbyPlayerPropertyExpression(rule, node, nodesById, visitedNodeIds);
            return new LuauExpression($"(function() local player = {player.Code}; if player == nil or player == \"\" then return nil end return player.Inventory end)()", "SceneObject");
        }

        if (node.Type.Equals("FindToolInInventory", StringComparison.OrdinalIgnoreCase))
        {
            var player = ObbyPlayerPropertyExpression(rule, node, nodesById, visitedNodeIds);
            var toolName = PropertyParameterExpression(rule, node, nodesById, "toolName", "String", "", visitedNodeIds);
            return new LuauExpression($"(function() local player = {player.Code}; if player == nil or player == \"\" then return nil end local inventory = player.Inventory; if inventory == nil or inventory.FindChild == nil then return nil end return inventory:FindChild(tostring({toolName.Code})) end)()", "SceneObject");
        }

        return null;
    }
}
