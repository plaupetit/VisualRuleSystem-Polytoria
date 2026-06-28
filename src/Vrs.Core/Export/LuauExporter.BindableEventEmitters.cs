using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static void AppendReadableFireBindableEventAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var target = ParameterExpression(rule, action, nodesById, "target", "String", "Self");
        var payload = ParameterExpression(rule, action, nodesById, "payload", "String", "");
        builder.AppendLine($"{indent}local bindableEvent = resolveTarget(triggerObject, {target.Code})");
        builder.AppendLine($"{indent}if bindableEvent == nil or bindableEvent.Invoke == nil then");
        builder.AppendLine($"{indent}    print(\"Fire Bindable Event stopped: target is not a BindableEvent or is missing Invoke.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}bindableEvent:Invoke({payload.Code})");
    }

    private static void AppendReadableBindableEventTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine("    local bindableEvent = triggerObject");
        builder.AppendLine("    if bindableEvent == nil or bindableEvent.Invoked == nil or bindableEvent.Invoked.Connect == nil then");
        builder.AppendLine("        print(\"On Bindable Event trigger stopped: target is not a BindableEvent or has no Invoked signal.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    bindableEvent.Invoked:Connect(function(payload)");
        builder.AppendLine("        local triggerContext = { object = triggerObject, bindableEvent = bindableEvent, payload = payload, message = payload }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
        if (!emitted)
        {
            builder.AppendLine("        print(\"On Bindable Event trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("    end)");
        builder.AppendLine("end");
    }
}
