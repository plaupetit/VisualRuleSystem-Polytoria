using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static readonly HashSet<string> CustomUiWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnUIBecameVisible",
        "OnUIBecameHidden",
        "OnUIImageChanged",
        "OnTextInputBecameEmpty",
        "OnTextInputNoLongerEmpty",
        "OnTextInputBecameReadOnly",
        "OnTextInputBecameEditable",
        "OnUIFieldStartedIgnoringMouse",
        "OnUIFieldStoppedIgnoringMouse",
        "OnUIFieldStartedClippingChildren",
        "OnUIFieldStoppedClippingChildren",
        "OnGui3DShadedEnabled",
        "OnGui3DShadedDisabled",
        "OnGui3DFaceCameraEnabled",
        "OnGui3DFaceCameraDisabled",
        "OnGui3DTransparentEnabled",
        "OnGui3DTransparentDisabled",
        "OnGridColumnsReached",
        "OnScrollViewHorizontalModeChanged"
    };

    private static bool IsCustomUiWatcherTrigger(string triggerType)
        => CustomUiWatcherTriggerTypes.Contains(triggerType);

    // Custom UI object nodes use the official UI object properties/events
    // directly. Keep them separate from CoreUI, which controls Polytoria's
    // built-in interface service rather than user-created UI objects.
    private static bool TryAppendReadableCustomUiActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (action.Type.Equals("SetUIText", StringComparison.OrdinalIgnoreCase))
        {
            var text = ParameterExpression(rule, action, nodesById, "text", "String", "");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "labelObject");
            AppendCustomUiPropertyGuard(builder, indentLevel, "labelObject", "Text", "Set UI Text");
            builder.AppendLine($"{indent}labelObject.Text = tostring({text.Code})");
            return true;
        }

        if (action.Type.Equals("SetUIColor", StringComparison.OrdinalIgnoreCase))
        {
            var red = ParameterExpression(rule, action, nodesById, "r", "Number", "1");
            var green = ParameterExpression(rule, action, nodesById, "g", "Number", "1");
            var blue = ParameterExpression(rule, action, nodesById, "b", "Number", "1");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "uiObject");
            AppendCustomUiPropertyGuard(builder, indentLevel, "uiObject", "Color", "Set UI Color");
            builder.AppendLine($"{indent}uiObject.Color = Color.New({red.Code}, {green.Code}, {blue.Code}, 1)");
            return true;
        }

        if (action.Type.Equals("SetUITextWrapped", StringComparison.OrdinalIgnoreCase))
        {
            var enabled = ParameterExpression(rule, action, nodesById, "enabled", "Boolean", "true");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "labelObject");
            AppendCustomUiPropertyGuard(builder, indentLevel, "labelObject", "TextWrapped", "Set UI Text Wrapping");
            builder.AppendLine($"{indent}labelObject.TextWrapped = {enabled.Code}");
            return true;
        }

        if (action.Type.Equals("SetUIVisible", StringComparison.OrdinalIgnoreCase))
        {
            var visible = ParameterExpression(rule, action, nodesById, "visible", "Boolean", "true");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "uiObject");
            AppendCustomUiPropertyGuard(builder, indentLevel, "uiObject", "Visible", "Set UI Visible");
            builder.AppendLine($"{indent}uiObject.Visible = {visible.Code}");
            return true;
        }

        if (action.Type.Equals("SetUIImage", StringComparison.OrdinalIgnoreCase))
        {
            var image = ParameterExpression(rule, action, nodesById, "image", "String", "");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "imageObject");
            AppendCustomUiPropertyGuard(builder, indentLevel, "imageObject", "Image", "Set UI Image");
            builder.AppendLine($"{indent}imageObject.Image = tostring({image.Code})");
            return true;
        }

        if (action.Type.Equals("SetTextInputText", StringComparison.OrdinalIgnoreCase))
        {
            var text = ParameterExpression(rule, action, nodesById, "text", "String", "");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "textInputObject");
            AppendCustomUiPropertyGuard(builder, indentLevel, "textInputObject", "Text", "Set Text Input Text");
            builder.AppendLine($"{indent}textInputObject.Text = tostring({text.Code})");
            return true;
        }

        if (action.Type.Equals("SetTextInputPlaceholder", StringComparison.OrdinalIgnoreCase))
        {
            var placeholder = ParameterExpression(rule, action, nodesById, "placeholder", "String", "");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "textInputObject");
            AppendCustomUiPropertyGuard(builder, indentLevel, "textInputObject", "Placeholder", "Set Text Input Placeholder");
            builder.AppendLine($"{indent}textInputObject.Placeholder = tostring({placeholder.Code})");
            return true;
        }

        if (action.Type.Equals("SetTextInputReadOnly", StringComparison.OrdinalIgnoreCase))
        {
            var readOnly = ParameterExpression(rule, action, nodesById, "readOnly", "Boolean", "true");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "textInputObject");
            AppendCustomUiPropertyGuard(builder, indentLevel, "textInputObject", "ReadOnly", "Set Text Input Read Only");
            builder.AppendLine($"{indent}textInputObject.ReadOnly = {readOnly.Code}");
            return true;
        }

        if (action.Type.Equals("FocusTextInput", StringComparison.OrdinalIgnoreCase))
        {
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "textInputObject");
            builder.AppendLine($"{indent}if textInputObject == nil then");
            builder.AppendLine($"{indent}    print(\"Focus Text Input stopped: target text input was not found.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if textInputObject.Focus == nil then");
            builder.AppendLine($"{indent}    print(\"Focus Text Input stopped: target text input does not expose Focus.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}textInputObject:Focus()");
            return true;
        }

        if (action.Type.Equals("SetUIFieldZIndex", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiSimplePropertyAction(builder, rule, action, plan, nodesById, indentLevel, "ZIndex", "zIndex", "Number", "1", "Set UI Layer");
            return true;
        }

        if (action.Type.Equals("SetUIFieldIgnoresMouse", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiSimplePropertyAction(builder, rule, action, plan, nodesById, indentLevel, "IgnoreMouse", "ignoreMouse", "Boolean", "true", "Set UI Ignores Mouse");
            return true;
        }

        if (action.Type.Equals("SetUIFieldClipDescendants", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiSimplePropertyAction(builder, rule, action, plan, nodesById, indentLevel, "ClipDescendants", "clipDescendants", "Boolean", "true", "Set UI Clips Children");
            return true;
        }

        if (action.Type.Equals("SetUIFieldRotation", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiSimplePropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Rotation", "rotation", "Number", "0", "Set UI Rotation");
            return true;
        }

        if (action.Type.Equals("SetUIFieldScale", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiSimplePropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Scale", "scale", "Number", "1", "Set UI Scale");
            return true;
        }

        if (action.Type.Equals("SetScrollViewMode", StringComparison.OrdinalIgnoreCase))
        {
            var axis = ParameterExpression(rule, action, nodesById, "axis", "String", "Both");
            var mode = ParameterExpression(rule, action, nodesById, "mode", "String", "Auto");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "scrollViewObject");
            builder.AppendLine($"{indent}if scrollViewObject == nil then");
            builder.AppendLine($"{indent}    print(\"Set Scroll View Mode stopped: target scroll view was not found.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local scrollAxis = tostring({axis.Code})");
            builder.AppendLine($"{indent}local scrollMode = tostring({mode.Code})");
            builder.AppendLine($"{indent}if scrollAxis == \"Both\" or scrollAxis == \"Horizontal\" then");
            builder.AppendLine($"{indent}    if scrollViewObject.HorizontalScrollMode == nil then");
            builder.AppendLine($"{indent}        print(\"Set Scroll View Mode stopped: target scroll view does not expose HorizontalScrollMode.\")");
            builder.AppendLine($"{indent}        return");
            builder.AppendLine($"{indent}    end");
            builder.AppendLine($"{indent}    scrollViewObject.HorizontalScrollMode = scrollMode");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if scrollAxis == \"Both\" or scrollAxis == \"Vertical\" then");
            builder.AppendLine($"{indent}    if scrollViewObject.VerticalScrollMode == nil then");
            builder.AppendLine($"{indent}        print(\"Set Scroll View Mode stopped: target scroll view does not expose VerticalScrollMode.\")");
            builder.AppendLine($"{indent}        return");
            builder.AppendLine($"{indent}    end");
            builder.AppendLine($"{indent}    scrollViewObject.VerticalScrollMode = scrollMode");
            builder.AppendLine($"{indent}end");
            return true;
        }

        if (action.Type.Equals("SetGridLayoutColumns", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiSimplePropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Columns", "columns", "Number", "1", "Set Grid Columns", "layoutObject");
            return true;
        }

        if (action.Type.Equals("CreateUIContainer", StringComparison.OrdinalIgnoreCase))
        {
            var uiType = SanitizedUiContainerType(action, BuildEffectiveParameterValues(rule, action, nodesById), "UIHLayout");
            var parent = ParameterExpression(rule, action, nodesById, "target", "Any", "PlayerGUI");
            var objectName = ParameterExpression(rule, action, nodesById, "objectName", "String", "");
            builder.AppendLine($"{indent}local requestedUiParent = {parent.Code}");
            builder.AppendLine($"{indent}local parentUiObject = nil");
            builder.AppendLine($"{indent}if type(requestedUiParent) == \"string\" then");
            builder.AppendLine($"{indent}    if requestedUiParent == \"\" or requestedUiParent == \"PlayerGUI\" then");
            builder.AppendLine($"{indent}        parentUiObject = PlayerGUI");
            builder.AppendLine($"{indent}    else");
            builder.AppendLine($"{indent}        parentUiObject = resolveTarget(triggerObject, requestedUiParent)");
            builder.AppendLine($"{indent}    end");
            builder.AppendLine($"{indent}else");
            builder.AppendLine($"{indent}    parentUiObject = requestedUiParent");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if parentUiObject == nil then");
            builder.AppendLine($"{indent}    print(\"Create UI Container stopped: parent UI was not found.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if Instance == nil or Instance.New == nil then");
            builder.AppendLine($"{indent}    print(\"Create UI Container stopped: object creation is not available.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local createdUiObject = Instance.New(\"{uiType}\", parentUiObject)");
            builder.AppendLine($"{indent}if createdUiObject == nil then");
            builder.AppendLine($"{indent}    print(\"Create UI Container stopped: new UI object could not be created.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local createdUiObjectName = tostring({objectName.Code})");
            builder.AppendLine($"{indent}if createdUiObjectName ~= \"\" and createdUiObject.Name ~= nil then");
            builder.AppendLine($"{indent}    createdUiObject.Name = createdUiObjectName");
            builder.AppendLine($"{indent}end");
            return true;
        }

        if (action.Type.Equals("SetGridLayoutSpacing", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiSimplePropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Spacing", "spacing", "Number", "0", "Set Grid Spacing", "layoutObject");
            return true;
        }

        if (action.Type.Equals("SetLayoutSpacing", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiSimplePropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Spacing", "spacing", "Number", "0", "Set Layout Spacing", "layoutObject");
            return true;
        }

        if (action.Type.Equals("SetLayoutChildAlignment", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiSimplePropertyAction(builder, rule, action, plan, nodesById, indentLevel, "ChildAlignment", "alignment", "String", "Center", "Set Layout Child Alignment", "layoutObject", wrapStringValue: true);
            return true;
        }

        if (action.Type.Equals("SetGui3DShaded", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiSimplePropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Shaded", "shaded", "Boolean", "true", "Set 3D UI Shaded", "gui3DObject");
            return true;
        }

        if (action.Type.Equals("SetGui3DFaceCamera", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiSimplePropertyAction(builder, rule, action, plan, nodesById, indentLevel, "FaceCamera", "faceCamera", "Boolean", "true", "Set 3D UI Face Camera", "gui3DObject");
            return true;
        }

        if (action.Type.Equals("SetGui3DTransparent", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiSimplePropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Transparent", "transparent", "Boolean", "true", "Set 3D UI Transparent", "gui3DObject");
            return true;
        }

        return false;
    }

    private static bool TryAppendReadableCustomUiConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        if (condition.Type.Equals("UITextIs", StringComparison.OrdinalIgnoreCase))
        {
            var expectedText = ParameterExpression(rule, condition, nodesById, "text", "String", "");
            var caseSensitive = ParameterExpression(rule, condition, nodesById, "caseSensitive", "Boolean", "false");
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil or targetObject.Text == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local currentText = tostring(targetObject.Text or \"\")");
            builder.AppendLine($"{indent}local expectedText = tostring({expectedText.Code})");
            builder.AppendLine($"{indent}if {caseSensitive.Code} then");
            builder.AppendLine($"{indent}    return currentText == expectedText");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return string.lower(currentText) == string.lower(expectedText)");
            return true;
        }

        if (condition.Type.Equals("UITextIsEmpty", StringComparison.OrdinalIgnoreCase))
        {
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return targetObject.Text == nil or tostring(targetObject.Text) == \"\"");
            return true;
        }

        if (condition.Type.Equals("UITextWrapped", StringComparison.OrdinalIgnoreCase))
        {
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return targetObject.TextWrapped == true");
            return true;
        }

        if (condition.Type.Equals("UIVisible", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiBooleanPropertyCondition(builder, plan, condition, indentLevel, "Visible", true);
            return true;
        }

        if (condition.Type.Equals("UIHidden", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiBooleanPropertyCondition(builder, plan, condition, indentLevel, "Visible", false);
            return true;
        }

        if (condition.Type.Equals("UIImageIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiStringPropertyEqualsCondition(builder, rule, condition, plan, nodesById, indentLevel, "Image", "image", "String", "", "caseSensitive", "false");
            return true;
        }

        if (condition.Type.Equals("UIImageHasImage", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiStringPropertyNotEmptyCondition(builder, plan, condition, indentLevel, "Image");
            return true;
        }

        if (condition.Type.Equals("TextInputTextIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiStringPropertyEqualsCondition(builder, rule, condition, plan, nodesById, indentLevel, "Text", "text", "String", "", "caseSensitive", "false");
            return true;
        }

        if (condition.Type.Equals("TextInputIsEmpty", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiStringPropertyEmptyCondition(builder, plan, condition, indentLevel, "Text");
            return true;
        }

        if (condition.Type.Equals("TextInputReadOnly", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiBooleanPropertyCondition(builder, plan, condition, indentLevel, "ReadOnly", true);
            return true;
        }

        if (condition.Type.Equals("TextInputEditable", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiBooleanPropertyCondition(builder, plan, condition, indentLevel, "ReadOnly", false);
            return true;
        }

        if (condition.Type.Equals("UIFieldIgnoresMouse", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiBooleanPropertyCondition(builder, plan, condition, indentLevel, "IgnoreMouse", true);
            return true;
        }

        if (condition.Type.Equals("UIFieldClipsChildren", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiBooleanPropertyCondition(builder, plan, condition, indentLevel, "ClipDescendants", true);
            return true;
        }

        if (condition.Type.Equals("Gui3DShaded", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiBooleanPropertyCondition(builder, plan, condition, indentLevel, "Shaded", true);
            return true;
        }

        if (condition.Type.Equals("Gui3DFacesCamera", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiBooleanPropertyCondition(builder, plan, condition, indentLevel, "FaceCamera", true);
            return true;
        }

        if (condition.Type.Equals("Gui3DTransparent", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiBooleanPropertyCondition(builder, plan, condition, indentLevel, "Transparent", true);
            return true;
        }

        if (condition.Type.Equals("GridColumnsAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "Columns", "columns", ">=", "1");
            return true;
        }

        if (condition.Type.Equals("ScrollViewHorizontalModeIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendCustomUiStringPropertyEqualsCondition(builder, rule, condition, plan, nodesById, indentLevel, "HorizontalScrollMode", "mode", "String", "Auto", "caseSensitive", "true");
            return true;
        }

        return false;
    }

    private static LuauExpression? TryResolveReadableCustomUiPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "UITextValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Text", "String", "UI Text", "return tostring(targetObject.Text or \"\")"),
            "UIColorValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Color", "Color", "UI Color", "return targetObject.Color"),
            "UIFontSizeValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "FontSize", "Number", "UI Font Size", "return tonumber(targetObject.FontSize) or 0"),
            "UITextWrappedValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "TextWrapped", "Boolean", "UI Text Wraps", "return targetObject.TextWrapped == true"),
            "UIVisibleValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Visible", "Boolean", "UI Visible", "return targetObject.Visible == true"),
            "UIImageValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Image", "String", "UI Image", "return tostring(targetObject.Image or \"\")"),
            "TextInputTextValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Text", "String", "Text Input Text", "return tostring(targetObject.Text or \"\")"),
            "TextInputPlaceholderValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Placeholder", "String", "Text Input Placeholder", "return tostring(targetObject.Placeholder or \"\")"),
            "TextInputReadOnlyValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "ReadOnly", "Boolean", "Text Input Read Only", "return targetObject.ReadOnly == true"),
            "UIFieldZIndexValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "ZIndex", "Number", "UI Layer", "return tonumber(targetObject.ZIndex) or 0"),
            "UIFieldIgnoresMouseValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "IgnoreMouse", "Boolean", "UI Ignores Mouse", "return targetObject.IgnoreMouse == true"),
            "UIFieldClipDescendantsValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "ClipDescendants", "Boolean", "UI Clips Children", "return targetObject.ClipDescendants == true"),
            "UIFieldRotationValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Rotation", "Number", "UI Rotation", "return tonumber(targetObject.Rotation) or 0"),
            "UIFieldScaleValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Scale", "Number", "UI Scale", "return tonumber(targetObject.Scale) or 1"),
            "ScrollViewHorizontalModeValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "HorizontalScrollMode", "String", "Horizontal Scroll Mode", "return tostring(targetObject.HorizontalScrollMode or \"\")"),
            "ScrollViewVerticalModeValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "VerticalScrollMode", "String", "Vertical Scroll Mode", "return tostring(targetObject.VerticalScrollMode or \"\")"),
            "GridLayoutColumnsValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Columns", "Number", "Grid Columns", "return tonumber(targetObject.Columns) or 0"),
            "GridLayoutSpacingValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Spacing", "Number", "Grid Spacing", "return tonumber(targetObject.Spacing) or 0"),
            "LayoutSpacingValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Spacing", "Number", "Layout Spacing", "return tonumber(targetObject.Spacing) or 0"),
            "LayoutChildAlignmentValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "ChildAlignment", "String", "Layout Child Alignment", "return tostring(targetObject.ChildAlignment or \"\")"),
            "Gui3DShadedValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Shaded", "Boolean", "3D UI Shaded", "return targetObject.Shaded == true"),
            "Gui3DFaceCameraValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "FaceCamera", "Boolean", "3D UI Faces Camera", "return targetObject.FaceCamera == true"),
            "Gui3DTransparentValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Transparent", "Boolean", "3D UI Transparent", "return targetObject.Transparent == true"),
            "PlayerUIRoot" => new LuauExpression("PlayerGUI", "SceneObject"),
            _ => null
        };
    }

    private static void AppendReadableUiButtonClickedTrigger(
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
        builder.AppendLine("    if triggerObject.Clicked == nil or triggerObject.Clicked.Connect == nil then");
        builder.AppendLine("        print(\"On UI Button Clicked trigger stopped: target button has no Clicked event.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    triggerObject.Clicked:Connect(function()");
        builder.AppendLine("        local triggerContext = { object = triggerObject, uiButton = triggerObject }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
        if (!emitted)
        {
            builder.AppendLine("        print(\"On UI Button Clicked trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("    end)");
        builder.AppendLine("end");
    }

    private static void AppendReadableTextInputEventTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName,
        string eventName,
        string readableName)
    {
        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    if triggerObject.{eventName} == nil or triggerObject.{eventName}.Connect == nil then");
        builder.AppendLine($"        print(\"{readableName} trigger stopped: target text input has no {eventName} event.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine($"    triggerObject.{eventName}:Connect(function(value)");
        builder.AppendLine("        local inputText = value");
        builder.AppendLine("        if inputText == nil and triggerObject.Text ~= nil then");
        builder.AppendLine("            inputText = triggerObject.Text");
        builder.AppendLine("        end");
        builder.AppendLine("        local triggerContext = { object = triggerObject, uiTextInput = triggerObject, text = tostring(inputText or \"\") }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
        if (!emitted)
        {
            builder.AppendLine($"        print(\"{readableName} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("    end)");
        builder.AppendLine("end");
    }

    private static void AppendReadableCustomUiWatcherTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var definition = CustomUiWatcherDefinition.For(trigger.Type);
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");
        var threshold = definition.ParameterKey is null
            ? null
            : ParameterExpression(rule, trigger, nodesById, definition.ParameterKey, "Number", definition.FallbackLimit);

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        if (threshold is not null)
        {
            builder.AppendLine($"    local watchedLimit = tonumber({threshold.Code}) or {definition.FallbackLimit}");
        }

        builder.AppendLine("    local function readWatchedValue()");
        builder.AppendLine("        if triggerObject == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        builder.AppendLine($"        if triggerObject.{definition.PropertyName} == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        switch (definition.ValueKind)
        {
            case CustomUiWatcherValueKind.Boolean:
                builder.AppendLine($"        return triggerObject.{definition.PropertyName} == true");
                break;
            case CustomUiWatcherValueKind.Number:
                builder.AppendLine($"        return tonumber(triggerObject.{definition.PropertyName}) or {definition.FallbackCurrent}");
                break;
            default:
                builder.AppendLine($"        return tostring(triggerObject.{definition.PropertyName} or \"\")");
                break;
        }

        builder.AppendLine("    end");

        var contextFields = $", {definition.ContextField} = currentValue";
        if (definition.TriggersOnAnyChange)
        {
            AppendReadableAnyChangeLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, contextFields, 1);
            builder.AppendLine("end");
            return;
        }

        builder.AppendLine("    local function readMatched()");
        builder.AppendLine("        local currentValue = readWatchedValue()");
        builder.AppendLine("        if currentValue == nil then");
        builder.AppendLine("            return false, nil");
        builder.AppendLine("        end");
        builder.AppendLine($"        return {definition.MatchExpression}, currentValue");
        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, contextFields, 1);
        builder.AppendLine("end");
    }

    private static void AppendCustomUiPropertyGuard(
        StringBuilder builder,
        int indentLevel,
        string variableName,
        string propertyName,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if {variableName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target UI object was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if {variableName}.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target UI object does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendCustomUiSimplePropertyAction(
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
        string readableName,
        string variableName = "uiFieldObject",
        bool wrapStringValue = false)
    {
        var indent = IndentText(indentLevel);
        var value = ParameterExpression(rule, action, nodesById, parameterKey, dataType, fallback);
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, variableName);
        AppendCustomUiPropertyGuard(builder, indentLevel, variableName, propertyName, readableName);
        var assignment = wrapStringValue ? $"tostring({value.Code})" : value.Code;
        builder.AppendLine($"{indent}{variableName}.{propertyName} = {assignment}");
    }

    private static void AppendCustomUiBooleanPropertyCondition(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode condition,
        int indentLevel,
        string propertyName,
        bool expected)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return (targetObject.{propertyName} == true) == {expected.ToString().ToLowerInvariant()}");
    }

    private static void AppendCustomUiStringPropertyEqualsCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string parameterKey,
        string dataType,
        string fallback,
        string caseSensitiveKey,
        string caseSensitiveFallback)
    {
        var indent = IndentText(indentLevel);
        var expectedText = ParameterExpression(rule, condition, nodesById, parameterKey, dataType, fallback);
        var caseSensitive = ParameterExpression(rule, condition, nodesById, caseSensitiveKey, "Boolean", caseSensitiveFallback);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local currentText = tostring(targetObject.{propertyName} or \"\")");
        builder.AppendLine($"{indent}local expectedText = tostring({expectedText.Code})");
        builder.AppendLine($"{indent}if {caseSensitive.Code} then");
        builder.AppendLine($"{indent}    return currentText == expectedText");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return string.lower(currentText) == string.lower(expectedText)");
    }

    private static void AppendCustomUiStringPropertyNotEmptyCondition(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode condition,
        int indentLevel,
        string propertyName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return tostring(targetObject.{propertyName} or \"\") ~= \"\"");
    }

    private static void AppendCustomUiStringPropertyEmptyCondition(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode condition,
        int indentLevel,
        string propertyName)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return tostring(targetObject.{propertyName} or \"\") == \"\"");
    }

    private static void AppendCustomUiNumberPropertyCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string parameterKey,
        string operatorText,
        string fallback)
    {
        var indent = IndentText(indentLevel);
        var threshold = ParameterExpression(rule, condition, nodesById, parameterKey, "Number", fallback);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local currentNumber = tonumber(targetObject.{propertyName}) or 0");
        builder.AppendLine($"{indent}local expectedNumber = tonumber({threshold.Code}) or 0");
        builder.AppendLine($"{indent}return currentNumber {operatorText} expectedNumber");
    }

    private static string SanitizedUiContainerType(
        RuleNode node,
        IReadOnlyDictionary<string, string> parameterValues,
        string fallback)
    {
        return ParameterValue(node, parameterValues, "uiKind").Trim() switch
        {
            "Horizontal Layout" => "UIHLayout",
            "Vertical Layout" => "UIVLayout",
            "Horizontal Flow" => "UIHFlow",
            "Vertical Flow" => "UIVFlow",
            "Viewport" => "UIViewport",
            "UIHLayout" => "UIHLayout",
            "UIVLayout" => "UIVLayout",
            "UIHFlow" => "UIHFlow",
            "UIVFlow" => "UIVFlow",
            "UIViewport" => "UIViewport",
            _ => fallback
        };
    }

    private enum CustomUiWatcherValueKind
    {
        Boolean,
        String,
        Number
    }

    private sealed record CustomUiWatcherDefinition(
        string PropertyName,
        CustomUiWatcherValueKind ValueKind,
        string ContextField,
        string MatchExpression,
        bool TriggersOnAnyChange = false,
        string? ParameterKey = null,
        string FallbackLimit = "0",
        string FallbackCurrent = "0")
    {
        public static CustomUiWatcherDefinition For(string triggerType)
        {
            return triggerType switch
            {
                "OnUIBecameVisible" => Boolean("Visible", true, "uiVisible"),
                "OnUIBecameHidden" => Boolean("Visible", false, "uiVisible"),
                "OnUIImageChanged" => ChangedString("Image", "uiImage"),
                "OnTextInputBecameEmpty" => StringMatch("Text", "currentValue == \"\"", "text"),
                "OnTextInputNoLongerEmpty" => StringMatch("Text", "currentValue ~= \"\"", "text"),
                "OnTextInputBecameReadOnly" => Boolean("ReadOnly", true, "textInputReadOnly"),
                "OnTextInputBecameEditable" => Boolean("ReadOnly", false, "textInputReadOnly"),
                "OnUIFieldStartedIgnoringMouse" => Boolean("IgnoreMouse", true, "uiIgnoresMouse"),
                "OnUIFieldStoppedIgnoringMouse" => Boolean("IgnoreMouse", false, "uiIgnoresMouse"),
                "OnUIFieldStartedClippingChildren" => Boolean("ClipDescendants", true, "uiClipsChildren"),
                "OnUIFieldStoppedClippingChildren" => Boolean("ClipDescendants", false, "uiClipsChildren"),
                "OnGui3DShadedEnabled" => Boolean("Shaded", true, "gui3DShaded"),
                "OnGui3DShadedDisabled" => Boolean("Shaded", false, "gui3DShaded"),
                "OnGui3DFaceCameraEnabled" => Boolean("FaceCamera", true, "gui3DFacesCamera"),
                "OnGui3DFaceCameraDisabled" => Boolean("FaceCamera", false, "gui3DFacesCamera"),
                "OnGui3DTransparentEnabled" => Boolean("Transparent", true, "gui3DTransparent"),
                "OnGui3DTransparentDisabled" => Boolean("Transparent", false, "gui3DTransparent"),
                "OnGridColumnsReached" => NumberThreshold("Columns", "columns", "2", "0", ">=", "gridColumns"),
                "OnScrollViewHorizontalModeChanged" => ChangedString("HorizontalScrollMode", "scrollMode"),
                _ => throw new ArgumentOutOfRangeException(nameof(triggerType), triggerType, "Unknown custom UI watcher trigger.")
            };
        }

        private static CustomUiWatcherDefinition Boolean(string propertyName, bool expected, string contextField)
        {
            return new CustomUiWatcherDefinition(
                propertyName,
                CustomUiWatcherValueKind.Boolean,
                contextField,
                expected ? "currentValue == true" : "currentValue == false");
        }

        private static CustomUiWatcherDefinition StringMatch(string propertyName, string matchExpression, string contextField)
        {
            return new CustomUiWatcherDefinition(propertyName, CustomUiWatcherValueKind.String, contextField, matchExpression);
        }

        private static CustomUiWatcherDefinition ChangedString(string propertyName, string contextField)
        {
            return new CustomUiWatcherDefinition(propertyName, CustomUiWatcherValueKind.String, contextField, "false", TriggersOnAnyChange: true);
        }

        private static CustomUiWatcherDefinition NumberThreshold(
            string propertyName,
            string parameterKey,
            string fallbackLimit,
            string fallbackCurrent,
            string comparison,
            string contextField)
        {
            return new CustomUiWatcherDefinition(
                propertyName,
                CustomUiWatcherValueKind.Number,
                contextField,
                $"currentValue {comparison} watchedLimit",
                ParameterKey: parameterKey,
                FallbackLimit: fallbackLimit,
                FallbackCurrent: fallbackCurrent);
        }
    }
}
