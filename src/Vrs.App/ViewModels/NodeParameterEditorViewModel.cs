using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vrs.Core.Catalog;
using Vrs.Core.ProjectInputs;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

/// <summary>
/// Inspector-facing parameter state. This view model still combines binding
/// edits, human choice labels, and tooltip text; keep domain semantics in the
/// catalog or graph model when extracting future helper services.
/// </summary>
public sealed partial class NodeParameterEditorViewModel : ObservableObject
{
    private readonly RuleParameter parameter;
    private readonly NodeCatalogParameterDefinition? definition;
    private readonly IReadOnlyList<NodeCatalogEntry> catalogEntries;
    private readonly IReadOnlyList<SceneObject> sceneObjects;
    private readonly IReadOnlyList<RuleParameter> peerParameters;
    private readonly Action valueChanged;
    private readonly int recipeDepth;
    private readonly string nodeType;
    private readonly IReadOnlyList<VrsInputActionChoice> inputActionChoices;
    private readonly List<ParameterChoiceViewModel> allOptionChoices;
    private readonly bool isInputActionChoiceParameter;

    [ObservableProperty]
    private string inputChoiceSourceFilter = "All";

    public NodeParameterEditorViewModel(
        RuleParameter parameter,
        NodeCatalogParameterDefinition? definition,
        IEnumerable<SceneObject> sceneObjects,
        Action valueChanged)
        : this(parameter, definition, sceneObjects, [], [], valueChanged, 0)
    {
    }

    public NodeParameterEditorViewModel(
        RuleParameter parameter,
        NodeCatalogParameterDefinition? definition,
        IEnumerable<SceneObject> sceneObjects,
        IEnumerable<NodeCatalogEntry> catalogEntries,
        Action valueChanged,
        int recipeDepth = 0)
        : this(parameter, definition, sceneObjects, catalogEntries, [], valueChanged, recipeDepth)
    {
    }

    public NodeParameterEditorViewModel(
        RuleParameter parameter,
        NodeCatalogParameterDefinition? definition,
        IEnumerable<SceneObject> sceneObjects,
        IEnumerable<NodeCatalogEntry> catalogEntries,
        IEnumerable<RuleParameter> peerParameters,
        Action valueChanged,
        int recipeDepth = 0,
        string nodeType = "",
        IEnumerable<VrsInputActionChoice>? inputActionChoices = null)
    {
        this.parameter = parameter;
        this.definition = definition;
        this.catalogEntries = catalogEntries.ToList();
        this.sceneObjects = sceneObjects.ToList();
        this.peerParameters = peerParameters.ToList();
        this.valueChanged = valueChanged;
        this.recipeDepth = recipeDepth;
        this.nodeType = nodeType;
        this.inputActionChoices = (inputActionChoices ?? VrsInputPresetCatalog.DefaultChoices).ToList();
        Key = parameter.Key;
        Label = string.IsNullOrWhiteSpace(definition?.Label) ? parameter.Key : definition.Label;
        Description = definition?.Description ?? "";
        Type = definition?.Type ?? "String";
        Control = definition?.Control ?? "";
        Required = definition?.Required ?? false;
        EnsureBinding(parameter, definition);
        NormalizeInspectorSourceKind(parameter);
        isInputActionChoiceParameter = TryResolveInputActionType(this.nodeType, parameter, definition, out var resolvedInputActionType);
        VrsInputActionType? inputActionType = isInputActionChoiceParameter ? resolvedInputActionType : null;
        Options = new ObservableCollection<string>(BuildOptions(parameter, definition, this.sceneObjects, this.inputActionChoices, inputActionType));
        ValueSourceOptions = new ObservableCollection<string>(BuildValueSourceOptions(parameter, definition));
        SourceKindOptions = new ObservableCollection<GraphValueSourceKind>(BuildSourceKindOptions(definition, this.catalogEntries, recipeDepth));
        VariableScopeOptions = new ObservableCollection<GraphVariableScope>(BuildVariableScopeOptions(definition));
        RecipeChoices = new ObservableCollection<ParameterChoiceViewModel>(BuildRecipeChoices(this.catalogEntries, Type));
        RecipeBrowserRows = new ObservableCollection<PropertyRecipeBrowserRowViewModel>();
        RecipeParameters = new ObservableCollection<NodeParameterEditorViewModel>();
        SourceKindChoices = new ObservableCollection<ValueSourceChoiceViewModel>(BuildSourceKindChoices(SourceKindOptions));
        allOptionChoices = BuildParameterChoices(parameter, definition, Options, this.inputActionChoices, inputActionType).ToList();
        OptionChoices = new ObservableCollection<ParameterChoiceViewModel>();
        SceneObjectChoices = new ObservableCollection<ParameterChoiceViewModel>(BuildSceneObjectChoices(parameter, definition, this.sceneObjects));
        VariableScopeChoices = new ObservableCollection<VariableScopeChoiceViewModel>(BuildVariableScopeChoices(VariableScopeOptions));
        RefreshOptionChoices();
        if (SourceKind == GraphValueSourceKind.CatalogValue)
        {
            RefreshRecipeBrowserRows(resetSelection: true);
        }

        RefreshRecipeParameters();
    }

    public string Key { get; }
    public string Label { get; }
    public string Description { get; }
    public string Type { get; }
    public string Control { get; }
    public bool Required { get; }
    public bool IsVisible => IsParameterVisible(definition, peerParameters);
    public ObservableCollection<string> Options { get; }
    public ObservableCollection<string> ValueSourceOptions { get; }
    public ObservableCollection<GraphValueSourceKind> SourceKindOptions { get; }
    public ObservableCollection<GraphVariableScope> VariableScopeOptions { get; }
    public ObservableCollection<ParameterChoiceViewModel> RecipeChoices { get; }
    public ObservableCollection<PropertyRecipeBrowserRowViewModel> RecipeBrowserRows { get; }
    public ObservableCollection<NodeParameterEditorViewModel> RecipeParameters { get; }
    public ObservableCollection<ValueSourceChoiceViewModel> SourceKindChoices { get; }
    public ObservableCollection<ParameterChoiceViewModel> OptionChoices { get; }
    public ObservableCollection<ParameterChoiceViewModel> SceneObjectChoices { get; }
    public ObservableCollection<VariableScopeChoiceViewModel> VariableScopeChoices { get; }
    public IReadOnlyList<string> InputChoiceSourceFilterChoices { get; } = ["All", "Project", "Presets"];

    partial void OnInputChoiceSourceFilterChanged(string value)
    {
        RefreshOptionChoices();
        NotifyTooltipChanged();
    }

    private void RefreshOptionChoices()
    {
        OptionChoices.Clear();
        foreach (var choice in allOptionChoices.Where(ChoiceMatchesInputSourceFilter))
        {
            OptionChoices.Add(choice);
        }
    }

    private bool ChoiceMatchesInputSourceFilter(ParameterChoiceViewModel choice)
    {
        if (!isInputActionChoiceParameter)
        {
            return true;
        }

        return InputChoiceSourceFilter switch
        {
            "Project" => choice.Category.Contains("Project", StringComparison.OrdinalIgnoreCase),
            "Presets" => choice.Category.Contains("Preset", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }
}
