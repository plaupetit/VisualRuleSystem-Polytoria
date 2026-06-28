using Vrs.Graph.Model;

namespace Vrs.Core.Catalog;

/// <summary>
/// Shared scene-object filtering rules used by the inspector and validator.
/// Keep class names here so manifests can describe intent with stable groups.
/// </summary>
public static class SceneObjectKindTaxonomy
{
    private static readonly Dictionary<string, string[]> GroupKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Any"] = [],
        ["PartLike"] = ["Part", "BasePart", "MeshPart"],
        ["Touchable"] = ["Part", "BasePart", "MeshPart"],
        ["PhysicsBody"] = ["Part", "BasePart", "MeshPart", "Rigidbody", "RigidBody"],
        ["RigidBodyObject"] = ["Rigidbody", "RigidBody"],
        ["GrabbableObject"] = ["Grabbable"],
        ["Humanoid"] = ["Humanoid"],
        ["UIButton2D"] = ["UIButton", "Button", "TextButton", "ImageButton"],
        ["UILabelObject"] = ["UILabel", "UIButton", "TextLabel", "TextButton"],
        ["UITextInputObject"] = ["UITextInput", "TextInput", "InputField"],
        ["UIFieldObject"] = ["UIField", "GUI", "UIView", "UIContainer", "UILabel", "UIButton", "UIImage", "UITextInput", "Frame", "TextLabel", "TextButton", "ImageButton"],
        ["UIScrollViewObject"] = ["UIScrollView", "ScrollView", "ScrollFrame"],
        ["UIGridLayoutObject"] = ["UIGridLayout", "GridLayout"],
        ["UIHVLayoutObject"] = ["UIHVLayout", "UIHLayout", "UIVLayout", "UIHFlow", "UIVFlow", "HorizontalLayout", "VerticalLayout"],
        ["GUI3DObject"] = ["GUI3D", "Gui3D", "BillboardGui", "BillboardGUI"],
        ["UIObject"] = ["GUI", "GUI3D", "ScreenGui", "UIView", "UIContainer", "UIField", "UILabel", "UIButton", "UIImage", "UITextInput", "UIScrollView", "UIGridLayout", "UIHVLayout", "UIHLayout", "UIVLayout", "UIHFlow", "UIVFlow", "Frame", "Button", "TextButton", "ImageButton", "TextLabel"],
        ["ScriptObject"] = ["ServerScript", "ClientScript", "LocalScript", "ModuleScript", "Script", "BaseScript", "ScriptInstance"],
        ["IntValueObject"] = ["IntValue", "IntegerValue"],
        ["InstanceValueObject"] = ["InstanceValue", "ObjectValue"],
        ["SoundObject"] = ["Sound"],
        ["StatObject"] = ["Stat"],
        ["TeamObject"] = ["Team"],
        ["ToolObject"] = ["Tool"],
        ["InventoryObject"] = ["Inventory"],
        ["LightObject"] = ["Light", "PointLight", "SpotLight", "SunLight"],
        ["SunLightObject"] = ["SunLight"],
        ["PointLightObject"] = ["PointLight"],
        ["SpotLightObject"] = ["SpotLight"],
        ["ColorAdjustModifierObject"] = ["ColorAdjustModifier"],
        ["ProceduralSkyObject"] = ["ProceduralSky"],
        ["GradientSkyObject"] = ["GradientSky"],
        ["ImageSkyObject"] = ["ImageSky"],
        ["ExplosionObject"] = ["Explosion"],
        ["ParticleObject"] = ["Particles"],
        ["MeshObject"] = ["Mesh"],
        ["AnimatorObject"] = ["Animator"],
        ["CharacterModelObject"] = ["CharacterModel", "PolytorianModel", "NPC", "PlayerCharacter", "Character"],
        ["PolytorianModelObject"] = ["PolytorianModel"],
        ["AccessoryObject"] = ["Accessory"],
        ["ClothingObject"] = ["Clothing"],
        ["Marker3DObject"] = ["Marker3D"],
        ["TrussObject"] = ["Truss"],
        ["EntityObject"] = ["Entity", "Part", "BasePart", "MeshPart", "Mesh"],
        ["Image3DObject"] = ["Image3D"],
        ["Text3DObject"] = ["Text3D"],
        ["NPCObject"] = ["NPC"],
        ["BodyPositionObject"] = ["BodyPosition"],
        ["SeatObject"] = ["Seat"],
        ["AssetObject"] = ["BaseAsset", "ResourceAsset", "ImageAsset", "AudioAsset", "MeshAsset", "FontAsset", "PTImageAsset", "PTAudioAsset", "PTMeshAsset", "PTMeshAnimationAsset", "BuiltInAudioAsset", "BuiltInFontAsset", "GradientImageAsset", "FileLinkAsset", "MeshAnimationAsset"],
        ["ResourceAssetObject"] = ["ResourceAsset", "ImageAsset", "AudioAsset", "MeshAsset", "FontAsset", "PTImageAsset", "PTAudioAsset", "PTMeshAsset", "PTMeshAnimationAsset", "BuiltInAudioAsset", "BuiltInFontAsset", "GradientImageAsset", "MeshAnimationAsset"],
        ["ImageAssetObject"] = ["ImageAsset", "PTImageAsset", "GradientImageAsset"],
        ["AudioAssetObject"] = ["AudioAsset", "PTAudioAsset", "BuiltInAudioAsset"],
        ["MeshAssetObject"] = ["MeshAsset", "PTMeshAsset"],
        ["MeshAnimationAssetObject"] = ["MeshAnimationAsset", "PTMeshAnimationAsset"],
        ["FontAssetObject"] = ["FontAsset", "BuiltInFontAsset"],
        ["FileLinkAssetObject"] = ["FileLinkAsset"]
    };

    public static bool HasObjectFilter(NodeCatalogParameterDefinition definition)
    {
        return definition.AcceptedKinds.Count > 0 ||
            definition.AcceptedObjectGroups.Count > 0 ||
            definition.AcceptedSceneRoots.Count > 0;
    }

    public static bool IsAnyObject(NodeCatalogParameterDefinition definition)
    {
        return definition.AcceptedObjectGroups.Any(group => group.Equals("Any", StringComparison.OrdinalIgnoreCase)) ||
            definition.AcceptedKinds.Any(kind => kind.Equals("Any", StringComparison.OrdinalIgnoreCase));
    }

    public static bool Matches(SceneObject sceneObject, NodeCatalogParameterDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(sceneObject.Path))
        {
            return false;
        }

        if (!MatchesSceneRoot(sceneObject, definition.AcceptedSceneRoots))
        {
            return false;
        }

        if (IsAnyObject(definition) || !HasKindFilter(definition))
        {
            return true;
        }

        return AcceptedKinds(definition)
            .Any(kind => kind.Equals(sceneObject.Kind, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<string> AcceptedKinds(NodeCatalogParameterDefinition definition)
    {
        var accepted = new HashSet<string>(definition.AcceptedKinds.Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.OrdinalIgnoreCase);
        foreach (var group in definition.AcceptedObjectGroups)
        {
            if (GroupKinds.TryGetValue(group, out var kinds))
            {
                foreach (var kind in kinds)
                {
                    accepted.Add(kind);
                }
            }
        }

        accepted.Remove("Any");
        return accepted.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string ConstraintLabel(NodeCatalogParameterDefinition? definition)
    {
        if (definition is null || !HasObjectFilter(definition) || IsAnyObject(definition))
        {
            return "any scene object";
        }

        if (definition.AcceptedObjectGroups.Count > 0)
        {
            return string.Join(
                " or ",
                definition.AcceptedObjectGroups
                    .Where(group => !group.Equals("Any", StringComparison.OrdinalIgnoreCase))
                    .Select(GroupLabel));
        }

        if (definition.AcceptedKinds.Count > 0)
        {
            return string.Join(" or ", definition.AcceptedKinds);
        }

        return "objects in " + string.Join(" or ", definition.AcceptedSceneRoots);
    }

    public static string SceneRoot(SceneObject sceneObject)
    {
        return SceneRoot(sceneObject.Path);
    }

    public static string SceneRoot(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 1 && parts[0].Equals("World", StringComparison.OrdinalIgnoreCase))
        {
            return parts[1];
        }

        return parts.FirstOrDefault() ?? "Scene Object";
    }

    public static bool IsContextValue(string value)
    {
        return value is "Self" or "Parent" or "Triggering Object" or "Target" or "Player" or "Selected Object";
    }

    private static bool HasKindFilter(NodeCatalogParameterDefinition definition)
    {
        return definition.AcceptedKinds.Count > 0 ||
            definition.AcceptedObjectGroups.Any(group => !group.Equals("Any", StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesSceneRoot(SceneObject sceneObject, IReadOnlyCollection<string> acceptedRoots)
    {
        if (acceptedRoots.Count == 0)
        {
            return true;
        }

        var pathRoot = sceneObject.Path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
        var displayRoot = SceneRoot(sceneObject);
        return acceptedRoots.Any(root =>
            root.Equals(pathRoot, StringComparison.OrdinalIgnoreCase) ||
            root.Equals(displayRoot, StringComparison.OrdinalIgnoreCase));
    }

    private static string GroupLabel(string group)
    {
        return group switch
        {
            "PartLike" => "Part-like objects",
            "Touchable" => "touchable objects",
            "PhysicsBody" => "physics objects",
            "RigidBodyObject" => "rigid body objects",
            "GrabbableObject" => "grabbable objects",
            "Humanoid" => "Humanoid objects",
            "UIButton2D" => "2D UI buttons",
            "UILabelObject" => "UI text objects",
            "UITextInputObject" => "UI text input objects",
            "UIFieldObject" => "UI field objects",
            "UIScrollViewObject" => "scroll view objects",
            "UIGridLayoutObject" => "UI grid layouts",
            "UIHVLayoutObject" => "UI horizontal/vertical layouts",
            "GUI3DObject" => "3D UI objects",
            "UIObject" => "UI objects",
            "ScriptObject" => "script objects",
            "IntValueObject" => "integer value objects",
            "InstanceValueObject" => "instance value objects",
            "SoundObject" => "sound objects",
            "StatObject" => "player stats",
            "TeamObject" => "game teams",
            "ToolObject" => "tools",
            "LightObject" => "light objects",
            "PointLightObject" => "point lights",
            "SpotLightObject" => "spot lights",
            "ColorAdjustModifierObject" => "color adjust modifiers",
            "ProceduralSkyObject" => "procedural skies",
            "GradientSkyObject" => "gradient skies",
            "ImageSkyObject" => "image skies",
            "ExplosionObject" => "explosions",
            "ParticleObject" => "particle effects",
            "MeshObject" => "mesh objects",
            "AnimatorObject" => "animation controllers",
            "CharacterModelObject" => "character objects",
            "PolytorianModelObject" => "Polytorian character models",
            "AccessoryObject" => "accessories",
            "ClothingObject" => "clothing objects",
            "Marker3DObject" => "marker objects",
            "TrussObject" => "climbable trusses",
            "EntityObject" => "entity objects",
            "Image3DObject" => "3D image objects",
            "Text3DObject" => "3D text objects",
            "NPCObject" => "NPC objects",
            "BodyPositionObject" => "Body Position objects",
            "SeatObject" => "seats",
            "AssetObject" => "asset objects",
            "ResourceAssetObject" => "resource assets",
            "ImageAssetObject" => "image assets",
            "AudioAssetObject" => "audio assets",
            "MeshAssetObject" => "mesh assets",
            "MeshAnimationAssetObject" => "mesh animation assets",
            "FontAssetObject" => "font assets",
            "FileLinkAssetObject" => "file link assets",
            _ => group
        };
    }
}
