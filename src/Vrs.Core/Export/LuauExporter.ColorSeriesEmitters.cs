using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static LuauExpression? TryResolveReadableColorSeriesPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        if (node.Type.Equals("ColorSeriesFromColors", StringComparison.OrdinalIgnoreCase))
        {
            var min = PropertyParameterExpression(rule, node, nodesById, "min", "Color", "1,1,1", visitedNodeIds);
            var max = PropertyParameterExpression(rule, node, nodesById, "max", "Color", "0,0,0", visitedNodeIds);
            return new LuauExpression($"vrsColorSeriesFromColors({min.Code}, {max.Code})", "ColorSeries");
        }

        if (node.Type.Equals("ColorFromColorSeries", StringComparison.OrdinalIgnoreCase))
        {
            var series = PropertyParameterExpression(rule, node, nodesById, "series", "ColorSeries", "", visitedNodeIds);
            var amount = PropertyParameterExpression(rule, node, nodesById, "amount", "Number", "0.5", visitedNodeIds);
            return new LuauExpression($"vrsColorSeriesLerp({series.Code}, {amount.Code})", "Color");
        }

        if (node.Type.Equals("ColorSeriesPointCount", StringComparison.OrdinalIgnoreCase))
        {
            var series = PropertyParameterExpression(rule, node, nodesById, "series", "ColorSeries", "", visitedNodeIds);
            return new LuauExpression($"vrsColorSeriesPointCount({series.Code})", "Number");
        }

        if (node.Type.Equals("ColorSeriesPointColor", StringComparison.OrdinalIgnoreCase))
        {
            var series = PropertyParameterExpression(rule, node, nodesById, "series", "ColorSeries", "", visitedNodeIds);
            var point = PropertyParameterExpression(rule, node, nodesById, "point", "Number", "1", visitedNodeIds);
            return new LuauExpression($"vrsColorSeriesColorAt({series.Code}, {point.Code})", "Color");
        }

        if (node.Type.Equals("ColorSeriesPointOffset", StringComparison.OrdinalIgnoreCase))
        {
            var series = PropertyParameterExpression(rule, node, nodesById, "series", "ColorSeries", "", visitedNodeIds);
            var point = PropertyParameterExpression(rule, node, nodesById, "point", "Number", "1", visitedNodeIds);
            return new LuauExpression($"vrsColorSeriesOffsetAt({series.Code}, {point.Code})", "Number");
        }

        return null;
    }
}
