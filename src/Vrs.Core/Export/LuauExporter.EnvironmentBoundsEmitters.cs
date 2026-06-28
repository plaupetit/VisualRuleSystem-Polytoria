using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Environment and bounds nodes are grouped together because both expose
    // gameplay-wide spatial rules without needing a new graph model concept.
    private static bool TryAppendReadableEnvironmentBoundsConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (!condition.Type.Equals("ObjectBoundsContainsPoint", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indent = IndentText(indentLevel);
        var x = ParameterExpression(rule, condition, nodesById, "x", "Number", "0");
        var y = ParameterExpression(rule, condition, nodesById, "y", "Number", "0");
        var z = ParameterExpression(rule, condition, nodesById, "z", "Number", "0");
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil or targetObject.GetBounds == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local bounds = targetObject:GetBounds()");
        builder.AppendLine($"{indent}if bounds == nil or bounds.Contains == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return bounds:Contains(makeVector3({x.Code}, {y.Code}, {z.Code}))");
        return true;
    }

    private static bool TryAppendReadableEnvironmentBoundsActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (action.Type.Equals("SetWorldGravity", StringComparison.OrdinalIgnoreCase))
        {
            AppendSetEnvironmentPropertyAction(builder, rule, action, nodesById, indentLevel, "Gravity", "gravity", "Number", "9.81", "Set World Gravity");
            return true;
        }

        if (action.Type.Equals("SetPartDestroyHeight", StringComparison.OrdinalIgnoreCase))
        {
            AppendSetEnvironmentPropertyAction(builder, rule, action, nodesById, indentLevel, "PartDestroyHeight", "height", "Number", "-100", "Set Fall Destroy Height");
            return true;
        }

        if (action.Type.Equals("SetAutoGenerateNavMesh", StringComparison.OrdinalIgnoreCase))
        {
            AppendSetEnvironmentPropertyAction(builder, rule, action, nodesById, indentLevel, "AutoGenerateNavMesh", "enabled", "Boolean", "true", "Set Auto Nav Mesh");
            return true;
        }

        if (action.Type.Equals("RebuildNavMesh", StringComparison.OrdinalIgnoreCase))
        {
            AppendRebuildNavMeshAction(builder, indentLevel);
            return true;
        }

        return false;
    }

    private static LuauExpression? TryResolveReadableEnvironmentBoundsPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "WorldGravityValue" => EnvironmentValueExpression("Gravity", "Number", "World Gravity", "return tonumber(Environment.Gravity) or 0"),
            "PartDestroyHeightValue" => EnvironmentValueExpression("PartDestroyHeight", "Number", "Fall Destroy Height", "return tonumber(Environment.PartDestroyHeight) or 0"),
            "AutoGenerateNavMeshValue" => EnvironmentValueExpression("AutoGenerateNavMesh", "Boolean", "Auto Nav Mesh", "return Environment.AutoGenerateNavMesh == true"),
            "CurrentCameraValue" => EnvironmentValueExpression("CurrentCamera", "SceneObject", "Current Camera", "return Environment.CurrentCamera"),
            "ObjectBoundsCenter" => BoundsValueExpression(rule, node, nodesById, visitedNodeIds, "Center", "Vector3", "Object Bounds Center", "return bounds.Center"),
            "ObjectBoundsSize" => BoundsValueExpression(rule, node, nodesById, visitedNodeIds, "Size", "Vector3", "Object Bounds Size", "return bounds.Size"),
            "ObjectBoundsExtents" => BoundsValueExpression(rule, node, nodesById, visitedNodeIds, "Extents", "Vector3", "Object Bounds Extents", "return bounds.Extents"),
            "ObjectBoundsVolume" => BoundsValueExpression(rule, node, nodesById, visitedNodeIds, "Volume", "Number", "Object Bounds Volume", "return tonumber(bounds.Volume) or 0"),
            _ => null
        };
    }

    private static void AppendSetEnvironmentPropertyAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string parameterKey,
        string dataType,
        string fallback,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        var value = ParameterExpression(rule, action, nodesById, parameterKey, dataType, fallback);
        builder.AppendLine($"{indent}if Environment == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: Environment is not available.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if Environment.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: Environment does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}Environment.{propertyName} = {value.Code}");
    }

    private static void AppendRebuildNavMeshAction(StringBuilder builder, int indentLevel)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if Environment == nil or Environment.RebuildNavMesh == nil then");
        builder.AppendLine($"{indent}    print(\"Rebuild Nav Mesh stopped: Environment:RebuildNavMesh is not available.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}Environment:RebuildNavMesh()");
    }

    private static LuauExpression EnvironmentValueExpression(
        string propertyName,
        string dataType,
        string readableName,
        string returnStatement)
    {
        var fallback = NormalizeExpressionDataType(dataType) switch
        {
            "String" => "\"\"",
            "Number" => "0",
            "Boolean" => "false",
            "Vector3" => "makeVector3(0, 0, 0)",
            "Color" => "Color.New(1, 1, 1, 1)",
            _ => "nil"
        };
        var code = $"(function() if Environment == nil then print(\"{readableName} stopped: Environment is not available.\"); return {fallback} end; if Environment.{propertyName} == nil then print(\"{readableName} stopped: Environment does not expose {propertyName}.\"); return {fallback} end; {returnStatement} end)()";
        return new LuauExpression(code, dataType);
    }

    private static LuauExpression BoundsValueExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string propertyName,
        string dataType,
        string readableName,
        string returnStatement)
    {
        var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Self", visitedNodeIds);
        var fallback = NormalizeExpressionDataType(dataType) switch
        {
            "Number" => "0",
            "Vector3" => "makeVector3(0, 0, 0)",
            _ => "nil"
        };
        var code = $"(function() local targetObject = resolveTarget(triggerObject, {target.Code}); if targetObject == nil then print(\"{readableName} stopped: target was not found.\"); return {fallback} end; if targetObject.GetBounds == nil then print(\"{readableName} stopped: target does not support GetBounds.\"); return {fallback} end; local bounds = targetObject:GetBounds(); if bounds == nil then print(\"{readableName} stopped: target returned no bounds.\"); return {fallback} end; if bounds.{propertyName} == nil then print(\"{readableName} stopped: bounds do not expose {propertyName}.\"); return {fallback} end; {returnStatement} end)()";
        return new LuauExpression(code, dataType);
    }
}
