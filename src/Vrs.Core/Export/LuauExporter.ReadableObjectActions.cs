using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Object-oriented readable actions share the same defensive target checks
    // because Polytoria Creator scene objects can expose different properties.
    private static void AppendDebugPrint(StringBuilder builder, RuleNode node, int indentLevel, string message)
    {
        if (node.DebugEnabled)
        {
            builder.AppendLine($"{IndentText(indentLevel)}print(\"{EscapeForDoubleQuotedString(message)}\")");
        }
    }

    private static string TargetExpression(ReadableExportPlan plan, RuleNode node)
    {
        return HasConfigName(plan, node, "target")
            ? $"resolveTarget(triggerObject, {ConfigName(plan, node, "target")})"
            : "triggerObject";
    }

    private static LuauExpression ColorExpression(Rule rule, RuleNode node, IReadOnlyDictionary<string, RuleNode> nodesById)
    {
        var red = ParameterExpression(rule, node, nodesById, "r", "Number", "1");
        var green = ParameterExpression(rule, node, nodesById, "g", "Number", "1");
        var blue = ParameterExpression(rule, node, nodesById, "b", "Number", "1");
        return new LuauExpression($"Color.New({red.Code}, {green.Code}, {blue.Code}, 1)", "Color");
    }

    private static void AppendResolvedTargetVariable(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode node,
        int indentLevel,
        string variableName)
    {
        builder.AppendLine($"{IndentText(indentLevel)}local {variableName} = {TargetExpression(plan, node)}");
    }

    private static void AppendSimpleObjectPropertyAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string parameterKey,
        string dataType,
        string fallback,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        var value = ParameterExpression(rule, action, nodesById, parameterKey, dataType, fallback);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}targetObject.{propertyName} = {value.Code}");
    }

    private static void AppendFixedObjectPropertyAction(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode action,
        int indentLevel,
        string propertyName,
        string valueExpression,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}targetObject.{propertyName} = {valueExpression}");
    }

    private static void AppendToggleObjectBooleanPropertyAction(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode action,
        int indentLevel,
        string propertyName,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}targetObject.{propertyName} = not (targetObject.{propertyName} == true)");
    }

    private static void AppendObjectVisibilityAction(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode action,
        int indentLevel,
        string visibleExpression,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.Visible ~= nil then");
        builder.AppendLine($"{indent}    targetObject.Visible = {visibleExpression}");
        builder.AppendLine($"{indent}elseif targetObject.Transparency ~= nil then");
        builder.AppendLine($"{indent}    targetObject.Transparency = ({visibleExpression}) and 0 or 1");
        builder.AppendLine($"{indent}else");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose Visible or Transparency.\")");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendToggleObjectVisibilityAction(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode action,
        int indentLevel,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.Visible ~= nil then");
        builder.AppendLine($"{indent}    targetObject.Visible = not (targetObject.Visible == true)");
        builder.AppendLine($"{indent}elseif targetObject.Transparency ~= nil then");
        builder.AppendLine($"{indent}    local isVisible = targetObject.Transparency < 1");
        builder.AppendLine($"{indent}    targetObject.Transparency = isVisible and 1 or 0");
        builder.AppendLine($"{indent}else");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose Visible or Transparency.\")");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendVectorPropertyAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string readableName,
        bool replace)
    {
        var indent = IndentText(indentLevel);
        var defaultValue = replace && propertyName == "Scale" ? "1" : "0";
        var vector = ParameterVectorExpression(rule, action, nodesById, "vector", defaultValue, defaultValue, defaultValue);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        var assignment = replace ? vector.Code : $"targetObject.{propertyName} + {vector.Code}";
        builder.AppendLine($"{indent}targetObject.{propertyName} = {assignment}");
    }

    private static void AppendMoveObjectAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var parameterValues = BuildEffectiveParameterValues(rule, action, nodesById);
        var replace = ParameterValue(action, parameterValues, "positionMode").Equals("Set", StringComparison.OrdinalIgnoreCase);
        AppendTransformVectorPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Position", "Move Object", replace);
    }

    private static void AppendMoveObjectToAnotherObjectAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var destination = ParameterExpression(rule, action, nodesById, "destination", "String", "Target");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}local destinationObject = resolveTarget(triggerObject, {destination.Code})");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"Move Object To Another Object stopped: object to move was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if destinationObject == nil then");
        builder.AppendLine($"{indent}    print(\"Move Object To Another Object stopped: move-to object was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.Position == nil or destinationObject.Position == nil then");
        builder.AppendLine($"{indent}    print(\"Move Object To Another Object stopped: an object has no Position.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}targetObject.Position = destinationObject.Position");
    }

    private static void AppendMoveObjectVerticalAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        int direction)
    {
        var indent = IndentText(indentLevel);
        var distance = ParameterExpression(rule, action, nodesById, "distance", "Number", "1");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{EscapeForDoubleQuotedString(action.Label)} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.Position == nil then");
        builder.AppendLine($"{indent}    print(\"{EscapeForDoubleQuotedString(action.Label)} stopped: target does not expose Position.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local moveDistance = math.abs({distance.Code})");
        var amount = direction < 0 ? "-moveDistance" : "moveDistance";
        builder.AppendLine($"{indent}targetObject.Position = targetObject.Position + makeVector3(0, {amount}, 0)");
    }

    private static void AppendSetObjectPositionAxisAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string axisVariableName,
        string parameterKey,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        var newAxisValue = ParameterExpression(rule, action, nodesById, parameterKey, "Number", "0");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.Position == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose Position.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        // Preserve the untouched axes so single-axis position edits do not
        // accidentally reset the object to the world origin.
        builder.AppendLine($"{indent}local currentPosition = targetObject.Position");
        builder.AppendLine($"{indent}local newX = vrsValueAxis(currentPosition, \"X\", \"x\", 0)");
        builder.AppendLine($"{indent}local newY = vrsValueAxis(currentPosition, \"Y\", \"y\", 0)");
        builder.AppendLine($"{indent}local newZ = vrsValueAxis(currentPosition, \"Z\", \"z\", 0)");
        builder.AppendLine($"{indent}{axisVariableName} = {newAxisValue.Code}");
        builder.AppendLine($"{indent}targetObject.Position = makeVector3(newX, newY, newZ)");
    }

    private static void AppendObjectTurnAngleAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        bool addToCurrent)
    {
        var indent = IndentText(indentLevel);
        var angle = ParameterExpression(rule, action, nodesById, "angle", "Number", "0");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{EscapeForDoubleQuotedString(action.Label)} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.Rotation == nil then");
        builder.AppendLine($"{indent}    print(\"{EscapeForDoubleQuotedString(action.Label)} stopped: target does not expose Rotation.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        // Keep the object's other rotation axes so a beginner turn action only
        // changes the left-right facing angle it promises to control.
        builder.AppendLine($"{indent}local currentRotation = targetObject.Rotation");
        builder.AppendLine($"{indent}local newX = vrsValueAxis(currentRotation, \"X\", \"x\", 0)");
        builder.AppendLine($"{indent}local newY = vrsValueAxis(currentRotation, \"Y\", \"y\", 0)");
        builder.AppendLine($"{indent}local newZ = vrsValueAxis(currentRotation, \"Z\", \"z\", 0)");
        builder.AppendLine(addToCurrent
            ? $"{indent}newY = newY + {angle.Code}"
            : $"{indent}newY = {angle.Code}");
        builder.AppendLine($"{indent}targetObject.Rotation = makeVector3(newX, newY, newZ)");
    }

    private static void AppendSetObjectSizeAxisAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string axisVariableName,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        var size = ParameterExpression(rule, action, nodesById, "size", "Number", "1");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.Scale == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose Scale.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        // Keep the untouched size directions so resizing one dimension does
        // not flatten or reset the object in the other directions.
        builder.AppendLine($"{indent}local currentScale = targetObject.Scale");
        builder.AppendLine($"{indent}local newX = vrsValueAxis(currentScale, \"X\", \"x\", 1)");
        builder.AppendLine($"{indent}local newY = vrsValueAxis(currentScale, \"Y\", \"y\", 1)");
        builder.AppendLine($"{indent}local newZ = vrsValueAxis(currentScale, \"Z\", \"z\", 1)");
        builder.AppendLine($"{indent}{axisVariableName} = {size.Code}");
        builder.AppendLine($"{indent}targetObject.Scale = makeVector3(newX, newY, newZ)");
    }

    private static void AppendLegacyMoveObjectOverTimeAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var parameterValues = BuildEffectiveParameterValues(rule, action, nodesById);
        var axis = ParameterValue(action, parameterValues, "axis");
        var moveMode = ParameterValue(action, parameterValues, "moveMode");
        var distance = ParameterExpression(rule, action, nodesById, "distance", "Number", "10");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"Move Object Over Time stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.Position == nil then");
        builder.AppendLine($"{indent}    print(\"Move Object Over Time stopped: target does not expose Position.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");

        if (moveMode.Equals("Constant", StringComparison.OrdinalIgnoreCase))
        {
            var perStep = AxisVectorExpression(axis, $"({distance.Code} * 0.03)");
            builder.AppendLine($"{indent}while true do");
            builder.AppendLine($"{indent}    wait(0.03)");
            builder.AppendLine($"{indent}    targetObject.Position = targetObject.Position + {perStep}");
            builder.AppendLine($"{indent}end");
            return;
        }

        var duration = ParameterExpression(rule, action, nodesById, "duration", "Number", "3");
        var transition = ParameterExpression(rule, action, nodesById, "transition", "String", "Sine");
        var direction = ParameterExpression(rule, action, nodesById, "direction", "String", "InOut");
        var waitToComplete = ParameterExpression(rule, action, nodesById, "waitToComplete", "Boolean", "true");
        var offset = AxisVectorExpression(axis, distance.Code);
        builder.AppendLine($"{indent}local endValue = targetObject.Position + {offset}");
        builder.AppendLine($"{indent}local function applyPositionTween()");
        builder.AppendLine($"{indent}    vrsRunVectorTween(function() return targetObject.Position end, function(value) targetObject.Position = value end, endValue, {duration.Code}, {transition.Code}, {direction.Code})");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if {waitToComplete.Code} then");
        builder.AppendLine($"{indent}    applyPositionTween()");
        builder.AppendLine($"{indent}elseif spawn ~= nil then");
        builder.AppendLine($"{indent}    spawn(applyPositionTween)");
        builder.AppendLine($"{indent}else");
        builder.AppendLine($"{indent}    applyPositionTween()");
        builder.AppendLine($"{indent}end");
    }

    private static string AxisVectorExpression(string axis, string amountExpression)
    {
        return axis.Equals("Y", StringComparison.OrdinalIgnoreCase)
            ? $"makeVector3(0, {amountExpression}, 0)"
            : axis.Equals("Z", StringComparison.OrdinalIgnoreCase)
                ? $"makeVector3(0, 0, {amountExpression})"
                : $"makeVector3({amountExpression}, 0, 0)";
    }

    private static void AppendRotateObjectAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var parameterValues = BuildEffectiveParameterValues(rule, action, nodesById);
        var rotationMode = ParameterValue(action, parameterValues, "rotationMode");
        if (rotationMode.Equals("Spin", StringComparison.OrdinalIgnoreCase))
        {
            AppendSpinRotationAction(builder, rule, action, plan, nodesById, indentLevel, "stepSeconds", "Rotate Object");
            return;
        }

        AppendTransformVectorPropertyAction(
            builder,
            rule,
            action,
            plan,
            nodesById,
            indentLevel,
            "Rotation",
            "Rotate Object",
            replace: rotationMode.Equals("Set", StringComparison.OrdinalIgnoreCase));
    }

    private static void AppendTransformVectorPropertyAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string readableName,
        bool replace)
    {
        var indent = IndentText(indentLevel);
        var scaleDefault = replace && propertyName.Equals("Scale", StringComparison.OrdinalIgnoreCase) ? "1" : "0";
        var vector = ParameterVectorExpression(rule, action, nodesById, "vector", scaleDefault, scaleDefault, scaleDefault);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");

        var endValue = replace ? vector.Code : $"targetObject.{propertyName} + {vector.Code}";
        var parameterValues = BuildEffectiveParameterValues(rule, action, nodesById);
        if (!ParameterValue(action, parameterValues, "motionMode").Equals("Smooth", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"{indent}targetObject.{propertyName} = {endValue}");
            return;
        }

        var duration = ParameterExpression(rule, action, nodesById, "duration", "Number", "1");
        var smoothing = ParameterExpression(rule, action, nodesById, "smoothing", "String", "Sine");
        var direction = ParameterExpression(rule, action, nodesById, "direction", "String", "InOut");
        var waitToComplete = ParameterExpression(rule, action, nodesById, "waitToComplete", "Boolean", "true");
        builder.AppendLine($"{indent}local endValue = {endValue}");
        builder.AppendLine($"{indent}local function apply{propertyName}Tween()");
        builder.AppendLine($"{indent}    vrsRunVectorTween(function() return targetObject.{propertyName} end, function(value) targetObject.{propertyName} = value end, endValue, {duration.Code}, {smoothing.Code}, {direction.Code})");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if {waitToComplete.Code} then");
        builder.AppendLine($"{indent}    apply{propertyName}Tween()");
        builder.AppendLine($"{indent}elseif spawn ~= nil then");
        builder.AppendLine($"{indent}    spawn(apply{propertyName}Tween)");
        builder.AppendLine($"{indent}else");
        builder.AppendLine($"{indent}    apply{propertyName}Tween()");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendSpinRotationAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string intervalKey,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        var rotationStep = ParameterVectorExpression(rule, action, nodesById, "vector", "0", "1", "0");
        var interval = ParameterExpression(rule, action, nodesById, intervalKey, "Number", "0.03");
        builder.AppendLine($"{indent}while true do");
        builder.AppendLine($"{indent}    wait({interval.Code})");
        builder.AppendLine($"{indent}    local targetObject = {TargetExpression(plan, action)}");
        builder.AppendLine($"{indent}    if targetObject == nil then");
        builder.AppendLine($"{indent}        print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}        return");
        builder.AppendLine($"{indent}    end");
        builder.AppendLine($"{indent}    if targetObject.Rotation == nil then");
        builder.AppendLine($"{indent}        print(\"{readableName} stopped: target does not expose Rotation.\")");
        builder.AppendLine($"{indent}        return");
        builder.AppendLine($"{indent}    end");
        builder.AppendLine($"{indent}    targetObject.Rotation = targetObject.Rotation + {rotationStep.Code}");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendLookAtPositionAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var lookPosition = ParameterVectorExpression(rule, action, nodesById, "lookPosition", "0", "0", "0");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}local lookPosition = {lookPosition.Code}");
        AppendLookAtAssignment(builder, indentLevel, "Look At Position");
    }

    private static void AppendLookAtObjectAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var lookTarget = ParameterExpression(rule, action, nodesById, "lookTarget", "String", "Target");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}local lookTargetObject = resolveTarget(triggerObject, {lookTarget.Code})");
        builder.AppendLine($"{indent}if lookTargetObject == nil then");
        builder.AppendLine($"{indent}    print(\"Look At Object stopped: look target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if lookTargetObject.Position == nil then");
        builder.AppendLine($"{indent}    print(\"Look At Object stopped: look target does not expose Position.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local lookPosition = lookTargetObject.Position");
        AppendLookAtAssignment(builder, indentLevel, "Look At Object");
    }

    private static void AppendLookAtAssignment(StringBuilder builder, int indentLevel, string readableName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.Position == nil or targetObject.Rotation == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target needs Position and Rotation.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local lookOffset = lookPosition - targetObject.Position");
        builder.AppendLine($"{indent}local lookX = 0");
        builder.AppendLine($"{indent}if lookOffset.X ~= nil then lookX = lookOffset.X elseif lookOffset.x ~= nil then lookX = lookOffset.x end");
        builder.AppendLine($"{indent}local lookZ = 0");
        builder.AppendLine($"{indent}if lookOffset.Z ~= nil then lookZ = lookOffset.Z elseif lookOffset.z ~= nil then lookZ = lookOffset.z end");
        builder.AppendLine($"{indent}if math.abs(lookX) + math.abs(lookZ) < 0.0001 then");
        builder.AppendLine($"{indent}    print(\"{readableName} skipped: target is already at the look position.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local angleRadians = math.atan2 ~= nil and math.atan2(lookX, lookZ) or math.atan(lookX, lookZ)");
        builder.AppendLine($"{indent}targetObject.Rotation = makeVector3(0, math.deg(angleRadians), 0)");
    }
}
