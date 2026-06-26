using CommunityToolkit.Mvvm.Input;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private void DeleteSelection()
    {
        DeleteGraphSelection();
    }

    [RelayCommand]
    private void CreateRuleFragmentFromSelection()
    {
        CreateFragmentFromSelection(GraphFragmentKind.Rule);
    }

    [RelayCommand]
    private void CreateStateFragmentFromSelection()
    {
        CreateFragmentFromSelection(GraphFragmentKind.State);
    }

    [RelayCommand]
    private void CreateNodeGroupFromSelection()
    {
        CreateGroupFromSelection();
    }

    [RelayCommand]
    private void AddRerouteToSelectedWire()
    {
        var rule = EnsureRule();
        if (SelectedConnectionIndex < 0 || SelectedConnectionIndex >= rule.Connections.Count)
        {
            SetStatus("Select a wire first, or right-click a wire and choose Add Reroute Here.");
            return;
        }

        var connection = rule.Connections[SelectedConnectionIndex];
        var geometry = new RuleGraphGeometryService();
        var nodesById = rule.Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        var reroutesById = rule.WireReroutes.ToDictionary(reroute => reroute.Id, StringComparer.OrdinalIgnoreCase);
        if (!geometry.TryGetConnectionPathSegments(connection, nodesById, reroutesById, out var segments) || segments.Count == 0)
        {
            SetStatus("Selected wire cannot receive a reroute because its endpoints are missing.");
            return;
        }

        var segmentIndex = Math.Clamp(segments.Count / 2, 0, segments.Count - 1);
        var segment = segments[segmentIndex];
        AddWireRerouteToConnection(
            SelectedConnectionIndex,
            (segment.From.X + segment.To.X) * 0.5F,
            (segment.From.Y + segment.To.Y) * 0.5F,
            segmentIndex);
    }

    [RelayCommand]
    private void AddRuleFragmentAtCanvasPosition()
    {
        AddFragmentAtGraphPoint(GraphFragmentKind.Rule, CanvasAddGraphX, CanvasAddGraphY);
    }

    [RelayCommand]
    private void AddStateFragmentAtCanvasPosition()
    {
        AddFragmentAtGraphPoint(GraphFragmentKind.State, CanvasAddGraphX, CanvasAddGraphY);
    }

    [RelayCommand]
    private void ExpandSelectedFragment()
    {
        var fragment = SelectedFragment;
        if (fragment is null)
        {
            SetStatus("No fragment is selected.");
            return;
        }

        ExpandGraphFragment(fragment.Id);
    }
}
