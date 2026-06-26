using Vrs.Core.Authoring;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using Vrs.Graph.Theming;

namespace Vrs.App.Services;

/// <summary>
/// Builds container-neutral inspector presentation data from the current graph
/// selection. ViewModels keep ownership of bindable state and notifications.
/// </summary>
public sealed class SelectionInspectorService
{
    public SelectedNodeInspectorPresentation BuildNodePresentation(
        RuleNode? node,
        IEnumerable<NodeCatalogEntry> catalogEntries)
    {
        if (node is null)
        {
            return new SelectedNodeInspectorPresentation(
                KindText: "",
                TypeText: "",
                BlockTitle: "",
                HumanVerb: "Node",
                BlockBadge: "NODE",
                AccentHex: "#7c8794",
                FillHex: "#20252c",
                BlockSubtitle: "",
                ConfiguredSummary: "");
        }

        var style = GraphTheme.Default.StyleFor(node.Kind);
        var entry = NodeCatalogService.FindByCatalogId(catalogEntries, node.CatalogId);
        return new SelectedNodeInspectorPresentation(
            KindText: node.Kind.ToString(),
            TypeText: node.Type,
            BlockTitle: NodeReadableSummaryService.BuildNodeDisplayName(node),
            HumanVerb: HumanVerb(node.Kind),
            BlockBadge: BlockBadge(node.Kind),
            AccentHex: style.AccentHex,
            FillHex: style.FillHex,
            BlockSubtitle: $"{node.Kind} / {node.Type}",
            ConfiguredSummary: NodeReadableSummaryService.BuildNodeSummary(node, entry));
    }

    public GraphFragment? FindSelectedFragment(Rule rule, string selectedFragmentId)
    {
        return rule.Fragments.FirstOrDefault(fragment =>
            string.Equals(fragment.Id, selectedFragmentId, StringComparison.OrdinalIgnoreCase));
    }

    public InspectorSummary BuildInspectorSummary(
        Rule rule,
        RuleNode? selectedNode,
        string selectedGroupId,
        string selectedWireRerouteId,
        string selectedFragmentId,
        int selectedConnectionIndex)
    {
        if (selectedNode is not null)
        {
            return new InspectorSummary(NodeReadableSummaryService.BuildNodeDisplayName(selectedNode), selectedNode.Description, selectedNode.UserComment);
        }

        var selectedGroup = rule.NodeGroups.FirstOrDefault(group =>
            string.Equals(group.Id, selectedGroupId, StringComparison.OrdinalIgnoreCase));
        if (selectedGroup is not null)
        {
            return new InspectorSummary(
                selectedGroup.Name,
                $"Visual group with {selectedGroup.MemberNodeIds.Count} node(s).",
                selectedGroup.Comment);
        }

        var selectedReroute = rule.WireReroutes.FirstOrDefault(reroute =>
            string.Equals(reroute.Id, selectedWireRerouteId, StringComparison.OrdinalIgnoreCase));
        if (selectedReroute is not null)
        {
            var connection = rule.Connections.FirstOrDefault(item => item.RerouteIds.Contains(selectedReroute.Id, StringComparer.OrdinalIgnoreCase));
            return new InspectorSummary(
                "Wire Reroute",
                connection is null ? "Visual wire anchor not attached to a wire." : "Visual wire anchor for shaping a selected cable.",
                $"Input {selectedReroute.InputDirection}, output {selectedReroute.OutputDirection}.");
        }

        var selectedFragment = FindSelectedFragment(rule, selectedFragmentId);
        if (selectedFragment is not null)
        {
            return new InspectorSummary(
                selectedFragment.Name,
                $"{selectedFragment.Kind} fragment with {selectedFragment.NodeIds.Count} node(s).",
                selectedFragment.Comment);
        }

        if (selectedConnectionIndex >= 0 && selectedConnectionIndex < rule.Connections.Count)
        {
            var connection = rule.Connections[selectedConnectionIndex];
            return new InspectorSummary(
                "Selected Wire",
                $"{connection.From.NodeId}.{connection.From.PortId} -> {connection.To.NodeId}.{connection.To.PortId}",
                "Delete Selection removes this wire.");
        }

        return new InspectorSummary(
            "No node selected",
            "Select a node to edit parameters, or select a wire to delete it.",
            "");
    }

    public IReadOnlyList<StateRuleRowData> BuildStateRuleRows(Rule rule)
    {
        var rows = new List<StateRuleRowData>();
        var nodesById = rule.Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        IEnumerable<GraphFragment> fragments = rule.Fragments.Count == 0
            ? [CreateSyntheticRuleFragment(rule)]
            : rule.Fragments;

        foreach (var fragment in fragments.OrderBy(fragment => fragment.Kind).ThenBy(fragment => fragment.GraphX).ThenBy(fragment => fragment.Name, StringComparer.OrdinalIgnoreCase))
        {
            var fragmentNodes = fragment.NodeIds.Count == 0 && fragment.Id == "RULE_All"
                ? rule.Nodes
                : fragment.NodeIds.Where(nodesById.ContainsKey).Select(id => nodesById[id]).ToList();

            rows.Add(new StateRuleRowData(
                Id: fragment.Id,
                Name: string.IsNullOrWhiteSpace(fragment.Name) ? fragment.Kind.ToString() : fragment.Name,
                Kind: fragment.Kind,
                TriggerSummary: GrammarSummary("On", fragmentNodes, NodeKind.Trigger),
                ConditionSummary: GrammarSummary("Is", fragmentNodes, NodeKind.Condition),
                ActionSummary: GrammarSummary("Do", fragmentNodes, NodeKind.Action),
                Comment: fragment.Comment,
                Collapsed: fragment.Collapsed));
        }

        return rows;
    }

    private static GraphFragment CreateSyntheticRuleFragment(Rule rule)
    {
        return new GraphFragment
        {
            Id = "RULE_All",
            Name = string.IsNullOrWhiteSpace(rule.Name) ? "Rule" : rule.Name,
            Kind = GraphFragmentKind.Rule,
            NodeIds = rule.Nodes.Select(node => node.Id).ToList(),
            ConnectionIds = rule.Connections.Select(connection => connection.Id).ToList(),
            Collapsed = false,
            Comment = rule.Comment
        };
    }

    private static string GrammarSummary(string prefix, IEnumerable<RuleNode> nodes, NodeKind kind)
    {
        var names = nodes
            .Where(node => node.Kind == kind)
            .Select(node => string.IsNullOrWhiteSpace(node.Label) ? node.Type : node.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Take(3)
            .ToList();

        return names.Count == 0
            ? $"{prefix}: none"
            : $"{prefix}: {string.Join(", ", names)}";
    }

    private static string HumanVerb(NodeKind kind)
    {
        return kind switch
        {
            NodeKind.Trigger => "On",
            NodeKind.Condition => "Is",
            NodeKind.Action => "Do",
            NodeKind.Property => "Property",
            NodeKind.Reference => "Reference",
            _ => "Node"
        };
    }

    private static string BlockBadge(NodeKind kind)
    {
        return kind switch
        {
            NodeKind.Trigger => "TRIGGER",
            NodeKind.Condition => "CONDITION",
            NodeKind.Action => "ACTION",
            NodeKind.Property => "PROPERTY",
            NodeKind.Reference => "REFERENCE",
            _ => "NODE"
        };
    }
}

/// <summary>
/// UI-ready text and colors for the selected node inspector header.
/// </summary>
/// <remarks>
/// Keeping this projection outside the ViewModel makes node-kind presentation a
/// service concern while the ViewModel only exposes bindable properties.
/// </remarks>
public sealed record SelectedNodeInspectorPresentation(
    string KindText,
    string TypeText,
    string BlockTitle,
    string HumanVerb,
    string BlockBadge,
    string AccentHex,
    string FillHex,
    string BlockSubtitle,
    string ConfiguredSummary);

/// <summary>
/// Compact inspector fallback text used when no detailed node presentation is active.
/// </summary>
public sealed record InspectorSummary(string Title, string Description, string Detail);

/// <summary>
/// UI-ready row data for state/rule fragment summaries shown outside the canvas.
/// </summary>
public sealed record StateRuleRowData(
    string Id,
    string Name,
    GraphFragmentKind Kind,
    string TriggerSummary,
    string ConditionSummary,
    string ActionSummary,
    string Comment,
    bool Collapsed);
