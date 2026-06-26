using System.Globalization;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

public sealed partial class NodeParameterEditorViewModel
{
    // Mutating properties keep RuleParameter.Value and GraphValueBinding in sync
    // before notifying the owning graph that preview/validation may need refresh.
    public string Value
    {
        get => parameter.Value;
        set
        {
            if (parameter.Value == value)
            {
                return;
            }

            parameter.Value = value;
            parameter.Binding.ConstantValue = value;
            parameter.Binding.DisplayText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NumberValue));
            OnPropertyChanged(nameof(BooleanValue));
            NotifyTooltipChanged();
            valueChanged();
        }
    }

    public decimal NumberValue
    {
        get => decimal.TryParse(parameter.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
        set => Value = value.ToString(CultureInfo.InvariantCulture);
    }

    public decimal VectorX
    {
        get => VectorComponent(0);
        set => SetVectorComponent(0, value);
    }

    public decimal VectorY
    {
        get => VectorComponent(1);
        set => SetVectorComponent(1, value);
    }

    public decimal VectorZ
    {
        get => VectorComponent(2);
        set => SetVectorComponent(2, value);
    }

    public bool BooleanValue
    {
        get => bool.TryParse(parameter.Value, out var value) && value;
        set => Value = value ? "true" : "false";
    }

    public string ValueSource
    {
        get => parameter.ValueSource;
        set
        {
            if (parameter.ValueSource == value)
            {
                return;
            }

            parameter.ValueSource = value;
            OnPropertyChanged();
            NotifyTooltipChanged();
            valueChanged();
        }
    }

    public GraphValueSourceKind SourceKind
    {
        get => parameter.Binding.SourceKind;
        set
        {
            if (parameter.Binding.SourceKind == value)
            {
                return;
            }

            parameter.Binding.SourceKind = value;
            parameter.ValueSource = SourceKindLabel(value);
            SyncValueFromBinding();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ValueSource));
            OnPropertyChanged(nameof(UsesConstantValue));
            OnPropertyChanged(nameof(UsesVariableSelector));
            OnPropertyChanged(nameof(UsesSceneObjectSelector));
            OnPropertyChanged(nameof(UsesCatalogValue));
            OnPropertyChanged(nameof(SourceKindKey));
            OnPropertyChanged(nameof(RecipeCatalogId));
            OnPropertyChanged(nameof(PreviewText));
            OnPropertyChanged(nameof(ValueInputTooltip));
            OnPropertyChanged(nameof(ChoicePickerTooltip));
            OnPropertyChanged(nameof(SceneObjectPickerTooltip));
            OnPropertyChanged(nameof(VariableNameTooltip));
            OnPropertyChanged(nameof(RecipePickerTooltip));
            NotifyRecipePickerButtonChanged();
            if (value == GraphValueSourceKind.CatalogValue)
            {
                RefreshRecipeBrowserRows(resetSelection: true);
            }
            else
            {
                ClearRecipeBrowserRows();
            }

            RefreshRecipeParameters();
            NotifyIconChanged();
            NotifyTooltipChanged();
            valueChanged();
        }
    }

    public string SourceKindKey
    {
        get => SourceKind.ToString();
        set
        {
            if (Enum.TryParse<GraphValueSourceKind>(value, out var parsed))
            {
                SourceKind = parsed;
            }
        }
    }

    public GraphVariableScope VariableScope
    {
        get => parameter.Binding.VariableScope;
        set
        {
            if (parameter.Binding.VariableScope == value)
            {
                return;
            }

            parameter.Binding.VariableScope = value;
            SyncValueFromBinding();
            OnPropertyChanged();
            OnPropertyChanged(nameof(VariableScopeKey));
            OnPropertyChanged(nameof(VariableScopePickerTooltip));
            NotifyTooltipChanged();
            valueChanged();
        }
    }

    public string VariableScopeKey
    {
        get => VariableScope.ToString();
        set
        {
            if (Enum.TryParse<GraphVariableScope>(value, out var parsed))
            {
                VariableScope = parsed;
            }
        }
    }

    public string VariableName
    {
        get => parameter.Binding.VariableName;
        set
        {
            if (parameter.Binding.VariableName == value)
            {
                return;
            }

            parameter.Binding.VariableName = value;
            SyncValueFromBinding();
            OnPropertyChanged();
            NotifyTooltipChanged();
            valueChanged();
        }
    }

    public string SceneObjectPath
    {
        get => parameter.Binding.SceneObjectPath;
        set
        {
            if (parameter.Binding.SceneObjectPath == value)
            {
                return;
            }

            parameter.Binding.SceneObjectPath = value;
            SyncValueFromBinding();
            OnPropertyChanged();
            NotifyTooltipChanged();
            valueChanged();
        }
    }

    public string RecipeCatalogId
    {
        get => parameter.Binding.CatalogId;
        set
        {
            if (parameter.Binding.CatalogId == value)
            {
                return;
            }

            ApplyRecipeSelection(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewText));
            OnPropertyChanged(nameof(SourceIconTooltip));
            NotifyRecipePickerButtonChanged();
            RefreshRecipeBrowserRows(resetSelection: false);
            NotifyTooltipChanged();
            valueChanged();
        }
    }

    private void SyncValueFromBinding()
    {
        parameter.Value = SourceKind switch
        {
            GraphValueSourceKind.Constant => parameter.Binding.ConstantValue,
            GraphValueSourceKind.Self => "Self",
            GraphValueSourceKind.Target => "Target",
            GraphValueSourceKind.TriggeringPlayer => "Triggering Player",
            GraphValueSourceKind.SceneObject => parameter.Binding.SceneObjectPath,
            GraphValueSourceKind.LocalVariable or GraphValueSourceKind.GlobalVariable => parameter.Binding.VariableName,
            GraphValueSourceKind.ConnectedPort => parameter.Value,
            GraphValueSourceKind.CatalogValue => parameter.Binding.CatalogId,
            _ => parameter.Value
        };
        parameter.Binding.DisplayText = BuildPreviewText();
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(NumberValue));
        OnPropertyChanged(nameof(VectorX));
        OnPropertyChanged(nameof(VectorY));
        OnPropertyChanged(nameof(VectorZ));
        OnPropertyChanged(nameof(BooleanValue));
    }

    private decimal VectorComponent(int index)
    {
        var components = ParseVectorComponents(parameter.Binding.ConstantValue);
        return components[index];
    }

    private void SetVectorComponent(int index, decimal value)
    {
        var components = ParseVectorComponents(parameter.Binding.ConstantValue);
        components[index] = value;
        Value = string.Join(",", components.Select(component => component.ToString(CultureInfo.InvariantCulture)));
    }

    private static decimal[] ParseVectorComponents(string? value)
    {
        var components = new decimal[] { 0, 0, 0 };
        if (string.IsNullOrWhiteSpace(value))
        {
            return components;
        }

        var parts = value
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(3)
            .ToArray();
        for (var index = 0; index < parts.Length; index++)
        {
            if (decimal.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                components[index] = parsed;
            }
        }

        return components;
    }
}
