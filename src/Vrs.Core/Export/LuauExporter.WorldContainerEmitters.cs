using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static readonly HashSet<string> SceneContainerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Folder",
        "Model"
    };

    private static bool TryAppendReadableWorldContainerActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (!action.Type.Equals("CreateSceneContainer", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indent = IndentText(indentLevel);
        var containerType = SanitizedSceneContainerType(action, BuildEffectiveParameterValues(rule, action, nodesById), "Folder");
        var parent = ParameterExpression(rule, action, nodesById, "target", "Any", "World");
        var objectName = ParameterExpression(rule, action, nodesById, "objectName", "String", "");
        builder.AppendLine($"{indent}local requestedParent = {parent.Code}");
        builder.AppendLine($"{indent}local parentObject = nil");
        builder.AppendLine($"{indent}if type(requestedParent) == \"string\" then");
        builder.AppendLine($"{indent}    if requestedParent == \"\" or requestedParent == \"World\" then");
        builder.AppendLine($"{indent}        parentObject = World");
        builder.AppendLine($"{indent}    else");
        builder.AppendLine($"{indent}        parentObject = resolveTarget(triggerObject, requestedParent)");
        builder.AppendLine($"{indent}    end");
        builder.AppendLine($"{indent}else");
        builder.AppendLine($"{indent}    parentObject = requestedParent");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if parentObject == nil then");
        builder.AppendLine($"{indent}    print(\"Create Scene Container stopped: parent object was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if Instance == nil or Instance.New == nil then");
        builder.AppendLine($"{indent}    print(\"Create Scene Container stopped: object creation is not available.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local createdContainerObject = Instance.New(\"{containerType}\", parentObject)");
        builder.AppendLine($"{indent}if createdContainerObject == nil then");
        builder.AppendLine($"{indent}    print(\"Create Scene Container stopped: new object could not be created.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local createdContainerName = tostring({objectName.Code})");
        builder.AppendLine($"{indent}if createdContainerName ~= \"\" and createdContainerObject.Name ~= nil then");
        builder.AppendLine($"{indent}    createdContainerObject.Name = createdContainerName");
        builder.AppendLine($"{indent}end");
        return true;
    }

    private static string SanitizedSceneContainerType(
        RuleNode node,
        IReadOnlyDictionary<string, string> parameterValues,
        string fallback)
    {
        var requested = ParameterValue(node, parameterValues, "containerKind").Trim();
        return SceneContainerTypes.Contains(requested) ? requested : fallback;
    }
}
