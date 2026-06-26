namespace Vrs.Core.Catalog;

public sealed class CatalogIndexService
{
    private readonly NodeCatalogService catalogService;
    private string loadedRoot = "";
    private string loadedFingerprint = "";
    private NodeCatalogData loadedCatalog = new();

    public CatalogIndexService(NodeCatalogService? catalogService = null)
    {
        this.catalogService = catalogService ?? new NodeCatalogService();
    }

    public NodeCatalogData GetCatalog(string catalogRoot)
    {
        var fingerprint = BuildFingerprint(catalogRoot);
        if (string.Equals(catalogRoot, loadedRoot, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(fingerprint, loadedFingerprint, StringComparison.Ordinal))
        {
            return loadedCatalog;
        }

        loadedCatalog = catalogService.LoadCatalog(catalogRoot);
        loadedRoot = catalogRoot;
        loadedFingerprint = fingerprint;
        return loadedCatalog;
    }

    public IReadOnlyList<NodeCatalogEntry> Search(IEnumerable<NodeCatalogEntry> entries, string search)
    {
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var scoredEntries = entries
            .Select(entry => new
            {
                Entry = entry,
                Search = NodeCatalogSearchService.Match(entry, search)
            })
            .Where(item => item.Search.IsMatch);

        var ordered = scoredEntries
            .OrderBy(item => NodeCatalogService.IsAddable(item.Entry) ? 0 : 1);

        if (hasSearch)
        {
            ordered = ordered.ThenByDescending(item => item.Search.Score);
        }

        return ordered
            .ThenBy(item => item.Entry.Kind)
            .ThenBy(item => item.Entry.Subcategory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Entry.Label, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Entry)
            .ToList();
    }

    private static string BuildFingerprint(string catalogRoot)
    {
        if (!Directory.Exists(catalogRoot))
        {
            return "missing";
        }

        var manifests = Directory.EnumerateFiles(catalogRoot, "manifest.vrs-node.json", SearchOption.AllDirectories)
            .Select(path =>
            {
                var info = new FileInfo(path);
                return $"{path}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
            })
            .Order(StringComparer.OrdinalIgnoreCase);

        return string.Join('\n', manifests);
    }
}
