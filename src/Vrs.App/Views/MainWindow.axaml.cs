using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Vrs.App.ViewModels;

namespace Vrs.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Activated += (_, _) => SetViewModelFocusState(focused: true);
        Deactivated += (_, _) => SetViewModelFocusState(focused: false);
        KeyDown += MainWindowKeyDown;
        DataContextChanged += (_, _) => AttachPreviewSource();
        AttachPreviewSource();
    }

    private async void ChangeProjectClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Polytoria 2.0 project folder",
            AllowMultiple = false
        });

        if (DataContext is MainWindowViewModel viewModel)
        {
            var projectRoot = folders.FirstOrDefault()?.Path.LocalPath;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                viewModel.CancelProjectSelection();
                return;
            }

            await viewModel.SetActiveProjectRootAsync(projectRoot);
        }
    }
}
