using CommunityToolkit.Mvvm.ComponentModel;
using Vrs.App.Services;

namespace Vrs.App.ViewModels;

/// <summary>
/// XAML projection for catalog value recipes. The underlying palette row stays
/// service-owned so the inspector and canvas use the same catalog vocabulary.
/// </summary>
public sealed partial class PropertyRecipeBrowserRowViewModel : ObservableObject
{
    public PropertyRecipeBrowserRowViewModel(
        NodePaletteBrowserRow row,
        string outputType,
        bool isCurrentValue,
        bool showPath)
    {
        Row = row;
        OutputType = string.IsNullOrWhiteSpace(outputType) ? "Value" : outputType;
        IsCurrentValue = isCurrentValue;
        SubText = showPath && !string.IsNullOrWhiteSpace(row.DomainLabel) ? row.DomainLabel : "";
    }

    public NodePaletteBrowserRow Row { get; }
    public bool IsFolder => Row.Kind == NodePaletteBrowserRowKind.Folder;
    public bool IsRecipe => Row.Kind == NodePaletteBrowserRowKind.Node;
    public string Value => Row.Entry?.IdBase ?? Row.Key;
    public string Label => Row.Label;
    public string Description => Row.Description;
    public string SubText { get; }
    public bool HasSubText => !string.IsNullOrWhiteSpace(SubText);
    public string OutputType { get; }
    public string RightLabel => IsFolder ? ">" : OutputTypeLabel(OutputType);
    public string IconGlyph => Row.IconGlyph;
    public string IconAccentHex => Row.IconAccentHex;
    public string IconBackgroundHex => Row.IconBackgroundHex;
    public string Tooltip => string.Join(
        "\n",
        new[] { Row.TooltipTitle, Row.TooltipText }.Where(line => !string.IsNullOrWhiteSpace(line)));
    public string RowBackgroundHex => IsKeyboardSelected ? "#225f93" : IsCurrentValue ? "#263545" : "#2b2b2b";
    public string PrimaryForegroundHex => IsKeyboardSelected ? "#ffffff" : "#d8dde4";
    public string SecondaryForegroundHex => IsKeyboardSelected ? "#dcecff" : "#aeb8c4";

    [ObservableProperty]
    private bool isKeyboardSelected;

    [ObservableProperty]
    private bool isCurrentValue;

    partial void OnIsKeyboardSelectedChanged(bool value)
    {
        NotifyPresentationChanged();
    }

    partial void OnIsCurrentValueChanged(bool value)
    {
        NotifyPresentationChanged();
    }

    private void NotifyPresentationChanged()
    {
        OnPropertyChanged(nameof(RowBackgroundHex));
        OnPropertyChanged(nameof(PrimaryForegroundHex));
        OnPropertyChanged(nameof(SecondaryForegroundHex));
    }

    private static string OutputTypeLabel(string value)
    {
        return value switch
        {
            "SceneObject" => "OBJECT",
            "Vector3" => "VECTOR",
            "3D Vector" => "3D",
            "Boolean" => "BOOL",
            "True/False" => "BOOL",
            "String" => "TEXT",
            "Text" => "TEXT",
            "Number" => "NUMBER",
            "Color" => "COLOR",
            _ => value.ToUpperInvariant()
        };
    }
}
