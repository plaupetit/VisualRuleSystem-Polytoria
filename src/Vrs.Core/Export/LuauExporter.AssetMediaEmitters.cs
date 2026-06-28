using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static readonly HashSet<string> AssetMediaWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnDecalImageChanged",
        "OnPTImageAssetIdChanged",
        "OnPTAudioAssetIdChanged",
        "OnPTMeshAssetIdChanged",
        "OnPTMeshAnimationAssetIdChanged",
        "OnBuiltInAudioPresetChanged",
        "OnBuiltInFontPresetChanged",
        "OnFileLinkAssetIdChanged",
        "OnMeshAnimationTypeChanged",
        "OnGradientImageWidthReached",
        "OnGradientImageHeightReached"
    };

    private static bool IsAssetMediaWatcherTrigger(string triggerType)
        => AssetMediaWatcherTriggerTypes.Contains(triggerType);

    private static bool TryAppendReadableAssetMediaActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (action.Type.Equals("SetPTImageAssetId", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "ImageID", "assetId", "Number", "0", action.Label);
            return true;
        }

        if (action.Type.Equals("SetPTAudioAssetId", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "AudioID", "assetId", "Number", "0", action.Label);
            return true;
        }

        if (action.Type.Equals("SetPTMeshAssetId", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("SetPTMeshAnimationAssetId", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "AssetID", "assetId", "Number", "0", action.Label);
            return true;
        }

        if (action.Type.Equals("SetBuiltInAudioPreset", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "AudioPreset", "preset", "String", "Jump", action.Label);
            return true;
        }

        if (action.Type.Equals("SetFileLinkAssetId", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "LinkedID", "linkedId", "String", "", action.Label);
            return true;
        }

        if (action.Type.Equals("SetMeshAnimationType", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "AnimationType", "animationType", "String", "Normal", action.Label);
            return true;
        }

        if (action.Type.Equals("SetGradientImageSize", StringComparison.OrdinalIgnoreCase))
        {
            AppendGradientImageSizeAction(builder, rule, action, plan, nodesById, indentLevel);
            return true;
        }

        if (action.Type.Equals("SetBuiltInFontSettings", StringComparison.OrdinalIgnoreCase))
        {
            AppendBuiltInFontSettingsAction(builder, rule, action, plan, nodesById, indentLevel);
            return true;
        }

        return false;
    }

    private static bool TryAppendReadableAssetMediaConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (condition.Type.Equals("DecalImageIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendAssetStringPropertyEqualsCondition(builder, rule, condition, plan, nodesById, indentLevel, "Image", "image", "", "caseSensitive", "false");
            return true;
        }

        if (condition.Type.Equals("DecalHasImage", StringComparison.OrdinalIgnoreCase))
        {
            AppendAssetStringPropertyNotEmptyCondition(builder, plan, condition, indentLevel, "Image");
            return true;
        }

        if (condition.Type.Equals("PTImageAssetIdIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendAssetNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "ImageID", "assetId", "==", "0");
            return true;
        }

        if (condition.Type.Equals("PTAudioAssetIdIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendAssetNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "AudioID", "assetId", "==", "0");
            return true;
        }

        if (condition.Type.Equals("PTMeshAssetIdIs", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("PTMeshAnimationAssetIdIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendAssetNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "AssetID", "assetId", "==", "0");
            return true;
        }

        if (condition.Type.Equals("BuiltInAudioPresetIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendAssetStringPropertyEqualsCondition(builder, rule, condition, plan, nodesById, indentLevel, "AudioPreset", "preset", "Jump", "caseSensitive", "true");
            return true;
        }

        if (condition.Type.Equals("BuiltInFontPresetIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendAssetStringPropertyEqualsCondition(builder, rule, condition, plan, nodesById, indentLevel, "FontPreset", "preset", "SourceSans", "caseSensitive", "true");
            return true;
        }

        if (condition.Type.Equals("FileLinkAssetIdIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendAssetStringPropertyEqualsCondition(builder, rule, condition, plan, nodesById, indentLevel, "LinkedID", "linkedId", "", "caseSensitive", "true");
            return true;
        }

        if (condition.Type.Equals("MeshAnimationTypeIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendAssetStringPropertyEqualsCondition(builder, rule, condition, plan, nodesById, indentLevel, "AnimationType", "animationType", "Normal", "caseSensitive", "true");
            return true;
        }

        if (condition.Type.Equals("GradientImageWidthAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            AppendAssetNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "Width", "width", ">=", "256");
            return true;
        }

        if (condition.Type.Equals("GradientImageHeightAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            AppendAssetNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "Height", "height", ">=", "256");
            return true;
        }

        return false;
    }

    private static void AppendReadableAssetMediaWatcherTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var definition = AssetMediaWatcherDefinition.For(trigger.Type);
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");
        var threshold = definition.ParameterKey is null
            ? null
            : ParameterExpression(rule, trigger, nodesById, definition.ParameterKey, "Number", definition.FallbackLimit);

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        if (threshold is not null)
        {
            builder.AppendLine($"    local watchedLimit = tonumber({threshold.Code}) or {definition.FallbackLimit}");
        }

        builder.AppendLine("    local function readWatchedValue()");
        builder.AppendLine("        if triggerObject == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        builder.AppendLine($"        if triggerObject.{definition.PropertyName} == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        if (definition.ValueKind == AssetMediaWatcherValueKind.Number)
        {
            builder.AppendLine($"        return tonumber(triggerObject.{definition.PropertyName}) or {definition.FallbackCurrent}");
        }
        else
        {
            builder.AppendLine($"        return tostring(triggerObject.{definition.PropertyName} or \"\")");
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

    private static LuauExpression? TryResolveReadableAssetMediaPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "AssetReferenceValue" => AssetReferenceExpression(rule, node, nodesById, visitedNodeIds, "Asset Reference"),
            "ResourceAssetReferenceValue" => AssetReferenceExpression(rule, node, nodesById, visitedNodeIds, "Resource Asset Reference"),
            "FontAssetReferenceValue" => AssetReferenceExpression(rule, node, nodesById, visitedNodeIds, "Font Asset Reference"),
            "PTImageAssetIdValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "ImageID", "Number", "PT Image Asset ID", "return tonumber(targetObject.ImageID) or 0"),
            "PTAudioAssetIdValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "AudioID", "Number", "PT Audio Asset ID", "return tonumber(targetObject.AudioID) or 0"),
            "PTMeshAssetIdValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "AssetID", "Number", "PT Mesh Asset ID", "return tonumber(targetObject.AssetID) or 0"),
            "PTMeshAnimationAssetIdValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "AssetID", "Number", "PT Mesh Animation Asset ID", "return tonumber(targetObject.AssetID) or 0"),
            "BuiltInAudioPresetValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "AudioPreset", "String", "Built-In Audio Preset", "return tostring(targetObject.AudioPreset or \"\")"),
            "BuiltInFontPresetValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "FontPreset", "String", "Built-In Font Preset", "return tostring(targetObject.FontPreset or \"\")"),
            "FileLinkAssetIdValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "LinkedID", "String", "File Link Asset ID", "return tostring(targetObject.LinkedID or \"\")"),
            "GradientImageWidthValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Width", "Number", "Gradient Image Width", "return tonumber(targetObject.Width) or 0"),
            "MeshAnimationInfoNameValue" => MeshAnimationInfoValueExpression(rule, node, nodesById, visitedNodeIds, "Name", "String", "Mesh Animation Info Name", "return tostring(animationInfo.Name or \"\")"),
            _ => null
        };
    }

    private static void AppendGradientImageSizeAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var width = ParameterExpression(rule, action, nodesById, "width", "Number", "256");
        var height = ParameterExpression(rule, action, nodesById, "height", "Number", "256");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "assetObject");
        AppendAssetPropertyGuard(builder, indentLevel, "Width", action.Label, "assetObject");
        AppendAssetPropertyGuard(builder, indentLevel, "Height", action.Label, "assetObject");
        builder.AppendLine($"{indent}assetObject.Width = {width.Code}");
        builder.AppendLine($"{indent}assetObject.Height = {height.Code}");
    }

    private static void AppendBuiltInFontSettingsAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var preset = ParameterExpression(rule, action, nodesById, "preset", "String", "SourceSans");
        var weight = ParameterExpression(rule, action, nodesById, "weight", "String", "Regular");
        var style = ParameterExpression(rule, action, nodesById, "style", "String", "Normal");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "fontAsset");
        AppendAssetPropertyGuard(builder, indentLevel, "FontPreset", action.Label, "fontAsset");
        AppendAssetPropertyGuard(builder, indentLevel, "FontWeight", action.Label, "fontAsset");
        AppendAssetPropertyGuard(builder, indentLevel, "FontStyle", action.Label, "fontAsset");
        builder.AppendLine($"{indent}fontAsset.FontPreset = tostring({preset.Code})");
        builder.AppendLine($"{indent}fontAsset.FontWeight = tostring({weight.Code})");
        builder.AppendLine($"{indent}fontAsset.FontStyle = tostring({style.Code})");
    }

    private static void AppendAssetPropertyGuard(StringBuilder builder, int indentLevel, string propertyName, string readableName, string variableName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if {variableName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: asset was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if {variableName}.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: asset does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendAssetStringPropertyEqualsCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string parameterKey,
        string fallback,
        string caseSensitiveKey,
        string caseSensitiveFallback)
    {
        var indent = IndentText(indentLevel);
        var expectedText = ParameterExpression(rule, condition, nodesById, parameterKey, "String", fallback);
        var caseSensitive = ParameterExpression(rule, condition, nodesById, caseSensitiveKey, "Boolean", caseSensitiveFallback);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local currentText = tostring(targetObject.{propertyName} or \"\")");
        builder.AppendLine($"{indent}local expectedText = tostring({expectedText.Code})");
        builder.AppendLine($"{indent}if {caseSensitive.Code} then");
        builder.AppendLine($"{indent}    return currentText == expectedText");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return string.lower(currentText) == string.lower(expectedText)");
    }

    private static void AppendAssetStringPropertyNotEmptyCondition(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode condition,
        int indentLevel,
        string propertyName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return tostring(targetObject.{propertyName} or \"\") ~= \"\"");
    }

    private static void AppendAssetNumberPropertyCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string parameterKey,
        string operatorText,
        string fallback)
    {
        var indent = IndentText(indentLevel);
        var threshold = ParameterExpression(rule, condition, nodesById, parameterKey, "Number", fallback);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local currentNumber = tonumber(targetObject.{propertyName}) or 0");
        builder.AppendLine($"{indent}local expectedNumber = tonumber({threshold.Code}) or 0");
        builder.AppendLine($"{indent}return currentNumber {operatorText} expectedNumber");
    }

    private static LuauExpression AssetReferenceExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string readableName)
    {
        var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Self", visitedNodeIds);
        var code = $"(function() local assetObject = resolveTarget(triggerObject, {target.Code}); if assetObject == nil then print(\"{readableName} stopped: asset was not found.\"); return nil end; return assetObject end)()";
        return new LuauExpression(code, "Any");
    }

    private static LuauExpression MeshAnimationInfoValueExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string propertyName,
        string dataType,
        string readableName,
        string returnStatement)
    {
        var info = PropertyParameterExpression(rule, node, nodesById, "info", "Any", "", visitedNodeIds);
        var fallback = NormalizeExpressionDataType(dataType) == "Number" ? "0" : "\"\"";
        var code = $"(function() local animationInfo = {info.Code}; if animationInfo == nil then print(\"{readableName} stopped: animation info was not found.\"); return {fallback} end; local animationInfoType = type(animationInfo); if animationInfoType ~= \"table\" and animationInfoType ~= \"userdata\" then print(\"{readableName} stopped: animation info is not an object.\"); return {fallback} end; if animationInfo.{propertyName} == nil then print(\"{readableName} stopped: animation info does not expose {propertyName}.\"); return {fallback} end; {returnStatement} end)()";
        return new LuauExpression(code, dataType);
    }

    private enum AssetMediaWatcherValueKind
    {
        String,
        Number
    }

    private sealed record AssetMediaWatcherDefinition(
        string PropertyName,
        AssetMediaWatcherValueKind ValueKind,
        string ContextField,
        string MatchExpression,
        bool TriggersOnAnyChange = false,
        string? ParameterKey = null,
        string FallbackLimit = "0",
        string FallbackCurrent = "0")
    {
        public static AssetMediaWatcherDefinition For(string triggerType)
        {
            return triggerType switch
            {
                "OnDecalImageChanged" => ChangedString("Image", "assetImage"),
                "OnPTImageAssetIdChanged" => ChangedNumber("ImageID", "assetId"),
                "OnPTAudioAssetIdChanged" => ChangedNumber("AudioID", "assetId"),
                "OnPTMeshAssetIdChanged" => ChangedNumber("AssetID", "assetId"),
                "OnPTMeshAnimationAssetIdChanged" => ChangedNumber("AssetID", "assetId"),
                "OnBuiltInAudioPresetChanged" => ChangedString("AudioPreset", "audioPreset"),
                "OnBuiltInFontPresetChanged" => ChangedString("FontPreset", "fontPreset"),
                "OnFileLinkAssetIdChanged" => ChangedString("LinkedID", "linkedId"),
                "OnMeshAnimationTypeChanged" => ChangedString("AnimationType", "animationType"),
                "OnGradientImageWidthReached" => NumberThreshold("Width", "width", "256", "0", ">=", "imageWidth"),
                "OnGradientImageHeightReached" => NumberThreshold("Height", "height", "256", "0", ">=", "imageHeight"),
                _ => throw new ArgumentOutOfRangeException(nameof(triggerType), triggerType, "Unknown asset watcher trigger.")
            };
        }

        private static AssetMediaWatcherDefinition ChangedString(string propertyName, string contextField)
        {
            return new AssetMediaWatcherDefinition(propertyName, AssetMediaWatcherValueKind.String, contextField, "false", TriggersOnAnyChange: true);
        }

        private static AssetMediaWatcherDefinition ChangedNumber(string propertyName, string contextField)
        {
            return new AssetMediaWatcherDefinition(propertyName, AssetMediaWatcherValueKind.Number, contextField, "false", TriggersOnAnyChange: true);
        }

        private static AssetMediaWatcherDefinition NumberThreshold(
            string propertyName,
            string parameterKey,
            string fallbackLimit,
            string fallbackCurrent,
            string comparison,
            string contextField)
        {
            return new AssetMediaWatcherDefinition(
                propertyName,
                AssetMediaWatcherValueKind.Number,
                contextField,
                $"currentValue {comparison} watchedLimit",
                ParameterKey: parameterKey,
                FallbackLimit: fallbackLimit,
                FallbackCurrent: fallbackCurrent);
        }
    }
}
