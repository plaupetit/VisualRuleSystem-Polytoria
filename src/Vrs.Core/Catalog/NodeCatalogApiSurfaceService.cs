namespace Vrs.Core.Catalog;

public enum NodeApiSurface
{
    Unspecified,
    Gameplay,
    Creator
}

/// <summary>
/// Classifies Polytoria API types for both user-facing palettes and generated
/// coverage reports. Keeping the rule here avoids having documentation and UI
/// filters drift into different meanings of "Gameplay" and "Creator".
/// </summary>
public static class NodeCatalogApiSurfaceService
{
    public static NodeApiSurface GetEntrySurface(NodeCatalogEntry entry)
    {
        if (entry.ApiSurface is NodeApiSurface.Gameplay or NodeApiSurface.Creator)
        {
            return entry.ApiSurface;
        }

        var referencedSurfaces = entry.ApiReferences
            .Select(reference => GetTypeSurface(reference.Type))
            .Where(surface => surface is NodeApiSurface.Gameplay or NodeApiSurface.Creator)
            .Distinct()
            .ToList();
        if (referencedSurfaces.Count == 1)
        {
            return referencedSurfaces[0];
        }

        if (referencedSurfaces.Contains(NodeApiSurface.Gameplay))
        {
            return NodeApiSurface.Gameplay;
        }

        if (!string.IsNullOrWhiteSpace(entry.ApiType))
        {
            return GetTypeSurface(BaseApiType(entry.ApiType));
        }

        if (LooksLikeCreatorMetadata(entry.ApiGroup) ||
            LooksLikeCreatorMetadata(entry.Category) ||
            LooksLikeCreatorMetadata(entry.Subcategory) ||
            LooksLikeCreatorMetadata(entry.FamilyFolder) ||
            LooksLikeCreatorMetadata(entry.UtilityLayer))
        {
            return NodeApiSurface.Creator;
        }

        return NodeApiSurface.Gameplay;
    }

    public static NodeApiSurface GetTypeSurface(string typeName)
    {
        return GetTypeSurface(typeName, GetTypeCategory(typeName));
    }

    public static NodeApiSurface GetTypeSurface(string typeName, string category)
    {
        typeName = BaseApiType(typeName);
        if (category.Equals("Creator/Addons", StringComparison.OrdinalIgnoreCase) ||
            IsCreatorInfrastructureType(typeName))
        {
            return NodeApiSurface.Creator;
        }

        return NodeApiSurface.Gameplay;
    }

    public static string GetTypeCategory(string typeName)
    {
        typeName = BaseApiType(typeName);
        if (typeName.Contains("Creator", StringComparison.OrdinalIgnoreCase) ||
            typeName.StartsWith("Addon", StringComparison.OrdinalIgnoreCase) ||
            typeName.Equals("PreferencesService", StringComparison.OrdinalIgnoreCase))
        {
            return "Creator/Addons";
        }

        if (typeName is "GUI" or "GUI3D" or "PlayerGUI" or "UIContainer" or "UIField" or "UIFlowLayout" or
            "UIGridLayout" or "UIHFlow" or "UIHLayout" or "UIHVLayout" or "UIImage" or "UILabel" or
            "UIScrollView" or "UITextInput" or "UIViewport" or "UIVFlow" or "UIVLayout" or "CoreUIService")
        {
            return "UI";
        }

        if (typeName.StartsWith("Input", StringComparison.OrdinalIgnoreCase) ||
            typeName is "NetworkEvent" or "BindableEvent" or "NetMessage" or "NetworkedObject")
        {
            return "Input/Network";
        }

        if (typeName.EndsWith("Asset", StringComparison.OrdinalIgnoreCase) ||
            typeName is "Decal" or "MeshAnimationInfo")
        {
            return "Assets";
        }

        if (typeName is "Stat" or "Stats" or "Datastore" or "DatastoreService" or "NumberValue" or "StringValue" or
            "BoolValue" or "ColorValue" or "Vector2Value" or "Vector3Value" or "InstanceValue" or "ValueBase" or
            "IntValue")
        {
            return "Data/Stats";
        }

        if (typeName is "Vector2" or "Vector3" or "Quaternion" or "Bounds" or "Color" or "ColorSeries" or "NumberRange" or "RayResult")
        {
            return "Math/Structs";
        }

        if (typeName.Equals("World", StringComparison.OrdinalIgnoreCase))
        {
            return "Runtime Gameplay";
        }

        if (typeName.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ||
            typeName is "Environment" or "Hidden" or "HiddenBase" or "ServerHidden" or
            "Temporary" or "ScriptService" or "WorldsService" or "IOService" or "FilterService" or
            "HttpRequestData" or "HttpResponseData" or "NewServerRequestData" or "PTSignal" or
            "PTSignalConnection" or "PTCallback" or "PTFunction")
        {
            return "Infrastructure";
        }

        return "Runtime Gameplay";
    }

    public static string DisplayName(NodeApiSurface surface)
    {
        return surface switch
        {
            NodeApiSurface.Creator => "Creator",
            NodeApiSurface.Gameplay => "Gameplay",
            _ => "Gameplay"
        };
    }

    private static bool LooksLikeCreatorMetadata(string value)
    {
        return value.Contains("Creator", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Addon", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Editor", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Tooling", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCreatorInfrastructureType(string typeName)
    {
        return typeName is "IOService" or "FilterService" or "HttpRequestData" or "HttpResponseData" or
            "NewServerRequestData" or "PTSignal" or "PTSignalConnection" or "PTCallback" or "PTFunction";
    }

    private static string BaseApiType(string value)
    {
        value = value.Trim();
        var dotIndex = value.IndexOf('.', StringComparison.Ordinal);
        return dotIndex <= 0 ? value : value[..dotIndex];
    }
}
