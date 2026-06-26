using System.ComponentModel;
using Vrs.App.Controls;
using Vrs.App.ViewModels;

namespace Vrs.App.Views;

public partial class MainWindow
{
    private INotifyPropertyChanged? previewSource;

    private void AttachPreviewSource()
    {
        if (previewSource is not null)
        {
            previewSource.PropertyChanged -= PreviewSourcePropertyChanged;
        }

        previewSource = DataContext as INotifyPropertyChanged;
        if (previewSource is not null)
        {
            previewSource.PropertyChanged += PreviewSourcePropertyChanged;
        }

        SetViewModelFocusState(IsActive);
        SyncLuauPreviewEditor();
    }

    private void SetViewModelFocusState(bool focused)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetWindowFocusState(focused);
        }
    }

    private void PreviewSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName) ||
            e.PropertyName == nameof(MainWindowViewModel.LuauPreview) ||
            e.PropertyName == nameof(MainWindowViewModel.LuauPreviewHighlightedLineNumbers) ||
            e.PropertyName == nameof(MainWindowViewModel.LuauPreviewFocusLineNumber) ||
            e.PropertyName == nameof(MainWindowViewModel.LuauPreviewFocusRequestId))
        {
            SyncLuauPreviewEditor();
        }
    }

    private void SyncLuauPreviewEditor()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            SyncLuauPreviewEditor(LuauPreviewEditor, viewModel);
        }
    }

    private static void SyncLuauPreviewEditor(LuauCodePreviewControl editor, MainWindowViewModel viewModel)
    {
        editor.CodeText = viewModel.LuauPreview;
        editor.HighlightedLineNumbers = viewModel.LuauPreviewHighlightedLineNumbers;
        editor.FocusLineNumber = viewModel.LuauPreviewFocusLineNumber;
        editor.FocusRequestId = viewModel.LuauPreviewFocusRequestId;
    }
}
