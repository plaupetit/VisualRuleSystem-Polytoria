using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static bool IsRunLuauNode(RuleNode node)
        => IsRunLuauType(node.Type);

    private static bool IsRunLuauType(string type)
        => type.Equals("RunLuauTrigger", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("RunLuauAction", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("RunLuauCondition", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("RunLuauProperty", StringComparison.OrdinalIgnoreCase);

    private static void AppendRunLuauTriggerBlock(
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
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine("    local function fire(customContext)");
        builder.AppendLine("        local triggerContext = { object = triggerObject }");
        builder.AppendLine("        if type(customContext) == \"table\" then");
        builder.AppendLine("            for key, value in pairs(customContext) do");
        builder.AppendLine("                triggerContext[key] = value");
        builder.AppendLine("            end");
        builder.AppendLine("        elseif customContext ~= nil then");
        builder.AppendLine("            triggerContext.value = customContext");
        builder.AppendLine("        end");
        builder.AppendLine("        local flowTriggerObject = triggerContext.object or triggerObject");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "flowTriggerObject", "triggerContext", 2, 0);
        if (!emitted)
        {
            builder.AppendLine("        print(\"Run Code Trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("    end");
        builder.AppendLine("    local ok, failure = pcall(function()");
        AppendRunLuauSource(builder, trigger, 2, "print(\"Run Code Trigger skipped: no script code.\")");
        builder.AppendLine("    end)");
        builder.AppendLine("    if not ok then");
        builder.AppendLine("        print(\"Run Code Trigger failed: \" .. tostring(failure))");
        builder.AppendLine("    end");
        builder.AppendLine("end");
    }

    private static void AppendRunLuauActionBody(StringBuilder builder, RuleNode action, int indentLevel)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}local ok, failure = pcall(function()");
        AppendRunLuauSource(builder, action, indentLevel + 1, "print(\"Run Code Action skipped: no script code.\")");
        builder.AppendLine($"{indent}end)");
        builder.AppendLine($"{indent}if not ok then");
        builder.AppendLine($"{indent}    print(\"Run Code Action failed: \" .. tostring(failure))");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendRunLuauConditionBody(StringBuilder builder, RuleNode condition, int indentLevel)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}local ok, result = pcall(function()");
        AppendRunLuauSource(builder, condition, indentLevel + 1, "return false");
        builder.AppendLine($"{indent}end)");
        builder.AppendLine($"{indent}if not ok then");
        builder.AppendLine($"{indent}    print(\"Code Condition failed: \" .. tostring(result))");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if type(result) ~= \"boolean\" then");
        builder.AppendLine($"{indent}    print(\"Code Condition did not return true or false; returning false.\")");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return result");
    }

    private static LuauExpression RunLuauPropertyExpression(RuleNode node, string expectedDataType, string fallback)
    {
        var resultType = RunLuauParameterValue(node, "resultType");
        var effectiveDataType = string.IsNullOrWhiteSpace(resultType) || resultType.Equals("Any", StringComparison.OrdinalIgnoreCase)
            ? expectedDataType
            : resultType;
        var normalizedDataType = NormalizeExpressionDataType(effectiveDataType);
        var fallbackCode = RunLuauFallbackExpression(normalizedDataType, fallback);
        var code = RunLuauCode(node);
        if (string.IsNullOrWhiteSpace(code))
        {
            return new LuauExpression(fallbackCode, normalizedDataType);
        }

        var expression = new StringBuilder();
        expression.AppendLine("(function()");
        expression.AppendLine("    local ok, result = pcall(function()");
        expression.Append(IndentRunLuauSource(code, 2));
        expression.AppendLine("    end)");
        expression.AppendLine("    if not ok then");
        expression.AppendLine("        print(\"Code Value failed: \" .. tostring(result))");
        expression.AppendLine($"        return {fallbackCode}");
        expression.AppendLine("    end");
        expression.AppendLine("    if result == nil then");
        expression.AppendLine($"        return {fallbackCode}");
        expression.AppendLine("    end");
        expression.AppendLine("    return result");
        expression.Append("end)()");
        return new LuauExpression(expression.ToString(), normalizedDataType);
    }

    private static void AppendRunLuauSource(StringBuilder builder, RuleNode node, int indentLevel, string emptyFallbackLine)
    {
        var code = RunLuauCode(node);
        if (string.IsNullOrWhiteSpace(code))
        {
            builder.AppendLine($"{IndentText(indentLevel)}{emptyFallbackLine}");
            return;
        }

        builder.Append(IndentRunLuauSource(code, indentLevel));
    }

    private static string IndentRunLuauSource(string code, int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var builder = new StringBuilder();
        var lines = code.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        foreach (var line in lines)
        {
            builder.AppendLine(string.IsNullOrWhiteSpace(line) ? "" : indent + line);
        }

        return builder.ToString();
    }

    private static string RunLuauCode(RuleNode node)
        => RunLuauParameterValue(node, "code");

    private static string RunLuauParameterValue(RuleNode node, string key)
    {
        var parameter = node.Parameters.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        return parameter is null ? "" : EffectiveParameterValue(parameter);
    }

    private static string RunLuauFallbackExpression(string dataType, string fallback)
    {
        var normalized = NormalizeExpressionDataType(dataType);
        return normalized switch
        {
            "Number" => NumericLiteral(string.IsNullOrWhiteSpace(fallback) ? "0" : fallback, "0"),
            "Boolean" => BooleanValue(string.IsNullOrWhiteSpace(fallback) ? "false" : fallback, fallback: false) ? "true" : "false",
            "String" => LuauStringLiteral(fallback),
            _ => "nil"
        };
    }
}
