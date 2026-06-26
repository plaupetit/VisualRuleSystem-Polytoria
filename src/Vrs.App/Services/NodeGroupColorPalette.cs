namespace Vrs.App.Services;

/// <summary>
/// UI projection for the stable color names stored on RuleNodeGroup.
/// </summary>
/// <remarks>
/// The graph model keeps only a simple string so documents stay portable;
/// Avalonia-specific fill, border, and text colors are resolved at the edge.
/// </remarks>
public static class NodeGroupColorPalette
{
    public static IReadOnlyList<string> Names { get; } =
    [
        "Teal",
        "Blue",
        "Green",
        "Amber",
        "Rose",
        "Purple",
        "Gray"
    ];

    public static NodeGroupColor Resolve(string color)
    {
        return color.Trim() switch
        {
            "Blue" => new NodeGroupColor("Blue", "#1d4d75", "#3b9ddd", "#dff4ff", "#102233"),
            "Green" => new NodeGroupColor("Green", "#1f5539", "#38b978", "#dcffe9", "#10291c"),
            "Amber" => new NodeGroupColor("Amber", "#5f4914", "#e2b632", "#fff3bd", "#30240a"),
            "Rose" => new NodeGroupColor("Rose", "#64273c", "#f4729d", "#ffe4ef", "#34131f"),
            "Purple" => new NodeGroupColor("Purple", "#453578", "#9a8cff", "#f0edff", "#241d3f"),
            "Gray" => new NodeGroupColor("Gray", "#3b4654", "#aeb8c4", "#edf5ff", "#1e2730"),
            _ => new NodeGroupColor("Teal", "#145a5a", "#2dd4bf", "#ddfffb", "#0b2d2d")
        };
    }
}

public sealed record NodeGroupColor(
    string Name,
    string BorderHex,
    string AccentHex,
    string TextHex,
    string FillHex);
