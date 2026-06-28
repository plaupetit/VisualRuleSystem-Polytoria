using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static bool TryAppendReadableSharedTableActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);

        if (action.Type.Equals("SetSharedValue", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, action, nodesById, "key", "Score");
            var value = ParameterExpression(rule, action, nodesById, "value", "Any", "0");
            AppendSharedTableAvailableGuard(builder, indent, action.Label);
            builder.AppendLine($"{indent}ScriptSharedTable{key} = {value.Code}");
            return true;
        }

        if (action.Type.Equals("IncrementSharedNumber", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, action, nodesById, "key", "Score");
            var amount = ParameterExpression(rule, action, nodesById, "amount", "Number", "1");
            AppendSharedTableAvailableGuard(builder, indent, action.Label);
            builder.AppendLine($"{indent}ScriptSharedTable{key} = (tonumber(ScriptSharedTable{key}) or 0) + {amount.Code}");
            return true;
        }

        if (action.Type.Equals("AppendSharedText", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, action, nodesById, "key", "Log");
            var text = ParameterExpression(rule, action, nodesById, "text", "String", "");
            AppendSharedTableAvailableGuard(builder, indent, action.Label);
            builder.AppendLine($"{indent}ScriptSharedTable{key} = tostring(ScriptSharedTable{key} or \"\") .. tostring({text.Code})");
            return true;
        }

        if (action.Type.Equals("RemoveSharedValue", StringComparison.OrdinalIgnoreCase))
        {
            var key = ParameterExpression(rule, action, nodesById, "key", "String", "Score");
            var keyIndex = StringKeyExpression(rule, action, nodesById, "key", "Score");
            AppendSharedTableAvailableGuard(builder, indent, action.Label);
            builder.AppendLine($"{indent}if ScriptSharedTable.Remove ~= nil then");
            builder.AppendLine($"{indent}    ScriptSharedTable:Remove(tostring({key.Code}))");
            builder.AppendLine($"{indent}else");
            builder.AppendLine($"{indent}    ScriptSharedTable{keyIndex} = nil");
            builder.AppendLine($"{indent}end");
            return true;
        }

        if (action.Type.Equals("ClearSharedValues", StringComparison.OrdinalIgnoreCase))
        {
            AppendSharedTableMethodGuard(builder, indent, action.Label, "Clear");
            builder.AppendLine($"{indent}ScriptSharedTable:Clear()");
            return true;
        }

        if (action.Type.Equals("ClearSharedPrefix", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = ParameterExpression(rule, action, nodesById, "prefix", "String", "player:");
            AppendSharedTableMethodGuard(builder, indent, action.Label, "ClearPrefix");
            builder.AppendLine($"{indent}ScriptSharedTable:ClearPrefix(tostring({prefix.Code}))");
            return true;
        }

        if (action.Type.Equals("ClearSharedSuffix", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = ParameterExpression(rule, action, nodesById, "suffix", "String", ":temp");
            AppendSharedTableMethodGuard(builder, indent, action.Label, "ClearSuffix");
            builder.AppendLine($"{indent}ScriptSharedTable:ClearSuffix(tostring({suffix.Code}))");
            return true;
        }

        return false;
    }

    private static bool TryAppendReadableSharedTableConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);

        if (condition.Type.Equals("SharedValueExists", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, condition, nodesById, "key", "Score");
            builder.AppendLine($"{indent}if ScriptSharedTable == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return ScriptSharedTable{key} ~= nil");
            return true;
        }

        if (condition.Type.Equals("SharedNumberAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, condition, nodesById, "key", "Score");
            var minimum = ParameterExpression(rule, condition, nodesById, "minimum", "Number", "10");
            builder.AppendLine($"{indent}if ScriptSharedTable == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return (tonumber(ScriptSharedTable{key}) or 0) >= {minimum.Code}");
            return true;
        }

        if (condition.Type.Equals("SharedValueMissing", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, condition, nodesById, "key", "Score");
            builder.AppendLine($"{indent}if ScriptSharedTable == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return ScriptSharedTable{key} == nil");
            return true;
        }

        if (condition.Type.Equals("SharedNumberEquals", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, condition, nodesById, "key", "Score");
            var expected = ParameterExpression(rule, condition, nodesById, "expected", "Number", "10");
            builder.AppendLine($"{indent}if ScriptSharedTable == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local sharedValue = ScriptSharedTable{key}");
            builder.AppendLine($"{indent}if sharedValue == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local currentNumber = tonumber(sharedValue)");
            builder.AppendLine($"{indent}if currentNumber == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return currentNumber == {expected.Code}");
            return true;
        }

        if (condition.Type.Equals("SharedNumberAtMost", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, condition, nodesById, "key", "Score");
            var maximum = ParameterExpression(rule, condition, nodesById, "maximum", "Number", "0");
            builder.AppendLine($"{indent}if ScriptSharedTable == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local sharedValue = ScriptSharedTable{key}");
            builder.AppendLine($"{indent}if sharedValue == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local currentNumber = tonumber(sharedValue)");
            builder.AppendLine($"{indent}if currentNumber == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return currentNumber <= {maximum.Code}");
            return true;
        }

        if (condition.Type.Equals("SharedTextEquals", StringComparison.OrdinalIgnoreCase))
        {
            AppendSharedTableTextCompareCondition(builder, rule, condition, nodesById, indent, contains: false);
            return true;
        }

        if (condition.Type.Equals("SharedTextContains", StringComparison.OrdinalIgnoreCase))
        {
            AppendSharedTableTextCompareCondition(builder, rule, condition, nodesById, indent, contains: true);
            return true;
        }

        return false;
    }

    private static bool IsSharedTableWatcherTrigger(string triggerType)
        => SharedTableWatcherTriggerTypes.Contains(triggerType);

    private static readonly HashSet<string> SharedTableWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnSharedValueChanged",
        "OnSharedValueExists",
        "OnSharedValueRemoved",
        "OnSharedNumberReachedAtLeast",
        "OnSharedNumberDroppedToAtMost",
        "OnSharedTextBecame",
        "OnSharedTextContains"
    };

    private static void AppendReadableSharedTableWatcherTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var key = ParameterExpression(rule, trigger, nodesById, "key", "String", "Score");
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        AppendSharedTableAvailableGuard(builder, "    ", $"{trigger.Label} trigger");
        builder.AppendLine($"    local watchedSharedKey = tostring({key.Code})");

        if (trigger.Type.Equals("OnSharedValueChanged", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("    local function readWatchedValue()");
            builder.AppendLine("        return ScriptSharedTable[watchedSharedKey]");
            builder.AppendLine("    end");
            AppendReadableAnyChangeLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, ", sharedKey = watchedSharedKey, sharedValue = currentValue", 1);
            builder.AppendLine("end");
            return;
        }

        builder.AppendLine("    local function readMatched()");
        if (trigger.Type.Equals("OnSharedValueExists", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("        local currentValue = ScriptSharedTable[watchedSharedKey]");
            builder.AppendLine("        return currentValue ~= nil, currentValue");
        }
        else if (trigger.Type.Equals("OnSharedValueRemoved", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("        local currentValue = ScriptSharedTable[watchedSharedKey]");
            builder.AppendLine("        return currentValue == nil, currentValue");
        }
        else if (trigger.Type.Equals("OnSharedNumberReachedAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            var minimum = ParameterExpression(rule, trigger, nodesById, "minimum", "Number", "10");
            builder.AppendLine($"        local watchedLimit = tonumber({minimum.Code}) or 10");
            builder.AppendLine("        local currentValue = tonumber(ScriptSharedTable[watchedSharedKey]) or 0");
            builder.AppendLine("        return currentValue >= watchedLimit, currentValue");
        }
        else if (trigger.Type.Equals("OnSharedNumberDroppedToAtMost", StringComparison.OrdinalIgnoreCase))
        {
            var maximum = ParameterExpression(rule, trigger, nodesById, "maximum", "Number", "0");
            builder.AppendLine($"        local watchedLimit = tonumber({maximum.Code}) or 0");
            builder.AppendLine("        local currentValue = tonumber(ScriptSharedTable[watchedSharedKey]) or 0");
            builder.AppendLine("        return currentValue <= watchedLimit, currentValue");
        }
        else if (trigger.Type.Equals("OnSharedTextBecame", StringComparison.OrdinalIgnoreCase))
        {
            var text = ParameterExpression(rule, trigger, nodesById, "text", "String", "Ready");
            var caseSensitive = ParameterExpression(rule, trigger, nodesById, "caseSensitive", "Boolean", "false");
            AppendSharedTableTextWatcherBody(builder, text.Code, caseSensitive.Code, contains: false);
        }
        else if (trigger.Type.Equals("OnSharedTextContains", StringComparison.OrdinalIgnoreCase))
        {
            var search = ParameterExpression(rule, trigger, nodesById, "search", "String", "Ready");
            var caseSensitive = ParameterExpression(rule, trigger, nodesById, "caseSensitive", "Boolean", "false");
            AppendSharedTableTextWatcherBody(builder, search.Code, caseSensitive.Code, contains: true);
        }

        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, ", sharedKey = watchedSharedKey, sharedValue = currentValue", 1);
        builder.AppendLine("end");
    }

    private static LuauExpression? TryResolveReadableSharedTablePropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        if (node.Type.Equals("ReadSharedValue", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, node, nodesById, "key", "Score", visitedNodeIds);
            return new LuauExpression($"(function() if ScriptSharedTable == nil then return nil end return ScriptSharedTable{key} end)()", "Any");
        }

        if (node.Type.Equals("ReadSharedNumber", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, node, nodesById, "key", "Score", visitedNodeIds);
            var fallback = PropertyParameterExpression(rule, node, nodesById, "fallback", "Number", "0", visitedNodeIds);
            return new LuauExpression($"(function() if ScriptSharedTable == nil then return {fallback.Code} end return tonumber(ScriptSharedTable{key}) or {fallback.Code} end)()", "Number");
        }

        if (node.Type.Equals("ReadSharedText", StringComparison.OrdinalIgnoreCase))
        {
            var key = StringKeyExpression(rule, node, nodesById, "key", "Log", visitedNodeIds);
            var fallback = PropertyParameterExpression(rule, node, nodesById, "fallback", "String", "", visitedNodeIds);
            return new LuauExpression($"(function() if ScriptSharedTable == nil then return {fallback.Code} end local sharedValue = ScriptSharedTable{key}; if sharedValue == nil then return {fallback.Code} end return tostring(sharedValue) end)()", "String");
        }

        return null;
    }

    private static void AppendSharedTableTextCompareCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        string indent,
        bool contains)
    {
        var key = StringKeyExpression(rule, condition, nodesById, "key", "Log");
        var expectedKey = contains ? "search" : "text";
        var expectedFallback = contains ? "Ready" : "";
        var expected = ParameterExpression(rule, condition, nodesById, expectedKey, "String", expectedFallback);
        var caseSensitive = ParameterExpression(rule, condition, nodesById, "caseSensitive", "Boolean", "false");

        builder.AppendLine($"{indent}if ScriptSharedTable == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local sharedValue = ScriptSharedTable{key}");
        builder.AppendLine($"{indent}if sharedValue == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local currentText = tostring(sharedValue)");
        builder.AppendLine($"{indent}local expectedText = tostring({expected.Code})");
        builder.AppendLine($"{indent}if {caseSensitive.Code} ~= true then");
        builder.AppendLine($"{indent}    currentText = string.lower(currentText)");
        builder.AppendLine($"{indent}    expectedText = string.lower(expectedText)");
        builder.AppendLine($"{indent}end");
        if (contains)
        {
            builder.AppendLine($"{indent}return string.find(currentText, expectedText, 1, true) ~= nil");
        }
        else
        {
            builder.AppendLine($"{indent}return currentText == expectedText");
        }
    }

    private static void AppendSharedTableTextWatcherBody(
        StringBuilder builder,
        string expectedCode,
        string caseSensitiveCode,
        bool contains)
    {
        builder.AppendLine("        local currentRaw = ScriptSharedTable[watchedSharedKey]");
        builder.AppendLine("        if currentRaw == nil then");
        builder.AppendLine("            return false, currentRaw");
        builder.AppendLine("        end");
        builder.AppendLine("        local currentText = tostring(currentRaw)");
        builder.AppendLine($"        local expectedText = tostring({expectedCode})");
        builder.AppendLine($"        if {caseSensitiveCode} ~= true then");
        builder.AppendLine("            currentText = string.lower(currentText)");
        builder.AppendLine("            expectedText = string.lower(expectedText)");
        builder.AppendLine("        end");
        if (contains)
        {
            builder.AppendLine("        return string.find(currentText, expectedText, 1, true) ~= nil, currentText");
        }
        else
        {
            builder.AppendLine("        return currentText == expectedText, currentText");
        }
    }

    private static void AppendSharedTableAvailableGuard(StringBuilder builder, string indent, string label)
    {
        builder.AppendLine($"{indent}if ScriptSharedTable == nil then");
        builder.AppendLine($"{indent}    print(\"{label} stopped: ScriptSharedTable is not available.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendSharedTableMethodGuard(StringBuilder builder, string indent, string label, string methodName)
    {
        AppendSharedTableAvailableGuard(builder, indent, label);
        builder.AppendLine($"{indent}if ScriptSharedTable.{methodName} == nil then");
        builder.AppendLine($"{indent}    print(\"{label} stopped: ScriptSharedTable:{methodName} is not available.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }
}
