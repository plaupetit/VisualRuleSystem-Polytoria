using Vrs.Graph.Model;

namespace Vrs.Graph.Theming;

public sealed class GraphNodeStyle
{
    public string DisplayName { get; set; } = "";
    public string AccentHex { get; set; } = "#7c8794";
    public string FillHex { get; set; } = "#20252c";
    public string SelectedFillHex { get; set; } = "#2b3542";
}

public sealed class GraphTheme
{
    private readonly Dictionary<NodeKind, GraphNodeStyle> styles;

    public GraphTheme(IReadOnlyDictionary<NodeKind, GraphNodeStyle> styles)
    {
        this.styles = styles.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    public static GraphTheme Default { get; } = new(new Dictionary<NodeKind, GraphNodeStyle>
    {
        [NodeKind.Trigger] = new GraphNodeStyle
        {
            DisplayName = "Trigger",
            AccentHex = "#d7a72f",
            FillHex = "#302812",
            SelectedFillHex = "#4a3914"
        },
        [NodeKind.Action] = new GraphNodeStyle
        {
            DisplayName = "Action",
            AccentHex = "#2f8fd7",
            FillHex = "#13283a",
            SelectedFillHex = "#173955"
        },
        [NodeKind.Condition] = new GraphNodeStyle
        {
            DisplayName = "Condition",
            AccentHex = "#30a66a",
            FillHex = "#143125",
            SelectedFillHex = "#184735"
        },
        [NodeKind.Property] = new GraphNodeStyle
        {
            DisplayName = "Property",
            AccentHex = "#7a6be0",
            FillHex = "#241f38",
            SelectedFillHex = "#30294b"
        },
        [NodeKind.Reference] = new GraphNodeStyle
        {
            DisplayName = "Reference",
            AccentHex = "#7c8794",
            FillHex = "#20252c",
            SelectedFillHex = "#2b3542"
        }
    });

    public GraphNodeStyle StyleFor(NodeKind kind)
    {
        return styles.TryGetValue(kind, out var style)
            ? style
            : styles[NodeKind.Reference];
    }
}
