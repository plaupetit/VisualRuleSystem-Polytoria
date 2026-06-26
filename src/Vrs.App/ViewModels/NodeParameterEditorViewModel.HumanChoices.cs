using Vrs.App.Icons;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

public sealed partial class NodeParameterEditorViewModel
{
    // Human labels, categories, descriptions, and search keywords shown by the searchable pickers.
    private static IEnumerable<string> SourceKindKeywords(GraphValueSourceKind sourceKind)
    {
        return sourceKind switch
        {
            GraphValueSourceKind.Constant => ["manual", "typed", "direct", "written here"],
            GraphValueSourceKind.Self => ["script parent", "this object", "owner"],
            GraphValueSourceKind.Target => ["target", "passed object"],
            GraphValueSourceKind.TriggeringPlayer => ["triggering player", "current player", "touching player", "player", "context"],
            GraphValueSourceKind.SceneObject => ["scene", "hierarchy", "creator", "object path"],
            GraphValueSourceKind.LocalVariable => ["local", "script variable"],
            GraphValueSourceKind.GlobalVariable => ["global", "shared", "graph variable"],
            GraphValueSourceKind.CatalogValue => ["build", "recipe", "math", "text", "random", "read variable"],
            _ => []
        };
    }

    private static string HumanOptionLabel(string parameterKey, string value)
    {
        if (parameterKey.Equals("operator", StringComparison.OrdinalIgnoreCase))
        {
            return value switch
            {
                ">" => "Greater Than",
                ">=" => "Greater Or Equal",
                "==" => "Equals",
                "~=" => "Not Equal",
                "<=" => "Less Or Equal",
                "<" => "Less Than",
                _ => value
            };
        }

        if (parameterKey.Equals("moveMode", StringComparison.OrdinalIgnoreCase) &&
            value.Equals("Constant", StringComparison.OrdinalIgnoreCase))
        {
            return "Continuous";
        }

        return value;
    }

    private static string OptionCategory(string parameterKey, string value)
    {
        if (parameterKey.Equals("moveMode", StringComparison.OrdinalIgnoreCase))
        {
            return "Movement Mode";
        }

        return parameterKey.Equals("operator", StringComparison.OrdinalIgnoreCase) ? "Comparison" : "Choice";
    }

    private static string OptionDescription(string parameterKey, string value)
    {
        if (parameterKey.Equals("operator", StringComparison.OrdinalIgnoreCase))
        {
            return value switch
            {
                ">" => "True when the left number is bigger than the right number.",
                ">=" => "True when the left number is bigger than or equal to the right number.",
                "==" => "True when both values are exactly the same.",
                "~=" => "True when the two values are different.",
                "<=" => "True when the left number is smaller than or equal to the right number.",
                "<" => "True when the left number is smaller than the right number.",
                _ => "Choose how the two values are compared."
            };
        }

        if (parameterKey.Equals("moveMode", StringComparison.OrdinalIgnoreCase))
        {
            return value switch
            {
                "Tween" => "Move once over the configured duration, with a smooth transition.",
                "Constant" => "Move continuously as a repeated, seamless cycle until another trigger, action, or condition changes it.",
                _ => "Choose how this movement should run."
            };
        }

        return $"Use {value} for this option.";
    }

    private static IEnumerable<string> OptionKeywords(string parameterKey, string value)
    {
        if (parameterKey.Equals("operator", StringComparison.OrdinalIgnoreCase))
        {
            return value switch
            {
                ">" => ["greater", "above", "more"],
                ">=" => ["greater", "equal", "minimum", "at least"],
                "==" => ["equal", "same", "is"],
                "~=" => ["different", "not", "unequal"],
                "<=" => ["less", "equal", "maximum", "at most"],
                "<" => ["less", "below", "smaller"],
                _ => []
            };
        }

        if (parameterKey.Equals("moveMode", StringComparison.OrdinalIgnoreCase))
        {
            return value switch
            {
                "Tween" => ["once", "duration", "smooth", "transition"],
                "Constant" => ["continuous", "repeat", "loop", "always", "seamless"],
                _ => []
            };
        }

        return [value];
    }

    private static string SceneObjectCategory(
        string value,
        SceneObject? sceneObject,
        NodeCatalogParameterDefinition? definition)
    {
        if (SceneObjectKindTaxonomy.IsContextValue(value))
        {
            return "Object Context";
        }

        if (sceneObject is null)
        {
            return "Current incompatible value";
        }

        var root = SceneObjectKindTaxonomy.SceneRoot(sceneObject);
        var rootPrefix = sceneObject.Path.StartsWith("World/", StringComparison.OrdinalIgnoreCase)
            ? "World"
            : root;
        if (root is "PlayerGUI" or "CoreUI")
        {
            return root;
        }

        return rootPrefix.Equals(root, StringComparison.OrdinalIgnoreCase)
            ? root
            : $"{rootPrefix} / {root}";
    }

    private static string SceneObjectLabel(string value, SceneObject? sceneObject)
    {
        if (sceneObject is not null && !string.IsNullOrWhiteSpace(sceneObject.Name))
        {
            return sceneObject.Name;
        }

        if (value.Contains('/', StringComparison.Ordinal))
        {
            return value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? value;
        }

        return value;
    }

    private static string SceneObjectDescription(
        string value,
        SceneObject? sceneObject,
        NodeCatalogParameterDefinition? definition)
    {
        if (SceneObjectKindTaxonomy.IsContextValue(value))
        {
            return value switch
            {
                "Self" => "Use the object that owns the deployed script. Its exact type is checked when the script runs.",
                "Parent" => "Use the parent object of the current object. Its exact type is checked when the script runs.",
                "Triggering Object" => "Use the object that caused the trigger when the trigger provides one.",
                "Target" => "Use the target passed through this graph flow.",
                "Player" => "Use the player context when the script type and trigger provide one.",
                "Selected Object" => "Use the object currently selected in Creator when available.",
                _ => "Use an object context value."
            };
        }

        if (sceneObject is null)
        {
            return $"Current value is not in the compatible {SceneObjectKindTaxonomy.ConstraintLabel(definition)} list.";
        }

        return $"Use {sceneObject.Kind} at {sceneObject.Path}. Expected: {SceneObjectKindTaxonomy.ConstraintLabel(definition)}.";
    }

    private static IEnumerable<string> SceneObjectKeywords(
        string value,
        SceneObject? sceneObject,
        NodeCatalogParameterDefinition? definition)
    {
        return value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat([value, sceneObject?.Kind ?? "", SceneObjectKindTaxonomy.ConstraintLabel(definition)])
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword));
    }

    private static string VariableScopeLabel(GraphVariableScope scope)
    {
        return scope switch
        {
            GraphVariableScope.Script => "Script",
            GraphVariableScope.State => "State",
            GraphVariableScope.Graph => "Graph",
            GraphVariableScope.Global => "Global",
            _ => scope.ToString()
        };
    }

    private static string VariableScopeCategory(GraphVariableScope scope)
    {
        return scope switch
        {
            GraphVariableScope.Script => "Script Scope",
            GraphVariableScope.State => "State Scope",
            GraphVariableScope.Graph => "Graph Scope",
            GraphVariableScope.Global => "Global Scope",
            _ => "Variable"
        };
    }

    private static string VariableScopeDescription(GraphVariableScope scope)
    {
        return scope switch
        {
            GraphVariableScope.Script => "Use this for a value that only this generated script needs.",
            GraphVariableScope.State => "Use this for a value shared by nodes in the same state.",
            GraphVariableScope.Graph => "Use this for a value shared across this visual graph.",
            GraphVariableScope.Global => "Use this for a value intended to be shared more broadly.",
            _ => "Choose where this variable should be read from."
        };
    }

    private static IconDescriptor VariableScopeIcon(GraphVariableScope scope)
    {
        return scope == GraphVariableScope.Global
            ? IconRegistry.ForValueSource(GraphValueSourceKind.GlobalVariable)
            : IconRegistry.ForValueSource(GraphValueSourceKind.LocalVariable);
    }

}
