using System.Text.RegularExpressions;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.Core.Authoring;

/// <summary>
/// Builds compact human-facing summaries from catalog metadata and visual parameter bindings.
/// </summary>
public static partial class NodeReadableSummaryService
{
    public static string BuildNodeDisplayName(RuleNode node)
    {
        if (node.Type.Equals("MoveObject", StringComparison.OrdinalIgnoreCase))
        {
            var mode = ParameterValue(node, "positionMode", "Add");
            return mode.Equals("Set", StringComparison.OrdinalIgnoreCase)
                ? "(Set) Move Object"
                : "(Add) Move Object";
        }

        if (node.Type.Equals("RotateObject", StringComparison.OrdinalIgnoreCase))
        {
            var mode = ParameterValue(node, "rotationMode", "Add");
            if (mode.Equals("Set", StringComparison.OrdinalIgnoreCase))
            {
                return "(Set) Rotate Object";
            }

            return mode.Equals("Spin", StringComparison.OrdinalIgnoreCase)
                ? "(Spin) Rotate Object"
                : "(Add) Rotate Object";
        }

        if (node.Type.Equals("SetObjectScale", StringComparison.OrdinalIgnoreCase))
        {
            return "(Set) Scale Object";
        }

        if (!string.IsNullOrWhiteSpace(node.Label))
        {
            return node.Label;
        }

        if (!string.IsNullOrWhiteSpace(node.Type))
        {
            return HumanizeIdentifier(node.Type);
        }

        return HumanizeIdentifier(node.Id);
    }

    public static string BuildNodeSummary(RuleNode node, NodeCatalogEntry? catalogEntry)
    {
        IReadOnlyList<NodeCatalogParameterDefinition> definitions = catalogEntry?.Parameters ?? [];
        var valuesByKey = node.Parameters.ToDictionary(
            parameter => parameter.Key,
            parameter => BuildParameterSummary(parameter, definitions.FirstOrDefault(definition =>
                string.Equals(definition.Key, parameter.Key, StringComparison.OrdinalIgnoreCase))),
            StringComparer.OrdinalIgnoreCase);

        var template = catalogEntry?.PreviewTemplate ?? "";
        if (!string.IsNullOrWhiteSpace(template))
        {
            return TemplateTokenRegex().Replace(template, match =>
            {
                var key = match.Groups["key"].Value;
                return valuesByKey.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                    ? value
                    : $"missing {HumanizeIdentifier(key)}";
            });
        }

        var parameterText = node.Parameters
            .Select(parameter =>
            {
                var definition = definitions.FirstOrDefault(item =>
                    string.Equals(item.Key, parameter.Key, StringComparison.OrdinalIgnoreCase));
                var label = string.IsNullOrWhiteSpace(definition?.Label)
                    ? HumanizeIdentifier(parameter.Key)
                    : definition.Label;
                var value = BuildParameterSummary(parameter, definition);
                return string.IsNullOrWhiteSpace(value) ? "" : $"{label}: {value}";
            })
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Take(3)
            .ToList();

        var verb = HumanVerb(node.Kind);
        var labelText = string.IsNullOrWhiteSpace(node.Label) ? HumanizeIdentifier(node.Type) : node.Label;
        return parameterText.Count == 0
            ? $"{verb} {labelText}"
            : $"{verb} {labelText}: {string.Join(", ", parameterText)}";
    }

    private static string ParameterValue(RuleNode node, string key, string fallback)
    {
        var parameter = node.Parameters.FirstOrDefault(item =>
            item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(parameter?.Value) ? fallback : parameter.Value;
    }

    public static string BuildParameterSummary(RuleParameter parameter, NodeCatalogParameterDefinition? definition)
    {
        var binding = parameter.Binding;
        var display = CleanDisplayText(binding.DisplayText);
        return binding.SourceKind switch
        {
            GraphValueSourceKind.Constant => FirstNonEmpty(display, CleanDisplayText(binding.ConstantValue), parameter.Value),
            GraphValueSourceKind.Self => "Self",
            GraphValueSourceKind.Target => "Target",
            GraphValueSourceKind.TriggeringPlayer => "Triggering Player",
            GraphValueSourceKind.SceneObject => FirstNonEmpty(display, CleanDisplayText(binding.SceneObjectPath), parameter.Value),
            GraphValueSourceKind.LocalVariable => FirstNonEmpty(display, CleanDisplayText(binding.VariableName), parameter.Value),
            GraphValueSourceKind.GlobalVariable => FirstNonEmpty(display, CleanDisplayText(binding.VariableName), parameter.Value),
            GraphValueSourceKind.ConnectedPort => "connected value",
            _ => FirstNonEmpty(display, parameter.Value, definition?.Fallback ?? "")
        };
    }

    private static string CleanDisplayText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var cleaned = value.Trim();
        foreach (var prefix in new[]
        {
            "Constant:",
            "Target context:",
            "Local variable:",
            "Global variable:",
            "Scene object:"
        })
        {
            if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return cleaned[prefix.Length..].Trim();
            }
        }

        return cleaned.Equals("(empty constant)", StringComparison.OrdinalIgnoreCase) ? "" : cleaned;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";
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
            _ => "Node"
        };
    }

    private static string HumanizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Node";
        }

        var spaced = IdentifierBoundaryRegex().Replace(value, " ");
        return string.Join(" ", spaced
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    [GeneratedRegex(@"\$\{(?<key>[^}]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex TemplateTokenRegex();

    [GeneratedRegex("(?<!^)(?=[A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierBoundaryRegex();
}
