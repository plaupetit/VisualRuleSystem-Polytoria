using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using System.Text.RegularExpressions;

namespace Vrs.Tests;

public sealed class CatalogTaxonomyTests
{
    private const int MaxPalettePathDepth = 2;
    private const int MaxCanonicalBucketSize = 14;
    private const int MaxEssentialsBucketSize = 18;

    [Fact]
    public void CatalogTaxonomy_UsesShallowCanonicalAndAliasPaths()
    {
        var catalog = LoadCatalog();

        foreach (var node in catalog.Nodes)
        {
            Assert.InRange(node.PalettePath.Count, 1, MaxPalettePathDepth);
            Assert.All(node.PalettePath, part => Assert.False(string.IsNullOrWhiteSpace(part)));
            Assert.All(node.PaletteAliases, alias =>
            {
                Assert.InRange(alias.Count, 1, MaxPalettePathDepth);
                Assert.All(alias, part => Assert.False(string.IsNullOrWhiteSpace(part)));
            });
        }
    }

    [Fact]
    public void CatalogTaxonomy_AvoidsScatteredCanonicalTopFolders()
    {
        var catalog = LoadCatalog();
        var disallowedTopFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Chat",
            "Color",
            "Debug",
            "General",
            "Input",
            "Instance",
            "Player",
            "Player Defaults",
            "State",
            "Tags",
            "Timing",
            "Transform",
            "Tween"
        };

        foreach (var node in catalog.Nodes)
        {
            var top = node.PalettePath.FirstOrDefault() ?? "";
            Assert.DoesNotContain(top, disallowedTopFolders);
        }
    }

    [Fact]
    public void CatalogTaxonomy_KeepsBroadDomainsInNamedSubfolders()
    {
        var catalog = LoadCatalog();
        var broadDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Logic",
            "Obby",
            "Players",
            "Scene Object",
            "Text",
            "UI & Feedback"
        };

        var rootOnlyNodes = catalog.Nodes
            .Where(node => node.PalettePath.Count == 1 && broadDomains.Contains(node.PalettePath[0]))
            .Select(node => $"{node.IdBase}: {string.Join(" / ", node.PalettePath)}")
            .ToList();

        Assert.Empty(rootOnlyNodes);
    }

    [Fact]
    public void CatalogTaxonomy_AvoidsAmbiguousLeafFolders()
    {
        var catalog = LoadCatalog();
        var disallowedLeafFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Appearance",
            "Area",
            "Check",
            "Defaults",
            "Object Lights",
            "Scale",
            "Size",
            "Watch"
        };

        var violations = catalog.Nodes
            .Select(node => new { node.IdBase, Path = node.PalettePath })
            .Where(item => item.Path.Any(disallowedLeafFolders.Contains))
            .Select(item => $"{item.IdBase}: {string.Join(" / ", item.Path)}")
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void CatalogTaxonomy_KeepsValuesCanonicalFolderManualOnly()
    {
        var catalog = LoadCatalog();
        var valueNodes = catalog.Nodes
            .Where(node => node.PalettePath.FirstOrDefault()?.Equals("Values", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        Assert.NotEmpty(valueNodes);
        Assert.All(valueNodes, node =>
        {
            Assert.Equal("Property", node.Kind.ToString());
            Assert.Equal(["Values", "Manual"], node.PalettePath);
        });
    }

    [Fact]
    public void CatalogTaxonomy_KeepsCanonicalAndEssentialsBucketsBelowReviewThresholds()
    {
        var catalog = LoadCatalog();

        var oversizedCanonicalBuckets = catalog.Nodes
            .GroupBy(node => string.Join(" / ", node.PalettePath), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > MaxCanonicalBucketSize)
            .Select(group => $"{group.Key}: {group.Count()}")
            .ToList();
        Assert.Empty(oversizedCanonicalBuckets);

        var oversizedEssentialsBuckets = catalog.Nodes
            .SelectMany(node => node.PaletteAliases)
            .Where(alias => alias.FirstOrDefault()?.Equals("Essentials", StringComparison.OrdinalIgnoreCase) == true)
            .GroupBy(alias => string.Join(" / ", alias), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > MaxEssentialsBucketSize)
            .Select(group => $"{group.Key}: {group.Count()}")
            .ToList();
        Assert.Empty(oversizedEssentialsBuckets);
    }

    [Fact]
    public void CatalogTaxonomy_UsesBeginnerFriendlyVisibleLanguage()
    {
        var catalog = LoadCatalog();
        var violations = new List<string>();

        foreach (var node in catalog.Nodes)
        {
            CheckVisibleText(violations, node.IdBase, "label", node.Label);
            CheckVisibleText(violations, node.IdBase, "description", node.Description);
            CheckVisibleText(violations, node.IdBase, "beginnerSummary", node.BeginnerSummary);
            CheckVisibleText(violations, node.IdBase, "previewTemplate", StripTemplatePlaceholders(node.PreviewTemplate));
            CheckVisibleTextList(violations, node.IdBase, "palettePath", node.PalettePath);
            foreach (var alias in node.PaletteAliases)
            {
                CheckVisibleTextList(violations, node.IdBase, "paletteAlias", alias);
            }

            foreach (var parameter in node.Parameters)
            {
                var parameterName = string.IsNullOrWhiteSpace(parameter.Key) ? "parameter" : parameter.Key;
                CheckVisibleText(violations, node.IdBase, $"{parameterName}.label", parameter.Label);
                CheckVisibleText(violations, node.IdBase, $"{parameterName}.description", parameter.Description);
                CheckVisibleText(violations, node.IdBase, $"{parameterName}.valueSource", parameter.ValueSource);

                foreach (var option in parameter.OptionDetails)
                {
                    CheckVisibleText(violations, node.IdBase, $"{parameterName}.option.label", option.Label);
                    CheckVisibleText(violations, node.IdBase, $"{parameterName}.option.category", option.Category);
                    CheckVisibleText(violations, node.IdBase, $"{parameterName}.option.description", option.Description);
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void CatalogTaxonomy_SceneObjectParametersDeclareTypedFilters()
    {
        var catalog = LoadCatalog();
        var allowedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Any",
            "PartLike",
            "Touchable",
            "PhysicsBody",
            "Humanoid",
            "UIButton2D",
            "UILabelObject",
            "UIObject",
            "ScriptObject",
            "SoundObject",
            "TeamObject",
            "ToolObject",
            "LightObject",
            "PointLightObject",
            "SpotLightObject",
            "ParticleObject",
            "MeshObject",
            "Image3DObject",
            "Text3DObject",
            "NPCObject",
            "BodyPositionObject",
            "SeatObject"
        };
        var missing = new List<string>();
        var invalid = new List<string>();

        foreach (var node in catalog.Nodes)
        {
            foreach (var parameter in node.Parameters.Where(NodeCatalogService.IsSceneObjectParameter))
            {
                if (!SceneObjectKindTaxonomy.HasObjectFilter(parameter))
                {
                    missing.Add($"{node.IdBase}.{parameter.Key}");
                    continue;
                }

                foreach (var group in parameter.AcceptedObjectGroups)
                {
                    if (!allowedGroups.Contains(group))
                    {
                        invalid.Add($"{node.IdBase}.{parameter.Key}: {group}");
                    }
                }
            }
        }

        Assert.Empty(missing);
        Assert.Empty(invalid);
    }

    [Theory]
    [InlineData("EV_OnTimerTick", "Flow & Timing", "Timing")]
    [InlineData("EV_AfterDelay", "Flow & Timing", "Timing")]
    [InlineData("EV_OnStart", "Flow & Timing", "Lifecycle")]
    [InlineData("EV_OnGateOpened", "Flow & Timing", "Gate")]
    [InlineData("EV_OnObjectAnchored", "Scene Object", "Physics")]
    [InlineData("COND_ObjectIsAnchored", "Scene Object", "Physics")]
    [InlineData("ACT_ToggleObjectAnchored", "Scene Object", "Physics")]
    [InlineData("PROP_ObjectAnchoredValue", "Scene Object", "Physics")]
    [InlineData("ACT_ToggleObjectVisibility", "Scene Object", "Visibility")]
    [InlineData("PROP_ObjectVisibleValue", "Scene Object", "Visibility")]
    [InlineData("PROP_ObjectTransparency", "Scene Object", "Transparency")]
    [InlineData("PROP_ObjectCollisionValue", "Scene Object", "Collision")]
    [InlineData("EV_OnObjectsBecameClose", "Scene Object", "Proximity")]
    [InlineData("EV_OnTeamScoreReached", "Game Rules", "Team Score")]
    [InlineData("EV_OnAnyPlayerScoreReached", "Players", "Score")]
    [InlineData("EV_OnNumberReachedAtLeast", "Math", "Comparison")]
    [InlineData("EV_OnNumberDroppedToAtMost", "Math", "Comparison")]
    [InlineData("EV_OnNumberEnteredRange", "Math", "Range")]
    [InlineData("EV_OnNumberLeftRange", "Math", "Range")]
    [InlineData("EV_OnTextBecame", "Text", "Changes")]
    [InlineData("EV_OnTextContains", "Text", "Changes")]
    [InlineData("EV_OnBooleanBecameTrue", "Logic", "Boolean")]
    [InlineData("EV_OnBooleanBecameFalse", "Logic", "Boolean")]
    [InlineData("EV_OnVariableNumberReachedAtLeast", "Variables", "Changes")]
    [InlineData("EV_OnVariableNumberDroppedToAtMost", "Variables", "Changes")]
    [InlineData("EV_OnVariableTextBecame", "Variables", "Changes")]
    [InlineData("EV_OnVariableBooleanBecameTrue", "Variables", "Changes")]
    [InlineData("EV_OnVariableBooleanBecameFalse", "Variables", "Changes")]
    [InlineData("EV_OnVariableBecameEmpty", "Variables", "Changes")]
    [InlineData("EV_OnVariableBecameNotEmpty", "Variables", "Changes")]
    [InlineData("EV_OnStateBecameTrue", "Variables", "State")]
    [InlineData("EV_OnStateBecameFalse", "Variables", "State")]
    [InlineData("EV_OnStateBecameEmpty", "Variables", "State")]
    [InlineData("EV_OnStateBecameNotEmpty", "Variables", "State")]
    [InlineData("EV_OnNumberChanged", "Math", "Comparison")]
    [InlineData("EV_OnTextChanged", "Text", "Changes")]
    [InlineData("EV_OnBooleanChanged", "Logic", "Boolean")]
    [InlineData("EV_OnVariableNumberChanged", "Variables", "Changes")]
    [InlineData("EV_OnVariableTextChanged", "Variables", "Changes")]
    [InlineData("EV_OnVariableBooleanChanged", "Variables", "Changes")]
    [InlineData("EV_OnPlayerCountReached", "Players", "Count")]
    [InlineData("EV_OnPlayerCountDroppedTo", "Players", "Count")]
    [InlineData("EV_OnEnoughPlayers", "Players", "Count")]
    [InlineData("EV_OnNotEnoughPlayers", "Players", "Count")]
    [InlineData("EV_OnObjectXPositionReached", "Scene Object", "Position")]
    [InlineData("EV_OnObjectHeightPositionReached", "Scene Object", "Height")]
    [InlineData("EV_OnObjectHeightPositionDroppedTo", "Scene Object", "Height")]
    [InlineData("EV_OnObjectTransparencyReached", "Scene Object", "Transparency")]
    [InlineData("EV_OnObjectTransparencyDroppedTo", "Scene Object", "Transparency")]
    [InlineData("EV_OnObjectTurnAngleReached", "Scene Object", "Rotation")]
    [InlineData("EV_OnObjectTurnAngleDroppedTo", "Scene Object", "Rotation")]
    [InlineData("EV_OnObjectWidthSizeReached", "Scene Object", "Dimensions")]
    [InlineData("EV_OnObjectWidthSizeDroppedTo", "Scene Object", "Dimensions")]
    [InlineData("EV_OnObjectHeightSizeReached", "Scene Object", "Dimensions")]
    [InlineData("EV_OnObjectHeightSizeDroppedTo", "Scene Object", "Dimensions")]
    [InlineData("EV_OnObjectCollisionChanged", "Scene Object", "Collision")]
    [InlineData("EV_OnObjectStartedMoving", "Scene Object", "Movement")]
    [InlineData("EV_OnObjectStoppedMoving", "Scene Object", "Movement")]
    [InlineData("EV_OnObjectSpeedReached", "Scene Object", "Movement")]
    [InlineData("EV_OnObjectSpeedDroppedTo", "Scene Object", "Movement")]
    [InlineData("EV_OnObjectEnteredArea", "Scene Object", "Zones")]
    [InlineData("EV_OnObjectLeftArea", "Scene Object", "Zones")]
    [InlineData("EV_OnObjectEnteredBoxArea", "Scene Object", "Zones")]
    [InlineData("EV_OnObjectLeftBoxArea", "Scene Object", "Zones")]
    [InlineData("EV_OnObjectEnteredHeightBand", "Scene Object", "Zones")]
    [InlineData("EV_OnObjectLeftHeightBand", "Scene Object", "Zones")]
    [InlineData("EV_OnUIButtonClicked", "UI & Feedback", "Custom UI")]
    [InlineData("ACT_StopFlow", "Flow & Timing", "Flow Control")]
    [InlineData("ACT_ShowMessage", "UI & Feedback", "Message")]
    [InlineData("ACT_BroadcastChatMessage", "UI & Feedback", "Chat")]
    [InlineData("ACT_SetUIText", "UI & Feedback", "Custom UI")]
    [InlineData("ACT_SetUIColor", "UI & Feedback", "Custom UI")]
    [InlineData("ACT_SetUITextWrapped", "UI & Feedback", "Custom UI")]
    [InlineData("COND_UITextIs", "UI & Feedback", "Custom UI")]
    [InlineData("COND_UITextIsEmpty", "UI & Feedback", "Custom UI")]
    [InlineData("COND_UITextWrapped", "UI & Feedback", "Custom UI")]
    [InlineData("PROP_UIText", "UI & Feedback", "Custom UI")]
    [InlineData("PROP_UIColor", "UI & Feedback", "Custom UI")]
    [InlineData("PROP_UIFontSize", "UI & Feedback", "Custom UI")]
    [InlineData("PROP_UITextWrapped", "UI & Feedback", "Custom UI")]
    [InlineData("ACT_SetWalkSpeed", "Players", "Default Movement")]
    [InlineData("ACT_SetMaxHealth", "Players", "Default Health")]
    [InlineData("EV_OnInputButtonDown", "Players", "Input")]
    [InlineData("EV_OnPlayerJoined", "Players", "Events")]
    [InlineData("PROP_TriggeringPlayer", "Players", "Context")]
    [InlineData("PROP_FindPlayerByName", "Players", "Lookup")]
    [InlineData("ACT_MoveObject", "Scene Object", "Position")]
    [InlineData("ACT_SetObjectName", "Scene Object", "Identity")]
    [InlineData("ACT_DestroyObject", "Scene Object", "Lifecycle")]
    [InlineData("COND_ObjectExists", "Scene Object", "Lifecycle")]
    [InlineData("COND_ObjectIsType", "Scene Object", "Type Checks")]
    [InlineData("COND_ObjectHasTag", "Scene Object", "Tags")]
    [InlineData("ACT_SetObjectXPosition", "Scene Object", "Position")]
    [InlineData("ACT_SetObjectHeightPosition", "Scene Object", "Height")]
    [InlineData("ACT_SetObjectZPosition", "Scene Object", "Position")]
    [InlineData("PROP_ObjectPosition", "Scene Object", "Position")]
    [InlineData("PROP_ObjectRotation", "Scene Object", "Rotation")]
    [InlineData("PROP_ObjectScale", "Scene Object", "Scaling")]
    [InlineData("ACT_Set3DImageColor", "Scene Object", "3D Image Display")]
    [InlineData("ACT_Set3DImageShadows", "Scene Object", "3D Image Display")]
    [InlineData("ACT_Set3DImageLighting", "Scene Object", "3D Image Display")]
    [InlineData("ACT_Set3DImageFaceCamera", "Scene Object", "3D Image Display")]
    [InlineData("ACT_Set3DImageTextureScale", "Scene Object", "3D Image Texture")]
    [InlineData("ACT_Set3DImageTextureOffset", "Scene Object", "3D Image Texture")]
    [InlineData("COND_3DImageCastsShadows", "Scene Object", "3D Image Display")]
    [InlineData("COND_3DImageUsesLighting", "Scene Object", "3D Image Display")]
    [InlineData("COND_3DImageFacesCamera", "Scene Object", "3D Image Display")]
    [InlineData("PROP_3DImageColor", "Scene Object", "3D Image Display")]
    [InlineData("PROP_3DImageCastsShadows", "Scene Object", "3D Image Display")]
    [InlineData("PROP_3DImageUsesLighting", "Scene Object", "3D Image Display")]
    [InlineData("PROP_3DImageFacesCamera", "Scene Object", "3D Image Display")]
    [InlineData("ACT_Set3DText", "Scene Object", "3D Text Content")]
    [InlineData("ACT_Set3DTextFontSize", "Scene Object", "3D Text Content")]
    [InlineData("ACT_Set3DTextRichText", "Scene Object", "3D Text Content")]
    [InlineData("ACT_Set3DTextColor", "Scene Object", "3D Text Display")]
    [InlineData("ACT_Set3DTextOutlineWidth", "Scene Object", "3D Text Display")]
    [InlineData("ACT_Set3DTextOutlineColor", "Scene Object", "3D Text Display")]
    [InlineData("ACT_Set3DTextFaceCamera", "Scene Object", "3D Text Display")]
    [InlineData("ACT_Set3DTextLighting", "Scene Object", "3D Text Display")]
    [InlineData("COND_3DTextIs", "Scene Object", "3D Text Content")]
    [InlineData("COND_3DTextIsEmpty", "Scene Object", "3D Text Content")]
    [InlineData("COND_3DTextFacesCamera", "Scene Object", "3D Text Display")]
    [InlineData("COND_3DTextUsesRichText", "Scene Object", "3D Text Content")]
    [InlineData("COND_3DTextUsesLighting", "Scene Object", "3D Text Display")]
    [InlineData("PROP_3DText", "Scene Object", "3D Text Content")]
    [InlineData("PROP_3DTextFontSize", "Scene Object", "3D Text Content")]
    [InlineData("PROP_3DTextColor", "Scene Object", "3D Text Display")]
    [InlineData("PROP_3DTextOutlineWidth", "Scene Object", "3D Text Display")]
    [InlineData("PROP_3DTextOutlineColor", "Scene Object", "3D Text Display")]
    [InlineData("PROP_3DTextFacesCamera", "Scene Object", "3D Text Display")]
    [InlineData("PROP_3DTextUsesRichText", "Scene Object", "3D Text Content")]
    [InlineData("PROP_3DTextUsesLighting", "Scene Object", "3D Text Display")]
    [InlineData("EV_OnNPCDied", "Scene Object", "NPC Health")]
    [InlineData("EV_OnNPCLanded", "Scene Object", "NPC Movement")]
    [InlineData("EV_OnNPCNavigationFinished", "Scene Object", "NPC Movement")]
    [InlineData("ACT_SetNPCHealth", "Scene Object", "NPC Health")]
    [InlineData("ACT_DamageNPC", "Scene Object", "NPC Health")]
    [InlineData("ACT_HealNPC", "Scene Object", "NPC Health")]
    [InlineData("ACT_KillNPC", "Scene Object", "NPC Health")]
    [InlineData("ACT_SetNPCWalkSpeed", "Scene Object", "NPC Movement")]
    [InlineData("ACT_SetNPCJumpPower", "Scene Object", "NPC Movement")]
    [InlineData("ACT_MakeNPCJump", "Scene Object", "NPC Movement")]
    [InlineData("ACT_SetNPCNavigationTarget", "Scene Object", "NPC Movement")]
    [InlineData("COND_NPCIsDead", "Scene Object", "NPC Health")]
    [InlineData("COND_NPCIsOnGround", "Scene Object", "NPC Movement")]
    [InlineData("COND_NPCHealthAtMost", "Scene Object", "NPC Health")]
    [InlineData("COND_NPCReachedNavigationTarget", "Scene Object", "NPC Movement")]
    [InlineData("PROP_NPCHealth", "Scene Object", "NPC Health")]
    [InlineData("PROP_NPCWalkSpeed", "Scene Object", "NPC Movement")]
    [InlineData("PROP_NPCJumpPower", "Scene Object", "NPC Movement")]
    [InlineData("PROP_NPCIsDead", "Scene Object", "NPC Health")]
    [InlineData("PROP_NPCIsOnGround", "Scene Object", "NPC Movement")]
    [InlineData("PROP_NPCNavigationDistance", "Scene Object", "NPC Movement")]
    [InlineData("PROP_ObjectXPosition", "Scene Object", "Position")]
    [InlineData("PROP_ObjectHeightPosition", "Scene Object", "Height")]
    [InlineData("PROP_ObjectZPosition", "Scene Object", "Position")]
    [InlineData("ACT_SetObjectTurnAngle", "Scene Object", "Rotation")]
    [InlineData("ACT_TurnObjectByAngle", "Scene Object", "Rotation")]
    [InlineData("PROP_ObjectTurnAngle", "Scene Object", "Rotation")]
    [InlineData("COND_ObjectTurnAngleAtLeast", "Scene Object", "Rotation")]
    [InlineData("COND_ObjectTurnAngleAtMost", "Scene Object", "Rotation")]
    [InlineData("ACT_SetObjectWidthSize", "Scene Object", "Dimensions")]
    [InlineData("ACT_SetObjectHeightSize", "Scene Object", "Dimensions")]
    [InlineData("ACT_SetObjectDepthSize", "Scene Object", "Dimensions")]
    [InlineData("PROP_ObjectWidthSize", "Scene Object", "Dimensions")]
    [InlineData("PROP_ObjectHeightSize", "Scene Object", "Dimensions")]
    [InlineData("PROP_ObjectDepthSize", "Scene Object", "Dimensions")]
    [InlineData("COND_ObjectSizeAtLeast", "Scene Object", "Dimensions")]
    [InlineData("COND_ObjectSizeAtMost", "Scene Object", "Dimensions")]
    [InlineData("ACT_SetObjectParent", "Scene Object", "Parent")]
    [InlineData("PROP_FindChild", "Scene Object", "Lookup")]
    [InlineData("COND_TextContains", "Text", "Match")]
    [InlineData("COND_TextIsEmpty", "Text", "Empty")]
    [InlineData("PROP_TextLength", "Text", "Length")]
    [InlineData("COND_ValueEquals", "Logic", "Values")]
    [InlineData("PROP_DirectionToObject", "Math", "Vector")]
    [InlineData("PROP_RGBColor", "Scene Object", "Color")]
    [InlineData("PROP_RandomColor", "Scene Object", "Color")]
    [InlineData("ACT_TweenObjectPosition", "Scene Object", "Animation")]
    [InlineData("COND_ObjectIsAboveHeight", "Scene Object", "Height")]
    [InlineData("COND_ObjectIsBelowHeight", "Scene Object", "Height")]
    [InlineData("ACT_SetState", "Variables", "State")]
    [InlineData("COND_StateIsFalse", "Variables", "State")]
    [InlineData("COND_ScriptNumberIsAtMost", "Variables", "Script Numbers")]
    [InlineData("ACT_SetPlayerScore", "Players", "Score")]
    [InlineData("COND_PlayerScoreEquals", "Players", "Score")]
    [InlineData("COND_PlayerCountAtMost", "Players", "Count")]
    [InlineData("PROP_ManualVector3", "Math", "Vector")]
    [InlineData("PROP_DistanceBetweenObjects", "Math", "Geometry")]
    [InlineData("PROP_MapNumberRange", "Math", "Range")]
    [InlineData("PROP_AverageNumber", "Math", "Arithmetic")]
    [InlineData("PROP_SquareRootNumber", "Math", "Arithmetic")]
    [InlineData("PROP_RandomWholeNumber", "Math", "Random")]
    [InlineData("PROP_RandomTextChoice", "Text", "Choice")]
    [InlineData("PROP_NumberToText", "Text", "Convert")]
    [InlineData("PROP_JoinText", "Text", "Edit")]
    [InlineData("PROP_TextBefore", "Text", "Extract")]
    [InlineData("PROP_TextAfter", "Text", "Extract")]
    [InlineData("PROP_TextBetween", "Text", "Extract")]
    [InlineData("PROP_FirstTextCharacters", "Text", "Extract")]
    [InlineData("PROP_LastTextCharacters", "Text", "Extract")]
    [InlineData("COND_NumberIsAtLeast", "Math", "Comparison")]
    [InlineData("COND_BooleanCheck", "Logic", "Boolean")]
    [InlineData("COND_NumberIsEven", "Math", "Comparison")]
    [InlineData("COND_NumberIsOdd", "Math", "Comparison")]
    [InlineData("COND_NumberIsPositive", "Math", "Comparison")]
    [InlineData("COND_NumberIsNegative", "Math", "Comparison")]
    [InlineData("COND_NumberOutsideRange", "Math", "Range")]
    [InlineData("COND_TextEquals", "Text", "Match")]
    [InlineData("COND_TextHasAtLeastCharacters", "Text", "Length")]
    [InlineData("COND_TextHasAtMostCharacters", "Text", "Length")]
    [InlineData("COND_ObjectChildCountAtLeast", "Scene Object", "Children")]
    [InlineData("ACT_StartPlayerTimer", "Obby", "Run State")]
    [InlineData("ACT_SetPlayerNumber", "Obby", "Temporary Player Values")]
    [InlineData("ACT_KillPlayer", "Obby", "Hazard")]
    [InlineData("ACT_MakePlayerJump", "Obby", "Player Control")]
    [InlineData("EV_OnPlayerRespawned", "Obby", "Player Events")]
    [InlineData("EV_OnSoundLoaded", "Audio", "Playback")]
    [InlineData("EV_OnSeatSat", "Scene Object", "Seats")]
    [InlineData("EV_OnSeatVacated", "Scene Object", "Seats")]
    [InlineData("ACT_SetSeatAllowsNPCs", "Scene Object", "Seats")]
    [InlineData("COND_SeatIsOccupied", "Scene Object", "Seats")]
    [InlineData("COND_SeatAllowsNPCs", "Scene Object", "Seats")]
    [InlineData("PROP_SeatOccupant", "Scene Object", "Seats")]
    [InlineData("PROP_SeatAllowsNPCs", "Scene Object", "Seats")]
    [InlineData("ACT_PlaySound", "Audio", "Playback")]
    [InlineData("ACT_SetSoundVolume", "Audio", "Settings")]
    [InlineData("PROP_SoundIsPlaying", "Audio", "Status")]
    [InlineData("ACT_SetFogEnabled", "Lighting", "Fog")]
    [InlineData("ACT_SetFogDistances", "Lighting", "Fog")]
    [InlineData("PROP_AmbientColor", "Lighting", "Ambient")]
    [InlineData("ACT_SetLightColor", "Lighting", "Light Settings")]
    [InlineData("ACT_SetLightBrightness", "Lighting", "Light Settings")]
    [InlineData("ACT_SetLightShine", "Lighting", "Light Settings")]
    [InlineData("ACT_SetLightShadows", "Lighting", "Light Settings")]
    [InlineData("ACT_SetPointLightRange", "Lighting", "Light Reach")]
    [InlineData("ACT_SetSpotLightRange", "Lighting", "Light Reach")]
    [InlineData("ACT_SetSpotLightAngle", "Lighting", "Light Reach")]
    [InlineData("PROP_LightColor", "Lighting", "Light Settings")]
    [InlineData("PROP_LightBrightness", "Lighting", "Light Settings")]
    [InlineData("PROP_LightShine", "Lighting", "Light Settings")]
    [InlineData("PROP_LightShadows", "Lighting", "Light Settings")]
    [InlineData("PROP_PointLightRange", "Lighting", "Light Reach")]
    [InlineData("PROP_SpotLightRange", "Lighting", "Light Reach")]
    [InlineData("PROP_SpotLightAngle", "Lighting", "Light Reach")]
    [InlineData("ACT_StartParticles", "Effects", "Particles")]
    [InlineData("ACT_StopParticles", "Effects", "Particles")]
    [InlineData("ACT_BurstParticles", "Effects", "Particles")]
    [InlineData("ACT_SetParticleAmount", "Effects", "Particles")]
    [InlineData("PROP_ParticlesPlaying", "Effects", "Particles")]
    [InlineData("PROP_ParticleAmount", "Effects", "Particles")]
    [InlineData("EV_OnMeshLoaded", "Scene Object", "Mesh Animation")]
    [InlineData("ACT_PlayMeshAnimation", "Scene Object", "Mesh Animation")]
    [InlineData("ACT_StopMeshAnimation", "Scene Object", "Mesh Animation")]
    [InlineData("PROP_CurrentMeshAnimation", "Scene Object", "Mesh Animation")]
    [InlineData("PROP_MeshAnimationPlaying", "Scene Object", "Mesh Animation")]
    [InlineData("PROP_MeshLoading", "Scene Object", "Mesh Animation")]
    [InlineData("EV_OnPlayerGameTeamChanged", "Players", "Game Team")]
    [InlineData("ACT_SetPlayerGameTeam", "Players", "Game Team")]
    [InlineData("COND_PlayerIsInGameTeam", "Players", "Game Team")]
    [InlineData("PROP_PlayerGameTeamName", "Players", "Game Team")]
    [InlineData("PROP_GameTeamName", "Game Rules", "Game Teams")]
    [InlineData("PROP_GameTeamPlayerCount", "Game Rules", "Game Teams")]
    [InlineData("EV_OnBodyPositionReachedTarget", "Scene Object", "Body Position")]
    [InlineData("ACT_SetBodyPositionTarget", "Scene Object", "Body Position")]
    [InlineData("COND_BodyPositionReachedTarget", "Scene Object", "Body Position")]
    [InlineData("PROP_BodyPositionDistanceToTarget", "Scene Object", "Body Position")]
    [InlineData("EV_OnObjectTouchEnded", "Scene Object", "Touch")]
    [InlineData("EV_OnObjectHoverStarted", "Scene Object", "Input")]
    [InlineData("ACT_MoveObjectWithPhysics", "Scene Object", "Physics Motion")]
    [InlineData("COND_ObjectIsMoving", "Scene Object", "Physics Motion")]
    [InlineData("PROP_ObjectSpeed", "Scene Object", "Physics Motion")]
    [InlineData("ACT_SetBuiltInUIVisible", "UI & Feedback", "Built-In UI")]
    [InlineData("PROP_BuiltInChatVisible", "UI & Feedback", "Built-In UI")]
    [InlineData("PROP_PlayerCanRespawn", "UI & Feedback", "Built-In UI")]
    [InlineData("EV_OnToolActivated", "Players", "Tools")]
    [InlineData("ACT_ActivateTool", "Players", "Tools")]
    [InlineData("COND_ToolIsHeld", "Players", "Tools")]
    [InlineData("PROP_ToolHolder", "Players", "Tools")]
    public void CatalogTaxonomy_UsesExpectedCanonicalFolders(string idBase, string top, string? leaf)
    {
        var catalog = LoadCatalog();
        var node = catalog.Nodes.Single(entry => entry.IdBase == idBase);

        var expectedPath = leaf is null ? [top] : new[] { top, leaf };
        Assert.Equal(expectedPath, node.PalettePath);
    }

    private static NodeCatalogData LoadCatalog()
    {
        return new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
    }

    private static void CheckVisibleText(List<string> violations, string idBase, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var text = RemoveAllowedUserIdText(value);

        var bannedTerms = new[]
        {
            @"\bRuntime\b",
            @"\bVector3\b",
            @"\bClass\b",
            @"\bIsA\b",
            @"\bInstance\b",
            @"\bVRS\b",
            @"\bCatalog\b",
            @"\bLuau\b",
            @"\bID\b",
            @"\bid\b"
        };

        foreach (var term in bannedTerms)
        {
            if (Regex.IsMatch(text, term, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
            {
                violations.Add($"{idBase} {field}: {value}");
                return;
            }
        }
    }

    private static void CheckVisibleTextList(List<string> violations, string idBase, string field, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            CheckVisibleText(violations, idBase, field, value);
        }
    }

    private static string StripTemplatePlaceholders(string value)
    {
        return Regex.Replace(value, @"\$\{[^}]+\}", "", RegexOptions.CultureInvariant);
    }

    private static string RemoveAllowedUserIdText(string value)
    {
        return Regex.Replace(value, @"\bUser ID\b", "", RegexOptions.CultureInvariant);
    }
}
