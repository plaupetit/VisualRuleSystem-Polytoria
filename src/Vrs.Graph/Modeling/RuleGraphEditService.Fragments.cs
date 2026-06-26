using Vrs.Graph.Model;

namespace Vrs.Graph.Modeling;

public sealed partial class RuleGraphEditService
{
    // Fragment mutations: create from selection, add empty fragments, collapse/expand, and remove.
    public GraphEditResult CreateFragmentFromSelection(
        Rule rule,
        IEnumerable<string> nodeIds,
        GraphFragmentKind kind = GraphFragmentKind.Rule,
        string? name = null)
    {
        var selectedIds = nodeIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (selectedIds.Count == 0)
        {
            return GraphEditResult.Fail("Select at least one node before creating a fragment.");
        }

        var selectedNodes = selectedIds
            .Select(id => FindNode(rule, id))
            .ToList();
        if (selectedNodes.Any(node => node is null))
        {
            return GraphEditResult.Fail("Fragment selection contains a missing node.");
        }

        var nodes = selectedNodes.OfType<RuleNode>().ToList();
        var fragmentId = CreateFragmentId(rule, kind);
        var selectedIdSet = selectedIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var internalConnections = rule.Connections
            .Where(connection =>
                selectedIdSet.Contains(connection.From.NodeId) &&
                selectedIdSet.Contains(connection.To.NodeId))
            .Select(connection => connection.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var fragment = new GraphFragment
        {
            Id = fragmentId,
            Name = string.IsNullOrWhiteSpace(name) ? DefaultFragmentName(kind, rule.Fragments.Count + 1) : name!,
            Kind = kind,
            NodeIds = selectedIds,
            ConnectionIds = internalConnections,
            Collapsed = true,
            GraphX = nodes.Min(node => node.GraphX),
            GraphY = nodes.Min(node => node.GraphY),
            Comment = "Created from selected graph nodes."
        };

        rule.Fragments.Add(fragment);
        foreach (var node in nodes)
        {
            node.FragmentId = fragment.Id;
        }

        return GraphEditResult.Ok($"Created {kind} fragment: {fragment.Name}");
    }

    public GraphEditResult AddFragment(
        Rule rule,
        GraphFragmentKind kind,
        string? name,
        float graphX,
        float graphY,
        bool collapsed = true)
    {
        var fragment = new GraphFragment
        {
            Id = CreateFragmentId(rule, kind),
            Name = string.IsNullOrWhiteSpace(name) ? DefaultFragmentName(kind, rule.Fragments.Count + 1) : name!,
            Kind = kind,
            Collapsed = collapsed,
            GraphX = graphX,
            GraphY = graphY,
            Comment = "Empty authoring fragment. Add or assign nodes before export."
        };

        rule.Fragments.Add(fragment);
        return GraphEditResult.Ok($"Added {kind} fragment: {fragment.Name}");
    }

    public GraphEditResult SetFragmentCollapsed(Rule rule, string fragmentId, bool collapsed)
    {
        var fragment = FindFragment(rule, fragmentId);
        if (fragment is null)
        {
            return GraphEditResult.Fail($"Fragment does not exist: {fragmentId}");
        }

        fragment.Collapsed = collapsed;
        foreach (var node in rule.Nodes.Where(node => fragment.NodeIds.Contains(node.Id, StringComparer.OrdinalIgnoreCase)))
        {
            node.Collapsed = collapsed;
        }

        return GraphEditResult.Ok(collapsed ? $"Collapsed fragment: {fragment.Name}" : $"Expanded fragment: {fragment.Name}");
    }

    public GraphEditResult RemoveFragment(Rule rule, string fragmentId)
    {
        var fragment = FindFragment(rule, fragmentId);
        if (fragment is null)
        {
            return GraphEditResult.Fail($"Fragment does not exist: {fragmentId}");
        }

        rule.Fragments.Remove(fragment);
        foreach (var node in rule.Nodes.Where(node => string.Equals(node.FragmentId, fragmentId, StringComparison.OrdinalIgnoreCase)))
        {
            node.FragmentId = "";
            node.Collapsed = false;
        }

        return GraphEditResult.Ok($"Removed fragment: {fragment.Name}");
    }
    private static string CreateFragmentId(Rule rule, GraphFragmentKind kind)
    {
        var prefix = $"FRAG_{kind}";
        for (var index = 1; index < 10_000; index++)
        {
            var candidate = $"{prefix}_{index}";
            if (rule.Fragments.All(fragment => !string.Equals(fragment.Id, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }

        return $"{prefix}_{Guid.NewGuid():N}";
    }

    private static string DefaultFragmentName(GraphFragmentKind kind, int index)
    {
        return $"{kind} {index}";
    }
}
