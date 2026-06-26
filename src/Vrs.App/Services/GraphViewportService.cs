using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.App.Services;

/// <summary>
/// Keeps graph viewport math outside Avalonia-bound ViewModels so zoom and fit
/// behavior can be regression-tested without constructing UI controls.
/// </summary>
public sealed class GraphViewportService
{
    private const double MinZoom = 0.25;
    private const double MaxZoom = 2.5;
    private const double FitMinZoom = 0.35;
    private const double FitMaxZoom = 1.6;
    private const double FitPadding = 80.0;
    private const double MinAvailableWidth = 200.0;
    private const double MinAvailableHeight = 160.0;

    public GraphViewportState ZoomIn(double currentZoom, double currentPanX, double currentPanY)
    {
        return new GraphViewportState(
            Math.Clamp(currentZoom * 1.12, MinZoom, MaxZoom),
            currentPanX,
            currentPanY,
            "");
    }

    public GraphViewportState ZoomOut(double currentZoom, double currentPanX, double currentPanY)
    {
        return new GraphViewportState(
            Math.Clamp(currentZoom * 0.88, MinZoom, MaxZoom),
            currentPanX,
            currentPanY,
            "");
    }

    public GraphViewportState Reset()
    {
        return new GraphViewportState(1.0, 0.0, 0.0, "");
    }

    public GraphViewportState Fit(
        IReadOnlyCollection<RuleNode> nodes,
        double viewportWidth,
        double viewportHeight,
        IReadOnlyCollection<RuleNodeGroup>? groups = null,
        IReadOnlyCollection<RuleWireReroute>? reroutes = null)
    {
        groups ??= [];
        reroutes ??= [];
        if (nodes.Count == 0 && groups.Count == 0 && reroutes.Count == 0)
        {
            return new GraphViewportState(1.0, 0.0, 0.0, "Reset empty canvas view.");
        }

        var nodeRects = nodes.Select(node => new GraphRect(node.GraphX, node.GraphY, RuleGraphGeometryService.NodeWidth, RuleGraphGeometryService.NodeHeightFor(node)));
        var groupRects = groups.Select(group => new GraphRect(group.GraphX, group.GraphY, group.Width, group.Height));
        var rerouteRects = reroutes.Select(reroute => new GraphRect(reroute.GraphX - 16.0F, reroute.GraphY - 16.0F, 32.0F, 32.0F));
        var rects = nodeRects.Concat(groupRects).Concat(rerouteRects).ToList();
        var minX = rects.Min(rect => rect.X);
        var minY = rects.Min(rect => rect.Y);
        var maxX = rects.Max(rect => rect.Right);
        var maxY = rects.Max(rect => rect.Bottom);
        var graphWidth = Math.Max(1.0, maxX - minX);
        var graphHeight = Math.Max(1.0, maxY - minY);
        var availableWidth = Math.Max(MinAvailableWidth, viewportWidth - (FitPadding * 2.0));
        var availableHeight = Math.Max(MinAvailableHeight, viewportHeight - (FitPadding * 2.0));
        var nextZoom = Math.Clamp(Math.Min(availableWidth / graphWidth, availableHeight / graphHeight), FitMinZoom, FitMaxZoom);

        return new GraphViewportState(
            nextZoom,
            ((viewportWidth - (graphWidth * nextZoom)) / 2.0) - (minX * nextZoom),
            ((viewportHeight - (graphHeight * nextZoom)) / 2.0) - (minY * nextZoom),
            "Framed graph in canvas.");
    }
}

/// <summary>
/// Immutable viewport result returned by graph view commands before the
/// ViewModel copies values into Avalonia-bound properties.
/// </summary>
public sealed record GraphViewportState(double Zoom, double PanX, double PanY, string StatusText);
