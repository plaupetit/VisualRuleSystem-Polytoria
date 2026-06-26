using Vrs.Graph.Model;
using Vrs.Graph.Modeling;
using Vrs.Graph.Theming;

namespace Vrs.Core.Catalog;

public sealed partial class NodeCatalogService
{
    // Port defaults preserve current graph behavior, then catalog parameters
    // add hidden-by-default value pins that Advanced Pins can expose.
    private static List<NodePort> CreatePorts(NodeCatalogEntry entry)
    {
        var ports = entry.Ports.Count == 0 || entry.Kind is NodeKind.Trigger or NodeKind.Condition or NodeKind.Action
            ? GraphPortDefaults.CreateDefaultPorts(entry.Kind)
            : entry.Ports
                .Select(port => new NodePort
                {
                    Id = port.Id,
                    Label = string.IsNullOrWhiteSpace(port.Label) ? port.Id : port.Label,
                    Direction = port.Direction,
                    PortKind = port.PortKind,
                    DataType = string.IsNullOrWhiteSpace(port.DataType) ? port.PortKind.ToString() : NormalizeDataType(port.DataType),
                    ColorHex = string.IsNullOrWhiteSpace(port.ColorHex) ? GraphTheme.Default.StyleFor(entry.Kind).AccentHex : port.ColorHex,
                    Order = port.Order
                })
                .ToList();

        TunePropertyOutput(entry, ports);
        AddPrimitiveParameterPorts(entry, ports);
        return ports;
    }

    private static void AddPrimitiveParameterPorts(NodeCatalogEntry entry, List<NodePort> ports)
    {
        var order = ports.Count == 0 ? 10 : ports.Max(port => port.Order) + 10;
        foreach (var parameter in entry.Parameters)
        {
            var dataType = NormalizeDataType(parameter.Type);
            if (!IsValuePortDataType(dataType))
            {
                continue;
            }

            var portId = GraphPortDefaults.ParameterPortId(parameter.Key);
            if (ports.Any(port => string.Equals(port.Id, portId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            ports.Add(GraphPortDefaults.CreateInput(
                portId,
                string.IsNullOrWhiteSpace(parameter.Label) ? parameter.Key : parameter.Label,
                NodePortKind.Value,
                dataType,
                ColorForDataType(dataType, GraphTheme.Default.StyleFor(entry.Kind).AccentHex),
                order++));
        }
    }

    private static bool IsValuePortDataType(string dataType)
    {
        return dataType.Equals("String", StringComparison.OrdinalIgnoreCase) ||
            dataType.Equals("Number", StringComparison.OrdinalIgnoreCase) ||
            dataType.Equals("Boolean", StringComparison.OrdinalIgnoreCase) ||
            dataType.Equals("SceneObject", StringComparison.OrdinalIgnoreCase) ||
            dataType.Equals("Vector3", StringComparison.OrdinalIgnoreCase) ||
            dataType.Equals("Color", StringComparison.OrdinalIgnoreCase) ||
            dataType.Equals("Any", StringComparison.OrdinalIgnoreCase);
    }

    private static void TunePropertyOutput(NodeCatalogEntry entry, List<NodePort> ports)
    {
        if (entry.Kind != NodeKind.Property)
        {
            return;
        }

        var valuePort = ports.FirstOrDefault(port =>
            port.Direction == NodePortDirection.Output &&
            string.Equals(port.Id, GraphPortDefaults.ValueOut, StringComparison.OrdinalIgnoreCase));
        if (valuePort is null)
        {
            return;
        }

        var dataType = NormalizeDataType(
            string.IsNullOrWhiteSpace(entry.ApiType)
                ? entry.Parameters.FirstOrDefault(parameter => parameter.Key.Equals("value", StringComparison.OrdinalIgnoreCase))?.Type
                : entry.ApiType);

        valuePort.DataType = dataType;
        valuePort.ColorHex = ColorForDataType(dataType, GraphTheme.Default.StyleFor(entry.Kind).AccentHex);
    }
}
