using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vrs.App.Icons;
using Vrs.App.Services;
using Vrs.Core.Catalog;
using Vrs.Core.Validation;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    // Avalonia-bound collections and generated properties live together so the
    // composition root does not become a mixed bag of state and behavior.
    public ObservableCollection<SceneObject> SceneObjects { get; } = [];
    public ObservableCollection<SceneHierarchyItemViewModel> SceneTreeRoots { get; } = [];
    public ObservableCollection<ProjectFileItemViewModel> ProjectFileTreeRoots { get; } = [];
    public ObservableCollection<RuleNode> Nodes { get; } = [];
    public ObservableCollection<GraphConnection> Connections { get; } = [];
    public ObservableCollection<GraphFragment> Fragments { get; } = [];
    public ObservableCollection<RuleNodeGroup> NodeGroups { get; } = [];
    public ObservableCollection<RuleWireReroute> WireReroutes { get; } = [];
    public ObservableCollection<string> SelectedNodeIds { get; } = [];
    public ObservableCollection<NodeCatalogEntry> CatalogEntries { get; } = [];
    public ObservableCollection<StateRuleRowViewModel> StateRuleRows { get; } = [];
    public ObservableCollection<NodeParameterEditorViewModel> SelectedNodeParameters { get; } = [];
    public ObservableCollection<NodeColorPickerViewModel> SelectedNodeColorPickers { get; } = [];
    public ObservableCollection<ColorSwatchViewModel> SessionRecentColors { get; } = [];
    public ObservableCollection<ValidationMessage> ValidationMessages { get; } = [];
    public ObservableCollection<string> Logs { get; } = [];
    public IReadOnlyList<ChoicePickerItemViewModel> SelectedNodeFallbackChoices { get; } =
    [
        new(
            "Log And Skip",
            "Log And Skip",
            "Fallback",
            "Write a runtime warning and continue with the next node.",
            IconRegistry.ForParameterType("String", "Choice"),
            ["fallback", "warning", "debug"]),
        new(
            "Skip Silently",
            "Skip Silently",
            "Fallback",
            "Ignore the failed node without adding a runtime log.",
            IconRegistry.ForParameterType("String", "Choice"),
            ["fallback", "quiet", "silent"]),
        new(
            "Stop Rule",
            "Stop Rule",
            "Fallback",
            "Stop the current rule when this node cannot complete safely.",
            IconRegistry.ForParameterType("String", "Choice"),
            ["fallback", "error", "halt"])
    ];
    public IReadOnlyList<string> SelectedNodeFallbackModes { get; } =
    [
        "Log And Skip",
        "Skip Silently",
        "Stop Rule"
    ];
    public IReadOnlyList<GraphViewMode> GraphViewModes { get; } =
    [
        GraphViewMode.Simple,
        GraphViewMode.StateMachine,
        GraphViewMode.Advanced
    ];
    public IReadOnlyList<GraphScriptKind> ScriptKinds { get; } =
    [
        GraphScriptKind.Server,
        GraphScriptKind.Local,
        GraphScriptKind.Module
    ];
    public IReadOnlyList<string> GroupColorChoices => NodeGroupColorPalette.Names;
    public IReadOnlyList<string> WireRerouteDirectionChoices => WireRerouteDirection.Choices;

    [ObservableProperty]
    private string graphName = "";

    [ObservableProperty]
    private string statusText = "";

    [ObservableProperty]
    private string startupStatus = "Starting VisualRuleSystem...";

    [ObservableProperty]
    private bool isCatalogLoading;

    [ObservableProperty]
    private bool isProjectFilesLoading;

    [ObservableProperty]
    private bool isSnapshotLoading;

    [ObservableProperty]
    private bool isLuauPreviewLoading;

    [ObservableProperty]
    private string catalogSearch = "";

    [ObservableProperty]
    private NodeCatalogEntry? selectedCatalogEntry;

    [ObservableProperty]
    private RuleNode? selectedNode;

    [ObservableProperty]
    private int selectedConnectionIndex = -1;

    [ObservableProperty]
    private string selectedFragmentId = "";

    [ObservableProperty]
    private string selectedGroupId = "";

    [ObservableProperty]
    private string selectedWireRerouteId = "";

    [ObservableProperty]
    private GraphViewMode currentViewMode = GraphViewMode.StateMachine;

    [ObservableProperty]
    private bool showAdvancedPins;

    [ObservableProperty]
    private int canvasRevision;

    [ObservableProperty]
    private string inspectorTitle = "No node selected";

    [ObservableProperty]
    private string inspectorDescription = "Select a node or wire on the canvas.";

    [ObservableProperty]
    private string inspectorDetail = "";

    [ObservableProperty]
    private string luauPreview = "";

    [ObservableProperty]
    private IReadOnlyList<int> luauPreviewHighlightedLineNumbers = [];

    [ObservableProperty]
    private int luauPreviewFocusLineNumber;

    [ObservableProperty]
    private int luauPreviewFocusRequestId;

    [ObservableProperty]
    private bool isOutputOverlayOpen;

    [ObservableProperty]
    private int selectedOutputTabIndex;

    [ObservableProperty]
    private double outputOverlayOpacity = 0.75;

    [ObservableProperty]
    private bool isOutputOverlayMouseInteractive = true;

    [ObservableProperty]
    private string bridgeFolderName = "VRS_Demo";

    [ObservableProperty]
    private string bridgeScriptName = "NewVisualScript";

    [ObservableProperty]
    private string draftScriptName = "NewVisualScript";

    [ObservableProperty]
    private string bridgeParentPath = "World/Hidden";

    [ObservableProperty]
    private string selectedCreatorObjectPath = "";

    [ObservableProperty]
    private bool bridgeDryRun = true;

    [ObservableProperty]
    private string activeProjectPath = "";

    [ObservableProperty]
    private string activeProjectName = "";

    [ObservableProperty]
    private string activeProjectRoot = "";

    [ObservableProperty]
    private string projectStatusText = "No Project Found";

    [ObservableProperty]
    private string projectStatusDetail = "";

    [ObservableProperty]
    private string projectStatusBackgroundHex = "#421818";

    [ObservableProperty]
    private string projectStatusBorderHex = "#ef4444";

    [ObservableProperty]
    private string projectStatusForegroundHex = "#ffd1d1";

    [ObservableProperty]
    private bool hasActiveProject;

    [ObservableProperty]
    private bool hasLinkedProject;

    [ObservableProperty]
    private bool isCreatorRuntimeReady;

    [ObservableProperty]
    private string projectUiModeText = "No project linked";

    [ObservableProperty]
    private string bridgeDirectory = "";

    [ObservableProperty]
    private bool isVrsWindowFocused;

    [ObservableProperty]
    private string bridgeBeatText = "Bridge not linked";

    [ObservableProperty]
    private string bridgeBeatDetail = "No active bridge directory is linked.";

    [ObservableProperty]
    private string bridgeBeatBackgroundHex = "#242936";

    [ObservableProperty]
    private string bridgeBeatBorderHex = "#64748b";

    [ObservableProperty]
    private string bridgeBeatForegroundHex = "#d7e2ee";

    [ObservableProperty]
    private string sceneFilter = "";

    [ObservableProperty]
    private string snapshotStatus = "Snapshot not loaded yet.";

    [ObservableProperty]
    private string projectFileFilter = "";

    [ObservableProperty]
    private string projectFileStatus = "No project files loaded yet.";

    [ObservableProperty]
    private string selectedProjectFilePath = "";

    [ObservableProperty]
    private int snapshotMaxObjects = 250;

    [ObservableProperty]
    private int snapshotMaxDepth = 5;

    [ObservableProperty]
    private bool includeBridgeTrash;

    [ObservableProperty]
    private bool graphAutosaveEnabled = true;

    [ObservableProperty]
    private double canvasZoom = 1.0;

    [ObservableProperty]
    private double canvasPanX;

    [ObservableProperty]
    private double canvasPanY;

    [ObservableProperty]
    private double canvasViewportWidth = 900;

    [ObservableProperty]
    private double canvasViewportHeight = 520;

    [ObservableProperty]
    private float canvasAddGraphX = 260;

    [ObservableProperty]
    private float canvasAddGraphY = 180;
}
