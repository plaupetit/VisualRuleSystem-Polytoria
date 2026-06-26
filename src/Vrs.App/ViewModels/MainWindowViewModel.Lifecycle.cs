using Vrs.App.Services;
using Vrs.Core.Catalog;
using Vrs.Core.Persistence;
using Vrs.Core.Samples;
using Vrs.Graph.Model;
using System.Diagnostics;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    public MainWindowViewModel()
    {
        var startupShellStopwatch = Stopwatch.StartNew();
        catalogIndexService = new CatalogIndexService(catalogService);
        graphInteraction = new GraphInteractionService(editor);
        scriptDeployment = new ScriptDeploymentService(bridge, inputManager);
        bridgeSync = new BridgeSyncService(bridge);
        graphRefresh = new GraphRefreshService(exporter);
        bridgeSyncTimer.Tick += async (_, _) => await RunBridgeAutoSyncAsync().ConfigureAwait(true);
        graphAutosaveTimer.Tick += async (_, _) => await RunGraphAutosaveAsync().ConfigureAwait(true);
        graph = SampleGraphFactory.CreateEmptyDraftGraph();
        documentStore.MarkDirty([GraphDocumentSection.Metadata, GraphDocumentSection.Rules, GraphDocumentSection.ViewState]);
        GraphName = graph.Name;
        GraphAutosaveEnabled = graph.Script.AutosaveEnabled;
        SnapshotStatus = "Creator hierarchy loading...";
        ProjectFileStatus = "Project files loading...";
        LuauPreview = $"{Vrs.Core.Export.LuauCommentTags.VsrComment("Script preview loading.")}{Environment.NewLine}";
        RefreshSceneObjects();
        RefreshNodes();
        NotifyExportCommandStateChanged();
        startupShellStopwatch.Stop();
        SetStartupStatus($"Startup shell ready in {startupShellStopwatch.ElapsedMilliseconds} ms.");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (isStartupInitialized || isStartupInitializing)
        {
            return;
        }

        isStartupInitializing = true;
        var startupStopwatch = Stopwatch.StartNew();
        try
        {
            await InitializeCatalogAsync(cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();

            await InitializeGraphPreviewAsync(cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();

            await InitializeProjectFilesAsync(cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();

            await RefreshSnapshot().ConfigureAwait(true);
            bridgeSyncTimer.Start();
            graphAutosaveTimer.Start();
            isStartupInitialized = true;
            startupStopwatch.Stop();
            SetStartupStatus($"Startup complete in {startupStopwatch.ElapsedMilliseconds} ms.");
        }
        catch (OperationCanceledException)
        {
            SetStartupStatus("Startup cancelled.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            SetStartupStatus($"Startup completed with errors: {ex.Message}");
            SetStatus(StartupStatus);
        }
        finally
        {
            IsCatalogLoading = false;
            IsProjectFilesLoading = false;
            IsSnapshotLoading = false;
            IsLuauPreviewLoading = false;
            isStartupInitializing = false;
        }
    }

    private async Task InitializeCatalogAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        IsCatalogLoading = true;
        SetStartupStatus("Loading node catalog...");
        var loadedCatalog = await Task.Run(() => catalogIndexService.GetCatalog(paths.CatalogRoot), cancellationToken).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
        ApplyCatalog(loadedCatalog);
        IsCatalogLoading = false;
        stopwatch.Stop();
        SetStartupStatus($"Loaded {catalog.Nodes.Count} catalog node(s) in {stopwatch.ElapsedMilliseconds} ms.");
    }

    private async Task InitializeGraphPreviewAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        IsLuauPreviewLoading = true;
        SetStartupStatus("Preparing empty draft graph and Luau preview...");
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        RuleGraphDocumentNormalizer.NormalizeScriptBinding(graph);
        RefreshAll("Loaded empty draft graph.");
        IsLuauPreviewLoading = false;
        stopwatch.Stop();
        SetStartupStatus($"Prepared empty graph preview in {stopwatch.ElapsedMilliseconds} ms.");
    }

    private async Task InitializeProjectFilesAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        SetStartupStatus("Resolving active Polytoria project...");
        deferProjectFileRefresh = true;
        try
        {
            var activeProjectRoot = await ResolveActiveProjectRoot().ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(activeProjectRoot))
            {
                SetNoProjectStatus($"No active Polytoria project. Probe: {paths.PathProbeSummary}");
            }
        }
        finally
        {
            deferProjectFileRefresh = false;
        }

        await RefreshProjectFilesAsync(cancellationToken).ConfigureAwait(true);
        stopwatch.Stop();
        SetStartupStatus($"Project files ready in {stopwatch.ElapsedMilliseconds} ms.");
    }

    partial void OnCatalogSearchChanged(string value)
    {
        RefreshCatalogList();
    }

    partial void OnSceneFilterChanged(string value)
    {
        RefreshSceneObjects();
    }

    partial void OnProjectFileFilterChanged(string value)
    {
        RefreshProjectFiles();
    }

    partial void OnSelectedNodeChanged(RuleNode? value)
    {
        if (value is not null && !string.IsNullOrWhiteSpace(SelectedFragmentId))
        {
            SelectedFragmentId = "";
        }

        if (value is not null && !string.IsNullOrWhiteSpace(SelectedGroupId))
        {
            SelectedGroupId = "";
        }

        if (value is not null && !string.IsNullOrWhiteSpace(SelectedWireRerouteId))
        {
            SelectedWireRerouteId = "";
        }

        if (value is not null)
        {
            value.DetailsOpen = true;
        }

        NotifySelectedNodeInspectorPropertiesChanged();
        RefreshSelectedNodeParameters();
        RefreshInspectorSummary();
    }

    partial void OnSelectedConnectionIndexChanged(int value)
    {
        if (value >= 0 && !string.IsNullOrWhiteSpace(SelectedFragmentId))
        {
            SelectedFragmentId = "";
        }

        if (value >= 0 && !string.IsNullOrWhiteSpace(SelectedGroupId))
        {
            SelectedGroupId = "";
        }

        if (value >= 0 && !string.IsNullOrWhiteSpace(SelectedWireRerouteId))
        {
            SelectedWireRerouteId = "";
        }

        RefreshInspectorSummary();
    }

    partial void OnSelectedFragmentIdChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            SelectedNode = null;
            SelectedConnectionIndex = -1;
        }

        if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(SelectedGroupId))
        {
            SelectedGroupId = "";
        }

        if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(SelectedWireRerouteId))
        {
            SelectedWireRerouteId = "";
        }

        NotifySelectedFragmentInspectorPropertiesChanged();
        RefreshInspectorSummary();
    }

    partial void OnSelectedGroupIdChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            SelectedNode = null;
            SelectedConnectionIndex = -1;
            SelectedFragmentId = "";
            SelectedWireRerouteId = "";
        }

        NotifySelectedGroupInspectorPropertiesChanged();
        RefreshInspectorSummary();
    }

    partial void OnSelectedWireRerouteIdChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            SelectedNode = null;
            SelectedConnectionIndex = -1;
            SelectedFragmentId = "";
            SelectedGroupId = "";
            SelectedNodeIds.Clear();
        }

        NotifySelectedWireRerouteInspectorPropertiesChanged();
        RefreshInspectorSummary();
    }

    partial void OnCurrentViewModeChanged(GraphViewMode value)
    {
        OnPropertyChanged(nameof(ShowsStateRuleBuilder));
        OnPropertyChanged(nameof(ShowsFragmentTools));
        documentStore.MarkDirty(GraphDocumentSection.ViewState);
        RefreshLuauPreview();
        SetStatus($"View mode: {value}");
    }

    partial void OnHasActiveProjectChanged(bool value)
    {
        OnPropertyChanged(nameof(CanUseCreatorBridgeCommands));
        NotifyExportCommandStateChanged();
    }

    partial void OnHasLinkedProjectChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoActiveProject));
        OnPropertyChanged(nameof(CanUseCreatorBridgeCommands));
        graph.AuthoringMode = value ? GraphAuthoringMode.CreatorLinked : GraphAuthoringMode.PolyCreatorLessDraft;
        documentStore.MarkDirty(GraphDocumentSection.Metadata);
        NotifyScriptBindingPropertiesChanged();
        NotifyExportCommandStateChanged();
    }

    partial void OnIsCreatorRuntimeReadyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanUseCreatorBridgeCommands));
        NotifyScriptBindingPropertiesChanged();
        NotifyExportCommandStateChanged();
    }

    partial void OnBridgeParentPathChanged(string value)
    {
        graph.Script.CreatorParentPath = value;
        documentStore.MarkDirty(GraphDocumentSection.Metadata);
        NotifyScriptBindingPropertiesChanged();
        NotifyDeployScriptPropertiesChanged();
    }

    partial void OnBridgeScriptNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            graph.Script.ScriptName = value.Trim();
            DraftScriptName = NormalizeDraftScriptName(value);
            documentStore.MarkDirty(GraphDocumentSection.Metadata);
            NotifyScriptBindingPropertiesChanged();
        }

        NotifyDeployScriptPropertiesChanged();
    }

    partial void OnDraftScriptNameChanged(string value)
    {
        OnPropertyChanged(nameof(ScriptCreatorPreviewName));
        OnPropertyChanged(nameof(ScriptFilePreviewPath));
        OnPropertyChanged(nameof(ScriptCreatorPreviewText));
        OnPropertyChanged(nameof(ScriptFilePreviewText));
    }

    partial void OnGraphAutosaveEnabledChanged(bool value)
    {
        graph.Script.AutosaveEnabled = value;
        documentStore.MarkDirty(GraphDocumentSection.Metadata);
        SetStatus(value ? "Graph autosave enabled." : "Graph autosave disabled.");
    }

    partial void OnActiveProjectRootChanged(string value)
    {
        NotifyDeployScriptPropertiesChanged();
        if (deferProjectFileRefresh)
        {
            return;
        }

        RefreshProjectFiles();
    }

    private void SetStartupStatus(string status)
    {
        StartupStatus = status;
        StatusText = status;
        Logs.Add($"{DateTimeOffset.Now:HH:mm:ss} {status}");
    }
}
