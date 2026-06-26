using System.Text.Json;
using Vrs.Core.Persistence;

namespace Vrs.Core.Catalog;

/// <summary>
/// Loads node packages without coupling the editor UI to JSON parsing rules.
/// </summary>
public sealed partial class NodeCatalogService
{
    public NodeCatalogData LoadCatalog(string catalogRoot)
    {
        var data = new NodeCatalogData { SourceLabel = catalogRoot };

        if (!Directory.Exists(catalogRoot))
        {
            data.Warnings.Add($"Catalog folder not found: {catalogRoot}");
            return data;
        }

        foreach (var manifestPath in Directory.EnumerateFiles(catalogRoot, "manifest.vrs-node.json", SearchOption.AllDirectories).Order())
        {
            try
            {
                var json = NormalizeManifestJson(File.ReadAllText(manifestPath));
                var entry = JsonSerializer.Deserialize(json, VrsJsonContext.Default.NodeCatalogEntry);
                if (entry is null)
                {
                    data.Warnings.Add($"Catalog manifest produced no entry: {manifestPath}");
                    continue;
                }

                entry.SourcePath = manifestPath;
                entry.PackageDirectory = Path.GetDirectoryName(manifestPath) ?? "";
                data.Nodes.Add(entry);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                data.Warnings.Add($"Catalog manifest failed: {manifestPath} ({ex.Message})");
            }
        }

        data.Nodes = data.Nodes
            .OrderBy(n => n.UtilityLayer, StringComparer.OrdinalIgnoreCase)
            .ThenBy(n => n.FamilyFolder, StringComparer.OrdinalIgnoreCase)
            .ThenBy(n => n.Subcategory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(n => n.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return data;
    }
}
