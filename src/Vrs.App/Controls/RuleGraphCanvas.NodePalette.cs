using Avalonia;
using Avalonia.Input;
using Vrs.App.Services;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    // Node palette entrypoints: open/close, query visible entries, accept selection, and handle pointer/keyboard input.
    private void OpenNodePalette(GraphPoint graphPoint, GraphPinHit? connectFrom = null)
    {
        nodePaletteOpen = true;
        nodePalettePoint = ToScreen(graphPoint);
        nodePaletteGraphPoint = graphPoint;
        nodePaletteConnectFrom = connectFrom;
        nodePaletteSearch = "";
        nodePaletteSelectedIndex = 0;
        nodePaletteScrollIndex = 0;
        nodePaletteCompatibleOnly = true;
        nodePaletteApiSurfaceFilter = NodePaletteApiSurfaceFilter.Gameplay;
        nodePaletteCurrentIntentKey = "";
        nodePaletteCurrentDomainPath.Clear();
        nodePalettePointerPoint = nodePalettePoint;
        nodePalettePointerInside = false;
        Focus();
        InvalidateVisual();
    }

    private void CloseNodePalette()
    {
        nodePaletteOpen = false;
        nodePaletteSearch = "";
        nodePaletteConnectFrom = null;
        nodePaletteSelectedIndex = 0;
        nodePaletteScrollIndex = 0;
        nodePalettePointerInside = false;
        InvalidateVisual();
    }

    private IReadOnlyList<NodeCatalogEntry> AddableCatalogEntries(GraphPinHit? connectFrom = null)
    {
        return CatalogEntries?
            .OfType<NodeCatalogEntry>()
            .Where(NodeCatalogService.IsAddable)
            .Where(entry => entry.Kind != NodeKind.Property)
            .ToList() ?? [];
    }

    private IReadOnlyList<NodePaletteBrowserRow> NodePaletteEntries()
    {
        return NodePaletteEntries(includeIncompatible: !nodePaletteCompatibleOnly);
    }

    private IReadOnlyList<NodePaletteBrowserRow> NodePaletteEntries(bool includeIncompatible)
    {
        // Keep incompatible entries in the query path so the palette can explain why they are disabled.
        return NodePaletteQuery.Browse(
            AddableCatalogEntries(nodePaletteConnectFrom),
            new NodePaletteBrowserQueryOptions(
                Search: nodePaletteSearch,
                ScriptKind: SelectedScriptKind,
                CompatibleOnly: !includeIncompatible,
                ApiSurfaceFilter: nodePaletteApiSurfaceFilter,
                CurrentIntentKey: nodePaletteCurrentIntentKey,
                CurrentDomainPath: nodePaletteCurrentDomainPath.ToList(),
                IncompatibilityReason: entry => NodePaletteIncompatibilityReason(entry, nodePaletteConnectFrom)));
    }

    private void AcceptNodePaletteSelection()
    {
        var entries = NodePaletteEntries();
        if (entries.Count == 0)
        {
            return;
        }

        CoerceNodePaletteSelection(entries);
        nodePaletteSelectedIndex = Math.Clamp(nodePaletteSelectedIndex, 0, entries.Count - 1);
        var selectedEntry = entries[nodePaletteSelectedIndex];
        if (selectedEntry.Kind == NodePaletteBrowserRowKind.Folder)
        {
            EnterNodePaletteFolder(selectedEntry);
            return;
        }

        if (!selectedEntry.IsCompatible || selectedEntry.Entry is null)
        {
            InvalidateVisual();
            return;
        }

        var selected = selectedEntry.Entry;
        var connectFrom = nodePaletteConnectFrom;
        var graphPoint = nodePaletteGraphPoint;
        CloseNodePalette();
        Host?.AddCatalogNodeAtGraphPoint(
            selected.IdBase,
            graphPoint.X,
            graphPoint.Y,
            connectFrom?.NodeId ?? "",
            connectFrom?.PortId ?? "");
    }

    private bool HandleNodePalettePointerPressed(Point point, PointerPointProperties properties)
    {
        var entries = NodePaletteEntries();
        var bounds = NodePaletteBounds(entries);
        if (!bounds.Contains(point))
        {
            if (properties.IsLeftButtonPressed)
            {
                CloseNodePalette();
                return true;
            }

            CloseNodePalette();
            return false;
        }

        if (properties.IsLeftButtonPressed)
        {
            if (HitTestNodePaletteBack(point, bounds))
            {
                _ = GoBackNodePalette();
                return true;
            }

            if (HitTestNodePaletteCompatibleToggle(point, bounds))
            {
                _ = ToggleNodePaletteCompatibleOnly();
                return true;
            }

            if (HitTestNodePaletteApiSurfaceToggle(point, bounds))
            {
                _ = ToggleNodePaletteApiSurfaceFilter();
                return true;
            }

            var hit = HitTestNodePaletteEntry(point, entries);
            if (hit >= 0)
            {
                nodePaletteSelectedIndex = hit;
                AcceptNodePaletteSelection();
            }

            return true;
        }

        return true;
    }

    private bool HandleNodePaletteKeyDown(KeyEventArgs e)
    {
        var entries = NodePaletteEntries();
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var handled = e.Key switch
            {
                Key.D0 or Key.NumPad0 => ResetNodePaletteFilters(),
                Key.G => ToggleNodePaletteApiSurfaceFilter(),
                Key.K => ToggleNodePaletteCompatibleOnly(),
                _ => false
            };

            if (handled)
            {
                e.Handled = true;
                return true;
            }
        }

        switch (e.Key)
        {
            case Key.Escape:
                CloseNodePalette();
                e.Handled = true;
                return true;
            case Key.Enter:
                AcceptNodePaletteSelection();
                e.Handled = true;
                return true;
            case Key.Down:
                MoveNodePaletteSelection(1, entries);
                e.Handled = true;
                return true;
            case Key.Up:
                MoveNodePaletteSelection(-1, entries);
                e.Handled = true;
                return true;
            case Key.PageDown:
                MoveNodePaletteSelection(NodePaletteMaxVisibleEntries(entries.Count), entries);
                e.Handled = true;
                return true;
            case Key.PageUp:
                MoveNodePaletteSelection(-NodePaletteMaxVisibleEntries(entries.Count), entries);
                e.Handled = true;
                return true;
            case Key.Back:
                if (nodePaletteSearch.Length > 0)
                {
                    nodePaletteSearch = nodePaletteSearch[..^1];
                    ResetNodePaletteSelection();
                    InvalidateVisual();
                }
                else
                {
                    _ = GoBackNodePalette();
                }

                e.Handled = true;
                return true;
            case Key.Delete:
                nodePaletteSearch = "";
                ResetNodePaletteSelection();
                InvalidateVisual();
                e.Handled = true;
                return true;
            case Key.Space:
                nodePaletteSearch += " ";
                ResetNodePaletteSelection();
                InvalidateVisual();
                e.Handled = true;
                return true;
            default:
                return false;
        }
    }

}
