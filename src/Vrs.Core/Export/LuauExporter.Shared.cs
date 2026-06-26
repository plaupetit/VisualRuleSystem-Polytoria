using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static GraphScriptKind EffectiveScriptKind(Rule rule, LuauExportOptions options)
    {
        return options.ScriptKindOverride ?? rule.ScriptKind;
    }

    private static string ScriptKindRole(GraphScriptKind kind)
    {
        return kind switch
        {
            GraphScriptKind.Local => "Local",
            GraphScriptKind.Module => "Module",
            _ => "Server"
        };
    }

    private static string ScriptKindSuffix(GraphScriptKind kind)
    {
        return kind switch
        {
            GraphScriptKind.Local => ".client.luau",
            GraphScriptKind.Module => ".module.luau",
            _ => ".server.luau"
        };
    }

    private static string TemplateRole(GraphScriptKind kind)
    {
        return kind switch
        {
            GraphScriptKind.Local => "local",
            GraphScriptKind.Module => "module",
            _ => "server"
        };
    }

    private static string? ResolveTemplatePath(NodeCatalogEntry? entry, string role)
    {
        if (entry is null || !entry.Templates.TryGetValue(role, out var relativePath) || string.IsNullOrWhiteSpace(entry.PackageDirectory))
        {
            return null;
        }

        var path = Path.Combine(entry.PackageDirectory, relativePath);
        return File.Exists(path) ? path : null;
    }

    private static string RenderTemplate(string template, RuleNode node, IReadOnlyDictionary<string, string> parameterValues)
    {
        return TemplatePattern().Replace(template, match =>
        {
            var kind = match.Groups["kind"].Value;
            var key = match.Groups["key"].Value;
            var value = ParameterValue(node, parameterValues, key);

            return kind switch
            {
                "param" => value,
                "paramLiteral" => LuauStringLiteral(value),
                "paramUpper" => value.ToUpperInvariant(),
                "targetName" => string.IsNullOrWhiteSpace(value) ? "Self" : value,
                "targetExpression" => $"vrsResolveTarget(context, {LuauStringLiteral(value)})",
                _ => match.Value
            };
        });
    }

    private static Dictionary<string, string> BuildEffectiveParameterValues(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById)
    {
        var values = node.Parameters.ToDictionary(
            parameter => parameter.Key,
            EffectiveParameterValue,
            StringComparer.OrdinalIgnoreCase);

        // Value wires override the literal inspector value, matching node graph
        // author expectations without removing the manual fallback.
        foreach (var parameter in node.Parameters)
        {
            var portId = GraphPortDefaults.ParameterPortId(parameter.Key);
            var incoming = rule.Connections.LastOrDefault(connection =>
                connection.ConnectionKind != GraphConnectionKind.Flow &&
                string.Equals(connection.To.NodeId, node.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(connection.To.PortId, portId, StringComparison.OrdinalIgnoreCase));

            if (incoming is null || !nodesById.TryGetValue(incoming.From.NodeId, out var sourceNode))
            {
                continue;
            }

            var resolved = ResolveSourceNodeValue(sourceNode);
            if (!string.IsNullOrEmpty(resolved))
            {
                values[parameter.Key] = resolved;
            }
        }

        return values;
    }

    private static string ResolveSourceNodeValue(RuleNode node)
    {
        if (node.Kind == NodeKind.Property)
        {
            var authoredParameter = node.Parameters.FirstOrDefault(parameter => parameter.Key.Equals("value", StringComparison.OrdinalIgnoreCase));
            var authoredValue = authoredParameter is null ? "" : EffectiveParameterValue(authoredParameter);
            return string.IsNullOrWhiteSpace(authoredValue) ? node.Value : authoredValue;
        }

        return node.Value;
    }

    private static string EffectiveParameterValue(RuleParameter parameter)
    {
        return parameter.Binding.SourceKind switch
        {
            GraphValueSourceKind.Constant => parameter.Value,
            GraphValueSourceKind.Self => "Self",
            GraphValueSourceKind.Target => "Target",
            GraphValueSourceKind.TriggeringPlayer => "Triggering Player",
            GraphValueSourceKind.SceneObject => string.IsNullOrWhiteSpace(parameter.Binding.SceneObjectPath) ? parameter.Value : parameter.Binding.SceneObjectPath,
            GraphValueSourceKind.LocalVariable or GraphValueSourceKind.GlobalVariable => string.IsNullOrWhiteSpace(parameter.Binding.VariableName) ? parameter.Value : parameter.Binding.VariableName,
            GraphValueSourceKind.CatalogValue => string.IsNullOrWhiteSpace(parameter.Binding.CatalogId) ? parameter.Value : parameter.Binding.CatalogId,
            GraphValueSourceKind.ConnectedPort => parameter.Value,
            _ => parameter.Value
        };
    }

    private static string ParameterValue(RuleNode node, IReadOnlyDictionary<string, string> parameterValues, string key)
    {
        return parameterValues.TryGetValue(key, out var value)
            ? value
            : node.Parameters.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)) is { } parameter
                ? EffectiveParameterValue(parameter)
                : "";
    }

    private static string NumericLiteral(string value, string fallback)
    {
        return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : fallback;
    }

    private static bool BooleanValue(string value, bool fallback)
    {
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static string Indent(string text, int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var lines = text.Replace("\r\n", "\n").Split('\n');
        return string.Join(Environment.NewLine, lines.Select(line => string.IsNullOrWhiteSpace(line) ? line : indent + line)) + Environment.NewLine;
    }

    private static string IndentText(int indentLevel)
    {
        return new string(' ', Math.Max(0, indentLevel) * 4);
    }

    private static string LuauStringLiteral(string value)
    {
        return JsonSerializer.Serialize(value);
    }

    private static string EscapeForDoubleQuotedString(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string SafeIdentifier(string value)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? "GeneratedRule" : value;
        var chars = raw.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray();
        var result = new string(chars);
        return char.IsDigit(result[0]) ? $"Rule_{result}" : result;
    }

    [GeneratedRegex(@"\$\{(?<kind>param|paramLiteral|paramUpper|targetName|targetExpression):(?<key>[A-Za-z0-9_]+)\}")]
    private static partial Regex TemplatePattern();

    [GeneratedRegex("[A-Za-z0-9]+")]
    private static partial Regex WordPattern();
}
