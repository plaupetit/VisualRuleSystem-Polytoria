namespace Vrs.App.ViewModels;

public sealed partial class NodeParameterEditorViewModel
{
    public void NotifyVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsVisible));
        foreach (var recipeParameter in RecipeParameters)
        {
            recipeParameter.NotifyVisibilityChanged();
        }
    }

    private void NotifyTooltipChanged()
    {
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(TooltipText));
        OnPropertyChanged(nameof(ValueInputTooltip));
        OnPropertyChanged(nameof(ChoicePickerTooltip));
        OnPropertyChanged(nameof(SceneObjectPickerTooltip));
        OnPropertyChanged(nameof(VariableNameTooltip));
        OnPropertyChanged(nameof(VariableScopePickerTooltip));
        OnPropertyChanged(nameof(RecipePickerTooltip));
        OnPropertyChanged(nameof(SourceIconTooltip));
    }

    private void NotifyIconChanged()
    {
        OnPropertyChanged(nameof(SourceIcon));
        OnPropertyChanged(nameof(SourceIconPath));
        OnPropertyChanged(nameof(SourceIconLabel));
        OnPropertyChanged(nameof(SourceIconAccentHex));
        OnPropertyChanged(nameof(SourceIconBackgroundHex));
        OnPropertyChanged(nameof(SourceIconTooltip));
    }
}
