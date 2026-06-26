using System.Text.Json.Serialization;
using Vrs.Graph.Model;

namespace Vrs.Core.Catalog;

/// <summary>
/// Machine-readable node package loaded from manifest.vrs-node.json. A catalog
/// entry describes authoring capability and optional export assets; it should
/// not require a specific UI control tree or live Creator object.
/// </summary>
public sealed class NodeCatalogEntry
{
    public string ModuleId { get; set; } = "";
    public NodeKind Kind { get; set; } = NodeKind.Action;
    public string RuntimeFamily { get; set; } = "Shared";
    public string ApiGroup { get; set; } = "";
    public string Category { get; set; } = "";
    public string Subcategory { get; set; } = "";
    public string IdBase { get; set; } = "";
    public string Type { get; set; } = "";
    public string ApiType { get; set; } = "";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string Description { get; set; } = "";
    public string BeginnerSummary { get; set; } = "";
    public string Surface { get; set; } = "UserFacing";
    public List<string> PalettePath { get; set; } = [];
    public List<List<string>> PaletteAliases { get; set; } = [];
    public string Comment { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public string PreviewTemplate { get; set; } = "";
    public string FallbackMode { get; set; } = "Log And Skip";
    public bool CorePack { get; set; }
    public string FamilyFolder { get; set; } = "";
    public string UtilityLayer { get; set; } = "";
    public string Origin { get; set; } = "";
    public List<NodeCatalogApiReference> ApiReferences { get; set; } = [];
    public Dictionary<string, string> Templates { get; set; } = [];
    public string Sketcher { get; set; } = "";
    public List<string> SearchKeywords { get; set; } = [];
    public List<NodeCatalogSelectorHint> SelectorHints { get; set; } = [];
    public List<string> DebugHints { get; set; } = [];
    public List<NodeCatalogPortDefinition> Ports { get; set; } = [];
    public List<NodeCatalogParameterDefinition> Parameters { get; set; } = [];
    public List<NodeCatalogSetupRequirement> SetupRequirements { get; set; } = [];

    /// <summary>
    /// Filled by the loader. It is not persisted inside strict package JSON.
    /// </summary>
    [JsonIgnore]
    public string SourcePath { get; set; } = "";

    /// <summary>
    /// Filled by the loader so template paths can stay package-relative.
    /// </summary>
    [JsonIgnore]
    public string PackageDirectory { get; set; } = "";
}

/// <summary>
/// Optional port override for packages that need more than the default ports
/// implied by their NodeKind.
/// </summary>
public sealed class NodeCatalogPortDefinition
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public NodePortDirection Direction { get; set; } = NodePortDirection.Input;
    public NodePortKind PortKind { get; set; } = NodePortKind.Flow;
    public string DataType { get; set; } = "Flow";
    public string ColorHex { get; set; } = "";
    public int Order { get; set; }
}

/// <summary>
/// Advises inspectors and validators which value sources make sense for a
/// parameter without forcing one concrete UI implementation.
/// </summary>
public sealed class NodeCatalogSelectorHint
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public string DataType { get; set; } = "Any";
    public List<GraphValueSourceKind> AllowedSources { get; set; } = [];
    public List<GraphVariableScope> AllowedScopes { get; set; } = [];
}

/// <summary>
/// Describes one configurable input of a node. The manifest owns labels,
/// defaults, option metadata, and human help text; runtime services decide how
/// to render, validate, or export the value.
/// </summary>
public sealed class NodeCatalogParameterDefinition
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "String";
    public string Control { get; set; } = "Text";
    public bool Required { get; set; }
    public string Default { get; set; } = "";
    public string Fallback { get; set; } = "";
    public string ValueSource { get; set; } = "String / Manual Text Input";
    public bool AllowCustom { get; set; } = true;
    public List<string> Options { get; set; } = [];
    public List<NodeCatalogOptionDetail> OptionDetails { get; set; } = [];
    public List<string> AcceptedKinds { get; set; } = [];
    public List<string> AcceptedObjectGroups { get; set; } = [];
    public List<string> AcceptedSceneRoots { get; set; } = [];
    public List<string> SearchKeywords { get; set; } = [];
    public List<NodeCatalogParameterVisibilityCondition> VisibleWhen { get; set; } = [];
    public List<NodeCatalogSelectorHint> SelectorHints { get; set; } = [];
    public List<NodeCatalogSnippet> Snippets { get; set; } = [];
}

/// <summary>
/// Optional authoring rule that lets a manifest hide advanced or variant-only
/// parameters without moving UI-specific logic into the graph model.
/// </summary>
public sealed class NodeCatalogParameterVisibilityCondition
{
    public string ParameterKey { get; set; } = "";
    [JsonPropertyName("equals")]
    public string EqualsValue { get; set; } = "";
    [JsonPropertyName("notEquals")]
    public string NotEqualsValue { get; set; } = "";
    public List<string> In { get; set; } = [];
    public List<string> NotIn { get; set; } = [];
}

/// <summary>
/// Human-facing metadata for one option value. Value stays stable for save/load
/// and export, while Label and Description can be friendlier than the raw token.
/// </summary>
public sealed class NodeCatalogOptionDetail
{
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> SearchKeywords { get; set; } = [];
}

/// <summary>
/// Small reusable catalog-authored code or help snippet reserved for richer
/// node authoring tools.
/// </summary>
public sealed class NodeCatalogSnippet
{
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public string Code { get; set; } = "";
}

/// <summary>
/// Declares external project or hierarchy setup a node needs before deployment.
/// Creator-side tools remain responsible for applying those requirements.
/// </summary>
public sealed class NodeCatalogSetupRequirement
{
    public string Path { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Reason { get; set; } = "";
    public bool Required { get; set; } = true;
}

public sealed class NodeCatalogData
{
    public List<NodeCatalogEntry> Nodes { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public string SourceLabel { get; set; } = "";
}

/// <summary>
/// Optional trace from a human-facing node to the public Polytoria API surface it covers.
/// </summary>
public sealed class NodeCatalogApiReference
{
    public string Type { get; set; } = "";
    public string MemberKind { get; set; } = "";
    public string Member { get; set; } = "";
    public string Coverage { get; set; } = "Direct";
}
