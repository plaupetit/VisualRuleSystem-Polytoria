using Vrs.Graph.Model;

namespace Vrs.Graph.Modeling;

public sealed record GraphWireRerouteMove(string RerouteId, float GraphX, float GraphY);

public sealed partial class RuleGraphEditService
{
    public GraphEditResult AddWireReroute(
        Rule rule,
        int connectionIndex,
        float graphX,
        float graphY,
        int insertAtIndex = -1)
    {
        if (connectionIndex < 0 || connectionIndex >= rule.Connections.Count)
        {
            return GraphEditResult.Fail("No graph connection is selected.");
        }

        var connection = rule.Connections[connectionIndex];
        var reroute = new RuleWireReroute
        {
            Id = CreateWireRerouteId(rule),
            GraphX = graphX,
            GraphY = graphY,
            InputDirection = WireRerouteDirection.Left,
            OutputDirection = WireRerouteDirection.Right
        };

        rule.WireReroutes.Add(reroute);
        var insertion = insertAtIndex < 0
            ? connection.RerouteIds.Count
            : Math.Clamp(insertAtIndex, 0, connection.RerouteIds.Count);
        connection.RerouteIds.Insert(insertion, reroute.Id);
        AssignRerouteToContainingGroup(rule, reroute);
        AutoFitAllGroups(rule);
        return GraphEditResult.Ok("Added wire reroute.");
    }

    public GraphEditResult MoveWireReroute(Rule rule, string rerouteId, float graphX, float graphY)
    {
        var reroute = FindWireReroute(rule, rerouteId);
        if (reroute is null)
        {
            return GraphEditResult.Fail($"Wire reroute does not exist: {rerouteId}");
        }

        reroute.GraphX = graphX;
        reroute.GraphY = graphY;
        AssignRerouteToContainingGroup(rule, reroute);
        AutoFitAllGroups(rule);

        return GraphEditResult.Ok("Moved wire reroute.");
    }

    public GraphEditResult SetWireRerouteDirections(
        Rule rule,
        string rerouteId,
        string inputDirection,
        string outputDirection)
    {
        var reroute = FindWireReroute(rule, rerouteId);
        if (reroute is null)
        {
            return GraphEditResult.Fail($"Wire reroute does not exist: {rerouteId}");
        }

        var normalizedInput = WireRerouteDirection.Normalize(inputDirection, WireRerouteDirection.Left);
        var normalizedOutput = WireRerouteDirection.Normalize(outputDirection, WireRerouteDirection.Right);
        if (string.Equals(reroute.InputDirection, normalizedInput, StringComparison.Ordinal) &&
            string.Equals(reroute.OutputDirection, normalizedOutput, StringComparison.Ordinal))
        {
            return GraphEditResult.Ok("Wire reroute directions unchanged.", changed: false);
        }

        reroute.InputDirection = normalizedInput;
        reroute.OutputDirection = normalizedOutput;
        return GraphEditResult.Ok("Updated wire reroute directions.");
    }

    public GraphEditResult RemoveWireReroute(Rule rule, string rerouteId)
    {
        var reroute = FindWireReroute(rule, rerouteId);
        if (reroute is null)
        {
            return GraphEditResult.Fail($"Wire reroute does not exist: {rerouteId}");
        }

        rule.WireReroutes.Remove(reroute);
        foreach (var connection in rule.Connections)
        {
            connection.RerouteIds.RemoveAll(id => string.Equals(id, rerouteId, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var group in rule.NodeGroups)
        {
            group.MemberRerouteIds.RemoveAll(id => string.Equals(id, rerouteId, StringComparison.OrdinalIgnoreCase));
        }

        AutoFitAllGroups(rule);
        return GraphEditResult.Ok("Removed wire reroute.");
    }

    public GraphEditResult MoveGroups(
        Rule rule,
        IReadOnlyCollection<GraphGroupMove> groupMoves,
        IReadOnlyCollection<GraphNodeMove> nodeMoves,
        IReadOnlyCollection<GraphWireRerouteMove> rerouteMoves)
    {
        var result = MoveGroups(rule, groupMoves, nodeMoves);
        if (!result.Success)
        {
            return result;
        }

        foreach (var move in rerouteMoves)
        {
            var reroute = FindWireReroute(rule, move.RerouteId);
            if (reroute is null)
            {
                continue;
            }

            reroute.GraphX = move.GraphX;
            reroute.GraphY = move.GraphY;
        }

        return GraphEditResult.Ok(result.Message, result.Changed || rerouteMoves.Count > 0);
    }

    public GraphEditResult NormalizeWireReroutes(Rule rule)
    {
        var changed = false;
        foreach (var reroute in rule.WireReroutes)
        {
            var input = WireRerouteDirection.Normalize(reroute.InputDirection, WireRerouteDirection.Left);
            var output = WireRerouteDirection.Normalize(reroute.OutputDirection, WireRerouteDirection.Right);
            if (!string.Equals(reroute.InputDirection, input, StringComparison.Ordinal))
            {
                reroute.InputDirection = input;
                changed = true;
            }

            if (!string.Equals(reroute.OutputDirection, output, StringComparison.Ordinal))
            {
                reroute.OutputDirection = output;
                changed = true;
            }
        }

        var rerouteIds = rule.WireReroutes.Select(reroute => reroute.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var group in rule.NodeGroups)
        {
            changed |= group.MemberRerouteIds.RemoveAll(id => !rerouteIds.Contains(id)) > 0;
        }

        return GraphEditResult.Ok(changed ? "Normalized wire reroutes." : "Wire reroutes are already normalized.", changed);
    }

    private RuleWireReroute? FindWireReroute(Rule rule, string rerouteId)
    {
        return rule.WireReroutes.FirstOrDefault(reroute => string.Equals(reroute.Id, rerouteId, StringComparison.OrdinalIgnoreCase));
    }

    private void AssignRerouteToContainingGroup(Rule rule, RuleWireReroute reroute)
    {
        foreach (var group in rule.NodeGroups)
        {
            group.MemberRerouteIds.RemoveAll(id => string.Equals(id, reroute.Id, StringComparison.OrdinalIgnoreCase));
        }

        var parentId = FindDeepestContainingGroup(rule, "", new GraphPoint(reroute.GraphX, reroute.GraphY));
        if (string.IsNullOrWhiteSpace(parentId))
        {
            return;
        }

        var parent = FindGroup(rule, parentId);
        parent?.MemberRerouteIds.Add(reroute.Id);
    }

    private void RemoveUnreferencedReroutes(Rule rule, IReadOnlyCollection<string> candidateIds)
    {
        if (candidateIds.Count == 0)
        {
            return;
        }

        var referenced = rule.Connections
            .SelectMany(connection => connection.RerouteIds)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removeIds = candidateIds
            .Where(id => !referenced.Contains(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (removeIds.Count == 0)
        {
            return;
        }

        rule.WireReroutes.RemoveAll(reroute => removeIds.Contains(reroute.Id));
        foreach (var group in rule.NodeGroups)
        {
            group.MemberRerouteIds.RemoveAll(removeIds.Contains);
        }
    }

    private static string CreateWireRerouteId(Rule rule)
    {
        for (var index = 1; index < 10_000; index++)
        {
            var candidate = $"REROUTE_{index}";
            if (rule.WireReroutes.All(reroute => !string.Equals(reroute.Id, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }

        return $"REROUTE_{Guid.NewGuid():N}";
    }
}
