using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.Core.Validation;

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

public sealed class ValidationMessage
{
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Info;
    public string Scope { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class ValidationResult
{
    public List<ValidationMessage> Messages { get; set; } = [];
    public int WarningCount => Messages.Count(m => m.Severity == ValidationSeverity.Warning);
    public int ErrorCount => Messages.Count(m => m.Severity == ValidationSeverity.Error);
}

/// <summary>
/// Validates graph authoring mistakes before export or bridge deployment.
/// </summary>
public sealed partial class RuleGraphValidator
{
    private readonly RuleGraphEditService editService = new();

    public ValidationResult Validate(RuleGraph graph, IEnumerable<NodeCatalogEntry>? catalogEntries = null)
    {
        var result = new ValidationResult();
        var catalog = catalogEntries?.ToList() ?? [];

        if (graph.Version != 3)
        {
            Add(result, ValidationSeverity.Error, graph.Name, $"Unsupported graph version: {graph.Version}. Expected v3.");
            return result;
        }

        if (graph.Rules.Count == 0)
        {
            Add(result, ValidationSeverity.Error, graph.Name, "Graph has no rules.");
            return result;
        }

        foreach (var rule in graph.Rules)
        {
            ValidateRule(rule, catalog, graph.SceneObjects, result);
        }

        if (result.Messages.Count == 0)
        {
            Add(result, ValidationSeverity.Info, graph.Name, "Graph is ready to export.");
        }

        return result;
    }

    private void ValidateRule(
        Rule rule,
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        IReadOnlyCollection<SceneObject> sceneObjects,
        ValidationResult result)
    {
        var nodes = rule.Nodes.ToList();

        if (!nodes.Any(node => node.Kind == NodeKind.Trigger && node.Enabled))
        {
            Add(result, ValidationSeverity.Error, rule.Name, "Rule has no enabled Trigger node.");
        }

        if (!nodes.Any(node => node.Kind == NodeKind.Action))
        {
            Add(result, ValidationSeverity.Warning, rule.Name, "Rule has no Action nodes yet.");
        }

        var duplicateIds = nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.Id))
            .GroupBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);

        foreach (var duplicate in duplicateIds)
        {
            Add(result, ValidationSeverity.Error, rule.Name, $"Duplicate node id: {duplicate.Key}");
        }

        var ids = nodes.Select(n => n.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ValidateConnections(rule, ids, result);
        ValidateTriggerFlows(rule, nodes, result);
        ValidateWireReroutes(rule, result);
        ValidateFragments(rule, ids, result);
        ValidateGroups(rule, ids, result);

        foreach (var node in nodes)
        {
            ValidateNode(rule, node, catalog, sceneObjects, result);
        }
    }

    private static void ValidateTriggerFlows(
        Rule rule,
        IReadOnlyCollection<RuleNode> nodes,
        ValidationResult result)
    {
        foreach (var trigger in nodes.Where(node => node.Enabled && node.Kind == NodeKind.Trigger))
        {
            var hasOutgoingFlow = rule.Connections.Any(connection =>
                connection.ConnectionKind == GraphConnectionKind.Flow &&
                connection.From.NodeId.Equals(trigger.Id, StringComparison.OrdinalIgnoreCase));
            if (!hasOutgoingFlow)
            {
                Add(result, ValidationSeverity.Warning, trigger.Label, "Enabled trigger has no connected flow.");
            }
        }
    }
}
