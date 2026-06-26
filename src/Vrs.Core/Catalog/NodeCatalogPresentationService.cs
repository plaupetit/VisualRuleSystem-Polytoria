using System.Globalization;
using Vrs.Graph.Model;

namespace Vrs.Core.Catalog;

/// <summary>
/// Converts technical catalog manifests into stable authoring language used by
/// palettes, search, and tests. It keeps the beginner vocabulary out of Avalonia
/// while preserving manifest ids for save/load/export.
/// </summary>
public static class NodeCatalogPresentationService
{
    private static readonly IReadOnlyDictionary<string, string> DomainLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["debug"] = "Debug",
            ["lifecycle"] = "Lifecycle",
            ["timing"] = "Timing",
            ["time"] = "Timing",
            ["scene object"] = "Scene Object",
            ["object"] = "Scene Object",
            ["objects"] = "Scene Object",
            ["scene"] = "Scene Object",
            ["transform"] = "Scene Object",
            ["movement"] = "Scene Object",
            ["motion"] = "Scene Object",
            ["ui & feedback"] = "UI & Feedback",
            ["built in ui"] = "Built-In UI",
            ["core ui"] = "Built-In UI",
            ["custom ui"] = "Custom UI",
            ["message"] = "Message",
            ["state"] = "State",
            ["variables"] = "Variables",
            ["variable"] = "Variables",
            ["script variable"] = "Variables",
            ["math"] = "Math",
            ["number"] = "Math",
            ["numbers"] = "Math",
            ["range"] = "Range",
            ["rounding"] = "Rounding",
            ["comparison"] = "Comparison",
            ["geometry"] = "Geometry",
            ["vector"] = "Vector",
            ["3d vector"] = "3D Vector",
            ["3d image display"] = "3D Image Display",
            ["3d image texture"] = "3D Image Texture",
            ["3d text content"] = "3D Text Content",
            ["3d text display"] = "3D Text Display",
            ["npc health"] = "NPC Health",
            ["npc movement"] = "NPC Movement",
            ["body position"] = "Body Position",
            ["text"] = "Text",
            ["string"] = "Text",
            ["logic"] = "Logic",
            ["condition"] = "Logic",
            ["conditions"] = "Logic",
            ["player"] = "Players",
            ["players"] = "Players",
            ["score"] = "Score",
            ["lives"] = "Lives",
            ["team"] = "Team",
            ["chat"] = "Chat",
            ["input"] = "Input",
            ["player default"] = "Players",
            ["player defaults"] = "Players",
            ["children"] = "Children",
            ["type checks"] = "Type Checks",
            ["instance"] = "Scene Object",
            ["instances"] = "Scene Object",
            ["tag"] = "Tags",
            ["tags"] = "Tags",
            ["tween"] = "Tween",
            ["tweens"] = "Tween",
            ["obby"] = "Obby",
            ["parcours"] = "Obby",
            ["checkpoint"] = "Checkpoint",
            ["checkpoints"] = "Checkpoint",
            ["hazard"] = "Hazard",
            ["hazards"] = "Hazard",
            ["finish"] = "Finish",
            ["collectible"] = "Collectibles",
            ["collectibles"] = "Collectibles",
            ["player state"] = "Player State",
            ["player runtime values"] = "Temporary Player Values",
            ["temporary player values"] = "Temporary Player Values",
            ["player control"] = "Player Control",
            ["player events"] = "Player Events",
            ["run state"] = "Run State",
            ["flow control"] = "Flow Control",
            ["moving platform"] = "Moving Platform",
            ["value source"] = "Values",
            ["value sources"] = "Values",
            ["values"] = "Values",
            ["primitive"] = "Values"
        };

    public static NodeCatalogIntent GetIntent(NodeKind kind)
    {
        return kind switch
        {
            NodeKind.Trigger => new NodeCatalogIntent("When", "When", "Starts a rule when something happens, such as time passing, a state change, or a player touching an object.", 0),
            NodeKind.Action => new NodeCatalogIntent("Do", "Do", "Runs an action after a trigger, such as showing a message, moving an object, or changing a variable.", 1),
            NodeKind.Condition => new NodeCatalogIntent("Check", "Check", "Tests whether something is true before the rule continues.", 2),
            NodeKind.Property => new NodeCatalogIntent("Value", "Value", "Provides a value for another node, such as text, a number, an object, or a calculated result.", 3),
            _ => new NodeCatalogIntent(kind.ToString(), kind.ToString(), "node", 9)
        };
    }

    public static string GetDomain(NodeCatalogEntry entry)
    {
        var raw = entry.PalettePath.FirstOrDefault(part => !string.IsNullOrWhiteSpace(part)) ??
            FirstNonEmpty(entry.Subcategory, entry.Category, entry.FamilyFolder, entry.UtilityLayer);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "General";
        }

        return NormalizeDomainSegment(raw);
    }

    public static IReadOnlyList<string> GetPalettePath(NodeCatalogEntry entry)
    {
        var path = entry.PalettePath
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(NormalizeDomainSegment)
            .ToList();
        if (path.Count > 0)
        {
            return path;
        }

        var fallback = GetDomain(entry);
        return string.IsNullOrWhiteSpace(fallback) ? ["General"] : [fallback];
    }

    public static IReadOnlyList<IReadOnlyList<string>> GetPalettePaths(NodeCatalogEntry entry)
    {
        var paths = new List<IReadOnlyList<string>> { GetPalettePath(entry) };
        foreach (var alias in entry.PaletteAliases)
        {
            var normalized = alias
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(NormalizeDomainSegment)
                .ToList();
            if (normalized.Count > 0)
            {
                paths.Add(normalized);
            }
        }

        // Manifests can temporarily repeat an alias while a pack is being
        // curated; presentation keeps only one row per distinct path.
        return paths
            .GroupBy(path => string.Join("/", path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public static string GetBeginnerSummary(NodeCatalogEntry entry)
    {
        return FirstNonEmpty(
            entry.BeginnerSummary,
            entry.Description,
            GetIntent(entry.Kind).Description,
            "Add this node to the graph.");
    }

    public static string GetSurface(NodeCatalogEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.Surface) ? "UserFacing" : entry.Surface.Trim();
    }

    public static bool IsDefaultPaletteSurface(NodeCatalogEntry entry)
    {
        var surface = GetSurface(entry);
        return !surface.Equals("Support", StringComparison.OrdinalIgnoreCase) &&
            !surface.Equals("Internal", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetRuntimeLabel(NodeCatalogEntry entry)
    {
        var family = entry.RuntimeFamily.Trim();
        if (string.IsNullOrWhiteSpace(family) ||
            family.Equals("Shared", StringComparison.OrdinalIgnoreCase) ||
            family.Equals("Any", StringComparison.OrdinalIgnoreCase) ||
            family.Equals("Generic", StringComparison.OrdinalIgnoreCase))
        {
            return "Shared";
        }

        if (family.Equals("Local", StringComparison.OrdinalIgnoreCase) ||
            family.Equals("Client", StringComparison.OrdinalIgnoreCase) ||
            family.Equals("ClientScript", StringComparison.OrdinalIgnoreCase))
        {
            return "Client";
        }

        if (family.Equals("ServerScript", StringComparison.OrdinalIgnoreCase))
        {
            return "Server";
        }

        if (family.Equals("ModuleScript", StringComparison.OrdinalIgnoreCase))
        {
            return "Module";
        }

        return TitleCase(family);
    }

    public static string GetDataTypeLabel(string? value)
    {
        return CatalogDataTypeNormalizer.NormalizeValueType(value) switch
        {
            "Vector3" => "3D Vector",
            "SceneObject" => "Scene Object",
            "String" => "Text",
            "Boolean" => "True/False",
            var normalized when string.IsNullOrWhiteSpace(normalized) => "Value",
            var normalized => normalized
        };
    }

    public static IEnumerable<string> GetSearchTerms(NodeCatalogEntry entry)
    {
        var intent = GetIntent(entry.Kind);
        yield return entry.Label;
        yield return entry.Type;
        yield return entry.IdBase;
        yield return entry.Kind.ToString();
        yield return intent.Key;
        yield return intent.Label;
        yield return intent.Description;
        yield return GetDomain(entry);
        foreach (var pathPart in GetPalettePath(entry))
        {
            yield return pathPart;
        }

        yield return string.Join(" ", GetPalettePath(entry));
        foreach (var path in GetPalettePaths(entry).Skip(1))
        {
            foreach (var pathPart in path)
            {
                yield return pathPart;
            }

            yield return string.Join(" ", path);
        }

        yield return GetBeginnerSummary(entry);
        yield return GetRuntimeLabel(entry);
        yield return GetSurface(entry);
        yield return entry.RuntimeFamily;
        yield return entry.Category;
        yield return entry.Subcategory;
        yield return entry.FamilyFolder;
        yield return entry.UtilityLayer;
        yield return entry.Description;
        yield return entry.ModuleId;
        yield return entry.PreviewTemplate;
        yield return entry.ApiGroup;
        yield return entry.ApiType;
        yield return entry.Value;

        foreach (var term in entry.SearchKeywords)
        {
            yield return term;
        }

        foreach (var hint in entry.DebugHints)
        {
            yield return hint;
        }

        foreach (var hint in entry.SelectorHints)
        {
            yield return hint.Key;
            yield return hint.Label;
            yield return hint.Description;
            yield return hint.DataType;
        }

        foreach (var parameter in entry.Parameters)
        {
            yield return parameter.Key;
            yield return parameter.Label;
            yield return parameter.Description;
            yield return parameter.Type;
            yield return parameter.Control;
            yield return parameter.ValueSource;
            yield return parameter.Default;

            foreach (var keyword in parameter.SearchKeywords)
            {
                yield return keyword;
            }

            foreach (var option in parameter.Options)
            {
                yield return option;
            }

            foreach (var detail in parameter.OptionDetails)
            {
                yield return detail.Value;
                yield return detail.Label;
                yield return detail.Category;
                yield return detail.Description;
                foreach (var keyword in detail.SearchKeywords)
                {
                    yield return keyword;
                }
            }

            foreach (var hint in parameter.SelectorHints)
            {
                yield return hint.Key;
                yield return hint.Label;
                yield return hint.Description;
                yield return hint.DataType;
            }

            foreach (var snippet in parameter.Snippets)
            {
                yield return snippet.Label;
                yield return snippet.Description;
                yield return snippet.Code;
            }
        }
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "";
    }

    private static string TitleCase(string value)
    {
        var lowered = value.Trim().ToLowerInvariant();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(lowered);
    }

    private static string NormalizeDomainSegment(string value)
    {
        var normalized = value.Replace('-', ' ').Replace('_', ' ').Trim();
        return DomainLabels.TryGetValue(normalized, out var friendly)
            ? friendly
            : TitleCase(normalized);
    }
}

public sealed record NodeCatalogIntent(string Key, string Label, string Description, int Order);
