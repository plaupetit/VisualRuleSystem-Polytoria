using Avalonia.Threading;
using Vrs.App.Services;
using Vrs.Core.Bridge;
using Vrs.Core.Catalog;
using Vrs.Core.Export;
using Vrs.Core.Persistence;
using Vrs.Core.ProjectInputs;
using Vrs.Graph.Interaction;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.App.ViewModels;

/// <summary>
/// Avalonia shell composition root for the prototype. Partial files keep bound
/// state, lifecycle hooks, command workflows, and graph interaction separated
/// while this class wires focused services around the neutral graph model.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IGraphInteractionHost
{
    // These services mark the current composition boundary between UI state and
    // container-neutral core behavior.
    private readonly WorkspacePathService paths = new();
    private readonly NodeCatalogService catalogService = new();
    private readonly CatalogIndexService catalogIndexService;
    private readonly GraphDocumentStore documentStore = new();
    private readonly LuauExporter exporter = new();
    private readonly BridgeFileService bridge = new();
    private readonly ProjectInputManagerService inputManager = new();
    private readonly RuleGraphEditService editor = new();
    private readonly RuleGraphClipboardService graphClipboard = new();
    private readonly GraphInteractionService graphInteraction;
    private readonly GraphRefreshService graphRefresh;
    private readonly ProjectRuntimeStatusService projectRuntimeStatus = new();
    private readonly SceneTreeBuilderService sceneTreeBuilder = new();
    private readonly ProjectFileTreeBuilderService projectFileTreeBuilder = new();
    private readonly SelectionInspectorService selectionInspector = new();
    private readonly GraphViewportService graphViewport = new();
    private readonly ScriptGraphLoadService scriptGraphLoader = new();
    private readonly ScriptDeploymentService scriptDeployment;
    private readonly BridgeSyncService bridgeSync;
    private readonly DispatcherTimer bridgeSyncTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer graphAutosaveTimer = new() { Interval = TimeSpan.FromMilliseconds(750) };

    private RuleGraph graph = new();
    private NodeCatalogData catalog = new();
    private bool isBridgeSyncRunning;
    private bool isGraphAutosaveRunning;
    private bool isStartupInitializing;
    private bool isStartupInitialized;
    private bool deferProjectFileRefresh;
    private bool hasInitializedCommandResults;
    private bool sceneTreeWasFiltering;
    private BridgeSyncResult? lastBridgeSyncResult;
    private IReadOnlyList<VrsInputActionChoice> inputActionChoices = VrsInputPresetCatalog.DefaultChoices;
    private HashSet<string> unfilteredSceneExpandedPaths = new(StringComparer.OrdinalIgnoreCase);
    private string lastObservedCommandResultId = "";
}
