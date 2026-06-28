using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private sealed record LuauExpression(string Code, string DataType);

    // Property nodes are exported as inline Luau expressions so generic value
    // nodes can feed actions and conditions without creating noisy helper
    // functions in the generated script.
    private static LuauExpression ParameterExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        string key,
        string dataType,
        string fallback)
    {
        var incoming = FindIncomingValueConnection(rule, node.Id, key);
        if (incoming is not null && nodesById.TryGetValue(incoming.From.NodeId, out var sourceNode))
        {
            return ResolveSourceNodeExpression(rule, sourceNode, nodesById, NormalizeExpressionDataType(dataType), fallback, []);
        }

        var authored = node.Parameters.FirstOrDefault(parameter => parameter.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (authored?.Binding.SourceKind == GraphValueSourceKind.CatalogValue)
        {
            return CatalogValueExpression(rule, authored.Binding, nodesById, dataType, fallback, []);
        }

        var authoredValue = authored is null ? fallback : EffectiveParameterValue(authored);
        return LiteralExpression(authoredValue, dataType, fallback);
    }

    private static LuauExpression ParameterVectorExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        string key,
        string fallbackX,
        string fallbackY,
        string fallbackZ)
    {
        var fallback = $"{fallbackX},{fallbackY},{fallbackZ}";
        var incoming = FindIncomingValueConnection(rule, node.Id, key);
        if (incoming is not null && nodesById.TryGetValue(incoming.From.NodeId, out var sourceNode))
        {
            return ResolveSourceNodeExpression(rule, sourceNode, nodesById, "Vector3", fallback, []);
        }

        var authored = node.Parameters.FirstOrDefault(parameter => parameter.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (authored?.Binding.SourceKind == GraphValueSourceKind.CatalogValue)
        {
            return CatalogValueExpression(rule, authored.Binding, nodesById, "Vector3", fallback, []);
        }

        if (authored is not null)
        {
            return LiteralExpression(EffectiveParameterValue(authored), "Vector3", fallback);
        }

        var x = ParameterExpression(rule, node, nodesById, "x", "Number", fallbackX);
        var y = ParameterExpression(rule, node, nodesById, "y", "Number", fallbackY);
        var z = ParameterExpression(rule, node, nodesById, "z", "Number", fallbackZ);
        return new LuauExpression($"makeVector3({x.Code}, {y.Code}, {z.Code})", "Vector3");
    }

    private static LuauExpression CatalogValueExpression(
        Rule rule,
        GraphValueBinding binding,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        string expectedDataType,
        string fallback,
        HashSet<string> visitedNodeIds)
    {
        if (string.IsNullOrWhiteSpace(binding.CatalogType))
        {
            return LiteralExpression(fallback, expectedDataType, fallback);
        }

        var recipeNode = new RuleNode
        {
            Id = $"CATALOG_VALUE_{binding.CatalogId}_{visitedNodeIds.Count}",
            Kind = NodeKind.Property,
            Type = binding.CatalogType,
            Label = string.IsNullOrWhiteSpace(binding.DisplayText) ? binding.CatalogId : binding.DisplayText,
            CatalogId = binding.CatalogId,
            Parameters = binding.CatalogParameters
        };

        return ResolveSourceNodeExpression(rule, recipeNode, nodesById, NormalizeExpressionDataType(expectedDataType), fallback, visitedNodeIds);
    }

    private static LuauExpression ResolveSourceNodeExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        string expectedDataType,
        string fallback,
        HashSet<string> visitedNodeIds)
    {
        if (!visitedNodeIds.Add(node.Id))
        {
            return LiteralExpression(fallback, expectedDataType, fallback);
        }

        if (node.Kind != NodeKind.Property)
        {
            var literal = LiteralExpression(node.Value, expectedDataType, fallback);
            visitedNodeIds.Remove(node.Id);
            return literal;
        }

        if (TryResolveReadableEssentialsPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } essentialsExpression)
        {
            visitedNodeIds.Remove(node.Id);
            return essentialsExpression;
        }

        if (TryResolveReadableGameplayApiPropertyExpression(rule, node, nodesById, visitedNodeIds) is { } gameplayExpression)
        {
            visitedNodeIds.Remove(node.Id);
            return gameplayExpression;
        }

        var expression = node.Type switch
        {
            "ManualText" => PropertyValueLiteral(node, "String", fallback),
            "ManualNumber" => PropertyValueLiteral(node, "Number", fallback),
            "ManualBoolean" => PropertyValueLiteral(node, "Boolean", fallback),
            "ChooseText" => PropertyValueLiteral(node, "String", fallback),
            "ChooseNumber" => PropertyValueLiteral(node, "Number", fallback),
            "ChooseObject" => ChooseObjectExpression(rule, node, nodesById, visitedNodeIds, expectedDataType),
            "RandomTrueOrFalse" => RandomTrueOrFalseExpression(rule, node, nodesById, visitedNodeIds),
            "NumberFromText" => NumberFromTextExpression(rule, node, nodesById, visitedNodeIds),
            "AddNumbers" => BinaryNumberExpression(rule, node, nodesById, visitedNodeIds, "+"),
            "SubtractNumbers" => BinaryNumberExpression(rule, node, nodesById, visitedNodeIds, "-"),
            "MultiplyNumbers" => BinaryNumberExpression(rule, node, nodesById, visitedNodeIds, "*"),
            "DivideNumbers" => DivideNumberExpression(rule, node, nodesById, visitedNodeIds),
            "AverageNumber" => AverageNumberExpression(rule, node, nodesById, visitedNodeIds),
            "SquareRootNumber" => SquareRootNumberExpression(rule, node, nodesById, visitedNodeIds),
            "MinNumber" => MinMaxNumberExpression(rule, node, nodesById, visitedNodeIds, "math.min"),
            "MaxNumber" => MinMaxNumberExpression(rule, node, nodesById, visitedNodeIds, "math.max"),
            "AbsoluteNumber" => UnaryNumberFunctionExpression(rule, node, nodesById, visitedNodeIds, "math.abs"),
            "FloorNumber" => UnaryNumberFunctionExpression(rule, node, nodesById, visitedNodeIds, "math.floor"),
            "CeilNumber" => UnaryNumberFunctionExpression(rule, node, nodesById, visitedNodeIds, "math.ceil"),
            "PowerNumbers" => PowerNumberExpression(rule, node, nodesById, visitedNodeIds),
            "ModuloNumbers" => ModuloNumberExpression(rule, node, nodesById, visitedNodeIds),
            "LerpNumber" => LerpNumberExpression(rule, node, nodesById, visitedNodeIds),
            "RoundNumber" => RoundNumberExpression(rule, node, nodesById, visitedNodeIds),
            "ClampNumber" => ClampNumberExpression(rule, node, nodesById, visitedNodeIds),
            "RandomNumber" => RandomNumberExpression(rule, node, nodesById, visitedNodeIds),
            "RandomWholeNumber" => RandomWholeNumberExpression(rule, node, nodesById, visitedNodeIds),
            "RandomNumberChoice" => RandomNumberChoiceExpression(rule, node, nodesById, visitedNodeIds),
            "JoinText" => JoinTextExpression(rule, node, nodesById, visitedNodeIds),
            "RandomTextChoice" => RandomTextChoiceExpression(rule, node, nodesById, visitedNodeIds),
            "LowercaseText" => TextUnaryFunctionExpression(rule, node, nodesById, visitedNodeIds, "string.lower"),
            "UppercaseText" => TextUnaryFunctionExpression(rule, node, nodesById, visitedNodeIds, "string.upper"),
            "TrimText" => TrimTextExpression(rule, node, nodesById, visitedNodeIds),
            "ReplaceText" => ReplaceTextExpression(rule, node, nodesById, visitedNodeIds),
            "TextBefore" => TextBeforeExpression(rule, node, nodesById, visitedNodeIds),
            "TextAfter" => TextAfterExpression(rule, node, nodesById, visitedNodeIds),
            "TextBetween" => TextBetweenExpression(rule, node, nodesById, visitedNodeIds),
            "FirstTextCharacters" => TextCharactersExpression(rule, node, nodesById, visitedNodeIds, takeFirst: true),
            "LastTextCharacters" => TextCharactersExpression(rule, node, nodesById, visitedNodeIds, takeFirst: false),
            "TextLength" => TextLengthExpression(rule, node, nodesById, visitedNodeIds),
            "NumberToText" => NumberToTextExpression(rule, node, nodesById, visitedNodeIds),
            "AndBoolean" => BooleanBinaryExpression(rule, node, nodesById, visitedNodeIds, "and"),
            "OrBoolean" => BooleanBinaryExpression(rule, node, nodesById, visitedNodeIds, "or"),
            "NotBoolean" => NotBooleanExpression(rule, node, nodesById, visitedNodeIds),
            "ReadScriptVariable" => ReadScriptVariableExpression(rule, node, nodesById),
            "ReadState" => ReadStateExpression(rule, node, nodesById),
            "RunLuauProperty" => RunLuauPropertyExpression(node, expectedDataType, fallback),
            "ManualVector3" => PropertyVectorParameterExpression(rule, node, nodesById, "vector", "0", "0", "0", visitedNodeIds),
            "VectorFromXYZ" => VectorFromXyzExpression(rule, node, nodesById, visitedNodeIds),
            "VectorAdd" => VectorBinaryExpression(rule, node, nodesById, visitedNodeIds, "+"),
            "VectorSubtract" => VectorBinaryExpression(rule, node, nodesById, visitedNodeIds, "-"),
            "DirectionToObject" => DirectionToObjectExpression(rule, node, nodesById, visitedNodeIds),
            "RGBColor" => RgbColorExpression(rule, node, nodesById, visitedNodeIds),
            "RandomColor" => new LuauExpression("Color.New(math.random(), math.random(), math.random(), 1)", "Color"),
            "ObjectName" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Name", "String", "Object Name", "return tostring(targetObject.Name)"),
            "ObjectTypeName" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "ClassName", "String", "Object Type Name", "return tostring(targetObject.ClassName)"),
            "ObjectNetworkKey" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "NetworkedObjectID", "String", "Object Network Key", "return tostring(targetObject.NetworkedObjectID)"),
            "ObjectSaveKey" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "ObjectID", "String", "Object Save Key", "return tostring(targetObject.ObjectID)"),
            "ObjectIsNetworkedValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "ExistInNetwork", "Boolean", "Object Is Networked", "return targetObject.ExistInNetwork == true"),
            "ObjectPosition" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Position", "Vector3", "Object Position", "return targetObject.Position"),
            "ObjectXPosition" => ObjectPositionAxisExpression(rule, node, nodesById, visitedNodeIds, "X", "x", "Object X Position"),
            "ObjectHeightPosition" => ObjectPositionAxisExpression(rule, node, nodesById, visitedNodeIds, "Y", "y", "Object Height Position"),
            "ObjectZPosition" => ObjectPositionAxisExpression(rule, node, nodesById, visitedNodeIds, "Z", "z", "Object Z Position"),
            "ObjectRotation" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Rotation", "Vector3", "Object Rotation", "return targetObject.Rotation"),
            "ObjectTurnAngle" => ObjectVectorAxisExpression(rule, node, nodesById, visitedNodeIds, "Rotation", "Y", "y", "Object Turn Angle"),
            "ObjectScale" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Scale", "Vector3", "Object Scale", "return targetObject.Scale"),
            "ObjectWidthSize" => ObjectVectorAxisExpression(rule, node, nodesById, visitedNodeIds, "Scale", "X", "x", "Object Width Size", "1"),
            "ObjectHeightSize" => ObjectVectorAxisExpression(rule, node, nodesById, visitedNodeIds, "Scale", "Y", "y", "Object Height Size", "1"),
            "ObjectDepthSize" => ObjectVectorAxisExpression(rule, node, nodesById, visitedNodeIds, "Scale", "Z", "z", "Object Depth Size", "1"),
            "ObjectColor" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Color", "Color", "Object Color", "return targetObject.Color"),
            "ObjectParent" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Parent", "SceneObject", "Object Parent", "return targetObject.Parent"),
            "ObjectVisibleValue" => ObjectVisibleValueExpression(rule, node, nodesById, visitedNodeIds),
            "ObjectCollisionValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "CanCollide", "Boolean", "Object Collision Value", "return targetObject.CanCollide == true"),
            "ObjectAnchoredValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Anchored", "Boolean", "Object Anchored Value", "return targetObject.Anchored == true"),
            "ObjectTransparency" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Transparency", "Number", "Object Transparency", "return tonumber(targetObject.Transparency) or 0"),
            _ => PropertyValueLiteral(node, expectedDataType, fallback)
        };

        visitedNodeIds.Remove(node.Id);
        return expression;
    }

    private static LuauExpression PropertyValueLiteral(RuleNode node, string dataType, string fallback)
    {
        var authoredParameter = node.Parameters.FirstOrDefault(parameter => parameter.Key.Equals("value", StringComparison.OrdinalIgnoreCase));
        var value = authoredParameter is null ? node.Value : EffectiveParameterValue(authoredParameter);
        return LiteralExpression(value, dataType, fallback);
    }

    private static LuauExpression ChooseObjectExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string expectedDataType)
    {
        var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Self", visitedNodeIds);
        if (NormalizeExpressionDataType(expectedDataType).Equals("SceneObject", StringComparison.OrdinalIgnoreCase))
        {
            return new LuauExpression($"resolveTarget(triggerObject, {target.Code})", "SceneObject");
        }

        return new LuauExpression(target.Code, "String");
    }

    private static LuauExpression RandomTrueOrFalseExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var trueChance = PropertyParameterExpression(rule, node, nodesById, "trueChance", "Number", "50", visitedNodeIds);
        return new LuauExpression($"(math.random() * 100 < math.max(0, math.min(100, {trueChance.Code})))", "Boolean");
    }

    private static LuauExpression NumberFromTextExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var text = PropertyParameterExpression(rule, node, nodesById, "text", "String", "0", visitedNodeIds);
        var fallbackNumber = PropertyParameterExpression(rule, node, nodesById, "fallback", "Number", "0", visitedNodeIds);
        return new LuauExpression($"(function() local parsed = tonumber(tostring({text.Code})); if parsed == nil then return {fallbackNumber.Code} end; return parsed end)()", "Number");
    }

    private static LuauExpression BinaryNumberExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string op)
    {
        var left = PropertyParameterExpression(rule, node, nodesById, "left", "Number", "0", visitedNodeIds);
        var right = PropertyParameterExpression(rule, node, nodesById, "right", "Number", "0", visitedNodeIds);
        return new LuauExpression($"({left.Code} {op} {right.Code})", "Number");
    }

    private static LuauExpression MinMaxNumberExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string functionName)
    {
        var left = PropertyParameterExpression(rule, node, nodesById, "left", "Number", "0", visitedNodeIds);
        var right = PropertyParameterExpression(rule, node, nodesById, "right", "Number", "0", visitedNodeIds);
        return new LuauExpression($"{functionName}({left.Code}, {right.Code})", "Number");
    }

    private static LuauExpression UnaryNumberFunctionExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string functionName)
    {
        var value = PropertyParameterExpression(rule, node, nodesById, "value", "Number", "0", visitedNodeIds);
        return new LuauExpression($"{functionName}({value.Code})", "Number");
    }

    private static LuauExpression PowerNumberExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var baseValue = PropertyParameterExpression(rule, node, nodesById, "base", "Number", "1", visitedNodeIds);
        var exponent = PropertyParameterExpression(rule, node, nodesById, "exponent", "Number", "1", visitedNodeIds);
        return new LuauExpression($"({baseValue.Code} ^ {exponent.Code})", "Number");
    }

    private static LuauExpression ModuloNumberExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var value = PropertyParameterExpression(rule, node, nodesById, "value", "Number", "0", visitedNodeIds);
        var divisor = PropertyParameterExpression(rule, node, nodesById, "divisor", "Number", "1", visitedNodeIds);
        return new LuauExpression($"(function() local divisor = {divisor.Code}; if divisor == 0 then print(\"Modulo Numbers stopped: divisor was zero.\"); return 0 end; return {value.Code} % divisor end)()", "Number");
    }

    private static LuauExpression LerpNumberExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var from = PropertyParameterExpression(rule, node, nodesById, "from", "Number", "0", visitedNodeIds);
        var to = PropertyParameterExpression(rule, node, nodesById, "to", "Number", "1", visitedNodeIds);
        var alpha = PropertyParameterExpression(rule, node, nodesById, "alpha", "Number", "0.5", visitedNodeIds);
        return new LuauExpression($"({from.Code} + (({to.Code} - {from.Code}) * {alpha.Code}))", "Number");
    }

    private static LuauExpression ClampNumberExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var value = PropertyParameterExpression(rule, node, nodesById, "value", "Number", "0", visitedNodeIds);
        var min = PropertyParameterExpression(rule, node, nodesById, "min", "Number", "0", visitedNodeIds);
        var max = PropertyParameterExpression(rule, node, nodesById, "max", "Number", "1", visitedNodeIds);
        return new LuauExpression($"math.max({min.Code}, math.min({max.Code}, {value.Code}))", "Number");
    }

    private static LuauExpression DivideNumberExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var numerator = PropertyParameterExpression(rule, node, nodesById, "numerator", "Number", "0", visitedNodeIds);
        var divisor = PropertyParameterExpression(rule, node, nodesById, "divisor", "Number", "1", visitedNodeIds);
        return new LuauExpression($"(function() local divisor = {divisor.Code}; if divisor == 0 then print(\"Divide Numbers stopped: division by zero.\"); return 0 end; return {numerator.Code} / divisor end)()", "Number");
    }

    private static LuauExpression AverageNumberExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var first = PropertyParameterExpression(rule, node, nodesById, "first", "Number", "0", visitedNodeIds);
        var second = PropertyParameterExpression(rule, node, nodesById, "second", "Number", "0", visitedNodeIds);
        return new LuauExpression($"(({first.Code} + {second.Code}) / 2)", "Number");
    }

    private static LuauExpression SquareRootNumberExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var value = PropertyParameterExpression(rule, node, nodesById, "value", "Number", "0", visitedNodeIds);
        return new LuauExpression($"math.sqrt(math.max(0, {value.Code}))", "Number");
    }

    private static LuauExpression RoundNumberExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var value = PropertyParameterExpression(rule, node, nodesById, "value", "Number", "0", visitedNodeIds);
        return new LuauExpression($"(function() local value = {value.Code}; if value >= 0 then return math.floor(value + 0.5) end; return math.ceil(value - 0.5) end)()", "Number");
    }

    private static LuauExpression RandomNumberExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var min = PropertyParameterExpression(rule, node, nodesById, "min", "Number", "0", visitedNodeIds);
        var max = PropertyParameterExpression(rule, node, nodesById, "max", "Number", "1", visitedNodeIds);
        return new LuauExpression($"(math.random() * ({max.Code} - {min.Code}) + {min.Code})", "Number");
    }

    private static LuauExpression RandomWholeNumberExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var min = PropertyParameterExpression(rule, node, nodesById, "min", "Number", "1", visitedNodeIds);
        var max = PropertyParameterExpression(rule, node, nodesById, "max", "Number", "10", visitedNodeIds);
        return new LuauExpression($"(function() local first = math.floor({min.Code}); local second = math.floor({max.Code}); if first > second then first, second = second, first end; return math.random(first, second) end)()", "Number");
    }

    private static LuauExpression RandomNumberChoiceExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var first = PropertyParameterExpression(rule, node, nodesById, "first", "Number", "1", visitedNodeIds);
        var second = PropertyParameterExpression(rule, node, nodesById, "second", "Number", "10", visitedNodeIds);
        var firstChance = PropertyParameterExpression(rule, node, nodesById, "firstChance", "Number", "50", visitedNodeIds);
        return new LuauExpression($"(function() local chancePercent = math.max(0, math.min(100, {firstChance.Code})); if math.random() * 100 < chancePercent then return {first.Code} end; return {second.Code} end)()", "Number");
    }

    private static LuauExpression JoinTextExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var first = PropertyParameterExpression(rule, node, nodesById, "first", "Any", "", visitedNodeIds);
        var separator = PropertyParameterExpression(rule, node, nodesById, "separator", "String", "", visitedNodeIds);
        var second = PropertyParameterExpression(rule, node, nodesById, "second", "Any", "", visitedNodeIds);
        return new LuauExpression($"(tostring({first.Code}) .. {separator.Code} .. tostring({second.Code}))", "String");
    }

    private static LuauExpression RandomTextChoiceExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var first = PropertyParameterExpression(rule, node, nodesById, "first", "String", "", visitedNodeIds);
        var second = PropertyParameterExpression(rule, node, nodesById, "second", "String", "", visitedNodeIds);
        var firstChance = PropertyParameterExpression(rule, node, nodesById, "firstChance", "Number", "50", visitedNodeIds);
        return new LuauExpression($"(function() local chancePercent = math.max(0, math.min(100, {firstChance.Code})); if math.random() * 100 < chancePercent then return tostring({first.Code}) end; return tostring({second.Code}) end)()", "String");
    }

    private static LuauExpression TextUnaryFunctionExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string functionName)
    {
        var text = PropertyParameterExpression(rule, node, nodesById, "text", "String", "", visitedNodeIds);
        return new LuauExpression($"{functionName}(tostring({text.Code}))", "String");
    }

    private static LuauExpression TrimTextExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var text = PropertyParameterExpression(rule, node, nodesById, "text", "String", "", visitedNodeIds);
        return new LuauExpression($"(string.match(tostring({text.Code}), \"^%s*(.-)%s*$\") or \"\")", "String");
    }

    private static LuauExpression ReplaceTextExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var text = PropertyParameterExpression(rule, node, nodesById, "text", "String", "", visitedNodeIds);
        var search = PropertyParameterExpression(rule, node, nodesById, "search", "String", "", visitedNodeIds);
        var replacement = PropertyParameterExpression(rule, node, nodesById, "replacement", "String", "", visitedNodeIds);
        return new LuauExpression($"(function() local textValue = tostring({text.Code}); local searchValue = tostring({search.Code}); if searchValue == \"\" then return textValue end; local pattern = string.gsub(searchValue, \"([%^%$%(%)%%%.%[%]%*%+%-%?])\", \"%%%1\"); local result = string.gsub(textValue, pattern, tostring({replacement.Code})); return result end)()", "String");
    }

    private static LuauExpression TextBeforeExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var text = PropertyParameterExpression(rule, node, nodesById, "text", "String", "", visitedNodeIds);
        var marker = PropertyParameterExpression(rule, node, nodesById, "marker", "String", "", visitedNodeIds);
        return new LuauExpression($"(function() local textValue = tostring({text.Code}); local markerValue = tostring({marker.Code}); if markerValue == \"\" then return textValue end; local pattern = string.gsub(markerValue, \"([%^%$%(%)%%%.%[%]%*%+%-%?])\", \"%%%1\"); local startIndex = string.find(textValue, pattern); if startIndex == nil then return \"\" end; return string.sub(textValue, 1, startIndex - 1) end)()", "String");
    }

    private static LuauExpression TextAfterExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var text = PropertyParameterExpression(rule, node, nodesById, "text", "String", "", visitedNodeIds);
        var marker = PropertyParameterExpression(rule, node, nodesById, "marker", "String", "", visitedNodeIds);
        return new LuauExpression($"(function() local textValue = tostring({text.Code}); local markerValue = tostring({marker.Code}); if markerValue == \"\" then return textValue end; local pattern = string.gsub(markerValue, \"([%^%$%(%)%%%.%[%]%*%+%-%?])\", \"%%%1\"); local _, endIndex = string.find(textValue, pattern); if endIndex == nil then return \"\" end; return string.sub(textValue, endIndex + 1) end)()", "String");
    }

    private static LuauExpression TextBetweenExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var text = PropertyParameterExpression(rule, node, nodesById, "text", "String", "", visitedNodeIds);
        var startMarker = PropertyParameterExpression(rule, node, nodesById, "startMarker", "String", "", visitedNodeIds);
        var endMarker = PropertyParameterExpression(rule, node, nodesById, "endMarker", "String", "", visitedNodeIds);
        return new LuauExpression($"(function() local textValue = tostring({text.Code}); local startMarker = tostring({startMarker.Code}); local endMarker = tostring({endMarker.Code}); local searchFrom = 1; if startMarker ~= \"\" then local _, startEnd = string.find(textValue, startMarker, 1, true); if startEnd == nil then return \"\" end; searchFrom = startEnd + 1 end; if endMarker == \"\" then return string.sub(textValue, searchFrom) end; local endStart = string.find(textValue, endMarker, searchFrom, true); if endStart == nil then return \"\" end; return string.sub(textValue, searchFrom, endStart - 1) end)()", "String");
    }

    private static LuauExpression TextCharactersExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        bool takeFirst)
    {
        var text = PropertyParameterExpression(rule, node, nodesById, "text", "String", "", visitedNodeIds);
        var count = PropertyParameterExpression(rule, node, nodesById, "count", "Number", "0", visitedNodeIds);
        var slice = takeFirst
            ? "return string.sub(textValue, 1, keepCount)"
            : "return string.sub(textValue, -keepCount)";
        return new LuauExpression($"(function() local textValue = tostring({text.Code}); local keepCount = math.max(0, math.floor({count.Code})); if keepCount <= 0 then return \"\" end; {slice} end)()", "String");
    }

    private static LuauExpression TextLengthExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var text = PropertyParameterExpression(rule, node, nodesById, "text", "String", "", visitedNodeIds);
        return new LuauExpression($"string.len(tostring({text.Code}))", "Number");
    }

    private static LuauExpression NumberToTextExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var value = PropertyParameterExpression(rule, node, nodesById, "value", "Number", "0", visitedNodeIds);
        return new LuauExpression($"tostring({value.Code})", "String");
    }

    private static LuauExpression BooleanBinaryExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string op)
    {
        var left = PropertyParameterExpression(rule, node, nodesById, "left", "Boolean", "false", visitedNodeIds);
        var right = PropertyParameterExpression(rule, node, nodesById, "right", "Boolean", "false", visitedNodeIds);
        return new LuauExpression($"(({left.Code}) {op} ({right.Code}))", "Boolean");
    }

    private static LuauExpression NotBooleanExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var value = PropertyParameterExpression(rule, node, nodesById, "value", "Boolean", "false", visitedNodeIds);
        return new LuauExpression($"(not ({value.Code}))", "Boolean");
    }

    private static LuauExpression ReadScriptVariableExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById)
    {
        return new LuauExpression($"VRS.vars{VariableKeyExpression(rule, node, nodesById, "name")}", "Any");
    }

    private static LuauExpression ReadStateExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById)
    {
        return new LuauExpression($"(VRS.states{VariableKeyExpression(rule, node, nodesById, "state")} == true)", "Boolean");
    }

    private static LuauExpression VectorFromXyzExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var x = PropertyParameterExpression(rule, node, nodesById, "x", "Number", "0", visitedNodeIds);
        var y = PropertyParameterExpression(rule, node, nodesById, "y", "Number", "0", visitedNodeIds);
        var z = PropertyParameterExpression(rule, node, nodesById, "z", "Number", "0", visitedNodeIds);
        return new LuauExpression($"makeVector3({x.Code}, {y.Code}, {z.Code})", "Vector3");
    }

    private static LuauExpression VectorBinaryExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string op)
    {
        var left = PropertyVectorParameterExpression(rule, node, nodesById, "left", "0", "0", "0", visitedNodeIds);
        var right = PropertyVectorParameterExpression(rule, node, nodesById, "right", "0", "0", "0", visitedNodeIds);
        return new LuauExpression($"(({left.Code}) {op} ({right.Code}))", "Vector3");
    }

    private static LuauExpression DirectionToObjectExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var from = PropertyParameterExpression(rule, node, nodesById, "from", "String", "Self", visitedNodeIds);
        var to = PropertyParameterExpression(rule, node, nodesById, "to", "String", "Target", visitedNodeIds);
        var code = $"(function() local fromObject = resolveTarget(triggerObject, {from.Code}); local toObject = resolveTarget(triggerObject, {to.Code}); if fromObject == nil or toObject == nil then print(\"Direction To Object stopped: an object was not found.\"); return makeVector3(0, 0, 0) end; if fromObject.Position == nil or toObject.Position == nil then print(\"Direction To Object stopped: an object has no Position.\"); return makeVector3(0, 0, 0) end; return toObject.Position - fromObject.Position end)()";
        return new LuauExpression(code, "Vector3");
    }

    private static LuauExpression RgbColorExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var red = PropertyParameterExpression(rule, node, nodesById, "r", "Number", "1", visitedNodeIds);
        var green = PropertyParameterExpression(rule, node, nodesById, "g", "Number", "1", visitedNodeIds);
        var blue = PropertyParameterExpression(rule, node, nodesById, "b", "Number", "1", visitedNodeIds);
        return new LuauExpression($"Color.New({red.Code}, {green.Code}, {blue.Code}, 1)", "Color");
    }

    private static LuauExpression ObjectValueExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string propertyName,
        string dataType,
        string readableName,
        string returnStatement)
    {
        var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Self", visitedNodeIds);
        var fallback = NormalizeExpressionDataType(dataType) switch
        {
            "String" => "\"\"",
            "Number" => "0",
            "Boolean" => "false",
            "Vector3" => "makeVector3(0, 0, 0)",
            "Color" => "Color.New(1, 1, 1, 1)",
            _ => "nil"
        };
        var code = $"(function() local targetObject = resolveTarget(triggerObject, {target.Code}); if targetObject == nil then print(\"{readableName} stopped: target was not found.\"); return {fallback} end; if targetObject.{propertyName} == nil then print(\"{readableName} stopped: target does not expose {propertyName}.\"); return {fallback} end; {returnStatement} end)()";
        return new LuauExpression(code, dataType);
    }

    private static LuauExpression ObjectPositionAxisExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string upperAxisName,
        string lowerAxisName,
        string readableName)
        => ObjectVectorAxisExpression(rule, node, nodesById, visitedNodeIds, "Position", upperAxisName, lowerAxisName, readableName);

    private static LuauExpression ObjectVectorAxisExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds,
        string propertyName,
        string upperAxisName,
        string lowerAxisName,
        string readableName,
        string fallback = "0")
    {
        var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Self", visitedNodeIds);
        var code = $"(function() local targetObject = resolveTarget(triggerObject, {target.Code}); if targetObject == nil then print(\"{readableName} stopped: target was not found.\"); return {fallback} end; if targetObject.{propertyName} == nil then print(\"{readableName} stopped: target does not expose {propertyName}.\"); return {fallback} end; return vrsValueAxis(targetObject.{propertyName}, \"{upperAxisName}\", \"{lowerAxisName}\", {fallback}) end)()";
        return new LuauExpression(code, "Number");
    }

    private static LuauExpression ObjectVisibleValueExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Self", visitedNodeIds);
        var code = $"(function() local targetObject = resolveTarget(triggerObject, {target.Code}); if targetObject == nil then print(\"Object Visible Value stopped: target was not found.\"); return false end; if targetObject.Visible ~= nil then return targetObject.Visible == true end; if targetObject.Transparency ~= nil then return targetObject.Transparency < 1 end; print(\"Object Visible Value stopped: target does not expose Visible or Transparency.\"); return false end)()";
        return new LuauExpression(code, "Boolean");
    }

    private static LuauExpression PropertyParameterExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        string key,
        string dataType,
        string fallback,
        HashSet<string> visitedNodeIds)
    {
        var incoming = FindIncomingValueConnection(rule, node.Id, key);
        if (incoming is not null && nodesById.TryGetValue(incoming.From.NodeId, out var sourceNode))
        {
            return ResolveSourceNodeExpression(rule, sourceNode, nodesById, NormalizeExpressionDataType(dataType), fallback, visitedNodeIds);
        }

        var authored = node.Parameters.FirstOrDefault(parameter => parameter.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (authored?.Binding.SourceKind == GraphValueSourceKind.CatalogValue)
        {
            return CatalogValueExpression(rule, authored.Binding, nodesById, dataType, fallback, visitedNodeIds);
        }

        var authoredValue = authored is null ? fallback : EffectiveParameterValue(authored);
        return LiteralExpression(authoredValue, dataType, fallback);
    }

    private static LuauExpression PropertyVectorParameterExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        string key,
        string fallbackX,
        string fallbackY,
        string fallbackZ,
        HashSet<string> visitedNodeIds)
    {
        var fallback = $"{fallbackX},{fallbackY},{fallbackZ}";
        var incoming = FindIncomingValueConnection(rule, node.Id, key);
        if (incoming is not null && nodesById.TryGetValue(incoming.From.NodeId, out var sourceNode))
        {
            return ResolveSourceNodeExpression(rule, sourceNode, nodesById, "Vector3", fallback, visitedNodeIds);
        }

        var authored = node.Parameters.FirstOrDefault(parameter => parameter.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (authored?.Binding.SourceKind == GraphValueSourceKind.CatalogValue)
        {
            return CatalogValueExpression(rule, authored.Binding, nodesById, "Vector3", fallback, visitedNodeIds);
        }

        var authoredValue = authored is null ? fallback : EffectiveParameterValue(authored);
        return LiteralExpression(authoredValue, "Vector3", fallback);
    }

    private static GraphConnection? FindIncomingValueConnection(Rule rule, string nodeId, string parameterKey)
    {
        var portId = GraphPortDefaults.ParameterPortId(parameterKey);
        return rule.Connections.LastOrDefault(connection =>
            connection.ConnectionKind != GraphConnectionKind.Flow &&
            string.Equals(connection.To.NodeId, nodeId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(connection.To.PortId, portId, StringComparison.OrdinalIgnoreCase));
    }

    private static string VariableKeyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        string parameterKey)
    {
        var name = ParameterExpression(rule, node, nodesById, parameterKey, "String", "");
        return IsStringLiteral(name.Code) ? $"[{name.Code}]" : $"[tostring({name.Code})]";
    }

    private static LuauExpression LiteralExpression(string value, string dataType, string fallback)
    {
        var normalized = NormalizeExpressionDataType(dataType);
        return normalized switch
        {
            "Number" => new LuauExpression(NumericLiteral(string.IsNullOrWhiteSpace(value) ? fallback : value, NumericLiteral(fallback, "0")), "Number"),
            "Boolean" => new LuauExpression(BooleanValue(string.IsNullOrWhiteSpace(value) ? fallback : value, fallback: false) ? "true" : "false", "Boolean"),
            "String" => new LuauExpression(LuauStringLiteral(value), "String"),
            "Quaternion" => new LuauExpression(QuaternionLiteral(string.IsNullOrWhiteSpace(value) ? fallback : value, fallback), "Quaternion"),
            "ColorSeries" => new LuauExpression(ColorSeriesLiteral(), "ColorSeries"),
            "Vector2" => new LuauExpression(Vector2Literal(string.IsNullOrWhiteSpace(value) ? fallback : value, fallback), "Vector2"),
            "Vector3" => new LuauExpression(VectorLiteral(string.IsNullOrWhiteSpace(value) ? fallback : value, fallback), "Vector3"),
            "Color" => new LuauExpression(ColorLiteral(string.IsNullOrWhiteSpace(value) ? fallback : value, fallback), "Color"),
            _ => new LuauExpression(InferAnyLiteral(value), "Any")
        };
    }

    private static string Vector2Literal(string value, string fallback)
    {
        var components = ParsePair(value, fallback, ["0", "0"]);
        return $"makeVector2({components[0]}, {components[1]})";
    }

    private static string QuaternionLiteral(string value, string fallback)
    {
        var components = ParseTuple(value, fallback, ["0", "0", "0", "1"], 4);
        return $"makeQuaternion({components[0]}, {components[1]}, {components[2]}, {components[3]})";
    }

    private static string ColorSeriesLiteral()
        => "vrsColorSeriesFromColors(Color.New(1, 1, 1, 1), Color.New(0, 0, 0, 1))";

    private static string VectorLiteral(string value, string fallback)
    {
        var components = ParseTriple(value, fallback, ["0", "0", "0"]);
        return $"makeVector3({components[0]}, {components[1]}, {components[2]})";
    }

    private static string ColorLiteral(string value, string fallback)
    {
        var components = ParseTriple(value, fallback, ["1", "1", "1"]);
        return $"Color.New({components[0]}, {components[1]}, {components[2]}, 1)";
    }

    private static string[] ParseTriple(string value, string fallback, string[] componentFallbacks)
        => ParseTuple(value, fallback, componentFallbacks, 3);

    private static string[] ParsePair(string value, string fallback, string[] componentFallbacks)
        => ParseTuple(value, fallback, componentFallbacks, 2);

    private static string[] ParseTuple(string value, string fallback, string[] componentFallbacks, int maxComponents)
    {
        var source = string.IsNullOrWhiteSpace(value) ? fallback : value;
        var parts = source
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(maxComponents)
            .ToArray();

        return Enumerable.Range(0, maxComponents)
            .Select(index => index < parts.Length
                ? NumericLiteral(parts[index], componentFallbacks[index])
                : componentFallbacks[index])
            .ToArray();
    }

    private static string InferAnyLiteral(string value)
    {
        if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (bool.TryParse(value, out var boolean))
        {
            return boolean ? "true" : "false";
        }

        return LuauStringLiteral(value);
    }

    private static string NormalizeExpressionDataType(string? value)
        => CatalogDataTypeNormalizer.NormalizeValueType(value);

    private static bool IsStringLiteral(string code)
    {
        return code.Length >= 2 && code[0] == '"' && code[^1] == '"';
    }
}
