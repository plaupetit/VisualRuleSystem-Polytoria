
namespace Vrs.App.ViewModels;

public sealed partial class NodeParameterEditorViewModel
{
    // Small inference helpers shared by the parameter editor slices.
    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";
    }

    private static IEnumerable<string> EnsureCurrentValue(string currentValue, IEnumerable<string> options)
    {
        var values = options
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(currentValue) && !values.Contains(currentValue, StringComparer.OrdinalIgnoreCase))
        {
            values.Add(currentValue);
        }

        return values;
    }

    private static bool LooksLikeBoolean(string? value)
    {
        return value is not null &&
            (value.Equals("Boolean", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("Bool", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("CheckBox", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeChoice(string? value)
    {
        return value is not null &&
            (value.Equals("Choice", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("Dropdown", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("Select", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("ComboBox", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeVector3(string? type, string? control)
    {
        return ContainsVectorType(type) || ContainsVectorType(control);
    }

    private static bool ContainsVectorType(string? value)
    {
        return value is not null &&
            (value.Contains("Vector3", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("Position", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("Rotation", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("Scale", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("Direction", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSceneObjectParameter(string type, string control, string valueSource)
    {
        return control.Contains("SceneObject", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Object", StringComparison.OrdinalIgnoreCase) ||
            valueSource.Contains("Scene Object", StringComparison.OrdinalIgnoreCase) ||
            valueSource.Contains("Target Context", StringComparison.OrdinalIgnoreCase);
    }
}
