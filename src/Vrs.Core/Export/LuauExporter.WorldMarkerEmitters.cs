using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static readonly HashSet<string> WorldMarkerWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnMarkerLengthReached",
        "OnMarkerAppearsOnTopEnabled",
        "OnMarkerVisibleInDevEnabled",
        "OnTrussClimbSpeedReached",
        "OnEntityStartedCastingShadows",
        "OnEntityBecameSpawn",
        "OnEntityColorChanged"
    };

    private static bool IsWorldMarkerWatcherTrigger(string triggerType)
        => WorldMarkerWatcherTriggerTypes.Contains(triggerType);

    private static bool TryAppendReadableWorldMarkerActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (action.Type.Equals("SetMarkerLength", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Length", "length", "Number", "10", "Set Marker Length");
            return true;
        }

        if (action.Type.Equals("SetMarkerAppearsOnTop", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "AppearOnTop", "enabled", "Boolean", "true", "Set Marker On Top");
            return true;
        }

        if (action.Type.Equals("SetMarkerVisibleInDev", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "VisibleInDev", "enabled", "Boolean", "true", "Set Marker Visible In Dev");
            return true;
        }

        if (action.Type.Equals("SetTrussClimbSpeed", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "ClimbSpeed", "speed", "Number", "12", "Set Truss Climb Speed");
            return true;
        }

        if (action.Type.Equals("SetEntityCastsShadows", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "CastShadows", "enabled", "Boolean", "true", "Set Entity Shadows");
            return true;
        }

        if (action.Type.Equals("SetEntityIsSpawn", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "IsSpawn", "enabled", "Boolean", "true", "Set Entity Spawn");
            return true;
        }

        if (action.Type.Equals("SetEntityColor", StringComparison.OrdinalIgnoreCase))
        {
            AppendSetEntityColorAction(builder, rule, action, plan, nodesById, indentLevel);
            return true;
        }

        return false;
    }

    private static bool TryAppendReadableWorldMarkerConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (condition.Type.Equals("MarkerLengthAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            AppendWorldMarkerNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "Length", "length", ">=", "10");
            return true;
        }

        if (condition.Type.Equals("MarkerAppearsOnTop", StringComparison.OrdinalIgnoreCase))
        {
            AppendWorldMarkerBooleanPropertyCondition(builder, plan, condition, indentLevel, "AppearOnTop", true);
            return true;
        }

        if (condition.Type.Equals("MarkerVisibleInDev", StringComparison.OrdinalIgnoreCase))
        {
            AppendWorldMarkerBooleanPropertyCondition(builder, plan, condition, indentLevel, "VisibleInDev", true);
            return true;
        }

        if (condition.Type.Equals("TrussClimbSpeedAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            AppendWorldMarkerNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "ClimbSpeed", "speed", ">=", "12");
            return true;
        }

        if (condition.Type.Equals("EntityCastsShadows", StringComparison.OrdinalIgnoreCase))
        {
            AppendWorldMarkerBooleanPropertyCondition(builder, plan, condition, indentLevel, "CastShadows", true);
            return true;
        }

        if (condition.Type.Equals("EntityIsSpawn", StringComparison.OrdinalIgnoreCase))
        {
            AppendWorldMarkerBooleanPropertyCondition(builder, plan, condition, indentLevel, "IsSpawn", true);
            return true;
        }

        if (condition.Type.Equals("EntityColorIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendWorldMarkerColorCondition(builder, rule, condition, plan, nodesById, indentLevel);
            return true;
        }

        return false;
    }

    private static void AppendReadableWorldMarkerWatcherTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var definition = WorldMarkerWatcherDefinition.For(trigger.Type);
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");
        var threshold = definition.ParameterKey is null
            ? null
            : ParameterExpression(rule, trigger, nodesById, definition.ParameterKey, "Number", definition.FallbackLimit);

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        if (threshold is not null)
        {
            builder.AppendLine($"    local watchedLimit = tonumber({threshold.Code}) or {definition.FallbackLimit}");
        }

        builder.AppendLine("    local function readWatchedValue()");
        builder.AppendLine("        if triggerObject == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        builder.AppendLine($"        if triggerObject.{definition.PropertyName} == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        switch (definition.ValueKind)
        {
            case WorldMarkerWatcherValueKind.Boolean:
                builder.AppendLine($"        return triggerObject.{definition.PropertyName} == true");
                break;
            case WorldMarkerWatcherValueKind.Color:
                builder.AppendLine($"        return triggerObject.{definition.PropertyName}");
                break;
            default:
                builder.AppendLine($"        return tonumber(triggerObject.{definition.PropertyName}) or {definition.FallbackCurrent}");
                break;
        }

        builder.AppendLine("    end");
        if (definition.TriggersOnAnyChange)
        {
            AppendReadableAnyChangeLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, $", {definition.ContextField} = currentValue", 1);
            builder.AppendLine("end");
            return;
        }

        builder.AppendLine("    local function readMatched()");
        builder.AppendLine("        local currentValue = readWatchedValue()");
        builder.AppendLine("        if currentValue == nil then");
        builder.AppendLine("            return false, nil");
        builder.AppendLine("        end");
        builder.AppendLine($"        return {definition.MatchExpression}, currentValue");
        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, $", {definition.ContextField} = currentValue", 1);
        builder.AppendLine("end");
    }

    private static LuauExpression? TryResolveReadableWorldMarkerPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "MarkerLengthValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Length", "Number", "Marker Length", "return tonumber(targetObject.Length) or 0"),
            "MarkerAppearsOnTopValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "AppearOnTop", "Boolean", "Marker On Top", "return targetObject.AppearOnTop == true"),
            "MarkerVisibleInDevValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "VisibleInDev", "Boolean", "Marker Visible In Dev", "return targetObject.VisibleInDev == true"),
            "TrussClimbSpeedValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "ClimbSpeed", "Number", "Truss Climb Speed", "return tonumber(targetObject.ClimbSpeed) or 0"),
            "EntityCastsShadowsValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "CastShadows", "Boolean", "Entity Shadows", "return targetObject.CastShadows == true"),
            "EntityIsSpawnValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "IsSpawn", "Boolean", "Entity Is Spawn", "return targetObject.IsSpawn == true"),
            "EntityColorValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Color", "Color", "Entity Color", "return targetObject.Color"),
            _ => null
        };
    }

    private static void AppendSetEntityColorAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var color = ColorExpression(rule, action, nodesById);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"Set Entity Color stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.Color == nil then");
        builder.AppendLine($"{indent}    print(\"Set Entity Color stopped: target does not expose Color.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}targetObject.Color = {color.Code}");
    }

    private static void AppendWorldMarkerBooleanPropertyCondition(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode condition,
        int indentLevel,
        string propertyName,
        bool expected)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return (targetObject.{propertyName} == true) == {expected.ToString().ToLowerInvariant()}");
    }

    private static void AppendWorldMarkerNumberPropertyCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string parameterKey,
        string operatorText,
        string fallback)
    {
        var indent = IndentText(indentLevel);
        var threshold = ParameterExpression(rule, condition, nodesById, parameterKey, "Number", fallback);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local currentNumber = tonumber(targetObject.{propertyName}) or 0");
        builder.AppendLine($"{indent}local expectedNumber = tonumber({threshold.Code}) or 0");
        builder.AppendLine($"{indent}return currentNumber {operatorText} expectedNumber");
    }

    private static void AppendWorldMarkerColorCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var color = ColorExpression(rule, condition, nodesById);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.Color == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local expectedColor = {color.Code}");
        builder.AppendLine($"{indent}return targetObject.Color == expectedColor");
    }

    private enum WorldMarkerWatcherValueKind
    {
        Boolean,
        Number,
        Color
    }

    private sealed record WorldMarkerWatcherDefinition(
        string PropertyName,
        WorldMarkerWatcherValueKind ValueKind,
        string ContextField,
        string MatchExpression,
        bool TriggersOnAnyChange = false,
        string? ParameterKey = null,
        string FallbackLimit = "0",
        string FallbackCurrent = "0")
    {
        public static WorldMarkerWatcherDefinition For(string triggerType)
        {
            return triggerType switch
            {
                "OnMarkerLengthReached" => NumberThreshold("Length", "length", "10", "0", ">=", "markerLength"),
                "OnMarkerAppearsOnTopEnabled" => Boolean("AppearOnTop", true, "markerOnTop"),
                "OnMarkerVisibleInDevEnabled" => Boolean("VisibleInDev", true, "markerVisibleInDev"),
                "OnTrussClimbSpeedReached" => NumberThreshold("ClimbSpeed", "speed", "12", "0", ">=", "trussClimbSpeed"),
                "OnEntityStartedCastingShadows" => Boolean("CastShadows", true, "castsShadows"),
                "OnEntityBecameSpawn" => Boolean("IsSpawn", true, "isSpawn"),
                "OnEntityColorChanged" => ChangedColor("Color", "entityColor"),
                _ => throw new ArgumentOutOfRangeException(nameof(triggerType), triggerType, "Unknown world marker watcher trigger.")
            };
        }

        private static WorldMarkerWatcherDefinition Boolean(string propertyName, bool expected, string contextField)
        {
            return new WorldMarkerWatcherDefinition(
                propertyName,
                WorldMarkerWatcherValueKind.Boolean,
                contextField,
                expected ? "currentValue == true" : "currentValue == false");
        }

        private static WorldMarkerWatcherDefinition ChangedColor(string propertyName, string contextField)
        {
            return new WorldMarkerWatcherDefinition(propertyName, WorldMarkerWatcherValueKind.Color, contextField, "false", TriggersOnAnyChange: true);
        }

        private static WorldMarkerWatcherDefinition NumberThreshold(
            string propertyName,
            string parameterKey,
            string fallbackLimit,
            string fallbackCurrent,
            string comparison,
            string contextField)
        {
            return new WorldMarkerWatcherDefinition(
                propertyName,
                WorldMarkerWatcherValueKind.Number,
                contextField,
                $"currentValue {comparison} watchedLimit",
                ParameterKey: parameterKey,
                FallbackLimit: fallbackLimit,
                FallbackCurrent: fallbackCurrent);
        }
    }
}
