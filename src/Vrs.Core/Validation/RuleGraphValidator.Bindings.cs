using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.Core.Validation;

public sealed partial class RuleGraphValidator
{
    // Binding validation focuses on parameter value sources and type
    // compatibility, independent from node existence or graph flow.
    private static void ValidateBinding(
        RuleNode node,
        NodeCatalogParameterDefinition definition,
        RuleParameter parameter,
        IReadOnlyCollection<SceneObject> sceneObjects,
        ValidationResult result)
    {
        var binding = parameter.Binding;
        if (!TypeMatches(definition.Type, binding.DataType))
        {
            Add(result, ValidationSeverity.Error, node.Label, $"Parameter {definition.Label} expects {definition.Type}, but binding is {binding.DataType}.");
        }

        if (binding.SourceKind is GraphValueSourceKind.LocalVariable or GraphValueSourceKind.GlobalVariable &&
            string.IsNullOrWhiteSpace(binding.VariableName))
        {
            Add(result, ValidationSeverity.Error, node.Label, $"Variable binding has no variable name: {definition.Label}");
        }

        if (binding.SourceKind == GraphValueSourceKind.SceneObject &&
            string.IsNullOrWhiteSpace(binding.SceneObjectPath))
        {
            Add(result, ValidationSeverity.Error, node.Label, $"Scene object binding has no object path: {definition.Label}");
        }

        if (binding.SourceKind == GraphValueSourceKind.SceneObject &&
            !string.IsNullOrWhiteSpace(binding.SceneObjectPath) &&
            sceneObjects.FirstOrDefault(sceneObject => sceneObject.Path.Equals(binding.SceneObjectPath, StringComparison.OrdinalIgnoreCase)) is { } selectedObject &&
            !SceneObjectKindTaxonomy.Matches(selectedObject, definition))
        {
            Add(
                result,
                ValidationSeverity.Warning,
                node.Label,
                $"{definition.Label} expects {SceneObjectKindTaxonomy.ConstraintLabel(definition)}, but {selectedObject.Name} is {selectedObject.Kind}.");
        }

        if (binding.SourceKind == GraphValueSourceKind.CatalogValue &&
            string.IsNullOrWhiteSpace(binding.CatalogId))
        {
            Add(result, ValidationSeverity.Error, node.Label, $"Value recipe binding has no recipe selected: {definition.Label}");
        }
    }

    private static string EffectiveParameterValue(RuleParameter parameter)
    {
        return parameter.Binding.SourceKind switch
        {
            GraphValueSourceKind.Constant => parameter.Value,
            GraphValueSourceKind.Self => "Self",
            GraphValueSourceKind.Target => "Target",
            GraphValueSourceKind.TriggeringPlayer => "Triggering Player",
            GraphValueSourceKind.SceneObject => parameter.Binding.SceneObjectPath,
            GraphValueSourceKind.LocalVariable or GraphValueSourceKind.GlobalVariable => parameter.Binding.VariableName,
            GraphValueSourceKind.CatalogValue => parameter.Binding.CatalogId,
            GraphValueSourceKind.ConnectedPort => parameter.Value,
            _ => parameter.Value
        };
    }

    private static bool TypeMatches(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
        {
            return true;
        }

        var normalizedExpected = NormalizeType(expected);
        var normalizedActual = NormalizeType(actual);
        if (normalizedActual.Equals("Any", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalizedExpected.Equals(normalizedActual, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeType(string value)
    {
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

        if (value.Contains("Text", StringComparison.OrdinalIgnoreCase))
        {
            return "String";
        }

        if (value.Equals("Bool", StringComparison.OrdinalIgnoreCase))
        {
            return "Boolean";
        }

        return value;
    }
}
