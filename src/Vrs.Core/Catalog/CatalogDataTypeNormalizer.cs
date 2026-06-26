namespace Vrs.Core.Catalog;

/// <summary>
/// Converts loose catalog data type labels into the canonical value families used by UI, validation, and export.
/// </summary>
public static class CatalogDataTypeNormalizer
{
    public static string NormalizeValueType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Any";
        }

        if (value.Contains("Number", StringComparison.OrdinalIgnoreCase))
        {
            return "Number";
        }

        if (value.Contains("Vector3", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Position", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Rotation", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Scale", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Direction", StringComparison.OrdinalIgnoreCase))
        {
            return "Vector3";
        }

        if (value.Contains("Color", StringComparison.OrdinalIgnoreCase))
        {
            return "Color";
        }

        if (value.Contains("Object", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Instance", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Player", StringComparison.OrdinalIgnoreCase))
        {
            return "SceneObject";
        }

        if (value.Contains("Text", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("String", StringComparison.OrdinalIgnoreCase))
        {
            return "String";
        }

        if (value.Equals("Bool", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Boolean", StringComparison.OrdinalIgnoreCase))
        {
            return "Boolean";
        }

        return value.Equals("Any", StringComparison.OrdinalIgnoreCase) ? "Any" : value.Trim();
    }
}
