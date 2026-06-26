using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Vrs.App.ViewModels;

namespace Vrs.App.Controls;

public partial class SearchableChoicePickerControl : UserControl
{
    // Keyboard selection is intentionally internal so the public API stays a simple value picker.
    private int keyboardIndex;
    private readonly List<string> currentFolderPath = [];

    public static readonly StyledProperty<IEnumerable<ChoicePickerItemViewModel>?> ItemsSourceProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, IEnumerable<ChoicePickerItemViewModel>?>(nameof(ItemsSource));

    public static readonly StyledProperty<string> SelectedValueProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, string>(
            nameof(SelectedValue),
            "",
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, string>(nameof(PlaceholderText), "Choose...");

    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, string>(nameof(SearchText), "");

    public static readonly StyledProperty<string> SearchPlaceholderTextProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, string>(nameof(SearchPlaceholderText), "Search...");

    public static readonly StyledProperty<string> PopupTitleProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, string>(nameof(PopupTitle), "Choose Value");

    public static readonly StyledProperty<string> PopupBreadcrumbProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, string>(nameof(PopupBreadcrumb), "Choices");

    public static readonly StyledProperty<bool> UseFolderNavigationProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, bool>(nameof(UseFolderNavigation));

    public static readonly StyledProperty<bool> CanGoBackProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, bool>(nameof(CanGoBack));

    public static readonly StyledProperty<string> BreadcrumbTextProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, string>(nameof(BreadcrumbText), "Choices");

    public static readonly StyledProperty<bool> HasNoChoicesProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, bool>(nameof(HasNoChoices));

    public static readonly StyledProperty<bool> IsDropDownOpenProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, bool>(nameof(IsDropDownOpen));

    public static readonly StyledProperty<string> ButtonTextProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, string>(nameof(ButtonText), "Choose...");

    public static readonly StyledProperty<string> ButtonDescriptionProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, string>(nameof(ButtonDescription), "");

    public static readonly StyledProperty<string> ButtonTooltipProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, string>(nameof(ButtonTooltip), "");

    public static readonly StyledProperty<string> ButtonIconPathProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, string>(nameof(ButtonIconPath), "/Assets/Icons/PolytoriaLike/tabler-list-tree.svg");

    public static readonly StyledProperty<string> ButtonIconAccentHexProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, string>(nameof(ButtonIconAccentHex), "#8fc8ff");

    public static readonly StyledProperty<string> ButtonIconBackgroundHexProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, string>(nameof(ButtonIconBackgroundHex), "#172f43");

    public static readonly StyledProperty<string> FooterTitleProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, string>(nameof(FooterTitle), "Choices");

    public static readonly StyledProperty<string> FooterDescriptionProperty =
        AvaloniaProperty.Register<SearchableChoicePickerControl, string>(nameof(FooterDescription), "");

    public SearchableChoicePickerControl()
    {
        InitializeComponent();
        RefreshChoices();
    }

    public IEnumerable<ChoicePickerItemViewModel>? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    public string PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public string SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public string SearchPlaceholderText
    {
        get => GetValue(SearchPlaceholderTextProperty);
        set => SetValue(SearchPlaceholderTextProperty, value);
    }

    public string PopupTitle
    {
        get => GetValue(PopupTitleProperty);
        set => SetValue(PopupTitleProperty, value);
    }

    public string PopupBreadcrumb
    {
        get => GetValue(PopupBreadcrumbProperty);
        set => SetValue(PopupBreadcrumbProperty, value);
    }

    public bool IsDropDownOpen
    {
        get => GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public bool UseFolderNavigation
    {
        get => GetValue(UseFolderNavigationProperty);
        set => SetValue(UseFolderNavigationProperty, value);
    }

    public bool CanGoBack
    {
        get => GetValue(CanGoBackProperty);
        private set => SetValue(CanGoBackProperty, value);
    }

    public string BreadcrumbText
    {
        get => GetValue(BreadcrumbTextProperty);
        private set => SetValue(BreadcrumbTextProperty, value);
    }

    public bool HasNoChoices
    {
        get => GetValue(HasNoChoicesProperty);
        private set => SetValue(HasNoChoicesProperty, value);
    }

    public string ButtonText
    {
        get => GetValue(ButtonTextProperty);
        private set => SetValue(ButtonTextProperty, value);
    }

    public string ButtonDescription
    {
        get => GetValue(ButtonDescriptionProperty);
        private set => SetValue(ButtonDescriptionProperty, value);
    }

    public string ButtonTooltip
    {
        get => GetValue(ButtonTooltipProperty);
        private set => SetValue(ButtonTooltipProperty, value);
    }

    public string ButtonIconPath
    {
        get => GetValue(ButtonIconPathProperty);
        private set => SetValue(ButtonIconPathProperty, value);
    }

    public string ButtonIconAccentHex
    {
        get => GetValue(ButtonIconAccentHexProperty);
        private set => SetValue(ButtonIconAccentHexProperty, value);
    }

    public string ButtonIconBackgroundHex
    {
        get => GetValue(ButtonIconBackgroundHexProperty);
        private set => SetValue(ButtonIconBackgroundHexProperty, value);
    }

    public string FooterTitle
    {
        get => GetValue(FooterTitleProperty);
        private set => SetValue(FooterTitleProperty, value);
    }

    public string FooterDescription
    {
        get => GetValue(FooterDescriptionProperty);
        private set => SetValue(FooterDescriptionProperty, value);
    }

    public ObservableCollection<ChoicePickerBrowserRowViewModel> FilteredItems { get; } = [];

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty || change.Property == SearchTextProperty || change.Property == UseFolderNavigationProperty)
        {
            RefreshChoices();
            return;
        }

        if (change.Property == SelectedValueProperty || change.Property == PlaceholderTextProperty)
        {
            RefreshSelectedButton();
            RefreshRowSelection();
            return;
        }

        if (change.Property == IsDropDownOpenProperty && IsDropDownOpen)
        {
            Dispatcher.UIThread.Post(() =>
            {
                currentFolderPath.Clear();
                SearchText = "";
                RefreshChoices();
                keyboardIndex = SelectedIndexOrFirst();
                RefreshRowSelection();
                SearchBox.Focus();
            });
            return;
        }

        if (change.Property == PopupBreadcrumbProperty)
        {
            NotifyNavigationChanged();
        }
    }

    private void PickerButtonClick(object? sender, RoutedEventArgs e)
    {
        IsDropDownOpen = !IsDropDownOpen;
    }

    private void ChoiceClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ChoicePickerBrowserRowViewModel item })
        {
            ActivateRow(item);
            e.Handled = true;
        }
    }

    private void ChoicePointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Button { Tag: ChoicePickerBrowserRowViewModel item })
        {
            keyboardIndex = Math.Max(0, FilteredItems.IndexOf(item));
            RefreshRowSelection();
        }
    }

    private void BackButtonClick(object? sender, RoutedEventArgs e)
    {
        GoBack();
        e.Handled = true;
    }

    private void SearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            IsDropDownOpen = false;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            MoveKeyboardSelection(1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            MoveKeyboardSelection(-1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Back && string.IsNullOrWhiteSpace(SearchText) && CanGoBack)
        {
            GoBack();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && FilteredItems.ElementAtOrDefault(keyboardIndex) is { } current)
        {
            ActivateRow(current);
            e.Handled = true;
        }
    }

    private void ActivateRow(ChoicePickerBrowserRowViewModel row)
    {
        if (row.IsFolder)
        {
            currentFolderPath.Clear();
            currentFolderPath.AddRange(row.FolderPath);
            SearchText = "";
            RefreshChoices();
            return;
        }

        if (row.Choice is { } choice)
        {
            SelectChoice(choice);
        }
    }

    private void GoBack()
    {
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            SearchText = "";
            RefreshChoices();
            return;
        }

        if (currentFolderPath.Count == 0)
        {
            return;
        }

        currentFolderPath.RemoveAt(currentFolderPath.Count - 1);
        RefreshChoices();
    }

    private void SelectChoice(ChoicePickerItemViewModel choice)
    {
        SelectedValue = choice.Value;
        SearchText = "";
        IsDropDownOpen = false;
    }

    private void RefreshChoices()
    {
        FilteredItems.Clear();
        foreach (var item in ChoicePickerBrowserService.Browse(ItemsSource ?? [], SearchText, currentFolderPath, UseFolderNavigation))
        {
            FilteredItems.Add(item);
        }

        keyboardIndex = SelectedIndexOrFirst();
        RefreshSelectedButton();
        RefreshRowSelection();
        NotifyNavigationChanged();
    }

    private void MoveKeyboardSelection(int offset)
    {
        if (FilteredItems.Count == 0)
        {
            keyboardIndex = 0;
            return;
        }

        keyboardIndex = Math.Clamp(keyboardIndex + offset, 0, FilteredItems.Count - 1);
        RefreshRowSelection();
    }

    private int SelectedIndexOrFirst()
    {
        for (var i = 0; i < FilteredItems.Count; i++)
        {
            if (!FilteredItems[i].IsFolder && FilteredItems[i].Value.Equals(SelectedValue, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    private void RefreshRowSelection()
    {
        for (var i = 0; i < FilteredItems.Count; i++)
        {
            var item = FilteredItems[i];
            item.IsKeyboardSelected = i == keyboardIndex;
            item.IsCurrentValue = !item.IsFolder && item.Value.Equals(SelectedValue, StringComparison.OrdinalIgnoreCase);
        }

        RefreshFooter();
    }

    private void RefreshFooter()
    {
        if (FilteredItems.Count == 0)
        {
            FooterTitle = "No choices";
            FooterDescription = string.IsNullOrWhiteSpace(SearchText)
                ? "No choices are available for this field."
                : "No choice matches this search.";
            return;
        }

        var current = FilteredItems[Math.Clamp(keyboardIndex, 0, FilteredItems.Count - 1)];
        FooterTitle = current.Label;
        FooterDescription = string.IsNullOrWhiteSpace(current.Description)
            ? current.Category
            : current.Description;
    }

    private void NotifyNavigationChanged()
    {
        BreadcrumbText = BuildBreadcrumbText();
        CanGoBack = !string.IsNullOrWhiteSpace(SearchText) || currentFolderPath.Count > 0;
        HasNoChoices = FilteredItems.Count == 0;
    }

    private string BuildBreadcrumbText()
    {
        if (!UseFolderNavigation)
        {
            return PopupBreadcrumb;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            return "Search Results";
        }

        return currentFolderPath.Count == 0
            ? PopupBreadcrumb
            : $"{PopupBreadcrumb} / {string.Join(" / ", currentFolderPath)}";
    }

    private void RefreshSelectedButton()
    {
        var selected = (ItemsSource ?? []).FirstOrDefault(item => item.Value.Equals(SelectedValue, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            ButtonText = string.IsNullOrWhiteSpace(SelectedValue) ? PlaceholderText : SelectedValue;
            ButtonDescription = string.IsNullOrWhiteSpace(SelectedValue) ? "" : "Custom value";
            ButtonTooltip = ButtonText;
            ButtonIconPath = "/Assets/Icons/PolytoriaLike/tabler-list-tree.svg";
            ButtonIconAccentHex = "#8fc8ff";
            ButtonIconBackgroundHex = "#172f43";
            return;
        }

        ButtonText = selected.Label;
        ButtonDescription = selected.Description;
        ButtonTooltip = selected.Tooltip;
        ButtonIconPath = selected.IconPath;
        ButtonIconAccentHex = selected.IconAccentHex;
        ButtonIconBackgroundHex = selected.IconBackgroundHex;
    }
}
