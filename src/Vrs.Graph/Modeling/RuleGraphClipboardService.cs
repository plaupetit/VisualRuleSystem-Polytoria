using System.Text.Json;
using Vrs.Graph.Model;

namespace Vrs.Graph.Modeling;

/// <summary>
/// Internal graph clipboard for VRS-only copy/paste operations. It stores a
/// detached payload instead of touching the OS clipboard so graph IDs and
/// visual metadata can be remapped safely on paste.
/// </summary>
public sealed class RuleGraphClipboardService
{
    private static readonly JsonSerializerOptions CloneJsonOptions = new(JsonSerializerDefaults.General);
    private const float RepeatedPasteOffset = 32.0F;

    private GraphClipboardPayload? payload;
    private int pasteCount;

    public bool HasClipboard => payload is not null && payload.HasContent;

    public GraphClipboardCopyResult Copy(Rule rule, GraphClipboardSelection selection)
    {
        var selectedNodeIds = selection.NodeIds
            .Where(id => rule.Nodes.Any(node => SameId(node.Id, id)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedRerouteIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selectedGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(selection.GroupId))
        {
            var group = rule.NodeGroups.FirstOrDefault(item => SameId(item.Id, selection.GroupId));
            if (group is not null)
            {
                selectedGroupIds.Add(group.Id);
                selectedNodeIds.UnionWith(group.MemberNodeIds);
                selectedRerouteIds.UnionWith(group.MemberRerouteIds);
            }
        }

        if (!string.IsNullOrWhiteSpace(selection.FragmentId))
        {
            var fragment = rule.Fragments.FirstOrDefault(item => SameId(item.Id, selection.FragmentId));
            if (fragment is not null)
            {
                selectedNodeIds.UnionWith(fragment.NodeIds);
            }
        }

        if (selection.ConnectionIndex >= 0 && selection.ConnectionIndex < rule.Connections.Count)
        {
            var connection = rule.Connections[selection.ConnectionIndex];
            selectedNodeIds.Add(connection.From.NodeId);
            selectedNodeIds.Add(connection.To.NodeId);
        }

        var copiedConnections = rule.Connections
            .Where(connection => selectedNodeIds.Contains(connection.From.NodeId) && selectedNodeIds.Contains(connection.To.NodeId))
            .ToList();
        foreach (var connection in copiedConnections)
        {
            selectedRerouteIds.UnionWith(connection.RerouteIds);
        }

        var copiedGroups = rule.NodeGroups
            .Where(group => selectedGroupIds.Contains(group.Id) || IsGroupFullyContained(group, selectedNodeIds, selectedRerouteIds))
            .ToList();

        if (selectedNodeIds.Count == 0 && copiedGroups.Count == 0)
        {
            payload = null;
            pasteCount = 0;
            return GraphClipboardCopyResult.Fail("Select nodes or a group to copy.");
        }

        var copiedNodes = rule.Nodes
            .Where(node => selectedNodeIds.Contains(node.Id))
            .Select(DeepClone)
            .ToList();
        var copiedReroutes = rule.WireReroutes
            .Where(reroute => selectedRerouteIds.Contains(reroute.Id))
            .Select(DeepClone)
            .ToList();

        payload = new GraphClipboardPayload(
            copiedNodes,
            copiedConnections.Select(DeepClone).ToList(),
            copiedReroutes,
            copiedGroups.Select(DeepClone).ToList());
        pasteCount = 0;

        return GraphClipboardCopyResult.Ok(
            $"Copied {payload.NodeCount} node(s), {payload.ConnectionCount} connection(s), {payload.GroupCount} group(s).",
            payload.NodeCount,
            payload.GroupCount);
    }

    public GraphClipboardPasteResult Paste(Rule rule, float graphX, float graphY)
    {
        if (payload is null || !payload.HasContent)
        {
            return GraphClipboardPasteResult.Fail("Clipboard is empty.");
        }

        var origin = payload.FindOrigin();
        var offset = pasteCount * RepeatedPasteOffset;
        pasteCount++;

        var nodeIdMap = CreateIdMap(payload.Nodes.Select(node => node.Id), ExistingNodeIds(rule), "Node");
        var rerouteIdMap = CreateIdMap(payload.Reroutes.Select(reroute => reroute.Id), ExistingRerouteIds(rule), "Reroute");
        var groupIdMap = CreateIdMap(payload.Groups.Select(group => group.Id), ExistingGroupIds(rule), "Group");
        var connectionIdMap = CreateIdMap(payload.Connections.Select(connection => connection.Id), ExistingConnectionIds(rule), "Connection");
        var pastedNodeIds = new List<string>();
        var pastedGroupIds = new List<string>();

        foreach (var sourceNode in payload.Nodes)
        {
            var node = DeepClone(sourceNode);
            var oldId = node.Id;
            node.Id = nodeIdMap[oldId];
            node.FragmentId = "";
            node.GraphX = graphX + (node.GraphX - origin.X) + offset;
            node.GraphY = graphY + (node.GraphY - origin.Y) + offset;
            node.GraphPositionSet = true;
            RemapNodeReferences(node, nodeIdMap);
            rule.Nodes.Add(node);
            pastedNodeIds.Add(node.Id);
        }

        foreach (var sourceReroute in payload.Reroutes)
        {
            var reroute = DeepClone(sourceReroute);
            reroute.Id = rerouteIdMap[reroute.Id];
            reroute.GraphX = graphX + (reroute.GraphX - origin.X) + offset;
            reroute.GraphY = graphY + (reroute.GraphY - origin.Y) + offset;
            rule.WireReroutes.Add(reroute);
        }

        foreach (var sourceConnection in payload.Connections)
        {
            var connection = DeepClone(sourceConnection);
            connection.Id = connectionIdMap[connection.Id];
            connection.From.NodeId = nodeIdMap[connection.From.NodeId];
            connection.To.NodeId = nodeIdMap[connection.To.NodeId];
            connection.RerouteIds = connection.RerouteIds
                .Where(rerouteIdMap.ContainsKey)
                .Select(rerouteId => rerouteIdMap[rerouteId])
                .ToList();
            rule.Connections.Add(connection);
        }

        foreach (var sourceGroup in payload.Groups)
        {
            var group = DeepClone(sourceGroup);
            group.Id = groupIdMap[group.Id];
            group.ParentGroupId = groupIdMap.TryGetValue(group.ParentGroupId, out var parentId) ? parentId : "";
            group.MemberNodeIds = group.MemberNodeIds
                .Where(nodeIdMap.ContainsKey)
                .Select(nodeId => nodeIdMap[nodeId])
                .ToList();
            group.MemberRerouteIds = group.MemberRerouteIds
                .Where(rerouteIdMap.ContainsKey)
                .Select(rerouteId => rerouteIdMap[rerouteId])
                .ToList();
            group.GraphX = graphX + (group.GraphX - origin.X) + offset;
            group.GraphY = graphY + (group.GraphY - origin.Y) + offset;
            rule.NodeGroups.Add(group);
            pastedGroupIds.Add(group.Id);
        }

        return GraphClipboardPasteResult.Ok(
            $"Pasted {pastedNodeIds.Count} node(s), {payload.ConnectionCount} connection(s), {pastedGroupIds.Count} group(s).",
            pastedNodeIds,
            pastedGroupIds);
    }

    private static bool IsGroupFullyContained(RuleNodeGroup group, IReadOnlySet<string> nodeIds, IReadOnlySet<string> rerouteIds)
    {
        var hasMembers = group.MemberNodeIds.Count > 0 || group.MemberRerouteIds.Count > 0;
        return hasMembers &&
            group.MemberNodeIds.All(nodeIds.Contains) &&
            group.MemberRerouteIds.All(rerouteIds.Contains);
    }

    private static Dictionary<string, string> CreateIdMap(IEnumerable<string> sourceIds, IEnumerable<string> existingIds, string fallbackPrefix)
    {
        var used = existingIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceId in sourceIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var safe = SanitizeId(sourceId, fallbackPrefix);
            for (var index = 1; index < 10_000; index++)
            {
                var candidate = $"{safe}_Paste{index}";
                if (used.Add(candidate))
                {
                    map[sourceId] = candidate;
                    break;
                }
            }

            if (!map.ContainsKey(sourceId))
            {
                var fallback = $"{safe}_Paste_{Guid.NewGuid():N}";
                used.Add(fallback);
                map[sourceId] = fallback;
            }
        }

        return map;
    }

    private static void RemapNodeReferences(RuleNode node, IReadOnlyDictionary<string, string> nodeIdMap)
    {
        foreach (var parameter in node.Parameters)
        {
            RemapBinding(parameter.Binding, nodeIdMap);
        }

        foreach (var child in node.ChildNodes)
        {
            if (nodeIdMap.TryGetValue(child.Id, out var childId))
            {
                child.Id = childId;
            }

            RemapNodeReferences(child, nodeIdMap);
        }
    }

    private static void RemapBinding(GraphValueBinding binding, IReadOnlyDictionary<string, string> nodeIdMap)
    {
        if (!string.IsNullOrWhiteSpace(binding.SourceNodeId))
        {
            if (nodeIdMap.TryGetValue(binding.SourceNodeId, out var remappedNodeId))
            {
                binding.SourceNodeId = remappedNodeId;
            }
            else if (binding.SourceKind == GraphValueSourceKind.ConnectedPort)
            {
                // External value wires are intentionally not copied; clear the
                // hidden endpoint so the pasted node does not depend on the
                // original selection's outside graph.
                binding.SourceNodeId = "";
                binding.SourcePortId = "";
            }
        }

        foreach (var catalogParameter in binding.CatalogParameters)
        {
            RemapBinding(catalogParameter.Binding, nodeIdMap);
        }
    }

    private static GraphClipboardPoint FindItemOrigin(
        IEnumerable<RuleNode> nodes,
        IEnumerable<RuleWireReroute> reroutes,
        IEnumerable<RuleNodeGroup> groups)
    {
        var points = nodes.Select(node => new GraphClipboardPoint(node.GraphX, node.GraphY))
            .Concat(reroutes.Select(reroute => new GraphClipboardPoint(reroute.GraphX, reroute.GraphY)))
            .Concat(groups.Select(group => new GraphClipboardPoint(group.GraphX, group.GraphY)))
            .ToList();

        return points.Count == 0
            ? new GraphClipboardPoint(0, 0)
            : new GraphClipboardPoint(points.Min(point => point.X), points.Min(point => point.Y));
    }

    private static T DeepClone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, CloneJsonOptions);
        return JsonSerializer.Deserialize<T>(json, CloneJsonOptions)
            ?? throw new InvalidOperationException($"Unable to clone {typeof(T).Name}.");
    }

    private static IEnumerable<string> ExistingNodeIds(Rule rule) => rule.Nodes.Select(node => node.Id);

    private static IEnumerable<string> ExistingRerouteIds(Rule rule) => rule.WireReroutes.Select(reroute => reroute.Id);

    private static IEnumerable<string> ExistingGroupIds(Rule rule) => rule.NodeGroups.Select(group => group.Id);

    private static IEnumerable<string> ExistingConnectionIds(Rule rule) => rule.Connections.Select(connection => connection.Id);

    private static bool SameId(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string SanitizeId(string value, string fallbackPrefix)
    {
        var safe = new string((value ?? "").Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray()).Trim('_');
        return string.IsNullOrWhiteSpace(safe) ? fallbackPrefix : safe;
    }

    private sealed record GraphClipboardPayload(
        List<RuleNode> Nodes,
        List<GraphConnection> Connections,
        List<RuleWireReroute> Reroutes,
        List<RuleNodeGroup> Groups)
    {
        public bool HasContent => Nodes.Count > 0 || Groups.Count > 0;

        public int NodeCount => Nodes.Count;

        public int ConnectionCount => Connections.Count;

        public int GroupCount => Groups.Count;

        public GraphClipboardPoint FindOrigin() => FindItemOrigin(Nodes, Reroutes, Groups);
    }
}

public sealed record GraphClipboardSelection(
    IReadOnlyCollection<string> NodeIds,
    string GroupId = "",
    string FragmentId = "",
    int ConnectionIndex = -1);

public sealed record GraphClipboardCopyResult(bool Success, string Message, int NodeCount, int GroupCount)
{
    public static GraphClipboardCopyResult Ok(string message, int nodeCount, int groupCount) => new(true, message, nodeCount, groupCount);

    public static GraphClipboardCopyResult Fail(string message) => new(false, message, 0, 0);
}

public sealed record GraphClipboardPasteResult(
    bool Success,
    bool Changed,
    string Message,
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<string> GroupIds)
{
    public static GraphClipboardPasteResult Ok(string message, IReadOnlyList<string> nodeIds, IReadOnlyList<string> groupIds) =>
        new(true, true, message, nodeIds, groupIds);

    public static GraphClipboardPasteResult Fail(string message) => new(false, false, message, [], []);
}

internal readonly record struct GraphClipboardPoint(float X, float Y);
