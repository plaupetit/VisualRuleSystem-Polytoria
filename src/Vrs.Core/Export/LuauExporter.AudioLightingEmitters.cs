using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
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
            "LightColor" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Color", "Color", "Light Color", "return targetObject.Color"),
            "LightBrightness" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Brightness", "Number", "Light Brightness", "return tonumber(targetObject.Brightness) or 0"),
            "LightShine" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Specular", "Number", "Light Shine", "return tonumber(targetObject.Specular) or 0"),
            "LightShadows" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Shadows", "Boolean", "Light Shadows", "return targetObject.Shadows == true"),
            "PointLightRange" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Range", "Number", "Point Light Range", "return tonumber(targetObject.Range) or 0"),
            "SpotLightRange" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Range", "Number", "Spot Light Range", "return tonumber(targetObject.Range) or 0"),
            "SpotLightAngle" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Angle", "Number", "Spot Light Angle", "return tonumber(targetObject.Angle) or 0"),
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
}
