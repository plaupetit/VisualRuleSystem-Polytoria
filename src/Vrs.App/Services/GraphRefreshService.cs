using System.Diagnostics.CodeAnalysis;
using Vrs.Core.Catalog;
using Vrs.Core.Export;
using Vrs.Core.Validation;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.App.Services;

/// <summary>
/// Container-neutral refresh logic for graph normalization, validation, exportability,
/// and Luau preview generation. ViewModels keep ownership of bindable collections
/// and property notifications.
/// </summary>
public sealed class GraphRefreshService
{
    private readonly LuauExporter exporter;
    private readonly RuleGraphValidator validator;

    public GraphRefreshService(LuauExporter? exporter = null, RuleGraphValidator? validator = null)
    {
        this.exporter = exporter ?? new LuauExporter();
        this.validator = validator ?? new RuleGraphValidator();
    }

    public bool HasExportableRule(RuleGraph graph)
    {
        return graph.Rules.FirstOrDefault()?.Nodes.Count > 0;
    }

    public bool TryGetExportableRule(RuleGraph graph, [NotNullWhen(true)] out Rule? rule)
    {
        rule = graph.Rules.FirstOrDefault();

        // Export-facing actions must not create a default rule because an empty canvas is an intentional authored state.
        return rule is not null && rule.Nodes.Count > 0;
    }

    public Rule EnsureRule(RuleGraph graph)
    {
        if (graph.Rules.Count == 0)
        {
            graph.Rules.Add(new Rule
            {
                Id = "RULE_Default",
                Name = "DefaultRule",
                Description = "Default rule created by the editor."
            });
        }

        return graph.Rules[0];
    }

    public bool NormalizeHumanFlowPorts(RuleGraph graph)
    {
        var changed = false;
        foreach (var rule in graph.Rules)
        {
            foreach (var node in rule.Nodes.Where(node => node.Kind is NodeKind.Trigger or NodeKind.Condition or NodeKind.Action))
            {
                var canonicalPorts = GraphPortDefaults.CreateDefaultPorts(node.Kind);
                if (!SamePorts(node.Ports, canonicalPorts))
                {
                    node.Ports = canonicalPorts;
                    changed = true;
                }
            }

            var removed = rule.Connections.RemoveAll(connection =>
                connection.ConnectionKind != GraphConnectionKind.Flow ||
                !HasEndpoint(rule, connection.From) ||
                !HasEndpoint(rule, connection.To));
            changed |= removed > 0;
            if (removed > 0)
            {
                var referencedReroutes = rule.Connections
                    .SelectMany(connection => connection.RerouteIds)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var removedReroutes = rule.WireReroutes.RemoveAll(reroute => !referencedReroutes.Contains(reroute.Id));
                foreach (var group in rule.NodeGroups)
                {
                    group.MemberRerouteIds.RemoveAll(id => !referencedReroutes.Contains(id));
                }

                changed |= removedReroutes > 0;
            }
        }

        return changed;
    }

    public bool BackfillCatalogParameters(RuleGraph graph, IEnumerable<NodeCatalogEntry> catalogEntries)
    {
        var catalogById = catalogEntries.ToDictionary(entry => entry.IdBase, StringComparer.OrdinalIgnoreCase);
        var changed = false;
        foreach (var node in graph.Rules.SelectMany(rule => rule.Nodes))
        {
            if (!catalogById.TryGetValue(node.CatalogId, out var entry))
            {
                continue;
            }

            // Keep old saved graphs compatible when catalog manifests add
            // required inspector parameters such as trigger targets.
            changed |= NodeCatalogService.BackfillMissingParameters(node, entry);
        }

        return changed;
    }

    public IReadOnlyList<ValidationMessage> ValidateGraph(RuleGraph graph, IEnumerable<NodeCatalogEntry> catalogEntries)
    {
        return validator.Validate(graph, catalogEntries).Messages;
    }

    public GraphPreviewRefreshResult BuildLuauPreview(
        RuleGraph graph,
        IReadOnlyCollection<NodeCatalogEntry> catalogEntries,
        string previousPreview,
        bool trackVisualDiff)
    {
        var nextPreview = "";
        if (TryGetExportableRule(graph, out var rule))
        {
            try
            {
                nextPreview = exporter.ExportRuleToLuau(rule, graph, catalogEntries);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Text.Json.JsonException)
            {
                nextPreview = $"{LuauCommentTags.VsrComment("Script Code Export Preview failed.")}\n{LuauCommentTags.VsrComment(ex.Message)}";
            }
        }

        var diff = LuauPreviewDiffService.Compare(previousPreview, nextPreview);
        if (trackVisualDiff && diff.HasPreviousPreview && diff.Changed)
        {
            return new GraphPreviewRefreshResult(
                nextPreview,
                diff,
                diff.HighlightedLineNumbers,
                diff.FirstChangedLine,
                IncrementFocusRequest: true);
        }

        return new GraphPreviewRefreshResult(
            nextPreview,
            diff,
            trackVisualDiff ? [] : null,
            FocusLineNumber: 0,
            IncrementFocusRequest: false);
    }

    public static string CombineStatusWithPreviewDiff(string status, LuauPreviewDiffResult diff)
    {
        return !string.IsNullOrWhiteSpace(diff.StatusSuffix)
            ? $"{status} {diff.StatusSuffix}"
            : status;
    }

    private static bool SamePorts(IReadOnlyList<NodePort> left, IReadOnlyList<NodePort> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            var a = left[index];
            var b = right[index];
            if (!string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(a.Label, b.Label, StringComparison.OrdinalIgnoreCase) ||
                a.Direction != b.Direction ||
                a.PortKind != b.PortKind ||
                !string.Equals(a.DataType, b.DataType, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(a.ColorHex, b.ColorHex, StringComparison.OrdinalIgnoreCase) ||
                a.Order != b.Order)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasEndpoint(Rule rule, GraphEndpoint endpoint)
    {
        var node = rule.Nodes.FirstOrDefault(item => string.Equals(item.Id, endpoint.NodeId, StringComparison.OrdinalIgnoreCase));
        return node?.Ports.Any(port => string.Equals(port.Id, endpoint.PortId, StringComparison.OrdinalIgnoreCase)) == true;
    }
}

/// <summary>
/// Complete preview refresh payload copied into ViewModel-bound preview and diff state.
/// </summary>
/// <remarks>
/// The service computes these values together so the text, changed lines, and
/// focus request stay synchronized after graph edits.
/// </remarks>
public sealed record GraphPreviewRefreshResult(
    string PreviewText,
    LuauPreviewDiffResult Diff,
    IReadOnlyList<int>? HighlightedLineNumbers,
    int FocusLineNumber,
    bool IncrementFocusRequest);
