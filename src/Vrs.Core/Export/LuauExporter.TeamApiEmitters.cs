using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static bool TryAppendReadableTeamApiActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (!action.Type.Equals("SetPlayerGameTeam", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indent = IndentText(indentLevel);
        AppendTriggerPlayerGuard(builder, indentLevel, action.Label, "return");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetTeam");
        builder.AppendLine($"{indent}if targetTeam == nil then");
        builder.AppendLine($"{indent}    print(\"Set Player Game Team stopped: team was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}player.Team = targetTeam");
        return true;
    }

    private static bool TryAppendReadableTeamApiConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (!condition.Type.Equals("PlayerIsInGameTeam", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indent = IndentText(indentLevel);
        AppendTriggerPlayerGuard(builder, indentLevel, condition.Label, "return false");
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetTeam");
        builder.AppendLine($"{indent}if targetTeam == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return player.Team == targetTeam");
        return true;
    }

    private static LuauExpression? TryResolveReadableTeamApiPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "TriggeringPlayerGameTeam" => new LuauExpression("((triggerContext ~= nil and triggerContext.player ~= nil and triggerContext.player.Team) or nil)", "SceneObject"),
            "PlayerGameTeamName" => new LuauExpression("(function() local team = ((triggerContext ~= nil and triggerContext.player ~= nil and triggerContext.player.Team) or nil); if team == nil then return \"\" end; if team.GetDisplayName ~= nil then return tostring(team:GetDisplayName()) end; if team.DisplayName ~= nil and tostring(team.DisplayName) ~= \"\" then return tostring(team.DisplayName) end; return tostring(team.Name or \"\") end)()", "String"),
            "PlayerGameTeamColor" => new LuauExpression("(function() local team = ((triggerContext ~= nil and triggerContext.player ~= nil and triggerContext.player.Team) or nil); if team == nil or team.Color == nil then return Color.New(1, 1, 1, 1) end return team.Color end)()", "Color"),
            "GameTeamName" => GameTeamTargetExpression(rule, node, nodesById, visitedNodeIds, "String", "Game Team Name", "if targetTeam.GetDisplayName ~= nil then return tostring(targetTeam:GetDisplayName()) end; if targetTeam.DisplayName ~= nil and tostring(targetTeam.DisplayName) ~= \"\" then return tostring(targetTeam.DisplayName) end; return tostring(targetTeam.Name or \"\")"),
            "GameTeamColor" => GameTeamTargetExpression(rule, node, nodesById, visitedNodeIds, "Color", "Game Team Color", "if targetTeam.Color == nil then return Color.New(1, 1, 1, 1) end return targetTeam.Color"),
            "GameTeamPlayerCount" => GameTeamTargetExpression(rule, node, nodesById, visitedNodeIds, "Number", "Game Team Player Count", "if targetTeam.GetPlayers == nil then return 0 end local players = targetTeam:GetPlayers(); if players == nil then return 0 end return #players"),
            "GameTeamPlayers" => GameTeamTargetExpression(rule, node, nodesById, visitedNodeIds, "Any", "Game Team Players", "if targetTeam.GetPlayers == nil then return {} end local players = targetTeam:GetPlayers(); if players == nil then return {} end return players"),
            "GameTeamCount" => new LuauExpression("(function() if Teams == nil or Teams.GetTeams == nil then return 0 end local teams = Teams:GetTeams(); if teams == nil then return 0 end return #teams end)()", "Number"),
            "AllGameTeams" => new LuauExpression("(function() if Teams == nil or Teams.GetTeams == nil then return {} end local teams = Teams:GetTeams(); if teams == nil then return {} end return teams end)()", "Any"),
            _ => null
        };
    }

    private static void AppendReadablePlayerGameTeamChangedTrigger(
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
        builder.AppendLine("    if Players == nil then");
        builder.AppendLine("        print(\"On Player Game Team Changed trigger stopped: Players is not available.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    local function connectPlayerTeamChanged(player)");
        builder.AppendLine("        if player == nil or player.TeamChanged == nil or player.TeamChanged.Connect == nil then");
        builder.AppendLine("            return");
        builder.AppendLine("        end");
        builder.AppendLine("        player.TeamChanged:Connect(function()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 3);
        builder.AppendLine("            local triggerContext = { object = triggerObject, player = player, team = player.Team }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 3, 0);
        if (!emitted)
        {
            builder.AppendLine("            print(\"On Player Game Team Changed trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("        end)");
        builder.AppendLine("    end");
        builder.AppendLine("    if Players.GetPlayers ~= nil then");
        builder.AppendLine("        for _, player in ipairs(Players:GetPlayers()) do");
        builder.AppendLine("            connectPlayerTeamChanged(player)");
        builder.AppendLine("        end");
        builder.AppendLine("    end");
        builder.AppendLine("    if Players.PlayerAdded ~= nil and Players.PlayerAdded.Connect ~= nil then");
        builder.AppendLine("        Players.PlayerAdded:Connect(connectPlayerTeamChanged)");
        builder.AppendLine("    end");
        builder.AppendLine("end");
    }

    private static LuauExpression GameTeamTargetExpression(
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
            "String" => "\"\"",
            "Number" => "0",
            "Color" => "Color.New(1, 1, 1, 1)",
            _ => "nil"
        };
        var code = $"(function() local targetTeam = resolveTarget(triggerObject, {target.Code}); if targetTeam == nil then print(\"{readableName} stopped: team was not found.\"); return {fallback} end; {returnStatement} end)()";
        return new LuauExpression(code, dataType);
    }
}
