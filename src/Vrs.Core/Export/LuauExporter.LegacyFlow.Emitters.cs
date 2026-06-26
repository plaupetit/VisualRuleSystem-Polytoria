using System.Text;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Legacy template-backed trigger setup and action code emitters.
    private static void AppendTriggerSetup(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        LuauExportOptions options,
        int indentLevel)
    {
        var parameterValues = BuildEffectiveParameterValues(rule, trigger, nodesById);
        var entry = NodeCatalogService.FindByCatalogId(catalog, trigger.CatalogId);
        var templatePath =
            ResolveTemplatePath(entry, TemplateRole(EffectiveScriptKind(rule, options))) ??
            ResolveTemplatePath(entry, "server");

        if (options.IncludeComments)
        {
            builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, "TRIGGER SETUP START"));
        }

        if (templatePath is null)
        {
            if (options.IncludeComments)
            {
                builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, "No trigger setup exporter configured."));
                builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, "TRIGGER SETUP END"));
            }

            return;
        }

        var template = File.ReadAllText(templatePath);
        var rendered = RenderTemplate(template, trigger, parameterValues);
        if (!rendered.Contains("${", StringComparison.Ordinal))
        {
            builder.Append(Indent(rendered, indentLevel));
            if (options.IncludeComments)
            {
                builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, "TRIGGER SETUP END"));
            }

            return;
        }

        builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, "Trigger template contains unresolved placeholders; setup skipped."));
        if (options.IncludeComments)
        {
            builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, "TRIGGER SETUP END"));
        }
    }

    private static void AppendActionCode(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        LuauExportOptions options,
        int indentLevel)
    {
        var parameterValues = BuildEffectiveParameterValues(rule, action, nodesById);
        if (action.Type.Equals("WaitSeconds", StringComparison.OrdinalIgnoreCase))
        {
            AppendWaitSeconds(builder, action, parameterValues, indentLevel);
            return;
        }

        if (action.Type.Equals("SetObjectColor", StringComparison.OrdinalIgnoreCase))
        {
            AppendSetObjectColor(builder, action, parameterValues, indentLevel);
            return;
        }

        var entry = NodeCatalogService.FindByCatalogId(catalog, action.CatalogId);
        var templatePath =
            ResolveTemplatePath(entry, TemplateRole(EffectiveScriptKind(rule, options))) ??
            ResolveTemplatePath(entry, "server");
        if (templatePath is not null)
        {
            var template = File.ReadAllText(templatePath);
            var rendered = RenderTemplate(template, action, parameterValues);
            if (!rendered.Contains("${", StringComparison.Ordinal))
            {
                builder.Append(Indent(rendered, indentLevel));
                return;
            }
        }

        builder.AppendLine($"{IndentText(indentLevel)}vrsLog(\"Action {EscapeForDoubleQuotedString(action.Label)} has no exporter yet; skipped safely.\")");
    }
}
