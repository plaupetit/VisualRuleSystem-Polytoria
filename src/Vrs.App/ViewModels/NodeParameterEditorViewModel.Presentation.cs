using Avalonia;
using Vrs.App.Icons;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

public sealed partial class NodeParameterEditorViewModel
{
    // Inline catalog recipes reuse the same editor; these hints flatten nested rows
    // without changing the graph model, saved JSON, or Luau export semantics.
    public int RecipeDepth => recipeDepth;
    public bool IsRecipeParameter => recipeDepth > 0;
    public string EditorBackgroundHex => IsRecipeParameter ? "#1f252d" : "#242830";
    public string EditorBorderHex => IsRecipeParameter ? "#344050" : "#3a414d";
    public Thickness EditorPadding => IsRecipeParameter ? new Thickness(6, 5, 6, 5) : new Thickness(8);
    public Thickness EditorMargin => IsRecipeParameter ? new Thickness(0, 2, 0, 0) : new Thickness(0, 4, 0, 0);
    public Thickness RecipeParameterListMargin => IsRecipeParameter ? new Thickness(22, 5, 0, 0) : new Thickness(28, 6, 0, 0);
    public double TypeIconBoxSize => IsRecipeParameter ? 20 : 24;
    public double TypeIconGlyphSize => IsRecipeParameter ? 13 : 15;
    public double SourceIconBoxSize => IsRecipeParameter ? 22 : 24;
    public double SourceIconGlyphSize => IsRecipeParameter ? 13 : 15;
    public double ParameterLabelFontSize => IsRecipeParameter ? 11.5 : 12;
    public double ParameterTypeFontSize => IsRecipeParameter ? 9.5 : 10;
    public bool ShowsValueSourceSelector => ValueSourceOptions.Count > 1;
    public bool UsesConstantValue => SourceKind == GraphValueSourceKind.Constant;
    public bool UsesVariableSelector => SourceKind is GraphValueSourceKind.LocalVariable or GraphValueSourceKind.GlobalVariable;
    public bool UsesSceneObjectSelector => SourceKind == GraphValueSourceKind.SceneObject;
    public bool UsesCatalogValue => SourceKind == GraphValueSourceKind.CatalogValue;
    public bool UsesBoolean => !Control.Equals("Choice", StringComparison.OrdinalIgnoreCase) && (LooksLikeBoolean(Type) || LooksLikeBoolean(Control));
    public bool UsesSceneObject => IsSceneObjectParameter(Type, Control, ValueSource);
    public bool UsesChoice => !UsesBoolean && !UsesSceneObject && (Options.Count > 0 || LooksLikeChoice(Control));
    public bool ShowsInputChoiceSourceFilter => isInputActionChoiceParameter &&
        allOptionChoices.Any(choice => choice.Category.Contains("Project", StringComparison.OrdinalIgnoreCase)) &&
        allOptionChoices.Any(choice => choice.Category.Contains("Preset", StringComparison.OrdinalIgnoreCase));
    public bool UsesVector3 => !UsesChoice && !UsesBoolean && LooksLikeVector3(Type, Control);
    public bool UsesNumber => !UsesChoice && !UsesBoolean && !UsesVector3 && Type.Equals("Number", StringComparison.OrdinalIgnoreCase);
    public bool UsesText => !UsesBoolean && !UsesSceneObject && !UsesChoice && !UsesVector3 && !UsesNumber;
    public string PreviewText => BuildPreviewText();
    public string TooltipText => string.IsNullOrWhiteSpace(Description)
        ? PreviewText
        : $"{PreviewText}\n\n{Description}";
    public string ValueInputTooltip => BuildValueInputTooltip();
    public string ChoicePickerTooltip => $"Choose {Label}.\n{HumanDescription()}\nCurrent value: {ReadableCurrentValue()}";
    public string SceneObjectPickerTooltip => $"Choose the object path used by {Label}.\nExpected: {SceneObjectKindTaxonomy.ConstraintLabel(definition)}.\nPick a context value when the exact object comes from the running rule.";
    public string VariableNameTooltip => $"Type the variable name used for {Label}.\nLeave it empty only while drafting; validation will warn before export.";
    public string VariableScopePickerTooltip => $"Choose where the variable for {Label} lives.\nScript is safest for values used only by this generated script.";
    public string RecipePickerTooltip => $"Build {Label} from a property node.\nUse this for math, text building, random numbers, or reading script variables without placing a value node on the canvas.";
    public IconDescriptor SourceIcon => IconRegistry.ForValueSource(SourceKind);
    public string SourceIconPath => SourceIcon.Path;
    public string SourceIconLabel => SourceIcon.Label;
    public string SourceIconAccentHex => SourceIcon.AccentHex;
    public string SourceIconBackgroundHex => SourceIcon.BackgroundHex;
    public string SourceIconTooltip => $"{SourceKindLabel(SourceKind)}\n{SourceKindHumanExplanation(SourceKind)}\nCurrent value: {ReadableCurrentValue()}";
    public IconDescriptor TypeIcon => IconRegistry.ForParameterType(Type, Control);
    public string TypeIconPath => TypeIcon.Path;
    public string TypeIconLabel => TypeIcon.Label;
    public string TypeIconAccentHex => TypeIcon.AccentHex;
    public string TypeIconBackgroundHex => TypeIcon.BackgroundHex;
    public string TypeIconTooltip => $"{Label}\n{NodeCatalogPresentationService.GetDataTypeLabel(Type)}";

    private static bool IsParameterVisible(
        NodeCatalogParameterDefinition? definition,
        IReadOnlyList<RuleParameter> peers)
    {
        if (definition is null || definition.VisibleWhen.Count == 0)
        {
            return true;
        }

        return definition.VisibleWhen.All(condition => VisibilityConditionMatches(condition, peers));
    }

    private static bool VisibilityConditionMatches(
        NodeCatalogParameterVisibilityCondition condition,
        IReadOnlyList<RuleParameter> peers)
    {
        if (string.IsNullOrWhiteSpace(condition.ParameterKey))
        {
            return true;
        }

        var value = peers.FirstOrDefault(parameter =>
            parameter.Key.Equals(condition.ParameterKey, StringComparison.OrdinalIgnoreCase))?.Value ?? "";

        if (!string.IsNullOrWhiteSpace(condition.EqualsValue) &&
            !value.Equals(condition.EqualsValue, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(condition.NotEqualsValue) &&
            value.Equals(condition.NotEqualsValue, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (condition.In.Count > 0 &&
            !condition.In.Any(item => value.Equals(item, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (condition.NotIn.Count > 0 &&
            condition.NotIn.Any(item => value.Equals(item, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private string BuildPreviewText()
    {
        return SourceKind switch
        {
            GraphValueSourceKind.Constant => string.IsNullOrWhiteSpace(parameter.Binding.ConstantValue) ? "No value typed yet" : $"Typed here: {parameter.Binding.ConstantValue}",
            GraphValueSourceKind.LocalVariable => string.IsNullOrWhiteSpace(VariableName) ? "Local variable: (missing)" : $"Local variable: {VariableName}",
            GraphValueSourceKind.GlobalVariable => string.IsNullOrWhiteSpace(VariableName) ? "Global variable: (missing)" : $"Global variable: {VariableName}",
            GraphValueSourceKind.Self => "Target context: Self",
            GraphValueSourceKind.Target => "Target context: Target",
            GraphValueSourceKind.TriggeringPlayer => "Trigger context: Player",
            GraphValueSourceKind.SceneObject => string.IsNullOrWhiteSpace(SceneObjectPath) ? "Scene object: (missing)" : $"Scene object: {SceneObjectPath}",
            GraphValueSourceKind.CatalogValue => string.IsNullOrWhiteSpace(RecipeCatalogId) ? "Build value: choose a property" : $"Build value: {RecipeLabel(RecipeCatalogId)}",
            GraphValueSourceKind.ConnectedPort => "Connected port overrides this value when wired",
            _ => parameter.Value
        };
    }
}
