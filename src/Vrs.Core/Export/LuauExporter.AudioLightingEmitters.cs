using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static readonly HashSet<string> AudioLightingWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnSoundStarted",
        "OnSoundStopped",
        "OnSoundVolumeReached",
        "OnSoundVolumeDroppedTo",
        "OnFogEnabled",
        "OnFogDisabled",
        "OnFogStartDistanceReached",
        "OnFogStartDistanceDroppedTo",
        "OnFogEndDistanceReached",
        "OnFogEndDistanceDroppedTo",
        "OnLightBrightnessReached",
        "OnLightBrightnessDroppedTo",
        "OnLightShadowsEnabled",
        "OnLightShadowsDisabled",
        "OnSunLightBrightnessReached",
        "OnSunLightBrightnessDroppedTo",
        "OnSunLightShadowsEnabled",
        "OnSunLightShadowsDisabled"
    };

    private static readonly HashSet<string> ImageSkyWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnImageSkyTopImageChanged",
        "OnImageSkyBottomImageChanged",
        "OnImageSkyLeftImageChanged",
        "OnImageSkyRightImageChanged",
        "OnImageSkyFrontImageChanged",
        "OnImageSkyBackImageChanged"
    };

    private static readonly HashSet<string> ProceduralSkyWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnProceduralSkySunSizeReached",
        "OnProceduralSkyTintChanged",
        "OnProceduralSkyHorizonColorChanged",
        "OnProceduralSkyGroundColorChanged",
        "OnProceduralSkyExposureReached"
    };

    private static bool IsAudioLightingWatcherTrigger(string triggerType)
    {
        return AudioLightingWatcherTriggerTypes.Contains(triggerType);
    }

    private static bool IsImageSkyWatcherTrigger(string triggerType)
    {
        return ImageSkyWatcherTriggerTypes.Contains(triggerType);
    }

    private static bool IsProceduralSkyWatcherTrigger(string triggerType)
    {
        return ProceduralSkyWatcherTriggerTypes.Contains(triggerType);
    }

    private static bool TryAppendReadableAudioLightingActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (action.Type.Equals("PlaySound", StringComparison.OrdinalIgnoreCase))
        {
            AppendSoundMethodAction(builder, plan, action, indentLevel, "Play", null, "Play Sound");
            return true;
        }

        if (action.Type.Equals("PlaySoundOnce", StringComparison.OrdinalIgnoreCase))
        {
            var volume = ParameterExpression(rule, action, nodesById, "volume", "Number", "1");
            AppendSoundMethodAction(builder, plan, action, indentLevel, "PlayOneShot", volume.Code, "Play Sound Once");
            return true;
        }

        if (action.Type.Equals("PauseSound", StringComparison.OrdinalIgnoreCase))
        {
            AppendSoundMethodAction(builder, plan, action, indentLevel, "Pause", null, "Pause Sound");
            return true;
        }

        if (action.Type.Equals("StopSound", StringComparison.OrdinalIgnoreCase))
        {
            AppendSoundMethodAction(builder, plan, action, indentLevel, "Stop", null, "Stop Sound");
            return true;
        }

        if (action.Type.Equals("SetSoundVolume", StringComparison.OrdinalIgnoreCase))
        {
            AppendSoundPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Volume", "volume", "Number", "1", "Set Sound Volume");
            return true;
        }

        if (action.Type.Equals("SetSoundLoop", StringComparison.OrdinalIgnoreCase))
        {
            AppendSoundPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Loop", "enabled", "Boolean", "true", "Set Sound Loop");
            return true;
        }

        if (action.Type.Equals("SetSoundAudio", StringComparison.OrdinalIgnoreCase))
        {
            AppendSoundPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Audio", "audio", "Any", "", "Set Sound Audio");
            return true;
        }

        if (action.Type.Equals("SetLightColor", StringComparison.OrdinalIgnoreCase))
        {
            AppendObjectLightColorAction(builder, rule, action, plan, nodesById, indentLevel, "Set Light Color");
            return true;
        }

        if (action.Type.Equals("SetLightBrightness", StringComparison.OrdinalIgnoreCase))
        {
            AppendObjectLightPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Brightness", "brightness", "Number", "1", "Set Light Brightness");
            return true;
        }

        if (action.Type.Equals("SetLightShine", StringComparison.OrdinalIgnoreCase))
        {
            AppendObjectLightPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Specular", "shine", "Number", "0.5", "Set Light Shine");
            return true;
        }

        if (action.Type.Equals("SetLightShadows", StringComparison.OrdinalIgnoreCase))
        {
            AppendObjectLightPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Shadows", "shadows", "Boolean", "true", "Set Light Shadows");
            return true;
        }

        if (action.Type.Equals("SetSunLightColor", StringComparison.OrdinalIgnoreCase))
        {
            AppendObjectLightColorAction(builder, rule, action, plan, nodesById, indentLevel, "Set Sun Light Color");
            return true;
        }

        if (action.Type.Equals("SetSunLightBrightness", StringComparison.OrdinalIgnoreCase))
        {
            AppendObjectLightPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Brightness", "brightness", "Number", "1", "Set Sun Light Brightness");
            return true;
        }

        if (action.Type.Equals("SetSunLightShine", StringComparison.OrdinalIgnoreCase))
        {
            AppendObjectLightPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Specular", "shine", "Number", "0.5", "Set Sun Light Shine");
            return true;
        }

        if (action.Type.Equals("SetSunLightShadows", StringComparison.OrdinalIgnoreCase))
        {
            AppendObjectLightPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Shadows", "shadows", "Boolean", "true", "Set Sun Light Shadows");
            return true;
        }

        if (action.Type.Equals("SetPointLightRange", StringComparison.OrdinalIgnoreCase))
        {
            AppendObjectLightPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Range", "range", "Number", "20", "Set Point Light Range");
            return true;
        }

        if (action.Type.Equals("SetSpotLightRange", StringComparison.OrdinalIgnoreCase))
        {
            AppendObjectLightPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Range", "range", "Number", "20", "Set Spot Light Range");
            return true;
        }

        if (action.Type.Equals("SetSpotLightAngle", StringComparison.OrdinalIgnoreCase))
        {
            AppendObjectLightPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Angle", "angle", "Number", "45", "Set Spot Light Angle");
            return true;
        }

        if (action.Type.Equals("SetColorAdjustBrightness", StringComparison.OrdinalIgnoreCase))
        {
            AppendColorAdjustPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Brightness", "brightness", "Number", "0", "Set Color Adjust Brightness");
            return true;
        }

        if (action.Type.Equals("SetColorAdjustContrast", StringComparison.OrdinalIgnoreCase))
        {
            AppendColorAdjustPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Contrast", "contrast", "Number", "0", "Set Color Adjust Contrast");
            return true;
        }

        if (action.Type.Equals("SetColorAdjustSaturation", StringComparison.OrdinalIgnoreCase))
        {
            AppendColorAdjustPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Saturation", "saturation", "Number", "0", "Set Color Adjust Saturation");
            return true;
        }

        if (action.Type.Equals("SetColorAdjustTint", StringComparison.OrdinalIgnoreCase))
        {
            AppendColorAdjustTintAction(builder, rule, action, plan, nodesById, indentLevel, "Set Color Adjust Tint");
            return true;
        }

        if (action.Type.Equals("SetProceduralSkySunSize", StringComparison.OrdinalIgnoreCase))
        {
            AppendProceduralSkyPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "SunSize", "size", "Number", "1", "Set Procedural Sky Sun Size");
            return true;
        }

        if (action.Type.Equals("SetProceduralSkyTint", StringComparison.OrdinalIgnoreCase))
        {
            AppendProceduralSkyColorAction(builder, rule, action, plan, nodesById, indentLevel, "SkyTint", "Set Procedural Sky Tint");
            return true;
        }

        if (action.Type.Equals("SetProceduralSkyHorizonColor", StringComparison.OrdinalIgnoreCase))
        {
            AppendProceduralSkyColorAction(builder, rule, action, plan, nodesById, indentLevel, "HorizonColor", "Set Procedural Sky Horizon Color");
            return true;
        }

        if (action.Type.Equals("SetProceduralSkyGroundColor", StringComparison.OrdinalIgnoreCase))
        {
            AppendProceduralSkyColorAction(builder, rule, action, plan, nodesById, indentLevel, "GroundColor", "Set Procedural Sky Ground Color");
            return true;
        }

        if (action.Type.Equals("SetProceduralSkyExposure", StringComparison.OrdinalIgnoreCase))
        {
            AppendProceduralSkyPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Exposure", "exposure", "Number", "1", "Set Procedural Sky Exposure");
            return true;
        }

        if (action.Type.Equals("SetGradientSkyColors", StringComparison.OrdinalIgnoreCase))
        {
            AppendGradientSkyColorsAction(builder, rule, action, plan, nodesById, indentLevel);
            return true;
        }

        if (action.Type.Equals("SetGradientSkySunDisc", StringComparison.OrdinalIgnoreCase))
        {
            AppendGradientSkyColorNumbersAction(builder, rule, action, plan, nodesById, indentLevel, "SunDiscColor", "SunDiscMultiplier", "multiplier", "SunDiscExponent", "exponent", "Set Gradient Sky Sun Disc");
            return true;
        }

        if (action.Type.Equals("SetGradientSkySunHalo", StringComparison.OrdinalIgnoreCase))
        {
            AppendGradientSkyColorNumbersAction(builder, rule, action, plan, nodesById, indentLevel, "SunHaloColor", "SunHaloExponent", "exponent", "SunHaloContribution", "contribution", "Set Gradient Sky Sun Halo");
            return true;
        }

        if (action.Type.Equals("SetGradientSkyHorizonLine", StringComparison.OrdinalIgnoreCase))
        {
            AppendGradientSkyColorNumbersAction(builder, rule, action, plan, nodesById, indentLevel, "HorizonLineColor", "HorizonLineExponent", "exponent", "HorizonLineContribution", "contribution", "Set Gradient Sky Horizon Line");
            return true;
        }

        if (action.Type.Equals("SetImageSkyAllImages", StringComparison.OrdinalIgnoreCase))
        {
            AppendImageSkyAllImagesAction(builder, rule, action, plan, nodesById, indentLevel);
            return true;
        }

        if (action.Type.Equals("SetImageSkyTopImage", StringComparison.OrdinalIgnoreCase))
        {
            AppendImageSkyImageAction(builder, rule, action, plan, nodesById, indentLevel, "TopImage", "Set Image Sky Top Image");
            return true;
        }

        if (action.Type.Equals("SetImageSkyBottomImage", StringComparison.OrdinalIgnoreCase))
        {
            AppendImageSkyImageAction(builder, rule, action, plan, nodesById, indentLevel, "BottomImage", "Set Image Sky Bottom Image");
            return true;
        }

        if (action.Type.Equals("SetImageSkyLeftImage", StringComparison.OrdinalIgnoreCase))
        {
            AppendImageSkyImageAction(builder, rule, action, plan, nodesById, indentLevel, "LeftImage", "Set Image Sky Left Image");
            return true;
        }

        if (action.Type.Equals("SetImageSkyRightImage", StringComparison.OrdinalIgnoreCase))
        {
            AppendImageSkyImageAction(builder, rule, action, plan, nodesById, indentLevel, "RightImage", "Set Image Sky Right Image");
            return true;
        }

        if (action.Type.Equals("SetImageSkyFrontImage", StringComparison.OrdinalIgnoreCase))
        {
            AppendImageSkyImageAction(builder, rule, action, plan, nodesById, indentLevel, "FrontImage", "Set Image Sky Front Image");
            return true;
        }

        if (action.Type.Equals("SetImageSkyBackImage", StringComparison.OrdinalIgnoreCase))
        {
            AppendImageSkyImageAction(builder, rule, action, plan, nodesById, indentLevel, "BackImage", "Set Image Sky Back Image");
            return true;
        }

        if (action.Type.Equals("SetFogEnabled", StringComparison.OrdinalIgnoreCase))
        {
            var enabled = ParameterExpression(rule, action, nodesById, "enabled", "Boolean", "true");
            AppendLightingPropertyAction(builder, indentLevel, "FogEnabled", enabled.Code, "Set Fog Enabled");
            return true;
        }

        if (action.Type.Equals("SetFogColor", StringComparison.OrdinalIgnoreCase))
        {
            AppendLightingColorAction(builder, rule, action, nodesById, indentLevel, "FogColor", "Set Fog Color");
            return true;
        }

        if (action.Type.Equals("SetAmbientColor", StringComparison.OrdinalIgnoreCase))
        {
            AppendLightingColorAction(builder, rule, action, nodesById, indentLevel, "AmbientColor", "Set Ambient Color");
            return true;
        }

        if (action.Type.Equals("SetFogDistances", StringComparison.OrdinalIgnoreCase))
        {
            var indent = IndentText(indentLevel);
            var start = ParameterExpression(rule, action, nodesById, "start", "Number", "25");
            var end = ParameterExpression(rule, action, nodesById, "end", "Number", "150");
            AppendLightingAvailabilityGuard(builder, indentLevel, "Set Fog Distances");
            builder.AppendLine($"{indent}Lighting.FogStartDistance = {start.Code}");
            builder.AppendLine($"{indent}Lighting.FogEndDistance = {end.Code}");
            return true;
        }

        return false;
    }

    private static bool TryAppendReadableAudioLightingConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (condition.Type.Equals("SoundIsPlaying", StringComparison.OrdinalIgnoreCase))
        {
            AppendObjectBooleanPropertyCondition(builder, condition, plan, indentLevel, "soundObject", "Playing");
            return true;
        }

        if (condition.Type.Equals("SoundIsLooping", StringComparison.OrdinalIgnoreCase))
        {
            AppendObjectBooleanPropertyCondition(builder, condition, plan, indentLevel, "soundObject", "Loop");
            return true;
        }

        if (condition.Type.Equals("SoundVolumeAtLeast", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("SoundVolumeAtMost", StringComparison.OrdinalIgnoreCase))
        {
            var comparison = condition.Type.Equals("SoundVolumeAtLeast", StringComparison.OrdinalIgnoreCase) ? ">=" : "<=";
            AppendObjectNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "soundObject", "Volume", "volume", "1", comparison);
            return true;
        }

        if (condition.Type.Equals("LightBrightnessAtLeast", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("LightBrightnessAtMost", StringComparison.OrdinalIgnoreCase))
        {
            var comparison = condition.Type.Equals("LightBrightnessAtLeast", StringComparison.OrdinalIgnoreCase) ? ">=" : "<=";
            AppendObjectNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "lightObject", "Brightness", "brightness", "1", comparison);
            return true;
        }

        if (condition.Type.Equals("LightShadowsEnabled", StringComparison.OrdinalIgnoreCase))
        {
            AppendObjectBooleanPropertyCondition(builder, condition, plan, indentLevel, "lightObject", "Shadows");
            return true;
        }

        if (condition.Type.Equals("SunLightBrightnessAtLeast", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("SunLightBrightnessAtMost", StringComparison.OrdinalIgnoreCase))
        {
            var comparison = condition.Type.Equals("SunLightBrightnessAtLeast", StringComparison.OrdinalIgnoreCase) ? ">=" : "<=";
            AppendObjectNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "sunLightObject", "Brightness", "brightness", "1", comparison);
            return true;
        }

        if (condition.Type.Equals("SunLightShadowsEnabled", StringComparison.OrdinalIgnoreCase))
        {
            AppendObjectBooleanPropertyCondition(builder, condition, plan, indentLevel, "sunLightObject", "Shadows");
            return true;
        }

        if (condition.Type.Equals("FogIsEnabled", StringComparison.OrdinalIgnoreCase))
        {
            AppendLightingBooleanPropertyCondition(builder, indentLevel, "FogEnabled");
            return true;
        }

        if (condition.Type.Equals("FogStartDistanceAtLeast", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("FogStartDistanceAtMost", StringComparison.OrdinalIgnoreCase))
        {
            var comparison = condition.Type.Equals("FogStartDistanceAtLeast", StringComparison.OrdinalIgnoreCase) ? ">=" : "<=";
            AppendLightingNumberPropertyCondition(builder, rule, condition, nodesById, indentLevel, "FogStartDistance", "distance", "25", comparison);
            return true;
        }

        if (condition.Type.Equals("FogEndDistanceAtLeast", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("FogEndDistanceAtMost", StringComparison.OrdinalIgnoreCase))
        {
            var comparison = condition.Type.Equals("FogEndDistanceAtLeast", StringComparison.OrdinalIgnoreCase) ? ">=" : "<=";
            AppendLightingNumberPropertyCondition(builder, rule, condition, nodesById, indentLevel, "FogEndDistance", "distance", "150", comparison);
            return true;
        }

        if (TryGetImageSkyProperty(condition.Type, out var imageSkyPropertyName))
        {
            AppendImageSkyStringPropertyEqualsCondition(builder, rule, condition, plan, nodesById, indentLevel, imageSkyPropertyName);
            return true;
        }

        if (TryGetProceduralSkyNumberCondition(condition.Type, out var proceduralNumber))
        {
            AppendProceduralSkyNumberCondition(builder, rule, condition, plan, nodesById, indentLevel, proceduralNumber);
            return true;
        }

        if (TryGetProceduralSkyColorCondition(condition.Type, out var proceduralColorProperty))
        {
            AppendProceduralSkyColorCondition(builder, rule, condition, plan, nodesById, indentLevel, proceduralColorProperty);
            return true;
        }

        return false;
    }

    private static LuauExpression? TryResolveReadableAudioLightingPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "SoundVolume" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Volume", "Number", "Sound Volume", "return tonumber(targetObject.Volume) or 0"),
            "SoundIsPlaying" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Playing", "Boolean", "Sound Is Playing", "return targetObject.Playing == true"),
            "SoundLength" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Length", "Number", "Sound Length", "return tonumber(targetObject.Length) or 0"),
            "SoundTime" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Time", "Number", "Sound Time", "return tonumber(targetObject.Time) or 0"),
            "SoundAudioValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Audio", "Any", "Sound Audio", "return targetObject.Audio"),
            "LightColor" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Color", "Color", "Light Color", "return targetObject.Color"),
            "LightBrightness" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Brightness", "Number", "Light Brightness", "return tonumber(targetObject.Brightness) or 0"),
            "LightShine" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Specular", "Number", "Light Shine", "return tonumber(targetObject.Specular) or 0"),
            "LightShadows" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Shadows", "Boolean", "Light Shadows", "return targetObject.Shadows == true"),
            "SunLightColorValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Color", "Color", "Sun Light Color", "return targetObject.Color"),
            "SunLightBrightnessValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Brightness", "Number", "Sun Light Brightness", "return tonumber(targetObject.Brightness) or 0"),
            "SunLightShineValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Specular", "Number", "Sun Light Shine", "return tonumber(targetObject.Specular) or 0"),
            "SunLightShadowsValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Shadows", "Boolean", "Sun Light Shadows", "return targetObject.Shadows == true"),
            "PointLightRange" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Range", "Number", "Point Light Range", "return tonumber(targetObject.Range) or 0"),
            "SpotLightRange" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Range", "Number", "Spot Light Range", "return tonumber(targetObject.Range) or 0"),
            "SpotLightAngle" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Angle", "Number", "Spot Light Angle", "return tonumber(targetObject.Angle) or 0"),
            "ColorAdjustBrightnessValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Brightness", "Number", "Color Adjust Brightness", "return tonumber(targetObject.Brightness) or 0"),
            "ColorAdjustContrastValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Contrast", "Number", "Color Adjust Contrast", "return tonumber(targetObject.Contrast) or 0"),
            "ColorAdjustSaturationValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Saturation", "Number", "Color Adjust Saturation", "return tonumber(targetObject.Saturation) or 0"),
            "ColorAdjustTintValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "TintColor", "Color", "Color Adjust Tint", "return targetObject.TintColor"),
            "ProceduralSkySunSizeValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "SunSize", "Number", "Procedural Sky Sun Size", "return tonumber(targetObject.SunSize) or 0"),
            "ProceduralSkyTintValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "SkyTint", "Color", "Procedural Sky Tint", "return targetObject.SkyTint"),
            "ProceduralSkyHorizonColorValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "HorizonColor", "Color", "Procedural Sky Horizon Color", "return targetObject.HorizonColor"),
            "ProceduralSkyGroundColorValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "GroundColor", "Color", "Procedural Sky Ground Color", "return targetObject.GroundColor"),
            "ProceduralSkyExposureValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Exposure", "Number", "Procedural Sky Exposure", "return tonumber(targetObject.Exposure) or 0"),
            "GradientSkyTopColorValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "SkyGradientTop", "Color", "Gradient Sky Top Color", "return targetObject.SkyGradientTop"),
            "GradientSkyBottomColorValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "SkyGradientBottom", "Color", "Gradient Sky Bottom Color", "return targetObject.SkyGradientBottom"),
            "GradientSkyExponentValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "SkyGradientExponent", "Number", "Gradient Sky Exponent", "return tonumber(targetObject.SkyGradientExponent) or 0"),
            "GradientSkySunDiscColorValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "SunDiscColor", "Color", "Gradient Sky Sun Disc Color", "return targetObject.SunDiscColor"),
            "GradientSkySunDiscMultiplierValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "SunDiscMultiplier", "Number", "Gradient Sky Sun Disc Multiplier", "return tonumber(targetObject.SunDiscMultiplier) or 0"),
            "GradientSkySunDiscExponentValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "SunDiscExponent", "Number", "Gradient Sky Sun Disc Exponent", "return tonumber(targetObject.SunDiscExponent) or 0"),
            "GradientSkySunHaloColorValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "SunHaloColor", "Color", "Gradient Sky Sun Halo Color", "return targetObject.SunHaloColor"),
            "GradientSkySunHaloExponentValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "SunHaloExponent", "Number", "Gradient Sky Sun Halo Exponent", "return tonumber(targetObject.SunHaloExponent) or 0"),
            "GradientSkySunHaloContributionValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "SunHaloContribution", "Number", "Gradient Sky Sun Halo Contribution", "return tonumber(targetObject.SunHaloContribution) or 0"),
            "GradientSkyHorizonLineColorValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "HorizonLineColor", "Color", "Gradient Sky Horizon Line Color", "return targetObject.HorizonLineColor"),
            "GradientSkyHorizonLineExponentValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "HorizonLineExponent", "Number", "Gradient Sky Horizon Line Exponent", "return tonumber(targetObject.HorizonLineExponent) or 0"),
            "GradientSkyHorizonLineContributionValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "HorizonLineContribution", "Number", "Gradient Sky Horizon Line Contribution", "return tonumber(targetObject.HorizonLineContribution) or 0"),
            "ImageSkyTopImageValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "TopImage", "String", "Image Sky Top Image", "return tostring(targetObject.TopImage or \"\")"),
            "ImageSkyBottomImageValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "BottomImage", "String", "Image Sky Bottom Image", "return tostring(targetObject.BottomImage or \"\")"),
            "ImageSkyLeftImageValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "LeftImage", "String", "Image Sky Left Image", "return tostring(targetObject.LeftImage or \"\")"),
            "ImageSkyRightImageValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "RightImage", "String", "Image Sky Right Image", "return tostring(targetObject.RightImage or \"\")"),
            "ImageSkyFrontImageValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "FrontImage", "String", "Image Sky Front Image", "return tostring(targetObject.FrontImage or \"\")"),
            "ImageSkyBackImageValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "BackImage", "String", "Image Sky Back Image", "return tostring(targetObject.BackImage or \"\")"),
            "FogEnabled" => LightingValueExpression("FogEnabled", "Boolean", "Fog Enabled", "return Lighting.FogEnabled == true"),
            "FogStartDistance" => LightingValueExpression("FogStartDistance", "Number", "Fog Start Distance", "return tonumber(Lighting.FogStartDistance) or 0"),
            "FogEndDistance" => LightingValueExpression("FogEndDistance", "Number", "Fog End Distance", "return tonumber(Lighting.FogEndDistance) or 0"),
            "AmbientColor" => LightingValueExpression("AmbientColor", "Color", "Ambient Color", "return Lighting.AmbientColor"),
            _ => null
        };
    }

    private static void AppendReadableSoundLoadedTrigger(
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
        builder.AppendLine("        print(\"On Sound Loaded trigger stopped: target has no Loaded event.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    triggerObject.Loaded:Connect(function()");
        builder.AppendLine("        local triggerContext = { object = triggerObject, sound = triggerObject }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
        if (!emitted)
        {
            builder.AppendLine("        print(\"On Sound Loaded trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("    end)");
        builder.AppendLine("end");
    }

    private static void AppendReadableAudioLightingWatcherTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");
        var definition = AudioLightingWatcherDefinition.For(trigger.Type);

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        if (definition.UsesLighting)
        {
            builder.AppendLine("    if Lighting == nil then");
            builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: Lighting is not available.\")");
            builder.AppendLine("        return");
            builder.AppendLine("    end");
            builder.AppendLine($"    if Lighting.{definition.PropertyName} == nil then");
            builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: Lighting does not expose {definition.PropertyName}.\")");
            builder.AppendLine("        return");
            builder.AppendLine("    end");
        }
        else
        {
            builder.AppendLine($"    if triggerObject.{definition.PropertyName} == nil then");
            builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target does not expose {definition.PropertyName}.\")");
            builder.AppendLine("        return");
            builder.AppendLine("    end");
        }

        if (definition.ParameterKey is not null)
        {
            var limit = ParameterExpression(rule, trigger, nodesById, definition.ParameterKey, "Number", definition.FallbackLimit);
            builder.AppendLine($"    local watchedLimit = tonumber({limit.Code}) or {definition.FallbackLimit}");
        }

        builder.AppendLine("    local function readMatched()");
        var propertyExpression = definition.UsesLighting
            ? $"Lighting.{definition.PropertyName}"
            : $"triggerObject.{definition.PropertyName}";
        if (definition.ParameterKey is null)
        {
            var expectedBoolean = definition.ExpectedBoolean ? "true" : "false";
            builder.AppendLine($"        local currentValue = {propertyExpression} == true");
            builder.AppendLine($"        return currentValue == {expectedBoolean}, currentValue");
        }
        else
        {
            builder.AppendLine($"        local currentValue = tonumber({propertyExpression}) or {definition.FallbackCurrent}");
            builder.AppendLine($"        return currentValue {definition.Comparison} watchedLimit, currentValue");
        }

        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, $", {definition.ContextField} = currentValue", 1);
        builder.AppendLine("end");
    }

    private static void AppendReadableImageSkyWatcherTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");
        var propertyName = ImageSkyWatcherProperty(trigger.Type);

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    if triggerObject.{propertyName} == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target does not expose {propertyName}.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    local function readWatchedValue()");
        builder.AppendLine($"        return tostring(triggerObject.{propertyName} or \"\")");
        builder.AppendLine("    end");
        AppendReadableAnyChangeLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, ", imageSkyImage = currentValue", 1);
        builder.AppendLine("end");
    }

    private static void AppendReadableProceduralSkyWatcherTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");
        var definition = ProceduralSkyWatcherDefinition.For(trigger.Type);

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    if triggerObject.{definition.PropertyName} == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target does not expose {definition.PropertyName}.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        if (definition.TriggersOnChange)
        {
            builder.AppendLine("    local function readWatchedValue()");
            builder.AppendLine($"        return triggerObject.{definition.PropertyName}");
            builder.AppendLine("    end");
            AppendReadableAnyChangeLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, $", {definition.ContextField} = currentValue", 1);
            builder.AppendLine("end");
            return;
        }

        var limit = ParameterExpression(rule, trigger, nodesById, definition.ParameterKey, "Number", definition.FallbackLimit);
        builder.AppendLine($"    local watchedLimit = tonumber({limit.Code}) or {definition.FallbackLimit}");
        builder.AppendLine("    local function readMatched()");
        builder.AppendLine($"        local currentValue = tonumber(triggerObject.{definition.PropertyName}) or {definition.FallbackCurrent}");
        builder.AppendLine("        return currentValue >= watchedLimit, currentValue");
        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, $", {definition.ContextField} = currentValue", 1);
        builder.AppendLine("end");
    }

    private static void AppendSoundMethodAction(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode action,
        int indentLevel,
        string methodName,
        string? argumentCode,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "soundObject");
        AppendSoundGuard(builder, indentLevel, methodName, readableName);
        if (string.IsNullOrWhiteSpace(argumentCode))
        {
            builder.AppendLine($"{indent}soundObject:{methodName}()");
        }
        else
        {
            builder.AppendLine($"{indent}soundObject:{methodName}({argumentCode})");
        }
    }

    private static void AppendSoundPropertyAction(
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
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "soundObject");
        builder.AppendLine($"{indent}if soundObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target sound was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if soundObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}soundObject.{propertyName} = {value.Code}");
    }

    private static void AppendSoundGuard(StringBuilder builder, int indentLevel, string methodName, string readableName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if soundObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target sound was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if soundObject.{methodName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not support {methodName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendObjectLightColorAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        var color = ColorExpression(rule, action, nodesById);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "lightObject");
        AppendObjectLightPropertyGuard(builder, indentLevel, "Color", readableName);
        builder.AppendLine($"{indent}lightObject.Color = {color.Code}");
    }

    private static void AppendObjectLightPropertyAction(
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
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "lightObject");
        AppendObjectLightPropertyGuard(builder, indentLevel, propertyName, readableName);
        builder.AppendLine($"{indent}lightObject.{propertyName} = {value.Code}");
    }

    private static void AppendObjectLightPropertyGuard(StringBuilder builder, int indentLevel, string propertyName, string readableName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if lightObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target light was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if lightObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendColorAdjustTintAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        var color = ColorExpression(rule, action, nodesById);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "modifierObject");
        AppendColorAdjustPropertyGuard(builder, indentLevel, "TintColor", readableName);
        builder.AppendLine($"{indent}modifierObject.TintColor = {color.Code}");
    }

    private static void AppendColorAdjustPropertyAction(
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
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "modifierObject");
        AppendColorAdjustPropertyGuard(builder, indentLevel, propertyName, readableName);
        builder.AppendLine($"{indent}modifierObject.{propertyName} = {value.Code}");
    }

    private static void AppendColorAdjustPropertyGuard(StringBuilder builder, int indentLevel, string propertyName, string readableName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if modifierObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target color adjust modifier was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if modifierObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendProceduralSkyColorAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        var color = ColorExpression(rule, action, nodesById);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "skyObject");
        AppendProceduralSkyPropertyGuard(builder, indentLevel, propertyName, readableName);
        builder.AppendLine($"{indent}skyObject.{propertyName} = {color.Code}");
    }

    private static void AppendProceduralSkyPropertyAction(
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
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "skyObject");
        AppendProceduralSkyPropertyGuard(builder, indentLevel, propertyName, readableName);
        builder.AppendLine($"{indent}skyObject.{propertyName} = {value.Code}");
    }

    private static void AppendProceduralSkyPropertyGuard(StringBuilder builder, int indentLevel, string propertyName, string readableName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if skyObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target procedural sky was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if skyObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendProceduralSkyNumberCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        ProceduralSkyNumberCondition definition)
    {
        var indent = IndentText(indentLevel);
        var expected = ParameterExpression(rule, condition, nodesById, definition.ParameterKey, "Number", definition.FallbackLimit);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "skyObject");
        builder.AppendLine($"{indent}if skyObject == nil or skyObject.{definition.PropertyName} == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local currentValue = tonumber(skyObject.{definition.PropertyName}) or {definition.FallbackCurrent}");
        builder.AppendLine($"{indent}local expectedValue = tonumber({expected.Code}) or {definition.FallbackLimit}");
        builder.AppendLine($"{indent}return currentValue >= expectedValue");
    }

    private static void AppendProceduralSkyColorCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName)
    {
        var indent = IndentText(indentLevel);
        var expectedColor = ColorExpression(rule, condition, nodesById);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "skyObject");
        builder.AppendLine($"{indent}if skyObject == nil or skyObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local expectedColor = {expectedColor.Code}");
        builder.AppendLine($"{indent}return skyObject.{propertyName} == expectedColor");
    }

    private static bool TryGetProceduralSkyNumberCondition(string conditionType, out ProceduralSkyNumberCondition definition)
    {
        if (conditionType.Equals("ProceduralSkySunSizeAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            definition = new("SunSize", "size", "1", "0");
            return true;
        }

        if (conditionType.Equals("ProceduralSkyExposureAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            definition = new("Exposure", "exposure", "1", "0");
            return true;
        }

        definition = default!;
        return false;
    }

    private static bool TryGetProceduralSkyColorCondition(string conditionType, out string propertyName)
    {
        propertyName = conditionType switch
        {
            "ProceduralSkyTintIs" => "SkyTint",
            "ProceduralSkyHorizonColorIs" => "HorizonColor",
            "ProceduralSkyGroundColorIs" => "GroundColor",
            _ => string.Empty
        };

        return propertyName.Length > 0;
    }

    private static void AppendGradientSkyColorsAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var topColor = ColorExpressionFromKeys(rule, action, nodesById, "topR", "topG", "topB", "0.2", "0.5", "1");
        var bottomColor = ColorExpressionFromKeys(rule, action, nodesById, "bottomR", "bottomG", "bottomB", "1", "0.8", "0.4");
        var exponent = ParameterExpression(rule, action, nodesById, "exponent", "Number", "1");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "skyObject");
        AppendGradientSkyPropertyGuard(builder, indentLevel, "SkyGradientTop", "Set Gradient Sky Colors");
        AppendGradientSkyPropertyGuard(builder, indentLevel, "SkyGradientBottom", "Set Gradient Sky Colors");
        AppendGradientSkyPropertyGuard(builder, indentLevel, "SkyGradientExponent", "Set Gradient Sky Colors");
        builder.AppendLine($"{indent}skyObject.SkyGradientTop = {topColor.Code}");
        builder.AppendLine($"{indent}skyObject.SkyGradientBottom = {bottomColor.Code}");
        builder.AppendLine($"{indent}skyObject.SkyGradientExponent = {exponent.Code}");
    }

    private static void AppendGradientSkyColorNumbersAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string colorPropertyName,
        string firstNumberPropertyName,
        string firstNumberParameterKey,
        string secondNumberPropertyName,
        string secondNumberParameterKey,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        var color = ColorExpression(rule, action, nodesById);
        var firstValue = ParameterExpression(rule, action, nodesById, firstNumberParameterKey, "Number", "1");
        var secondValue = ParameterExpression(rule, action, nodesById, secondNumberParameterKey, "Number", "1");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "skyObject");
        AppendGradientSkyPropertyGuard(builder, indentLevel, colorPropertyName, readableName);
        AppendGradientSkyPropertyGuard(builder, indentLevel, firstNumberPropertyName, readableName);
        AppendGradientSkyPropertyGuard(builder, indentLevel, secondNumberPropertyName, readableName);
        builder.AppendLine($"{indent}skyObject.{colorPropertyName} = {color.Code}");
        builder.AppendLine($"{indent}skyObject.{firstNumberPropertyName} = {firstValue.Code}");
        builder.AppendLine($"{indent}skyObject.{secondNumberPropertyName} = {secondValue.Code}");
    }

    private static void AppendGradientSkyPropertyGuard(StringBuilder builder, int indentLevel, string propertyName, string readableName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if skyObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target gradient sky was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if skyObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendImageSkyAllImagesAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var top = ParameterExpression(rule, action, nodesById, "topImage", "String", "");
        var bottom = ParameterExpression(rule, action, nodesById, "bottomImage", "String", "");
        var left = ParameterExpression(rule, action, nodesById, "leftImage", "String", "");
        var right = ParameterExpression(rule, action, nodesById, "rightImage", "String", "");
        var front = ParameterExpression(rule, action, nodesById, "frontImage", "String", "");
        var back = ParameterExpression(rule, action, nodesById, "backImage", "String", "");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "skyObject");
        AppendImageSkyPropertyGuard(builder, indentLevel, "TopImage", "Set Image Sky Images");
        AppendImageSkyPropertyGuard(builder, indentLevel, "BottomImage", "Set Image Sky Images");
        AppendImageSkyPropertyGuard(builder, indentLevel, "LeftImage", "Set Image Sky Images");
        AppendImageSkyPropertyGuard(builder, indentLevel, "RightImage", "Set Image Sky Images");
        AppendImageSkyPropertyGuard(builder, indentLevel, "FrontImage", "Set Image Sky Images");
        AppendImageSkyPropertyGuard(builder, indentLevel, "BackImage", "Set Image Sky Images");
        builder.AppendLine($"{indent}skyObject.TopImage = {top.Code}");
        builder.AppendLine($"{indent}skyObject.BottomImage = {bottom.Code}");
        builder.AppendLine($"{indent}skyObject.LeftImage = {left.Code}");
        builder.AppendLine($"{indent}skyObject.RightImage = {right.Code}");
        builder.AppendLine($"{indent}skyObject.FrontImage = {front.Code}");
        builder.AppendLine($"{indent}skyObject.BackImage = {back.Code}");
    }

    private static void AppendImageSkyImageAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        var image = ParameterExpression(rule, action, nodesById, "image", "String", "");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "skyObject");
        AppendImageSkyPropertyGuard(builder, indentLevel, propertyName, readableName);
        builder.AppendLine($"{indent}skyObject.{propertyName} = {image.Code}");
    }

    private static void AppendImageSkyPropertyGuard(StringBuilder builder, int indentLevel, string propertyName, string readableName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if skyObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target image sky was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if skyObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendImageSkyStringPropertyEqualsCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName)
    {
        var indent = IndentText(indentLevel);
        var expectedImage = ParameterExpression(rule, condition, nodesById, "image", "String", "");
        var caseSensitive = ParameterExpression(rule, condition, nodesById, "caseSensitive", "Boolean", "false");
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local currentText = tostring(targetObject.{propertyName} or \"\")");
        builder.AppendLine($"{indent}local expectedText = tostring({expectedImage.Code})");
        builder.AppendLine($"{indent}if {caseSensitive.Code} then");
        builder.AppendLine($"{indent}    return currentText == expectedText");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return string.lower(currentText) == string.lower(expectedText)");
    }

    private static bool TryGetImageSkyProperty(string conditionType, out string propertyName)
    {
        propertyName = conditionType switch
        {
            "ImageSkyTopImageIs" => "TopImage",
            "ImageSkyBottomImageIs" => "BottomImage",
            "ImageSkyLeftImageIs" => "LeftImage",
            "ImageSkyRightImageIs" => "RightImage",
            "ImageSkyFrontImageIs" => "FrontImage",
            "ImageSkyBackImageIs" => "BackImage",
            _ => ""
        };
        return propertyName.Length > 0;
    }

    private static string ImageSkyWatcherProperty(string triggerType)
    {
        return triggerType switch
        {
            "OnImageSkyTopImageChanged" => "TopImage",
            "OnImageSkyBottomImageChanged" => "BottomImage",
            "OnImageSkyLeftImageChanged" => "LeftImage",
            "OnImageSkyRightImageChanged" => "RightImage",
            "OnImageSkyFrontImageChanged" => "FrontImage",
            "OnImageSkyBackImageChanged" => "BackImage",
            _ => throw new ArgumentOutOfRangeException(nameof(triggerType), triggerType, "Unknown image sky watcher trigger.")
        };
    }

    private static LuauExpression ColorExpressionFromKeys(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        string redKey,
        string greenKey,
        string blueKey,
        string redFallback,
        string greenFallback,
        string blueFallback)
    {
        var red = ParameterExpression(rule, node, nodesById, redKey, "Number", redFallback);
        var green = ParameterExpression(rule, node, nodesById, greenKey, "Number", greenFallback);
        var blue = ParameterExpression(rule, node, nodesById, blueKey, "Number", blueFallback);
        return new LuauExpression($"Color.New({red.Code}, {green.Code}, {blue.Code}, 1)", "Color");
    }

    private static void AppendObjectBooleanPropertyCondition(
        StringBuilder builder,
        RuleNode condition,
        ReadableExportPlan plan,
        int indentLevel,
        string variableName,
        string propertyName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, variableName);
        builder.AppendLine($"{indent}if {variableName} == nil or {variableName}.{propertyName} == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return {variableName}.{propertyName} == true");
    }

    private static void AppendObjectNumberPropertyCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string variableName,
        string propertyName,
        string parameterKey,
        string fallback,
        string comparison)
    {
        var indent = IndentText(indentLevel);
        var threshold = ParameterExpression(rule, condition, nodesById, parameterKey, "Number", fallback);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, variableName);
        builder.AppendLine($"{indent}if {variableName} == nil or {variableName}.{propertyName} == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return (tonumber({variableName}.{propertyName}) or 0) {comparison} {threshold.Code}");
    }

    private static void AppendLightingBooleanPropertyCondition(
        StringBuilder builder,
        int indentLevel,
        string propertyName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if Lighting == nil or Lighting.{propertyName} == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return Lighting.{propertyName} == true");
    }

    private static void AppendLightingNumberPropertyCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string parameterKey,
        string fallback,
        string comparison)
    {
        var indent = IndentText(indentLevel);
        var threshold = ParameterExpression(rule, condition, nodesById, parameterKey, "Number", fallback);
        builder.AppendLine($"{indent}if Lighting == nil or Lighting.{propertyName} == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return (tonumber(Lighting.{propertyName}) or 0) {comparison} {threshold.Code}");
    }

    private static void AppendLightingPropertyAction(
        StringBuilder builder,
        int indentLevel,
        string propertyName,
        string valueCode,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        AppendLightingAvailabilityGuard(builder, indentLevel, readableName);
        builder.AppendLine($"{indent}Lighting.{propertyName} = {valueCode}");
    }

    private static void AppendLightingColorAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        var color = ColorExpression(rule, action, nodesById);
        AppendLightingAvailabilityGuard(builder, indentLevel, readableName);
        builder.AppendLine($"{indent}Lighting.{propertyName} = {color.Code}");
    }

    private static void AppendLightingAvailabilityGuard(StringBuilder builder, int indentLevel, string readableName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if Lighting == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: Lighting is not available.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private static LuauExpression LightingValueExpression(
        string propertyName,
        string dataType,
        string readableName,
        string returnStatement)
    {
        var fallback = NormalizeExpressionDataType(dataType) switch
        {
            "Number" => "0",
            "Boolean" => "false",
            "Color" => "Color.New(1, 1, 1, 1)",
            _ => "nil"
        };
        var code = $"(function() if Lighting == nil then print(\"{readableName} stopped: Lighting is not available.\"); return {fallback} end; if Lighting.{propertyName} == nil then print(\"{readableName} stopped: Lighting does not expose {propertyName}.\"); return {fallback} end; {returnStatement} end)()";
        return new LuauExpression(code, dataType);
    }

    private readonly record struct ProceduralSkyNumberCondition(
        string PropertyName,
        string ParameterKey,
        string FallbackLimit,
        string FallbackCurrent);

    private sealed record ProceduralSkyWatcherDefinition(
        string PropertyName,
        string ParameterKey,
        string FallbackLimit,
        string FallbackCurrent,
        string ContextField,
        bool TriggersOnChange)
    {
        public static ProceduralSkyWatcherDefinition For(string triggerType)
        {
            return triggerType switch
            {
                "OnProceduralSkySunSizeReached" => Number("SunSize", "size", "1", "0", "proceduralSkySunSize"),
                "OnProceduralSkyTintChanged" => Changed("SkyTint", "proceduralSkyTint"),
                "OnProceduralSkyHorizonColorChanged" => Changed("HorizonColor", "proceduralSkyHorizonColor"),
                "OnProceduralSkyGroundColorChanged" => Changed("GroundColor", "proceduralSkyGroundColor"),
                "OnProceduralSkyExposureReached" => Number("Exposure", "exposure", "1", "0", "proceduralSkyExposure"),
                _ => throw new ArgumentOutOfRangeException(nameof(triggerType), triggerType, "Unknown procedural sky watcher trigger.")
            };
        }

        private static ProceduralSkyWatcherDefinition Number(
            string propertyName,
            string parameterKey,
            string fallbackLimit,
            string fallbackCurrent,
            string contextField)
            => new(propertyName, parameterKey, fallbackLimit, fallbackCurrent, contextField, TriggersOnChange: false);

        private static ProceduralSkyWatcherDefinition Changed(string propertyName, string contextField)
            => new(propertyName, string.Empty, "0", "0", contextField, TriggersOnChange: true);
    }

    private sealed record AudioLightingWatcherDefinition(
        bool UsesLighting,
        string PropertyName,
        string? ParameterKey,
        string FallbackLimit,
        string FallbackCurrent,
        string Comparison,
        bool ExpectedBoolean,
        string ContextField)
    {
        public static AudioLightingWatcherDefinition For(string triggerType)
        {
            return triggerType switch
            {
                "OnSoundStarted" => BooleanTarget("Playing", true, "soundPlaying"),
                "OnSoundStopped" => BooleanTarget("Playing", false, "soundPlaying"),
                "OnSoundVolumeReached" => NumberTarget("Volume", "volume", "1", "0", ">=", "soundVolume"),
                "OnSoundVolumeDroppedTo" => NumberTarget("Volume", "volume", "0.25", "0", "<=", "soundVolume"),
                "OnFogEnabled" => BooleanLighting("FogEnabled", true, "fogEnabled"),
                "OnFogDisabled" => BooleanLighting("FogEnabled", false, "fogEnabled"),
                "OnFogStartDistanceReached" => NumberLighting("FogStartDistance", "distance", "25", "0", ">=", "fogDistance"),
                "OnFogStartDistanceDroppedTo" => NumberLighting("FogStartDistance", "distance", "25", "0", "<=", "fogDistance"),
                "OnFogEndDistanceReached" => NumberLighting("FogEndDistance", "distance", "150", "0", ">=", "fogDistance"),
                "OnFogEndDistanceDroppedTo" => NumberLighting("FogEndDistance", "distance", "150", "0", "<=", "fogDistance"),
                "OnLightBrightnessReached" => NumberTarget("Brightness", "brightness", "1", "0", ">=", "lightBrightness"),
                "OnLightBrightnessDroppedTo" => NumberTarget("Brightness", "brightness", "0.25", "0", "<=", "lightBrightness"),
                "OnLightShadowsEnabled" => BooleanTarget("Shadows", true, "lightShadows"),
                "OnLightShadowsDisabled" => BooleanTarget("Shadows", false, "lightShadows"),
                "OnSunLightBrightnessReached" => NumberTarget("Brightness", "brightness", "1", "0", ">=", "sunLightBrightness"),
                "OnSunLightBrightnessDroppedTo" => NumberTarget("Brightness", "brightness", "0.25", "0", "<=", "sunLightBrightness"),
                "OnSunLightShadowsEnabled" => BooleanTarget("Shadows", true, "sunLightShadows"),
                "OnSunLightShadowsDisabled" => BooleanTarget("Shadows", false, "sunLightShadows"),
                _ => throw new ArgumentOutOfRangeException(nameof(triggerType), triggerType, "Unknown audio or lighting watcher trigger.")
            };
        }

        private static AudioLightingWatcherDefinition BooleanTarget(string propertyName, bool expected, string contextField)
        {
            return new AudioLightingWatcherDefinition(false, propertyName, null, "0", "0", "==", expected, contextField);
        }

        private static AudioLightingWatcherDefinition BooleanLighting(string propertyName, bool expected, string contextField)
        {
            return new AudioLightingWatcherDefinition(true, propertyName, null, "0", "0", "==", expected, contextField);
        }

        private static AudioLightingWatcherDefinition NumberTarget(string propertyName, string parameterKey, string fallbackLimit, string fallbackCurrent, string comparison, string contextField)
        {
            return new AudioLightingWatcherDefinition(false, propertyName, parameterKey, fallbackLimit, fallbackCurrent, comparison, false, contextField);
        }

        private static AudioLightingWatcherDefinition NumberLighting(string propertyName, string parameterKey, string fallbackLimit, string fallbackCurrent, string comparison, string contextField)
        {
            return new AudioLightingWatcherDefinition(true, propertyName, parameterKey, fallbackLimit, fallbackCurrent, comparison, false, contextField);
        }
    }
}
