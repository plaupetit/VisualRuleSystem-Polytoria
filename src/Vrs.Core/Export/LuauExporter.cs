using System.Text;
using Vrs.Core.Authoring;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.Core.Export;

/// <summary>
/// Turns the human-readable graph into readable Luau without requiring a heavy
/// visual runtime inside the game.
/// </summary>
public sealed partial class LuauExporter
{
    // The public exporter stays data-first: catalog packages provide node
    // metadata/templates, while this service translates graph intent into Luau.
    public string ExportRuleToLuau(Rule rule, RuleGraph graph, IEnumerable<NodeCatalogEntry> catalogEntries, LuauExportOptions? options = null)
    {
        options ??= new LuauExportOptions();
        var scriptKind = EffectiveScriptKind(rule, options);
        var catalog = catalogEntries.ToList();
        var builder = new StringBuilder();

        AppendReadableScript(builder, rule, graph, catalog, options);
        return builder.ToString();
    }

    public IReadOnlyList<ExportedLuauFile> ExportRuleToLuauFiles(Rule rule, RuleGraph graph, IEnumerable<NodeCatalogEntry> catalogEntries, LuauExportOptions? options = null)
    {
        options ??= new LuauExportOptions();
        var scriptKind = EffectiveScriptKind(rule, options);

        return
        [
            new ExportedLuauFile
            {
                Suffix = ScriptKindSuffix(scriptKind),
                Role = ScriptKindRole(scriptKind),
                Content = ExportRuleToLuau(rule, graph, catalogEntries, options)
            }
        ];
    }

    private sealed record ReadableExportPlan(
        IReadOnlyDictionary<string, string> FunctionNames,
        IReadOnlyDictionary<string, string> ConfigNames,
        bool UsesTargetResolver,
        bool UsesVectorFactory,
        bool UsesVectorTween,
        bool UsesObbyPlayerState,
        bool UsesObbyTouchResolver,
        bool UsesObbyObjectPosition,
        bool UsesEssentialsRuntime,
        bool UsesTweenTargetRuntime,
        bool UsesInputEventRuntime);

    private static void AppendReadableScript(
        StringBuilder builder,
        Rule rule,
        RuleGraph graph,
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        LuauExportOptions options)
    {
        // Keep the emitted file organized like a human script: user-reserved
        // configuration, context declarations, triggers, reusable
        // condition/action blocks, trigger bootstrap, then graph metadata for
        // round-trip loading.
        var nodesById = rule.Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        var plan = BuildReadableExportPlan(rule, nodesById);
        var reachedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AppendReadableUserConfigurationVariables(builder);
        builder.AppendLine();
        AppendReadableScriptContext(builder, rule, plan);
        builder.AppendLine();

        var triggers = OrderedNodes(rule)
            .Where(node => node.Enabled && node.Kind == NodeKind.Trigger)
            .ToList();
        if (triggers.Count == 0)
        {
            builder.AppendLine(LuauCommentTags.VsrComment("TRIGGER: NO ENABLED TRIGGER"));
            builder.AppendLine("print(\"No enabled Trigger node is configured.\")");
            builder.AppendLine();
        }
        else
        {
            foreach (var trigger in triggers)
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AppendReadableTriggerBlock(builder, rule, trigger, catalog, plan, nodesById, visited, reachedNodeIds);
                builder.AppendLine();
            }
        }

        foreach (var condition in OrderedNodes(rule).Where(node => node.Enabled && node.Kind == NodeKind.Condition))
        {
            AppendReadableConditionBlock(builder, rule, condition, catalog, plan, nodesById);
            builder.AppendLine();
        }

        foreach (var action in OrderedNodes(rule).Where(node => node.Enabled && node.Kind == NodeKind.Action))
        {
            AppendReadableActionBlock(builder, rule, action, catalog, plan, nodesById);
            builder.AppendLine();
        }

        AppendReadableDisconnectedReport(builder, rule, catalog, reachedNodeIds);
        AppendReadableScriptStart(builder, triggers, plan);

        if (options.IncludeGraphMetadata)
        {
            builder.AppendLine();
            builder.AppendLine(LuauCommentTags.VsrComment("GENERATED GRAPH METADATA"));
            AppendGraphMetadata(builder, graph);
        }
    }

}
