namespace Vrs.App.Services;

/// <summary>
/// Owns palette row copy, icons, and tooltips so query code can stay focused
/// on filtering and navigation state.
/// </summary>
internal static class NodePaletteBrowserRowFactory
{
    private static readonly IReadOnlyDictionary<string, string> FolderGuideTexts =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Lifecycle"] = "Use these when a script, rule, or object reaches a lifecycle moment.",
            ["Timing"] = "Use these when time controls when the rule starts, repeats, waits, or stops.",
            ["Scene Object"] = "Use these to work with objects placed in the Polytoria scene.",
            ["State"] = "Use these to react to or update named states in the rule flow.",
            ["Variables"] = "Use these to store, read, or update reusable values while the rule runs.",
            ["Debug"] = "Use these to print messages, warnings, or diagnostics while testing a rule.",
            ["Transform"] = "Use these to move, rotate, scale, or otherwise reposition scene objects.",
            ["Physics"] = "Use these to control collision, anchoring, and other physics behavior.",
            ["Physics Motion"] = "Use these to move physics objects and read their speed or spin.",
            ["Logic"] = "Use these to compare true or false situations before continuing.",
            ["Audio"] = "Use these to play, pause, stop, or check sounds placed in the scene.",
            ["Playback"] = "Use these to start, pause, stop, or react to sound playback.",
            ["Settings"] = "Use these to tune values such as volume, looping, or other game behavior.",
            ["Status"] = "Use these to check what is currently happening.",
            ["Lighting"] = "Use these to change fog and world lighting colors.",
            ["Fog"] = "Use these to enable fog and control where it starts and ends.",
            ["Ambient"] = "Use these to change the general light color of the world.",
            ["Light Settings"] = "Use these to change or read light color, brightness, shine, and shadows.",
            ["Light Reach"] = "Use these to change or read how far and wide point lights and spot lights shine.",
            ["Math"] = "Use these to calculate, compare, or validate numeric values.",
            ["Random"] = "Use these to pick random values or random outcomes.",
            ["Text"] = "Use these to check, build, or compare text values.",
            ["Check"] = "Use these to test text, numbers, and other values before the rule continues.",
            ["Edit"] = "Use these to join, trim, replace, or change text before using it elsewhere.",
            ["Empty"] = "Use these to check whether text or another value is empty.",
            ["Input"] = "Use these when player or object input should start a rule.",
            ["Length"] = "Use these to count text characters or check text length limits.",
            ["Match"] = "Use these to compare text or check whether it starts, ends, or contains something.",
            ["Touch"] = "Use these when touching or colliding with an object should start a rule.",
            ["Color"] = "Use these to read, build, compare, or change object colors.",
            ["Tools"] = "Use these to react to tools, activate them, or check who is holding them.",
            ["Animation"] = "Use these to smoothly animate object position, rotation, scale, or color.",
            ["Mesh Animation"] = "Use these to play, stop, or check animations stored on mesh objects.",
            ["Seats"] = "Use these to react to seats or check who is sitting.",
            ["Visibility"] = "Use these to show, hide, or check whether scene objects are visible.",
            ["Transparency"] = "Use these to change or check how faded or see-through an object looks.",
            ["Identity"] = "Use these to read, rename, or check an object's name and type.",
            ["Parent"] = "Use these to read or check the object that contains another object.",
            ["Position"] = "Use these to place an object exactly or move it by an offset.",
            ["Height"] = "Use these to move or check an object's up-down position.",
            ["Zones"] = "Use these to react when an object enters or leaves a watched area.",
            ["Movement"] = "Use these to move an object through the scene.",
            ["Proximity"] = "Use these to react when scene objects become close or far apart.",
            ["Rotation"] = "Use these to turn an object once or keep it spinning.",
            ["Dimensions"] = "Use these to change or check an object's width, height, or depth.",
            ["Scaling"] = "Use these to stretch or shrink an object using its scale values.",
            ["Size"] = "Use these to change or check an object's width, height, depth, or overall size.",
            ["Script"] = "Use these with script-owned values that your rule can reuse.",
            ["Changes"] = "Use these to start a rule when a temporary variable changes.",
            ["Script Numbers"] = "Use these to change or check number variables stored by this script.",
            ["Arithmetic"] = "Use these to add, subtract, multiply, divide, or clamp numbers.",
            ["Range"] = "Use these to clamp, map, interpolate, or check numbers inside a useful range.",
            ["Rounding"] = "Use these to round numbers up, down, or to the nearest whole value.",
            ["Comparison"] = "Use these to compare numbers and choose whether a numeric condition is true.",
            ["Geometry"] = "Use these to measure distances and other spatial values.",
            ["Vector"] = "Use these to build, add, subtract, or type 3D vector values.",
            ["3D Vector"] = "Use these to build, add, subtract, or type 3D position-style values.",
            ["Boolean"] = "Use these to work with true or false values.",
            ["Players"] = "Use these to react to players joining, leaving, or being looked up.",
            ["Count"] = "Use these to check or react to how many players are currently connected.",
            ["Score"] = "Use these to read, change, or compare temporary player score values.",
            ["Lives"] = "Use these to read, change, or compare temporary player life counts.",
            ["Team"] = "Use these to read, set, or compare simple temporary team text.",
            ["Game Team"] = "Use these to read or change the game team assigned to a player.",
            ["Game Teams"] = "Use these to read Polytoria game team names, colors, and player counts.",
            ["Default Health"] = "Use these to read or update default health and respawn settings.",
            ["Default Movement"] = "Use these to read or update default player movement settings.",
            ["Defaults"] = "Use these to read or update default player movement and health settings.",
            ["Events"] = "Use these to react when players join, leave, or reach a player-related moment.",
            ["Context"] = "Use these to read the player or object supplied by the current rule.",
            ["Lookup"] = "Use these to find a player or scene object before checking or changing values.",
            ["Chat"] = "Use these to react to chat or send safe chat messages.",
            ["Children"] = "Use these to find, count, or check child objects under a scene object.",
            ["Type Checks"] = "Use these to check what kind of scene object you are working with.",
            ["Tags"] = "Use these to identify tagged objects without hard-coding every object name.",
            ["Tween"] = "Use these to animate object position, rotation, scale, or color over time.",
            ["Obby"] = "Use these to build checkpoints, hazards, finish lines, coins, timers, and moving platforms for a parcours.",
            ["Checkpoint"] = "Use these to save a player's current stage and send them back to it after a mistake.",
            ["Hazard"] = "Use these for kill bricks, reset zones, or unsafe objects that punish a player touch.",
            ["Finish"] = "Use these to end a run, record the timer, and mark the course as completed.",
            ["Collectibles"] = "Use these to count coins and remember which one-time pickups a player already collected.",
            ["Player State"] = "Use these to store temporary per-player numbers, text, flags, and run progress during this server session.",
            ["Run State"] = "Use these to start, reset, check, and read temporary obby run progress.",
            ["Temporary Player Values"] = "Use these to store and check temporary per-player numbers, text, and flags.",
            ["Player Control"] = "Use these to temporarily control player movement during an obby run.",
            ["Player Events"] = "Use these to react to obby-related player lifecycle moments.",
            ["Moving Platform"] = "Use these to start simple looping platform movement for timing challenges.",
            ["Essentials"] = "Use these curated nodes as a reusable starter pack for most Polytoria games.",
            ["Flow & Timing"] = "Use these for startup flow, repeated timers, cooldowns, debounces, and gates.",
            ["Flow Control"] = "Use these to repeat, stop, or control how execution continues.",
            ["Gate"] = "Use these to open, close, check, or react to simple named gates.",
            ["Effects"] = "Use these to control visual effects placed in the scene.",
            ["Particles"] = "Use these to start, stop, burst, or read particle effects.",
            ["3D Image Display"] = "Use these to change how world images look, light, and face the camera.",
            ["3D Image Texture"] = "Use these to scale or move a texture on a 3D image.",
            ["3D Text Content"] = "Use these to write or read 3D text in the world.",
            ["3D Text Display"] = "Use these to change 3D text color, outline, lighting, and camera-facing behavior.",
            ["NPC Health"] = "Use these to damage, heal, kill, or read NPC health.",
            ["NPC Movement"] = "Use these to move NPCs, make them jump, or react to navigation moments.",
            ["Body Position"] = "Use these to pull an object toward a target position and react when it gets close.",
            ["Game Rules"] = "Use these to control rounds, match state, scores, lives, and reusable gameplay rules.",
            ["UI & Feedback"] = "Use these to show messages, warnings, chat feedback, and test output for players or creators.",
            ["Built-In UI"] = "Use these to show, hide, or check Polytoria's built-in player interface parts.",
            ["Custom UI"] = "Use these to react to or update UI objects placed in PlayerGUI or CoreUI.",
            ["Message"] = "Use these to show simple messages and warnings during play or testing.",
            ["Look At"] = "Use these to rotate an object so it faces a position or another object."
        };

    public static NodePaletteBrowserRow CreateNodeRow(NodePaletteCandidate candidate)
    {
        var icon = NodeIcon(candidate.IntentKey, candidate.IsCompatible);
        return new NodePaletteBrowserRow(
            Kind: NodePaletteBrowserRowKind.Node,
            Key: candidate.Entry.IdBase,
            Index: 0,
            Entry: candidate.Entry,
            Label: candidate.Entry.Label,
            Description: candidate.BeginnerSummary,
            IntentKey: candidate.IntentKey,
            IntentLabel: candidate.IntentLabel,
            DomainPath: candidate.PalettePath,
            DomainLabel: candidate.PathLabel,
            RuntimeLabel: candidate.RuntimeLabel,
            IconGlyph: icon.Glyph,
            IconAccentHex: icon.AccentHex,
            IconBackgroundHex: icon.BackgroundHex,
            MatchSummary: candidate.MatchSummary,
            TooltipTitle: candidate.Entry.Label,
            TooltipText: NodeTooltipText(candidate),
            IsCompatible: candidate.IsCompatible,
            IncompatibilityReason: candidate.IncompatibilityReason,
            CompatibleCount: candidate.IsCompatible ? 1 : 0,
            TotalCount: 1,
            SortOrder: 0);
    }

    public static NodePaletteBrowserRow CreateFolderRow(
        string key,
        string label,
        string intentKey,
        IReadOnlyList<string> domainPath,
        int compatibleCount,
        int totalCount,
        int order)
    {
        var icon = FolderIcon(intentKey, domainPath.Count == 0, compatibleCount > 0);
        var category = FolderCategoryText(intentKey, domainPath);
        var description = FolderGuideText(intentKey, domainPath);
        return new NodePaletteBrowserRow(
            Kind: NodePaletteBrowserRowKind.Folder,
            Key: key,
            Index: 0,
            Entry: null,
            Label: label,
            Description: description,
            IntentKey: intentKey,
            IntentLabel: intentKey,
            DomainPath: domainPath,
            DomainLabel: string.Join(" / ", domainPath),
            RuntimeLabel: "",
            IconGlyph: icon.Glyph,
            IconAccentHex: icon.AccentHex,
            IconBackgroundHex: icon.BackgroundHex,
            MatchSummary: "",
            TooltipTitle: label,
            TooltipText: FolderTooltipText(category, description, compatibleCount, totalCount),
            IsCompatible: compatibleCount > 0,
            IncompatibilityReason: compatibleCount > 0 ? "" : "No compatible nodes in this folder.",
            CompatibleCount: compatibleCount,
            TotalCount: totalCount,
            SortOrder: order);
    }

    private static NodePaletteIcon NodeIcon(string intentKey, bool compatible)
    {
        if (!compatible)
        {
            return new NodePaletteIcon("!", "#d19a66", "#3a2514");
        }

        return intentKey switch
        {
            "When" => new NodePaletteIcon("▶", "#e6b829", "#3a3210"),
            "Do" => new NodePaletteIcon("◆", "#3aa0dc", "#123047"),
            "Check" => new NodePaletteIcon("?", "#35b779", "#123728"),
            "Value" => new NodePaletteIcon("123", "#b58cff", "#2b2444"),
            _ => new NodePaletteIcon("•", "#aeb8c4", "#252b34")
        };
    }

    private static NodePaletteIcon FolderIcon(string intentKey, bool isIntentRoot, bool compatible)
    {
        if (!compatible)
        {
            return new NodePaletteIcon("!", "#d19a66", "#3a2514");
        }

        if (!isIntentRoot)
        {
            return new NodePaletteIcon("▣", "#a0a7ad", "#242424");
        }

        return NodeIcon(intentKey, compatible);
    }

    private static string NodeTooltipText(NodePaletteCandidate candidate)
    {
        var path = string.IsNullOrWhiteSpace(candidate.PathLabel) ? candidate.IntentLabel : candidate.PathLabel;
        var compatibility = candidate.IsCompatible
            ? "Compatible with the current graph context."
            : candidate.IncompatibilityReason;
        return string.Join(
            "\n",
            new[]
            {
                $"{candidate.IntentLabel} / {path}",
                string.IsNullOrWhiteSpace(candidate.MatchSummary) ? "" : $"Match: {candidate.MatchSummary}",
                candidate.BeginnerSummary,
                $"Runs on: {candidate.RuntimeLabel}",
                compatibility
            }.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static string FolderTooltipText(
        string category,
        string description,
        int compatibleCount,
        int totalCount)
    {
        return string.Join(
            "\n",
            new[]
            {
                category,
                description,
                $"{compatibleCount} compatible / {totalCount} total"
            }.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static string FolderCategoryText(string intentKey, IReadOnlyList<string> domainPath)
    {
        if (domainPath.Count > 0)
        {
            return "";
        }

        return intentKey switch
        {
            "When" => "Trigger node category",
            "Do" => "Action node category",
            "Check" => "Condition node category",
            "Value" => "Value node category",
            _ => "Node category"
        };
    }

    private static string FolderGuideText(string intentKey, IReadOnlyList<string> domainPath)
    {
        if (domainPath.Count == 0)
        {
            return intentKey switch
            {
                "When" => "Starts a rule when something happens, such as time passing, a state change, or a player touching an object.",
                "Do" => "Runs an action after a trigger, such as showing a message, moving an object, or changing a variable.",
                "Check" => "Tests whether something is true before the rule continues.",
                "Value" => "Provides a value for another node, such as text, a number, an object, or a calculated result.",
                _ => "Open this category to choose related nodes."
            };
        }

        var leaf = domainPath[^1];
        if (FolderGuideTexts.TryGetValue(leaf, out var text))
        {
            return text;
        }

        return "Open this group to choose related nodes.";
    }

    private sealed record NodePaletteIcon(string Glyph, string AccentHex, string BackgroundHex);
}
