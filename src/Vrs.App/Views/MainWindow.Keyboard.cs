using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Vrs.App.ViewModels;

namespace Vrs.App.Views;

public partial class MainWindow
{
    private void MainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.F11)
        {
            viewModel.ToggleOutputOverlayCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && viewModel.IsOutputOverlayOpen)
        {
            viewModel.CloseOutputOverlayCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (IsTextEditingTarget(e.Source))
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key == Key.C)
            {
                viewModel.CopyGraphSelection();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.X)
            {
                viewModel.CutGraphSelection();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.V)
            {
                viewModel.PasteGraphClipboard(viewModel.CanvasAddGraphX, viewModel.CanvasAddGraphY);
                e.Handled = true;
                return;
            }
        }

        if (e.KeyModifiers == KeyModifiers.None && e.Key is Key.Delete or Key.Back)
        {
            viewModel.DeleteGraphSelection();
            e.Handled = true;
        }
    }

    private static bool IsTextEditingTarget(object? source)
    {
        for (var current = source as StyledElement; current is not null; current = current.Parent as StyledElement)
        {
            if (current is TextBox)
            {
                return true;
            }
        }

        return false;
    }
}
