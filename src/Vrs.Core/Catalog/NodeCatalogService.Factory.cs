using Vrs.Graph.Model;

namespace Vrs.Core.Catalog;

public sealed partial class NodeCatalogService
{
    // Node creation maps catalog authoring metadata into the container-neutral
    // graph model. UI state and deployment concerns must not leak into this path.
    public static RuleNode CreateNode(NodeCatalogEntry entry, float graphX = 0, float graphY = 0, string? stableId = null)
    {
        var suffix = stableId ?? ShortId(entry.IdBase);
        var node = new RuleNode
        {
            Kind = entry.Kind,
            Id = suffix,
            Type = entry.Type,
            Label = entry.Label,
            Value = entry.Value,
            Description = entry.Description,
            Comment = entry.Comment,
            CatalogId = entry.IdBase,
            FallbackMode = string.IsNullOrWhiteSpace(entry.FallbackMode) ? "Log And Skip" : entry.FallbackMode,
            GraphX = graphX,
            GraphY = graphY,
            GraphPositionSet = true
        };

        node.Ports = CreatePorts(entry);

        foreach (var parameter in entry.Parameters)
        {
            node.Parameters.Add(CreateRuleParameter(parameter));
        }

        return node;
    }

    public static bool BackfillMissingParameters(RuleNode node, NodeCatalogEntry entry)
    {
        var changed = false;
        foreach (var parameter in entry.Parameters)
        {
            if (node.Parameters.Any(authored => string.Equals(authored.Key, parameter.Key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // Catalog manifests can gain new required fields after a graph was
            // saved. Append defaults only; never overwrite authored values.
            node.Parameters.Add(CreateRuleParameter(parameter));
            changed = true;
        }

        return changed;
    }

    private static RuleParameter CreateRuleParameter(NodeCatalogParameterDefinition parameter)
    {
        var sourceKind = DefaultSourceKind(parameter);
        var value = string.IsNullOrWhiteSpace(parameter.Default) ? parameter.Fallback : parameter.Default;
        if (sourceKind == GraphValueSourceKind.TriggeringPlayer && string.IsNullOrWhiteSpace(value))
        {
            value = "Triggering Player";
        }

        return new RuleParameter
        {
            Key = parameter.Key,
            Value = value,
            ValueSource = string.IsNullOrWhiteSpace(parameter.ValueSource)
                ? "String / Manual Text Input"
                : parameter.ValueSource,
            Binding = new GraphValueBinding
            {
                SourceKind = sourceKind,
                DataType = NormalizeDataType(parameter.Type),
                ConstantValue = sourceKind == GraphValueSourceKind.Constant ? value : "",
                DisplayText = value
            }
        };
    }
}
