using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vrs.Core.ProjectInputs;

public sealed partial class ProjectInputManagerService
{
    private static readonly JsonSerializerOptions PrettyJsonOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions CompactJsonOptions = new() { WriteIndented = false };

    private static async Task<JsonObject> ReadInputDocumentAsync(string inputPath, bool shouldCreate, CancellationToken cancellationToken)
    {
        if (!File.Exists(inputPath))
        {
            return shouldCreate ? new JsonObject { ["Actions"] = new JsonArray() } : new JsonObject();
        }

        var json = await File.ReadAllTextAsync(inputPath, cancellationToken).ConfigureAwait(false);
        var node = JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        if (node is not JsonObject root)
        {
            throw new InvalidOperationException("input.json must contain a JSON object.");
        }

        return root;
    }

    private static JsonArray GetOrCreateActionsArray(JsonObject inputDocument)
    {
        if (!inputDocument.TryGetPropertyValue("Actions", out var actionsNode) || actionsNode is null)
        {
            var actions = new JsonArray();
            inputDocument["Actions"] = actions;
            return actions;
        }

        if (actionsNode is JsonArray actionsArray)
        {
            return actionsArray;
        }

        throw new InvalidOperationException("input.json Actions must be an array.");
    }

    private static JsonObject? FindAction(JsonArray actions, string name)
    {
        foreach (var actionNode in actions)
        {
            if (actionNode is not JsonObject action)
            {
                continue;
            }

            var actionName = ReadString(action, "Name");
            if (actionName.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return action;
            }
        }

        return null;
    }

    private static void RemoveAction(JsonArray actions, JsonObject action)
    {
        for (var i = 0; i < actions.Count; i++)
        {
            if (ReferenceEquals(actions[i], action))
            {
                actions.RemoveAt(i);
                return;
            }
        }
    }

    private static JsonObject CreateGeneratedAction(VrsInputActionDefinition action)
    {
        if (VrsInputPresetCatalog.TryFind(action.Name, action.Type, out var preset))
        {
            return CreateGeneratedAction(preset);
        }

        return action.Type switch
        {
            VrsInputActionType.Button => new JsonObject
            {
                ["Type"] = "Button",
                ["Name"] = action.Name,
                ["Buttons"] = new JsonArray()
            },
            VrsInputActionType.Axis => new JsonObject
            {
                ["Type"] = "Axis",
                ["Name"] = action.Name,
                ["Negative"] = new JsonArray(),
                ["Positive"] = new JsonArray()
            },
            VrsInputActionType.Vector2 => new JsonObject
            {
                ["Type"] = "Vector2",
                ["Name"] = action.Name,
                ["Up"] = new JsonArray(),
                ["Down"] = new JsonArray(),
                ["Left"] = new JsonArray(),
                ["Right"] = new JsonArray()
            },
            _ => throw new ArgumentOutOfRangeException(nameof(action), "Unknown input action type.")
        };
    }

    private static JsonObject CreateGeneratedAction(VrsInputPreset preset)
    {
        return preset.Type switch
        {
            VrsInputActionType.Button => new JsonObject
            {
                ["Type"] = "Button",
                ["Name"] = preset.Name,
                ["Buttons"] = ToJsonArray(preset.Buttons)
            },
            VrsInputActionType.Axis => new JsonObject
            {
                ["Type"] = "Axis",
                ["Name"] = preset.Name,
                ["Negative"] = ToJsonArray(preset.Negative),
                ["Positive"] = ToJsonArray(preset.Positive)
            },
            VrsInputActionType.Vector2 => new JsonObject
            {
                ["Type"] = "Vector2",
                ["Name"] = preset.Name,
                ["Up"] = ToJsonArray(preset.Up),
                ["Down"] = ToJsonArray(preset.Down),
                ["Left"] = ToJsonArray(preset.Left),
                ["Right"] = ToJsonArray(preset.Right)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(preset), "Unknown input preset type.")
        };
    }

    private static JsonArray ToJsonArray(IEnumerable<int> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static IReadOnlyList<VrsInputActionChoice> ReadInputActionChoices(JsonObject inputDocument)
    {
        if (!inputDocument.TryGetPropertyValue("Actions", out var actionsNode) || actionsNode is not JsonArray actions)
        {
            return VrsInputPresetCatalog.DefaultChoices;
        }

        var choices = VrsInputPresetCatalog.DefaultChoices
            .ToDictionary(choice => ChoiceKey(choice.Name, choice.Type), choice => choice, StringComparer.OrdinalIgnoreCase);

        foreach (var actionNode in actions)
        {
            if (actionNode is not JsonObject action || !TryReadActionType(action, out var type))
            {
                continue;
            }

            var name = ReadString(action, "Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var key = ChoiceKey(name, type);
            choices[key] = choices.TryGetValue(key, out var existing) && existing.Source.Contains("Preset", StringComparison.OrdinalIgnoreCase)
                ? VrsInputPresetCatalog.ToChoice(name, type, "Project+Preset")
                : VrsInputPresetCatalog.ToChoice(name, type, "Project");
        }

        return choices.Values
            .OrderBy(choice => InputActionTypeOrder(choice.Type))
            .ThenBy(choice => choice.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ChoiceKey(string name, VrsInputActionType type)
        => $"{type}:{name.Trim()}";

    private static int InputActionTypeOrder(VrsInputActionType type)
    {
        return type switch
        {
            VrsInputActionType.Button => 0,
            VrsInputActionType.Axis => 1,
            VrsInputActionType.Vector2 => 2,
            _ => 99
        };
    }

    private static bool TryReadActionType(JsonObject action, out VrsInputActionType type)
    {
        var rawType = ReadString(action, "Type");
        if (rawType.Equals("Button", StringComparison.OrdinalIgnoreCase))
        {
            type = VrsInputActionType.Button;
            return true;
        }

        if (rawType.Equals("Axis", StringComparison.OrdinalIgnoreCase))
        {
            type = VrsInputActionType.Axis;
            return true;
        }

        if (rawType.Equals("Vector2", StringComparison.OrdinalIgnoreCase))
        {
            type = VrsInputActionType.Vector2;
            return true;
        }

        type = default;
        return false;
    }

    private static string ReadString(JsonObject obj, string propertyName)
    {
        return obj.TryGetPropertyValue(propertyName, out var value) && value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
            ? text
            : "";
    }

    private static string HashAction(JsonObject action)
    {
        var bytes = Encoding.UTF8.GetBytes(action.ToJsonString(CompactJsonOptions));
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
