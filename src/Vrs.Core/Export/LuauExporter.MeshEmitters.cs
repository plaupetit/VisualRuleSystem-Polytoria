using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Mesh nodes stay in a dedicated emitter because they mix event wiring,
    // animation methods, and status values from the Polytoria Mesh API.
    private static bool TryAppendReadableMeshActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (action.Type.Equals("PlayMeshAnimation", StringComparison.OrdinalIgnoreCase))
        {
            var animationName = ParameterExpression(rule, action, nodesById, "animationName", "String", "Idle");
            var speed = ParameterExpression(rule, action, nodesById, "speed", "Number", "1");
            var loop = ParameterExpression(rule, action, nodesById, "loop", "Boolean", "true");
            AppendMeshMethodAction(builder, plan, action, indentLevel, "PlayAnimation", $"tostring({animationName.Code}), {speed.Code}, {loop.Code}", "Play Mesh Animation");
            return true;
        }

        if (action.Type.Equals("StopMeshAnimation", StringComparison.OrdinalIgnoreCase))
        {
            var animationName = ParameterExpression(rule, action, nodesById, "animationName", "String", "Idle");
            AppendMeshMethodAction(builder, plan, action, indentLevel, "StopAnimation", $"tostring({animationName.Code})", "Stop Mesh Animation");
            return true;
        }

        return false;
    }

    private static LuauExpression? TryResolveReadableMeshPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "CurrentMeshAnimation" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "CurrentAnimation", "String", "Current Mesh Animation", "return tostring(targetObject.CurrentAnimation or \"\")"),
            "MeshAnimationPlaying" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "IsAnimationPlaying", "Boolean", "Mesh Animation Playing", "return targetObject.IsAnimationPlaying == true"),
            "MeshLoading" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Loading", "Boolean", "Mesh Loading", "return targetObject.Loading == true"),
            _ => null
        };
    }

    private static void AppendReadableMeshLoadedTrigger(
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
        builder.AppendLine("    if triggerObject.Loaded == nil or triggerObject.Loaded.Connect == nil then");
        builder.AppendLine("        print(\"On Mesh Loaded trigger stopped: target has no Loaded event.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    triggerObject.Loaded:Connect(function()");
        builder.AppendLine("        local triggerContext = { object = triggerObject, mesh = triggerObject }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
        if (!emitted)
        {
            builder.AppendLine("        print(\"On Mesh Loaded trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("    end)");
        builder.AppendLine("end");
    }

    private static void AppendMeshMethodAction(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode action,
        int indentLevel,
        string methodName,
        string argumentCode,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "meshObject");
        AppendMeshMethodGuard(builder, indentLevel, methodName, readableName);
        builder.AppendLine($"{indent}meshObject:{methodName}({argumentCode})");
    }

    private static void AppendMeshMethodGuard(StringBuilder builder, int indentLevel, string methodName, string readableName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if meshObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target mesh was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if meshObject.{methodName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not support {methodName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }
}
