using Vrs.App.Services;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

public sealed partial class NodeParameterEditorViewModel
{
    private readonly NodePaletteQueryService recipeBrowserService = new();
    private readonly List<string> recipeBrowserCurrentDomainPath = [];
    private int recipeBrowserSelectedIndex;
    private string recipeBrowserSearch = "";

    public string RecipeBrowserSearch
    {
        get => recipeBrowserSearch;
        set
        {
            if (recipeBrowserSearch == value)
            {
                return;
            }

            recipeBrowserSearch = value;
            RefreshRecipeBrowserRows(resetSelection: true);
            OnPropertyChanged();
        }
    }

    public string RecipeBrowserBreadcrumb
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(RecipeBrowserSearch))
            {
                return "Search results";
            }

            return recipeBrowserCurrentDomainPath.Count == 0
                ? "Properties"
                : $"Properties / {string.Join(" / ", recipeBrowserCurrentDomainPath)}";
        }
    }

    public bool CanGoBackRecipeBrowser => !string.IsNullOrWhiteSpace(RecipeBrowserSearch) || recipeBrowserCurrentDomainPath.Count > 0;
    public bool HasNoRecipeBrowserRows => RecipeBrowserRows.Count == 0;
    public string RecipeBrowserExpectedType => DisplayRecipeDataType(Type);

    public string RecipeBrowserFooterTitle
    {
        get
        {
            var row = CurrentRecipeBrowserRow();
            if (row is not null)
            {
                return row.Label;
            }

            return HasNoRecipeBrowserRows ? "No compatible properties" : "Properties";
        }
    }

    public string RecipeBrowserFooterDescription
    {
        get
        {
            var row = CurrentRecipeBrowserRow();
            if (row is not null)
            {
                return row.IsFolder
                    ? $"{row.Description} ({row.Row.CompatibleCount} compatible / {row.Row.TotalCount} total)"
                    : row.Description;
            }

            return HasNoRecipeBrowserRows
                ? $"No value property matches {DisplayRecipeDataType(Type)} here."
                : $"Choose a compatible {DisplayRecipeDataType(Type)} property.";
        }
    }

    public string RecipePickerButtonText
    {
        get
        {
            if (FindRecipeEntry(RecipeCatalogId) is { } entry)
            {
                return entry.Label;
            }

            return "Choose property node";
        }
    }

    public string RecipePickerButtonSubText
    {
        get
        {
            if (FindRecipeEntry(RecipeCatalogId) is { } entry)
            {
                var path = NodeCatalogPresentationService.GetPalettePath(entry);
                return path.Count == 0 ? DisplayRecipeDataType(RecipeOutputDataType(entry)) : string.Join(" / ", path);
            }

            return $"Build {Label} from a compatible property node.";
        }
    }

    public string RecipePickerButtonIconGlyph
    {
        get
        {
            if (FindRecipeEntry(RecipeCatalogId) is { } entry)
            {
                return RecipeIconPresentation.Glyph(RecipeOutputDataType(entry));
            }

            return "123";
        }
    }

    public string RecipePickerButtonIconAccentHex
    {
        get
        {
            if (FindRecipeEntry(RecipeCatalogId) is { } entry)
            {
                return RecipeIconPresentation.AccentHex(RecipeOutputDataType(entry));
            }

            return "#b58cff";
        }
    }

    public string RecipePickerButtonIconBackgroundHex
    {
        get
        {
            if (FindRecipeEntry(RecipeCatalogId) is { } entry)
            {
                return RecipeIconPresentation.BackgroundHex(RecipeOutputDataType(entry));
            }

            return "#2b2444";
        }
    }

    public void OpenRecipeBrowser()
    {
        recipeBrowserSearch = "";
        recipeBrowserCurrentDomainPath.Clear();
        RefreshRecipeBrowserRows(resetSelection: true);
        OnPropertyChanged(nameof(RecipeBrowserSearch));
    }

    public bool ActivateRecipeBrowserRow(PropertyRecipeBrowserRowViewModel row)
    {
        recipeBrowserSelectedIndex = Math.Max(0, RecipeBrowserRows.IndexOf(row));
        RefreshRecipeBrowserSelection();

        if (row.IsFolder)
        {
            recipeBrowserCurrentDomainPath.Clear();
            recipeBrowserCurrentDomainPath.AddRange(row.Row.DomainPath);
            recipeBrowserSearch = "";
            RefreshRecipeBrowserRows(resetSelection: true);
            OnPropertyChanged(nameof(RecipeBrowserSearch));
            return false;
        }

        RecipeCatalogId = row.Value;
        return true;
    }

    public bool ActivateCurrentRecipeBrowserRow()
    {
        var row = CurrentRecipeBrowserRow();
        return row is not null && ActivateRecipeBrowserRow(row);
    }

    public void MoveRecipeBrowserSelection(int offset)
    {
        if (RecipeBrowserRows.Count == 0)
        {
            recipeBrowserSelectedIndex = 0;
            RefreshRecipeBrowserSelection();
            return;
        }

        recipeBrowserSelectedIndex = Math.Clamp(recipeBrowserSelectedIndex + offset, 0, RecipeBrowserRows.Count - 1);
        RefreshRecipeBrowserSelection();
    }

    public void SelectRecipeBrowserRow(PropertyRecipeBrowserRowViewModel row)
    {
        var index = RecipeBrowserRows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        recipeBrowserSelectedIndex = index;
        RefreshRecipeBrowserSelection();
    }

    public bool GoBackRecipeBrowser()
    {
        if (!string.IsNullOrWhiteSpace(recipeBrowserSearch))
        {
            recipeBrowserSearch = "";
            RefreshRecipeBrowserRows(resetSelection: true);
            OnPropertyChanged(nameof(RecipeBrowserSearch));
            return true;
        }

        if (recipeBrowserCurrentDomainPath.Count == 0)
        {
            return false;
        }

        recipeBrowserCurrentDomainPath.RemoveAt(recipeBrowserCurrentDomainPath.Count - 1);
        RefreshRecipeBrowserRows(resetSelection: true);
        return true;
    }

    private void RefreshRecipeBrowserRows(bool resetSelection)
    {
        RecipeBrowserRows.Clear();

        var rows = recipeBrowserService.Browse(
            RecipeBrowserCatalogEntries(),
            new NodePaletteBrowserQueryOptions(
                Search: recipeBrowserSearch,
                ScriptKind: GraphScriptKind.Server,
                CompatibleOnly: true,
                CurrentIntentKey: "Value",
                CurrentDomainPath: recipeBrowserCurrentDomainPath,
                IncompatibilityReason: RecipeBrowserIncompatibilityReason));

        var showPath = !string.IsNullOrWhiteSpace(recipeBrowserSearch);
        foreach (var row in rows)
        {
            var outputType = row.Entry is null ? "" : DisplayRecipeDataType(RecipeOutputDataType(row.Entry));
            RecipeBrowserRows.Add(new PropertyRecipeBrowserRowViewModel(
                row,
                outputType,
                row.Entry?.IdBase.Equals(RecipeCatalogId, StringComparison.OrdinalIgnoreCase) == true,
                showPath));
        }

        if (resetSelection)
        {
            recipeBrowserSelectedIndex = SelectedRecipeIndexOrFirst();
        }

        recipeBrowserSelectedIndex = RecipeBrowserRows.Count == 0
            ? 0
            : Math.Clamp(recipeBrowserSelectedIndex, 0, RecipeBrowserRows.Count - 1);
        RefreshRecipeBrowserSelection();
        NotifyRecipeBrowserPresentationChanged();
    }

    private void ClearRecipeBrowserRows()
    {
        recipeBrowserSearch = "";
        recipeBrowserCurrentDomainPath.Clear();
        recipeBrowserSelectedIndex = 0;
        RecipeBrowserRows.Clear();
        OnPropertyChanged(nameof(RecipeBrowserSearch));
        NotifyRecipeBrowserPresentationChanged();
    }

    private IEnumerable<NodeCatalogEntry> RecipeBrowserCatalogEntries()
    {
        return catalogEntries
            .Where(entry => entry.Kind == NodeKind.Property)
            .Where(entry => !IsManualPrimitiveRecipe(entry));
    }

    private string? RecipeBrowserIncompatibilityReason(NodeCatalogEntry entry)
    {
        if (entry.Kind != NodeKind.Property)
        {
            return "Only value properties are available here.";
        }

        if (IsManualPrimitiveRecipe(entry))
        {
            return "Manual primitive values are edited directly.";
        }

        var actualType = RecipeOutputDataType(entry);
        return RecipeTypeMatches(Type, actualType)
            ? null
            : $"Outputs {DisplayRecipeDataType(actualType)}, but this field needs {DisplayRecipeDataType(Type)}.";
    }

    private int SelectedRecipeIndexOrFirst()
    {
        for (var index = 0; index < RecipeBrowserRows.Count; index++)
        {
            if (RecipeBrowserRows[index].Value.Equals(RecipeCatalogId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return 0;
    }

    private PropertyRecipeBrowserRowViewModel? CurrentRecipeBrowserRow()
    {
        return RecipeBrowserRows.ElementAtOrDefault(recipeBrowserSelectedIndex);
    }

    private void RefreshRecipeBrowserSelection()
    {
        for (var index = 0; index < RecipeBrowserRows.Count; index++)
        {
            var row = RecipeBrowserRows[index];
            row.IsKeyboardSelected = index == recipeBrowserSelectedIndex;
            row.IsCurrentValue = row.Value.Equals(RecipeCatalogId, StringComparison.OrdinalIgnoreCase);
        }

        NotifyRecipeBrowserFooterChanged();
    }

    private void NotifyRecipeBrowserPresentationChanged()
    {
        OnPropertyChanged(nameof(RecipeBrowserBreadcrumb));
        OnPropertyChanged(nameof(CanGoBackRecipeBrowser));
        OnPropertyChanged(nameof(HasNoRecipeBrowserRows));
        NotifyRecipeBrowserFooterChanged();
    }

    private void NotifyRecipeBrowserFooterChanged()
    {
        OnPropertyChanged(nameof(RecipeBrowserFooterTitle));
        OnPropertyChanged(nameof(RecipeBrowserFooterDescription));
    }

    private void NotifyRecipePickerButtonChanged()
    {
        OnPropertyChanged(nameof(RecipePickerButtonText));
        OnPropertyChanged(nameof(RecipePickerButtonSubText));
        OnPropertyChanged(nameof(RecipePickerButtonIconGlyph));
        OnPropertyChanged(nameof(RecipePickerButtonIconAccentHex));
        OnPropertyChanged(nameof(RecipePickerButtonIconBackgroundHex));
    }

}
