using System.Text.Json;
using Vrs.Graph.Model;

namespace Vrs.Core.Persistence;

/// <summary>
/// Strict JSON persistence for visual graph documents.
/// </summary>
public static class RuleGraphJson
{
    public static string Serialize(RuleGraph graph)
    {
        return JsonSerializer.Serialize(graph, VrsJsonContext.Default.RuleGraph);
    }

    public static RuleGraph Deserialize(string json)
    {
        var graph = JsonSerializer.Deserialize(json, VrsJsonContext.Default.RuleGraph)
            ?? throw new JsonException("The graph JSON did not contain a RuleGraph document.");

        if (graph.Version != 3)
        {
            throw new JsonException($"Unsupported graph version {graph.Version}. This build expects VisualRuleSystem graph v3.");
        }

        RuleGraphDocumentNormalizer.NormalizeScriptBinding(graph);
        return graph;
    }

    public static async Task SaveAsync(RuleGraph graph, string path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        await File.WriteAllTextAsync(path, Serialize(graph), cancellationToken).ConfigureAwait(false);
    }

    public static async Task SaveAtomicAsync(RuleGraph graph, string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");

        var temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, Serialize(graph), cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static async Task<RuleGraph> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Deserialize(json);
    }
}
