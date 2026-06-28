using System.Text;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Readable action and condition function blocks emitted after trigger definitions.
    private static void AppendReadableConditionBlock(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById)
    {
        builder.AppendLine(LuauCommentTags.VsrComment($"CONDITION: {HumanBlockName(condition.Label)}"));
        if (AppendReadableNodeLocalVariables(builder, rule, condition, nodesById, plan))
        {
            builder.AppendLine();
        }

        AppendReadableNodeSummary(builder, condition, catalog);
        builder.AppendLine($"{RegistryFunctionReference(plan, condition)} = function(triggerObject, triggerContext)");
        AppendReadableConditionBody(builder, rule, condition, plan, nodesById, 1);
        builder.AppendLine("end");
    }

    private static void AppendReadableConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var parameterValues = BuildEffectiveParameterValues(rule, condition, nodesById);
        if (condition.Type.Equals("RunLuauCondition", StringComparison.OrdinalIgnoreCase))
        {
            AppendRunLuauConditionBody(builder, condition, indentLevel);
            return;
        }

        if (condition.Type.Equals("NumberCompare", StringComparison.OrdinalIgnoreCase))
        {
            var rawOperator = ParameterValue(condition, parameterValues, "operator").Trim();
            var allowedOperators = new HashSet<string>(StringComparer.Ordinal) { "==", "~=", "<", "<=", ">", ">=" };
            if (!allowedOperators.Contains(rawOperator))
            {
                builder.AppendLine($"{indent}print(\"Condition {EscapeForDoubleQuotedString(condition.Label)} has invalid operator {EscapeForDoubleQuotedString(rawOperator)}; returning false.\")");
                builder.AppendLine($"{indent}return false");
                return;
            }

            var left = ParameterExpression(rule, condition, nodesById, "left", "Number", "0");
            var right = ParameterExpression(rule, condition, nodesById, "right", "Number", "0");
            builder.AppendLine($"{indent}return {left.Code} {rawOperator} {right.Code}");
            return;
        }

        if (condition.Type.Equals("ValueEquals", StringComparison.OrdinalIgnoreCase))
        {
            var left = ParameterExpression(rule, condition, nodesById, "left", "Any", "");
            var right = ParameterExpression(rule, condition, nodesById, "right", "Any", "");
            builder.AppendLine($"{indent}return tostring({left.Code}) == tostring({right.Code})");
            return;
        }

        if (condition.Type.Equals("ObjectExists", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"{indent}return {TargetExpression(plan, condition)} ~= nil");
            return;
        }

        if (condition.Type.Equals("BooleanCheck", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, condition, nodesById, "value", "Boolean", "false");
            var expected = ParameterExpression(rule, condition, nodesById, "expected", "Boolean", "true");
            builder.AppendLine($"{indent}return ({value.Code}) == ({expected.Code})");
            return;
        }

        if (condition.Type.Equals("NumberInRange", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, condition, nodesById, "value", "Number", "0");
            var min = ParameterExpression(rule, condition, nodesById, "min", "Number", "0");
            var max = ParameterExpression(rule, condition, nodesById, "max", "Number", "1");
            builder.AppendLine($"{indent}return {value.Code} >= {min.Code} and {value.Code} <= {max.Code}");
            return;
        }

        if (condition.Type.Equals("TextContains", StringComparison.OrdinalIgnoreCase))
        {
            var text = ParameterExpression(rule, condition, nodesById, "text", "String", "");
            var search = ParameterExpression(rule, condition, nodesById, "search", "String", "");
            var caseSensitive = ParameterExpression(rule, condition, nodesById, "caseSensitive", "Boolean", "false");
            builder.AppendLine($"{indent}if {caseSensitive.Code} then");
            builder.AppendLine($"{indent}    return string.find(tostring({text.Code}), tostring({search.Code}), 1, true) ~= nil");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return string.find(string.lower(tostring({text.Code})), string.lower(tostring({search.Code})), 1, true) ~= nil");
            return;
        }

        if (condition.Type.Equals("TextEquals", StringComparison.OrdinalIgnoreCase))
        {
            var left = ParameterExpression(rule, condition, nodesById, "left", "String", "");
            var right = ParameterExpression(rule, condition, nodesById, "right", "String", "");
            var caseSensitive = ParameterExpression(rule, condition, nodesById, "caseSensitive", "Boolean", "false");
            builder.AppendLine($"{indent}local leftValue = tostring({left.Code})");
            builder.AppendLine($"{indent}local rightValue = tostring({right.Code})");
            builder.AppendLine($"{indent}if not ({caseSensitive.Code}) then");
            builder.AppendLine($"{indent}    leftValue = string.lower(leftValue)");
            builder.AppendLine($"{indent}    rightValue = string.lower(rightValue)");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return leftValue == rightValue");
            return;
        }

        if (condition.Type.Equals("TextIsEmpty", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("TextIsNotEmpty", StringComparison.OrdinalIgnoreCase))
        {
            var text = ParameterExpression(rule, condition, nodesById, "text", "String", "");
            var ignoreSpaces = ParameterExpression(rule, condition, nodesById, "ignoreSpaces", "Boolean", "true");
            var comparison = condition.Type.Equals("TextIsEmpty", StringComparison.OrdinalIgnoreCase) ? "==" : "~=";
            builder.AppendLine($"{indent}local textValue = tostring({text.Code})");
            builder.AppendLine($"{indent}if {ignoreSpaces.Code} then");
            builder.AppendLine($"{indent}    textValue = string.gsub(textValue, \"^%s*(.-)%s*$\", \"%1\")");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return textValue {comparison} \"\"");
            return;
        }

        if (condition.Type.Equals("TextIsANumber", StringComparison.OrdinalIgnoreCase))
        {
            var text = ParameterExpression(rule, condition, nodesById, "text", "String", "");
            builder.AppendLine($"{indent}return tonumber(tostring({text.Code})) ~= nil");
            return;
        }

        if (condition.Type.Equals("TextHasAtLeastCharacters", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("TextHasAtMostCharacters", StringComparison.OrdinalIgnoreCase))
        {
            var text = ParameterExpression(rule, condition, nodesById, "text", "String", "");
            var count = ParameterExpression(rule, condition, nodesById, "count", "Number", "0");
            var comparison = condition.Type.Equals("TextHasAtLeastCharacters", StringComparison.OrdinalIgnoreCase) ? ">=" : "<=";
            builder.AppendLine($"{indent}local textValue = tostring({text.Code})");
            builder.AppendLine($"{indent}local characterLimit = math.max(0, math.floor({count.Code}))");
            builder.AppendLine($"{indent}return string.len(textValue) {comparison} characterLimit");
            return;
        }

        if (condition.Type.Equals("ValueIsEmpty", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, condition, nodesById, "value", "Any", "");
            builder.AppendLine($"{indent}local value = {value.Code}");
            builder.AppendLine($"{indent}return value == nil or tostring(value) == \"\"");
            return;
        }

        if (condition.Type.Equals("NumberIsEven", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, condition, nodesById, "value", "Number", "0");
            builder.AppendLine($"{indent}local value = {value.Code}");
            builder.AppendLine($"{indent}return math.floor(value) == value and value % 2 == 0");
            return;
        }

        if (condition.Type.Equals("NumberIsOdd", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, condition, nodesById, "value", "Number", "0");
            builder.AppendLine($"{indent}local value = {value.Code}");
            builder.AppendLine($"{indent}return math.floor(value) == value and math.abs(value % 2) == 1");
            return;
        }

        if (condition.Type.Equals("NumberIsPositive", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, condition, nodesById, "value", "Number", "0");
            var includeZero = ParameterExpression(rule, condition, nodesById, "includeZero", "Boolean", "true");
            builder.AppendLine($"{indent}if {includeZero.Code} then");
            builder.AppendLine($"{indent}    return {value.Code} >= 0");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return {value.Code} > 0");
            return;
        }

        if (condition.Type.Equals("NumberIsNegative", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, condition, nodesById, "value", "Number", "0");
            builder.AppendLine($"{indent}return {value.Code} < 0");
            return;
        }

        if (condition.Type.Equals("NumberIsZero", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, condition, nodesById, "value", "Number", "0");
            builder.AppendLine($"{indent}return {value.Code} == 0");
            return;
        }

        if (condition.Type.Equals("NumberOutsideRange", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, condition, nodesById, "value", "Number", "0");
            var min = ParameterExpression(rule, condition, nodesById, "min", "Number", "0");
            var max = ParameterExpression(rule, condition, nodesById, "max", "Number", "1");
            builder.AppendLine($"{indent}return {value.Code} < {min.Code} or {value.Code} > {max.Code}");
            return;
        }

        if (condition.Type.Equals("NumberIsAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, condition, nodesById, "value", "Number", "0");
            var minimum = ParameterExpression(rule, condition, nodesById, "minimum", "Number", "0");
            builder.AppendLine($"{indent}return {value.Code} >= {minimum.Code}");
            return;
        }

        if (condition.Type.Equals("NumberIsAtMost", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, condition, nodesById, "value", "Number", "0");
            var maximum = ParameterExpression(rule, condition, nodesById, "maximum", "Number", "0");
            builder.AppendLine($"{indent}return {value.Code} <= {maximum.Code}");
            return;
        }

        if (condition.Type.Equals("NumberEquals", StringComparison.OrdinalIgnoreCase))
        {
            var left = ParameterExpression(rule, condition, nodesById, "left", "Number", "0");
            var right = ParameterExpression(rule, condition, nodesById, "right", "Number", "0");
            builder.AppendLine($"{indent}return {left.Code} == {right.Code}");
            return;
        }

        if (condition.Type.Equals("RandomChance", StringComparison.OrdinalIgnoreCase))
        {
            var percent = ParameterExpression(rule, condition, nodesById, "percent", "Number", "50");
            builder.AppendLine($"{indent}local chancePercent = math.max(0, math.min(100, {percent.Code}))");
            builder.AppendLine($"{indent}return math.random() * 100 < chancePercent");
            return;
        }

        if (condition.Type.Equals("StateIsTrue", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("StateIsFalse", StringComparison.OrdinalIgnoreCase))
        {
            var expected = condition.Type.Equals("StateIsTrue", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
            builder.AppendLine($"{indent}return VRS.states{VariableKeyExpression(rule, condition, nodesById, "state")} == {expected}");
            return;
        }

        if (condition.Type.Equals("TextStartsWith", StringComparison.OrdinalIgnoreCase))
        {
            var text = ParameterExpression(rule, condition, nodesById, "text", "String", "");
            var prefix = ParameterExpression(rule, condition, nodesById, "prefix", "String", "");
            var caseSensitive = ParameterExpression(rule, condition, nodesById, "caseSensitive", "Boolean", "false");
            builder.AppendLine($"{indent}local textValue = tostring({text.Code})");
            builder.AppendLine($"{indent}local prefixValue = tostring({prefix.Code})");
            builder.AppendLine($"{indent}if not ({caseSensitive.Code}) then");
            builder.AppendLine($"{indent}    textValue = string.lower(textValue)");
            builder.AppendLine($"{indent}    prefixValue = string.lower(prefixValue)");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return string.sub(textValue, 1, string.len(prefixValue)) == prefixValue");
            return;
        }

        if (condition.Type.Equals("TextEndsWith", StringComparison.OrdinalIgnoreCase))
        {
            var text = ParameterExpression(rule, condition, nodesById, "text", "String", "");
            var suffix = ParameterExpression(rule, condition, nodesById, "suffix", "String", "");
            var caseSensitive = ParameterExpression(rule, condition, nodesById, "caseSensitive", "Boolean", "false");
            builder.AppendLine($"{indent}local textValue = tostring({text.Code})");
            builder.AppendLine($"{indent}local suffixValue = tostring({suffix.Code})");
            builder.AppendLine($"{indent}if not ({caseSensitive.Code}) then");
            builder.AppendLine($"{indent}    textValue = string.lower(textValue)");
            builder.AppendLine($"{indent}    suffixValue = string.lower(suffixValue)");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if suffixValue == \"\" then");
            builder.AppendLine($"{indent}    return true");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return string.sub(textValue, -string.len(suffixValue)) == suffixValue");
            return;
        }

        if (condition.Type.Equals("ScriptVariableExists", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"{indent}return VRS.vars{VariableKeyExpression(rule, condition, nodesById, "name")} ~= nil");
            return;
        }

        if (condition.Type.Equals("ScriptNumberIsAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            var key = VariableKeyExpression(rule, condition, nodesById, "name");
            var minimum = ParameterExpression(rule, condition, nodesById, "minimum", "Number", "0");
            builder.AppendLine($"{indent}return (tonumber(VRS.vars{key}) or 0) >= {minimum.Code}");
            return;
        }

        if (condition.Type.Equals("ScriptNumberIsAtMost", StringComparison.OrdinalIgnoreCase))
        {
            var key = VariableKeyExpression(rule, condition, nodesById, "name");
            var maximum = ParameterExpression(rule, condition, nodesById, "maximum", "Number", "0");
            builder.AppendLine($"{indent}return (tonumber(VRS.vars{key}) or 0) <= {maximum.Code}");
            return;
        }

        if (condition.Type.Equals("ScriptNumberEquals", StringComparison.OrdinalIgnoreCase))
        {
            var key = VariableKeyExpression(rule, condition, nodesById, "name");
            var expected = ParameterExpression(rule, condition, nodesById, "expected", "Number", "0");
            builder.AppendLine($"{indent}return (tonumber(VRS.vars{key}) or 0) == {expected.Code}");
            return;
        }

        if (condition.Type.Equals("ScriptTextEquals", StringComparison.OrdinalIgnoreCase))
        {
            var expected = ParameterExpression(rule, condition, nodesById, "expected", "String", "");
            var caseSensitive = ParameterExpression(rule, condition, nodesById, "caseSensitive", "Boolean", "false");
            builder.AppendLine($"{indent}local currentValue = tostring(VRS.vars{VariableKeyExpression(rule, condition, nodesById, "name")} or \"\")");
            builder.AppendLine($"{indent}local expectedValue = tostring({expected.Code})");
            builder.AppendLine($"{indent}if not ({caseSensitive.Code}) then");
            builder.AppendLine($"{indent}    currentValue = string.lower(currentValue)");
            builder.AppendLine($"{indent}    expectedValue = string.lower(expectedValue)");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return currentValue == expectedValue");
            return;
        }

        if (condition.Type.Equals("ScriptBooleanIsTrue", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"{indent}return VRS.vars{VariableKeyExpression(rule, condition, nodesById, "name")} == true");
            return;
        }

        if (condition.Type.Equals("ObjectIsNamed", StringComparison.OrdinalIgnoreCase))
        {
            var name = ParameterExpression(rule, condition, nodesById, "name", "String", "");
            builder.AppendLine($"{indent}local targetObject = {TargetExpression(plan, condition)}");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return tostring(targetObject.Name) == tostring({name.Code})");
            return;
        }

        if (condition.Type.Equals("ObjectIsType", StringComparison.OrdinalIgnoreCase))
        {
            var typeName = ParameterExpression(rule, condition, nodesById, "typeName", "String", "Part");
            builder.AppendLine($"{indent}local targetObject = {TargetExpression(plan, condition)}");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if targetObject.IsA == nil then");
            builder.AppendLine($"{indent}    print(\"Object Is Type stopped: target does not support IsA.\")");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return targetObject:IsA(tostring({typeName.Code}))");
            return;
        }

        if (condition.Type.Equals("ObjectIsVisible", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"{indent}local targetObject = {TargetExpression(plan, condition)}");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if targetObject.Visible ~= nil then");
            builder.AppendLine($"{indent}    return targetObject.Visible == true");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if targetObject.Transparency ~= nil then");
            builder.AppendLine($"{indent}    return targetObject.Transparency < 1");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}print(\"Object Is Visible stopped: target does not expose Visible or Transparency.\")");
            builder.AppendLine($"{indent}return false");
            return;
        }

        if (condition.Type.Equals("ObjectIsHidden", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"{indent}local targetObject = {TargetExpression(plan, condition)}");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if targetObject.Visible ~= nil then");
            builder.AppendLine($"{indent}    return targetObject.Visible == false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if targetObject.Transparency ~= nil then");
            builder.AppendLine($"{indent}    return targetObject.Transparency >= 1");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}print(\"Object Is Hidden stopped: target does not expose Visible or Transparency.\")");
            builder.AppendLine($"{indent}return false");
            return;
        }

        if (condition.Type.Equals("ObjectCollisionIsOn", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("ObjectCollisionIsOff", StringComparison.OrdinalIgnoreCase))
        {
            var expected = condition.Type.Equals("ObjectCollisionIsOn", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
            builder.AppendLine($"{indent}local targetObject = {TargetExpression(plan, condition)}");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if targetObject.CanCollide == nil then");
            builder.AppendLine($"{indent}    print(\"{EscapeForDoubleQuotedString(condition.Label)} stopped: target does not expose CanCollide.\")");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return targetObject.CanCollide == {expected}");
            return;
        }

        if (condition.Type.Equals("ObjectIsAnchored", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("ObjectIsUnanchored", StringComparison.OrdinalIgnoreCase))
        {
            var expected = condition.Type.Equals("ObjectIsAnchored", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
            builder.AppendLine($"{indent}local targetObject = {TargetExpression(plan, condition)}");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if targetObject.Anchored == nil then");
            builder.AppendLine($"{indent}    print(\"{EscapeForDoubleQuotedString(condition.Label)} stopped: target does not expose Anchored.\")");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return targetObject.Anchored == {expected}");
            return;
        }

        if (condition.Type.Equals("ObjectIsAboveHeight", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("ObjectIsBelowHeight", StringComparison.OrdinalIgnoreCase))
        {
            var height = ParameterExpression(rule, condition, nodesById, "height", "Number", "0");
            var comparison = condition.Type.Equals("ObjectIsAboveHeight", StringComparison.OrdinalIgnoreCase) ? ">" : "<";
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if targetObject.Position == nil then");
            builder.AppendLine($"{indent}    print(\"{EscapeForDoubleQuotedString(condition.Label)} stopped: target does not expose Position.\")");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local objectHeight = vrsValueAxis(targetObject.Position, \"Y\", \"y\", 0)");
            builder.AppendLine($"{indent}return objectHeight {comparison} {height.Code}");
            return;
        }

        if (condition.Type.Equals("ObjectTurnAngleAtLeast", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("ObjectTurnAngleAtMost", StringComparison.OrdinalIgnoreCase))
        {
            var angle = ParameterExpression(rule, condition, nodesById, "angle", "Number", "0");
            var comparison = condition.Type.Equals("ObjectTurnAngleAtLeast", StringComparison.OrdinalIgnoreCase) ? ">=" : "<=";
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if targetObject.Rotation == nil then");
            builder.AppendLine($"{indent}    print(\"{EscapeForDoubleQuotedString(condition.Label)} stopped: target does not expose Rotation.\")");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local turnAngle = vrsValueAxis(targetObject.Rotation, \"Y\", \"y\", 0)");
            builder.AppendLine($"{indent}return turnAngle {comparison} {angle.Code}");
            return;
        }

        if (condition.Type.Equals("ObjectSizeAtLeast", StringComparison.OrdinalIgnoreCase) ||
            condition.Type.Equals("ObjectSizeAtMost", StringComparison.OrdinalIgnoreCase))
        {
            var direction = ParameterExpression(rule, condition, nodesById, "direction", "String", "Width");
            var size = ParameterExpression(rule, condition, nodesById, "size", "Number", "1");
            var comparison = condition.Type.Equals("ObjectSizeAtLeast", StringComparison.OrdinalIgnoreCase) ? ">=" : "<=";
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if targetObject.Scale == nil then");
            builder.AppendLine($"{indent}    print(\"{EscapeForDoubleQuotedString(condition.Label)} stopped: target does not expose Scale.\")");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local sizeDirection = tostring({direction.Code})");
            builder.AppendLine($"{indent}local sizeAxisUpper = \"X\"");
            builder.AppendLine($"{indent}local sizeAxisLower = \"x\"");
            builder.AppendLine($"{indent}if sizeDirection == \"Height\" then");
            builder.AppendLine($"{indent}    sizeAxisUpper = \"Y\"");
            builder.AppendLine($"{indent}    sizeAxisLower = \"y\"");
            builder.AppendLine($"{indent}elseif sizeDirection == \"Depth\" then");
            builder.AppendLine($"{indent}    sizeAxisUpper = \"Z\"");
            builder.AppendLine($"{indent}    sizeAxisLower = \"z\"");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local currentSize = vrsValueAxis(targetObject.Scale, sizeAxisUpper, sizeAxisLower, 1)");
            builder.AppendLine($"{indent}return currentSize {comparison} {size.Code}");
            return;
        }

        if (condition.Type.Equals("ObjectTransparencyAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            var minimum = ParameterExpression(rule, condition, nodesById, "minimum", "Number", "0.5");
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if targetObject.Transparency == nil then");
            builder.AppendLine($"{indent}    print(\"Object Transparency At Least stopped: target does not expose Transparency.\")");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return targetObject.Transparency >= {minimum.Code}");
            return;
        }

        if (ObjectDistanceConditionTypes.Contains(condition.Type))
        {
            var first = ParameterExpression(rule, condition, nodesById, "first", "String", "Self");
            var second = ParameterExpression(rule, condition, nodesById, "second", "String", "Target");
            var distanceParameter = condition.Type.Equals("ObjectIsFarFromObject", StringComparison.OrdinalIgnoreCase)
                ? "minDistance"
                : "maxDistance";
            var fallbackDistance = condition.Type.Equals("ObjectIsFarFromObject", StringComparison.OrdinalIgnoreCase) ? "25" : "10";
            var distanceLimit = ParameterExpression(rule, condition, nodesById, distanceParameter, "Number", fallbackDistance);
            builder.AppendLine($"{indent}local firstObject = resolveTarget(triggerObject, {first.Code})");
            builder.AppendLine($"{indent}local secondObject = resolveTarget(triggerObject, {second.Code})");
            builder.AppendLine($"{indent}if firstObject == nil or secondObject == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if firstObject.Position == nil or secondObject.Position == nil then");
            builder.AppendLine($"{indent}    print(\"{EscapeForDoubleQuotedString(condition.Label)} stopped: an object has no Position.\")");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local distance = vrsDistanceBetweenPositions(firstObject.Position, secondObject.Position)");
            if (condition.Type.Equals("ObjectIsFarFromObject", StringComparison.OrdinalIgnoreCase))
            {
                builder.AppendLine($"{indent}return distance >= {distanceLimit.Code}");
            }
            else
            {
                builder.AppendLine($"{indent}return distance <= {distanceLimit.Code}");
            }

            return;
        }

        if (condition.Type.Equals("ObjectHasParent", StringComparison.OrdinalIgnoreCase))
        {
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}return targetObject ~= nil and targetObject.Parent ~= nil");
            return;
        }

        if (condition.Type.Equals("ObjectParentIs", StringComparison.OrdinalIgnoreCase))
        {
            var parent = ParameterExpression(rule, condition, nodesById, "parent", "String", "Target");
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}local expectedParent = resolveTarget(triggerObject, {parent.Code})");
            builder.AppendLine($"{indent}if targetObject == nil or expectedParent == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return targetObject.Parent == expectedParent");
            return;
        }

        if (condition.Type.Equals("ObjectIsUnderObject", StringComparison.OrdinalIgnoreCase))
        {
            var ancestor = ParameterExpression(rule, condition, nodesById, "ancestor", "String", "Target");
            AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
            builder.AppendLine($"{indent}local expectedAncestor = resolveTarget(triggerObject, {ancestor.Code})");
            builder.AppendLine($"{indent}if targetObject == nil or expectedAncestor == nil then");
            builder.AppendLine($"{indent}    return false");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}local current = targetObject.Parent");
            builder.AppendLine($"{indent}while current ~= nil do");
            builder.AppendLine($"{indent}    if current == expectedAncestor then");
            builder.AppendLine($"{indent}        return true");
            builder.AppendLine($"{indent}    end");
            builder.AppendLine($"{indent}    current = current.Parent");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}return false");
            return;
        }

        if (TryAppendReadableEssentialsConditionBody(builder, rule, condition, nodesById, indentLevel) ||
            TryAppendReadableObbyConditionBody(builder, rule, condition, nodesById, indentLevel) ||
            TryAppendReadableGameplayApiConditionBody(builder, rule, condition, plan, nodesById, indentLevel))
        {
            return;
        }

        builder.AppendLine($"{indent}print(\"Condition {EscapeForDoubleQuotedString(condition.Label)} is not implemented yet; returning false.\")");
        builder.AppendLine($"{indent}return false");
    }

    private static void AppendReadableActionBlock(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        IReadOnlyCollection<NodeCatalogEntry> catalog,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById)
    {
        builder.AppendLine(LuauCommentTags.VsrComment($"ACTION: {HumanBlockName(action.Label)}"));
        if (AppendReadableNodeLocalVariables(builder, rule, action, nodesById, plan))
        {
            builder.AppendLine();
        }

        AppendReadableNodeSummary(builder, action, catalog);
        builder.AppendLine($"{RegistryFunctionReference(plan, action)} = function(triggerObject, triggerContext)");
        AppendReadableActionBody(builder, rule, action, plan, nodesById, 1);
        builder.AppendLine("end");
    }

    private static void AppendReadableActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var parameterValues = BuildEffectiveParameterValues(rule, action, nodesById);
        if (action.Type.Equals("RunLuauAction", StringComparison.OrdinalIgnoreCase))
        {
            AppendRunLuauActionBody(builder, action, indentLevel);
            return;
        }

        if (action.Type.Equals("SendInputEvent", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("SendInputTextEvent", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableSendInputEventAction(builder, rule, action, nodesById, indentLevel);
            return;
        }

        if (action.Type.Equals("FireBindableEvent", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableFireBindableEventAction(builder, rule, action, nodesById, indentLevel);
            return;
        }

        if (action.Type.Equals("ShowMessage", StringComparison.OrdinalIgnoreCase))
        {
            var message = HasConfigName(plan, action, "message")
                ? new LuauExpression(ConfigName(plan, action, "message"), "String")
                : ParameterExpression(rule, action, nodesById, "message", "String", "");
            builder.AppendLine($"{indent}print({message.Code})");
            return;
        }

        if (action.Type.Equals("ShowWarning", StringComparison.OrdinalIgnoreCase))
        {
            var message = ParameterExpression(rule, action, nodesById, "message", "String", "Warning from VisualRuleSystem.");
            builder.AppendLine($"{indent}print(\"[VRS warning] \" .. tostring({message.Code}))");
            return;
        }

        if (action.Type.Equals("PrintValue", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, action, nodesById, "value", "Any", "");
            builder.AppendLine($"{indent}print(tostring({value.Code}))");
            return;
        }

        if (action.Type.Equals("WaitSeconds", StringComparison.OrdinalIgnoreCase))
        {
            var waitToComplete = BooleanValue(ParameterValue(action, parameterValues, "waitToComplete"), fallback: true);
            if (waitToComplete)
            {
                var duration = HasConfigName(plan, action, "duration")
                    ? new LuauExpression(ConfigName(plan, action, "duration"), "Number")
                    : ParameterExpression(rule, action, nodesById, "duration", "Number", "1");
                builder.AppendLine($"{indent}wait({duration.Code})");
            }
            else
            {
                AppendDebugPrint(builder, action, indentLevel, "Wait Seconds is configured to continue immediately.");
            }

            return;
        }

        if (action.Type.Equals("SetScriptVariable", StringComparison.OrdinalIgnoreCase))
        {
            var value = ParameterExpression(rule, action, nodesById, "value", "Any", "");
            builder.AppendLine($"{indent}VRS.vars{VariableKeyExpression(rule, action, nodesById, "name")} = {value.Code}");
            return;
        }

        if (action.Type.Equals("IncrementScriptNumber", StringComparison.OrdinalIgnoreCase))
        {
            var amount = ParameterExpression(rule, action, nodesById, "amount", "Number", "1");
            var key = VariableKeyExpression(rule, action, nodesById, "name");
            builder.AppendLine($"{indent}VRS.vars{key} = (tonumber(VRS.vars{key}) or 0) + {amount.Code}");
            return;
        }

        if (action.Type.Equals("DecrementScriptNumber", StringComparison.OrdinalIgnoreCase))
        {
            var amount = ParameterExpression(rule, action, nodesById, "amount", "Number", "1");
            var key = VariableKeyExpression(rule, action, nodesById, "name");
            builder.AppendLine($"{indent}VRS.vars{key} = (tonumber(VRS.vars{key}) or 0) - {amount.Code}");
            return;
        }

        if (action.Type.Equals("MultiplyScriptNumber", StringComparison.OrdinalIgnoreCase))
        {
            var factor = ParameterExpression(rule, action, nodesById, "factor", "Number", "1");
            var key = VariableKeyExpression(rule, action, nodesById, "name");
            builder.AppendLine($"{indent}VRS.vars{key} = (tonumber(VRS.vars{key}) or 0) * {factor.Code}");
            return;
        }

        if (action.Type.Equals("AppendScriptText", StringComparison.OrdinalIgnoreCase))
        {
            var text = ParameterExpression(rule, action, nodesById, "text", "String", "");
            var key = VariableKeyExpression(rule, action, nodesById, "name");
            builder.AppendLine($"{indent}VRS.vars{key} = tostring(VRS.vars{key} or \"\") .. tostring({text.Code})");
            return;
        }

        if (action.Type.Equals("SetState", StringComparison.OrdinalIgnoreCase))
        {
            var enabled = ParameterExpression(rule, action, nodesById, "enabled", "Boolean", "true");
            builder.AppendLine($"{indent}VRS.states{VariableKeyExpression(rule, action, nodesById, "state")} = {enabled.Code}");
            return;
        }

        if (action.Type.Equals("ToggleState", StringComparison.OrdinalIgnoreCase))
        {
            var key = VariableKeyExpression(rule, action, nodesById, "state");
            builder.AppendLine($"{indent}VRS.states{key} = not (VRS.states{key} == true)");
            return;
        }

        if (action.Type.Equals("ClearScriptVariable", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"{indent}VRS.vars{VariableKeyExpression(rule, action, nodesById, "name")} = nil");
            return;
        }

        if (action.Type.Equals("SetObjectColor", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"{indent}local targetPart = {TargetExpression(plan, action)}");
            builder.AppendLine($"{indent}if targetPart == nil then");
            if (HasConfigName(plan, action, "target"))
            {
                builder.AppendLine($"{indent}    print(\"Set Object Color stopped: target \" .. tostring({ConfigName(plan, action, "target")}) .. \" was not found.\")");
            }
            else
            {
                builder.AppendLine($"{indent}    print(\"Set Object Color stopped: target Self was not found.\")");
            }

            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine();
            builder.AppendLine($"{indent}if not targetPart:IsA(\"Part\") then");
            builder.AppendLine($"{indent}    print(\"Set Object Color stopped: target \" .. tostring(targetPart.Name) .. \" is not a Part.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine();
            var color = HasConfigName(plan, action, "color")
                ? new LuauExpression(ConfigName(plan, action, "color"), "Any")
                : ColorExpression(rule, action, nodesById);
            builder.AppendLine($"{indent}targetPart.Color = {color.Code}");
            if (action.DebugEnabled)
            {
                builder.AppendLine($"{indent}print(targetPart.Name .. \" color changed by Set Object Color.\")");
            }
            return;
        }

        if (action.Type.Equals("SetObjectVisible", StringComparison.OrdinalIgnoreCase))
        {
            var visible = ParameterExpression(rule, action, nodesById, "visible", "Boolean", "true");
            AppendObjectVisibilityAction(builder, plan, action, indentLevel, visible.Code, "Set Object Visible");
            return;
        }

        if (action.Type.Equals("ShowObject", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("HideObject", StringComparison.OrdinalIgnoreCase))
        {
            var visible = action.Type.Equals("ShowObject", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
            AppendObjectVisibilityAction(builder, plan, action, indentLevel, visible, action.Label);
            return;
        }

        if (action.Type.Equals("ToggleObjectVisibility", StringComparison.OrdinalIgnoreCase))
        {
            AppendToggleObjectVisibilityAction(builder, plan, action, indentLevel, "Toggle Object Visibility");
            return;
        }

        if (action.Type.Equals("SetObjectName", StringComparison.OrdinalIgnoreCase))
        {
            var name = ParameterExpression(rule, action, nodesById, "name", "String", "Object");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    print(\"Set Object Name stopped: target was not found.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}targetObject.Name = tostring({name.Code})");
            return;
        }

        if (action.Type.Equals("SetObjectParent", StringComparison.OrdinalIgnoreCase))
        {
            var newParent = ParameterExpression(rule, action, nodesById, "newParent", "String", "Target");
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
            builder.AppendLine($"{indent}local newParentObject = resolveTarget(triggerObject, {newParent.Code})");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    print(\"Set Object Parent stopped: object to move was not found.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if newParentObject == nil then");
            builder.AppendLine($"{indent}    print(\"Set Object Parent stopped: new parent was not found.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}targetObject.Parent = newParentObject");
            return;
        }

        if (action.Type.Equals("SetObjectTransparency", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Transparency", "transparency", "Number", "0", "Set Object Transparency");
            return;
        }

        if (action.Type.Equals("SetObjectAnchored", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Anchored", "anchored", "Boolean", "true", "Set Object Anchored");
            return;
        }

        if (action.Type.Equals("SetObjectCanCollide", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "CanCollide", "canCollide", "Boolean", "true", "Set Object Can Collide");
            return;
        }

        if (action.Type.Equals("ToggleObjectAnchored", StringComparison.OrdinalIgnoreCase))
        {
            AppendToggleObjectBooleanPropertyAction(builder, plan, action, indentLevel, "Anchored", "Toggle Object Anchored");
            return;
        }

        if (action.Type.Equals("TurnObjectCollisionOn", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("TurnObjectCollisionOff", StringComparison.OrdinalIgnoreCase))
        {
            var enabled = action.Type.Equals("TurnObjectCollisionOn", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
            AppendFixedObjectPropertyAction(builder, plan, action, indentLevel, "CanCollide", enabled, action.Label);
            return;
        }

        if (action.Type.Equals("ToggleObjectCollision", StringComparison.OrdinalIgnoreCase))
        {
            AppendToggleObjectBooleanPropertyAction(builder, plan, action, indentLevel, "CanCollide", "Toggle Object Collision");
            return;
        }

        if (action.Type.Equals("DestroyObject", StringComparison.OrdinalIgnoreCase))
        {
            AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
            builder.AppendLine($"{indent}if targetObject == nil then");
            builder.AppendLine($"{indent}    print(\"Destroy Object stopped: target was not found.\")");
            builder.AppendLine($"{indent}    return");
            builder.AppendLine($"{indent}end");
            builder.AppendLine($"{indent}if targetObject.Destroy ~= nil then");
            builder.AppendLine($"{indent}    targetObject:Destroy()");
            builder.AppendLine($"{indent}elseif targetObject.Remove ~= nil then");
            builder.AppendLine($"{indent}    targetObject:Remove()");
            builder.AppendLine($"{indent}else");
            builder.AppendLine($"{indent}    print(\"Destroy Object stopped: target does not support Destroy or Remove.\")");
            builder.AppendLine($"{indent}end");
            return;
        }

        if (action.Type.Equals("MoveObject", StringComparison.OrdinalIgnoreCase))
        {
            AppendMoveObjectAction(builder, rule, action, plan, nodesById, indentLevel);
            return;
        }

        if (action.Type.Equals("MoveObjectToAnotherObject", StringComparison.OrdinalIgnoreCase))
        {
            AppendMoveObjectToAnotherObjectAction(builder, rule, action, plan, nodesById, indentLevel);
            return;
        }

        if (action.Type.Equals("MoveObjectUp", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("MoveObjectDown", StringComparison.OrdinalIgnoreCase))
        {
            var direction = action.Type.Equals("MoveObjectDown", StringComparison.OrdinalIgnoreCase) ? -1 : 1;
            AppendMoveObjectVerticalAction(builder, rule, action, plan, nodesById, indentLevel, direction);
            return;
        }

        if (action.Type.Equals("MoveObjectOverTime", StringComparison.OrdinalIgnoreCase))
        {
            AppendLegacyMoveObjectOverTimeAction(builder, rule, action, plan, nodesById, indentLevel);
            return;
        }

        if (action.Type.Equals("SetObjectPosition", StringComparison.OrdinalIgnoreCase))
        {
            AppendVectorPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Position", "Set Object Position", replace: true);
            return;
        }

        if (action.Type.Equals("SetObjectXPosition", StringComparison.OrdinalIgnoreCase))
        {
            AppendSetObjectPositionAxisAction(builder, rule, action, plan, nodesById, indentLevel, "newX", "x", "Set Object X Position");
            return;
        }

        if (action.Type.Equals("SetObjectHeightPosition", StringComparison.OrdinalIgnoreCase))
        {
            AppendSetObjectPositionAxisAction(builder, rule, action, plan, nodesById, indentLevel, "newY", "height", "Set Object Height Position");
            return;
        }

        if (action.Type.Equals("SetObjectZPosition", StringComparison.OrdinalIgnoreCase))
        {
            AppendSetObjectPositionAxisAction(builder, rule, action, plan, nodesById, indentLevel, "newZ", "z", "Set Object Z Position");
            return;
        }

        if (action.Type.Equals("AddObjectPosition", StringComparison.OrdinalIgnoreCase))
        {
            AppendVectorPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Position", "Add Object Position", replace: false);
            return;
        }

        if (action.Type.Equals("RotateObject", StringComparison.OrdinalIgnoreCase))
        {
            AppendRotateObjectAction(builder, rule, action, plan, nodesById, indentLevel);
            return;
        }

        if (action.Type.Equals("SetObjectTurnAngle", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("TurnObjectByAngle", StringComparison.OrdinalIgnoreCase))
        {
            AppendObjectTurnAngleAction(
                builder,
                rule,
                action,
                plan,
                nodesById,
                indentLevel,
                addToCurrent: action.Type.Equals("TurnObjectByAngle", StringComparison.OrdinalIgnoreCase));
            return;
        }

        if (action.Type.Equals("SetObjectWidthSize", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("SetObjectHeightSize", StringComparison.OrdinalIgnoreCase) ||
            action.Type.Equals("SetObjectDepthSize", StringComparison.OrdinalIgnoreCase))
        {
            var axisVariableName = action.Type.Equals("SetObjectHeightSize", StringComparison.OrdinalIgnoreCase)
                ? "newY"
                : action.Type.Equals("SetObjectDepthSize", StringComparison.OrdinalIgnoreCase)
                    ? "newZ"
                    : "newX";
            AppendSetObjectSizeAxisAction(builder, rule, action, plan, nodesById, indentLevel, axisVariableName, action.Label);
            return;
        }

        if (action.Type.Equals("SetObjectScale", StringComparison.OrdinalIgnoreCase))
        {
            AppendTransformVectorPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Scale", "Scale Object", replace: true);
            return;
        }

        if (action.Type.Equals("LookAtPosition", StringComparison.OrdinalIgnoreCase))
        {
            AppendLookAtPositionAction(builder, rule, action, plan, nodesById, indentLevel);
            return;
        }

        if (action.Type.Equals("LookAtObject", StringComparison.OrdinalIgnoreCase))
        {
            AppendLookAtObjectAction(builder, rule, action, plan, nodesById, indentLevel);
            return;
        }

        if (action.Type.Equals("RotateObjectContinuously", StringComparison.OrdinalIgnoreCase))
        {
            AppendSpinRotationAction(builder, rule, action, plan, nodesById, indentLevel, "interval", "Rotate Object Continuously");
            return;
        }

        if (TryAppendReadableEssentialsActionBody(builder, rule, action, nodesById, indentLevel) ||
            TryAppendReadableObbyActionBody(builder, rule, action, plan, nodesById, indentLevel) ||
            TryAppendReadableGameplayApiActionBody(builder, rule, action, plan, nodesById, indentLevel))
        {
            return;
        }

        builder.AppendLine($"{indent}print(\"Action {EscapeForDoubleQuotedString(action.Label)} is not implemented in the readable exporter yet.\")");
    }

}
