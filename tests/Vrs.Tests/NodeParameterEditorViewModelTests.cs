using Avalonia.Media;
using Vrs.App.Icons;
using Vrs.App.ViewModels;
using Vrs.Core.Catalog;
using Vrs.Core.ProjectInputs;
using Vrs.Graph.Model;

namespace Vrs.Tests;

public sealed class NodeParameterEditorViewModelTests
{
    [Fact]
    public void SourceKindChoices_HaveReadableLabelsCategoriesAndTooltips()
    {
        var parameter = new RuleParameter
        {
            Key = "target",
            Value = "Self",
            Binding = new GraphValueBinding { SourceKind = GraphValueSourceKind.Constant, ConstantValue = "Self" }
        };
        var definition = new NodeCatalogParameterDefinition
        {
            Key = "target",
            Label = "Target Object",
            Type = "Object Path",
            Control = "SceneObject",
            SelectorHints =
            [
                new NodeCatalogSelectorHint
                {
                    AllowedSources =
                    [
                        GraphValueSourceKind.Constant,
                        GraphValueSourceKind.Self,
                        GraphValueSourceKind.Target,
                        GraphValueSourceKind.SceneObject,
                        GraphValueSourceKind.LocalVariable,
                        GraphValueSourceKind.GlobalVariable,
                        GraphValueSourceKind.ConnectedPort
                    ]
                }
            ]
        };

        var editor = new NodeParameterEditorViewModel(parameter, definition, [], () => { });

        Assert.DoesNotContain(editor.SourceKindChoices, choice => choice.Value == nameof(GraphValueSourceKind.ConnectedPort));
        Assert.All(editor.SourceKindChoices, choice =>
        {
            Assert.False(string.IsNullOrWhiteSpace(choice.Label));
            Assert.False(string.IsNullOrWhiteSpace(choice.Category));
            Assert.False(string.IsNullOrWhiteSpace(choice.Description));
            Assert.False(string.IsNullOrWhiteSpace(choice.Tooltip));
            Assert.False(string.IsNullOrWhiteSpace(choice.SearchText));
        });

        var manual = editor.SourceKindChoices.Single(choice => choice.Value == nameof(GraphValueSourceKind.Constant));
        Assert.Equal("Manual Value", manual.Label);
        Assert.Equal("Direct Value", manual.Category);
        Assert.Contains("typed in this node", manual.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManualValueTooltips_ExplainTypedValueWithoutInternalConstantLabel()
    {
        var parameter = new RuleParameter
        {
            Key = "intervalSeconds",
            Value = "1",
            Binding = new GraphValueBinding
            {
                SourceKind = GraphValueSourceKind.Constant,
                ConstantValue = "1"
            }
        };
        var definition = new NodeCatalogParameterDefinition
        {
            Key = "intervalSeconds",
            Label = "Interval Seconds",
            Type = "Number",
            Required = true
        };

        var editor = new NodeParameterEditorViewModel(parameter, definition, [], () => { });

        Assert.Equal("Typed here: 1", editor.PreviewText);
        Assert.Contains("uses exactly what you type", editor.SourceIconTooltip, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not read a variable", editor.ValueInputTooltip, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Constant:", editor.SourceIconTooltip);
        Assert.DoesNotContain("Value: Constant", editor.SourceIconTooltip);
    }

    [Fact]
    public void KillPlayerPlayerSource_DefaultsToTriggeringPlayerAndKeepsManualAvailable()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var killPlayer = catalog.Nodes.Single(node => node.IdBase == "ACT_KillPlayer");
        var node = NodeCatalogService.CreateNode(killPlayer);
        var parameter = node.Parameters.Single(item => item.Key == "player");
        var definition = killPlayer.Parameters.Single(item => item.Key == "player");

        var editor = new NodeParameterEditorViewModel(parameter, definition, [], catalog.Nodes, () => { });

        Assert.Equal(GraphValueSourceKind.TriggeringPlayer, editor.SourceKind);
        Assert.Equal("Trigger context: Player", editor.PreviewText);
        Assert.Equal(nameof(GraphValueSourceKind.TriggeringPlayer), editor.SourceKindChoices.First().Value);
        Assert.Contains(editor.SourceKindChoices, choice =>
            choice.Value == nameof(GraphValueSourceKind.TriggeringPlayer) &&
            choice.Category == "Compatible" &&
            choice.Label == "Triggering Player");
        Assert.Contains(editor.SourceKindChoices, choice =>
            choice.Value == nameof(GraphValueSourceKind.Constant) &&
            choice.Label == "Manual Value");
    }

    [Fact]
    public void ParameterChoices_KeepSimpleOptionsAndUseOptionDetails()
    {
        var parameter = new RuleParameter
        {
            Key = "operator",
            Value = "legacy",
            Binding = new GraphValueBinding { SourceKind = GraphValueSourceKind.Constant, ConstantValue = "legacy" }
        };
        var definition = new NodeCatalogParameterDefinition
        {
            Key = "operator",
            Label = "Operator",
            Type = "String",
            Control = "Choice",
            Options = ["==", "magic"],
            OptionDetails =
            [
                new NodeCatalogOptionDetail
                {
                    Value = "magic",
                    Label = "Magic Compare",
                    Category = "Custom Operators",
                    Description = "Use the catalog-defined comparison behavior.",
                    SearchKeywords = ["spell", "custom"]
                }
            ]
        };

        var editor = new NodeParameterEditorViewModel(parameter, definition, [], () => { });

        Assert.Contains(editor.OptionChoices, choice => choice.Value == "==");
        Assert.Contains(editor.OptionChoices, choice => choice.Value == "legacy");
        var equality = editor.OptionChoices.Single(choice => choice.Value == "==");
        Assert.Equal("Equals", equality.Label);
        Assert.Equal("Comparison", equality.Category);

        var detailed = editor.OptionChoices.Single(choice => choice.Value == "magic");
        Assert.Equal("Magic Compare", detailed.Label);
        Assert.Equal("Custom Operators", detailed.Category);
        Assert.Contains("catalog-defined", detailed.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("spell", detailed.SearchText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InputActionChoices_CanFilterProjectActionsAndVrsPresets()
    {
        var parameter = new RuleParameter
        {
            Key = "inputAction",
            Value = "Jump",
            Binding = new GraphValueBinding
            {
                SourceKind = GraphValueSourceKind.Constant,
                ConstantValue = "Jump"
            }
        };
        var definition = new NodeCatalogParameterDefinition
        {
            Key = "inputAction",
            Label = "Input Action",
            Type = "String",
            Control = "Choice"
        };
        var choices = new[]
        {
            VrsInputPresetCatalog.ToChoice("Jump", VrsInputActionType.Button, "Project+Preset"),
            VrsInputPresetCatalog.ToChoice("Interact", VrsInputActionType.Button, "Preset"),
            VrsInputPresetCatalog.ToChoice("CustomDance", VrsInputActionType.Button, "Project")
        };

        var editor = new NodeParameterEditorViewModel(
            parameter,
            definition,
            [],
            [],
            [],
            () => { },
            nodeType: "SendInputEvent",
            inputActionChoices: choices);

        Assert.True(editor.ShowsInputChoiceSourceFilter);
        Assert.Contains(editor.OptionChoices, choice => choice.Value == "Jump" && choice.Category == "Project Input / VRS Preset");
        Assert.Contains(editor.OptionChoices, choice => choice.Value == "Interact" && choice.Category == "VRS Preset");

        editor.InputChoiceSourceFilter = "Project";

        Assert.Contains(editor.OptionChoices, choice => choice.Value == "Jump");
        Assert.Contains(editor.OptionChoices, choice => choice.Value == "CustomDance");
        Assert.DoesNotContain(editor.OptionChoices, choice => choice.Value == "Interact");

        editor.InputChoiceSourceFilter = "Presets";

        Assert.Contains(editor.OptionChoices, choice => choice.Value == "Jump");
        Assert.Contains(editor.OptionChoices, choice => choice.Value == "Interact");
        Assert.DoesNotContain(editor.OptionChoices, choice => choice.Value == "CustomDance");
    }

    [Fact]
    public void MoveModeConstantOption_IsPresentedAsContinuousGameplayBehavior()
    {
        var parameter = new RuleParameter
        {
            Key = "moveMode",
            Value = "Constant",
            Binding = new GraphValueBinding { SourceKind = GraphValueSourceKind.Constant, ConstantValue = "Constant" }
        };
        var definition = new NodeCatalogParameterDefinition
        {
            Key = "moveMode",
            Label = "Move Mode",
            Type = "String",
            Control = "Choice"
        };

        var editor = new NodeParameterEditorViewModel(parameter, definition, [], () => { });

        var continuous = editor.OptionChoices.Single(choice => choice.Value == "Constant");
        Assert.Equal("Continuous", continuous.Label);
        Assert.Equal("Movement Mode", continuous.Category);
        Assert.Contains("repeated", continuous.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("seamless", continuous.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VisibleWhen_UsesSiblingParameterValues()
    {
        var motionMode = new RuleParameter
        {
            Key = "motionMode",
            Value = "Instant",
            Binding = new GraphValueBinding { SourceKind = GraphValueSourceKind.Constant, ConstantValue = "Instant" }
        };
        var duration = new RuleParameter
        {
            Key = "duration",
            Value = "1",
            Binding = new GraphValueBinding { SourceKind = GraphValueSourceKind.Constant, ConstantValue = "1" }
        };
        var definition = new NodeCatalogParameterDefinition
        {
            Key = "duration",
            Label = "Duration Seconds",
            Type = "Number",
            Control = "Number",
            VisibleWhen =
            [
                new NodeCatalogParameterVisibilityCondition
                {
                    ParameterKey = "motionMode",
                    EqualsValue = "Smooth"
                }
            ]
        };

        var editor = new NodeParameterEditorViewModel(
            duration,
            definition,
            [],
            [],
            [motionMode, duration],
            () => { });

        Assert.False(editor.IsVisible);

        motionMode.Value = "Smooth";

        Assert.True(editor.IsVisible);
    }

    [Fact]
    public void SceneObjectChoices_AreCategorizedByHierarchyRoot()
    {
        var parameter = new RuleParameter
        {
            Key = "target",
            Value = "Self",
            Binding = new GraphValueBinding { SourceKind = GraphValueSourceKind.Self, ConstantValue = "Self" }
        };
        var definition = new NodeCatalogParameterDefinition
        {
            Key = "target",
            Label = "Target Object",
            Type = "Object Path",
            Control = "SceneObject"
        };

        var editor = new NodeParameterEditorViewModel(
            parameter,
            definition,
            [
                Scene("World/Environment/Tree", "Tree", "Part"),
                Scene("World/Hidden/VRS_Demo", "VRS_Demo", "ServerScript"),
                Scene("World/PlayerGUI/MainHud", "MainHud", "ScreenGui")
            ],
            () => { });

        Assert.Equal("Object Context", editor.SceneObjectChoices.Single(choice => choice.Value == "Self").Category);
        Assert.Equal("World / Environment", editor.SceneObjectChoices.Single(choice => choice.Value == "World/Environment/Tree").Category);
        Assert.Equal("World / Hidden", editor.SceneObjectChoices.Single(choice => choice.Value == "World/Hidden/VRS_Demo").Category);
        Assert.Equal("PlayerGUI", editor.SceneObjectChoices.Single(choice => choice.Value == "World/PlayerGUI/MainHud").Category);
    }

    [Fact]
    public void SceneObjectChoices_FilterPartLikeObjects()
    {
        var parameter = new RuleParameter
        {
            Key = "target",
            Value = "Self",
            Binding = new GraphValueBinding { SourceKind = GraphValueSourceKind.Self, ConstantValue = "Self" }
        };
        var definition = new NodeCatalogParameterDefinition
        {
            Key = "target",
            Label = "Target Object",
            Type = "Object Path",
            Control = "SceneObject",
            AcceptedObjectGroups = ["PartLike"]
        };

        var editor = new NodeParameterEditorViewModel(
            parameter,
            definition,
            [
                Scene("World/Environment/MovingPart", "MovingPart", "Part"),
                Scene("World/PlayerGUI/MainHud", "MainHud", "ScreenGui")
            ],
            () => { });

        Assert.Contains(editor.SceneObjectChoices, choice => choice.Value == "World/Environment/MovingPart");
        Assert.DoesNotContain(editor.SceneObjectChoices, choice => choice.Value == "World/PlayerGUI/MainHud");
        Assert.Contains("Part-like objects", editor.SceneObjectPickerTooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void SceneObjectChoices_FilterUiButtonsToUiRoots()
    {
        var parameter = new RuleParameter
        {
            Key = "target",
            Value = "Self",
            Binding = new GraphValueBinding { SourceKind = GraphValueSourceKind.Self, ConstantValue = "Self" }
        };
        var definition = new NodeCatalogParameterDefinition
        {
            Key = "target",
            Label = "Target Button",
            Type = "Object Path",
            Control = "SceneObject",
            AcceptedObjectGroups = ["UIButton2D"],
            AcceptedSceneRoots = ["PlayerGUI", "CoreUI"]
        };

        var editor = new NodeParameterEditorViewModel(
            parameter,
            definition,
            [
                Scene("World/PlayerGUI/StartButton", "StartButton", "TextButton"),
                Scene("World/CoreUI/CloseButton", "CloseButton", "ImageButton"),
                Scene("World/Environment/FakeButton", "FakeButton", "TextButton"),
                Scene("World/PlayerGUI/Panel", "Panel", "Frame")
            ],
            () => { });

        Assert.Contains(editor.SceneObjectChoices, choice => choice.Value == "World/PlayerGUI/StartButton" && choice.Category == "PlayerGUI");
        Assert.Contains(editor.SceneObjectChoices, choice => choice.Value == "World/CoreUI/CloseButton" && choice.Category == "CoreUI");
        Assert.DoesNotContain(editor.SceneObjectChoices, choice => choice.Value == "World/Environment/FakeButton");
        Assert.DoesNotContain(editor.SceneObjectChoices, choice => choice.Value == "World/PlayerGUI/Panel");
    }

    [Fact]
    public void SceneObjectChoices_KeepCurrentIncompatibleValueVisible()
    {
        var parameter = new RuleParameter
        {
            Key = "target",
            Value = "World/PlayerGUI/MainHud",
            Binding = new GraphValueBinding
            {
                SourceKind = GraphValueSourceKind.SceneObject,
                SceneObjectPath = "World/PlayerGUI/MainHud"
            }
        };
        var definition = new NodeCatalogParameterDefinition
        {
            Key = "target",
            Label = "Target Object",
            Type = "Object Path",
            Control = "SceneObject",
            AcceptedObjectGroups = ["PartLike"]
        };

        var editor = new NodeParameterEditorViewModel(
            parameter,
            definition,
            [
                Scene("World/Environment/MovingPart", "MovingPart", "Part"),
                Scene("World/PlayerGUI/MainHud", "MainHud", "ScreenGui")
            ],
            () => { });

        var current = editor.SceneObjectChoices.Single(choice => choice.Value == "World/PlayerGUI/MainHud");
        Assert.Equal("Current incompatible value", current.Category);
        Assert.Contains("not in the compatible", current.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VariableScopeChoices_HaveHumanDescriptions()
    {
        var parameter = new RuleParameter
        {
            Key = "message",
            Value = "DebugText",
            Binding = new GraphValueBinding
            {
                SourceKind = GraphValueSourceKind.LocalVariable,
                VariableName = "DebugText",
                VariableScope = GraphVariableScope.Script
            }
        };

        var editor = new NodeParameterEditorViewModel(parameter, new NodeCatalogParameterDefinition(), [], () => { });

        Assert.Contains(editor.VariableScopeChoices, choice => choice.Value == nameof(GraphVariableScope.Script));
        Assert.All(editor.VariableScopeChoices, choice =>
        {
            Assert.False(string.IsNullOrWhiteSpace(choice.Label));
            Assert.False(string.IsNullOrWhiteSpace(choice.Category));
            Assert.False(string.IsNullOrWhiteSpace(choice.Tooltip));
        });
    }

    [Fact]
    public void ValueRecipes_AreAvailableFromCompatibleParameters()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var parameter = new RuleParameter
        {
            Key = "amount",
            Value = "1",
            Binding = new GraphValueBinding
            {
                SourceKind = GraphValueSourceKind.Constant,
                ConstantValue = "1"
            }
        };
        var definition = new NodeCatalogParameterDefinition
        {
            Key = "amount",
            Label = "Amount",
            Type = "Number",
            Control = "Number"
        };

        var editor = new NodeParameterEditorViewModel(parameter, definition, [], catalog.Nodes, () => { });

        Assert.Contains(editor.SourceKindChoices, choice => choice.Value == nameof(GraphValueSourceKind.CatalogValue));
        Assert.Contains(editor.RecipeChoices, choice => choice.Value == "PROP_AddNumbers");
        Assert.DoesNotContain(editor.RecipeChoices, choice => choice.Value == "PROP_JoinText");
        Assert.DoesNotContain(editor.RecipeChoices, choice => choice.Value == "PROP_ManualNumber");

        editor.SourceKind = GraphValueSourceKind.CatalogValue;
        editor.RecipeCatalogId = "PROP_AddNumbers";

        Assert.Equal("PROP_AddNumbers", parameter.Binding.CatalogId);
        Assert.Equal("AddNumbers", parameter.Binding.CatalogType);
        Assert.Contains(editor.RecipeParameters, item => item.Key == "left");
        Assert.Contains(editor.RecipeParameters, item => item.Key == "right");
        Assert.Contains("Add Numbers", editor.PreviewText);

        var textEditor = new NodeParameterEditorViewModel(
            new RuleParameter { Key = "message", Value = "", Binding = new GraphValueBinding { SourceKind = GraphValueSourceKind.Constant } },
            new NodeCatalogParameterDefinition { Key = "message", Label = "Message", Type = "String", Control = "Text" },
            [],
            catalog.Nodes,
            () => { });
        Assert.Contains(textEditor.RecipeChoices, choice => choice.Value == "PROP_JoinText");
    }

    [Fact]
    public void InlinePropertyParameters_ExposeCompactPresentationDepthAndStopAtDepthLimit()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var editor = new NodeParameterEditorViewModel(
            new RuleParameter
            {
                Key = "amount",
                Value = "1",
                Binding = new GraphValueBinding { SourceKind = GraphValueSourceKind.Constant, ConstantValue = "1" }
            },
            new NodeCatalogParameterDefinition { Key = "amount", Label = "Amount", Type = "Number", Control = "Number" },
            [],
            catalog.Nodes,
            () => { });

        editor.SourceKind = GraphValueSourceKind.CatalogValue;
        editor.RecipeCatalogId = "PROP_AddNumbers";

        var firstLevel = editor.RecipeParameters.Single(parameter => parameter.Key == "left");
        Assert.Equal(1, firstLevel.RecipeDepth);
        Assert.True(firstLevel.IsRecipeParameter);
        Assert.Contains(firstLevel.SourceKindChoices, choice => choice.Value == nameof(GraphValueSourceKind.CatalogValue));

        firstLevel.SourceKind = GraphValueSourceKind.CatalogValue;
        firstLevel.RecipeCatalogId = "PROP_AddNumbers";
        var secondLevel = firstLevel.RecipeParameters.Single(parameter => parameter.Key == "left");
        Assert.Equal(2, secondLevel.RecipeDepth);
        Assert.True(secondLevel.IsRecipeParameter);
        Assert.Contains(secondLevel.SourceKindChoices, choice => choice.Value == nameof(GraphValueSourceKind.CatalogValue));

        secondLevel.SourceKind = GraphValueSourceKind.CatalogValue;
        secondLevel.RecipeCatalogId = "PROP_AddNumbers";
        var cappedLevel = secondLevel.RecipeParameters.Single(parameter => parameter.Key == "left");
        Assert.Equal(3, cappedLevel.RecipeDepth);
        Assert.True(cappedLevel.IsRecipeParameter);
        Assert.DoesNotContain(cappedLevel.SourceKindChoices, choice => choice.Value == nameof(GraphValueSourceKind.CatalogValue));
    }

    [Fact]
    public void Vector3Editor_OffersCompatibleTypedPropertiesAcrossDomains()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var changed = false;
        var parameter = new RuleParameter
        {
            Key = "vector",
            Value = "1,2,3",
            Binding = new GraphValueBinding
            {
                SourceKind = GraphValueSourceKind.Constant,
                ConstantValue = "1,2,3",
                DataType = "Vector3"
            }
        };
        var definition = new NodeCatalogParameterDefinition
        {
            Key = "vector",
            Label = "Position / Offset",
            Type = "Vector3",
            Control = "Vector3"
        };

        var editor = new NodeParameterEditorViewModel(parameter, definition, [], catalog.Nodes, () => changed = true);

        Assert.True(editor.UsesVector3);
        Assert.Contains(editor.SourceKindChoices, choice => choice.Value == nameof(GraphValueSourceKind.CatalogValue));
        Assert.Contains(editor.RecipeChoices, choice => choice.Value == "PROP_ObjectPosition");
        Assert.Contains(editor.RecipeChoices, choice => choice.Value == "PROP_PlayerCheckpointPosition");
        Assert.Contains(editor.RecipeChoices, choice => choice.Value == "PROP_VectorAdd");
        Assert.DoesNotContain(editor.RecipeChoices, choice => choice.Value == "PROP_RGBColor");
        Assert.DoesNotContain(editor.RecipeChoices, choice => choice.Value == "PROP_JoinText");

        editor.VectorY = 8;

        Assert.True(changed);
        Assert.Equal("1,8,3", parameter.Value);
        Assert.Equal("1,8,3", parameter.Binding.ConstantValue);
    }

    [Fact]
    public void PropertyRecipeBrowser_RootShowsCompatiblePropertyFolders()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var editor = new NodeParameterEditorViewModel(
            new RuleParameter
            {
                Key = "vector",
                Value = "1,2,3",
                Binding = new GraphValueBinding { SourceKind = GraphValueSourceKind.Constant, ConstantValue = "1,2,3" }
            },
            new NodeCatalogParameterDefinition { Key = "vector", Label = "Position / Offset", Type = "Vector3", Control = "Vector3" },
            [],
            catalog.Nodes,
            () => { });

        editor.OpenRecipeBrowser();

        Assert.Contains(editor.RecipeBrowserRows, row => row.IsFolder && row.Label == "Scene Object");
        Assert.Contains(editor.RecipeBrowserRows, row => row.IsFolder && row.Label == "Math");
        Assert.DoesNotContain(editor.RecipeBrowserRows, row => row.IsRecipe && row.Value == "PROP_RGBColor");
        Assert.Equal("Properties", editor.RecipeBrowserBreadcrumb);
        Assert.Equal("3D Vector", editor.RecipeBrowserExpectedType);
        Assert.Contains("3D Vector", editor.TypeIconTooltip, StringComparison.Ordinal);
        Assert.Contains("3D Vector property", editor.ValueInputTooltip, StringComparison.Ordinal);
        Assert.DoesNotContain("Vector3", editor.TypeIconTooltip, StringComparison.Ordinal);
        Assert.DoesNotContain("Vector3", editor.ValueInputTooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void PropertyRecipeBrowser_NavigatesMathVectorFolder()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var editor = new NodeParameterEditorViewModel(
            new RuleParameter
            {
                Key = "vector",
                Value = "1,2,3",
                Binding = new GraphValueBinding { SourceKind = GraphValueSourceKind.Constant, ConstantValue = "1,2,3" }
            },
            new NodeCatalogParameterDefinition { Key = "vector", Label = "Position / Offset", Type = "Vector3", Control = "Vector3" },
            [],
            catalog.Nodes,
            () => { });

        editor.OpenRecipeBrowser();

        Assert.False(editor.ActivateRecipeBrowserRow(editor.RecipeBrowserRows.Single(row => row.IsFolder && row.Label == "Math")));
        Assert.Equal("Properties / Math", editor.RecipeBrowserBreadcrumb);
        Assert.Contains(editor.RecipeBrowserRows, row => row.IsFolder && row.Label == "Vector");

        Assert.False(editor.ActivateRecipeBrowserRow(editor.RecipeBrowserRows.Single(row => row.IsFolder && row.Label == "Vector")));

        Assert.Contains(editor.RecipeBrowserRows, row => row.IsRecipe && row.Value == "PROP_VectorAdd");
        Assert.Contains(editor.RecipeBrowserRows, row => row.IsRecipe && row.Value == "PROP_VectorFromXYZ");
        Assert.DoesNotContain(editor.RecipeBrowserRows, row => row.IsRecipe && row.Value == "PROP_JoinText");
    }

    [Theory]
    [InlineData("Vector3", "object position", "PROP_ObjectPosition")]
    [InlineData("Vector3", "checkpoint", "PROP_PlayerCheckpointPosition")]
    [InlineData("Color", "random color", "PROP_RandomColor")]
    public void PropertyRecipeBrowser_SearchFindsCompatibleProperties(string type, string search, string expectedId)
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var editor = new NodeParameterEditorViewModel(
            new RuleParameter
            {
                Key = "value",
                Value = "",
                Binding = new GraphValueBinding { SourceKind = GraphValueSourceKind.Constant }
            },
            new NodeCatalogParameterDefinition { Key = "value", Label = "Value", Type = type, Control = type },
            [],
            catalog.Nodes,
            () => { });

        editor.RecipeBrowserSearch = search;

        var row = Assert.Single(editor.RecipeBrowserRows, item => item.Value == expectedId);
        Assert.True(row.IsRecipe);
        Assert.False(string.IsNullOrWhiteSpace(row.Description));
        Assert.False(string.IsNullOrWhiteSpace(row.SubText));
    }

    [Fact]
    public void PropertyRecipeBrowser_SelectingRecipeUpdatesBindingAndInlineParameters()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var changed = false;
        var parameter = new RuleParameter
        {
            Key = "amount",
            Value = "1",
            Binding = new GraphValueBinding
            {
                SourceKind = GraphValueSourceKind.CatalogValue
            }
        };
        var editor = new NodeParameterEditorViewModel(
            parameter,
            new NodeCatalogParameterDefinition { Key = "amount", Label = "Amount", Type = "Number", Control = "Number" },
            [],
            catalog.Nodes,
            () => changed = true);

        editor.RecipeBrowserSearch = "add numbers";

        Assert.True(editor.ActivateRecipeBrowserRow(editor.RecipeBrowserRows.Single(row => row.Value == "PROP_AddNumbers")));
        Assert.True(changed);
        Assert.Equal("PROP_AddNumbers", parameter.Binding.CatalogId);
        Assert.Equal("AddNumbers", parameter.Binding.CatalogType);
        Assert.Contains(editor.RecipeParameters, item => item.Key == "left");
        Assert.Contains(editor.RecipeParameters, item => item.Key == "right");
        Assert.Equal("Add Numbers", editor.RecipePickerButtonText);
        Assert.Contains("Build value: Add Numbers", editor.PreviewText);
    }

    [Fact]
    public void ChoicePickerSearchService_FiltersByLabelValueCategoryAndKeyword()
    {
        var icon = IconRegistry.ForParameterType("String", "Choice");
        var choices = new ChoicePickerItemViewModel[]
        {
            new("World/Hidden/VRS_Demo", "VRS Demo", "Hidden", "Creator object path.", icon, ["bridge"]),
            new("Tween", "Tween", "Motion", "Smooth movement.", icon, ["ease"])
        };

        Assert.Single(ChoicePickerSearchService.FilterAndGroup(choices, "bridge"));
        Assert.Single(ChoicePickerSearchService.FilterAndGroup(choices, "vrs demo"));
        Assert.Single(ChoicePickerSearchService.FilterAndGroup(choices, "hidden"));
        Assert.Single(ChoicePickerSearchService.FilterAndGroup(choices, "Tween"));

        var result = ChoicePickerSearchService.FilterAndGroup(choices, "");
        Assert.True(result.First(item => item.Category == "Hidden").ShowsCategoryHeader);
    }

    [Fact]
    public void ChoicePickerBrowserService_OrganizesValueSourcesIntoFolders()
    {
        var choices = new ChoicePickerItemViewModel[]
        {
            new(
                nameof(GraphValueSourceKind.Constant),
                "Manual Value",
                "Direct Value",
                "Use exactly the typed value.",
                IconRegistry.ForValueSource(GraphValueSourceKind.Constant),
                ["manual"]),
            new(
                nameof(GraphValueSourceKind.Self),
                "Self",
                "Object Context",
                "Use the object that owns the deployed script.",
                IconRegistry.ForValueSource(GraphValueSourceKind.Self),
                ["owner"]),
            new(
                nameof(GraphValueSourceKind.LocalVariable),
                "Local Variable",
                "Variable",
                "Read a variable used only inside this generated script.",
                IconRegistry.ForValueSource(GraphValueSourceKind.LocalVariable),
                ["script variable"]),
            new(
                nameof(GraphValueSourceKind.CatalogValue),
                "Build Value",
                "Build Value",
                "Build this value from a property node.",
                IconRegistry.ForValueSource(GraphValueSourceKind.CatalogValue),
                ["property"])
        };

        var rootRows = ChoicePickerBrowserService.Browse(choices, "", [], useFolderNavigation: true);

        Assert.Contains(rootRows, row => row.IsFolder && row.Label == "Direct Value");
        Assert.Contains(rootRows, row => row.IsFolder && row.Label == "Object Context");
        Assert.Contains(rootRows, row => row.IsFolder && row.Label == "Variable");
        Assert.Contains(rootRows, row => row.IsFolder && row.Label == "Build Value");
        Assert.DoesNotContain(rootRows, row => !row.IsFolder && row.Label == "Self");

        var objectRows = ChoicePickerBrowserService.Browse(choices, "", ["Object Context"], useFolderNavigation: true);

        var selfRow = Assert.Single(objectRows, row => !row.IsFolder && row.Label == "Self");
        Assert.Equal(nameof(GraphValueSourceKind.Self), selfRow.Value);

        var searchRows = ChoicePickerBrowserService.Browse(choices, "self", [], useFolderNavigation: true);

        var searchRow = Assert.Single(searchRows, row => !row.IsFolder);
        Assert.Equal("Self", searchRow.Label);
        Assert.Contains("Object Context", searchRow.Description);
    }

    [Fact]
    public void NodeColorPicker_ConvertsHexToPolytoriaRgbParameters()
    {
        var changed = false;
        var red = ColorParameter("r", "1");
        var green = ColorParameter("g", "0.5");
        var blue = ColorParameter("b", "0");
        var picker = new NodeColorPickerViewModel(red, green, blue, "Color", () => changed = true);

        Assert.Equal("#FF8000", picker.HexColor);

        picker.HexColor = "#00A3FF";

        Assert.True(changed);
        Assert.Equal("0", red.Value);
        Assert.Equal("0.639216", green.Value);
        Assert.Equal("1", blue.Value);
        Assert.Equal(GraphValueSourceKind.Constant, red.Binding.SourceKind);
        Assert.Equal("#00A3FF", picker.PreviewHex);
        Assert.Contains("Color.New(0, 0.639, 1, 1)", picker.PolytoriaValueText);

        changed = false;
        picker.SelectedColor = Color.FromRgb(128, 64, 255);

        Assert.True(changed);
        Assert.Equal("0.501961", red.Value);
        Assert.Equal("0.25098", green.Value);
        Assert.Equal("1", blue.Value);
        Assert.Equal("#8040FF", picker.HexColor);
        Assert.Equal(Color.FromRgb(128, 64, 255), picker.SelectedColor);

        changed = false;
        picker.ApplySwatchCommand.Execute(picker.Swatches.Single(swatch => swatch.Name == "Coin"));

        Assert.True(changed);
        Assert.Equal("#FFD24A", picker.HexColor);
        Assert.Equal(Color.FromRgb(255, 210, 74), picker.SelectedColor);
        Assert.Equal("#FFD24A", picker.RecentColors.First().Hex);
        Assert.Contains(picker.RecentColors, color => color.Hex == "#00A3FF");
    }

    [Fact]
    public void NodeColorPicker_HsvControlsUpdatePolytoriaRgbParameters()
    {
        var changed = false;
        var red = ColorParameter("r", "1");
        var green = ColorParameter("g", "1");
        var blue = ColorParameter("b", "1");
        var picker = new NodeColorPickerViewModel(red, green, blue, "Color", () => changed = true);

        picker.SetColorModeCommand.Execute("Hsv");
        picker.HueDegrees = 200;
        picker.SaturationPercent = 50;
        picker.ValuePercent = 80;

        Assert.True(changed);
        Assert.True(picker.IsHsvMode);
        Assert.Equal(ColorPickerEditMode.Hsv, picker.CurrentMode);
        Assert.Equal(GraphValueSourceKind.Constant, red.Binding.SourceKind);
        Assert.Equal(picker.SelectedColor, picker.SelectedHsvColor.ToRgb());
        Assert.NotEqual("#FFFFFF", picker.HexColor);
        Assert.NotEmpty(picker.RecentColors);
    }

    [Fact]
    public void NodeColorPicker_LinearControlsEditNormalizedPolytoriaValues()
    {
        var changed = false;
        var red = ColorParameter("r", "0");
        var green = ColorParameter("g", "0");
        var blue = ColorParameter("b", "0");
        var picker = new NodeColorPickerViewModel(red, green, blue, "Color", () => changed = true);

        picker.SetColorModeCommand.Execute("Linear");
        picker.LinearRed = 0.25;
        picker.LinearGreen = 0.5;
        picker.LinearBlue = 0.75;

        Assert.True(changed);
        Assert.True(picker.IsLinearMode);
        Assert.Equal("0.25", red.Value);
        Assert.Equal("0.5", green.Value);
        Assert.Equal("0.75", blue.Value);
        Assert.Equal("#4080BF", picker.HexColor);
        Assert.Contains("Color.New(0.25, 0.5, 0.75, 1)", picker.PolytoriaValueText);
    }

    [Fact]
    public void NodeColorPicker_AlphaIsVisibleButLockedForCurrentExport()
    {
        var picker = new NodeColorPickerViewModel(
            ColorParameter("r", "0.2"),
            ColorParameter("g", "0.3"),
            ColorParameter("b", "0.4"),
            "Color",
            () => { });

        Assert.Equal(255, picker.AlphaByte);
        Assert.Equal(1, picker.AlphaLinear);
        Assert.Contains("locked", picker.AlphaTooltip, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(", 1)", picker.PolytoriaValueText, StringComparison.Ordinal);
    }

    private static RuleParameter ColorParameter(string key, string value)
    {
        return new RuleParameter
        {
            Key = key,
            Value = value,
            Binding = new GraphValueBinding
            {
                SourceKind = GraphValueSourceKind.Constant,
                ConstantValue = value,
                DisplayText = value
            }
        };
    }

    private static SceneObject Scene(string path, string name, string kind)
    {
        return new SceneObject
        {
            Id = path,
            Path = path,
            Name = name,
            Kind = kind
        };
    }
}
