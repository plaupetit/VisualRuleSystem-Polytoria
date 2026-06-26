using Vrs.Graph.Model;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    private static NodeHeaderPalette HeaderPalette(NodeKind kind)
    {
        return kind switch
        {
            NodeKind.Trigger => new NodeHeaderPalette("#e2b632", "#1b1708", "#5a4107", "#fff3bd", "#f0ca43", "ON"),
            NodeKind.Action => new NodeHeaderPalette("#3b9ddd", "#061524", "#0b3659", "#dff4ff", "#76c4ff", "DO"),
            NodeKind.Condition => new NodeHeaderPalette("#38b978", "#071b10", "#0c4528", "#dcffe9", "#72df9b", "IS"),
            NodeKind.Property => new NodeHeaderPalette("#9a8cff", "#111025", "#3b3270", "#f0edff", "#b7aeff", "VAL"),
            NodeKind.Reference => new NodeHeaderPalette("#aeb8c4", "#111720", "#313b46", "#edf5ff", "#c8d6e4", "REF"),
            _ => new NodeHeaderPalette("#aeb8c4", "#111720", "#313b46", "#edf5ff", "#c8d6e4", "NODE")
        };
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
            _ => kind.ToString()
        };
    }

    private static string HumanizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Node";
        }

        var cleaned = value
            .Replace("ACT_", "", StringComparison.OrdinalIgnoreCase)
            .Replace("EV_", "", StringComparison.OrdinalIgnoreCase)
            .Replace("COND_", "", StringComparison.OrdinalIgnoreCase)
            .Replace("PROP_", "", StringComparison.OrdinalIgnoreCase)
            .Replace("REF_", "", StringComparison.OrdinalIgnoreCase)
            .Replace('_', ' ')
            .Replace('-', ' ');

        var words = new List<string>();
        foreach (var part in cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var word = "";
            for (var index = 0; index < part.Length; index++)
            {
                var current = part[index];
                if (index > 0 && char.IsUpper(current) && !char.IsUpper(part[index - 1]))
                {
                    word += " ";
                }

                word += current;
            }

            words.Add(word);
        }

        return words.Count == 0 ? value : string.Join(" ", words);
    }

    private readonly record struct NodeHeaderPalette(
        string HeaderHex,
        string TitleTextHex,
        string BadgeHex,
        string BadgeTextHex,
        string MetaTextHex,
        string BadgeText);
}
