using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static void AppendWaitSeconds(StringBuilder builder, RuleNode action, IReadOnlyDictionary<string, string> parameterValues, int indentLevel)
    {
        var duration = NumericLiteral(ParameterValue(action, parameterValues, "duration"), "1");
        var waitToComplete = BooleanValue(ParameterValue(action, parameterValues, "waitToComplete"), fallback: true);
        if (waitToComplete)
        {
            builder.AppendLine($"{IndentText(indentLevel)}wait({duration})");
            return;
        }

        builder.AppendLine(LuauCommentTags.IndentedVsrComment(indentLevel, "Wait To Complete is false; continuing immediately."));
    }

    private static void AppendSetObjectColor(StringBuilder builder, RuleNode action, IReadOnlyDictionary<string, string> parameterValues, int indentLevel)
    {
        var targetName = ParameterValue(action, parameterValues, "target");
        if (string.IsNullOrWhiteSpace(targetName))
        {
            targetName = "Self";
        }

        var red = NumericLiteral(ParameterValue(action, parameterValues, "r"), "1");
        var green = NumericLiteral(ParameterValue(action, parameterValues, "g"), "1");
        var blue = NumericLiteral(ParameterValue(action, parameterValues, "b"), "1");
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}local target = vrsResolveTarget(context, {LuauStringLiteral(targetName)})");
        builder.AppendLine($"{indent}if target == nil then");
        builder.AppendLine($"{indent}    vrsLog(\"Set Object Color skipped: target {EscapeForDoubleQuotedString(targetName)} was not found.\")");
        builder.AppendLine($"{indent}else");
        builder.AppendLine($"{indent}    local targetColor = Color.New({red}, {green}, {blue}, 1)");
        builder.AppendLine($"{indent}    if target.Color == nil then");
        builder.AppendLine($"{indent}        vrsLog(\"Set Object Color skipped: target {EscapeForDoubleQuotedString(targetName)} has no Color property.\")");
        builder.AppendLine($"{indent}    else");
        builder.AppendLine($"{indent}        target.Color = targetColor");
        builder.AppendLine($"{indent}    end");
        builder.AppendLine($"{indent}end");
    }
}
