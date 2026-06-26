using Vrs.Graph.Model;

namespace Vrs.Graph.Modeling;

public sealed partial class RuleGraphEditService
{
    // Node mutations: add, remove, move, duplicate, enable/disable, and clone support.
    public GraphEditResult AddNode(Rule rule, RuleNode node)
    {
        if (string.IsNullOrWhiteSpace(node.Id))
        {
            return GraphEditResult.Fail("Node id is required.");
        }

        if (FindNode(rule, node.Id) is not null)
        {
            return GraphEditResult.Fail($"Node id already exists: {node.Id}");
        }

        if (node.Ports.Count == 0)
        {
            node.Ports = GraphPortDefaults.CreateDefaultPorts(node.Kind);
        }

        rule.Nodes.Add(node);
        return GraphEditResult.Ok($"Added node: {node.Label}");
    }

    public GraphEditResult RemoveNode(Rule rule, string nodeId)
    {
        var node = FindNode(rule, nodeId);
        if (node is null)
        {
            return GraphEditResult.Fail($"Node does not exist: {nodeId}");
        }

        rule.Nodes.Remove(node);
        DisconnectNode(rule, nodeId);
        foreach (var fragment in rule.Fragments)
        {
            fragment.NodeIds.RemoveAll(id => string.Equals(id, nodeId, StringComparison.OrdinalIgnoreCase));
            fragment.ConnectionIds.RemoveAll(connectionId => !rule.Connections.Any(connection => string.Equals(connection.Id, connectionId, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var group in rule.NodeGroups)
        {
            group.MemberNodeIds.RemoveAll(id => string.Equals(id, nodeId, StringComparison.OrdinalIgnoreCase));
        }

        rule.Fragments.RemoveAll(fragment => fragment.NodeIds.Count == 0);
        AutoFitAllGroups(rule);
        return GraphEditResult.Ok($"Removed node: {node.Label}");
    }

    public GraphEditResult MoveNode(Rule rule, string nodeId, float graphX, float graphY)
    {
        var node = FindNode(rule, nodeId);
        if (node is null)
        {
            return GraphEditResult.Fail($"Node does not exist: {nodeId}");
        }

        node.GraphX = graphX;
        node.GraphY = graphY;
        node.GraphPositionSet = true;
        ReassignNodeToContainingGroup(rule, node);
        AutoFitAllGroups(rule);

        return GraphEditResult.Ok($"Moved node: {node.Label}");
    }
    public GraphEditResult DuplicateNode(Rule rule, string nodeId, float offsetX = 48.0F, float offsetY = 48.0F)
    {
        var node = FindNode(rule, nodeId);
        if (node is null)
        {
            return GraphEditResult.Fail($"Node does not exist: {nodeId}");
        }

        var clone = CloneNode(node);
        clone.Id = CreateDuplicateNodeId(rule, node.Id);
        clone.Label = $"{node.Label} Copy";
        clone.GraphX = node.GraphX + offsetX;
        clone.GraphY = node.GraphY + offsetY;
        clone.GraphPositionSet = true;
        rule.Nodes.Add(clone);
        return GraphEditResult.Ok($"Duplicated node: {node.Label}");
    }

    public GraphEditResult ToggleNodeEnabled(Rule rule, string nodeId)
    {
        var node = FindNode(rule, nodeId);
        if (node is null)
        {
            return GraphEditResult.Fail($"Node does not exist: {nodeId}");
        }

        node.Enabled = !node.Enabled;
        return GraphEditResult.Ok(node.Enabled ? $"Enabled node: {node.Label}" : $"Disabled node: {node.Label}");
    }
    private static string CreateDuplicateNodeId(Rule rule, string baseId)
    {
        var safe = SanitizeId(baseId);
        for (var index = 1; index < 10_000; index++)
        {
            var candidate = $"{safe}_Copy{index}";
            if (rule.Nodes.All(node => !string.Equals(node.Id, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }

        return $"{safe}_Copy_{Guid.NewGuid():N}";
    }
    private static RuleNode CloneNode(RuleNode node)
    {
        return new RuleNode
        {
            Kind = node.Kind,
            Type = node.Type,
            Label = node.Label,
            Value = node.Value,
            Description = node.Description,
            Comment = node.Comment,
            UserComment = node.UserComment,
            Enabled = node.Enabled,
            DebugEnabled = node.DebugEnabled,
            Breakpoint = node.Breakpoint,
            DetailsOpen = node.DetailsOpen,
            FallbackMode = node.FallbackMode,
            FallbackNote = node.FallbackNote,
            FragmentId = node.FragmentId,
            Collapsed = node.Collapsed,
            ExposeAdvancedPorts = node.ExposeAdvancedPorts,
            Ports = GraphPortDefaults.ClonePorts(node.Ports),
            Parameters = node.Parameters
                .Select(parameter => new RuleParameter
                {
                    Key = parameter.Key,
                    Value = parameter.Value,
                    ValueSource = parameter.ValueSource,
                    CustomValue = parameter.CustomValue,
                    SourceCatalogId = parameter.SourceCatalogId
                })
                .ToList(),
            CompositeMode = node.CompositeMode,
            CatalogId = node.CatalogId
        };
    }
}
