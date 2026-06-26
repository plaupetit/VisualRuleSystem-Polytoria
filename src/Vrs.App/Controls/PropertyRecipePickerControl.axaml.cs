using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Vrs.App.ViewModels;

namespace Vrs.App.Controls;

public partial class PropertyRecipePickerControl : UserControl
{
    public PropertyRecipePickerControl()
    {
        InitializeComponent();
    }

    private NodeParameterEditorViewModel? ViewModel => DataContext as NodeParameterEditorViewModel;

    private void PickerButtonClick(object? sender, RoutedEventArgs e)
    {
        RecipePopup.IsOpen = !RecipePopup.IsOpen;
        if (!RecipePopup.IsOpen)
        {
            return;
        }

        ViewModel?.OpenRecipeBrowser();
        Dispatcher.UIThread.Post(() => SearchBox.Focus());
    }

    private void BackButtonClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.GoBackRecipeBrowser();
        e.Handled = true;
    }

    private void RowButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PropertyRecipeBrowserRowViewModel row } ||
            ViewModel is not { } viewModel)
        {
            return;
        }

        var selectedRecipe = viewModel.ActivateRecipeBrowserRow(row);
        if (selectedRecipe)
        {
            RecipePopup.IsOpen = false;
        }

        e.Handled = true;
    }

    private void RowPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Button { Tag: PropertyRecipeBrowserRowViewModel row })
        {
            ViewModel?.SelectRecipeBrowserRow(row);
        }
    }

    private void SearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            RecipePopup.IsOpen = false;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            viewModel.MoveRecipeBrowserSelection(1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            viewModel.MoveRecipeBrowserSelection(-1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Back && string.IsNullOrWhiteSpace(viewModel.RecipeBrowserSearch) && viewModel.CanGoBackRecipeBrowser)
        {
            viewModel.GoBackRecipeBrowser();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            var selectedRecipe = viewModel.ActivateCurrentRecipeBrowserRow();
            if (selectedRecipe)
            {
                RecipePopup.IsOpen = false;
            }

            e.Handled = true;
        }
    }
}
