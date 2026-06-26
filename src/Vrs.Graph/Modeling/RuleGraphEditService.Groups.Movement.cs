using Vrs.Graph.Model;

namespace Vrs.Graph.Modeling;

/// <summary>
/// Final node positions saved with a visual group move.
/// </summary>
/// <remarks>
/// Canvas drags update nodes optimistically during pointer movement; this
/// payload lets the host persist the same final positions through the edit
/// boundary instead of letting the control own durable graph state.
/// </remarks>
public sealed record GraphNodeMove(string NodeId, float GraphX, float GraphY);

/// <summary>
/// Final group position saved with a visual group move.
/// </summary>
public sealed record GraphGroupMove(string GroupId, float GraphX, float GraphY);

public sealed partial class RuleGraphEditService
{
    public GraphEditResult MoveGroup(
        Rule rule,
        string groupId,
        float graphX,
        float graphY,
        IReadOnlyCollection<GraphNodeMove>? nodeMoves = null,
        IReadOnlyCollection<GraphWireRerouteMove>? rerouteMoves = null)
    {
        var group = FindGroup(rule, groupId);
        if (group is null)
        {
            return GraphEditResult.Fail($"Group does not exist: {groupId}");
        }

        group.GraphX = graphX;
        group.GraphY = graphY;
        foreach (var move in nodeMoves ?? [])
        {
            var node = FindNode(rule, move.NodeId);
            if (node is null)
            {
                continue;
            }

            node.GraphX = move.GraphX;
            node.GraphY = move.GraphY;
            node.GraphPositionSet = true;
        }

        foreach (var move in rerouteMoves ?? [])
        {
            var reroute = rule.WireReroutes.FirstOrDefault(item => string.Equals(item.Id, move.RerouteId, StringComparison.OrdinalIgnoreCase));
            if (reroute is null)
            {
                continue;
            }

            reroute.GraphX = move.GraphX;
            reroute.GraphY = move.GraphY;
        }

        return GraphEditResult.Ok($"Moved group: {group.Name}");
    }

    public GraphEditResult MoveGroups(
        Rule rule,
        IReadOnlyCollection<GraphGroupMove> groupMoves,
        IReadOnlyCollection<GraphNodeMove> nodeMoves)
    {
        if (groupMoves.Count == 0 && nodeMoves.Count == 0)
        {
            return GraphEditResult.Fail("No group or node movement was supplied.");
        }

        foreach (var move in groupMoves)
        {
            var group = FindGroup(rule, move.GroupId);
            if (group is null)
            {
                return GraphEditResult.Fail($"Group does not exist: {move.GroupId}");
            }

            group.GraphX = move.GraphX;
            group.GraphY = move.GraphY;
        }

        foreach (var move in nodeMoves)
        {
            var node = FindNode(rule, move.NodeId);
            if (node is null)
            {
                continue;
            }

            node.GraphX = move.GraphX;
            node.GraphY = move.GraphY;
            node.GraphPositionSet = true;
        }

        var primaryName = groupMoves.Count == 1
            ? FindGroup(rule, groupMoves.First().GroupId)?.Name ?? "group"
            : $"{groupMoves.Count} group(s)";
        return GraphEditResult.Ok($"Moved {primaryName}.");
    }

    public GraphEditResult ResizeGroup(Rule rule, string groupId, float width, float height)
    {
        var group = FindGroup(rule, groupId);
        if (group is null)
        {
            return GraphEditResult.Fail($"Group does not exist: {groupId}");
        }

        group.Width = MathF.Max(MinimumGroupWidth, width);
        group.Height = MathF.Max(MinimumGroupHeight, height);
        return GraphEditResult.Ok($"Resized group: {group.Name}");
    }
}
