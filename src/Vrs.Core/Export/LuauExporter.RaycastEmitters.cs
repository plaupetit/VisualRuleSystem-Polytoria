using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static bool TryAppendReadableRaycastConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (!condition.Type.Equals("RaycastHits", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indent = IndentText(indentLevel);
        AppendRaycastResultLocal(builder, rule, condition, nodesById, indentLevel, "raycastResult", "Raycast Hits", emitReturnOnMissingEnvironment: false);
        builder.AppendLine($"{indent}return raycastResult ~= nil and raycastResult.Instance ~= nil");
        return true;
    }

    private static LuauExpression? TryResolveReadableRaycastPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        if (node.Type.Equals("RaycastResult", StringComparison.OrdinalIgnoreCase))
        {
            return RaycastValueExpression(rule, node, nodesById, visitedNodeIds, "Raycast Result", "nil", "return raycastResult", null, "Any");
        }

        if (node.Type.Equals("RaycastHitObject", StringComparison.OrdinalIgnoreCase))
        {
            return RaycastValueExpression(rule, node, nodesById, visitedNodeIds, "Raycast Hit Object", "nil", "return raycastResult.Instance", "Instance", "SceneObject");
        }

        if (node.Type.Equals("RaycastHitPosition", StringComparison.OrdinalIgnoreCase))
        {
            return RaycastValueExpression(rule, node, nodesById, visitedNodeIds, "Raycast Hit Position", "makeVector3(0, 0, 0)", "return raycastResult.Position", "Position", "Vector3");
        }

        if (node.Type.Equals("RaycastHitNormal", StringComparison.OrdinalIgnoreCase))
        {
            return RaycastValueExpression(rule, node, nodesById, visitedNodeIds, "Raycast Hit Normal", "makeVector3(0, 0, 0)", "return raycastResult.Normal", "Normal", "Vector3");
        }

        if (node.Type.Equals("RaycastHitDistance", StringComparison.OrdinalIgnoreCase))
        {
            return RaycastValueExpression(rule, node, nodesById, visitedNodeIds, "Raycast Hit Distance", "0", "return tonumber(raycastResult.Distance) or 0", "Distance", "Number");
        }

        return null;
    }

    private static LuauExpression RaycastValueExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string readableName,
        string fallback,
        string returnStatement,
        string? requiredProperty,
        string dataType)
    {
        var origin = PropertyVectorParameterExpression(rule, node, nodesById, "origin", "0", "0", "0", visitedNodeIds);
        var direction = PropertyVectorParameterExpression(rule, node, nodesById, "direction", "0", "-1", "0", visitedNodeIds);
        var maxDistance = PropertyParameterExpression(rule, node, nodesById, "maxDistance", "Number", "100", visitedNodeIds);
        var propertyGuard = requiredProperty is null
            ? ""
            : $" if raycastResult.{requiredProperty} == nil then return {fallback} end;";
        var code = $"(function() if Environment == nil or Environment.Raycast == nil then print(\"{readableName} stopped: Environment:Raycast is not available.\"); return {fallback} end; local raycastResult = Environment:Raycast({origin.Code}, {direction.Code}, {maxDistance.Code}); if raycastResult == nil then return {fallback} end;{propertyGuard} {returnStatement} end)()";
        return new LuauExpression(code, dataType);
    }

    private static void AppendRaycastResultLocal(
        StringBuilder builder,
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string localName,
        string readableName,
        bool emitReturnOnMissingEnvironment)
    {
        var indent = IndentText(indentLevel);
        var origin = ParameterVectorExpression(rule, node, nodesById, "origin", "0", "0", "0");
        var direction = ParameterVectorExpression(rule, node, nodesById, "direction", "0", "-1", "0");
        var maxDistance = ParameterExpression(rule, node, nodesById, "maxDistance", "Number", "100");
        builder.AppendLine($"{indent}if Environment == nil or Environment.Raycast == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: Environment:Raycast is not available.\")");
        if (emitReturnOnMissingEnvironment)
        {
            builder.AppendLine($"{indent}    return");
        }
        else
        {
            builder.AppendLine($"{indent}    return false");
        }

        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local {localName} = Environment:Raycast({origin.Code}, {direction.Code}, {maxDistance.Code})");
    }
}
