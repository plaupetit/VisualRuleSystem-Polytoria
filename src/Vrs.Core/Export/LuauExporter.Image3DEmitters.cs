using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Image3D nodes cover documented world-image properties only. Asset/image
    // assignment is intentionally left for a later asset-picker pass.
    private static readonly HashSet<string> Image3DWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "On3DImageColorChanged",
        "On3DImageShadowsEnabled",
        "On3DImageLightingEnabled",
        "On3DImageFaceCameraEnabled",
        "On3DImageTextureScaleChanged",
        "On3DImageTextureOffsetChanged"
    };

    private static bool IsImage3DWatcherTrigger(string triggerType)
        => Image3DWatcherTriggerTypes.Contains(triggerType);

    private static bool TryAppendReadableImage3DActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (action.Type.Equals("Set3DImageColor", StringComparison.OrdinalIgnoreCase))
        {
            var red = ParameterExpression(rule, action, nodesById, "r", "Number", "1");
            var green = ParameterExpression(rule, action, nodesById, "g", "Number", "1");
            var blue = ParameterExpression(rule, action, nodesById, "b", "Number", "1");
            AppendImage3DTargetVariable(builder, plan, action, indentLevel, "Color", "Set 3D Image Color");
            builder.AppendLine($"{indent}imageObject.Color = Color.New({red.Code}, {green.Code}, {blue.Code}, 1)");
            return true;
        }

        if (action.Type.Equals("Set3DImageShadows", StringComparison.OrdinalIgnoreCase))
        {
            var enabled = ParameterExpression(rule, action, nodesById, "enabled", "Boolean", "true");
            AppendImage3DTargetVariable(builder, plan, action, indentLevel, "CastShadows", "Set 3D Image Shadows");
            builder.AppendLine($"{indent}imageObject.CastShadows = {enabled.Code}");
            return true;
        }

        if (action.Type.Equals("Set3DImageLighting", StringComparison.OrdinalIgnoreCase))
        {
            var enabled = ParameterExpression(rule, action, nodesById, "enabled", "Boolean", "true");
            AppendImage3DTargetVariable(builder, plan, action, indentLevel, "Shaded", "Set 3D Image Lighting");
            builder.AppendLine($"{indent}imageObject.Shaded = {enabled.Code}");
            return true;
        }

        if (action.Type.Equals("Set3DImageFaceCamera", StringComparison.OrdinalIgnoreCase))
        {
            var enabled = ParameterExpression(rule, action, nodesById, "enabled", "Boolean", "true");
            AppendImage3DTargetVariable(builder, plan, action, indentLevel, "FaceCamera", "Set 3D Image Face Camera");
            builder.AppendLine($"{indent}imageObject.FaceCamera = {enabled.Code}");
            return true;
        }

        if (action.Type.Equals("Set3DImageTextureScale", StringComparison.OrdinalIgnoreCase))
        {
            var x = ParameterExpression(rule, action, nodesById, "x", "Number", "1");
            var y = ParameterExpression(rule, action, nodesById, "y", "Number", "1");
            AppendImage3DTargetVariable(builder, plan, action, indentLevel, "TextureScale", "Set 3D Image Texture Scale");
            builder.AppendLine($"{indent}imageObject.TextureScale = makeVector2({x.Code}, {y.Code})");
            return true;
        }

        if (action.Type.Equals("Set3DImageTextureOffset", StringComparison.OrdinalIgnoreCase))
        {
            var x = ParameterExpression(rule, action, nodesById, "x", "Number", "0");
            var y = ParameterExpression(rule, action, nodesById, "y", "Number", "0");
            AppendImage3DTargetVariable(builder, plan, action, indentLevel, "TextureOffset", "Set 3D Image Texture Offset");
            builder.AppendLine($"{indent}imageObject.TextureOffset = makeVector2({x.Code}, {y.Code})");
            return true;
        }

        return false;
    }

    private static bool TryAppendReadableImage3DConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (condition.Type.Equals("Image3DCastsShadows", StringComparison.OrdinalIgnoreCase))
        {
            AppendImage3DBooleanCondition(builder, condition, plan, indentLevel, "CastShadows");
            return true;
        }

        if (condition.Type.Equals("Image3DUsesLighting", StringComparison.OrdinalIgnoreCase))
        {
            AppendImage3DBooleanCondition(builder, condition, plan, indentLevel, "Shaded");
            return true;
        }

        if (condition.Type.Equals("Image3DFacesCamera", StringComparison.OrdinalIgnoreCase))
        {
            AppendImage3DBooleanCondition(builder, condition, plan, indentLevel, "FaceCamera");
            return true;
        }

        if (condition.Type.Equals("Image3DColorIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendImage3DColorCondition(builder, rule, condition, plan, nodesById, indentLevel);
            return true;
        }

        if (condition.Type.Equals("Image3DTextureScaleIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendImage3DVector2Condition(builder, rule, condition, plan, nodesById, indentLevel, "TextureScale", "1", "1");
            return true;
        }

        if (condition.Type.Equals("Image3DTextureOffsetIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendImage3DVector2Condition(builder, rule, condition, plan, nodesById, indentLevel, "TextureOffset", "0", "0");
            return true;
        }

        return false;
    }

    private static void AppendReadableImage3DWatcherTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var definition = Image3DWatcherDefinition.For(trigger.Type);
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine("    local function readWatchedValue()");
        builder.AppendLine("        if triggerObject == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        builder.AppendLine($"        if triggerObject.{definition.PropertyName} == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        if (definition.ValueKind == Image3DWatcherValueKind.Boolean)
        {
            builder.AppendLine($"        return triggerObject.{definition.PropertyName} == true");
        }
        else
        {
            builder.AppendLine($"        return triggerObject.{definition.PropertyName}");
        }

        builder.AppendLine("    end");
        if (definition.TriggersOnAnyChange)
        {
            AppendReadableAnyChangeLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, $", {definition.ContextField} = currentValue", 1);
            builder.AppendLine("end");
            return;
        }

        builder.AppendLine("    local function readMatched()");
        builder.AppendLine("        local currentValue = readWatchedValue()");
        builder.AppendLine("        if currentValue == nil then");
        builder.AppendLine("            return false, nil");
        builder.AppendLine("        end");
        builder.AppendLine($"        return {definition.MatchExpression}, currentValue");
        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, $", {definition.ContextField} = currentValue", 1);
        builder.AppendLine("end");
    }

    private static LuauExpression? TryResolveReadableImage3DPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "Image3DColorValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Color", "Color", "3D Image Color", "return targetObject.Color"),
            "Image3DCastsShadowsValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "CastShadows", "Boolean", "3D Image Casts Shadows", "return targetObject.CastShadows == true"),
            "Image3DUsesLightingValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Shaded", "Boolean", "3D Image Uses Lighting", "return targetObject.Shaded == true"),
            "Image3DFacesCameraValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "FaceCamera", "Boolean", "3D Image Faces Camera", "return targetObject.FaceCamera == true"),
            _ => null
        };
    }

    private static void AppendImage3DBooleanCondition(
        StringBuilder builder,
        RuleNode condition,
        ReadableExportPlan plan,
        int indentLevel,
        string propertyName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "imageObject");
        builder.AppendLine($"{indent}if imageObject == nil or imageObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return imageObject.{propertyName} == true");
    }

    private static void AppendImage3DColorCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var color = ColorExpression(rule, condition, nodesById);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "imageObject");
        builder.AppendLine($"{indent}if imageObject == nil or imageObject.Color == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local expectedColor = {color.Code}");
        builder.AppendLine($"{indent}return imageObject.Color == expectedColor");
    }

    private static void AppendImage3DVector2Condition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string fallbackX,
        string fallbackY)
    {
        var indent = IndentText(indentLevel);
        var expectedX = ParameterExpression(rule, condition, nodesById, "x", "Number", fallbackX);
        var expectedY = ParameterExpression(rule, condition, nodesById, "y", "Number", fallbackY);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "imageObject");
        builder.AppendLine($"{indent}if imageObject == nil or imageObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local currentX = vrsVector2Axis(imageObject.{propertyName}, \"X\", \"x\", {fallbackX})");
        builder.AppendLine($"{indent}local currentY = vrsVector2Axis(imageObject.{propertyName}, \"Y\", \"y\", {fallbackY})");
        builder.AppendLine($"{indent}local expectedX = tonumber({expectedX.Code}) or {fallbackX}");
        builder.AppendLine($"{indent}local expectedY = tonumber({expectedY.Code}) or {fallbackY}");
        builder.AppendLine($"{indent}return currentX == expectedX and currentY == expectedY");
    }

    private static void AppendImage3DTargetVariable(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode node,
        int indentLevel,
        string propertyName,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, node, indentLevel, "imageObject");
        builder.AppendLine($"{indent}if imageObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target 3D image was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if imageObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target 3D image does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private enum Image3DWatcherValueKind
    {
        Boolean,
        Any
    }

    private sealed record Image3DWatcherDefinition(
        string PropertyName,
        Image3DWatcherValueKind ValueKind,
        string ContextField,
        string MatchExpression,
        bool TriggersOnAnyChange = false)
    {
        public static Image3DWatcherDefinition For(string triggerType)
        {
            return triggerType switch
            {
                "On3DImageColorChanged" => Changed("Color", "image3DColor"),
                "On3DImageShadowsEnabled" => Boolean("CastShadows", "image3DCastsShadows"),
                "On3DImageLightingEnabled" => Boolean("Shaded", "image3DUsesLighting"),
                "On3DImageFaceCameraEnabled" => Boolean("FaceCamera", "image3DFacesCamera"),
                "On3DImageTextureScaleChanged" => Changed("TextureScale", "image3DTextureScale"),
                "On3DImageTextureOffsetChanged" => Changed("TextureOffset", "image3DTextureOffset"),
                _ => throw new ArgumentOutOfRangeException(nameof(triggerType), triggerType, "Unknown Image3D watcher trigger.")
            };
        }

        private static Image3DWatcherDefinition Changed(string propertyName, string contextField)
            => new(propertyName, Image3DWatcherValueKind.Any, contextField, "false", TriggersOnAnyChange: true);

        private static Image3DWatcherDefinition Boolean(string propertyName, string contextField)
            => new(propertyName, Image3DWatcherValueKind.Boolean, contextField, "currentValue == true");
    }
}
