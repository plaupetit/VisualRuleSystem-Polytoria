using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static bool TryAppendReadableVector2ConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (!condition.Type.Equals("Vector2DistanceAtMost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indent = IndentText(indentLevel);
        var first = ParameterExpression(rule, condition, nodesById, "first", "Vector2", "0,0");
        var second = ParameterExpression(rule, condition, nodesById, "second", "Vector2", "0,0");
        var maximum = ParameterExpression(rule, condition, nodesById, "maximum", "Number", "1");
        builder.AppendLine($"{indent}return vrsVector2Distance({first.Code}, {second.Code}) <= {maximum.Code}");
        return true;
    }

    private static LuauExpression? TryResolveReadableVector2PropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        if (node.Type.Equals("Vector2FromXY", StringComparison.OrdinalIgnoreCase))
        {
            var x = PropertyParameterExpression(rule, node, nodesById, "x", "Number", "0", visitedNodeIds);
            var y = PropertyParameterExpression(rule, node, nodesById, "y", "Number", "0", visitedNodeIds);
            return new LuauExpression($"makeVector2({x.Code}, {y.Code})", "Vector2");
        }

        if (node.Type.Equals("Vector2X", StringComparison.OrdinalIgnoreCase))
        {
            var vector = PropertyParameterExpression(rule, node, nodesById, "vector", "Vector2", "0,0", visitedNodeIds);
            return new LuauExpression($"vrsVector2Axis({vector.Code}, \"X\", \"x\", 0)", "Number");
        }

        if (node.Type.Equals("Vector2Y", StringComparison.OrdinalIgnoreCase))
        {
            var vector = PropertyParameterExpression(rule, node, nodesById, "vector", "Vector2", "0,0", visitedNodeIds);
            return new LuauExpression($"vrsVector2Axis({vector.Code}, \"Y\", \"y\", 0)", "Number");
        }

        if (node.Type.Equals("Vector2Magnitude", StringComparison.OrdinalIgnoreCase))
        {
            var vector = PropertyParameterExpression(rule, node, nodesById, "vector", "Vector2", "0,0", visitedNodeIds);
            return new LuauExpression($"vrsVector2Magnitude({vector.Code})", "Number");
        }

        if (node.Type.Equals("Vector2Normalized", StringComparison.OrdinalIgnoreCase))
        {
            var vector = PropertyParameterExpression(rule, node, nodesById, "vector", "Vector2", "0,0", visitedNodeIds);
            return new LuauExpression($"vrsVector2Normalized({vector.Code})", "Vector2");
        }

        if (node.Type.Equals("Vector2Distance", StringComparison.OrdinalIgnoreCase))
        {
            var first = PropertyParameterExpression(rule, node, nodesById, "first", "Vector2", "0,0", visitedNodeIds);
            var second = PropertyParameterExpression(rule, node, nodesById, "second", "Vector2", "0,0", visitedNodeIds);
            return new LuauExpression($"vrsVector2Distance({first.Code}, {second.Code})", "Number");
        }

        if (node.Type.Equals("Vector2Lerp", StringComparison.OrdinalIgnoreCase))
        {
            var from = PropertyParameterExpression(rule, node, nodesById, "from", "Vector2", "0,0", visitedNodeIds);
            var to = PropertyParameterExpression(rule, node, nodesById, "to", "Vector2", "1,1", visitedNodeIds);
            var amount = PropertyParameterExpression(rule, node, nodesById, "amount", "Number", "0.5", visitedNodeIds);
            return new LuauExpression($"vrsVector2Lerp({from.Code}, {to.Code}, {amount.Code})", "Vector2");
        }

        return null;
    }
}
