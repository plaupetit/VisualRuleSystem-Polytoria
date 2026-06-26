using Vrs.Graph.Model;

namespace Vrs.Graph.Modeling;

public sealed partial class RuleGraphEditService
{
    // Connection mutations and endpoint validation shared by canvas interactions and graph validation.
    public GraphEditResult AddConnection(
        Rule rule,
        string fromNodeId,
        string fromPortId,
        string toNodeId,
        string toPortId)
    {
        var validation = ValidateEndpointPair(rule, fromNodeId, fromPortId, toNodeId, toPortId);
        if (!validation.Success)
        {
            return validation;
        }

        if (rule.Connections.Any(connection =>
            SameEndpoint(connection.From, fromNodeId, fromPortId) &&
            SameEndpoint(connection.To, toNodeId, toPortId)))
        {
            return GraphEditResult.Ok("Connection already exists.", changed: false);
        }

        var fromPort = FindPort(rule, fromNodeId, fromPortId)!;
        rule.Connections.Add(new GraphConnection
        {
            Id = CreateConnectionId(fromNodeId, fromPortId, toNodeId, toPortId),
            From = new GraphEndpoint { NodeId = fromNodeId, PortId = fromPortId },
            To = new GraphEndpoint { NodeId = toNodeId, PortId = toPortId },
            ConnectionKind = ConnectionKindFor(fromPort)
        });
        return GraphEditResult.Ok("Added graph connection.");
    }

    public GraphEditResult RemoveConnection(Rule rule, int connectionIndex)
    {
        if (connectionIndex < 0 || connectionIndex >= rule.Connections.Count)
        {
            return GraphEditResult.Fail("No graph connection is selected.");
        }

        var connection = rule.Connections[connectionIndex];
        var rerouteIds = connection.RerouteIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        rule.Connections.RemoveAt(connectionIndex);
        RemoveUnreferencedReroutes(rule, rerouteIds);
        return GraphEditResult.Ok("Removed graph connection.");
    }

    public GraphEditResult DisconnectNode(Rule rule, string nodeId)
    {
        var removedRerouteIds = rule.Connections
            .Where(connection =>
                string.Equals(connection.From.NodeId, nodeId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(connection.To.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(connection => connection.RerouteIds)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var removed = rule.Connections.RemoveAll(connection =>
            string.Equals(connection.From.NodeId, nodeId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(connection.To.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));

        RemoveUnreferencedReroutes(rule, removedRerouteIds);
        return GraphEditResult.Ok(removed == 0 ? "Node had no connections." : $"Disconnected {removed} connection(s).", removed > 0);
    }
    public GraphEditResult ValidateEndpointPair(
        Rule rule,
        string fromNodeId,
        string fromPortId,
        string toNodeId,
        string toPortId)
    {
        if (string.IsNullOrWhiteSpace(fromNodeId) ||
            string.IsNullOrWhiteSpace(fromPortId) ||
            string.IsNullOrWhiteSpace(toNodeId) ||
            string.IsNullOrWhiteSpace(toPortId))
        {
            return GraphEditResult.Fail("Both connection endpoints and ports are required.");
        }

        if (string.Equals(fromNodeId, toNodeId, StringComparison.OrdinalIgnoreCase))
        {
            return GraphEditResult.Fail("A node cannot connect to itself.");
        }

        var fromNode = FindNode(rule, fromNodeId);
        if (fromNode is null)
        {
            return GraphEditResult.Fail($"Connection source does not exist: {fromNodeId}");
        }

        var toNode = FindNode(rule, toNodeId);
        if (toNode is null)
        {
            return GraphEditResult.Fail($"Connection target does not exist: {toNodeId}");
        }

        var fromPort = FindPort(rule, fromNodeId, fromPortId);
        if (fromPort is null)
        {
            return GraphEditResult.Fail($"Connection source port does not exist: {fromNode.Label}.{fromPortId}");
        }

        var toPort = FindPort(rule, toNodeId, toPortId);
        if (toPort is null)
        {
            return GraphEditResult.Fail($"Connection target port does not exist: {toNode.Label}.{toPortId}");
        }

        if (fromPort.Direction != NodePortDirection.Output)
        {
            return GraphEditResult.Fail($"Source port must be an output: {fromNode.Label}.{fromPort.Label}");
        }

        if (toPort.Direction != NodePortDirection.Input)
        {
            return GraphEditResult.Fail($"Target port must be an input: {toNode.Label}.{toPort.Label}");
        }

        if (fromPort.PortKind != toPort.PortKind)
        {
            return GraphEditResult.Fail($"Port kinds are incompatible: {fromPort.PortKind} -> {toPort.PortKind}");
        }

        if (!DataTypesCompatible(fromPort.DataType, toPort.DataType))
        {
            return GraphEditResult.Fail($"Port data types are incompatible: {fromPort.DataType} -> {toPort.DataType}");
        }

        return GraphEditResult.Ok("Connection endpoints are valid.", changed: false);
    }

    public static IEnumerable<NodePort> InputPorts(RuleNode node)
    {
        return node.Ports
            .Where(port => port.Direction == NodePortDirection.Input)
            .OrderBy(port => port.Order)
            .ThenBy(port => port.Label, StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<NodePort> OutputPorts(RuleNode node)
    {
        return node.Ports
            .Where(port => port.Direction == NodePortDirection.Output)
            .OrderBy(port => port.Order)
            .ThenBy(port => port.Label, StringComparer.OrdinalIgnoreCase);
    }

    public static GraphConnectionKind ConnectionKindFor(NodePort fromPort)
    {
        return fromPort.PortKind switch
        {
            NodePortKind.Flow => GraphConnectionKind.Flow,
            NodePortKind.Target or NodePortKind.State => GraphConnectionKind.Reference,
            _ => GraphConnectionKind.Value
        };
    }

    private static bool SameEndpoint(GraphEndpoint endpoint, string nodeId, string portId)
    {
        return string.Equals(endpoint.NodeId, nodeId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(endpoint.PortId, portId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool DataTypesCompatible(string from, string to)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return true;
        }

        var normalizedFrom = NormalizeConnectionDataType(from);
        var normalizedTo = NormalizeConnectionDataType(to);
        return normalizedFrom.Equals("Any", StringComparison.OrdinalIgnoreCase) ||
            normalizedTo.Equals("Any", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedFrom, normalizedTo, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeConnectionDataType(string value)
    {
        if (value.Contains("Vector3", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Position", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Rotation", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Scale", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Direction", StringComparison.OrdinalIgnoreCase))
        {
            return "Vector3";
        }

        if (value.Contains("Color", StringComparison.OrdinalIgnoreCase))
        {
            return "Color";
        }

        if (value.Contains("Object", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Instance", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Player", StringComparison.OrdinalIgnoreCase))
        {
            return "SceneObject";
        }

        if (value.Contains("Text", StringComparison.OrdinalIgnoreCase))
        {
            return "String";
        }

        if (value.Equals("Bool", StringComparison.OrdinalIgnoreCase))
        {
            return "Boolean";
        }

        return value.Trim();
    }

    private static string CreateConnectionId(string fromNodeId, string fromPortId, string toNodeId, string toPortId)
    {
        return $"CONN_{SanitizeId(fromNodeId)}_{SanitizeId(fromPortId)}_{SanitizeId(toNodeId)}_{SanitizeId(toPortId)}";
    }
}
