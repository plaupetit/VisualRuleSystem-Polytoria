using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static readonly HashSet<string> EssentialsRuntimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "StartCooldown",
        "ResetCooldown",
        "CooldownReady",
        "CooldownRemainingSeconds",
        "OpenGate",
        "CloseGate",
        "ToggleGate",
        "GateIsOpen",
        "StartRound",
        "EndRound",
        "SetRoundTime",
        "RoundIsRunning",
        "RoundTimeRemaining",
        "RoundTimeExpired",
        "RoundHasTimeLeft",
        "OnRoundTimeExpired",
        "SetPlayerScore",
        "AddPlayerScore",
        "ResetPlayerScore",
        "PlayerScore",
        "PlayerScoreAtLeast",
        "PlayerScoreAtMost",
        "PlayerScoreEquals",
        "SetPlayerLives",
        "AddPlayerLives",
        "ResetPlayerLives",
        "PlayerLives",
        "PlayerLivesAtLeast",
        "PlayerLivesAtMost",
        "PlayerLivesEquals",
        "PlayerHasNoLivesLeft",
        "SetPlayerTeam",
        "PlayerTeam",
        "PlayerTeamIs",
        "SetTeamScore",
        "AddTeamScore",
        "ResetTeamScore",
        "TeamScore",
        "TeamScoreAtLeast",
        "TeamScoreAtMost",
        "OnTeamScoreReached",
        "OnTeamScoreDroppedTo",
        "OnAnyPlayerScoreReached",
        "OnAnyPlayerHasNoLivesLeft",
        "OnObjectXPositionReached",
        "OnObjectHeightPositionReached",
        "OnObjectHeightPositionDroppedTo",
        "OnObjectTurnAngleReached",
        "OnObjectTurnAngleDroppedTo",
        "OnObjectWidthSizeReached",
        "OnObjectWidthSizeDroppedTo",
        "OnObjectHeightSizeReached",
        "OnObjectHeightSizeDroppedTo",
        "OnObjectStartedMoving",
        "OnObjectStoppedMoving",
        "OnObjectSpeedReached",
        "OnObjectSpeedDroppedTo",
        "OnObjectEnteredArea",
        "OnObjectLeftArea",
        "OnObjectEnteredBoxArea",
        "OnObjectLeftBoxArea",
        "OnObjectEnteredHeightBand",
        "OnObjectLeftHeightBand",
        "DistanceBetweenObjects",
        "DistanceBetweenPositions",
        "ObjectIsCloseToObject",
        "ObjectIsFarFromObject",
        "ObjectXPosition",
        "ObjectHeightPosition",
        "ObjectZPosition",
        "ObjectTurnAngle",
        "ObjectWidthSize",
        "ObjectHeightSize",
        "ObjectDepthSize",
        "ObjectIsAboveHeight",
        "ObjectIsBelowHeight",
        "ObjectTurnAngleAtLeast",
        "ObjectTurnAngleAtMost",
        "ObjectSizeAtLeast",
        "ObjectSizeAtMost",
        "BodyPositionReachedTarget",
        "BodyPositionDistanceToTarget",
        "OnBodyPositionReachedTarget",
        "MoveObjectWithPhysics",
        "TurnObjectWithPhysics",
        "SetObjectVelocity",
        "SetObjectSpinVelocity",
        "ObjectIsMoving",
        "ObjectSpeedAtLeast",
        "ObjectVelocity",
        "ObjectSpeed",
        "ObjectSpinVelocity",
        "TouchingObjectCount",
        "SetObjectXPosition",
        "SetObjectHeightPosition",
        "SetObjectZPosition",
        "SetObjectTurnAngle",
        "TurnObjectByAngle",
        "SetObjectWidthSize",
        "SetObjectHeightSize",
        "SetObjectDepthSize",
        "TimeNowSeconds"
    };

    private static readonly HashSet<string> EssentialsVectorFactoryTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "DistanceBetweenPositions"
    };

    private static readonly HashSet<string> EssentialsTargetResolverTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "DistanceBetweenObjects"
    };

    private static readonly HashSet<string> ObjectDistanceConditionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ObjectIsCloseToObject",
        "ObjectIsFarFromObject",
        "OnObjectsBecameClose",
        "OnObjectsBecameFar"
    };

    private static bool RuleUsesEssentialsRuntime(Rule rule)
        => RuleUsesCatalogTypes(rule, EssentialsRuntimeTypes);

    private static bool RuleUsesEssentialsVectorFactory(Rule rule)
        => RuleUsesCatalogTypes(rule, EssentialsVectorFactoryTypes);

    private static bool RuleUsesEssentialsTargetResolver(Rule rule)
        => RuleUsesCatalogTypes(rule, EssentialsTargetResolverTypes);

    private static bool RuleUsesCatalogTypes(Rule rule, HashSet<string> catalogTypes)
    {
        return rule.Nodes.Any(node =>
            catalogTypes.Contains(node.Type) ||
            node.Parameters.Any(parameter => BindingUsesCatalogTypes(parameter.Binding, catalogTypes)));
    }

    private static bool BindingUsesCatalogTypes(GraphValueBinding binding, HashSet<string> catalogTypes)
    {
        if (binding.SourceKind == GraphValueSourceKind.CatalogValue && catalogTypes.Contains(binding.CatalogType))
        {
            return true;
        }

        return binding.CatalogParameters.Any(parameter => BindingUsesCatalogTypes(parameter.Binding, catalogTypes));
    }

    private static bool TryAppendReadableEssentialsActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (action.Type.Equals("StartCooldown", StringComparison.OrdinalIgnoreCase))
        {
            var duration = ParameterExpression(rule, action, nodesById, "duration", "Number", "1");
            AppendNamedRuntimeKey(builder, rule, action, nodesById, indentLevel, "cooldownKey", "cooldownName", "Default", "cooldown");
            builder.AppendLine($"{IndentText(indentLevel)}VRS.vars[cooldownKey .. \":readyAt\"] = vrsNow() + math.max(0, {duration.Code})");
            return true;
        }

        if (action.Type.Equals("ResetCooldown", StringComparison.OrdinalIgnoreCase))
        {
            AppendNamedRuntimeKey(builder, rule, action, nodesById, indentLevel, "cooldownKey", "cooldownName", "Default", "cooldown");
            builder.AppendLine($"{IndentText(indentLevel)}VRS.vars[cooldownKey .. \":readyAt\"] = nil");
            return true;
        }

        if (action.Type.Equals("OpenGate", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("CloseGate", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("ToggleGate", StringComparison.OrdinalIgnoreCase))
        {
            AppendGateAction(builder, rule, action, nodesById, indentLevel);
            return true;
        }

        if (action.Type.Equals("StartRound", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("EndRound", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("SetRoundTime", StringComparison.OrdinalIgnoreCase))
        {
            AppendRoundAction(builder, rule, action, nodesById, indentLevel);
            return true;
        }

        if (action.Type.Equals("SetPlayerScore", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("AddPlayerScore", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("ResetPlayerScore", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("SetPlayerLives", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("AddPlayerLives", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("ResetPlayerLives", StringComparison.OrdinalIgnoreCase))
        {
            var valueKey = action.Type.Contains("Lives", StringComparison.OrdinalIgnoreCase) ? "lives" : "score";
            var parameterKey = action.Type.StartsWith("Set", StringComparison.OrdinalIgnoreCase) ? valueKey : "amount";
            var fallback = valueKey.Equals("lives", StringComparison.OrdinalIgnoreCase) ? "3" : "0";
            var value = action.Type.Equals("ResetPlayerScore", StringComparison.OrdinalIgnoreCase)
                ? new LuauExpression("0", "Number")
                : action.Type.Equals("ResetPlayerLives", StringComparison.OrdinalIgnoreCase)
                    ? ParameterExpression(rule, action, nodesById, "lives", "Number", "3")
                : ParameterExpression(rule, action, nodesById, parameterKey, "Number", parameterKey == "amount" ? "1" : fallback);
            AppendTriggerPlayerGuard(builder, indentLevel, action.Label, "return");
            builder.AppendLine($"{IndentText(indentLevel)}local runtimeKey = \"player:\" .. vrsPlayerKey(player) .. \":{valueKey}\"");
            var assignment = action.Type.StartsWith("Add", StringComparison.OrdinalIgnoreCase)
                ? $"(tonumber(VRS.vars[runtimeKey]) or 0) + {value.Code}"
                : value.Code;
            builder.AppendLine($"{IndentText(indentLevel)}VRS.vars[runtimeKey] = {assignment}");
            return true;
        }

        if (action.Type.Equals("SetPlayerTeam", StringComparison.OrdinalIgnoreCase))
        {
            var teamName = ParameterExpression(rule, action, nodesById, "teamName", "String", "Players");
            AppendTriggerPlayerGuard(builder, indentLevel, action.Label, "return");
            builder.AppendLine($"{IndentText(indentLevel)}VRS.vars[\"player:\" .. vrsPlayerKey(player) .. \":team\"] = tostring({teamName.Code})");
            return true;
        }

        if (action.Type.Equals("SetTeamScore", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("AddTeamScore", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("ResetTeamScore", StringComparison.OrdinalIgnoreCase))
        {
            var value = action.Type.Equals("ResetTeamScore", StringComparison.OrdinalIgnoreCase)
                ? new LuauExpression("0", "Number")
                : ParameterExpression(rule, action, nodesById, action.Type.StartsWith("Add", StringComparison.OrdinalIgnoreCase) ? "amount" : "score", "Number", action.Type.StartsWith("Add", StringComparison.OrdinalIgnoreCase) ? "1" : "0");
            AppendNamedRuntimeKey(builder, rule, action, nodesById, indentLevel, "teamKey", "teamName", "Players", "team");
            builder.AppendLine($"{IndentText(indentLevel)}local runtimeKey = teamKey .. \":score\"");
            var assignment = action.Type.StartsWith("Add", StringComparison.OrdinalIgnoreCase)
                ? $"(tonumber(VRS.vars[runtimeKey]) or 0) + {value.Code}"
                : value.Code;
            builder.AppendLine($"{IndentText(indentLevel)}VRS.vars[runtimeKey] = {assignment}");
            return true;
        }

        return false;
    }

    private static void AppendGateAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        AppendNamedRuntimeKey(builder, rule, action, nodesById, indentLevel, "gateKey", "gateName", "Main Gate", "gate");
        if (action.Type.Equals("ToggleGate", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"{indent}VRS.states[gateKey] = not (VRS.states[gateKey] == true)");
            return;
        }

        var enabled = action.Type.Equals("OpenGate", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
        builder.AppendLine($"{indent}VRS.states[gateKey] = {enabled}");
    }

    private static void AppendRoundAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        AppendNamedRuntimeKey(builder, rule, action, nodesById, indentLevel, "roundKey", "roundName", "Main Round", "round");
        if (action.Type.Equals("EndRound", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"{indent}VRS.states[roundKey .. \":running\"] = false");
            builder.AppendLine($"{indent}VRS.vars[roundKey .. \":endAt\"] = vrsNow()");
            return;
        }

        var duration = ParameterExpression(rule, action, nodesById, "duration", "Number", "60");
        if (action.Type.Equals("StartRound", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"{indent}VRS.states[roundKey .. \":running\"] = true");
            builder.AppendLine($"{indent}VRS.vars[roundKey .. \":startAt\"] = vrsNow()");
        }

        builder.AppendLine($"{indent}VRS.vars[roundKey .. \":duration\"] = math.max(0, {duration.Code})");
        builder.AppendLine($"{indent}if (tonumber(VRS.vars[roundKey .. \":duration\"]) or 0) > 0 then");
        builder.AppendLine($"{indent}    VRS.vars[roundKey .. \":endAt\"] = vrsNow() + VRS.vars[roundKey .. \":duration\"]");
        builder.AppendLine($"{indent}else");
        builder.AppendLine($"{indent}    VRS.vars[roundKey .. \":endAt\"] = nil");
        builder.AppendLine($"{indent}end");
    }

    private static bool TryAppendReadableEssentialsConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (condition.Type.Equals("CooldownReady", StringComparison.OrdinalIgnoreCase))
        {
            AppendNamedRuntimeKey(builder, rule, condition, nodesById, indentLevel, "cooldownKey", "cooldownName", "Default", "cooldown");
            builder.AppendLine($"{indent}local readyAt = tonumber(VRS.vars[cooldownKey .. \":readyAt\"]) or 0");
            builder.AppendLine($"{indent}return vrsNow() >= readyAt");
            return true;
        }

        if (condition.Type.Equals("GateIsOpen", StringComparison.OrdinalIgnoreCase))
        {
            AppendNamedRuntimeKey(builder, rule, condition, nodesById, indentLevel, "gateKey", "gateName", "Main Gate", "gate");
            builder.AppendLine($"{indent}return VRS.states[gateKey] == true");
            return true;
        }

        if (condition.Type.Equals("RoundIsRunning", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("RoundTimeExpired", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("RoundHasTimeLeft", StringComparison.OrdinalIgnoreCase))
        {
            AppendNamedRuntimeKey(builder, rule, condition, nodesById, indentLevel, "roundKey", "roundName", "Main Round", "round");
            if (condition.Type.Equals("RoundIsRunning", StringComparison.OrdinalIgnoreCase))
            {
                builder.AppendLine($"{indent}return VRS.states[roundKey .. \":running\"] == true");
            }
            else if (condition.Type.Equals("RoundHasTimeLeft", StringComparison.OrdinalIgnoreCase))
            {
                builder.AppendLine($"{indent}local endAt = tonumber(VRS.vars[roundKey .. \":endAt\"])");
                builder.AppendLine($"{indent}return VRS.states[roundKey .. \":running\"] == true and endAt ~= nil and vrsNow() < endAt");
            }
            else
            {
                builder.AppendLine($"{indent}local endAt = tonumber(VRS.vars[roundKey .. \":endAt\"])");
                builder.AppendLine($"{indent}return VRS.states[roundKey .. \":running\"] == true and endAt ~= nil and vrsNow() >= endAt");
            }

            return true;
        }

        if (condition.Type.Equals("PlayerScoreAtLeast", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("PlayerScoreAtMost", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("PlayerScoreEquals", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("PlayerLivesAtLeast", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("PlayerLivesAtMost", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("PlayerLivesEquals", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("PlayerHasNoLivesLeft", StringComparison.OrdinalIgnoreCase))
        {
            var valueKey = condition.Type.Contains("Lives", StringComparison.OrdinalIgnoreCase) ? "lives" : "score";
            var limitParameter = condition.Type.EndsWith("AtMost", StringComparison.OrdinalIgnoreCase)
                ? "maximum"
                : condition.Type.EndsWith("Equals", StringComparison.OrdinalIgnoreCase)
                    ? valueKey
                    : "minimum";
            var limit = ParameterExpression(rule, condition, nodesById, limitParameter, "Number", valueKey == "lives" ? "1" : "10");
            AppendTriggerPlayerGuard(builder, indentLevel, condition.Label, "return false");
            if (condition.Type.Equals("PlayerHasNoLivesLeft", StringComparison.OrdinalIgnoreCase))
            {
                builder.AppendLine($"{indent}return (tonumber(VRS.vars[\"player:\" .. vrsPlayerKey(player) .. \":lives\"]) or 0) <= 0");
                return true;
            }

            var comparison = condition.Type.EndsWith("AtMost", StringComparison.OrdinalIgnoreCase)
                ? "<="
                : condition.Type.EndsWith("Equals", StringComparison.OrdinalIgnoreCase)
                    ? "=="
                    : ">=";
            builder.AppendLine($"{indent}return (tonumber(VRS.vars[\"player:\" .. vrsPlayerKey(player) .. \":{valueKey}\"]) or 0) {comparison} {limit.Code}");
            return true;
        }

        if (condition.Type.Equals("PlayerTeamIs", StringComparison.OrdinalIgnoreCase))
        {
            var teamName = ParameterExpression(rule, condition, nodesById, "teamName", "String", "Players");
            AppendTriggerPlayerGuard(builder, indentLevel, condition.Label, "return false");
            builder.AppendLine($"{indent}return tostring(VRS.vars[\"player:\" .. vrsPlayerKey(player) .. \":team\"] or \"\") == tostring({teamName.Code})");
            return true;
        }

        if (condition.Type.Equals("TeamScoreAtLeast", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("TeamScoreAtMost", StringComparison.OrdinalIgnoreCase))
        {
            var limitParameter = condition.Type.Equals("TeamScoreAtMost", StringComparison.OrdinalIgnoreCase) ? "maximum" : "minimum";
            var limit = ParameterExpression(rule, condition, nodesById, limitParameter, "Number", "10");
            AppendNamedRuntimeKey(builder, rule, condition, nodesById, indentLevel, "teamKey", "teamName", "Players", "team");
            var comparison = condition.Type.Equals("TeamScoreAtMost", StringComparison.OrdinalIgnoreCase) ? "<=" : ">=";
            builder.AppendLine($"{indent}return (tonumber(VRS.vars[teamKey .. \":score\"]) or 0) {comparison} {limit.Code}");
            return true;
        }

        return false;
    }

    private static LuauExpression? TryResolveReadableEssentialsPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        if (node.Type.Equals("CooldownRemainingSeconds", StringComparison.OrdinalIgnoreCase))
        {
            var key = RuntimeKeyExpression(rule, node, nodesById, "cooldownName", "Default", "cooldown", visitedNodeIds);
            return new LuauExpression($"(function() local readyAt = tonumber(VRS.vars[{key} .. \":readyAt\"]) or 0; return math.max(0, readyAt - vrsNow()) end)()", "Number");
        }

        if (node.Type.Equals("RoundTimeRemaining", StringComparison.OrdinalIgnoreCase))
        {
            var key = RuntimeKeyExpression(rule, node, nodesById, "roundName", "Main Round", "round", visitedNodeIds);
            return new LuauExpression($"(function() local endAt = tonumber(VRS.vars[{key} .. \":endAt\"]); if endAt == nil then return 0 end return math.max(0, endAt - vrsNow()) end)()", "Number");
        }

        if (node.Type.Equals("PlayerScore", StringComparison.OrdinalIgnoreCase) ||
            node.Type.Equals("PlayerLives", StringComparison.OrdinalIgnoreCase))
        {
            var valueKey = node.Type.Equals("PlayerLives", StringComparison.OrdinalIgnoreCase) ? "lives" : "score";
            return new LuauExpression($"(function() local player = ((triggerContext ~= nil and triggerContext.player) or nil); if player == nil then return 0 end return tonumber(VRS.vars[\"player:\" .. vrsPlayerKey(player) .. \":{valueKey}\"]) or 0 end)()", "Number");
        }

        if (node.Type.Equals("PlayerTeam", StringComparison.OrdinalIgnoreCase))
        {
            return new LuauExpression("(function() local player = ((triggerContext ~= nil and triggerContext.player) or nil); if player == nil then return \"\" end return tostring(VRS.vars[\"player:\" .. vrsPlayerKey(player) .. \":team\"] or \"\") end)()", "String");
        }

        if (node.Type.Equals("TeamScore", StringComparison.OrdinalIgnoreCase))
        {
            var key = RuntimeKeyExpression(rule, node, nodesById, "teamName", "Players", "team", visitedNodeIds);
            return new LuauExpression($"(tonumber(VRS.vars[{key} .. \":score\"]) or 0)", "Number");
        }

        if (node.Type.Equals("DistanceBetweenObjects", StringComparison.OrdinalIgnoreCase))
        {
            var first = PropertyParameterExpression(rule, node, nodesById, "from", "String", "Self", visitedNodeIds);
            var second = PropertyParameterExpression(rule, node, nodesById, "to", "String", "Target", visitedNodeIds);
            var code = $"(function() local firstObject = resolveTarget(triggerObject, {first.Code}); local secondObject = resolveTarget(triggerObject, {second.Code}); if firstObject == nil or secondObject == nil then print(\"Distance Between Objects stopped: an object was not found.\"); return 0 end; if firstObject.Position == nil or secondObject.Position == nil then print(\"Distance Between Objects stopped: an object has no Position.\"); return 0 end; return vrsDistanceBetweenPositions(firstObject.Position, secondObject.Position) end)()";
            return new LuauExpression(code, "Number");
        }

        if (node.Type.Equals("DistanceBetweenPositions", StringComparison.OrdinalIgnoreCase))
        {
            var first = PropertyVectorParameterExpression(rule, node, nodesById, "first", "0", "0", "0", visitedNodeIds);
            var second = PropertyVectorParameterExpression(rule, node, nodesById, "second", "0", "0", "0", visitedNodeIds);
            return new LuauExpression($"vrsDistanceBetweenPositions({first.Code}, {second.Code})", "Number");
        }

        if (node.Type.Equals("PercentNumber", StringComparison.OrdinalIgnoreCase))
        {
            var part = PropertyParameterExpression(rule, node, nodesById, "part", "Number", "0", visitedNodeIds);
            var whole = PropertyParameterExpression(rule, node, nodesById, "whole", "Number", "100", visitedNodeIds);
            return new LuauExpression($"(function() local wholeValue = {whole.Code}; if wholeValue == 0 then return 0 end return ({part.Code} / wholeValue) * 100 end)()", "Number");
        }

        if (node.Type.Equals("MapNumberRange", StringComparison.OrdinalIgnoreCase))
        {
            var value = PropertyParameterExpression(rule, node, nodesById, "value", "Number", "0", visitedNodeIds);
            var inMin = PropertyParameterExpression(rule, node, nodesById, "inMin", "Number", "0", visitedNodeIds);
            var inMax = PropertyParameterExpression(rule, node, nodesById, "inMax", "Number", "1", visitedNodeIds);
            var outMin = PropertyParameterExpression(rule, node, nodesById, "outMin", "Number", "0", visitedNodeIds);
            var outMax = PropertyParameterExpression(rule, node, nodesById, "outMax", "Number", "100", visitedNodeIds);
            return new LuauExpression($"(function() local inputSpan = {inMax.Code} - {inMin.Code}; if inputSpan == 0 then return {outMin.Code} end local alpha = ({value.Code} - {inMin.Code}) / inputSpan; return {outMin.Code} + (({outMax.Code} - {outMin.Code}) * alpha) end)()", "Number");
        }

        if (node.Type.Equals("TimeNowSeconds", StringComparison.OrdinalIgnoreCase))
        {
            return new LuauExpression("vrsNow()", "Number");
        }

        return null;
    }

    private static void AppendNamedRuntimeKey(
        StringBuilder builder,
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string localName,
        string parameterKey,
        string fallback,
        string prefix)
    {
        var expression = RuntimeKeyExpression(rule, node, nodesById, parameterKey, fallback, prefix, []);
        builder.AppendLine($"{IndentText(indentLevel)}local {localName} = {expression}");
    }

    private static string RuntimeKeyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        string parameterKey,
        string fallback,
        string prefix,
        HashSet<string> visitedNodeIds)
    {
        var value = PropertyParameterExpression(rule, node, nodesById, parameterKey, "String", fallback, visitedNodeIds);
        return $"({LuauStringLiteral(prefix + ":")} .. tostring({value.Code}))";
    }

    private static void AppendTriggerPlayerGuard(StringBuilder builder, int indentLevel, string readableName, string exitStatement)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}local player = ((triggerContext ~= nil and triggerContext.player) or nil)");
        builder.AppendLine($"{indent}if player == nil then");
        builder.AppendLine($"{indent}    print(\"{EscapeForDoubleQuotedString(readableName)} stopped: trigger context has no player.\")");
        builder.AppendLine($"{indent}    {exitStatement}");
        builder.AppendLine($"{indent}end");
    }
}
