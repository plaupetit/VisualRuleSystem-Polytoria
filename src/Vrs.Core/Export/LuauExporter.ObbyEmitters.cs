using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static readonly HashSet<string> ObbyTouchTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnPlayerTouchedObject",
        "OnCheckpointTouched",
        "OnHazardTouched",
        "OnFinishTouched"
    };

    private static readonly HashSet<string> ObbyPlayerStateTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SetPlayerCheckpoint",
        "SendPlayerToCheckpoint",
        "StartPlayerTimer",
        "FinishPlayerTimer",
        "ResetPlayerRun",
        "AddPlayerCoin",
        "MarkPlayerCollectible",
        "ClearPlayerCollectibles",
        "SetPlayerNumber",
        "AddPlayerNumber",
        "SetPlayerText",
        "SetPlayerFlag",
        "PlayerHasCheckpoint",
        "PlayerReachedCheckpoint",
        "PlayerHasCollectible",
        "PlayerNumberAtLeast",
        "PlayerFlagIsTrue",
        "RunIsActive",
        "RunIsFinished",
        "PlayerCheckpointName",
        "PlayerCheckpointPosition",
        "PlayerRunTime",
        "PlayerDeathCount",
        "PlayerCoinCount",
        "PlayerRuntimeNumber",
        "PlayerRuntimeText",
        "PlayerRuntimeFlag"
    };

    private static readonly HashSet<string> ObbyObjectPositionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SetPlayerCheckpoint",
        "PlayerCheckpointPosition",
        "StartMovingPlatformLoop"
    };

    private static bool TryAppendReadableObbyConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (condition.Type.Equals("PlayerHasCheckpoint", StringComparison.OrdinalIgnoreCase))
        {
            AppendObbyDataGuard(builder, rule, condition, nodesById, indentLevel, "Player Has Checkpoint", "return false");
            builder.AppendLine($"{indent}return data.checkpointSet == true and data.checkpointName ~= nil and tostring(data.checkpointName) ~= \"\"");
            return true;
        }

        if (condition.Type.Equals("PlayerReachedCheckpoint", StringComparison.OrdinalIgnoreCase))
        {
            var checkpointName = ParameterExpression(rule, condition, nodesById, "checkpointName", "String", "");
            AppendObbyDataGuard(builder, rule, condition, nodesById, indentLevel, "Player Reached Checkpoint", "return false");
            builder.AppendLine($"{indent}local expectedCheckpoint = tostring({checkpointName.Code})");
            builder.AppendLine($"{indent}if expectedCheckpoint == \"\" and triggerContext ~= nil and triggerContext.checkpointObject ~= nil and triggerContext.checkpointObject.Name ~= nil then");
            builder.AppendLine($"{indent}    expectedCheckpoint = tostring(triggerContext.checkpointObject.Name)");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return expectedCheckpoint ~= \"\" and tostring(data.checkpointName or \"\") == expectedCheckpoint");
            return true;
        }

        if (condition.Type.Equals("PlayerHasCollectible", StringComparison.OrdinalIgnoreCase))
        {
            var collectibleId = ParameterExpression(rule, condition, nodesById, "collectibleId", "String", "Coin");
            AppendObbyDataGuard(builder, rule, condition, nodesById, indentLevel, "Player Has Collectible", "return false");
            builder.AppendLine($"{indent}local collectibleId = tostring({collectibleId.Code})");
            builder.AppendLine($"{indent}return collectibleId ~= \"\" and data.collectibles ~= nil and data.collectibles[collectibleId] == true");
            return true;
        }

        if (condition.Type.Equals("PlayerNumberAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, condition, nodesById, "key", "Score");
            var minimum = ParameterExpression(rule, condition, nodesById, "minimum", "Number", "1");
            AppendObbyDataGuard(builder, rule, condition, nodesById, indentLevel, "Player Number At Least", "return false");
            builder.AppendLine($"{indent}return (tonumber(data.numbers{key}) or 0) >= {minimum.Code}");
            return true;
        }

        if (condition.Type.Equals("PlayerFlagIsTrue", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, condition, nodesById, "key", "Flag");
            AppendObbyDataGuard(builder, rule, condition, nodesById, indentLevel, "Player Flag Is True", "return false");
            builder.AppendLine($"{indent}return data.flags{key} == true");
            return true;
        }

        if (condition.Type.Equals("RunIsActive", StringComparison.OrdinalIgnoreCase))
        {
            AppendObbyDataGuard(builder, rule, condition, nodesById, indentLevel, "Run Is Active", "return false");
            builder.AppendLine($"{indent}return data.runStart ~= nil and data.runFinished ~= true");
            return true;
        }

        if (condition.Type.Equals("RunIsFinished", StringComparison.OrdinalIgnoreCase))
        {
            AppendObbyDataGuard(builder, rule, condition, nodesById, indentLevel, "Run Is Finished", "return false");
            builder.AppendLine($"{indent}return data.runFinished == true");
            return true;
        }

        return false;
    }

    private static bool TryAppendReadableObbyActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (action.Type.Equals("SetPlayerCheckpoint", StringComparison.OrdinalIgnoreCase))
        {
            var checkpointName = ParameterExpression(rule, action, nodesById, "checkpointName", "String", "");
            AppendObbyDataGuard(builder, rule, action, nodesById, indentLevel, "Set Player Checkpoint", "return");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "checkpointObject");
            builder.AppendLine($"{indent}if checkpointObject == nil then");
            builder.AppendLine($"{indent}    print(\"Set Player Checkpoint stopped: checkpoint object was not found.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local checkpointPosition = vrsObjectPosition(checkpointObject)");
            builder.AppendLine($"{indent}if checkpointPosition == nil then");
            builder.AppendLine($"{indent}    print(\"Set Player Checkpoint stopped: checkpoint object has no position.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local checkpointName = tostring({checkpointName.Code})");
            builder.AppendLine($"{indent}if checkpointName == \"\" and checkpointObject.Name ~= nil then");
            builder.AppendLine($"{indent}    checkpointName = tostring(checkpointObject.Name)");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}data.checkpointName = checkpointName");
            builder.AppendLine($"{indent}data.checkpointObject = checkpointObject");
            builder.AppendLine($"{indent}data.checkpointPosition = checkpointPosition");
            builder.AppendLine($"{indent}data.checkpointSet = true");
            return true;
        }

        if (action.Type.Equals("SendPlayerToCheckpoint", StringComparison.OrdinalIgnoreCase))
        {
            var countDeath = ParameterExpression(rule, action, nodesById, "countDeath", "Boolean", "true");
            AppendObbyDataGuard(builder, rule, action, nodesById, indentLevel, "Send Player To Checkpoint", "return");
            builder.AppendLine($"{indent}if {countDeath.Code} then");
            builder.AppendLine($"{indent}    data.deaths = (tonumber(data.deaths) or 0) + 1");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local checkpointPosition = data.checkpointPosition");
            builder.AppendLine($"{indent}if checkpointPosition ~= nil and player.MovePosition ~= nil then");
            builder.AppendLine($"{indent}    player:MovePosition(checkpointPosition)");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if checkpointPosition ~= nil and player.Position ~= nil then");
            builder.AppendLine($"{indent}    player.Position = checkpointPosition");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if player.Respawn ~= nil then");
            builder.AppendLine($"{indent}    player:Respawn()");
            builder.AppendLine($"{indent}else");
            builder.AppendLine($"{indent}    print(\"Send Player To Checkpoint stopped: player has no checkpoint movement or Respawn method.\")");
            builder.AppendLine($"{indent}end");
            return true;
        }

        if (action.Type.Equals("RespawnPlayer", StringComparison.OrdinalIgnoreCase))
        {
            AppendObbyPlayerGuard(builder, rule, action, nodesById, indentLevel, "Respawn Player", "return");
            builder.AppendLine($"{indent}if player.Respawn ~= nil then");
            builder.AppendLine($"{indent}    player:Respawn()");
            builder.AppendLine($"{indent}else");
            builder.AppendLine($"{indent}    print(\"Respawn Player stopped: player has no Respawn method.\")");
            builder.AppendLine($"{indent}end");
            return true;
        }

        if (action.Type.Equals("KillPlayer", StringComparison.OrdinalIgnoreCase))
        {
            AppendObbyPlayerGuard(builder, rule, action, nodesById, indentLevel, "Kill Player", "return");
            builder.AppendLine($"{indent}local killedPlayer = false");
            builder.AppendLine($"{indent}if player.Kill ~= nil then");
            builder.AppendLine($"{indent}    player:Kill()");
            builder.AppendLine($"{indent}    killedPlayer = true");
            builder.AppendLine($"{indent}elseif player.Health ~= nil then");
            builder.AppendLine($"{indent}    player.Health = 0");
            builder.AppendLine($"{indent}    killedPlayer = true");
            builder.AppendLine($"{indent}else");
            builder.AppendLine($"{indent}    local character = player.Character");
            builder.AppendLine($"{indent}    local humanoid = nil");
            builder.AppendLine($"{indent}    if character ~= nil and character.Humanoid ~= nil then");
            builder.AppendLine($"{indent}        humanoid = character.Humanoid");
            builder.AppendLine($"{indent}    elseif character ~= nil and character.FindFirstChild ~= nil then");
            builder.AppendLine($"{indent}        humanoid = character:FindFirstChild(\"Humanoid\")");
            builder.AppendLine($"{indent}    end");
            builder.AppendLine($"{indent}    if humanoid ~= nil and humanoid.Health ~= nil then");
            builder.AppendLine($"{indent}        humanoid.Health = 0");
            builder.AppendLine($"{indent}        killedPlayer = true");
            builder.AppendLine($"{indent}    end");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if not killedPlayer then");
            builder.AppendLine($"{indent}    if player.Respawn ~= nil then");
            builder.AppendLine($"{indent}        player:Respawn()");
            builder.AppendLine($"{indent}    else");
            builder.AppendLine($"{indent}        print(\"Kill Player stopped: player has no Kill, Health, Humanoid, or Respawn API.\")");
            builder.AppendLine($"{indent}    end");
            builder.AppendLine($"{indent}end");
            return true;
        }

        if (action.Type.Equals("StartPlayerTimer", StringComparison.OrdinalIgnoreCase))
        {
            AppendObbyDataGuard(builder, rule, action, nodesById, indentLevel, "Start Player Timer", "return");
            builder.AppendLine($"{indent}data.runStart = vrsNow()");
            builder.AppendLine($"{indent}data.runFinish = nil");
            builder.AppendLine($"{indent}data.runFinished = false");
            return true;
        }

        if (action.Type.Equals("FinishPlayerTimer", StringComparison.OrdinalIgnoreCase))
        {
            AppendObbyDataGuard(builder, rule, action, nodesById, indentLevel, "Finish Player Timer", "return");
            builder.AppendLine($"{indent}if data.runStart == nil then");
            builder.AppendLine($"{indent}    data.runStart = vrsNow()");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}data.runFinish = vrsNow()");
            builder.AppendLine($"{indent}data.runFinished = true");
            builder.AppendLine($"{indent}data.bestTime = data.bestTime == nil and (data.runFinish - data.runStart) or math.min(data.bestTime, data.runFinish - data.runStart)");
            return true;
        }

        if (action.Type.Equals("ResetPlayerRun", StringComparison.OrdinalIgnoreCase))
        {
            AppendObbyDataGuard(builder, rule, action, nodesById, indentLevel, "Reset Player Run", "return");
            builder.AppendLine($"{indent}data.checkpointName = nil");
            builder.AppendLine($"{indent}data.checkpointObject = nil");
            builder.AppendLine($"{indent}data.checkpointPosition = nil");
            builder.AppendLine($"{indent}data.checkpointSet = false");
            builder.AppendLine($"{indent}data.runStart = nil");
            builder.AppendLine($"{indent}data.runFinish = nil");
            builder.AppendLine($"{indent}data.runFinished = false");
            builder.AppendLine($"{indent}data.deaths = 0");
            builder.AppendLine($"{indent}data.coins = 0");
            builder.AppendLine($"{indent}data.collectibles = {{}}");
            return true;
        }

        if (action.Type.Equals("AddPlayerCoin", StringComparison.OrdinalIgnoreCase))
        {
            var amount = ParameterExpression(rule, action, nodesById, "amount", "Number", "1");
            AppendObbyDataGuard(builder, rule, action, nodesById, indentLevel, "Add Player Coin", "return");
            builder.AppendLine($"{indent}data.coins = (tonumber(data.coins) or 0) + {amount.Code}");
            return true;
        }

        if (action.Type.Equals("MarkPlayerCollectible", StringComparison.OrdinalIgnoreCase))
        {
            var collectibleId = ParameterExpression(rule, action, nodesById, "collectibleId", "String", "Coin");
            AppendObbyDataGuard(builder, rule, action, nodesById, indentLevel, "Mark Player Collectible", "return");
            builder.AppendLine($"{indent}local collectibleId = tostring({collectibleId.Code})");
            builder.AppendLine($"{indent}if collectibleId == \"\" then");
            builder.AppendLine($"{indent}    print(\"Mark Player Collectible stopped: collectible id is empty.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}data.collectibles[collectibleId] = true");
            return true;
        }

        if (action.Type.Equals("ClearPlayerCollectibles", StringComparison.OrdinalIgnoreCase))
        {
            AppendObbyDataGuard(builder, rule, action, nodesById, indentLevel, "Clear Player Collectibles", "return");
            builder.AppendLine($"{indent}data.collectibles = {{}}");
            return true;
        }

        if (action.Type.Equals("SetPlayerNumber", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, action, nodesById, "key", "Score");
            var value = ParameterExpression(rule, action, nodesById, "value", "Number", "0");
            AppendObbyDataGuard(builder, rule, action, nodesById, indentLevel, "Set Player Number", "return");
            builder.AppendLine($"{indent}data.numbers{key} = {value.Code}");
            return true;
        }

        if (action.Type.Equals("AddPlayerNumber", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, action, nodesById, "key", "Score");
            var amount = ParameterExpression(rule, action, nodesById, "amount", "Number", "1");
            AppendObbyDataGuard(builder, rule, action, nodesById, indentLevel, "Add Player Number", "return");
            builder.AppendLine($"{indent}data.numbers{key} = (tonumber(data.numbers{key}) or 0) + {amount.Code}");
            return true;
        }

        if (action.Type.Equals("SetPlayerText", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, action, nodesById, "key", "Label");
            var value = ParameterExpression(rule, action, nodesById, "value", "String", "");
            AppendObbyDataGuard(builder, rule, action, nodesById, indentLevel, "Set Player Text", "return");
            builder.AppendLine($"{indent}data.texts{key} = tostring({value.Code})");
            return true;
        }

        if (action.Type.Equals("SetPlayerFlag", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, action, nodesById, "key", "Flag");
            var value = ParameterExpression(rule, action, nodesById, "value", "Boolean", "true");
            AppendObbyDataGuard(builder, rule, action, nodesById, indentLevel, "Set Player Flag", "return");
            builder.AppendLine($"{indent}data.flags{key} = {value.Code} == true");
            return true;
        }

        if (action.Type.Equals("SetObjectSpawnEnabled", StringComparison.OrdinalIgnoreCase))
        {
            var enabled = ParameterExpression(rule, action, nodesById, "enabled", "Boolean", "true");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    print(\"Set Object Spawn Enabled stopped: target was not found.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if targetObject.SpawnEnabled ~= nil then");
            builder.AppendLine($"{indent}    targetObject.SpawnEnabled = {enabled.Code}");
            builder.AppendLine($"{indent}elseif targetObject.Enabled ~= nil then");
            builder.AppendLine($"{indent}    targetObject.Enabled = {enabled.Code}");
            builder.AppendLine($"{indent}elseif targetObject.Visible ~= nil then");
            builder.AppendLine($"{indent}    targetObject.Visible = {enabled.Code}");
            builder.AppendLine($"{indent}else");
            builder.AppendLine($"{indent}    print(\"Set Object Spawn Enabled stopped: target has no SpawnEnabled, Enabled, or Visible property.\")");
            builder.AppendLine($"{indent}end");
            return true;
        }

        if (action.Type.Equals("SetPlayerCanMove", StringComparison.OrdinalIgnoreCase))
        {
            var canMove = ParameterExpression(rule, action, nodesById, "canMove", "Boolean", "true");
            AppendObbyPlayerGuard(builder, rule, action, nodesById, indentLevel, "Set Player Can Move", "return");
            builder.AppendLine($"{indent}if player.CanMove == nil then");
            builder.AppendLine($"{indent}    print(\"Set Player Can Move stopped: player does not expose CanMove.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}player.CanMove = {canMove.Code}");
            return true;
        }

        if (action.Type.Equals("MakePlayerJump", StringComparison.OrdinalIgnoreCase))
        {
            AppendObbyPlayerGuard(builder, rule, action, nodesById, indentLevel, "Make Player Jump", "return");
            builder.AppendLine($"{indent}if player.Jump ~= nil then");
            builder.AppendLine($"{indent}    player:Jump()");
            builder.AppendLine($"{indent}else");
            builder.AppendLine($"{indent}    print(\"Make Player Jump stopped: player has no Jump method.\")");
            builder.AppendLine($"{indent}end");
            return true;
        }

        if (action.Type.Equals("StartMovingPlatformLoop", StringComparison.OrdinalIgnoreCase))
        {
            AppendStartMovingPlatformLoop(builder, rule, action, plan, nodesById, indentLevel);
            return true;
        }

        return false;
    }

    private static LuauExpression? TryResolveReadableObbyPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        if (node.Type.Equals("TriggeringTouchObject", StringComparison.OrdinalIgnoreCase))
        {
            return new LuauExpression("((triggerContext ~= nil and triggerContext.touchObject) or nil)", "Any");
        }

        if (node.Type.Equals("PlayerCheckpointName", StringComparison.OrdinalIgnoreCase))
        {
            return ObbyDataExpression(rule, node, nodesById, visitedNodeIds, "String", "\"\"", "tostring(data.checkpointName or \"\")");
        }

        if (node.Type.Equals("PlayerCheckpointPosition", StringComparison.OrdinalIgnoreCase))
        {
            return ObbyDataExpression(rule, node, nodesById, visitedNodeIds, "Vector3", "makeVector3(0, 0, 0)", "data.checkpointPosition");
        }

        if (node.Type.Equals("PlayerRunTime", StringComparison.OrdinalIgnoreCase))
        {
            return ObbyDataExpression(
                rule,
                node,
                nodesById,
                visitedNodeIds,
                "Number",
                "0",
                "data.runStart == nil and 0 or math.max(0, ((data.runFinish or vrsNow()) - data.runStart))");
        }

        if (node.Type.Equals("PlayerDeathCount", StringComparison.OrdinalIgnoreCase))
        {
            return ObbyDataExpression(rule, node, nodesById, visitedNodeIds, "Number", "0", "(tonumber(data.deaths) or 0)");
        }

        if (node.Type.Equals("PlayerCoinCount", StringComparison.OrdinalIgnoreCase))
        {
            return ObbyDataExpression(rule, node, nodesById, visitedNodeIds, "Number", "0", "(tonumber(data.coins) or 0)");
        }

        if (node.Type.Equals("PlayerRuntimeNumber", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, node, nodesById, "key", "Score", visitedNodeIds);
            return ObbyDataExpression(rule, node, nodesById, visitedNodeIds, "Number", "0", $"(tonumber(data.numbers{key}) or 0)");
        }

        if (node.Type.Equals("PlayerRuntimeText", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, node, nodesById, "key", "Label", visitedNodeIds);
            return ObbyDataExpression(rule, node, nodesById, visitedNodeIds, "String", "\"\"", $"tostring(data.texts{key} or \"\")");
        }

        if (node.Type.Equals("PlayerRuntimeFlag", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, node, nodesById, "key", "Flag", visitedNodeIds);
            return ObbyDataExpression(rule, node, nodesById, visitedNodeIds, "Boolean", "false", $"data.flags{key} == true");
        }

        return null;
    }

    private static void AppendStartMovingPlatformLoop(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var x = ParameterExpression(rule, action, nodesById, "x", "Number", "0");
        var y = ParameterExpression(rule, action, nodesById, "y", "Number", "5");
        var z = ParameterExpression(rule, action, nodesById, "z", "Number", "0");
        var duration = ParameterExpression(rule, action, nodesById, "duration", "Number", "2");
        var waitAtEnds = ParameterExpression(rule, action, nodesById, "waitAtEnds", "Number", "0.5");
        builder.AppendLine($"{indent}local targetObject = {TargetExpression(plan, action)}");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"Start Moving Platform Loop stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.Position == nil then");
        builder.AppendLine($"{indent}    print(\"Start Moving Platform Loop stopped: target does not expose Position.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local offset = makeVector3({x.Code}, {y.Code}, {z.Code})");
        builder.AppendLine($"{indent}local durationSeconds = math.max(0.03, {duration.Code})");
        builder.AppendLine($"{indent}local pauseSeconds = math.max(0, {waitAtEnds.Code})");
        builder.AppendLine($"{indent}local function runPlatformLoop()");
        builder.AppendLine($"{indent}    local startPosition = vrsObjectPosition(targetObject)");
        builder.AppendLine($"{indent}    if startPosition == nil then");
        builder.AppendLine($"{indent}        print(\"Start Moving Platform Loop stopped: platform has no readable position.\")");
        builder.AppendLine($"{indent}        return");
        builder.AppendLine($"{indent}    end");
        builder.AppendLine($"{indent}    local endPosition = startPosition + offset");
        builder.AppendLine($"{indent}    while true do");
        builder.AppendLine($"{indent}        vrsRunVectorTween(function() return targetObject.Position end, function(value) targetObject.Position = value end, endPosition, durationSeconds, \"Sine\", \"InOut\")");
        builder.AppendLine($"{indent}        wait(pauseSeconds)");
        builder.AppendLine($"{indent}        vrsRunVectorTween(function() return targetObject.Position end, function(value) targetObject.Position = value end, startPosition, durationSeconds, \"Sine\", \"InOut\")");
        builder.AppendLine($"{indent}        wait(pauseSeconds)");
        builder.AppendLine($"{indent}    end");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if spawn ~= nil then");
        builder.AppendLine($"{indent}    spawn(runPlatformLoop)");
        builder.AppendLine($"{indent}else");
        builder.AppendLine($"{indent}    runPlatformLoop()");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendObbyDataGuard(
        StringBuilder builder,
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string readableName,
        string fallbackStatement)
    {
        AppendObbyPlayerGuard(builder, rule, node, nodesById, indentLevel, readableName, fallbackStatement);
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}local data = vrsPlayerData(player)");
        builder.AppendLine($"{indent}if data == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: runtime state could not be created for the player.\")");
        builder.AppendLine($"{indent}    {fallbackStatement}");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendObbyPlayerGuard(
        StringBuilder builder,
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string readableName,
        string fallbackStatement)
    {
        var indent = IndentText(indentLevel);
        var player = ObbyPlayerExpression(rule, node, nodesById);
        builder.AppendLine($"{indent}local player = {player.Code}");
        builder.AppendLine($"{indent}if player == nil or player == \"\" then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: no player was available.\")");
        builder.AppendLine($"{indent}    {fallbackStatement}");
        builder.AppendLine($"{indent}end");
    }

    private static LuauExpression ObbyDataExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string dataType,
        string fallback,
        string returnExpression)
    {
        var player = ObbyPlayerPropertyExpression(rule, node, nodesById, visitedNodeIds);
        return new LuauExpression($"(function() local player = {player.Code}; local data = vrsPlayerData(player); if data == nil then return {fallback} end; return {returnExpression} end)()", dataType);
    }

    private static LuauExpression ObbyPlayerExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById)
    {
        var incoming = FindIncomingValueConnection(rule, node.Id, "player");
        if (incoming is not null && nodesById.TryGetValue(incoming.From.NodeId, out var sourceNode))
        {
            return ResolveSourceNodeExpression(rule, sourceNode, nodesById, "Any", "", []);
        }

        var authored = node.Parameters.FirstOrDefault(parameter => parameter.Key.Equals("player", StringComparison.OrdinalIgnoreCase));
        if (authored?.Binding.SourceKind == GraphValueSourceKind.TriggeringPlayer)
        {
            return new LuauExpression("((triggerContext ~= nil and triggerContext.player) or nil)", "Any");
        }

        if (authored?.Binding.SourceKind == GraphValueSourceKind.CatalogValue)
        {
            return CatalogValueExpression(rule, authored.Binding, nodesById, "Any", "", []);
        }

        var value = authored is null ? "" : EffectiveParameterValue(authored);
        return IsTriggeringPlayerDefault(value)
            ? new LuauExpression("((triggerContext ~= nil and triggerContext.player) or nil)", "Any")
            : LiteralExpression(value, "Any", "");
    }

    private static LuauExpression ObbyPlayerPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var incoming = FindIncomingValueConnection(rule, node.Id, "player");
        if (incoming is not null && nodesById.TryGetValue(incoming.From.NodeId, out var sourceNode))
        {
            return ResolveSourceNodeExpression(rule, sourceNode, nodesById, "Any", "", visitedNodeIds);
        }

        var authored = node.Parameters.FirstOrDefault(parameter => parameter.Key.Equals("player", StringComparison.OrdinalIgnoreCase));
        if (authored?.Binding.SourceKind == GraphValueSourceKind.TriggeringPlayer)
        {
            return new LuauExpression("((triggerContext ~= nil and triggerContext.player) or nil)", "Any");
        }

        if (authored?.Binding.SourceKind == GraphValueSourceKind.CatalogValue)
        {
            return CatalogValueExpression(rule, authored.Binding, nodesById, "Any", "", visitedNodeIds);
        }

        var value = authored is null ? "" : EffectiveParameterValue(authored);
        return IsTriggeringPlayerDefault(value)
            ? new LuauExpression("((triggerContext ~= nil and triggerContext.player) or nil)", "Any")
            : LiteralExpression(value, "Any", "");
    }

    private static bool IsTriggeringPlayerDefault(string value)
    {
        return string.IsNullOrWhiteSpace(value) ||
            value.Equals("Triggering Player", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Current Player", StringComparison.OrdinalIgnoreCase);
    }

    private static string StringKeyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        string parameterKey,
        string fallback)
    {
        var key = ParameterExpression(rule, node, nodesById, parameterKey, "String", fallback);
        return IsStringLiteral(key.Code) ? $"[{key.Code}]" : $"[tostring({key.Code})]";
    }

    private static string StringKeyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        string parameterKey,
        string fallback,
        HashSet<string> visitedNodeIds)
    {
        var key = PropertyParameterExpression(rule, node, nodesById, parameterKey, "String", fallback, visitedNodeIds);
        return IsStringLiteral(key.Code) ? $"[{key.Code}]" : $"[tostring({key.Code})]";
    }
}
