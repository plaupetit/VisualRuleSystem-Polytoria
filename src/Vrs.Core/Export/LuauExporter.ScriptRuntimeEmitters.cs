using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static readonly HashSet<string> ScriptRuntimeWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnScriptEnabled",
        "OnScriptDisabled",
        "OnScriptEnabledChanged",
        "OnScriptCallAvailable",
        "OnScriptCallAsyncAvailable",
        "OnScriptTargetMissing"
    };

    private static bool IsScriptRuntimeWatcherTrigger(string triggerType)
        => ScriptRuntimeWatcherTriggerTypes.Contains(triggerType);

    private static bool TryAppendReadableScriptRuntimeActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (action.Type.Equals("SetScriptEnabled", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "IsEnabled", "enabled", "Boolean", "true", action.Label);
            return true;
        }

        if (action.Type.Equals("EnableScript", StringComparison.OrdinalIgnoreCase))
        {
            AppendFixedObjectPropertyAction(builder, plan, action, indentLevel, "IsEnabled", "true", action.Label);
            return true;
        }

        if (action.Type.Equals("DisableScript", StringComparison.OrdinalIgnoreCase))
        {
            AppendFixedObjectPropertyAction(builder, plan, action, indentLevel, "IsEnabled", "false", action.Label);
            return true;
        }

        if (action.Type.Equals("ToggleScriptEnabled", StringComparison.OrdinalIgnoreCase))
        {
            AppendToggleObjectBooleanPropertyAction(builder, plan, action, indentLevel, "IsEnabled", action.Label);
            return true;
        }

        if (action.Type.Equals("CallScriptFunction", StringComparison.OrdinalIgnoreCase))
        {
            AppendScriptCallAction(builder, rule, action, plan, nodesById, indentLevel, "Call");
            return true;
        }

        if (action.Type.Equals("CallScriptFunctionAsync", StringComparison.OrdinalIgnoreCase))
        {
            AppendScriptCallAction(builder, rule, action, plan, nodesById, indentLevel, "CallAsync");
            return true;
        }

        return false;
    }

    private static bool TryAppendReadableScriptRuntimeConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);

        if (condition.Type.Equals("ScriptIsEnabled", StringComparison.OrdinalIgnoreCase))
        {
            AppendScriptBooleanPropertyCondition(builder, condition, plan, indentLevel, "IsEnabled", "true");
            return true;
        }

        if (condition.Type.Equals("ScriptIsDisabled", StringComparison.OrdinalIgnoreCase))
        {
            AppendScriptBooleanPropertyCondition(builder, condition, plan, indentLevel, "IsEnabled", "false");
            return true;
        }

        if (condition.Type.Equals("ScriptCanCallFunction", StringComparison.OrdinalIgnoreCase))
        {
            AppendScriptMethodAvailableCondition(builder, condition, plan, indentLevel, "Call");
            return true;
        }

        if (condition.Type.Equals("ScriptCanCallAsyncFunction", StringComparison.OrdinalIgnoreCase))
        {
            AppendScriptMethodAvailableCondition(builder, condition, plan, indentLevel, "CallAsync");
            return true;
        }

        if (condition.Type.Equals("ScriptTargetExists", StringComparison.OrdinalIgnoreCase))
        {
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "scriptObject");
            builder.AppendLine($"{indent}return scriptObject ~= nil and not {MissingInstancePredicate("scriptObject")}");
            return true;
        }

        if (condition.Type.Equals("ScriptTargetMissing", StringComparison.OrdinalIgnoreCase))
        {
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "scriptObject");
            builder.AppendLine($"{indent}return scriptObject == nil or {MissingInstancePredicate("scriptObject")}");
            return true;
        }

        if (condition.Type.Equals("ObjectIsMissingInstance", StringComparison.OrdinalIgnoreCase))
        {
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}return {MissingInstancePredicate("targetObject")}");
            return true;
        }

        return false;
    }

    private static void AppendReadableScriptRuntimeWatcherTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var definition = ScriptRuntimeWatcherDefinition.For(trigger.Type);
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");

        builder.AppendLine($"local function {functionName}()");
        if (definition.WatchesMissingTarget)
        {
            AppendScriptRuntimeMissingTargetWatcher(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code);
            builder.AppendLine("end");
            return;
        }

        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine("    local function readWatchedValue()");
        builder.AppendLine("        if triggerObject == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        if (definition.PropertyName is not null)
        {
            builder.AppendLine($"        if triggerObject.{definition.PropertyName} == nil then");
            builder.AppendLine("            return nil");
            builder.AppendLine("        end");
            builder.AppendLine($"        return triggerObject.{definition.PropertyName} == true");
        }
        else
        {
            builder.AppendLine($"        return triggerObject.{definition.MethodName} ~= nil");
        }

        builder.AppendLine("    end");
        if (definition.TriggersOnAnyChange)
        {
            AppendReadableAnyChangeLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, $", {definition.ContextField} = currentValue, scriptObject = triggerObject", 1);
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
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, $", {definition.ContextField} = currentValue, scriptObject = triggerObject", 1);
        builder.AppendLine("end");
    }

    private static LuauExpression? TryResolveReadableScriptRuntimePropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        if (node.Type.Equals("ScriptEnabledValue", StringComparison.OrdinalIgnoreCase))
        {
            return ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "IsEnabled", "Boolean", "Script Enabled", "return targetObject.IsEnabled == true");
        }

        if (node.Type.Equals("ObjectIsMissingInstanceValue", StringComparison.OrdinalIgnoreCase))
        {
            var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Target", visitedNodeIds);
            return new LuauExpression($"(function() local targetObject = resolveTarget(triggerObject, {target.Code}); return {MissingInstancePredicate("targetObject")} end)()", "Boolean");
        }

        return null;
    }

    private static void AppendScriptCallAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string methodName)
    {
        var indent = IndentText(indentLevel);
        var functionName = ParameterExpression(rule, action, nodesById, "functionName", "String", "Run");
        var argument = ParameterExpression(rule, action, nodesById, "argument", "Any", "");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "scriptObject");
        builder.AppendLine($"{indent}if scriptObject == nil then");
        builder.AppendLine($"{indent}    print(\"{action.Label} stopped: target script was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if scriptObject.{methodName} == nil then");
        builder.AppendLine($"{indent}    print(\"{action.Label} stopped: target script does not expose {methodName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}scriptObject:{methodName}(tostring({functionName.Code}), {argument.Code})");
    }

    private static void AppendScriptBooleanPropertyCondition(
        StringBuilder builder,
        RuleNode condition,
        ReadableExportPlan plan,
        int indentLevel,
        string propertyName,
        string expectedValue)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "scriptObject");
        builder.AppendLine($"{indent}if scriptObject == nil or scriptObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return scriptObject.{propertyName} == {expectedValue}");
    }

    private static void AppendScriptMethodAvailableCondition(
        StringBuilder builder,
        RuleNode condition,
        ReadableExportPlan plan,
        int indentLevel,
        string methodName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "scriptObject");
        builder.AppendLine($"{indent}if scriptObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return scriptObject.{methodName} ~= nil");
    }

    private static void AppendScriptRuntimeMissingTargetWatcher(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string intervalCode)
    {
        builder.AppendLine("    local scriptParent = script.Parent");
        builder.AppendLine("    local triggerObject = nil");
        builder.AppendLine("    local function readMatched()");
        builder.AppendLine($"        triggerObject = {ReadableTriggerTargetExpression(plan, trigger, "scriptParent")}");
        builder.AppendLine($"        local targetMissing = triggerObject == nil or {MissingInstancePredicate("triggerObject")}");
        builder.AppendLine("        return targetMissing, triggerObject");
        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, intervalCode, ", scriptObject = currentValue, scriptTargetMissing = currentMatched", 1);
    }

    private static string MissingInstancePredicate(string variableName)
    {
        return $"({variableName} ~= nil and (({variableName}.ClassName == \"MissingInstance\") or ({variableName}.IsA ~= nil and {variableName}:IsA(\"MissingInstance\"))))";
    }

    private sealed record ScriptRuntimeWatcherDefinition(
        string? PropertyName,
        string? MethodName,
        string ContextField,
        string MatchExpression,
        bool TriggersOnAnyChange = false,
        bool WatchesMissingTarget = false)
    {
        public static ScriptRuntimeWatcherDefinition For(string triggerType)
        {
            return triggerType switch
            {
                "OnScriptEnabled" => Property("IsEnabled", "scriptEnabled", "currentValue == true"),
                "OnScriptDisabled" => Property("IsEnabled", "scriptEnabled", "currentValue == false"),
                "OnScriptEnabledChanged" => Property("IsEnabled", "scriptEnabled", "false", TriggersOnAnyChange: true),
                "OnScriptCallAvailable" => Method("Call", "scriptCanCall"),
                "OnScriptCallAsyncAvailable" => Method("CallAsync", "scriptCanCallAsync"),
                "OnScriptTargetMissing" => new(null, null, "scriptTargetMissing", "currentValue == true", WatchesMissingTarget: true),
                _ => throw new ArgumentOutOfRangeException(nameof(triggerType), triggerType, "Unknown script runtime watcher trigger.")
            };
        }

        private static ScriptRuntimeWatcherDefinition Property(
            string propertyName,
            string contextField,
            string matchExpression,
            bool TriggersOnAnyChange = false)
            => new(propertyName, null, contextField, matchExpression, TriggersOnAnyChange);

        private static ScriptRuntimeWatcherDefinition Method(string methodName, string contextField)
            => new(null, methodName, contextField, "currentValue == true");
    }
}
