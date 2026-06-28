using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static bool TryAppendReadableStatsActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var isNumber = action.Type.Equals("SetPlayerStatNumber", StringComparison.OrdinalIgnoreCase);
        if (!isNumber && !action.Type.Equals("SetPlayerStatText", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indent = IndentText(indentLevel);
        var player = ObbyPlayerExpression(rule, action, nodesById);
        var value = ParameterExpression(rule, action, nodesById, "value", isNumber ? "Number" : "String", isNumber ? "0" : "");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "statObject");
        builder.AppendLine($"{indent}if statObject == nil then");
        builder.AppendLine($"{indent}    print(\"{action.Label} stopped: stat was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if statObject.Set == nil then");
        builder.AppendLine($"{indent}    print(\"{action.Label} stopped: target does not expose Set.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local player = {player.Code}");
        builder.AppendLine($"{indent}if player == nil or player == \"\" then");
        builder.AppendLine($"{indent}    print(\"{action.Label} stopped: no player was available.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine(isNumber
            ? $"{indent}statObject:Set(player, tonumber({value.Code}) or 0)"
            : $"{indent}statObject:Set(player, tostring({value.Code}))");
        return true;
    }

    private static bool TryAppendReadableStatsConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (!condition.Type.Equals("PlayerStatAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indent = IndentText(indentLevel);
        var player = ObbyPlayerExpression(rule, condition, nodesById);
        var minimum = ParameterExpression(rule, condition, nodesById, "minimum", "Number", "1");
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "statObject");
        builder.AppendLine($"{indent}if statObject == nil or statObject.Get == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local player = {player.Code}");
        builder.AppendLine($"{indent}if player == nil or player == \"\" then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return (tonumber(statObject:Get(player)) or 0) >= (tonumber({minimum.Code}) or 0)");
        return true;
    }

    private static LuauExpression? TryResolveReadableStatsPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "PlayerStatValue" => PlayerStatExpression(rule, node, nodesById, visitedNodeIds, "Any", "Player Stat Value", "Get", "return statObject:Get(player)"),
            "PlayerStatDisplayValue" => PlayerStatExpression(rule, node, nodesById, visitedNodeIds, "String", "Player Stat Display Value", "GetDisplayValue", "return tostring(statObject:GetDisplayValue(player) or \"\")"),
            "StatDisplayName" => StatTargetExpression(rule, node, nodesById, visitedNodeIds, "String", "Stat Display Name", "GetDisplayName", "if statObject.GetDisplayName ~= nil then return tostring(statObject:GetDisplayName()) end; return tostring(statObject.DisplayName or statObject.Name or \"\")"),
            "TeamStatTotal" => TeamStatTotalExpression(rule, node, nodesById, visitedNodeIds),
            "AllPlayerStats" => new LuauExpression("(function() if Stats == nil or Stats.GetStats == nil then return {} end local stats = Stats:GetStats(); if stats == nil then return {} end return stats end)()", "Any"),
            _ => null
        };
    }

    private static LuauExpression PlayerStatExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string dataType,
        string readableName,
        string requiredMethod,
        string returnStatement)
    {
        var player = ObbyPlayerPropertyExpression(rule, node, nodesById, visitedNodeIds);
        return StatTargetExpression(
            rule,
            node,
            nodesById,
            visitedNodeIds,
            dataType,
            readableName,
            requiredMethod,
            $"local player = {player.Code}; if player == nil or player == \"\" then return {StatsFallback(dataType)} end; {returnStatement}");
    }

    private static LuauExpression TeamStatTotalExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var team = PropertyParameterExpression(rule, node, nodesById, "team", "String", "Target", visitedNodeIds);
        return StatTargetExpression(
            rule,
            node,
            nodesById,
            visitedNodeIds,
            "Number",
            "Team Stat Total",
            "GetTotalForTeam",
            $"local teamObject = resolveTarget(triggerObject, {team.Code}); if teamObject == nil then return 0 end; return tonumber(statObject:GetTotalForTeam(teamObject)) or 0");
    }

    private static LuauExpression StatTargetExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string dataType,
        string readableName,
        string? requiredMethod,
        string returnStatement)
    {
        var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Target", visitedNodeIds);
        var fallback = StatsFallback(dataType);
        var methodGuard = string.IsNullOrWhiteSpace(requiredMethod)
            ? ""
            : $" if statObject.{requiredMethod} == nil then return {fallback} end;";
        var code = $"(function() local statObject = resolveTarget(triggerObject, {target.Code}); if statObject == nil then print(\"{readableName} stopped: stat was not found.\"); return {fallback} end;{methodGuard} {returnStatement} end)()";
        return new LuauExpression(code, dataType);
    }

    private static string StatsFallback(string dataType)
    {
        return NormalizeExpressionDataType(dataType) switch
        {
            "String" => "\"\"",
            "Number" => "0",
            "Boolean" => "false",
            _ => "nil"
        };
    }
}
