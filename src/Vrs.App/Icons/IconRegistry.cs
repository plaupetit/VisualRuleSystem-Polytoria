using Vrs.Graph.Model;

namespace Vrs.App.Icons;

public static class IconRegistry
{
    private const string IconRoot = "/Assets/Icons/PolytoriaLike/";

    public static IconDescriptor ForSceneKind(string kind)
    {
        var normalized = Normalize(kind);
        return normalized switch
        {
            "world" or "root" => Tabler("world", "WORLD", "#4aa3ff", "#12314d"),
            "environment" => Tabler("trees", "ENV", "#39c98a", "#123728"),
            "lighting" => Tabler("bulb", "LIGHT", "#e7c246", "#3a3210"),
            "players" => Tabler("users", "PLAYERS", "#78b7ff", "#162c45"),
            "teams" => Tabler("users-group", "TEAMS", "#9cc66f", "#26381c"),
            "scriptservice" => Tabler("folder-code", "SCRIPTS", "#5eb7ff", "#14324a"),
            "serverhidden" or "hidden" => Phosphor("folder-simple-lock", "HIDDEN", "#b9a7ff", "#27223f"),
            "folder" => Phosphor("folder-simple", "FOLDER", "#d9b657", "#3a2c10"),
            "serverscript" => Tabler("server-cog", "SERVER", "#73c2ff", "#132e48"),
            "clientscript" or "localscript" => Tabler("device-desktop-code", "CLIENT", "#4bc8f2", "#10313d"),
            "modulescript" => Tabler("package", "MODULE", "#c5a7ff", "#2b2342"),
            "script" or "basescript" or "scriptinstance" => Tabler("file-code", "SCRIPT", "#8dc8ff", "#192f44"),
            "part" or "basepart" => Tabler("cube", "PART", "#7dd3fc", "#14333e"),
            "meshpart" => Phosphor("cube-transparent", "MESH", "#7dd3fc", "#14333e"),
            "model" or "physicalmodel" => Tabler("box", "MODEL", "#c9b77a", "#342d18"),
            "polytorianmodel" or "charactermodel" or "npc" => Phosphor("stack", "MODEL", "#97d182", "#1f351b"),
            "camera" => Tabler("camera", "CAMERA", "#8ab4ff", "#1c2e4c"),
            "sound" => Phosphor("speaker-high", "SOUND", "#e7c46a", "#382f16"),
            "particles" => Tabler("sparkles", "FX", "#d4a8ff", "#2d2140"),
            "sunlight" or "spotlight" => Phosphor("lightbulb", "LIGHT", "#e7c246", "#3a3210"),
            "gui" or "playergui" or "coreuiservice" or "uilabel" or "text3d" => Phosphor("textbox", "UI", "#80d8ff", "#153442"),
            "achievementsservice" => Tabler("trophy", "ACH", "#f2c84b", "#3c3111"),
            "captureservice" => Tabler("target", "CAP", "#f28f5b", "#3b2117"),
            "stats" => Tabler("chart-bar", "STATS", "#79d189", "#173620"),
            "inventory" => Tabler("package", "INV", "#b9c4d8", "#242c39"),
            "networkevent" => Tabler("hierarchy", "EVENT", "#9ad6ff", "#183449"),
            _ => Tabler("atom", string.IsNullOrWhiteSpace(kind) ? "OBJ" : ShortLabel(kind), "#9aa8b5", "#242c39")
        };
    }

    public static IconDescriptor ForValueSource(GraphValueSourceKind sourceKind)
    {
        return sourceKind switch
        {
            GraphValueSourceKind.Constant => Tabler("text-caption", "CONST", "#8fd0ff", "#142d3b"),
            GraphValueSourceKind.Self => Tabler("target", "SELF", "#78d18f", "#18351f"),
            GraphValueSourceKind.Target => Tabler("target", "TARGET", "#f0c45c", "#372d15"),
            GraphValueSourceKind.TriggeringPlayer => Tabler("users", "PLAYER", "#69d1ff", "#143342"),
            GraphValueSourceKind.SceneObject => Tabler("cube", "OBJECT", "#7dd3fc", "#14333e"),
            GraphValueSourceKind.LocalVariable => Tabler("database", "LOCAL", "#c3b1ff", "#2a2441"),
            GraphValueSourceKind.GlobalVariable => Tabler("database", "GLOBAL", "#f0a35c", "#3a2414"),
            GraphValueSourceKind.ConnectedPort => Tabler("hierarchy", "WIRE", "#95d5ff", "#17354a"),
            GraphValueSourceKind.CatalogValue => Tabler("list-tree", "BUILD", "#b58cff", "#2b2444"),
            _ => Tabler("atom", "VALUE", "#9aa8b5", "#242c39")
        };
    }

    public static IconDescriptor ForParameterType(string type, string control)
    {
        var normalizedType = Normalize(type);
        var normalizedControl = Normalize(control);

        if (normalizedType.Contains("vector3", StringComparison.OrdinalIgnoreCase) ||
            normalizedType.Contains("position", StringComparison.OrdinalIgnoreCase) ||
            normalizedType.Contains("rotation", StringComparison.OrdinalIgnoreCase) ||
            normalizedType.Contains("scale", StringComparison.OrdinalIgnoreCase) ||
            normalizedType.Contains("direction", StringComparison.OrdinalIgnoreCase) ||
            normalizedControl.Contains("vector", StringComparison.OrdinalIgnoreCase))
        {
            return Tabler("layers-selected", "VECTOR", "#67e8f9", "#14333e");
        }

        if (normalizedType.Contains("color", StringComparison.OrdinalIgnoreCase) ||
            normalizedControl.Contains("color", StringComparison.OrdinalIgnoreCase))
        {
            return Tabler("palette", "COLOR", "#fb7185", "#3a1e2a");
        }

        if (normalizedType.Contains("object", StringComparison.OrdinalIgnoreCase) ||
            normalizedType.Contains("instance", StringComparison.OrdinalIgnoreCase) ||
            normalizedControl.Contains("object", StringComparison.OrdinalIgnoreCase))
        {
            return Tabler("cube", "OBJECT", "#7dd3fc", "#14333e");
        }

        if (normalizedType is "number" or "integer" or "float" or "decimal")
        {
            return Tabler("chart-bar", "NUMBER", "#f0c45c", "#372d15");
        }

        if (normalizedType is "boolean" or "bool")
        {
            return Tabler("settings-cog", "BOOL", "#88d28a", "#17351f");
        }

        if (normalizedControl.Contains("choice", StringComparison.OrdinalIgnoreCase) ||
            normalizedControl.Contains("dropdown", StringComparison.OrdinalIgnoreCase))
        {
            return Tabler("list-tree", "CHOICE", "#8fc8ff", "#172f43");
        }

        return Phosphor("textbox", "TEXT", "#b8c7d9", "#242d39");
    }

    public static IconDescriptor ForProjectFile(string name, string extension, bool isDirectory)
    {
        if (isDirectory)
        {
            return Phosphor("folder-simple", "FOLDER", "#f0b166", "#362514");
        }

        var normalizedName = name.Trim().ToLowerInvariant();
        var normalizedExtension = extension.Trim().ToLowerInvariant();
        if (normalizedExtension is ".luau" or ".lua")
        {
            return normalizedName.Contains(".client.", StringComparison.Ordinal)
                ? Tabler("device-desktop-code", "CLIENT", "#4bc8f2", "#10313d")
                : normalizedName.Contains(".module.", StringComparison.Ordinal)
                    ? Tabler("package", "MODULE", "#c5a7ff", "#2b2342")
                    : Tabler("server-cog", "SERVER", "#73c2ff", "#132e48");
        }

        return normalizedExtension switch
        {
            ".json" or ".ptproj" => Tabler("settings-cog", "CONFIG", "#e8c86f", "#352d14"),
            ".md" => Tabler("text-caption", "DOC", "#b9c4d8", "#242c39"),
            ".ptaddon" => Tabler("package", "ADDON", "#c5a7ff", "#2b2342"),
            ".poly" => Tabler("world", "WORLD", "#4aa3ff", "#12314d"),
            ".model" or ".ptmodel" => Tabler("box", "MODEL", "#c9b77a", "#342d18"),
            ".ps1" or ".cmd" or ".bat" => Phosphor("terminal-window", "TOOL", "#8fd0ff", "#142d3b"),
            _ => Tabler("file-code", "FILE", "#9aa8b5", "#242c39")
        };
    }

    private static IconDescriptor Tabler(string name, string label, string accentHex, string backgroundHex)
    {
        return new IconDescriptor(name, $"{IconRoot}tabler-{name}.svg", label, accentHex, backgroundHex);
    }

    private static IconDescriptor Phosphor(string name, string label, string accentHex, string backgroundHex)
    {
        return new IconDescriptor(name, $"{IconRoot}phosphor-{name}.svg", label, accentHex, backgroundHex);
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace(" ", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
    }

    private static string ShortLabel(string value)
    {
        var letters = new string(value.Where(char.IsLetterOrDigit).Take(8).ToArray());
        return string.IsNullOrWhiteSpace(letters) ? "OBJ" : letters.ToUpperInvariant();
    }
}
