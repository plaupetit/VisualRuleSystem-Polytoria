using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static bool TryAppendReadableEffectsActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (action.Type.Equals("StartParticles", StringComparison.OrdinalIgnoreCase))
        {
            AppendParticleMethodAction(builder, plan, action, indentLevel, "Play", null, "Start Particles");
            return true;
        }

        if (action.Type.Equals("StopParticles", StringComparison.OrdinalIgnoreCase))
        {
            AppendParticleMethodAction(builder, plan, action, indentLevel, "Stop", null, "Stop Particles");
            return true;
        }

        if (action.Type.Equals("EmitParticles", StringComparison.OrdinalIgnoreCase))
        {
            var count = ParameterExpression(rule, action, nodesById, "count", "Number", "20");
            AppendParticleMethodAction(builder, plan, action, indentLevel, "Emit", count.Code, "Burst Particles");
            return true;
        }

        if (action.Type.Equals("SetParticleAmount", StringComparison.OrdinalIgnoreCase))
        {
            AppendParticlePropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Amount", "amount", "Number", "20", "Set Particle Amount");
            return true;
        }

        return false;
    }

    private static LuauExpression? TryResolveReadableEffectsPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "ParticlesPlaying" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Playing", "Boolean", "Particles Playing", "return targetObject.Playing == true"),
            "ParticleAmount" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Amount", "Number", "Particle Amount", "return tonumber(targetObject.Amount) or 0"),
            _ => null
        };
    }

    private static void AppendParticleMethodAction(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode action,
        int indentLevel,
        string methodName,
        string? argumentCode,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "particleObject");
        AppendParticleMethodGuard(builder, indentLevel, methodName, readableName);
        if (string.IsNullOrWhiteSpace(argumentCode))
        {
            builder.AppendLine($"{indent}particleObject:{methodName}()");
        }
        else
        {
            builder.AppendLine($"{indent}particleObject:{methodName}({argumentCode})");
        }
    }

    private static void AppendParticlePropertyAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
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
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "particleObject");
        AppendParticlePropertyGuard(builder, indentLevel, propertyName, readableName);
        builder.AppendLine($"{indent}particleObject.{propertyName} = {value.Code}");
    }

    private static void AppendParticleMethodGuard(StringBuilder builder, int indentLevel, string methodName, string readableName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if particleObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target particle effect was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if particleObject.{methodName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not support {methodName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendParticlePropertyGuard(StringBuilder builder, int indentLevel, string propertyName, string readableName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if particleObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target particle effect was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if particleObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }
}
