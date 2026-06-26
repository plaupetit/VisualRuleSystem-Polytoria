using Vrs.Graph.Model;

namespace Vrs.Core.ProjectInputs;

/// <summary>
/// Converts graph input nodes into the project input actions that VRS is
/// allowed to manage before deployment touches Polytoria's input.json file.
/// </summary>
internal static class VrsInputGenerationPlanner
{
    public static VrsInputGenerationPlan Build(Rule rule)
    {
        var required = new Dictionary<string, RequiredInputBuilder>(StringComparer.OrdinalIgnoreCase);
        var conflicts = new List<string>();

        foreach (var node in EnumerateNodes(rule.Nodes))
        {
            if (!TryGetInputActionType(node.Type, out var actionType))
            {
                continue;
            }

            var actionName = ResolveActionName(node, actionType);
            if (required.TryGetValue(actionName, out var existing))
            {
                if (existing.Type != actionType)
                {
                    conflicts.Add($"Input action '{actionName}' is used as both {existing.Type} and {actionType}. Use different action names for different input types.");
                    continue;
                }

                existing.NodeIds.Add(node.Id);
                continue;
            }

            required[actionName] = new RequiredInputBuilder(actionName, actionType, [node.Id]);
        }

        var actions = required.Values
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new VrsInputActionDefinition
            {
                Name = item.Name,
                Type = item.Type,
                NodeIds = item.NodeIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .ToList();

        return new VrsInputGenerationPlan(actions, conflicts.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static bool TryGetInputActionType(string nodeType, out VrsInputActionType actionType)
    {
        if (nodeType.Equals("OnInputButtonDown", StringComparison.OrdinalIgnoreCase) ||
            nodeType.Equals("InputButtonDown", StringComparison.OrdinalIgnoreCase) ||
            nodeType.Equals("SendInputEvent", StringComparison.OrdinalIgnoreCase) ||
            nodeType.Equals("OnVrsInputEvent", StringComparison.OrdinalIgnoreCase))
        {
            actionType = VrsInputActionType.Button;
            return true;
        }

        if (nodeType.Equals("InputAxisValue", StringComparison.OrdinalIgnoreCase))
        {
            actionType = VrsInputActionType.Axis;
            return true;
        }

        if (nodeType.Equals("InputVectorX", StringComparison.OrdinalIgnoreCase) ||
            nodeType.Equals("InputVectorY", StringComparison.OrdinalIgnoreCase))
        {
            actionType = VrsInputActionType.Vector2;
            return true;
        }

        actionType = default;
        return false;
    }

    private static string ResolveActionName(RuleNode node, VrsInputActionType actionType)
    {
        var key = node.Parameters.Any(item => item.Key.Equals("inputAction", StringComparison.OrdinalIgnoreCase))
            ? "inputAction"
            : "actionName";
        var parameter = node.Parameters.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        var bindingValue = parameter?.Binding.SourceKind == GraphValueSourceKind.Constant ? parameter.Binding.ConstantValue : "";
        var value = FirstNonEmpty(bindingValue, parameter?.Value, ReadKeyValue(node.Value, key), DefaultActionName(actionType));
        return value.Trim();
    }

    private static string DefaultActionName(VrsInputActionType actionType)
    {
        return actionType == VrsInputActionType.Button ? "Interact" : "Horizontal";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "";
    }

    private static string ReadKeyValue(string value, string key)
    {
        foreach (var segment in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var index = segment.IndexOf('=', StringComparison.Ordinal);
            if (index <= 0)
            {
                continue;
            }

            var segmentKey = segment[..index].Trim();
            if (segmentKey.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return segment[(index + 1)..].Trim();
            }
        }

        return "";
    }

    private static IEnumerable<RuleNode> EnumerateNodes(IEnumerable<RuleNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in EnumerateNodes(node.ChildNodes))
            {
                yield return child;
            }
        }
    }

    private sealed record RequiredInputBuilder(string Name, VrsInputActionType Type, List<string> NodeIds);
}
