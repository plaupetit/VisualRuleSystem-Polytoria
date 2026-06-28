using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static readonly HashSet<string> TweenTargetWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnTweenPositionReached",
        "OnTweenRotationReached",
        "OnTweenScaleReached",
        "OnTweenColorReached",
        "OnTweenTransparencyReached"
    };

    private static bool IsTweenTargetWatcherTrigger(string triggerType)
        => TweenTargetWatcherTriggerTypes.Contains(triggerType);

    private static bool TryAppendReadableTweenConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (TweenTargetDefinition.TryForCondition(condition.Type) is not { } definition)
        {
            return false;
        }

        AppendTweenTargetReachedCondition(builder, rule, condition, plan, nodesById, indentLevel, definition);
        return true;
    }

    private static void AppendReadableTweenTargetWatcherTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var definition = TweenTargetDefinition.ForTrigger(trigger.Type);
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.05");

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        AppendTweenTargetExpectation(builder, rule, trigger, nodesById, definition, 1);
        builder.AppendLine($"    if triggerObject.{definition.PropertyName} == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target does not expose {definition.PropertyName}.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    local function readMatched()");
        if (definition.Kind == TweenTargetKind.Vector3)
        {
            builder.AppendLine($"        local currentValue = triggerObject.{definition.PropertyName}");
            builder.AppendLine("        return vrsTweenVectorReached(currentValue, expectedTweenValue, tweenTolerance), currentValue");
        }
        else if (definition.Kind == TweenTargetKind.Number)
        {
            builder.AppendLine($"        local currentValue = tonumber(triggerObject.{definition.PropertyName}) or 0");
            builder.AppendLine("        return math.abs(currentValue - expectedTweenValue) <= tweenTolerance, currentValue");
        }
        else
        {
            builder.AppendLine($"        local currentValue = triggerObject.{definition.PropertyName}");
            builder.AppendLine("        return currentValue == expectedTweenValue, currentValue");
        }

        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, $", {definition.ContextField} = currentValue", 1);
        builder.AppendLine("end");
    }

    private static void AppendTweenTargetReachedCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        TweenTargetDefinition definition)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.{definition.PropertyName} == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        AppendTweenTargetExpectation(builder, rule, condition, nodesById, definition, indentLevel);
        if (definition.Kind == TweenTargetKind.Vector3)
        {
            builder.AppendLine($"{indent}return vrsTweenVectorReached(targetObject.{definition.PropertyName}, expectedTweenValue, tweenTolerance)");
            return;
        }

        if (definition.Kind == TweenTargetKind.Number)
        {
            builder.AppendLine($"{indent}local currentValue = tonumber(targetObject.{definition.PropertyName}) or 0");
            builder.AppendLine($"{indent}return math.abs(currentValue - expectedTweenValue) <= tweenTolerance");
            return;
        }

        builder.AppendLine($"{indent}return targetObject.{definition.PropertyName} == expectedTweenValue");
    }

    private static void AppendTweenTargetExpectation(
        StringBuilder builder,
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        TweenTargetDefinition definition,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (definition.Kind == TweenTargetKind.Vector3)
        {
            var expected = ParameterVectorExpression(rule, node, nodesById, "vector", definition.FallbackX, definition.FallbackY, definition.FallbackZ);
            var tolerance = ParameterExpression(rule, node, nodesById, "tolerance", "Number", "0.01");
            builder.AppendLine($"{indent}local expectedTweenValue = {expected.Code}");
            builder.AppendLine($"{indent}local tweenTolerance = math.max(tonumber({tolerance.Code}) or 0.01, 0)");
            return;
        }

        if (definition.Kind == TweenTargetKind.Number)
        {
            var expected = ParameterExpression(rule, node, nodesById, definition.ParameterKey, "Number", definition.FallbackNumber);
            var tolerance = ParameterExpression(rule, node, nodesById, "tolerance", "Number", "0.01");
            builder.AppendLine($"{indent}local expectedTweenValue = tonumber({expected.Code}) or {definition.FallbackNumber}");
            builder.AppendLine($"{indent}local tweenTolerance = math.max(tonumber({tolerance.Code}) or 0.01, 0)");
            return;
        }

        var color = ColorExpression(rule, node, nodesById);
        builder.AppendLine($"{indent}local expectedTweenValue = {color.Code}");
    }

    private static void AppendReadableTweenTargetRuntime(StringBuilder builder)
    {
        builder.AppendLine("local function vrsTweenVectorReached(currentValue, expectedValue, tolerance)");
        builder.AppendLine("    local allowed = math.max(tonumber(tolerance) or 0.01, 0)");
        builder.AppendLine("    return math.abs(vrsVector3Axis(currentValue, \"X\", \"x\", 0) - vrsVector3Axis(expectedValue, \"X\", \"x\", 0)) <= allowed and");
        builder.AppendLine("        math.abs(vrsVector3Axis(currentValue, \"Y\", \"y\", 0) - vrsVector3Axis(expectedValue, \"Y\", \"y\", 0)) <= allowed and");
        builder.AppendLine("        math.abs(vrsVector3Axis(currentValue, \"Z\", \"z\", 0) - vrsVector3Axis(expectedValue, \"Z\", \"z\", 0)) <= allowed");
        builder.AppendLine("end");
        builder.AppendLine();
    }

    private enum TweenTargetKind
    {
        Vector3,
        Number,
        Color
    }

    private sealed record TweenTargetDefinition(
        string PropertyName,
        TweenTargetKind Kind,
        string ParameterKey,
        string ContextField,
        string FallbackX,
        string FallbackY,
        string FallbackZ,
        string FallbackNumber)
    {
        public static TweenTargetDefinition? TryForCondition(string nodeType)
        {
            return nodeType switch
            {
                "TweenPositionReached" => Position(),
                "TweenRotationReached" => Rotation(),
                "TweenScaleReached" => Scale(),
                "TweenColorReached" => Color(),
                "TweenTransparencyReached" => Transparency(),
                _ => null
            };
        }

        public static TweenTargetDefinition ForTrigger(string nodeType)
        {
            return nodeType switch
            {
                "OnTweenPositionReached" => Position(),
                "OnTweenRotationReached" => Rotation(),
                "OnTweenScaleReached" => Scale(),
                "OnTweenColorReached" => Color(),
                "OnTweenTransparencyReached" => Transparency(),
                _ => throw new ArgumentOutOfRangeException(nameof(nodeType), nodeType, "Unknown tween target watcher trigger.")
            };
        }

        private static TweenTargetDefinition Position()
            => new("Position", TweenTargetKind.Vector3, "vector", "tweenPosition", "0", "1", "0", "0");

        private static TweenTargetDefinition Rotation()
            => new("Rotation", TweenTargetKind.Vector3, "vector", "tweenRotation", "0", "90", "0", "0");

        private static TweenTargetDefinition Scale()
            => new("Scale", TweenTargetKind.Vector3, "vector", "tweenScale", "1", "1", "1", "1");

        private static TweenTargetDefinition Color()
            => new("Color", TweenTargetKind.Color, "color", "tweenColor", "1", "1", "1", "0");

        private static TweenTargetDefinition Transparency()
            => new("Transparency", TweenTargetKind.Number, "transparency", "tweenTransparency", "0", "0", "0", "0");
    }
}
