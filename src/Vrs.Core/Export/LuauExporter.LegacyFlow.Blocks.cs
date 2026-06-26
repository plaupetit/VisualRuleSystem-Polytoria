using System.Text;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Legacy flow blocks wrap trigger/action/condition emission with comments
    // before traversal decides what connected node executes next.
    private static void AppendTriggerBlock(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        LuauExportOptions options,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        int indentLevel,
        int depth)
    {
        reachedNodeIds.Add(trigger.Id);
        AppendNodeBlockStart(builder, trigger, catalog, indentLevel, options);
        AppendTriggerSetup(builder, rule, trigger, catalog, nodesById, options, indentLevel);
        var flowBuilder = new StringBuilder();
        var hasFlow = AppendFlowFromNode(flowBuilder, rule, catalog, options, nodesById, trigger.Id, visited, reachedNodeIds, indentLevel, depth + 1);

        if (options.IncludeComments)
        {
            builder.AppendLine();
            if (hasFlow)
            {
                builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, $"FLOW OUT START: {trigger.Label}"));
            }
            else
            {
                builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, $"FLOW OUT: no connected Action or Condition; {trigger.Label} stops here."));
            }
        }

        builder.Append(flowBuilder);
        if (options.IncludeComments && hasFlow)
        {
            builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, $"FLOW OUT END: {trigger.Label}"));
        }

        AppendNodeBlockEnd(builder, trigger, indentLevel, options);
    }

    private static void AppendActionBlock(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        LuauExportOptions options,
        int indentLevel)
    {
        AppendNodeBlockStart(builder, action, catalog, indentLevel, options);
        AppendActionCode(builder, rule, action, catalog, nodesById, options, indentLevel);
        AppendNodeBlockEnd(builder, action, indentLevel, options);
    }

    private static void AppendConditionBlock(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        LuauExportOptions options,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        int indentLevel,
        int depth)
    {
        AppendNodeBlockStart(builder, condition, catalog, indentLevel, options);
        var predicate = BuildConditionPredicate(rule, condition, nodesById);
        if (!string.IsNullOrWhiteSpace(predicate.VsrComment))
        {
            builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, predicate.VsrComment));
        }

        if (!string.IsNullOrWhiteSpace(predicate.RuntimeLog))
        {
            builder.AppendLine($"{IndentText(indentLevel)}vrsLog({LuauStringLiteral(predicate.RuntimeLog)})");
        }

        var conditionVariable = $"vrsCondition_{SafeIdentifier(condition.Id)}";
        builder.AppendLine($"{IndentText(indentLevel)}local {conditionVariable} = {predicate.Expression}");
        builder.AppendLine();
        builder.AppendLine($"{IndentText(indentLevel)}if {conditionVariable} then");
        AppendConditionBranch(builder, rule, catalog, options, nodesById, condition.Id, GraphPortDefaults.TrueOut, "TRUE", visited, reachedNodeIds, indentLevel + 1, depth + 1);
        builder.AppendLine($"{IndentText(indentLevel)}else");
        AppendConditionBranch(builder, rule, catalog, options, nodesById, condition.Id, GraphPortDefaults.FalseOut, "FALSE", visited, reachedNodeIds, indentLevel + 1, depth + 1);
        builder.AppendLine($"{IndentText(indentLevel)}end");
        AppendNodeBlockEnd(builder, condition, indentLevel, options);
    }
}
