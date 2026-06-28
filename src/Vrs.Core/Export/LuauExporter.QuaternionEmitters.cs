using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static bool TryAppendReadableQuaternionConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (!condition.Type.Equals("QuaternionAngleAtMost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indent = IndentText(indentLevel);
        var first = ParameterExpression(rule, condition, nodesById, "first", "Quaternion", "0,0,0,1");
        var second = ParameterExpression(rule, condition, nodesById, "second", "Quaternion", "0,0,0,1");
        var maximum = ParameterExpression(rule, condition, nodesById, "maximum", "Number", "5");
        builder.AppendLine($"{indent}return vrsQuaternionAngle({first.Code}, {second.Code}) <= {maximum.Code}");
        return true;
    }

    private static LuauExpression? TryResolveReadableQuaternionPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        if (node.Type.Equals("QuaternionIdentity", StringComparison.OrdinalIgnoreCase))
        {
            return new LuauExpression("vrsQuaternionIdentity()", "Quaternion");
        }

        if (node.Type.Equals("QuaternionFromComponents", StringComparison.OrdinalIgnoreCase))
        {
            var x = PropertyParameterExpression(rule, node, nodesById, "x", "Number", "0", visitedNodeIds);
            var y = PropertyParameterExpression(rule, node, nodesById, "y", "Number", "0", visitedNodeIds);
            var z = PropertyParameterExpression(rule, node, nodesById, "z", "Number", "0", visitedNodeIds);
            var w = PropertyParameterExpression(rule, node, nodesById, "w", "Number", "1", visitedNodeIds);
            return new LuauExpression($"makeQuaternion({x.Code}, {y.Code}, {z.Code}, {w.Code})", "Quaternion");
        }

        if (node.Type.Equals("QuaternionFromEuler", StringComparison.OrdinalIgnoreCase))
        {
            var euler = PropertyVectorParameterExpression(rule, node, nodesById, "euler", "0", "0", "0", visitedNodeIds);
            return new LuauExpression($"vrsQuaternionFromEuler({euler.Code})", "Quaternion");
        }

        if (node.Type.Equals("QuaternionToEuler", StringComparison.OrdinalIgnoreCase))
        {
            var rotation = PropertyParameterExpression(rule, node, nodesById, "rotation", "Quaternion", "0,0,0,1", visitedNodeIds);
            return new LuauExpression($"vrsQuaternionToEuler({rotation.Code})", "Vector3");
        }

        if (node.Type.Equals("QuaternionFromAxisAngle", StringComparison.OrdinalIgnoreCase))
        {
            var axis = PropertyVectorParameterExpression(rule, node, nodesById, "axis", "0", "1", "0", visitedNodeIds);
            var angle = PropertyParameterExpression(rule, node, nodesById, "angle", "Number", "90", visitedNodeIds);
            return new LuauExpression($"vrsQuaternionFromAxisAngle({axis.Code}, {angle.Code})", "Quaternion");
        }

        if (node.Type.Equals("QuaternionLookRotation", StringComparison.OrdinalIgnoreCase))
        {
            var forward = PropertyVectorParameterExpression(rule, node, nodesById, "forward", "0", "0", "1", visitedNodeIds);
            var upwards = PropertyVectorParameterExpression(rule, node, nodesById, "upwards", "0", "1", "0", visitedNodeIds);
            return new LuauExpression($"vrsQuaternionLookRotation({forward.Code}, {upwards.Code})", "Quaternion");
        }

        if (node.Type.Equals("QuaternionFromToRotation", StringComparison.OrdinalIgnoreCase))
        {
            var from = PropertyVectorParameterExpression(rule, node, nodesById, "fromDirection", "0", "0", "1", visitedNodeIds);
            var to = PropertyVectorParameterExpression(rule, node, nodesById, "toDirection", "1", "0", "0", visitedNodeIds);
            return new LuauExpression($"vrsQuaternionFromToRotation({from.Code}, {to.Code})", "Quaternion");
        }

        if (node.Type.Equals("QuaternionInverse", StringComparison.OrdinalIgnoreCase))
        {
            var rotation = PropertyParameterExpression(rule, node, nodesById, "rotation", "Quaternion", "0,0,0,1", visitedNodeIds);
            return new LuauExpression($"vrsQuaternionInverse({rotation.Code})", "Quaternion");
        }

        if (node.Type.Equals("QuaternionNormalize", StringComparison.OrdinalIgnoreCase))
        {
            var rotation = PropertyParameterExpression(rule, node, nodesById, "rotation", "Quaternion", "0,0,0,1", visitedNodeIds);
            return new LuauExpression($"vrsQuaternionNormalize({rotation.Code})", "Quaternion");
        }

        if (node.Type.Equals("QuaternionLerp", StringComparison.OrdinalIgnoreCase))
        {
            var from = PropertyParameterExpression(rule, node, nodesById, "from", "Quaternion", "0,0,0,1", visitedNodeIds);
            var to = PropertyParameterExpression(rule, node, nodesById, "to", "Quaternion", "0,1,0,0", visitedNodeIds);
            var amount = PropertyParameterExpression(rule, node, nodesById, "amount", "Number", "0.5", visitedNodeIds);
            return new LuauExpression($"vrsQuaternionLerp({from.Code}, {to.Code}, {amount.Code})", "Quaternion");
        }

        if (node.Type.Equals("QuaternionSlerp", StringComparison.OrdinalIgnoreCase))
        {
            var from = PropertyParameterExpression(rule, node, nodesById, "from", "Quaternion", "0,0,0,1", visitedNodeIds);
            var to = PropertyParameterExpression(rule, node, nodesById, "to", "Quaternion", "0,1,0,0", visitedNodeIds);
            var amount = PropertyParameterExpression(rule, node, nodesById, "amount", "Number", "0.5", visitedNodeIds);
            return new LuauExpression($"vrsQuaternionSlerp({from.Code}, {to.Code}, {amount.Code})", "Quaternion");
        }

        if (node.Type.Equals("QuaternionRotateTowards", StringComparison.OrdinalIgnoreCase))
        {
            var from = PropertyParameterExpression(rule, node, nodesById, "from", "Quaternion", "0,0,0,1", visitedNodeIds);
            var to = PropertyParameterExpression(rule, node, nodesById, "to", "Quaternion", "0,1,0,0", visitedNodeIds);
            var maxDegrees = PropertyParameterExpression(rule, node, nodesById, "maxDegrees", "Number", "30", visitedNodeIds);
            return new LuauExpression($"vrsQuaternionRotateTowards({from.Code}, {to.Code}, {maxDegrees.Code})", "Quaternion");
        }

        if (node.Type.Equals("QuaternionAngle", StringComparison.OrdinalIgnoreCase))
        {
            var first = PropertyParameterExpression(rule, node, nodesById, "first", "Quaternion", "0,0,0,1", visitedNodeIds);
            var second = PropertyParameterExpression(rule, node, nodesById, "second", "Quaternion", "0,1,0,0", visitedNodeIds);
            return new LuauExpression($"vrsQuaternionAngle({first.Code}, {second.Code})", "Number");
        }

        if (node.Type.Equals("QuaternionDot", StringComparison.OrdinalIgnoreCase))
        {
            var first = PropertyParameterExpression(rule, node, nodesById, "first", "Quaternion", "0,0,0,1", visitedNodeIds);
            var second = PropertyParameterExpression(rule, node, nodesById, "second", "Quaternion", "0,1,0,0", visitedNodeIds);
            return new LuauExpression($"vrsQuaternionDot({first.Code}, {second.Code})", "Number");
        }

        return null;
    }
}
