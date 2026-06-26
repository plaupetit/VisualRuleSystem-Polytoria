namespace Vrs.Graph.Model;

/// <summary>
/// High-level node families used by the human-readable rule graph. These are
/// authoring roles, not container or engine classes.
/// </summary>
public enum NodeKind
{
    /// <summary>
    /// Starts rule execution, for example when a script starts or a timer ticks.
    /// </summary>
    Trigger,

    /// <summary>
    /// Decides which branch of a rule should run without performing the action itself.
    /// </summary>
    Condition,

    /// <summary>
    /// Performs work after a trigger or branch reaches it.
    /// </summary>
    Action,

    /// <summary>
    /// Produces a value for another node; it should not start or advance flow by itself.
    /// </summary>
    Property,

    /// <summary>
    /// Points to external authoring context such as documentation or future linked assets.
    /// </summary>
    Reference
}

public enum NodePortDirection
{
    Input,
    Output
}

public enum NodePortKind
{
    Flow,
    Value,
    Target,
    State,
    Data
}

public enum GraphConnectionKind
{
    Flow,
    Value,
    Reference
}

public enum GraphFragmentKind
{
    State,
    Rule,
    Sequence,
    Macro,
    Utility
}

public enum GraphViewMode
{
    Simple,
    StateMachine,
    Advanced
}

public enum GraphScriptKind
{
    Server,
    Local,
    Module
}

public enum GraphAuthoringMode
{
    PolyCreatorLessDraft,
    CreatorLinked
}

public enum GraphValueSourceKind
{
    /// <summary>
    /// Uses the literal value typed in the inspector. This means "manual value",
    /// not continuous gameplay behavior.
    /// </summary>
    Constant,

    /// <summary>
    /// Reads a variable scoped to the generated script or local authoring context.
    /// </summary>
    LocalVariable,

    /// <summary>
    /// Reads a variable intended to be shared beyond one generated script.
    /// </summary>
    GlobalVariable,

    /// <summary>
    /// Uses the object that owns the deployed script.
    /// </summary>
    Self,

    /// <summary>
    /// Uses the target object supplied by the trigger, branch, or previous flow.
    /// </summary>
    Target,

    /// <summary>
    /// Uses the player supplied by the currently running trigger context.
    /// </summary>
    TriggeringPlayer,

    /// <summary>
    /// Uses an abstract scene path chosen from a snapshot; the model does not know the container API.
    /// </summary>
    SceneObject,

    /// <summary>
    /// Receives the value through a graph connection instead of the inspector.
    /// </summary>
    ConnectedPort,

    /// <summary>
    /// Builds the parameter value from a catalog-authored value recipe rather than a visible flow node.
    /// </summary>
    CatalogValue
}

public enum GraphVariableScope
{
    Script,
    Graph,
    State,
    Local,
    Global,
    Object,
    Scene,
    App
}

public enum GraphVariablePersistence
{
    RuntimeOnly,
    Session,
    Saved,
    Project
}

/// <summary>
/// A complete visual scripting document. This is the neutral persistence root:
/// platform-specific behavior belongs in catalog, export, validation, or bridge
/// services so the graph can survive container changes.
/// </summary>
public sealed class RuleGraph
{
    public string Schema { get; set; } = "VisualRuleSystem.RuleGraph";
    public int Version { get; set; } = 3;
    public string Name { get; set; } = "VisualRuleSystemDraft";
    public GraphAuthoringMode AuthoringMode { get; set; } = GraphAuthoringMode.PolyCreatorLessDraft;
    public GraphScriptBinding Script { get; set; } = new();
    public List<SceneObject> SceneObjects { get; set; } = [];
    public List<VisualVariable> SharedVariables { get; set; } = [];
    public List<Rule> Rules { get; set; } = [];
}

/// <summary>
/// Script-level authoring metadata. Rule.ScriptKind is kept for old saved
/// graphs and exporters, while this binding records how the document is linked
/// to a Polytoria script or an offline draft.
/// </summary>
public sealed class GraphScriptBinding
{
    public string ScriptName { get; set; } = "NewVisualScript";
    public GraphScriptKind ScriptKind { get; set; } = GraphScriptKind.Server;
    public string ProjectRelativePath { get; set; } = "";
    public string CreatorParentPath { get; set; } = "";
    public string CreatorObjectPath { get; set; } = "";
    public string LinkedScriptPath { get; set; } = "";
    public string Source { get; set; } = "Draft";
    public bool IsScriptKindLocked { get; set; }
    public bool AutosaveEnabled { get; set; } = true;
}

/// <summary>
/// A node graph rule. Nodes and explicit port-to-port connections are the source
/// of truth; execution order is derived by services, never stored as container
/// callbacks or engine-specific script objects.
/// </summary>
public sealed class Rule
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Comment { get; set; } = "";
    public GraphScriptKind ScriptKind { get; set; } = GraphScriptKind.Server;
    public List<RuleNode> Nodes { get; set; } = [];
    public List<GraphConnection> Connections { get; set; } = [];
    public List<GraphFragment> Fragments { get; set; } = [];
    public List<RuleNodeGroup> NodeGroups { get; set; } = [];
    public List<RuleWireReroute> WireReroutes { get; set; } = [];
    public List<VisualVariable> ScriptVariables { get; set; } = [];
}

/// <summary>
/// A visual node plus its authoring metadata. The catalog identifies what the
/// node can do, while this instance stores graph-local position, user notes,
/// enabled state, and parameter choices.
/// </summary>
public sealed class RuleNode
{
    public NodeKind Kind { get; set; } = NodeKind.Trigger;
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string Description { get; set; } = "";
    public string Comment { get; set; } = "";
    public string UserComment { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool DebugEnabled { get; set; }
    public bool Breakpoint { get; set; }
    public bool DetailsOpen { get; set; }
    public string FragmentId { get; set; } = "";
    public bool Collapsed { get; set; }
    public bool ExposeAdvancedPorts { get; set; }
    public string FallbackMode { get; set; } = "Log And Skip";
    public string FallbackNote { get; set; } = "";
    public List<NodePort> Ports { get; set; } = [];
    public List<RuleParameter> Parameters { get; set; } = [];
    public List<RuleNode> ChildNodes { get; set; } = [];
    public string CompositeMode { get; set; } = "All";
    public float GraphX { get; set; }
    public float GraphY { get; set; }
    public bool GraphPositionSet { get; set; }
    public string CatalogId { get; set; } = "";
}

public sealed class NodePort
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public NodePortDirection Direction { get; set; } = NodePortDirection.Input;
    public NodePortKind PortKind { get; set; } = NodePortKind.Flow;
    public string DataType { get; set; } = "Flow";
    public string ColorHex { get; set; } = "#7c8794";
    public int Order { get; set; }
}

/// <summary>
/// Stores the effective parameter value and how the author chose it. Value is
/// kept as the compatibility/executable fallback; Binding preserves the richer
/// authoring intent used by UI, validation, and exporters.
/// </summary>
public sealed class RuleParameter
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string ValueSource { get; set; } = "String / Manual Text Input";
    public bool CustomValue { get; set; }
    public string SourceCatalogId { get; set; } = "";
    public GraphValueBinding Binding { get; set; } = new();
}

/// <summary>
/// Typed authoring source for a parameter value. The binding describes intent
/// in container-neutral terms, so exporters can translate it without putting
/// Polytoria Creator or Avalonia concepts into the graph model.
/// </summary>
public sealed class GraphValueBinding
{
    public GraphValueSourceKind SourceKind { get; set; } = GraphValueSourceKind.Constant;
    public string DataType { get; set; } = "String";
    public string ConstantValue { get; set; } = "";
    public string VariableName { get; set; } = "";
    public GraphVariableScope VariableScope { get; set; } = GraphVariableScope.Script;
    public string SceneObjectPath { get; set; } = "";
    public string SourceNodeId { get; set; } = "";
    public string SourcePortId { get; set; } = "";
    public string CatalogId { get; set; } = "";
    public string CatalogType { get; set; } = "";
    public List<RuleParameter> CatalogParameters { get; set; } = [];
    public string DisplayText { get; set; } = "";
}

public sealed class GraphEndpoint
{
    public string NodeId { get; set; } = "";
    public string PortId { get; set; } = "";
}

public sealed class GraphConnection
{
    public string Id { get; set; } = "";
    public GraphEndpoint From { get; set; } = new();
    public GraphEndpoint To { get; set; } = new();
    public GraphConnectionKind ConnectionKind { get; set; } = GraphConnectionKind.Flow;
    public List<string> RerouteIds { get; set; } = [];
    public string Comment { get; set; } = "";
}

/// <summary>
/// Authoring layer that lets a GC2-like state/rule block collapse a set of
/// Unreal-like nodes without creating a second execution model.
/// </summary>
public sealed class GraphFragment
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public GraphFragmentKind Kind { get; set; } = GraphFragmentKind.Rule;
    public List<string> NodeIds { get; set; } = [];
    public List<string> ConnectionIds { get; set; } = [];
    public bool Collapsed { get; set; }
    public float GraphX { get; set; }
    public float GraphY { get; set; }
    public string Comment { get; set; } = "";
}

/// <summary>
/// Visual-only grouping metadata. Export ignores groups so organization never
/// changes gameplay behavior.
/// </summary>
public sealed class RuleNodeGroup
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Comment { get; set; } = "";
    public string Color { get; set; } = "Teal";
    public string ParentGroupId { get; set; } = "";
    public List<string> MemberNodeIds { get; set; } = [];
    public List<string> MemberRerouteIds { get; set; } = [];
    public float GraphX { get; set; }
    public float GraphY { get; set; }
    public float Width { get; set; } = 360.0F;
    public float Height { get; set; } = 220.0F;
    public bool Collapsed { get; set; }
}

/// <summary>
/// Visual-only wire anchor used to keep larger graphs readable.
/// </summary>
public sealed class RuleWireReroute
{
    public string Id { get; set; } = "";
    public float GraphX { get; set; }
    public float GraphY { get; set; }
    public string InputDirection { get; set; } = "Left";
    public string OutputDirection { get; set; } = "Right";
}

public static class WireRerouteDirection
{
    public const string Left = "Left";
    public const string Right = "Right";
    public const string Up = "Up";
    public const string Down = "Down";

    public static readonly IReadOnlyList<string> Choices = [Left, Right, Up, Down];

    public static bool IsValid(string value)
    {
        return Choices.Any(choice => string.Equals(choice, value, StringComparison.OrdinalIgnoreCase));
    }

    public static string Normalize(string value, string fallback)
    {
        return Choices.FirstOrDefault(choice => string.Equals(choice, value, StringComparison.OrdinalIgnoreCase)) ?? fallback;
    }
}

public sealed class VisualVariable
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public GraphVariableScope Scope { get; set; } = GraphVariableScope.Script;
    public string ValueKind { get; set; } = "String";
    public bool IsList { get; set; }
    public string DefaultValue { get; set; } = "";
    public string Description { get; set; } = "";
    public GraphVariablePersistence Persistence { get; set; } = GraphVariablePersistence.RuntimeOnly;
    public string RuntimePreviewValue { get; set; } = "";
    public string Comment { get; set; } = "";
    public string UserComment { get; set; } = "";
}

public sealed class SceneObject
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Path { get; set; } = "";
    public string LinkedScriptPath { get; set; } = "";
    public bool IsLinkedScript { get; set; }
    public bool IsVisualScriptName { get; set; }
}
