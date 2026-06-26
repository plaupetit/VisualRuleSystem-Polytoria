using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Readable export naming/order helpers: stable function/config names for human-oriented Luau output.
    private static IEnumerable<RuleNode> OrderedNodes(Rule rule)
    {
        return rule.Nodes
            .OrderBy(node => NodeOrder(node.Kind))
            .ThenBy(node => node.GraphX)
            .ThenBy(node => node.GraphY)
            .ThenBy(node => node.Label, StringComparer.OrdinalIgnoreCase);
    }

    private static int NodeOrder(NodeKind kind)
    {
        return kind switch
        {
            NodeKind.Trigger => 0,
            NodeKind.Condition => 1,
            NodeKind.Action => 2,
            NodeKind.Property => 3,
            _ => 10
        };
    }

    private static int TriggerStartupOrder(RuleNode trigger)
    {
        return trigger.Type.Equals("OnStart", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    private static string PreferredFunctionName(RuleNode node)
    {
        if (node.Kind == NodeKind.Trigger && node.Type.Equals("OnStart", StringComparison.OrdinalIgnoreCase))
        {
            return "onStart";
        }

        if (node.Kind == NodeKind.Trigger && node.Type.Equals("OnTimerTick", StringComparison.OrdinalIgnoreCase))
        {
            return "onTimerTick";
        }

        if (node.Kind == NodeKind.Action && node.Type.Equals("SetObjectColor", StringComparison.OrdinalIgnoreCase))
        {
            return "changeObjectColor";
        }

        var prefix = node.Kind == NodeKind.Condition ? "check" : "";
        return ToCamelIdentifier($"{prefix} {node.Label}", node.Kind == NodeKind.Condition ? "checkCondition" : "runAction");
    }

    private static string FunctionName(ReadableExportPlan plan, RuleNode node)
    {
        return plan.FunctionNames.TryGetValue(node.Id, out var value) ? value : PreferredFunctionName(node);
    }

    private static string ConfigName(ReadableExportPlan plan, RuleNode node, string key)
    {
        return plan.ConfigNames.TryGetValue(ConfigKey(node, key), out var value) ? value : UniqueUpperIdentifier($"{node.Label}_{key}");
    }

    private static bool HasConfigName(ReadableExportPlan plan, RuleNode node, string key)
    {
        return plan.ConfigNames.ContainsKey(ConfigKey(node, key));
    }

    private static string ConfigKey(RuleNode node, string key)
    {
        return $"{node.Id}\u001F{key}";
    }

    private static bool ReadableTargetNeedsResolver(Rule rule, RuleNode node, IReadOnlyDictionary<string, RuleNode> nodesById)
    {
        var parameterValues = BuildEffectiveParameterValues(rule, node, nodesById);
        var targetName = ParameterValue(node, parameterValues, "target");
        return !string.IsNullOrWhiteSpace(targetName) && !targetName.Equals("Self", StringComparison.OrdinalIgnoreCase);
    }

    private static string UniqueIdentifier(string preferred, ISet<string> used)
    {
        var sanitized = preferred.All(ch => char.IsLetterOrDigit(ch) || ch == '_')
            ? preferred
            : SafeIdentifier(preferred);
        if (used.Add(sanitized))
        {
            return sanitized;
        }

        var index = 2;
        while (!used.Add($"{sanitized}_{index}"))
        {
            index++;
        }

        return $"{sanitized}_{index}";
    }

    private static string UniqueUpperIdentifier(string value)
    {
        var words = WordPattern().Matches(value)
            .Select(match => match.Value.ToUpperInvariant())
            .ToList();
        return words.Count == 0 ? "CONFIG_VALUE" : string.Join("_", words);
    }

    private static string ToCamelIdentifier(string value, string fallback)
    {
        var words = WordPattern().Matches(value)
            .Select(match => match.Value)
            .ToList();
        if (words.Count == 0)
        {
            return fallback;
        }

        var builder = new StringBuilder(words[0].ToLowerInvariant());
        foreach (var word in words.Skip(1))
        {
            builder.Append(char.ToUpperInvariant(word[0]));
            if (word.Length > 1)
            {
                builder.Append(word[1..].ToLowerInvariant());
            }
        }

        var result = builder.ToString();
        return char.IsDigit(result[0]) ? $"{fallback}{result}" : result;
    }

    private static string HumanBlockName(string value)
    {
        var words = WordPattern().Matches(value)
            .Select(match => match.Value.ToUpperInvariant())
            .ToList();
        return words.Count == 0 ? "NODE" : string.Join(" ", words);
    }

}
