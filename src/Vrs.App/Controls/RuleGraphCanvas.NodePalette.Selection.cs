using Avalonia;
using Vrs.App.Services;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    // Selection and browser navigation for the GC2-style node palette.
    private void MoveNodePaletteSelection(int delta, IReadOnlyList<NodePaletteBrowserRow> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        nodePaletteSelectedIndex = Math.Clamp(nodePaletteSelectedIndex + delta, 0, entries.Count - 1);
        EnsureNodePaletteSelectionVisible(entries.Count);
        InvalidateVisual();
    }

    private void ResetNodePaletteSelection()
    {
        nodePaletteScrollIndex = 0;
        nodePaletteSelectedIndex = 0;
        CoerceNodePaletteSelection(NodePaletteEntries());
    }

    private void EnsureNodePaletteSelectionVisible(int entryCount)
    {
        var maxVisible = NodePaletteMaxVisibleEntries(entryCount);
        if (nodePaletteSelectedIndex < nodePaletteScrollIndex)
        {
            nodePaletteScrollIndex = nodePaletteSelectedIndex;
        }
        else if (nodePaletteSelectedIndex >= nodePaletteScrollIndex + maxVisible)
        {
            nodePaletteScrollIndex = nodePaletteSelectedIndex - maxVisible + 1;
        }

        nodePaletteScrollIndex = Math.Clamp(nodePaletteScrollIndex, 0, Math.Max(0, entryCount - maxVisible));
    }

    private void CoerceNodePaletteSelection(IReadOnlyList<NodePaletteBrowserRow> entries)
    {
        if (entries.Count == 0)
        {
            nodePaletteSelectedIndex = 0;
            nodePaletteScrollIndex = 0;
            return;
        }

        nodePaletteSelectedIndex = Math.Clamp(nodePaletteSelectedIndex, 0, entries.Count - 1);
    }

    private bool ResetNodePaletteFilters()
    {
        nodePaletteCompatibleOnly = true;
        nodePaletteApiSurfaceFilter = NodePaletteApiSurfaceFilter.Gameplay;
        nodePaletteCurrentIntentKey = "";
        nodePaletteCurrentDomainPath.Clear();
        ResetNodePaletteSelection();
        InvalidateVisual();
        return true;
    }

    private bool ToggleNodePaletteCompatibleOnly()
    {
        nodePaletteCompatibleOnly = !nodePaletteCompatibleOnly;
        ResetNodePaletteSelection();
        InvalidateVisual();
        return true;
    }

    private bool ToggleNodePaletteApiSurfaceFilter()
    {
        nodePaletteApiSurfaceFilter = nodePaletteApiSurfaceFilter switch
        {
            NodePaletteApiSurfaceFilter.Gameplay => NodePaletteApiSurfaceFilter.All,
            NodePaletteApiSurfaceFilter.All => NodePaletteApiSurfaceFilter.Creator,
            _ => NodePaletteApiSurfaceFilter.Gameplay
        };
        ResetNodePaletteSelection();
        InvalidateVisual();
        return true;
    }

    private void EnterNodePaletteFolder(NodePaletteBrowserRow row)
    {
        if (string.IsNullOrWhiteSpace(nodePaletteCurrentIntentKey))
        {
            nodePaletteCurrentIntentKey = row.IntentKey;
            nodePaletteCurrentDomainPath.Clear();
        }
        else
        {
            nodePaletteCurrentDomainPath.Clear();
            nodePaletteCurrentDomainPath.AddRange(row.DomainPath);
        }

        ResetNodePaletteSelection();
        InvalidateVisual();
    }

    private bool GoBackNodePalette()
    {
        if (!string.IsNullOrWhiteSpace(nodePaletteSearch))
        {
            nodePaletteSearch = "";
            ResetNodePaletteSelection();
            InvalidateVisual();
            return true;
        }

        if (nodePaletteCurrentDomainPath.Count > 0)
        {
            nodePaletteCurrentDomainPath.RemoveAt(nodePaletteCurrentDomainPath.Count - 1);
            ResetNodePaletteSelection();
            InvalidateVisual();
            return true;
        }

        if (!string.IsNullOrWhiteSpace(nodePaletteCurrentIntentKey))
        {
            nodePaletteCurrentIntentKey = "";
            ResetNodePaletteSelection();
            InvalidateVisual();
            return true;
        }

        return false;
    }

    private bool HitTestNodePaletteBack(Point point, Rect paletteBounds)
    {
        return CanGoBackNodePalette() && NodePaletteBackBounds(paletteBounds).Contains(point);
    }

    private bool HitTestNodePaletteCompatibleToggle(Point point, Rect paletteBounds)
    {
        return NodePaletteCompatibleToggleBounds(paletteBounds).Contains(point);
    }

    private bool HitTestNodePaletteApiSurfaceToggle(Point point, Rect paletteBounds)
    {
        return NodePaletteApiSurfaceToggleBounds(paletteBounds).Contains(point);
    }

    private bool HandleNodePalettePointerMoved(Point point)
    {
        var entries = NodePaletteEntries();
        var bounds = NodePaletteBounds(entries);
        var wasInside = nodePalettePointerInside;
        nodePalettePointerPoint = point;
        nodePalettePointerInside = bounds.Contains(point);
        if (!bounds.Contains(point))
        {
            if (wasInside)
            {
                InvalidateVisual();
            }

            return false;
        }

        var hit = HitTestNodePaletteEntry(point, entries);
        if (hit >= 0 && hit != nodePaletteSelectedIndex)
        {
            nodePaletteSelectedIndex = hit;
        }

        InvalidateVisual();
        return true;
    }

    private bool CanGoBackNodePalette()
    {
        return !string.IsNullOrWhiteSpace(nodePaletteSearch) ||
            !string.IsNullOrWhiteSpace(nodePaletteCurrentIntentKey) ||
            nodePaletteCurrentDomainPath.Count > 0;
    }

    private int HitTestNodePaletteEntry(Point point, IReadOnlyList<NodePaletteBrowserRow> entries)
    {
        foreach (var row in NodePaletteRows(entries))
        {
            if (row.EntryIndex >= 0 && row.Bounds.Contains(point))
            {
                return row.EntryIndex;
            }
        }

        return -1;
    }
}
