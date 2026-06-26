using Vrs.Graph.Model;

namespace Vrs.Core.Persistence;

public static class RuleGraphDocumentNormalizer
{
    public static bool NormalizeScriptBinding(RuleGraph graph)
    {
        var changed = false;
        graph.Script ??= new GraphScriptBinding();

        var firstRule = graph.Rules.FirstOrDefault();
        if (firstRule is not null)
        {
            if (!graph.Script.IsScriptKindLocked)
            {
                graph.Script.ScriptKind = firstRule.ScriptKind;
            }

            if (string.IsNullOrWhiteSpace(graph.Script.ScriptName) ||
                string.Equals(graph.Script.ScriptName, "NewVisualScript", StringComparison.Ordinal))
            {
                graph.Script.ScriptName = string.IsNullOrWhiteSpace(firstRule.Name)
                    ? "NewVisualScript"
                    : firstRule.Name;
            }
        }

        foreach (var rule in graph.Rules)
        {
            if (rule.ScriptKind == graph.Script.ScriptKind)
            {
                continue;
            }

            rule.ScriptKind = graph.Script.ScriptKind;
            changed = true;
        }

        if (!graph.Script.AutosaveEnabled)
        {
            // False remains a valid authored value once the property exists in
            // saved JSON. The normalizer intentionally does not force it back.
        }

        changed |= NormalizeTriggeringPlayerSources(graph);

        return changed;
    }

    private static bool NormalizeTriggeringPlayerSources(RuleGraph graph)
    {
        var changed = false;
        foreach (var node in graph.Rules.SelectMany(rule => rule.Nodes))
        {
            if (!IsKillPlayerNode(node))
            {
                continue;
            }

            var player = node.Parameters.FirstOrDefault(parameter =>
                parameter.Key.Equals("player", StringComparison.OrdinalIgnoreCase));
            if (player is null)
            {
                continue;
            }

            changed |= NormalizeTriggeringPlayerParameter(player);
        }

        return changed;
    }

    private static bool IsKillPlayerNode(RuleNode node)
    {
        return node.Type.Equals("KillPlayer", StringComparison.OrdinalIgnoreCase) ||
            node.CatalogId.Equals("ACT_KillPlayer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool NormalizeTriggeringPlayerParameter(RuleParameter parameter)
    {
        if (parameter.Binding.SourceKind == GraphValueSourceKind.TriggeringPlayer)
        {
            var changed = false;
            if (!parameter.Value.Equals("Triggering Player", StringComparison.OrdinalIgnoreCase))
            {
                parameter.Value = "Triggering Player";
                changed = true;
            }

            if (!parameter.ValueSource.Equals("Trigger Context", StringComparison.OrdinalIgnoreCase))
            {
                parameter.ValueSource = "Trigger Context";
                changed = true;
            }

            if (parameter.Binding.DisplayText != "Triggering Player")
            {
                parameter.Binding.DisplayText = "Triggering Player";
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(parameter.Binding.ConstantValue))
            {
                parameter.Binding.ConstantValue = "";
                changed = true;
            }

            return changed;
        }

        if (parameter.Binding.SourceKind != GraphValueSourceKind.Constant)
        {
            return false;
        }

        var authoredValue = FirstNonEmpty(parameter.Binding.ConstantValue, parameter.Value, parameter.Binding.DisplayText);
        if (!string.IsNullOrWhiteSpace(authoredValue) &&
            !authoredValue.Equals("Triggering Player", StringComparison.OrdinalIgnoreCase) &&
            !authoredValue.Equals("Current Player", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        parameter.Value = "Triggering Player";
        parameter.ValueSource = "Trigger Context";
        parameter.Binding.SourceKind = GraphValueSourceKind.TriggeringPlayer;
        parameter.Binding.ConstantValue = "";
        parameter.Binding.VariableName = "";
        parameter.Binding.SceneObjectPath = "";
        parameter.Binding.CatalogId = "";
        parameter.Binding.CatalogType = "";
        parameter.Binding.CatalogParameters.Clear();
        parameter.Binding.DisplayText = "Triggering Player";
        if (string.IsNullOrWhiteSpace(parameter.Binding.DataType))
        {
            parameter.Binding.DataType = "Any";
        }

        return true;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";
    }

    public static void ApplyScriptBinding(
        RuleGraph graph,
        GraphScriptKind scriptKind,
        string scriptName,
        GraphAuthoringMode authoringMode,
        string source,
        bool lockScriptKind,
        string creatorParentPath = "",
        string creatorObjectPath = "",
        string linkedScriptPath = "",
        string projectRelativePath = "")
    {
        graph.Script ??= new GraphScriptBinding();
        graph.AuthoringMode = authoringMode;
        graph.Script.ScriptKind = scriptKind;
        graph.Script.ScriptName = string.IsNullOrWhiteSpace(scriptName) ? "NewVisualScript" : scriptName.Trim();
        graph.Script.Source = string.IsNullOrWhiteSpace(source) ? "Draft" : source;
        graph.Script.IsScriptKindLocked = lockScriptKind;
        graph.Script.CreatorParentPath = creatorParentPath;
        graph.Script.CreatorObjectPath = creatorObjectPath;
        graph.Script.LinkedScriptPath = linkedScriptPath;
        graph.Script.ProjectRelativePath = projectRelativePath;

        foreach (var rule in graph.Rules)
        {
            rule.ScriptKind = scriptKind;
            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                rule.Name = graph.Script.ScriptName;
            }
        }
    }
}
