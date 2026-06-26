using Avalonia;
using Vrs.Core.Catalog;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    // Shared labels, ordering, and small internal data records used by all palette slices.
    private static string RuntimeBadge(NodeCatalogEntry entry)
    {
        return NodeCatalogPresentationService.GetRuntimeLabel(entry);
    }

    private readonly record struct NodePaletteRow(Rect Bounds, int EntryIndex, string Header);
    private readonly record struct NodePaletteTooltip(string Title, string Body, string AccentHex);
}
