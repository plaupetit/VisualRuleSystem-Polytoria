using System.Text.Json;
using System.Text.Json.Serialization;
using Vrs.Graph.Model;

namespace Vrs.Core.Persistence;

public sealed class PortableScriptGraphDocument
{
    public string Format { get; set; } = PortableScriptGraphService.FormatName;
    public int FormatVersion { get; set; } = PortableScriptGraphService.CurrentVersion;
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.UtcNow;
    public GraphScriptBinding Script { get; set; } = new();
    public GraphAuthoringMode AuthoringMode { get; set; } = GraphAuthoringMode.PolyCreatorLessDraft;
    public RuleGraph Graph { get; set; } = new();
    public List<PortableRuleSummary> ReadableIndex { get; set; } = [];
}

public sealed class PortableRuleSummary
{
    public string RuleId { get; set; } = "";
    public string RuleName { get; set; } = "";
    public GraphScriptKind ScriptKind { get; set; } = GraphScriptKind.Server;
    public List<PortableNodeSummary> Nodes { get; set; } = [];
    public List<PortableConnectionSummary> Connections { get; set; } = [];
    public List<string> Groups { get; set; } = [];
    public List<string> Reroutes { get; set; } = [];
}

public sealed class PortableNodeSummary
{
    public string NodeId { get; set; } = "";
    public string CatalogId { get; set; } = "";
    public NodeKind Kind { get; set; } = NodeKind.Action;
    public string Label { get; set; } = "";
    public string ConfiguredAs { get; set; } = "";
    public float GraphX { get; set; }
    public float GraphY { get; set; }
}

public sealed class PortableConnectionSummary
{
    public string ConnectionId { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Kind { get; set; } = "";
    public List<string> RerouteIds { get; set; } = [];
}

public sealed class PortableScriptGraphLoadResult
{
    public RuleGraph Graph { get; init; } = new();
    public List<string> Warnings { get; init; } = [];
}

public static class PortableScriptGraphService
{
    public const string FormatName = "VisualRuleSystem.Polytoria.ScriptGraph";
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(RuleGraph graph)
    {
        var normalized = CloneGraph(graph);
        RuleGraphDocumentNormalizer.NormalizeScriptBinding(normalized);
        var document = new PortableScriptGraphDocument
        {
            ExportedAt = DateTimeOffset.UtcNow,
            Script = CloneScriptBinding(normalized.Script),
            AuthoringMode = normalized.AuthoringMode,
            Graph = normalized,
            ReadableIndex = BuildReadableIndex(normalized)
        };

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public static PortableScriptGraphLoadResult Deserialize(string json)
    {
        var warnings = new List<string>();
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("format", out var formatElement) &&
            !string.Equals(formatElement.GetString(), FormatName, StringComparison.Ordinal))
        {
            warnings.Add($"Unexpected portable graph format '{formatElement.GetString()}'. Attempting best-effort import.");
        }

        if (document.RootElement.TryGetProperty("formatVersion", out var versionElement) &&
            versionElement.TryGetInt32(out var version) &&
            version != CurrentVersion)
        {
            warnings.Add($"Portable graph version {version} differs from supported version {CurrentVersion}. Attempting best-effort import.");
        }

        RuleGraph? graph = null;
        if (document.RootElement.TryGetProperty("graph", out var graphElement))
        {
            graph = graphElement.Deserialize<RuleGraph>(JsonOptions);
        }
        else
        {
            graph = document.RootElement.Deserialize<RuleGraph>(JsonOptions);
        }

        if (graph is null)
        {
            throw new JsonException("The portable graph document did not contain a graph.");
        }

        if (graph.Version != 3)
        {
            warnings.Add($"Graph version {graph.Version} was normalized to v3 for this build.");
            graph.Version = 3;
        }

        if (document.RootElement.TryGetProperty("script", out var scriptElement))
        {
            var script = scriptElement.Deserialize<GraphScriptBinding>(JsonOptions);
            if (script is not null)
            {
                graph.Script = script;
            }
        }

        if (document.RootElement.TryGetProperty("authoringMode", out var modeElement) &&
            Enum.TryParse<GraphAuthoringMode>(modeElement.GetString(), ignoreCase: true, out var mode))
        {
            graph.AuthoringMode = mode;
        }

        RuleGraphDocumentNormalizer.NormalizeScriptBinding(graph);
        return new PortableScriptGraphLoadResult
        {
            Graph = graph,
            Warnings = warnings
        };
    }

    public static async Task SaveAsync(RuleGraph graph, string path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        await File.WriteAllTextAsync(path, Serialize(graph), cancellationToken).ConfigureAwait(false);
    }

    public static async Task<PortableScriptGraphLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Deserialize(json);
    }

    private static RuleGraph CloneGraph(RuleGraph graph)
    {
        return JsonSerializer.Deserialize<RuleGraph>(JsonSerializer.Serialize(graph, JsonOptions), JsonOptions)
            ?? throw new JsonException("Failed to clone graph for portable export.");
    }

    private static GraphScriptBinding CloneScriptBinding(GraphScriptBinding binding)
    {
        return JsonSerializer.Deserialize<GraphScriptBinding>(JsonSerializer.Serialize(binding, JsonOptions), JsonOptions)
            ?? new GraphScriptBinding();
    }

    private static List<PortableRuleSummary> BuildReadableIndex(RuleGraph graph)
    {
        return graph.Rules.Select(rule => new PortableRuleSummary
        {
            RuleId = rule.Id,
            RuleName = rule.Name,
            ScriptKind = rule.ScriptKind,
            Nodes = rule.Nodes.Select(node => new PortableNodeSummary
            {
                NodeId = node.Id,
                CatalogId = node.CatalogId,
                Kind = node.Kind,
                Label = node.Label,
                ConfiguredAs = BuildConfiguredSummary(node),
                GraphX = node.GraphX,
                GraphY = node.GraphY
            }).ToList(),
            Connections = rule.Connections.Select(connection => new PortableConnectionSummary
            {
                ConnectionId = connection.Id,
                From = $"{connection.From.NodeId}.{connection.From.PortId}",
                To = $"{connection.To.NodeId}.{connection.To.PortId}",
                Kind = connection.ConnectionKind.ToString(),
                RerouteIds = connection.RerouteIds.ToList()
            }).ToList(),
            Groups = rule.NodeGroups.Select(group => $"{group.Id}: {group.Name} [{string.Join(", ", group.MemberNodeIds)}]").ToList(),
            Reroutes = rule.WireReroutes.Select(reroute => $"{reroute.Id}: {reroute.GraphX:0}, {reroute.GraphY:0}").ToList()
        }).ToList();
    }

    private static string BuildConfiguredSummary(RuleNode node)
    {
        var parameters = node.Parameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
            .Select(parameter => $"{parameter.Key}={parameter.Value}")
            .ToList();
        return parameters.Count == 0
            ? node.Label
            : $"{node.Label}: {string.Join(", ", parameters)}";
    }
}
