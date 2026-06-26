using Vrs.Graph.Model;

namespace Vrs.Graph.Modeling;

/// <summary>
/// Applies validated authoring mutations to a single rule graph without depending on UI, Creator, bridge, or export code.
/// </summary>
public sealed partial class RuleGraphEditService
{
    // Lookup helpers are kept with the public facade because validation, editing, and UI hosts all depend on them.
    public IReadOnlyList<RuleNode> RuntimeNodes(Rule rule)
    {
        return rule.Nodes;
    }

    public RuleNode? FindNode(Rule rule, string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        return rule.Nodes.FirstOrDefault(node => string.Equals(node.Id, nodeId, StringComparison.OrdinalIgnoreCase));
    }

    public NodePort? FindPort(Rule rule, string nodeId, string portId)
    {
        return FindNode(rule, nodeId)?.Ports.FirstOrDefault(port => string.Equals(port.Id, portId, StringComparison.OrdinalIgnoreCase));
    }

    public GraphFragment? FindFragment(Rule rule, string fragmentId)
    {
        if (string.IsNullOrWhiteSpace(fragmentId))
        {
            return null;
        }

        return rule.Fragments.FirstOrDefault(fragment => string.Equals(fragment.Id, fragmentId, StringComparison.OrdinalIgnoreCase));
    }
}
