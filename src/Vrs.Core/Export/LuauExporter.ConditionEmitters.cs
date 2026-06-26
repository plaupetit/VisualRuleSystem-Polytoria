using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private sealed record ConditionPredicate(string Expression, string VsrComment = "", string RuntimeLog = "");

    private static ConditionPredicate BuildConditionPredicate(
        Rule rule,
        RuleNode condition,
        IReadOnlyDictionary<string, RuleNode> nodesById)
    {
        var parameterValues = BuildEffectiveParameterValues(rule, condition, nodesById);
        if (condition.Type.Equals("NumberCompare", StringComparison.OrdinalIgnoreCase))
        {
            return BuildNumberComparePredicate(condition, parameterValues);
        }

        if (condition.Type.Equals("ValueEquals", StringComparison.OrdinalIgnoreCase))
        {
            return BuildValueEqualsPredicate(condition, parameterValues);
        }

        if (condition.Type.Equals("ObjectExists", StringComparison.OrdinalIgnoreCase))
        {
            return BuildObjectExistsPredicate(condition, parameterValues);
        }

        return new ConditionPredicate(
            "false",
            "Unsupported condition exporter; fallback false.");
    }

    private static ConditionPredicate BuildNumberComparePredicate(RuleNode condition, IReadOnlyDictionary<string, string> parameterValues)
    {
        var rawOperator = ParameterValue(condition, parameterValues, "operator").Trim();
        var allowedOperators = new HashSet<string>(StringComparer.Ordinal) { "==", "~=", "<", "<=", ">", ">=" };
        if (!allowedOperators.Contains(rawOperator))
        {
            return new ConditionPredicate(
                "false",
                "Number Compare operator is invalid; fallback false.",
                $"Condition {condition.Label} has invalid operator {rawOperator}; fallback false.");
        }

        var left = NumericLiteral(ParameterValue(condition, parameterValues, "left"), "0");
        var right = NumericLiteral(ParameterValue(condition, parameterValues, "right"), "0");
        return new ConditionPredicate($"{left} {rawOperator} {right}");
    }

    private static ConditionPredicate BuildValueEqualsPredicate(RuleNode condition, IReadOnlyDictionary<string, string> parameterValues)
    {
        var left = LuauStringLiteral(ParameterValue(condition, parameterValues, "left"));
        var right = LuauStringLiteral(ParameterValue(condition, parameterValues, "right"));
        return new ConditionPredicate($"tostring({left}) == tostring({right})");
    }

    private static ConditionPredicate BuildObjectExistsPredicate(RuleNode condition, IReadOnlyDictionary<string, string> parameterValues)
    {
        var targetName = ParameterValue(condition, parameterValues, "target");
        if (string.IsNullOrWhiteSpace(targetName))
        {
            targetName = "Self";
        }

        return new ConditionPredicate($"vrsResolveTarget(context, {LuauStringLiteral(targetName)}) ~= nil");
    }
}
