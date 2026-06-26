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
        ["Humanoid"] = ["Humanoid"],
        ["UIButton2D"] = ["UIButton", "Button", "TextButton", "ImageButton"],
        ["UILabelObject"] = ["UILabel", "UIButton", "TextLabel", "TextButton"],
        ["UIObject"] = ["GUI", "ScreenGui", "UIView", "UIContainer", "UIField", "UILabel", "UIButton", "UIImage", "UITextInput", "Frame", "Button", "TextButton", "ImageButton", "TextLabel"],
        ["ScriptObject"] = ["ServerScript", "ClientScript", "LocalScript", "ModuleScript", "Script", "BaseScript", "ScriptInstance"],
        ["SoundObject"] = ["Sound"],
        ["TeamObject"] = ["Team"],
        ["ToolObject"] = ["Tool"],
        ["LightObject"] = ["Light", "PointLight", "SpotLight", "SunLight"],
        ["PointLightObject"] = ["PointLight"],
        ["SpotLightObject"] = ["SpotLight"],
        ["ParticleObject"] = ["Particles"],
        ["MeshObject"] = ["Mesh"],
        ["Image3DObject"] = ["Image3D"],
        ["Text3DObject"] = ["Text3D"],
        ["NPCObject"] = ["NPC"],
        ["BodyPositionObject"] = ["BodyPosition"],
        ["SeatObject"] = ["Seat"]
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
            "Humanoid" => "Humanoid objects",
            "UIButton2D" => "2D UI buttons",
            "UILabelObject" => "UI text objects",
            "UIObject" => "UI objects",
            "ScriptObject" => "script objects",
            "SoundObject" => "sound objects",
            "TeamObject" => "game teams",
            "ToolObject" => "tools",
            "LightObject" => "light objects",
            "PointLightObject" => "point lights",
            "SpotLightObject" => "spot lights",
            "ParticleObject" => "particle effects",
            "MeshObject" => "mesh objects",
            "Image3DObject" => "3D image objects",
            "Text3DObject" => "3D text objects",
            "NPCObject" => "NPC objects",
            "BodyPositionObject" => "Body Position objects",
            "SeatObject" => "seats",
            _ => group
        };
    }
}
