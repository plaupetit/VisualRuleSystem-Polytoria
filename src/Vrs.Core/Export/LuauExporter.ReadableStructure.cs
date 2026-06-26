using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    // Readable export plan, user variables, node-local variables, script context declarations, and final startup calls.
    private static ReadableExportPlan BuildReadableExportPlan(Rule rule, IReadOnlyDictionary<string, RuleNode> nodesById)
    {
        // The plan assigns stable readable names before writing code, avoiding
        // accidental coupling between graph traversal order and identifier text.
        var functionNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usedFunctionNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in OrderedNodes(rule).Where(node => node.Kind is NodeKind.Trigger or NodeKind.Condition or NodeKind.Action))
        {
            functionNames[node.Id] = UniqueIdentifier(PreferredFunctionName(node), usedFunctionNames);
        }

        var configNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usedConfigNames = new HashSet<string>(StringComparer.Ordinal);
        var usesTargetResolver = RuleUsesTargetValueRecipe(rule) || RuleUsesEssentialsTargetResolver(rule);
        var usesVectorFactory = RuleUsesEssentialsVectorFactory(rule);
        var usesVectorTween = false;
        var usesObbyPlayerState = false;
        var usesObbyTouchResolver = false;
        var usesObbyObjectPosition = false;
        var usesEssentialsRuntime = RuleUsesEssentialsRuntime(rule);
        var usesInputEventRuntime = false;
        foreach (var node in OrderedNodes(rule))
        {
            if (IsInputEventRuntimeNode(node.Type))
            {
                usesInputEventRuntime = true;
            }

            if (IsRunLuauNode(node))
            {
                usesTargetResolver = true;
                usesVectorFactory = true;
            }

            if (node.Type.Equals("MoveObjectToAnotherObject", StringComparison.OrdinalIgnoreCase))
            {
                usesTargetResolver = true;
            }

            if (ObjectDistanceConditionTypes.Contains(node.Type))
            {
                usesTargetResolver = true;
                usesEssentialsRuntime = true;
            }

            if (NodeUsesVectorFactory(node))
            {
                usesVectorFactory = true;
            }

            if (NodeUsesVectorTween(rule, node, nodesById))
            {
                usesVectorFactory = true;
                usesVectorTween = true;
            }

            if (ObbyPlayerStateTypes.Contains(node.Type))
            {
                usesObbyPlayerState = true;
            }

            if (ObbyTouchTriggerTypes.Contains(node.Type))
            {
                usesObbyTouchResolver = true;
            }

            if (ObbyObjectPositionTypes.Contains(node.Type))
            {
                usesObbyObjectPosition = true;
            }

            if (NodeUsesResolvedTarget(node) && ReadableTargetNeedsResolver(rule, node, nodesById))
            {
                var targetName = node.Type.Equals("SetObjectColor", StringComparison.OrdinalIgnoreCase)
                    ? "TARGET_NAME"
                    : $"{UniqueUpperIdentifier(node.Label)}_TARGET_NAME";
                configNames[ConfigKey(node, "target")] = UniqueIdentifier(targetName, usedConfigNames);
                usesTargetResolver = true;
            }

            if (node.Kind == NodeKind.Trigger && node.Type.Equals("OnTimerTick", StringComparison.OrdinalIgnoreCase))
            {
                configNames[ConfigKey(node, "interval")] = UniqueIdentifier("TIMER_INTERVAL_SECONDS", usedConfigNames);
            }
            else if (node.Kind == NodeKind.Action &&
                node.Type.Equals("ShowMessage", StringComparison.OrdinalIgnoreCase) &&
                !ParameterUsesRuntimeExpression(rule, node, nodesById, "message"))
            {
                configNames[ConfigKey(node, "message")] = UniqueIdentifier("MESSAGE_TEXT", usedConfigNames);
            }
            else if (node.Kind == NodeKind.Action &&
                node.Type.Equals("WaitSeconds", StringComparison.OrdinalIgnoreCase) &&
                !ParameterUsesRuntimeExpression(rule, node, nodesById, "duration"))
            {
                configNames[ConfigKey(node, "duration")] = UniqueIdentifier("WAIT_DURATION_SECONDS", usedConfigNames);
            }
            else if (node.Kind == NodeKind.Action &&
                node.Type.Equals("SetObjectColor", StringComparison.OrdinalIgnoreCase) &&
                !ParameterUsesRuntimeExpression(rule, node, nodesById, "r") &&
                !ParameterUsesRuntimeExpression(rule, node, nodesById, "g") &&
                !ParameterUsesRuntimeExpression(rule, node, nodesById, "b"))
            {
                configNames[ConfigKey(node, "color")] = UniqueIdentifier("TARGET_COLOR", usedConfigNames);
            }
        }

        return new ReadableExportPlan(
            functionNames,
            configNames,
            usesTargetResolver,
            usesVectorFactory,
            usesVectorTween,
            usesObbyPlayerState,
            usesObbyTouchResolver,
            usesObbyObjectPosition,
            usesEssentialsRuntime,
            usesInputEventRuntime);
    }

    private static void AppendReadableUserConfigurationVariables(StringBuilder builder)
    {
        builder.AppendLine(LuauCommentTags.VsrComment("USER CONFIGURATION"));
    }

    private static bool NodeUsesResolvedTarget(RuleNode node)
    {
        if (!node.Parameters.Any(parameter => parameter.Key.Equals("target", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return node.Kind == NodeKind.Trigger
            || node.Type.Equals("SetObjectColor", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectVisible", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ShowObject", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("HideObject", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ToggleObjectVisibility", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectName", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectParent", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectTransparency", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectAnchored", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ToggleObjectAnchored", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectCanCollide", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("TurnObjectCollisionOn", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("TurnObjectCollisionOff", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ToggleObjectCollision", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("DestroyObject", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("MoveObject", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("MoveObjectToAnotherObject", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("MoveObjectUp", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("MoveObjectDown", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("MoveObjectOverTime", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectPosition", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectXPosition", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectHeightPosition", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectZPosition", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("AddObjectPosition", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("RotateObject", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectTurnAngle", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("TurnObjectByAngle", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("RotateObjectContinuously", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectScale", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectWidthSize", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectHeightSize", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectDepthSize", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DImageTextureScale", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DImageTextureOffset", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("TweenObjectPosition", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("TweenObjectRotation", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("TweenObjectScale", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("TweenObjectColor", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetPlayerGameTeam", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("PlayerIsInGameTeam", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("PlaySound", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("PlaySoundOnce", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("PauseSound", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("StopSound", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetSoundVolume", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetSoundLoop", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetLightColor", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetLightBrightness", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetLightShine", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetLightShadows", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetPointLightRange", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetSpotLightRange", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetSpotLightAngle", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("StartParticles", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("StopParticles", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("EmitParticles", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetParticleAmount", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("PlayMeshAnimation", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("StopMeshAnimation", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetSeatAllowsNPCs", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SeatIsOccupied", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SeatAllowsNPCs", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetNPCHealth", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("DamageNPC", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("HealNPC", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("KillNPC", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetNPCWalkSpeed", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetNPCJumpPower", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("MakeNPCJump", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetNPCNavigationTarget", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("NPCIsDead", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("NPCIsOnGround", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("NPCHealthAtMost", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("NPCReachedNavigationTarget", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetBodyPositionTarget", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetBodyPositionForce", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetBodyPositionAcceptanceDistance", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("BodyPositionReachedTarget", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("BodyPositionForceAtLeast", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("MoveObjectWithPhysics", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("TurnObjectWithPhysics", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectVelocity", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectSpinVelocity", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectIsMoving", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectSpeedAtLeast", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DImageColor", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DImageShadows", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DImageLighting", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DImageFaceCamera", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DImageTextureScale", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DImageTextureOffset", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Image3DCastsShadows", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Image3DUsesLighting", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Image3DFacesCamera", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DText", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DTextFontSize", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DTextRichText", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DTextColor", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DTextOutlineWidth", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DTextOutlineColor", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DTextFaceCamera", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DTextLighting", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Text3DIs", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Text3DIsEmpty", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Text3DFacesCamera", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Text3DUsesRichText", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Text3DUsesLighting", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetUIText", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetUIColor", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetUITextWrapped", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("UITextIs", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("UITextIsEmpty", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("UITextWrapped", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ActivateTool", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("DeactivateTool", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("PlayToolAnimation", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetToolDroppable", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ToolCanBeDropped", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ToolIsHeld", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("OnPlayerTouchedObject", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("OnCheckpointTouched", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("OnHazardTouched", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("OnFinishTouched", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetPlayerCheckpoint", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectSpawnEnabled", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("StartMovingPlatformLoop", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectExists", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectIsNamed", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectIsType", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectIsVisible", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectIsHidden", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectCollisionIsOn", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectCollisionIsOff", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectTransparencyAtLeast", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectIsAboveHeight", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectIsBelowHeight", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectTurnAngleAtLeast", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectTurnAngleAtMost", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectSizeAtLeast", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectSizeAtMost", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectHasParent", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectParentIs", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectIsUnderObject", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectHasTag", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectIsA", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectHasChild", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectHasChildClass", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectHasChildren", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectHasNoChildren", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectChildCountAtLeast", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectChildCountAtMost", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("FindChild", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("FindChildByClass", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("ObjectChildCount", StringComparison.OrdinalIgnoreCase);
    }

    private static bool NodeUsesVectorFactory(RuleNode node)
    {
        return node.Type.Equals("MoveObject", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("MoveObjectUp", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("MoveObjectDown", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("MoveObjectOverTime", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectPosition", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectXPosition", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectHeightPosition", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectZPosition", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("AddObjectPosition", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("RotateObject", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectTurnAngle", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("TurnObjectByAngle", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("RotateObjectContinuously", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectScale", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectWidthSize", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectHeightSize", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectDepthSize", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DImageTextureScale", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("Set3DImageTextureOffset", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetNPCNavigationTarget", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetBodyPositionTarget", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("MoveObjectWithPhysics", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("TurnObjectWithPhysics", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectVelocity", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("SetObjectSpinVelocity", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("TweenObjectPosition", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("TweenObjectRotation", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("TweenObjectScale", StringComparison.OrdinalIgnoreCase)
            || node.Type.Equals("StartMovingPlatformLoop", StringComparison.OrdinalIgnoreCase);
    }

    private static bool NodeUsesVectorTween(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById)
    {
        if (!node.Type.Equals("MoveObject", StringComparison.OrdinalIgnoreCase) &&
            !node.Type.Equals("MoveObjectOverTime", StringComparison.OrdinalIgnoreCase) &&
            !node.Type.Equals("RotateObject", StringComparison.OrdinalIgnoreCase) &&
            !node.Type.Equals("SetObjectScale", StringComparison.OrdinalIgnoreCase) &&
            !node.Type.Equals("StartMovingPlatformLoop", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (node.Type.Equals("StartMovingPlatformLoop", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parameterValues = BuildEffectiveParameterValues(rule, node, nodesById);
        if (node.Type.Equals("RotateObject", StringComparison.OrdinalIgnoreCase) &&
            ParameterValue(node, parameterValues, "rotationMode").Equals("Spin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (node.Type.Equals("MoveObjectOverTime", StringComparison.OrdinalIgnoreCase))
        {
            return ParameterValue(node, parameterValues, "moveMode").Equals("Tween", StringComparison.OrdinalIgnoreCase);
        }

        return ParameterValue(node, parameterValues, "motionMode").Equals("Smooth", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RuleUsesTargetValueRecipe(Rule rule)
    {
        foreach (var node in rule.Nodes)
        {
            if (TargetValueRecipeTypes.Contains(node.Type))
            {
                return true;
            }

            if (node.Parameters.Any(parameter => BindingUsesTargetValueRecipe(parameter.Binding)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool BindingUsesTargetValueRecipe(GraphValueBinding binding)
    {
        if (binding.SourceKind == GraphValueSourceKind.CatalogValue && TargetValueRecipeTypes.Contains(binding.CatalogType))
        {
            return true;
        }

        return binding.CatalogParameters.Any(parameter => BindingUsesTargetValueRecipe(parameter.Binding));
    }

    private static readonly HashSet<string> TargetValueRecipeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ObjectName",
        "ChooseObject",
        "ObjectPosition",
        "ObjectXPosition",
        "ObjectHeightPosition",
        "ObjectZPosition",
        "ObjectTurnAngle",
        "ObjectWidthSize",
        "ObjectHeightSize",
        "ObjectDepthSize",
        "ObjectColor",
        "ObjectParent",
        "ObjectVisibleValue",
        "ObjectCollisionValue",
        "ObjectAnchoredValue",
        "ObjectTransparency",
        "SoundVolume",
        "SoundIsPlaying",
        "SoundLength",
        "SoundTime",
        "LightColor",
        "LightBrightness",
        "LightShine",
        "LightShadows",
        "PointLightRange",
        "SpotLightRange",
        "SpotLightAngle",
        "ParticlesPlaying",
        "ParticleAmount",
        "CurrentMeshAnimation",
        "MeshAnimationPlaying",
        "MeshLoading",
        "SeatOccupant",
        "SeatAllowsNPCsValue",
        "NPCHealth",
        "NPCWalkSpeed",
        "NPCJumpPower",
        "NPCIsDeadValue",
        "NPCIsOnGroundValue",
        "NPCNavigationDistance",
        "BodyPositionTarget",
        "BodyPositionForce",
        "BodyPositionAcceptanceDistance",
        "BodyPositionDistanceToTarget",
        "ObjectVelocity",
        "ObjectSpeed",
        "ObjectSpinVelocity",
        "TouchingObjectCount",
        "Image3DColorValue",
        "Image3DCastsShadowsValue",
        "Image3DUsesLightingValue",
        "Image3DFacesCameraValue",
        "Text3DValue",
        "Text3DFontSizeValue",
        "Text3DColorValue",
        "Text3DOutlineWidthValue",
        "Text3DOutlineColorValue",
        "Text3DFacesCameraValue",
        "Text3DUsesRichTextValue",
        "Text3DUsesLightingValue",
        "GameTeamName",
        "GameTeamColor",
        "GameTeamPlayerCount",
        "ToolHolder",
        "ToolCanBeDroppedValue",
        "FindChild",
        "FindChildByClass",
        "ObjectChildCount",
        "UITextValue",
        "UIColorValue",
        "UIFontSizeValue",
        "UITextWrappedValue"
    };

    private static readonly HashSet<string> RuntimeExpressionPropertyTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ReadScriptVariable",
        "ReadState",
        "RunLuauProperty",
        "RandomNumber",
        "RandomWholeNumber",
        "RandomNumberChoice",
        "RandomTextChoice",
        "RandomTrueOrFalse",
        "RandomColor",
        "ObjectName",
        "ObjectPosition",
        "ObjectXPosition",
        "ObjectHeightPosition",
        "ObjectZPosition",
        "ObjectTurnAngle",
        "ObjectWidthSize",
        "ObjectHeightSize",
        "ObjectDepthSize",
        "ObjectColor",
        "ObjectParent",
        "ObjectVisibleValue",
        "ObjectCollisionValue",
        "ObjectAnchoredValue",
        "ObjectTransparency",
        "SoundVolume",
        "SoundIsPlaying",
        "SoundLength",
        "SoundTime",
        "LightColor",
        "LightBrightness",
        "LightShine",
        "LightShadows",
        "PointLightRange",
        "SpotLightRange",
        "SpotLightAngle",
        "ParticlesPlaying",
        "ParticleAmount",
        "CurrentMeshAnimation",
        "MeshAnimationPlaying",
        "MeshLoading",
        "SeatOccupant",
        "SeatAllowsNPCsValue",
        "NPCHealth",
        "NPCWalkSpeed",
        "NPCJumpPower",
        "NPCIsDeadValue",
        "NPCIsOnGroundValue",
        "NPCNavigationDistance",
        "BodyPositionTarget",
        "BodyPositionForce",
        "BodyPositionAcceptanceDistance",
        "BodyPositionDistanceToTarget",
        "ObjectVelocity",
        "ObjectSpeed",
        "ObjectSpinVelocity",
        "TouchingObjectCount",
        "FogEnabled",
        "FogStartDistance",
        "FogEndDistance",
        "AmbientColor",
        "TriggeringPlayerGameTeam",
        "PlayerGameTeamName",
        "PlayerGameTeamColor",
        "GameTeamName",
        "GameTeamColor",
        "GameTeamPlayerCount",
        "GameTeamCount",
        "ToolHolder",
        "ToolCanBeDroppedValue",
        "BuiltInChatVisible",
        "BuiltInLeaderboardVisible",
        "BuiltInHealthBarVisible",
        "BuiltInHotbarVisible",
        "BuiltInBackpackAvailable",
        "BuiltInMenuButtonVisible",
        "BuiltInEmoteWheelVisible",
        "BuiltInUserCardVisible",
        "PlayerCanRespawn",
        "TriggeringPlayer",
        "TriggeringChatPlayer",
        "TriggeringChatMessage",
        "TriggeringInputAction",
        "TriggeringInputValue",
        "LocalPlayer",
        "PlayerCount",
        "FindPlayerByName",
        "FindPlayerByID",
        "InputAxisValue",
        "InputVectorX",
        "InputVectorY",
        "PlayerDefaultValue",
        "FindChild",
        "FindChildByClass",
        "ObjectChildCount",
        "TriggeringTouchObject",
        "PlayerCheckpointName",
        "PlayerCheckpointPosition",
        "PlayerRunTime",
        "PlayerDeathCount",
        "PlayerCoinCount",
        "PlayerRuntimeNumber",
        "PlayerRuntimeText",
        "PlayerRuntimeFlag",
        "Image3DColorValue",
        "Image3DCastsShadowsValue",
        "Image3DUsesLightingValue",
        "Image3DFacesCameraValue",
        "Text3DValue",
        "Text3DFontSizeValue",
        "Text3DColorValue",
        "Text3DOutlineWidthValue",
        "Text3DOutlineColorValue",
        "Text3DFacesCameraValue",
        "Text3DUsesRichTextValue",
        "Text3DUsesLightingValue",
        "UITextValue",
        "UIColorValue",
        "UIFontSizeValue",
        "UITextWrappedValue"
    };

    private static bool ParameterUsesRuntimeExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        string parameterKey)
    {
        var incoming = FindIncomingValueConnection(rule, node.Id, parameterKey);
        if (incoming is not null && nodesById.TryGetValue(incoming.From.NodeId, out var sourceNode))
        {
            return NodeUsesRuntimeExpression(rule, sourceNode, nodesById, []);
        }

        var authored = node.Parameters.FirstOrDefault(parameter => parameter.Key.Equals(parameterKey, StringComparison.OrdinalIgnoreCase));
        return authored is not null && BindingUsesRuntimeExpression(authored.Binding);
    }

    private static bool NodeUsesRuntimeExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        if (!visitedNodeIds.Add(node.Id))
        {
            return true;
        }

        if (node.Kind != NodeKind.Property)
        {
            visitedNodeIds.Remove(node.Id);
            return false;
        }

        if (RuntimeExpressionPropertyTypes.Contains(node.Type))
        {
            visitedNodeIds.Remove(node.Id);
            return true;
        }

        foreach (var parameter in node.Parameters)
        {
            var incoming = FindIncomingValueConnection(rule, node.Id, parameter.Key);
            if (incoming is not null &&
                nodesById.TryGetValue(incoming.From.NodeId, out var sourceNode) &&
                NodeUsesRuntimeExpression(rule, sourceNode, nodesById, visitedNodeIds))
            {
                visitedNodeIds.Remove(node.Id);
                return true;
            }

            if (BindingUsesRuntimeExpression(parameter.Binding))
            {
                visitedNodeIds.Remove(node.Id);
                return true;
            }
        }

        visitedNodeIds.Remove(node.Id);
        return false;
    }

    private static bool BindingUsesRuntimeExpression(GraphValueBinding binding)
    {
        if (binding.SourceKind == GraphValueSourceKind.CatalogValue && RuntimeExpressionPropertyTypes.Contains(binding.CatalogType))
        {
            return true;
        }

        return binding.CatalogParameters.Any(parameter => BindingUsesRuntimeExpression(parameter.Binding));
    }

    private static bool AppendReadableNodeLocalVariables(
        StringBuilder builder,
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        ReadableExportPlan plan)
    {
        var variableBuilder = new StringBuilder();
        var parameterValues = BuildEffectiveParameterValues(rule, node, nodesById);
        var wroteAny = false;

        if (HasConfigName(plan, node, "target"))
        {
            var targetName = ParameterValue(node, parameterValues, "target");
            if (string.IsNullOrWhiteSpace(targetName))
            {
                targetName = "Self";
            }

            variableBuilder.AppendLine($"local {ConfigName(plan, node, "target")} = {LuauStringLiteral(targetName)}");
            wroteAny = true;
        }

        if (node.Kind == NodeKind.Trigger && node.Type.Equals("OnTimerTick", StringComparison.OrdinalIgnoreCase))
        {
            var interval = ParameterExpression(rule, node, nodesById, "interval", "Number", "1");
            variableBuilder.AppendLine($"local {ConfigName(plan, node, "interval")} = {interval.Code}");
            wroteAny = true;
        }
        else if (node.Kind == NodeKind.Action &&
            node.Type.Equals("ShowMessage", StringComparison.OrdinalIgnoreCase) &&
            HasConfigName(plan, node, "message"))
        {
            var message = ParameterExpression(rule, node, nodesById, "message", "String", "");
            variableBuilder.AppendLine($"local {ConfigName(plan, node, "message")} = {message.Code}");
            wroteAny = true;
        }
        else if (node.Kind == NodeKind.Action &&
            node.Type.Equals("WaitSeconds", StringComparison.OrdinalIgnoreCase) &&
            HasConfigName(plan, node, "duration"))
        {
            var duration = ParameterExpression(rule, node, nodesById, "duration", "Number", "1");
            variableBuilder.AppendLine($"local {ConfigName(plan, node, "duration")} = {duration.Code}");
            wroteAny = true;
        }
        else if (node.Kind == NodeKind.Action &&
            node.Type.Equals("SetObjectColor", StringComparison.OrdinalIgnoreCase) &&
            HasConfigName(plan, node, "color"))
        {
            var red = ParameterExpression(rule, node, nodesById, "r", "Number", "1");
            var green = ParameterExpression(rule, node, nodesById, "g", "Number", "1");
            var blue = ParameterExpression(rule, node, nodesById, "b", "Number", "1");

            variableBuilder.AppendLine($"local {ConfigName(plan, node, "color")} = Color.New({red.Code}, {green.Code}, {blue.Code}, 1)");
            wroteAny = true;
        }

        if (!wroteAny)
        {
            return false;
        }

        builder.Append(variableBuilder);
        return true;
    }

    private static void AppendReadableScriptContext(StringBuilder builder, Rule rule, ReadableExportPlan plan)
    {
        builder.AppendLine(LuauCommentTags.VsrComment("SCRIPT CONTEXT"));
        builder.AppendLine(plan.UsesObbyPlayerState
            ? "local VRS = { actions = {}, conditions = {}, vars = {}, states = {}, playerState = {} }"
            : "local VRS = { actions = {}, conditions = {}, vars = {}, states = {} }");

        if (!plan.UsesTargetResolver &&
            !plan.UsesVectorFactory &&
            !plan.UsesVectorTween &&
            !plan.UsesObbyPlayerState &&
            !plan.UsesObbyTouchResolver &&
            !plan.UsesObbyObjectPosition &&
            !plan.UsesEssentialsRuntime &&
            !plan.UsesInputEventRuntime)
        {
            return;
        }

        if (plan.UsesObbyPlayerState)
        {
            AppendReadableObbyPlayerStateRuntime(builder);
        }

        if (plan.UsesEssentialsRuntime)
        {
            AppendReadableEssentialsRuntime(builder, includeClockAndPlayer: !plan.UsesObbyPlayerState);
        }

        if (plan.UsesObbyTouchResolver)
        {
            AppendReadableObbyTouchResolver(builder);
        }

        if (plan.UsesObbyObjectPosition)
        {
            AppendReadableObbyObjectPositionRuntime(builder);
        }

        if (plan.UsesVectorFactory)
        {
            AppendReadableVectorFactory(builder);
        }

        if (plan.UsesVectorTween)
        {
            AppendReadableVectorTweenRuntime(builder);
        }

        if (plan.UsesTargetResolver)
        {
            AppendReadableTargetResolver(builder);
        }

        if (plan.UsesInputEventRuntime)
        {
            AppendReadableInputEventRuntime(builder);
        }
    }

    private static string RegistryFunctionReference(ReadableExportPlan plan, RuleNode node)
    {
        return node.Kind switch
        {
            NodeKind.Action => $"VRS.actions.{FunctionName(plan, node)}",
            NodeKind.Condition => $"VRS.conditions.{FunctionName(plan, node)}",
            _ => FunctionName(plan, node)
        };
    }

    private static void AppendReadableScriptStart(
        StringBuilder builder,
        IReadOnlyList<RuleNode> triggers,
        ReadableExportPlan plan)
    {
        if (triggers.Count == 0)
        {
            return;
        }

        builder.AppendLine(LuauCommentTags.VsrComment("TRIGGER BOOTSTRAP"));
        foreach (var trigger in triggers.OrderBy(TriggerStartupOrder).ThenBy(trigger => trigger.GraphX).ThenBy(trigger => trigger.GraphY))
        {
            builder.AppendLine($"{FunctionName(plan, trigger)}()");
        }
    }

}
