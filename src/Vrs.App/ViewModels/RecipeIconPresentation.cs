using Vrs.Core.Catalog;

namespace Vrs.App.ViewModels;

/// <summary>
/// Centralizes recipe picker icon language so value-type presentation changes
/// do not touch recipe browser navigation state.
/// </summary>
internal static class RecipeIconPresentation
{
    public static string Glyph(string dataType)
    {
        return CatalogDataTypeNormalizer.NormalizeValueType(dataType) switch
        {
            "Vector3" => "XYZ",
            "SceneObject" => "OBJ",
            "Color" => "RGB",
            "String" => "TXT",
            "Boolean" => "?",
            "Number" => "123",
            _ => "VAL"
        };
    }

    public static string AccentHex(string dataType)
    {
        return CatalogDataTypeNormalizer.NormalizeValueType(dataType) switch
        {
            "Vector3" => "#67e8f9",
            "SceneObject" => "#7dd3fc",
            "Color" => "#fb7185",
            "String" => "#b8c7d9",
            "Boolean" => "#88d28a",
            "Number" => "#f0c45c",
            _ => "#b58cff"
        };
    }

    public static string BackgroundHex(string dataType)
    {
        return CatalogDataTypeNormalizer.NormalizeValueType(dataType) switch
        {
            "Vector3" => "#14333e",
            "SceneObject" => "#14333e",
            "Color" => "#3a1e2a",
            "String" => "#242d39",
            "Boolean" => "#17351f",
            "Number" => "#372d15",
            _ => "#2b2444"
        };
    }
}
