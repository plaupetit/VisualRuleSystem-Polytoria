using CommunityToolkit.Mvvm.ComponentModel;
using Vrs.App.Icons;

namespace Vrs.App.ViewModels;

/// <summary>
/// UI-facing choice metadata shared by searchable pickers without leaking catalog parsing into controls.
/// </summary>
public class ChoicePickerItemViewModel
{
    public ChoicePickerItemViewModel(
        string value,
        string label,
        string category,
        string description,
        IconDescriptor icon,
        IEnumerable<string>? searchKeywords = null)
    {
        Value = value;
        Label = string.IsNullOrWhiteSpace(label) ? value : label;
        Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
        Description = description;
        IconName = icon.Key;
        IconPath = icon.Path;
        IconLabel = icon.Label;
        IconAccentHex = icon.AccentHex;
        IconBackgroundHex = icon.BackgroundHex;
        SearchKeywords = searchKeywords?.Where(keyword => !string.IsNullOrWhiteSpace(keyword)).Select(keyword => keyword.Trim()).ToList() ?? [];
    }

    public string Value { get; }
    public string Label { get; }
    public string Category { get; }
    public string Description { get; }
    public string IconName { get; }
    public string IconPath { get; }
    public string IconLabel { get; }
    public string IconAccentHex { get; }
    public string IconBackgroundHex { get; }
    public IReadOnlyList<string> SearchKeywords { get; }
    public string Tooltip => string.IsNullOrWhiteSpace(Description)
        ? Label
        : $"{Label}\n{Description}";
    public string SearchText => string.Join(
        " ",
        new[] { Value, Label, Category, Description }.Concat(SearchKeywords));
}

public sealed class ValueSourceChoiceViewModel : ChoicePickerItemViewModel
{
    public ValueSourceChoiceViewModel(
        string value,
        string label,
        string category,
        string description,
        IconDescriptor icon,
        IEnumerable<string>? searchKeywords = null)
        : base(value, label, category, description, icon, searchKeywords)
    {
    }
}

public sealed class ParameterChoiceViewModel : ChoicePickerItemViewModel
{
    public ParameterChoiceViewModel(
        string value,
        string label,
        string category,
        string description,
        IconDescriptor icon,
        IEnumerable<string>? searchKeywords = null)
        : base(value, label, category, description, icon, searchKeywords)
    {
    }
}

public sealed class VariableScopeChoiceViewModel : ChoicePickerItemViewModel
{
    public VariableScopeChoiceViewModel(
        string value,
        string label,
        string category,
        string description,
        IconDescriptor icon,
        IEnumerable<string>? searchKeywords = null)
        : base(value, label, category, description, icon, searchKeywords)
    {
    }
}

public sealed partial class ChoicePickerDisplayItemViewModel : ObservableObject
{
    public ChoicePickerDisplayItemViewModel(ChoicePickerItemViewModel choice, bool showsCategoryHeader)
    {
        Choice = choice;
        ShowsCategoryHeader = showsCategoryHeader;
    }

    public ChoicePickerItemViewModel Choice { get; }
    public bool ShowsCategoryHeader { get; }
    public string Value => Choice.Value;
    public string Label => Choice.Label;
    public string Category => Choice.Category;
    public string Description => Choice.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public string Tooltip => Choice.Tooltip;
    public string IconPath => Choice.IconPath;
    public string IconLabel => Choice.IconLabel;
    public string IconAccentHex => Choice.IconAccentHex;
    public string IconBackgroundHex => Choice.IconBackgroundHex;
    public string RowBackgroundHex => IsKeyboardSelected ? "#225f93" : IsCurrentValue ? "#263545" : "#2b2b2b";
    public string PrimaryForegroundHex => IsKeyboardSelected ? "#ffffff" : "#d8dde4";
    public string SecondaryForegroundHex => IsKeyboardSelected ? "#dcecff" : "#aeb8c4";

    [ObservableProperty]
    private bool isKeyboardSelected;

    [ObservableProperty]
    private bool isCurrentValue;

    partial void OnIsKeyboardSelectedChanged(bool value)
    {
        NotifyRowPresentationChanged();
    }

    partial void OnIsCurrentValueChanged(bool value)
    {
        NotifyRowPresentationChanged();
    }

    private void NotifyRowPresentationChanged()
    {
        OnPropertyChanged(nameof(RowBackgroundHex));
        OnPropertyChanged(nameof(PrimaryForegroundHex));
        OnPropertyChanged(nameof(SecondaryForegroundHex));
    }
}

public sealed partial class ChoicePickerBrowserRowViewModel : ObservableObject
{
    private const string FolderIconPath = "/Assets/Icons/PolytoriaLike/tabler-folder.svg";

    private ChoicePickerBrowserRowViewModel(
        ChoicePickerItemViewModel? choice,
        IReadOnlyList<string> folderPath,
        bool isFolder,
        bool showsCategoryHeader,
        string label,
        string category,
        string description,
        string iconPath,
        string iconLabel,
        string iconAccentHex,
        string iconBackgroundHex)
    {
        Choice = choice;
        FolderPath = folderPath;
        IsFolder = isFolder;
        ShowsCategoryHeader = showsCategoryHeader;
        Label = label;
        Category = category;
        Description = description;
        IconPath = iconPath;
        IconLabel = iconLabel;
        IconAccentHex = iconAccentHex;
        IconBackgroundHex = iconBackgroundHex;
    }

    public ChoicePickerBrowserRowViewModel(ChoicePickerDisplayItemViewModel displayItem)
        : this(
            displayItem.Choice,
            [],
            false,
            displayItem.ShowsCategoryHeader,
            displayItem.Label,
            displayItem.Category,
            displayItem.Description,
            displayItem.IconPath,
            displayItem.IconLabel,
            displayItem.IconAccentHex,
            displayItem.IconBackgroundHex)
    {
    }

    public ChoicePickerBrowserRowViewModel(ChoicePickerItemViewModel choice, bool showPath)
        : this(
            choice,
            [],
            false,
            false,
            choice.Label,
            choice.Category,
            showPath && !string.IsNullOrWhiteSpace(choice.Category)
                ? $"{choice.Category}: {choice.Description}"
                : choice.Description,
            choice.IconPath,
            choice.IconLabel,
            choice.IconAccentHex,
            choice.IconBackgroundHex)
    {
    }

    public ChoicePickerBrowserRowViewModel(string folderLabel, IReadOnlyList<string> folderPath, int choiceCount)
        : this(
            null,
            folderPath,
            true,
            false,
            folderLabel,
            string.Join(" / ", folderPath),
            choiceCount == 1 ? "1 choice in this folder." : $"{choiceCount} choices in this folder.",
            FolderIconPath,
            "FOLDER",
            "#d9b657",
            "#3a2c10")
    {
    }

    public ChoicePickerItemViewModel? Choice { get; }
    public IReadOnlyList<string> FolderPath { get; }
    public bool IsFolder { get; }
    public bool ShowsCategoryHeader { get; }
    public string Value => Choice?.Value ?? string.Join("/", FolderPath);
    public string Label { get; }
    public string Category { get; }
    public string Description { get; }
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public string Tooltip => IsFolder
        ? $"{Label}\nOpen this folder.\n{Description}"
        : Choice?.Tooltip ?? Label;
    public string IconPath { get; }
    public string IconLabel { get; }
    public string IconAccentHex { get; }
    public string IconBackgroundHex { get; }
    public string RightLabel => IsFolder ? ">" : IconLabel;
    public string RowBackgroundHex => IsKeyboardSelected ? "#225f93" : IsCurrentValue ? "#263545" : "#2b2b2b";
    public string PrimaryForegroundHex => IsKeyboardSelected ? "#ffffff" : "#d8dde4";
    public string SecondaryForegroundHex => IsKeyboardSelected ? "#dcecff" : "#aeb8c4";

    [ObservableProperty]
    private bool isKeyboardSelected;

    [ObservableProperty]
    private bool isCurrentValue;

    partial void OnIsKeyboardSelectedChanged(bool value)
    {
        NotifyRowPresentationChanged();
    }

    partial void OnIsCurrentValueChanged(bool value)
    {
        NotifyRowPresentationChanged();
    }

    private void NotifyRowPresentationChanged()
    {
        OnPropertyChanged(nameof(RowBackgroundHex));
        OnPropertyChanged(nameof(PrimaryForegroundHex));
        OnPropertyChanged(nameof(SecondaryForegroundHex));
    }
}

public static class ChoicePickerSearchService
{
    /// <summary>
    /// Returns matching choices with category headers preserved for compact popup rendering.
    /// </summary>
    public static IReadOnlyList<ChoicePickerDisplayItemViewModel> FilterAndGroup(
        IEnumerable<ChoicePickerItemViewModel> items,
        string searchText)
    {
        var terms = (searchText ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var filtered = items
            .Where(item => terms.Count == 0 || terms.All(term => item.SearchText.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => CategoryOrder(item.Category))
            .ThenBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<ChoicePickerDisplayItemViewModel>();
        string? previousCategory = null;
        foreach (var item in filtered)
        {
            var showHeader = !item.Category.Equals(previousCategory, StringComparison.OrdinalIgnoreCase);
            result.Add(new ChoicePickerDisplayItemViewModel(item, showHeader));
            previousCategory = item.Category;
        }

        return result;
    }

    public static int CategoryOrder(string category)
    {
        return category switch
        {
            "Direct Value" => 0,
            "Object Context" => 1,
            "Current incompatible value" => 2,
            "Scene Object" => 3,
            "Variable" => 4,
            "Script Scope" => 5,
            "State Scope" => 6,
            "Graph Scope" => 7,
            "Global Scope" => 8,
            _ => 100
        };
    }
}

public static class ChoicePickerBrowserService
{
    /// <summary>
    /// Builds node-palette-like folder rows for choices while preserving the flat grouped picker for simple menus.
    /// </summary>
    public static IReadOnlyList<ChoicePickerBrowserRowViewModel> Browse(
        IEnumerable<ChoicePickerItemViewModel> items,
        string searchText,
        IReadOnlyList<string> currentFolderPath,
        bool useFolderNavigation)
    {
        var choices = items.ToList();
        if (!useFolderNavigation)
        {
            return ChoicePickerSearchService.FilterAndGroup(choices, searchText)
                .Select(item => new ChoicePickerBrowserRowViewModel(item))
                .ToList();
        }

        var terms = SearchTerms(searchText);
        if (terms.Count > 0)
        {
            return choices
                .Where(item => terms.All(term => item.SearchText.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(item => ChoicePickerSearchService.CategoryOrder(item.Category))
                .ThenBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                .Select(item => new ChoicePickerBrowserRowViewModel(item, showPath: true))
                .ToList();
        }

        var currentPath = currentFolderPath.ToList();
        var folderRows = choices
            .Select(choice => new { Choice = choice, Path = CategoryPath(choice.Category) })
            .Where(item => StartsWithPath(item.Path, currentPath) && item.Path.Count > currentPath.Count)
            .GroupBy(item => item.Path[currentPath.Count], StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var folderPath = currentPath.Concat([group.Key]).ToList();
                return new ChoicePickerBrowserRowViewModel(group.Key, folderPath, group.Count());
            })
            .OrderBy(row => ChoicePickerSearchService.CategoryOrder(row.Label))
            .ThenBy(row => row.Label, StringComparer.OrdinalIgnoreCase);

        var choiceRows = choices
            .Select(choice => new { Choice = choice, Path = CategoryPath(choice.Category) })
            .Where(item => PathEquals(item.Path, currentPath))
            .OrderBy(item => item.Choice.Label, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ChoicePickerBrowserRowViewModel(item.Choice, showPath: false));

        return folderRows.Concat(choiceRows).ToList();
    }

    public static IReadOnlyList<string> CategoryPath(string category)
    {
        var parts = (category ?? "")
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();

        return parts.Count == 0 ? ["General"] : parts;
    }

    private static IReadOnlyList<string> SearchTerms(string searchText)
    {
        return (searchText ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static bool StartsWithPath(IReadOnlyList<string> path, IReadOnlyList<string> prefix)
    {
        return path.Count >= prefix.Count &&
            prefix.Select((segment, index) => path[index].Equals(segment, StringComparison.OrdinalIgnoreCase)).All(matches => matches);
    }

    private static bool PathEquals(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        return left.Count == right.Count &&
            left.Select((segment, index) => segment.Equals(right[index], StringComparison.OrdinalIgnoreCase)).All(matches => matches);
    }
}
