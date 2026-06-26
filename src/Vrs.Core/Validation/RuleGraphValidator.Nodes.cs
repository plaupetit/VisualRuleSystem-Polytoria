using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.Core.Validation;

public sealed partial class RuleGraphValidator
{
    // Node validation compares authored nodes against their catalog definition
    // and delegates value-source details to the binding validator.
    private static void ValidateNode(
        Rule rule,
        RuleNode node,
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        IReadOnlyCollection<SceneObject> sceneObjects,
        ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(node.Id))
        {
            Add(result, ValidationSeverity.Error, node.Label, "Node is missing an id.");
        }

        if (node.Ports.Count == 0)
        {
            Add(result, ValidationSeverity.Warning, node.Label, "Node has no ports.");
        }

        var entry = NodeCatalogService.FindByCatalogId(catalog, node.CatalogId);
        if (entry is null)
        {
            Add(result, ValidationSeverity.Warning, node.Label, $"Node has no loaded catalog entry: {node.CatalogId}");
            return;
        }

        if (!entry.Status.Equals("Ready", StringComparison.OrdinalIgnoreCase))
        {
            Add(result, ValidationSeverity.Warning, node.Label, $"Catalog entry is not marked Ready: {entry.Status}");
        }

        if (node.Kind == NodeKind.Property && !RuleUsesPropertyNode(rule, node.Id))
        {
            Add(result, ValidationSeverity.Warning, node.Label, "This value is not used by any parameter. Choose it from a parameter instead.");
        }

        foreach (var required in entry.Parameters.Where(p => p.Required))
        {
            var authored = node.Parameters.FirstOrDefault(p => string.Equals(p.Key, required.Key, StringComparison.OrdinalIgnoreCase));
            var suppliedByWire = rule.Connections.Any(connection =>
                connection.ConnectionKind != GraphConnectionKind.Flow &&
                string.Equals(connection.To.NodeId, node.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(connection.To.PortId, GraphPortDefaults.ParameterPortId(required.Key), StringComparison.OrdinalIgnoreCase));

            if (!suppliedByWire && (authored is null || string.IsNullOrWhiteSpace(EffectiveParameterValue(authored))))
            {
                Add(result, ValidationSeverity.Error, node.Label, $"Required parameter is empty: {required.Label}");
            }

            if (authored is not null)
            {
                ValidateBinding(node, required, authored, sceneObjects, result);
            }
        }
    }

    private static bool RuleUsesPropertyNode(Rule rule, string nodeId)
    {
        return rule.Connections.Any(connection =>
            connection.ConnectionKind != GraphConnectionKind.Flow &&
            string.Equals(connection.From.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
    }
}
