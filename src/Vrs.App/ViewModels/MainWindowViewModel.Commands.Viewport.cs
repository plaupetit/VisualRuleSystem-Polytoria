using CommunityToolkit.Mvvm.Input;
using Vrs.App.Services;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private void ZoomIn()
    {
        ApplyViewport(graphViewport.ZoomIn(CanvasZoom, CanvasPanX, CanvasPanY));
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ApplyViewport(graphViewport.ZoomOut(CanvasZoom, CanvasPanX, CanvasPanY));
    }

    [RelayCommand]
    private void ResetView()
    {
        ApplyViewport(graphViewport.Reset());
    }

    [RelayCommand]
    private void FitGraph()
    {
        var state = graphViewport.Fit(Nodes.ToList(), CanvasViewportWidth, CanvasViewportHeight, NodeGroups.ToList(), WireReroutes.ToList());
        ApplyViewport(state);
        if (!string.IsNullOrWhiteSpace(state.StatusText))
        {
            SetStatus(state.StatusText);
        }
    }

    private void ApplyViewport(GraphViewportState state)
    {
        CanvasZoom = state.Zoom;
        CanvasPanX = state.PanX;
        CanvasPanY = state.PanY;
    }
}
