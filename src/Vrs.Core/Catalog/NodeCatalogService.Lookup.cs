using Vrs.Graph.Model;

namespace Vrs.Core.Catalog;

public sealed partial class NodeCatalogService
{
    // Catalog lookup and filtering are kept independent from manifest parsing
    // so UI palettes and graph services share one definition of addable nodes.
    public static NodeCatalogEntry? FindByCatalogId(IEnumerable<NodeCatalogEntry> entries, string catalogId)
    {
        if (string.IsNullOrWhiteSpace(catalogId))
        {
            return null;
        }

        return entries.FirstOrDefault(entry =>
            string.Equals(entry.IdBase, catalogId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.Type, catalogId, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsAddable(NodeCatalogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.IdBase))
        {
            return false;
        }

        return !entry.Status.Contains("Reference", StringComparison.OrdinalIgnoreCase) &&
            !entry.Status.Contains("Documentation", StringComparison.OrdinalIgnoreCase) &&
            entry.Kind is NodeKind.Trigger or NodeKind.Condition or NodeKind.Action or NodeKind.Property;
    }

    public static bool IsCompatibleWithScriptKind(NodeCatalogEntry entry, GraphScriptKind scriptKind)
    {
        var family = entry.RuntimeFamily.Trim();
        if (string.IsNullOrWhiteSpace(family) ||
            family.Equals("Shared", StringComparison.OrdinalIgnoreCase) ||
            family.Equals("Any", StringComparison.OrdinalIgnoreCase) ||
            family.Equals("Generic", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return scriptKind switch
        {
            GraphScriptKind.Server => family.Equals("Server", StringComparison.OrdinalIgnoreCase) ||
                family.Equals("ServerScript", StringComparison.OrdinalIgnoreCase),
            GraphScriptKind.Local => family.Equals("Local", StringComparison.OrdinalIgnoreCase) ||
                family.Equals("Client", StringComparison.OrdinalIgnoreCase) ||
                family.Equals("ClientScript", StringComparison.OrdinalIgnoreCase),
            GraphScriptKind.Module => family.Equals("Module", StringComparison.OrdinalIgnoreCase) ||
                family.Equals("ModuleScript", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    public static bool Matches(NodeCatalogEntry entry, string search)
    {
        return NodeCatalogSearchService.Match(entry, search).IsMatch;
    }
}
