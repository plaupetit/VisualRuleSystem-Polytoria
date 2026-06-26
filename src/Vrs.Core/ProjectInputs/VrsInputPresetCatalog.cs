namespace Vrs.Core.ProjectInputs;

/// <summary>
/// Built-in VRS input presets. These are project-file actions, not runtime
/// scene objects; NetworkEvents are derived separately from button actions.
/// </summary>
public static class VrsInputPresetCatalog
{
    public const string RuntimeInputEventFolderPath = "World/Hidden/VRS/Events/Input";

    private static readonly VrsInputPreset[] Presets =
    [
        Button("Interact", 69),
        Button("Jump", 32),
        Button("Sprint"),
        Button("Crouch", 67),
        Button("Dash", 81),
        Button("Attack", 70),
        Button("Use"),
        Button("Inventory", 73),
        Button("Menu", 77),
        Axis("Horizontal", negative: [65, 4194319], positive: [68, 4194321, 3000]),
        Axis("Vertical", negative: [83, 4194322], positive: [87, 4194320, 3001]),
        Axis("Camera")
    ];

    public static IReadOnlyList<VrsInputPreset> All => Presets;

    public static IReadOnlyList<VrsInputActionChoice> DefaultChoices =>
        Presets.Select(preset => ToChoice(preset.Name, preset.Type, "Preset")).ToList();

    public static IEnumerable<VrsInputActionChoice> ButtonEventChoices(IEnumerable<VrsInputActionChoice> choices)
    {
        return choices
            .Where(choice => choice.Type == VrsInputActionType.Button)
            .GroupBy(choice => choice.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(choice => PresetSortIndex(choice.Name))
            .ThenBy(choice => choice.Name, StringComparer.OrdinalIgnoreCase);
    }

    public static bool TryFind(string name, VrsInputActionType type, out VrsInputPreset preset)
    {
        preset = Presets.FirstOrDefault(item =>
            item.Type == type &&
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))!;
        return preset is not null;
    }

    public static VrsInputActionChoice ToChoice(string name, VrsInputActionType type, string source)
    {
        var eventName = ToInputEventName(name);
        return new VrsInputActionChoice(
            name,
            type,
            source,
            eventName,
            $"{RuntimeInputEventFolderPath}/{eventName}");
    }

    public static string ToInputEventName(string inputActionName)
    {
        var cleaned = new string((inputActionName ?? "")
            .Trim()
            .Select(character => character is '/' or '\\' ? ' ' : character)
            .ToArray());
        cleaned = string.Join(" ", cleaned.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(cleaned) ? "Input" : cleaned;
    }

    private static int PresetSortIndex(string name)
    {
        for (var i = 0; i < Presets.Length; i++)
        {
            if (Presets[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return Presets.Length + 1;
    }

    private static VrsInputPreset Button(string name, params int[] buttons)
    {
        return new VrsInputPreset(name, VrsInputActionType.Button, buttons, [], [], [], [], [], []);
    }

    private static VrsInputPreset Axis(string name, IReadOnlyList<int>? negative = null, IReadOnlyList<int>? positive = null)
    {
        return new VrsInputPreset(name, VrsInputActionType.Axis, [], negative ?? [], positive ?? [], [], [], [], []);
    }
}
