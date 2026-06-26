using Vrs.Graph.Model;
using Vrs.Core.Catalog;

namespace Vrs.App.ViewModels;

public sealed partial class NodeParameterEditorViewModel
{
    // Human tooltip text for the active parameter value and source kind.
    private string BuildValueInputTooltip()
    {
        var description = HumanDescription();
        var current = ReadableCurrentValue();
        var requirement = Required ? "This value is required." : "This value can stay empty while drafting.";
        if (UsesNumber)
        {
            return $"Enter a number for {Label}.\nUse decimals with a dot, for example 0.5.\n{ManualValueExplanation()}\n{requirement}\nCurrent value: {current}\n{description}";
        }

        if (UsesBoolean)
        {
            return $"Turn {Label} on or off.\nChecked means true; unchecked means false.\n{ManualValueExplanation()}\nCurrent value: {current}\n{description}";
        }

        if (UsesVector3)
        {
            return $"Enter X, Y, and Z values for {Label}.\nUse Manual Value for fixed coordinates, or Build Value to read a compatible 3D Vector property.\n{requirement}\nCurrent value: {current}\n{description}";
        }

        if (UsesText)
        {
            return $"Type the text used by {Label}.\n{ManualValueExplanation()}\n{requirement}\nCurrent value: {current}\n{description}";
        }

        return $"{Label}\nCurrent value: {current}\n{description}";
    }

    private string HumanDescription()
    {
        return string.IsNullOrWhiteSpace(Description)
            ? $"{Label} controls this node's {NodeCatalogPresentationService.GetDataTypeLabel(Type).ToLowerInvariant()} value."
            : Description;
    }

    private string ReadableCurrentValue()
    {
        return string.IsNullOrWhiteSpace(PreviewText) ? "(empty)" : PreviewText;
    }

    private static string SourceKindCategory(GraphValueSourceKind sourceKind)
    {
        return sourceKind switch
        {
            GraphValueSourceKind.Constant => "Direct Value",
            GraphValueSourceKind.TriggeringPlayer => "Compatible",
            GraphValueSourceKind.Self or GraphValueSourceKind.Target or GraphValueSourceKind.SceneObject => "Object Context",
            GraphValueSourceKind.LocalVariable or GraphValueSourceKind.GlobalVariable => "Variable",
            GraphValueSourceKind.CatalogValue => "Build Value",
            _ => "General"
        };
    }

    private static string SourceKindDescription(GraphValueSourceKind sourceKind)
    {
        return sourceKind switch
        {
            GraphValueSourceKind.Constant => "Use exactly the value typed in this node.",
            GraphValueSourceKind.Self => "Use the object that owns the deployed script.",
            GraphValueSourceKind.Target => "Use the target object passed by the trigger or previous node when available.",
            GraphValueSourceKind.TriggeringPlayer => "Use the player supplied by the current player-related trigger.",
            GraphValueSourceKind.SceneObject => "Pick an object path from the Creator snapshot.",
            GraphValueSourceKind.LocalVariable => "Read a variable used only inside this generated script.",
            GraphValueSourceKind.GlobalVariable => "Read a graph-level variable shared by generated logic.",
            GraphValueSourceKind.CatalogValue => "Build this value from a reusable property node with a compatible type.",
            _ => "Choose where this value comes from."
        };
    }

    private static string SourceKindHumanExplanation(GraphValueSourceKind sourceKind)
    {
        return sourceKind switch
        {
            GraphValueSourceKind.Constant => ManualValueExplanation(),
            GraphValueSourceKind.Self => "The script will use the object that owns the deployed script.",
            GraphValueSourceKind.Target => "The script will use the object passed by the trigger or previous node when one exists.",
            GraphValueSourceKind.TriggeringPlayer => "The script will use triggerContext.player from the current touch/player trigger.",
            GraphValueSourceKind.SceneObject => "The script will use an object path chosen from the Creator hierarchy snapshot.",
            GraphValueSourceKind.LocalVariable => "The script will read a named value used only inside this generated script.",
            GraphValueSourceKind.GlobalVariable => "The script will read a named value intended to be shared by graph logic.",
            GraphValueSourceKind.CatalogValue => "The script will calculate this field from a property node. The inline property is used only by this parameter.",
            _ => "Choose where this field gets its value from."
        };
    }

    private static string ManualValueExplanation()
    {
        return "Manual Value means the script uses exactly what you type in this field. It does not read a variable or search the scene, and it stays the same until you edit this node.";
    }

}
