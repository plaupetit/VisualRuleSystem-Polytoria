using CommunityToolkit.Mvvm.Input;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private void ToggleOutputOverlay()
    {
        IsOutputOverlayOpen = !IsOutputOverlayOpen;
    }

    [RelayCommand]
    private void CloseOutputOverlay()
    {
        IsOutputOverlayOpen = false;
    }
}
