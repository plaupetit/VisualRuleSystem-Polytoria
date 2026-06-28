using System.Globalization;
using Vrs.Graph.Model;

namespace Vrs.Core.Catalog;

public sealed partial class NodeCatalogService
{
    public static bool IsSceneObjectParameter(NodeCatalogParameterDefinition parameter)
    {
        return ContainsToken(parameter.Control, "SceneObject") ||
            ContainsToken(parameter.Type, "Object") ||
            ContainsToken(parameter.ValueSource, "Scene Object") ||
            ContainsToken(parameter.ValueSource, "Target Context");
    }

    public static bool IsTriggeringPlayerParameter(NodeCatalogParameterDefinition parameter)
    {
        return parameter.Key.Equals("player", StringComparison.OrdinalIgnoreCase) &&
            ContainsToken(parameter.ValueSource, "Trigger Context");
    }

    private static string NormalizeDataType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Any";
        }

        if (value.Contains("Quaternion", StringComparison.OrdinalIgnoreCase))
        {
            return "Quaternion";
        }

        if (value.Contains("ColorSeries", StringComparison.OrdinalIgnoreCase))
        {
            return "ColorSeries";
        }

        if (IsVectorDataType(value))
        {
            return "Vector3";
        }

        if (value.Contains("Color", StringComparison.OrdinalIgnoreCase))
        {
            return "Color";
        }

        if (IsSceneObjectDataType(value))
        {
            return "SceneObject";
        }

        if (value.Equals("Bool", StringComparison.OrdinalIgnoreCase))
        {
            return "Boolean";
        }

        if (value.Contains("Number", StringComparison.OrdinalIgnoreCase))
        {
            return "Number";
        }

        if (value.Contains("String", StringComparison.OrdinalIgnoreCase) || value.Contains("Text", StringComparison.OrdinalIgnoreCase))
        {
            return "String";
        }

        if (value.Contains("Boolean", StringComparison.OrdinalIgnoreCase))
        {
            return "Boolean";
        }

        return value;
    }

    private static string ColorForDataType(string dataType, string fallback)
    {
        return dataType switch
        {
            "String" => "#c084fc",
            "Number" => "#f59e0b",
            "Boolean" => "#22c55e",
            "SceneObject" => "#38bdf8",
            "Quaternion" => "#a78bfa",
            "ColorSeries" => "#f472b6",
            "Vector3" => "#67e8f9",
            "Color" => "#fb7185",
            "Any" => "#9aa8b5",
            _ => fallback
        };
    }

    private static GraphValueSourceKind DefaultSourceKind(NodeCatalogParameterDefinition parameter)
    {
        if (IsTriggeringPlayerParameter(parameter))
        {
            return GraphValueSourceKind.TriggeringPlayer;
        }

        if (IsSceneObjectParameter(parameter))
        {
            return GraphValueSourceKind.Self;
        }

        return GraphValueSourceKind.Constant;
    }

    private static bool ContainsToken(string? value, string token)
    {
        return value?.Contains(token, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsVectorDataType(string value)
    {
        return value.Contains("Vector3", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Position", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Rotation", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Scale", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Direction", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSceneObjectDataType(string value)
    {
        return value.Contains("Object", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Instance", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Player", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Inventory", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeManifestJson(string json)
    {
        return json.Replace("\"kind\": \"Event\"", "\"kind\": \"Trigger\"", StringComparison.OrdinalIgnoreCase);
    }

    private static string ShortId(string prefix)
    {
        var safePrefix = string.IsNullOrWhiteSpace(prefix) ? "NODE" : prefix;
        var raw = string.Create(CultureInfo.InvariantCulture, $"{safePrefix}_{Guid.NewGuid():N}");
        return raw[..Math.Min(raw.Length, safePrefix.Length + 9)];
    }
}
