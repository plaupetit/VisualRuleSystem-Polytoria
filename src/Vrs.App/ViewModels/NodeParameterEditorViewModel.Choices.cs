using Vrs.App.Icons;
using Vrs.Core.Catalog;
using Vrs.Core.ProjectInputs;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

public sealed partial class NodeParameterEditorViewModel
{
    // Choice factory helpers for source kinds, catalog options, scene objects, and variable scopes.
    private static IEnumerable<string> BuildOptions(
        RuleParameter parameter,
        NodeCatalogParameterDefinition? definition,
        IEnumerable<SceneObject> sceneObjects,
        IReadOnlyList<VrsInputActionChoice> inputActionChoices,
        VrsInputActionType? inputActionType)
    {
        if (inputActionType is { } requiredInputType)
        {
            var options = inputActionChoices
                .Where(choice => choice.Type == requiredInputType)
                .Select(choice => choice.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            return EnsureCurrentValue(parameter.Value, options);
        }

        if (definition is not null && NodeCatalogService.IsSceneObjectParameter(definition))
        {
            return EnsureCurrentValue(parameter.Value, new[]
            {
                "Self",
                "Parent",
                "Triggering Object",
                "Target",
                "Player",
                "Selected Object"
            }.Concat(CompatibleSceneObjectPaths(definition, sceneObjects)).Distinct(StringComparer.OrdinalIgnoreCase));
        }

        if (LooksLikeBoolean(definition?.Type) || LooksLikeBoolean(definition?.Control))
        {
            return EnsureCurrentValue(parameter.Value, ["true", "false"]);
        }

        if (definition?.Options.Count > 0)
        {
            return EnsureCurrentValue(parameter.Value, definition.Options);
        }

        string[] inferred = parameter.Key.ToLowerInvariant() switch
        {
            "operator" => [">", ">=", "==", "~=", "<=", "<"],
            "axis" => ["X", "Y", "Z"],
            "space" => ["World", "Local"],
            "movemode" => ["Tween", "Constant"],
            "transition" => ["Linear", "Sine", "Quad", "Cubic"],
            "direction" => ["InOut", "In", "Out"],
            _ => []
        };

        return inferred.Length == 0 ? [] : EnsureCurrentValue(parameter.Value, inferred);
    }

    private static IEnumerable<ValueSourceChoiceViewModel> BuildSourceKindChoices(IEnumerable<GraphValueSourceKind> sourceKinds)
    {
        return sourceKinds.Select(sourceKind => new ValueSourceChoiceViewModel(
            sourceKind.ToString(),
            SourceKindLabel(sourceKind),
            SourceKindCategory(sourceKind),
            SourceKindDescription(sourceKind),
            IconRegistry.ForValueSource(sourceKind),
            SourceKindKeywords(sourceKind)));
    }

    private static IEnumerable<ParameterChoiceViewModel> BuildParameterChoices(
        RuleParameter parameter,
        NodeCatalogParameterDefinition? definition,
        IEnumerable<string> options,
        IReadOnlyList<VrsInputActionChoice> inputActionChoices,
        VrsInputActionType? inputActionType)
    {
        var details = definition?.OptionDetails
            .Where(detail => !string.IsNullOrWhiteSpace(detail.Value))
            .ToDictionary(detail => detail.Value, detail => detail, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, NodeCatalogOptionDetail>(StringComparer.OrdinalIgnoreCase);
        var icon = IconRegistry.ForParameterType(definition?.Type ?? "String", definition?.Control ?? "");
        var inputChoices = inputActionChoices
            .Where(choice => inputActionType is null || choice.Type == inputActionType)
            .GroupBy(choice => choice.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var option in options)
        {
            details.TryGetValue(option, out var detail);
            inputChoices.TryGetValue(option, out var inputChoice);
            yield return new ParameterChoiceViewModel(
                option,
                FirstNonEmpty(detail?.Label, HumanOptionLabel(parameter.Key, option)),
                FirstNonEmpty(detail?.Category, InputChoiceCategory(inputChoice), OptionCategory(parameter.Key, option)),
                FirstNonEmpty(detail?.Description, InputChoiceDescription(inputChoice), OptionDescription(parameter.Key, option)),
                icon,
                detail?.SearchKeywords ?? InputChoiceKeywords(inputChoice).Concat(OptionKeywords(parameter.Key, option)));
        }
    }

    private static IEnumerable<ParameterChoiceViewModel> BuildSceneObjectChoices(
        RuleParameter parameter,
        NodeCatalogParameterDefinition? definition,
        IEnumerable<SceneObject> sceneObjects)
    {
        var contextValues = new[]
        {
            "Self",
            "Parent",
            "Triggering Object",
            "Target",
            "Player",
            "Selected Object"
        };

        var compatibleSceneObjects = sceneObjects
            .Where(sceneObject => definition is null || SceneObjectKindTaxonomy.Matches(sceneObject, definition))
            .GroupBy(sceneObject => sceneObject.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(sceneObject => SceneObjectKindTaxonomy.SceneRoot(sceneObject), StringComparer.OrdinalIgnoreCase)
            .ThenBy(sceneObject => sceneObject.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var values = EnsureCurrentValue(
            parameter.Value,
            contextValues.Concat(compatibleSceneObjects.Select(sceneObject => sceneObject.Path)).Distinct(StringComparer.OrdinalIgnoreCase));

        foreach (var value in values)
        {
            var sceneObject = compatibleSceneObjects.FirstOrDefault(item => item.Path.Equals(value, StringComparison.OrdinalIgnoreCase));
            var category = SceneObjectCategory(value, sceneObject, definition);
            yield return new ParameterChoiceViewModel(
                value,
                SceneObjectLabel(value, sceneObject),
                category,
                SceneObjectDescription(value, sceneObject, definition),
                IconRegistry.ForSceneKind(sceneObject?.Kind ?? category),
                SceneObjectKeywords(value, sceneObject, definition));
        }
    }

    private static IEnumerable<string> CompatibleSceneObjectPaths(
        NodeCatalogParameterDefinition definition,
        IEnumerable<SceneObject> sceneObjects)
    {
        return sceneObjects
            .Where(sceneObject => SceneObjectKindTaxonomy.Matches(sceneObject, definition))
            .Select(sceneObject => sceneObject.Path)
            .Where(path => !string.IsNullOrWhiteSpace(path));
    }

    private static IEnumerable<VariableScopeChoiceViewModel> BuildVariableScopeChoices(IEnumerable<GraphVariableScope> scopes)
    {
        foreach (var scope in scopes)
        {
            yield return new VariableScopeChoiceViewModel(
                scope.ToString(),
                VariableScopeLabel(scope),
                VariableScopeCategory(scope),
                VariableScopeDescription(scope),
                VariableScopeIcon(scope),
                [scope.ToString(), VariableScopeLabel(scope)]);
        }
    }

    private static IEnumerable<string> BuildValueSourceOptions(RuleParameter parameter, NodeCatalogParameterDefinition? definition)
    {
        var options = new[]
        {
            definition?.ValueSource,
            parameter.ValueSource,
            "Manual Value",
            "Suggested Value",
            "Scene Object Picker",
            "Target Context"
        };

        return options
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Where(value => !value!.Equals("Connected Port", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(value => value!);
    }

    private static IEnumerable<GraphValueSourceKind> BuildSourceKindOptions(
        NodeCatalogParameterDefinition? definition,
        IReadOnlyCollection<NodeCatalogEntry> catalogEntries,
        int recipeDepth)
    {
        var recipeAvailable = recipeDepth < 3 && HasRecipeChoices(catalogEntries, definition?.Type ?? "Any");
        var hinted = definition?.SelectorHints
            .SelectMany(hint => hint.AllowedSources)
            .Where(IsInspectorSourceKind)
            .Distinct()
            .ToList() ?? [];
        if (hinted.Count > 0)
        {
            if (recipeAvailable && !hinted.Contains(GraphValueSourceKind.CatalogValue))
            {
                hinted.Add(GraphValueSourceKind.CatalogValue);
            }

            return hinted;
        }

        if (definition is not null && NodeCatalogService.IsTriggeringPlayerParameter(definition))
        {
            var playerSources = new List<GraphValueSourceKind>
            {
                GraphValueSourceKind.TriggeringPlayer,
                GraphValueSourceKind.Constant,
                GraphValueSourceKind.LocalVariable,
                GraphValueSourceKind.GlobalVariable
            };
            if (recipeAvailable)
            {
                playerSources.Add(GraphValueSourceKind.CatalogValue);
            }

            return playerSources;
        }

        if (definition is not null && NodeCatalogService.IsSceneObjectParameter(definition))
        {
            return
            [
                GraphValueSourceKind.Self,
                GraphValueSourceKind.Target,
                GraphValueSourceKind.SceneObject,
                GraphValueSourceKind.LocalVariable,
                GraphValueSourceKind.GlobalVariable
            ];
        }

        var sources = new List<GraphValueSourceKind>
        {
            GraphValueSourceKind.Constant,
            GraphValueSourceKind.LocalVariable,
            GraphValueSourceKind.GlobalVariable
        };
        if (recipeAvailable)
        {
            sources.Add(GraphValueSourceKind.CatalogValue);
        }

        return sources;
    }

    private static IEnumerable<GraphVariableScope> BuildVariableScopeOptions(NodeCatalogParameterDefinition? definition)
    {
        var hinted = definition?.SelectorHints
            .SelectMany(hint => hint.AllowedScopes)
            .Distinct()
            .ToList() ?? [];
        if (hinted.Count > 0)
        {
            return hinted;
        }

        return
        [
            GraphVariableScope.Script,
            GraphVariableScope.State,
            GraphVariableScope.Graph,
            GraphVariableScope.Global
        ];
    }

    private static IEnumerable<ParameterChoiceViewModel> BuildRecipeChoices(
        IReadOnlyCollection<NodeCatalogEntry> catalogEntries,
        string expectedType)
    {
        foreach (var entry in RecipeEntries(catalogEntries, expectedType))
        {
            var path = NodeCatalogPresentationService.GetPalettePath(entry);
            var category = path.Count == 0 ? "Values" : string.Join(" / ", path);
            yield return new ParameterChoiceViewModel(
                entry.IdBase,
                entry.Label,
                category,
                NodeCatalogPresentationService.GetBeginnerSummary(entry),
                IconRegistry.ForParameterType(RecipeOutputDataType(entry), ""),
                NodeCatalogPresentationService.GetSearchTerms(entry));
        }
    }

    private static bool HasRecipeChoices(IReadOnlyCollection<NodeCatalogEntry> catalogEntries, string expectedType)
    {
        return RecipeEntries(catalogEntries, expectedType).Any();
    }

    private static IEnumerable<NodeCatalogEntry> RecipeEntries(
        IReadOnlyCollection<NodeCatalogEntry> catalogEntries,
        string expectedType)
    {
        return catalogEntries
            .Where(entry => entry.Kind == NodeKind.Property)
            .Where(NodeCatalogService.IsAddable)
            .Where(NodeCatalogPresentationService.IsDefaultPaletteSurface)
            .Where(entry => !IsManualPrimitiveRecipe(entry))
            .Where(entry => RecipeTypeMatches(expectedType, RecipeOutputDataType(entry)))
            .OrderBy(entry => string.Join(" / ", NodeCatalogPresentationService.GetPalettePath(entry)), StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase);
    }

    private static string RecipeOutputDataType(NodeCatalogEntry entry)
    {
        return entry.Ports.FirstOrDefault(port =>
            port.PortKind == NodePortKind.Value &&
            port.Direction == NodePortDirection.Output)?.DataType ??
            (string.IsNullOrWhiteSpace(entry.ApiType) ? "Any" : entry.ApiType);
    }

    private static bool IsManualPrimitiveRecipe(NodeCatalogEntry entry)
    {
        return entry.Type.Equals("ManualText", StringComparison.OrdinalIgnoreCase) ||
            entry.Type.Equals("ManualNumber", StringComparison.OrdinalIgnoreCase) ||
            entry.Type.Equals("ManualBoolean", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RecipeTypeMatches(string expectedType, string actualType)
    {
        var expected = NormalizeRecipeDataType(expectedType);
        var actual = NormalizeRecipeDataType(actualType);
        return expected == "Any" || actual == "Any" || expected.Equals(actual, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRecipeDataType(string? value)
        => CatalogDataTypeNormalizer.NormalizeValueType(value);

    private static string DisplayRecipeDataType(string? value)
        => NodeCatalogPresentationService.GetDataTypeLabel(value);

    private static bool TryResolveInputActionType(
        string nodeType,
        RuleParameter parameter,
        NodeCatalogParameterDefinition? definition,
        out VrsInputActionType type)
    {
        var key = parameter.Key;
        var isInputActionKey =
            key.Equals("actionName", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("inputAction", StringComparison.OrdinalIgnoreCase);
        if (!isInputActionKey)
        {
            type = default;
            return false;
        }

        if (nodeType.Equals("OnInputButtonDown", StringComparison.OrdinalIgnoreCase) ||
            nodeType.Equals("InputButtonDown", StringComparison.OrdinalIgnoreCase) ||
            nodeType.Equals("SendInputEvent", StringComparison.OrdinalIgnoreCase) ||
            nodeType.Equals("OnVrsInputEvent", StringComparison.OrdinalIgnoreCase))
        {
            type = VrsInputActionType.Button;
            return true;
        }

        if (nodeType.Equals("InputAxisValue", StringComparison.OrdinalIgnoreCase))
        {
            type = VrsInputActionType.Axis;
            return true;
        }

        if (nodeType.Equals("InputVectorX", StringComparison.OrdinalIgnoreCase) ||
            nodeType.Equals("InputVectorY", StringComparison.OrdinalIgnoreCase))
        {
            type = VrsInputActionType.Vector2;
            return true;
        }

        if ((definition?.Description ?? "").Contains("input", StringComparison.OrdinalIgnoreCase))
        {
            type = VrsInputActionType.Button;
            return true;
        }

        type = default;
        return false;
    }

    private static string InputChoiceCategory(VrsInputActionChoice? choice)
    {
        if (choice is null)
        {
            return "";
        }

        return choice.Source switch
        {
            "Project+Preset" => "Project Input / VRS Preset",
            "Project" => "Project Input",
            _ => "VRS Preset"
        };
    }

    private static string InputChoiceDescription(VrsInputActionChoice? choice)
    {
        if (choice is null)
        {
            return "";
        }

        var source = choice.Source switch
        {
            "Project+Preset" => "project action and VRS preset",
            "Project" => "project action",
            _ => "VRS preset"
        };
        return choice.Type == VrsInputActionType.Button
            ? $"{source}; server event: {choice.EventPath}"
            : $"{source}; {choice.Type} input action.";
    }

    private static IEnumerable<string> InputChoiceKeywords(VrsInputActionChoice? choice)
    {
        if (choice is null)
        {
            return [];
        }

        return [choice.Source, choice.Type.ToString(), choice.EventName, choice.EventPath];
    }
}
