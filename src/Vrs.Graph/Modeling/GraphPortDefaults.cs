using Vrs.Graph.Model;
using Vrs.Graph.Theming;

namespace Vrs.Graph.Modeling;

public static class GraphPortDefaults
{
    public const string FlowOut = "flowOut";
    public const string FlowIn = "flowIn";
    public const string TrueOut = "true";
    public const string FalseOut = "false";
    public const string ValueOut = "valueOut";
    public const string ParameterPortPrefix = "param_";
    public const string TriggerFlowColor = "#e2b632";
    public const string ActionFlowColor = "#3b9ddd";
    public const string ConditionFlowColor = "#38b978";
    public const string ConditionFalseColor = "#ef4444";

    public static string ParameterPortId(string key)
    {
        var safeKey = new string((key ?? "")
            .Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_')
            .ToArray());

        return $"{ParameterPortPrefix}{(string.IsNullOrWhiteSpace(safeKey) ? "value" : safeKey)}";
    }

    public static List<NodePort> CreateDefaultPorts(NodeKind kind)
    {
        return kind switch
        {
            NodeKind.Trigger =>
            [
                Output(FlowOut, "Out", NodePortKind.Flow, "Flow", TriggerFlowColor, 0)
            ],
            NodeKind.Action =>
            [
                Input(FlowIn, "In", NodePortKind.Flow, "Flow", ActionFlowColor, 0),
                Output(FlowOut, "Out", NodePortKind.Flow, "Flow", ActionFlowColor, 0)
            ],
            NodeKind.Condition =>
            [
                Input(FlowIn, "In", NodePortKind.Flow, "Flow", ConditionFlowColor, 0),
                Output(TrueOut, "True", NodePortKind.Flow, "Flow", ConditionFlowColor, 0),
                Output(FalseOut, "False", NodePortKind.Flow, "Flow", ConditionFalseColor, 1)
            ],
            NodeKind.Property =>
            [
                Output(ValueOut, "Value", NodePortKind.Value, "Any", GraphTheme.Default.StyleFor(NodeKind.Property).AccentHex, 0)
            ],
            _ => []
        };
    }

    public static List<NodePort> ClonePorts(IEnumerable<NodePort> ports)
    {
        return ports
            .Select(port => new NodePort
            {
                Id = port.Id,
                Label = port.Label,
                Direction = port.Direction,
                PortKind = port.PortKind,
                DataType = port.DataType,
                ColorHex = port.ColorHex,
                Order = port.Order
            })
            .ToList();
    }

    public static NodePort CreateInput(string id, string label, NodePortKind kind, string dataType, string colorHex, int order)
    {
        return Input(id, label, kind, dataType, colorHex, order);
    }

    public static NodePort CreateOutput(string id, string label, NodePortKind kind, string dataType, string colorHex, int order)
    {
        return Output(id, label, kind, dataType, colorHex, order);
    }

    private static NodePort Input(string id, string label, NodePortKind kind, string dataType, string colorHex, int order)
    {
        return Port(id, label, NodePortDirection.Input, kind, dataType, colorHex, order);
    }

    private static NodePort Output(string id, string label, NodePortKind kind, string dataType, string colorHex, int order)
    {
        return Port(id, label, NodePortDirection.Output, kind, dataType, colorHex, order);
    }

    private static NodePort Port(string id, string label, NodePortDirection direction, NodePortKind kind, string dataType, string colorHex, int order)
    {
        return new NodePort
        {
            Id = id,
            Label = label,
            Direction = direction,
            PortKind = kind,
            DataType = dataType,
            ColorHex = colorHex,
            Order = order
        };
    }
}
