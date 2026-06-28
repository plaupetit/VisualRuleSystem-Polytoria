using System.Text.Json;
using Vrs.App.Services;
using Vrs.App.ViewModels;
using Vrs.Core.Bridge;
using Vrs.Core.Persistence;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.Tests;

public sealed class AppViewModelTests
{
    [Fact]
    public void Constructor_DoesNotLoadCatalogSnapshotOrProjectFiles()
    {
        var viewModel = new MainWindowViewModel();

        Assert.Empty(viewModel.CatalogEntries);
        Assert.Empty(viewModel.Nodes);
        Assert.Empty(viewModel.ProjectFileTreeRoots);
        Assert.True(viewModel.IsPolyCreatorLessDraft);
        Assert.True(viewModel.StartupStatus.Length > 0);
        Assert.Contains("loading", viewModel.ProjectFileStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("loading", viewModel.SnapshotStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InitializedViewModel_StartsAsEmptyDraft()
    {
        var viewModel = CreateInitializedViewModel(loadSample: false);

        Assert.Empty(viewModel.Nodes);
        Assert.Equal(GraphScriptKind.Server, viewModel.SelectedScriptKind);
        Assert.Equal(!viewModel.HasLinkedProject, viewModel.IsPolyCreatorLessDraft);
        Assert.False(viewModel.ExportLuauCommand.CanExecute(null));
        Assert.False(viewModel.DeployScriptFileCommand.CanExecute(null));
        Assert.Contains(viewModel.HasLinkedProject ? "Project Server" : "Draft Server", viewModel.ScriptBindingSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void AddSelectedCatalogNodeCommand_AddsAndSelectsHumanFlowNode()
    {
        var viewModel = CreateInitializedViewModel();
        var initialCount = viewModel.Nodes.Count;
        Assert.Contains(viewModel.CatalogEntries, entry => entry.Kind == NodeKind.Property);

        var condition = viewModel.CatalogEntries.Single(entry => entry.IdBase == "COND_NumberCompare");
        viewModel.SelectedCatalogEntry = condition;
        viewModel.CanvasAddGraphX = 123;
        viewModel.CanvasAddGraphY = 456;

        viewModel.AddSelectedCatalogNodeAtCanvasPositionCommand.Execute(null);

        var added = viewModel.Nodes.Single(node =>
            node.Kind == NodeKind.Condition &&
            node.CatalogId == "COND_NumberCompare" &&
            node.GraphX == 123 &&
            node.GraphY == 456);

        Assert.Equal(initialCount + 1, viewModel.Nodes.Count);
        Assert.Same(added, viewModel.SelectedNode);
    }

    [Fact]
    public void EmptyCanvas_ClearsPreviewAndBlocksScriptExportUntilNodeReturns()
    {
        var viewModel = CreateInitializedViewModel();

        while (viewModel.Nodes.Count > 0)
        {
            viewModel.SelectedNode = viewModel.Nodes[0];
            viewModel.DeleteSelectionCommand.Execute(null);
        }

        Assert.Empty(viewModel.Nodes);
        Assert.Null(viewModel.SelectedNode);
        Assert.Equal("", viewModel.LuauPreview);
        Assert.False(viewModel.ExportLuauCommand.CanExecute(null));
        Assert.False(viewModel.DeployScriptFileCommand.CanExecute(null));

        viewModel.SelectedCatalogEntry = viewModel.CatalogEntries.First(entry => entry.Kind == NodeKind.Trigger);
        viewModel.AddSelectedCatalogNodeAtCanvasPositionCommand.Execute(null);

        Assert.Single(viewModel.Nodes);
        Assert.Same(viewModel.Nodes.Single(), viewModel.SelectedNode);
        Assert.NotEqual("", viewModel.LuauPreview);
        Assert.True(viewModel.ExportLuauCommand.CanExecute(null));
        Assert.False(viewModel.DeployScriptFileCommand.CanExecute(null));
    }

    [Fact]
    public async Task SceneHierarchyClick_LoadsGraphFromLinkedScriptMetadata()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-linked-load-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var viewModel = new MainWindowViewModel();
            await viewModel.InitializeAsync();
            viewModel.LoadTimerMessageSampleCommand.Execute(null);
            var exportedWithMetadata = viewModel.LuauPreview;
            var linkedPath = Path.Combine(tempRoot, "scripts", "VRS", "server", "VRS TimerMessage.server.luau");
            Directory.CreateDirectory(Path.GetDirectoryName(linkedPath)!);
            await File.WriteAllTextAsync(linkedPath, exportedWithMetadata);

            while (viewModel.Nodes.Count > 0)
            {
                viewModel.SelectedNode = viewModel.Nodes[0];
                viewModel.DeleteSelectionCommand.Execute(null);
            }

            Assert.Empty(viewModel.Nodes);

            viewModel.ActiveProjectRoot = tempRoot;
            var item = new SceneHierarchyItemViewModel(new SceneObject
            {
                Name = "VRS TimerMessage",
                Kind = "ServerScript",
                Path = "World/Hidden/VRS TimerMessage",
                LinkedScriptPath = "scripts/VRS/server/VRS TimerMessage.server.luau",
                IsLinkedScript = true
            });

            await viewModel.SelectSceneHierarchyItemAsync(item);

            Assert.NotEmpty(viewModel.Nodes);
            Assert.Equal("World/Hidden", viewModel.BridgeParentPath);
            Assert.Equal("VRS TimerMessage", viewModel.BridgeScriptName);
            Assert.Equal(GraphScriptKind.Server, viewModel.SelectedScriptKind);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ProjectFileLoad_LoadsGraphFromVrsLuauMetadata()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-file-load-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var source = CreateInitializedViewModel();
            var exportedWithMetadata = source.LuauPreview;
            var relativePath = "scripts/VRS/server/VRS TimerMessage.server.luau";
            var scriptPath = Path.Combine(tempRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
            await File.WriteAllTextAsync(scriptPath, exportedWithMetadata);

            var viewModel = CreateInitializedViewModel(loadSample: false);
            viewModel.ActiveProjectRoot = tempRoot;
            var item = ProjectFileItem(tempRoot, relativePath);

            var loaded = await viewModel.LoadGraphFromProjectFileAsync(item);

            Assert.True(loaded);
            Assert.NotEmpty(viewModel.Nodes);
            Assert.Equal(GraphScriptKind.Server, viewModel.SelectedScriptKind);
            Assert.Equal("VRS TimerMessage", viewModel.BridgeScriptName);
            Assert.Equal(relativePath, viewModel.SelectedProjectFilePath);
            Assert.Contains("Loaded VRS graph from:", viewModel.StatusText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ProjectFileLoad_AppliesKindNameAndPreviewFromFilePath()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-file-load-kind-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var source = CreateInitializedViewModel();
            var relativePath = "scripts/VRS/client/VRS Input Rule.client.luau";
            var scriptPath = Path.Combine(tempRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
            await File.WriteAllTextAsync(scriptPath, source.LuauPreview);

            var viewModel = CreateInitializedViewModel(loadSample: false);
            viewModel.ActiveProjectRoot = tempRoot;

            var loaded = await viewModel.LoadGraphFromProjectFileAsync(ProjectFileItem(tempRoot, relativePath));

            Assert.True(loaded);
            Assert.Equal(GraphScriptKind.Local, viewModel.SelectedScriptKind);
            Assert.Equal("VRS Input Rule", viewModel.BridgeScriptName);
            Assert.Equal("Input Rule", viewModel.DraftScriptName);
            Assert.Equal("scripts/VRS/client/VRS Input Rule.client.luau", viewModel.ScriptFilePreviewPath);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ProjectFileLoad_BlocksLuauWithoutVrsMetadata()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-file-load-missing-metadata-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var relativePath = "scripts/VRS/server/VRS Plain.server.luau";
            var scriptPath = Path.Combine(tempRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
            await File.WriteAllTextAsync(scriptPath, "print('plain luau')");

            var viewModel = CreateInitializedViewModel();
            var nodeCount = viewModel.Nodes.Count;
            viewModel.ActiveProjectRoot = tempRoot;

            var loaded = await viewModel.LoadGraphFromProjectFileAsync(ProjectFileItem(tempRoot, relativePath));

            Assert.False(loaded);
            Assert.Equal(nodeCount, viewModel.Nodes.Count);
            Assert.Contains("No VRS graph metadata found", viewModel.StatusText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ProjectFileLoad_BlocksMissingOrNonLuauFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-file-load-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var viewModel = CreateInitializedViewModel(loadSample: false);
            viewModel.ActiveProjectRoot = tempRoot;

            var missing = await viewModel.LoadGraphFromProjectFileAsync(ProjectFileItem(tempRoot, "scripts/VRS/server/VRS Missing.server.luau"));
            Assert.Contains("Linked script file was not found", viewModel.StatusText, StringComparison.Ordinal);

            var nonLuau = await viewModel.LoadGraphFromProjectFileAsync(ProjectFileItem(tempRoot, "scripts/VRS/server/readme.md", ".md"));

            Assert.False(missing);
            Assert.False(nonLuau);
            Assert.Contains("Cannot load this file as a VRS graph.", viewModel.StatusText, StringComparison.Ordinal);
            Assert.Empty(viewModel.Nodes);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void NodeContextFlags_ToggleDebugAndBreakpointMarkers()
    {
        var viewModel = CreateInitializedViewModel();
        var node = viewModel.Nodes.First();
        viewModel.SelectedNode = node;
        var previewBeforeInspectorToggle = viewModel.LuauPreview;

        viewModel.SelectedNodeDebugEnabled = true;
        viewModel.ToggleGraphNodeBreakpoint(node.Id);

        Assert.True(node.DebugEnabled);
        Assert.True(viewModel.SelectedNodeDebugEnabled);
        Assert.True(node.Breakpoint);
        Assert.NotEqual(previewBeforeInspectorToggle, viewModel.LuauPreview);

        viewModel.ToggleGraphNodeDebug(node.Id);

        Assert.False(node.DebugEnabled);
        Assert.False(viewModel.SelectedNodeDebugEnabled);
    }

    [Fact]
    public void SelectedNodeFallbackChoices_MirrorFallbackModesAndUpdateSelectedNode()
    {
        var viewModel = CreateInitializedViewModel();
        var node = viewModel.Nodes.First();
        viewModel.SelectedNode = node;

        Assert.Equal(viewModel.SelectedNodeFallbackModes, viewModel.SelectedNodeFallbackChoices.Select(choice => choice.Value));
        Assert.All(viewModel.SelectedNodeFallbackChoices, choice =>
        {
            Assert.False(string.IsNullOrWhiteSpace(choice.Label));
            Assert.Equal("Fallback", choice.Category);
            Assert.False(string.IsNullOrWhiteSpace(choice.Description));
            Assert.False(string.IsNullOrWhiteSpace(choice.Tooltip));
        });

        viewModel.SelectedNodeFallbackMode = "Stop Rule";

        Assert.Equal("Stop Rule", node.FallbackMode);
        Assert.Equal("Stop Rule", viewModel.SelectedNodeFallbackMode);
    }

    [Fact]
    public void ScriptDeployPreview_ReportsProjectFileReadiness()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-viewmodel-deploy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var viewModel = CreateInitializedViewModel();
            viewModel.ActiveProjectRoot = tempRoot;

            var missing = viewModel.BuildScriptDeployPreview("TimerMessage", GraphScriptKind.Server);

            Assert.Equal("VRS TimerMessage", missing.CreatorScriptName);
            Assert.Equal("scripts/VRS/server/VRS TimerMessage.server.luau", missing.ProjectRelativePath);
            Assert.False(missing.ProjectFileExists);
            Assert.False(missing.ProjectFileReady);
            Assert.Equal("File missing", missing.ProjectFileStatusText);

            var linkedPath = Path.Combine(tempRoot, "scripts", "VRS", "server", "VRS TimerMessage.server.luau");
            Directory.CreateDirectory(Path.GetDirectoryName(linkedPath)!);
            File.WriteAllText(linkedPath, "");

            var empty = viewModel.BuildScriptDeployPreview("TimerMessage", GraphScriptKind.Server);

            Assert.True(empty.ProjectFileExists);
            Assert.False(empty.ProjectFileReady);
            Assert.Equal("File empty", empty.ProjectFileStatusText);

            File.WriteAllText(linkedPath, "-- linked");

            var ready = viewModel.BuildScriptDeployPreview("TimerMessage", GraphScriptKind.Server);

            Assert.True(ready.ProjectFileExists);
            Assert.True(ready.ProjectFileReady);
            Assert.Equal("File ready", ready.ProjectFileStatusText);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ScriptRenameCommand_UpdatesTargetPreviewAndKeepsScriptKind()
    {
        var viewModel = CreateInitializedViewModel(loadSample: false);

        Assert.Equal(GraphScriptKind.Server, viewModel.SelectedScriptKind);

        viewModel.DraftScriptName = "TimerMessage";
        viewModel.ApplyScriptRenameCommand.Execute(null);

        Assert.Equal("TimerMessage", viewModel.BridgeScriptName);
        Assert.Equal("TimerMessage", viewModel.DraftScriptName);
        Assert.Equal("VRS TimerMessage", viewModel.ScriptCreatorPreviewName);
        Assert.Equal("Creator: VRS TimerMessage", viewModel.ScriptCreatorPreviewText);
        Assert.Equal("scripts/VRS/server/VRS TimerMessage.server.luau", viewModel.ScriptFilePreviewPath);
        Assert.Equal(GraphScriptKind.Server, viewModel.SelectedScriptKind);
        Assert.Contains("Renamed script target", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void ScriptRenameCommand_NormalizesPrefixAndEmptyNames()
    {
        var viewModel = CreateInitializedViewModel(loadSample: false);

        viewModel.DraftScriptName = "VRS TimerMessage";
        viewModel.ApplyScriptRenameCommand.Execute(null);

        Assert.Equal("TimerMessage", viewModel.BridgeScriptName);
        Assert.Equal("VRS TimerMessage", viewModel.ScriptCreatorPreviewName);
        Assert.DoesNotContain("VRS VRS", viewModel.ScriptCreatorPreviewName, StringComparison.OrdinalIgnoreCase);

        viewModel.DraftScriptName = "   ";
        viewModel.ApplyScriptRenameCommand.Execute(null);

        Assert.Equal("NewVisualScript", viewModel.BridgeScriptName);
        Assert.Equal("NewVisualScript", viewModel.DraftScriptName);
        Assert.Equal("VRS NewVisualScript", viewModel.ScriptCreatorPreviewName);
    }

    [Fact]
    public void CreateStateFragmentCommand_CollapsesSelectedNodeIntoFragment()
    {
        var viewModel = CreateInitializedViewModel();
        viewModel.SelectedNode = viewModel.Nodes.First();

        viewModel.CreateStateFragmentFromSelectionCommand.Execute(null);

        var fragment = viewModel.Fragments.Single();
        Assert.Equal(GraphFragmentKind.State, fragment.Kind);
        Assert.True(fragment.Collapsed);
        Assert.Equal(fragment.Id, viewModel.SelectedFragmentId);
        Assert.Contains(viewModel.Nodes.First().Id, fragment.NodeIds);
    }

    [Fact]
    public void CreateNodeGroupCommand_PublishesSelectableEditableGroup()
    {
        var viewModel = CreateInitializedViewModel();
        viewModel.SelectGraphNodes(viewModel.Nodes.Select(node => node.Id).ToList(), viewModel.Nodes.First().Id);

        viewModel.CreateNodeGroupFromSelectionCommand.Execute(null);

        var group = Assert.Single(viewModel.NodeGroups);
        Assert.Equal(group.Id, viewModel.SelectedGroupId);
        Assert.True(viewModel.HasSelectedGroup);
        Assert.False(viewModel.ShowsGenericInspectorHeader);

        viewModel.SelectedGroupName = "Readable Flow";
        viewModel.SelectedGroupColor = "Purple";

        Assert.Equal("Readable Flow", group.Name);
        Assert.Equal("Purple", group.Color);
        Assert.Equal(viewModel.Nodes.Count, viewModel.SelectedGroupMemberCount);

        viewModel.DeleteSelectionCommand.Execute(null);

        Assert.Empty(viewModel.NodeGroups);
        Assert.Equal("", viewModel.SelectedGroupId);
        Assert.NotEmpty(viewModel.Nodes);
    }

    [Fact]
    public void WireRerouteSelection_PublishesInspectorAndCanDelete()
    {
        var viewModel = CreateInitializedViewModel();

        viewModel.AddWireRerouteToConnection(0, 320, 180, insertAtIndex: 0);

        var reroute = Assert.Single(viewModel.WireReroutes);
        Assert.Equal(reroute.Id, viewModel.SelectedWireRerouteId);
        Assert.True(viewModel.HasSelectedWireReroute);
        Assert.False(viewModel.ShowsGenericInspectorHeader);

        viewModel.SelectedWireRerouteInputDirection = WireRerouteDirection.Up;
        viewModel.SelectedWireRerouteOutputDirection = WireRerouteDirection.Down;

        Assert.Equal(WireRerouteDirection.Up, reroute.InputDirection);
        Assert.Equal(WireRerouteDirection.Down, reroute.OutputDirection);

        viewModel.DeleteSelectionCommand.Execute(null);

        Assert.Empty(viewModel.WireReroutes);
        Assert.Equal("", viewModel.SelectedWireRerouteId);
    }

    [Fact]
    public void AddRerouteToSelectedWireCommand_AddsRerouteAtSelectedConnection()
    {
        var viewModel = CreateInitializedViewModel();
        viewModel.SelectGraphConnection(0);

        viewModel.AddRerouteToSelectedWireCommand.Execute(null);

        var reroute = Assert.Single(viewModel.WireReroutes);
        Assert.Equal(reroute.Id, viewModel.SelectedWireRerouteId);
        Assert.Equal(reroute.Id, Assert.Single(viewModel.Connections.Single().RerouteIds));
    }

    [Fact]
    public void CanvasSelectionTargets_AreMutuallyExclusive()
    {
        var viewModel = CreateInitializedViewModel();
        var firstNode = viewModel.Nodes.First();
        var secondNode = viewModel.Nodes.Last();

        viewModel.SelectGraphNodes([firstNode.Id, secondNode.Id], firstNode.Id);

        Assert.Same(firstNode, viewModel.SelectedNode);
        Assert.Equal([firstNode.Id, secondNode.Id], viewModel.SelectedNodeIds);
        Assert.Equal(-1, viewModel.SelectedConnectionIndex);

        viewModel.SelectedConnectionIndex = 0;

        Assert.Null(viewModel.SelectedNode);
        Assert.Empty(viewModel.SelectedNodeIds);
        Assert.Equal("", viewModel.SelectedGroupId);
        Assert.Equal("", viewModel.SelectedWireRerouteId);

        viewModel.CreateEmptyGroupAtGraphPoint(100, 100);

        var group = Assert.Single(viewModel.NodeGroups);
        Assert.Equal(group.Id, viewModel.SelectedGroupId);
        Assert.Null(viewModel.SelectedNode);
        Assert.Empty(viewModel.SelectedNodeIds);
        Assert.Equal(-1, viewModel.SelectedConnectionIndex);

        viewModel.AddWireRerouteToConnection(0, 320, 180, insertAtIndex: 0);

        var reroute = Assert.Single(viewModel.WireReroutes);
        Assert.Equal(reroute.Id, viewModel.SelectedWireRerouteId);
        Assert.Equal("", viewModel.SelectedGroupId);
        Assert.Null(viewModel.SelectedNode);
        Assert.Empty(viewModel.SelectedNodeIds);
        Assert.Equal(-1, viewModel.SelectedConnectionIndex);
    }

    [Fact]
    public void ColorNodeParameters_ArePresentedAsPolytoriaColorPicker()
    {
        var viewModel = CreateInitializedViewModel();
        var colorEntry = viewModel.CatalogEntries.Single(entry => entry.IdBase == "ACT_SetObjectColor");
        viewModel.SelectedCatalogEntry = colorEntry;

        viewModel.AddSelectedCatalogNodeAtCanvasPositionCommand.Execute(null);

        var picker = Assert.Single(viewModel.SelectedNodeColorPickers);
        Assert.Equal("#FFFFFF", picker.HexColor);
        Assert.DoesNotContain(viewModel.SelectedNodeParameters, parameter => parameter.Key is "r" or "g" or "b");

        picker.HexColor = "#00A3FF";

        Assert.Equal("0", viewModel.SelectedNode!.Parameters.Single(parameter => parameter.Key == "r").Value);
        Assert.Equal("0.639216", viewModel.SelectedNode.Parameters.Single(parameter => parameter.Key == "g").Value);
        Assert.Equal("1", viewModel.SelectedNode.Parameters.Single(parameter => parameter.Key == "b").Value);
    }

    [Fact]
    public void CreateNodeGroupCommand_CanGroupSelectedWireReroute()
    {
        var viewModel = CreateInitializedViewModel();
        viewModel.AddWireRerouteToConnection(0, 320, 180, insertAtIndex: 0);
        var reroute = Assert.Single(viewModel.WireReroutes);

        viewModel.CreateNodeGroupFromSelectionCommand.Execute(null);

        var group = Assert.Single(viewModel.NodeGroups);
        Assert.Equal(group.Id, viewModel.SelectedGroupId);
        Assert.Empty(group.MemberNodeIds);
        Assert.Contains(reroute.Id, group.MemberRerouteIds);
    }

    [Fact]
    public void NotifyGraphGroupsMoved_PersistsUngroupedMovedNodesAndReroutesInPrimaryGroup()
    {
        var viewModel = CreateInitializedViewModel();
        var primaryNode = viewModel.Nodes.First();
        var movedNode = viewModel.Nodes.Last();
        viewModel.SelectGraphNodes([primaryNode.Id], primaryNode.Id);
        viewModel.CreateNodeGroupFromSelectionCommand.Execute(null);
        var group = viewModel.NodeGroups.Single();
        viewModel.AddWireRerouteToConnection(0, group.GraphX + group.Width + 120, group.GraphY + group.Height + 120, insertAtIndex: 0);
        var reroute = viewModel.WireReroutes.Single();
        Assert.DoesNotContain(movedNode.Id, group.MemberNodeIds);
        Assert.DoesNotContain(reroute.Id, group.MemberRerouteIds);

        viewModel.NotifyGraphGroupsMoved(
            [new GraphGroupMove(group.Id, group.GraphX + 20, group.GraphY + 20)],
            [new GraphNodeMove(movedNode.Id, movedNode.GraphX + 20, movedNode.GraphY + 20)],
            [new GraphWireRerouteMove(reroute.Id, reroute.GraphX + 20, reroute.GraphY + 20)],
            group.Id);

        Assert.Contains(movedNode.Id, group.MemberNodeIds);
        Assert.Contains(reroute.Id, group.MemberRerouteIds);
    }

    [Fact]
    public void StateRuleBuilderRows_ReflectFragmentsAndGrammar()
    {
        var viewModel = CreateInitializedViewModel();

        Assert.Contains(viewModel.StateRuleRows, row => row.TriggerSummary.StartsWith("On:", StringComparison.Ordinal));

        viewModel.SelectedNode = viewModel.Nodes.First(node => node.Kind == NodeKind.Trigger);
        viewModel.CreateStateFragmentFromSelectionCommand.Execute(null);
        viewModel.SelectedNode = viewModel.Nodes.First(node => node.Kind == NodeKind.Action);
        viewModel.CreateRuleFragmentFromSelectionCommand.Execute(null);

        Assert.Contains(viewModel.StateRuleRows, row => row.Kind == GraphFragmentKind.State);
        Assert.Contains(viewModel.StateRuleRows, row => row.Kind == GraphFragmentKind.Rule);
    }

    [Fact]
    public void ViewModeDefaultsToStateMachineWithAdvancedPinsOff()
    {
        var viewModel = new MainWindowViewModel();

        Assert.Equal(GraphViewMode.StateMachine, viewModel.CurrentViewMode);
        Assert.False(viewModel.ShowAdvancedPins);
        Assert.False(viewModel.ShowsStateRuleBuilder);
        Assert.False(viewModel.ShowsFragmentTools);

        viewModel.CurrentViewMode = GraphViewMode.Advanced;

        Assert.True(viewModel.ShowsFragmentTools);
    }

    [Fact]
    public void ScriptKind_IsLockedAfterGraphHasContent()
    {
        var viewModel = CreateInitializedViewModel();

        Assert.Equal(GraphScriptKind.Server, viewModel.SelectedScriptKind);

        viewModel.SelectedScriptKind = GraphScriptKind.Local;

        Assert.Equal(GraphScriptKind.Server, viewModel.SelectedScriptKind);
        Assert.Contains("locked", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeployFileThenInstance_AppliesExplicitLocalScriptType()
    {
        var tempRoot = CreateTempPolytoriaProject("vrs-viewmodel-local-deploy");

        try
        {
            var viewModel = CreateDeployableViewModel(tempRoot);
            AddCatalogNode(viewModel, "EV_OnStart");
            var item = new SceneHierarchyItemViewModel(new SceneObject
            {
                Name = "Baseplate",
                Kind = "Part",
                Path = "World/Environment/Baseplate"
            });

            await viewModel.DeployScriptFileAsync(GraphScriptKind.Local, "TimerMessage");

            Assert.Equal(GraphScriptKind.Local, viewModel.SelectedScriptKind);
            Assert.Equal("TimerMessage", viewModel.BridgeScriptName);
            Assert.Equal("scripts/VRS/client/VRS TimerMessage.client.luau", viewModel.ScriptFilePreviewPath);
            Assert.True(File.Exists(Path.Combine(tempRoot, "scripts", "VRS", "client", "VRS TimerMessage.client.luau")));
            Assert.False(File.Exists(Path.Combine(tempRoot, "addons", "visual-programming-bridge", "bridge", "pending-commands.json")));

            var deployTask = viewModel.DeployScriptInstanceToSceneItemAsync(item, GraphScriptKind.Local, "TimerMessage", dryRun: true);
            await CompleteNextUpsertCommandAsync(tempRoot);
            await deployTask;

            Assert.Equal(GraphScriptKind.Local, viewModel.SelectedScriptKind);
            Assert.Equal("TimerMessage", viewModel.BridgeScriptName);
            Assert.Equal("scripts/VRS/client/VRS TimerMessage.client.luau", viewModel.ScriptFilePreviewPath);

            var command = await ReadLatestPendingUpsertAsync(tempRoot);
            Assert.Equal("ClientScript", command.Kind);
            Assert.Equal("Local", command.ScriptKind);
            Assert.True(command.DryRun);
            Assert.Equal("World/Environment/Baseplate/VRS TimerMessage", command.TargetPath);
            Assert.Contains("Creator applied", viewModel.StatusText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DeployFileThenInstance_AppliesExplicitModuleScriptType()
    {
        var tempRoot = CreateTempPolytoriaProject("vrs-viewmodel-module-deploy");

        try
        {
            var viewModel = CreateDeployableViewModel(tempRoot);
            AddCatalogNode(viewModel, "EV_OnStart");
            var item = new SceneHierarchyItemViewModel(new SceneObject
            {
                Name = "Scripts",
                Kind = "Folder",
                Path = "World/Hidden"
            });

            await viewModel.DeployScriptFileAsync(GraphScriptKind.Module, "VRS SharedConfig");

            Assert.Equal(GraphScriptKind.Module, viewModel.SelectedScriptKind);
            Assert.Equal("SharedConfig", viewModel.BridgeScriptName);
            Assert.Equal("VRS SharedConfig", viewModel.ScriptCreatorPreviewName);
            Assert.Equal("scripts/VRS/module/VRS SharedConfig.module.luau", viewModel.ScriptFilePreviewPath);
            Assert.True(File.Exists(Path.Combine(tempRoot, "scripts", "VRS", "module", "VRS SharedConfig.module.luau")));
            Assert.False(File.Exists(Path.Combine(tempRoot, "addons", "visual-programming-bridge", "bridge", "pending-commands.json")));

            var deployTask = viewModel.DeployScriptInstanceToSceneItemAsync(item, GraphScriptKind.Module, "VRS SharedConfig", dryRun: false);
            await CompleteNextUpsertCommandAsync(tempRoot);
            await deployTask;

            Assert.Equal(GraphScriptKind.Module, viewModel.SelectedScriptKind);
            Assert.Equal("SharedConfig", viewModel.BridgeScriptName);
            Assert.Equal("VRS SharedConfig", viewModel.ScriptCreatorPreviewName);
            Assert.Equal("scripts/VRS/module/VRS SharedConfig.module.luau", viewModel.ScriptFilePreviewPath);
            var command = await ReadLatestPendingUpsertAsync(tempRoot);
            Assert.Equal("ModuleScript", command.Kind);
            Assert.Equal("Module", command.ScriptKind);
            Assert.False(command.DryRun);
            Assert.Equal("World/Hidden/VRS SharedConfig", command.TargetPath);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DeploySavedInstance_UsesCurrentSavedScriptNotClickedItemName()
    {
        var tempRoot = CreateTempPolytoriaProject("vrs-viewmodel-saved-instance-deploy");

        try
        {
            var viewModel = CreateDeployableViewModel(tempRoot);
            AddCatalogNode(viewModel, "EV_OnStart");
            var item = new SceneHierarchyItemViewModel(new SceneObject
            {
                Name = "Baseplate",
                Kind = "Part",
                Path = "World/Environment/Kill Brick"
            });

            await viewModel.DeployScriptFileAsync(GraphScriptKind.Server, "KillBrickt");
            var scriptPath = Path.Combine(tempRoot, "scripts", "VRS", "server", "VRS KillBrickt.server.luau");
            var originalContent = await File.ReadAllTextAsync(scriptPath);

            var deployTask = viewModel.DeployCurrentSavedScriptInstanceToSceneItemAsync(item, dryRun: true);
            await CompleteNextUpsertCommandAsync(tempRoot);
            await deployTask;

            var command = await ReadLatestPendingUpsertAsync(tempRoot);
            Assert.Equal("VRS KillBrickt", command.Name);
            Assert.Equal("World/Environment/Kill Brick/VRS KillBrickt", command.TargetPath);
            Assert.Equal("scripts/VRS/server/VRS KillBrickt.server.luau", command.LinkedFilePath);
            Assert.True(command.DryRun);
            Assert.Equal(originalContent, await File.ReadAllTextAsync(scriptPath));
            Assert.Equal("KillBrickt", viewModel.BridgeScriptName);
            Assert.Equal(GraphScriptKind.Server, viewModel.SelectedScriptKind);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DeploySavedInstance_UsesScriptParentWhenClickingExistingScriptInstance()
    {
        var tempRoot = CreateTempPolytoriaProject("vrs-viewmodel-saved-instance-script-parent");

        try
        {
            var viewModel = CreateDeployableViewModel(tempRoot);
            AddCatalogNode(viewModel, "EV_OnStart");
            var item = new SceneHierarchyItemViewModel(new SceneObject
            {
                Name = "VRS KillBrickt",
                Kind = "ServerScript",
                Path = "World/Environment/Kill Brick/VRS KillBrickt",
                LinkedScriptPath = "scripts/VRS/server/VRS KillBrickt.server.luau",
                IsLinkedScript = true
            });

            await viewModel.DeployScriptFileAsync(GraphScriptKind.Server, "KillBrickt");

            var deployTask = viewModel.DeployCurrentSavedScriptInstanceToSceneItemAsync(item, dryRun: true);
            await CompleteNextUpsertCommandAsync(tempRoot);
            await deployTask;

            var command = await ReadLatestPendingUpsertAsync(tempRoot);
            Assert.Equal("World/Environment/Kill Brick/VRS KillBrickt", command.TargetPath);
            Assert.Equal("KillBrickt", viewModel.BridgeScriptName);
            Assert.Equal(GraphScriptKind.Server, viewModel.SelectedScriptKind);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DeploySavedInstance_BlocksUntilCurrentProjectFileExists()
    {
        var tempRoot = CreateTempPolytoriaProject("vrs-viewmodel-saved-instance-missing-file");

        try
        {
            var viewModel = CreateDeployableViewModel(tempRoot);
            AddCatalogNode(viewModel, "EV_OnStart");
            var item = new SceneHierarchyItemViewModel(new SceneObject
            {
                Name = "Baseplate",
                Kind = "Part",
                Path = "World/Environment/Baseplate"
            });

            await viewModel.DeployCurrentSavedScriptInstanceToSceneItemAsync(item, dryRun: true);

            Assert.Equal(GraphScriptKind.Server, viewModel.SelectedScriptKind);
            Assert.Equal("NewVisualScript", viewModel.BridgeScriptName);
            Assert.Contains("Deploy File first", viewModel.StatusText, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(tempRoot, "addons", "visual-programming-bridge", "bridge", "pending-commands.json")));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DeploySavedInstance_BlocksWhenCurrentProjectFileIsEmpty()
    {
        var tempRoot = CreateTempPolytoriaProject("vrs-viewmodel-saved-instance-empty-file");

        try
        {
            var viewModel = CreateDeployableViewModel(tempRoot);
            AddCatalogNode(viewModel, "EV_OnStart");
            var item = new SceneHierarchyItemViewModel(new SceneObject
            {
                Name = "Baseplate",
                Kind = "Part",
                Path = "World/Environment/Baseplate"
            });
            var linkedPath = Path.Combine(tempRoot, "scripts", "VRS", "server", "VRS NewVisualScript.server.luau");
            Directory.CreateDirectory(Path.GetDirectoryName(linkedPath)!);
            File.WriteAllText(linkedPath, "");

            await viewModel.DeployCurrentSavedScriptInstanceToSceneItemAsync(item, dryRun: true);

            Assert.Equal(GraphScriptKind.Server, viewModel.SelectedScriptKind);
            Assert.Equal("NewVisualScript", viewModel.BridgeScriptName);
            Assert.Contains("Deploy File first", viewModel.StatusText, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(tempRoot, "addons", "visual-programming-bridge", "bridge", "pending-commands.json")));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DeployInstance_BlocksUntilProjectFileExists()
    {
        var tempRoot = CreateTempPolytoriaProject("vrs-viewmodel-instance-missing-file");

        try
        {
            var viewModel = CreateDeployableViewModel(tempRoot);
            AddCatalogNode(viewModel, "EV_OnStart");
            var item = new SceneHierarchyItemViewModel(new SceneObject
            {
                Name = "Baseplate",
                Kind = "Part",
                Path = "World/Environment/Baseplate"
            });

            await viewModel.DeployScriptInstanceToSceneItemAsync(item, GraphScriptKind.Local, "TimerMessage", dryRun: true);

            Assert.Equal(GraphScriptKind.Server, viewModel.SelectedScriptKind);
            Assert.Equal("NewVisualScript", viewModel.BridgeScriptName);
            Assert.Contains("Deploy File first", viewModel.StatusText, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(tempRoot, "scripts", "VRS", "client", "VRS TimerMessage.client.luau")));
            Assert.False(File.Exists(Path.Combine(tempRoot, "addons", "visual-programming-bridge", "bridge", "pending-commands.json")));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DeployInstance_BlocksWhenProjectFileIsEmpty()
    {
        var tempRoot = CreateTempPolytoriaProject("vrs-viewmodel-instance-empty-file");

        try
        {
            var viewModel = CreateDeployableViewModel(tempRoot);
            AddCatalogNode(viewModel, "EV_OnStart");
            var item = new SceneHierarchyItemViewModel(new SceneObject
            {
                Name = "Baseplate",
                Kind = "Part",
                Path = "World/Environment/Baseplate"
            });
            var linkedPath = Path.Combine(tempRoot, "scripts", "VRS", "server", "VRS TimerMessage.server.luau");
            Directory.CreateDirectory(Path.GetDirectoryName(linkedPath)!);
            File.WriteAllText(linkedPath, "");

            await viewModel.DeployScriptInstanceToSceneItemAsync(item, GraphScriptKind.Server, "TimerMessage", dryRun: true);

            Assert.Contains("Deploy File first", viewModel.StatusText, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(tempRoot, "addons", "visual-programming-bridge", "bridge", "pending-commands.json")));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DeployFile_BlocksIncompatibleScriptKindWithoutChangingCurrentType()
    {
        var tempRoot = CreateTempPolytoriaProject("vrs-viewmodel-blocked-deploy");

        try
        {
            var viewModel = CreateDeployableViewModel(tempRoot);
            AddCatalogNode(viewModel, "EV_OnTimerTick");
            var item = new SceneHierarchyItemViewModel(new SceneObject
            {
                Name = "Baseplate",
                Kind = "Part",
                Path = "World/Baseplate"
            });

            await viewModel.DeployScriptFileAsync(GraphScriptKind.Local, "TimerMessage");

            Assert.Equal(GraphScriptKind.Server, viewModel.SelectedScriptKind);
            Assert.Equal("NewVisualScript", viewModel.BridgeScriptName);
            Assert.Contains("Cannot deploy as Local", viewModel.StatusText, StringComparison.Ordinal);
            Assert.Contains("On Timer Tick", viewModel.StatusText, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(tempRoot, "scripts", "VRS", "client", "VRS TimerMessage.client.luau")));
            Assert.False(File.Exists(Path.Combine(tempRoot, "addons", "visual-programming-bridge", "bridge", "pending-commands.json")));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ProjectLinkedButCreatorOff_BlocksDeployFileAndInstanceFlows()
    {
        var tempRoot = CreateTempPolytoriaProject("vrs-viewmodel-creator-off-deploy");

        try
        {
            var viewModel = CreateInitializedViewModel(loadSample: false);
            viewModel.ActiveProjectRoot = tempRoot;
            viewModel.HasLinkedProject = true;
            viewModel.IsCreatorRuntimeReady = false;
            AddCatalogNode(viewModel, "EV_OnStart");
            var item = new SceneHierarchyItemViewModel(new SceneObject
            {
                Name = "Baseplate",
                Kind = "Part",
                Path = "World/Environment/Baseplate"
            });

            await viewModel.DeployScriptFileAsync(GraphScriptKind.Local, "TimerMessage");

            Assert.Contains("bridge is not running", viewModel.StatusText, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(tempRoot, "scripts", "VRS", "client", "VRS TimerMessage.client.luau")));

            await viewModel.DeployScriptInstanceToSceneItemAsync(item, GraphScriptKind.Local, "TimerMessage", dryRun: true);

            Assert.Contains("bridge is not running", viewModel.StatusText, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(tempRoot, "addons", "visual-programming-bridge", "bridge", "pending-commands.json")));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void NewScriptCommands_CreateEmptyLockedGraphsWithChosenType()
    {
        var viewModel = CreateInitializedViewModel();

        viewModel.NewLocalScriptCommand.Execute(null);

        Assert.Empty(viewModel.Nodes);
        Assert.Equal(GraphScriptKind.Local, viewModel.SelectedScriptKind);
        Assert.Contains(viewModel.HasLinkedProject ? "Project Local" : "Draft Local", viewModel.ScriptBindingSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectLinkedButCreatorOff_UsesProjectBadgeAndBlocksBridgeCommands()
    {
        var viewModel = CreateInitializedViewModel(loadSample: false);

        viewModel.HasLinkedProject = true;
        viewModel.IsCreatorRuntimeReady = false;

        Assert.False(viewModel.IsPolyCreatorLessDraft);
        Assert.False(viewModel.CanUseCreatorBridgeCommands);
        Assert.False(viewModel.QueueCreateFolderCommand.CanExecute(null));
        Assert.False(viewModel.DeployScriptFileCommand.CanExecute(null));
        Assert.Contains("Project Server", viewModel.ScriptBindingSummary, StringComparison.Ordinal);
        Assert.Contains("Creator off", viewModel.ScriptBindingSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("Draft", viewModel.ScriptBindingSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestCreatorSnapshot_WritesSnapshotRequestAndAppState()
    {
        var tempRoot = CreateTempPolytoriaProject("vrs-viewmodel-snapshot-request");

        try
        {
            var viewModel = CreateInitializedViewModel(loadSample: false);
            viewModel.ActiveProjectRoot = tempRoot;
            viewModel.ActiveProjectName = "SnapshotProject";
            viewModel.HasLinkedProject = true;
            viewModel.IsCreatorRuntimeReady = false;
            viewModel.SetWindowFocusState(true);
            AddCatalogNode(viewModel, "EV_OnStart");

            Assert.True(viewModel.RequestCreatorSnapshotCommand.CanExecute(null));

            await viewModel.RequestCreatorSnapshotCommand.ExecuteAsync(null);

            var bridgeDirectory = Path.Combine(tempRoot, "addons", "visual-programming-bridge", "bridge");
            var request = JsonSerializer.Deserialize(
                await File.ReadAllTextAsync(Path.Combine(bridgeDirectory, "snapshot-request.json")),
                VrsJsonContext.Default.SnapshotRequest);
            var appState = JsonSerializer.Deserialize(
                await File.ReadAllTextAsync(Path.Combine(bridgeDirectory, "app-state.json")),
                VrsJsonContext.Default.BridgeAppState);

            Assert.NotNull(request);
            Assert.Equal("manual-vrs-workspace-request", request!.Reason);
            Assert.Equal("full", request.Mode);
            Assert.False(string.IsNullOrWhiteSpace(request.SessionId));
            Assert.NotNull(appState);
            Assert.True(appState!.ProjectLinked);
            Assert.False(appState.CreatorReady);
            Assert.True(appState.Focused);
            Assert.Equal("Server", appState.ScriptKind);
            Assert.Equal("VRS NewVisualScript", appState.CreatorScriptName);
            Assert.Equal("scripts/VRS/server/VRS NewVisualScript.server.luau", appState.ProjectRelativeScriptPath);
            Assert.Equal(1, appState.NodeCount);
            Assert.Contains("Requested Creator snapshot", viewModel.StatusText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void CancelProjectSelection_DoesNotClearCurrentProject()
    {
        var viewModel = new MainWindowViewModel
        {
            ActiveProjectRoot = @"D:\Polytoria\ExistingProject",
            ActiveProjectName = "ExistingProject",
            HasLinkedProject = true,
            IsCreatorRuntimeReady = false
        };

        viewModel.CancelProjectSelection();

        Assert.Equal(@"D:\Polytoria\ExistingProject", viewModel.ActiveProjectRoot);
        Assert.Equal("ExistingProject", viewModel.ActiveProjectName);
        Assert.True(viewModel.HasLinkedProject);
        Assert.Contains("cancelled", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureInputManager_WritesPresetsAndReportsBridgeRequirementWhenCreatorIsOff()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-input-manager-vm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "project.ptproj"), "{}");

        try
        {
            var viewModel = new MainWindowViewModel
            {
                ActiveProjectRoot = tempRoot,
                ActiveProjectName = "InputProject",
                HasLinkedProject = true,
                IsCreatorRuntimeReady = false
            };

            Assert.True(viewModel.EnsureInputManagerCommand.CanExecute(null));

            await viewModel.EnsureInputManagerCommand.ExecuteAsync(null);

            Assert.Contains("Creator bridge required", viewModel.StatusText, StringComparison.Ordinal);
            using var inputDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(tempRoot, "input.json")));
            var actions = inputDocument.RootElement.GetProperty("Actions").EnumerateArray().ToList();

            Assert.Contains(actions, action => action.GetProperty("Name").GetString() == "Jump");
            Assert.Contains(actions, action => action.GetProperty("Name").GetString() == "Interact");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void MainToolbar_UsesCompactDropdownsForScriptAndGraphActions()
    {
        var xaml = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "src", "Vrs.App", "Views", "MainWindow.axaml"));
        var propertySelectorXaml = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "src", "Vrs.App", "Controls", "PropertySelectorControl.axaml"));

        Assert.Contains("<Button Content=\"New Script\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<Button Content=\"Graph\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MenuItem Header=\"Server Script\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MenuItem Header=\"Save Graph\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Auto\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Deploy File\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Input Manager\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visual Scripting Workspace", xaml, StringComparison.Ordinal);
        Assert.Contains("Creator Bridge", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Request Snapshot\"", xaml, StringComparison.Ordinal);
        Assert.Contains("RequestCreatorSnapshotCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("CreatorBridgeWorkspaceSummary", xaml, StringComparison.Ordinal);
        Assert.Contains("EnsureInputManagerCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("InputChoiceSourceFilterChoices", propertySelectorXaml, StringComparison.Ordinal);
        Assert.Contains("InputChoiceSourceFilter", propertySelectorXaml, StringComparison.Ordinal);
        Assert.Contains("ShowsInputChoiceSourceFilter", propertySelectorXaml, StringComparison.Ordinal);
        Assert.Contains("DeployScriptFileCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ScriptFilePreviewText", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding GraphAutosaveTooltip}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("BridgeBeatText", xaml, StringComparison.Ordinal);
        Assert.Contains("BridgeBeatDetail", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Script target", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Apply Rename", xaml, StringComparison.Ordinal);
        Assert.Contains("IsOutputOverlayOpen", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedOutputTabIndex", xaml, StringComparison.Ordinal);
        Assert.Contains("OutputOverlayOpacity", xaml, StringComparison.Ordinal);
        Assert.Contains("IsOutputOverlayMouseInteractive", xaml, StringComparison.Ordinal);
        Assert.Contains("ToggleOutputOverlayCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("CloseOutputOverlayCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("Output: F11", xaml, StringComparison.Ordinal);
        Assert.Contains("F11 toggle · Esc close", xaml, StringComparison.Ordinal);
        Assert.Contains("Script Code Preview", xaml, StringComparison.Ordinal);
        Assert.Contains("Validation", xaml, StringComparison.Ordinal);
        Assert.Contains("Logs", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Full\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Grid.Row=\"3\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Button Content=\"New Server\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Button Content=\"New Local\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Button Content=\"New Module\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Button Content=\"Save Graph\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Button Content=\"Load Graph\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Button Content=\"Export Graph\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Button Content=\"Import Graph\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SceneHierarchyContextMenu_DeploysSavedScriptInstanceWithoutEditableDialog()
    {
        var codeBehind = ReadMainWindowCodeBehind();

        Assert.Contains("ShowScriptRenameDialogAsync", codeBehind, StringComparison.Ordinal);
        Assert.Contains("BuildSavedScriptInstanceDeployPreview", codeBehind, StringComparison.Ordinal);
        Assert.Contains("DeployCurrentSavedScriptInstanceToSceneItemAsync", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Saved file:", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Instance:", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Deploy Saved Script Instance Here", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Dry Run Saved Instance Here", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Script file setup", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Deploy File first", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ProjectFileReady", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ProjectFileStatusText", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Rename Script Target...", codeBehind, StringComparison.Ordinal);
        Assert.Contains("DraftScriptName", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ApplyScriptRenameCommand", codeBehind, StringComparison.Ordinal);
        Assert.Contains("As Script Name", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Set Deploy Target", codeBehind, StringComparison.Ordinal);
        Assert.Contains("LoadGraphFromSceneScriptWithConfirmationAsync", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ConfirmLoadGraphReplacementAsync", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowScriptDeployDialogAsync", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("Deploy Script Instance", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("Deploy Instance Here...", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("Dry Run Instance Here...", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("DeployScriptToSceneItemAsync(item, dryRun", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("Deploy Here...", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("Dry Run Deploy Here...", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectFileContextMenu_LoadsVrsGraphFilesWithConfirmation()
    {
        var codeBehind = ReadMainWindowCodeBehind();

        Assert.Contains("Load VRS Graph From File", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ProjectFileItemPointerPressed", codeBehind, StringComparison.Ordinal);
        Assert.Contains("OpenProjectFileContextMenu", codeBehind, StringComparison.Ordinal);
        Assert.Contains("LoadGraphFromProjectFileWithConfirmationAsync", codeBehind, StringComparison.Ordinal);
        Assert.Contains("ConfirmLoadGraphReplacementAsync", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Content = \"Load\"", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Content = \"Cancel\"", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void OutputOverlayCommands_ToggleCloseAndKeepSelectedTab()
    {
        var viewModel = new MainWindowViewModel();

        Assert.False(viewModel.IsOutputOverlayOpen);
        Assert.Equal(0, viewModel.SelectedOutputTabIndex);
        Assert.Equal(0.75, viewModel.OutputOverlayOpacity, precision: 2);
        Assert.True(viewModel.IsOutputOverlayMouseInteractive);

        viewModel.SelectedOutputTabIndex = 2;
        viewModel.IsOutputOverlayMouseInteractive = false;
        viewModel.ToggleOutputOverlayCommand.Execute(null);

        Assert.True(viewModel.IsOutputOverlayOpen);
        Assert.Equal(2, viewModel.SelectedOutputTabIndex);
        Assert.False(viewModel.IsOutputOverlayMouseInteractive);

        viewModel.ToggleOutputOverlayCommand.Execute(null);

        Assert.False(viewModel.IsOutputOverlayOpen);
        Assert.Equal(2, viewModel.SelectedOutputTabIndex);

        viewModel.CloseOutputOverlayCommand.Execute(null);

        Assert.False(viewModel.IsOutputOverlayOpen);
        Assert.Equal(2, viewModel.SelectedOutputTabIndex);
    }

    [Fact]
    public void MainWindowCodeBehind_WiresOutputOverlayShortcuts()
    {
        var code = ReadMainWindowCodeBehind();

        Assert.Contains("Key.F11", code, StringComparison.Ordinal);
        Assert.Contains("Key.Escape", code, StringComparison.Ordinal);
        Assert.Contains("ToggleOutputOverlayCommand", code, StringComparison.Ordinal);
        Assert.Contains("CloseOutputOverlayCommand", code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AutosaveToggle_DisabledSkipsViewModelAutosave()
    {
        var viewModel = CreateInitializedViewModel();
        viewModel.GraphAutosaveEnabled = false;
        viewModel.SelectedNode = viewModel.Nodes.First();
        viewModel.SelectedNodeUserComment = "autosave disabled";

        await viewModel.RunGraphAutosaveAsync();

        Assert.DoesNotContain(viewModel.Logs, log => log.Contains("Autosaved graph", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParameterSelector_UpdatesTypedBinding()
    {
        var viewModel = CreateInitializedViewModel();
        viewModel.SelectedNode = viewModel.Nodes.Single(node => node.Kind == NodeKind.Action);

        var message = viewModel.SelectedNodeParameters.Single(parameter => parameter.Key == "message");
        message.SourceKind = GraphValueSourceKind.LocalVariable;
        message.VariableName = "DebugText";

        var authored = viewModel.SelectedNode!.Parameters.Single(parameter => parameter.Key == "message");
        Assert.Equal(GraphValueSourceKind.LocalVariable, authored.Binding.SourceKind);
        Assert.Equal("DebugText", authored.Binding.VariableName);
        Assert.Equal("DebugText", authored.Value);
    }

    [Fact]
    public void SceneObjectSelector_UsesTypedSceneObjectBinding()
    {
        var viewModel = CreateInitializedViewModel();
        viewModel.SelectedNode = viewModel.Nodes.Single(node => node.Kind == NodeKind.Trigger);

        var target = viewModel.SelectedNodeParameters.Single(parameter => parameter.Key == "target");
        target.SourceKind = GraphValueSourceKind.SceneObject;
        target.SceneObjectPath = "World/Hidden";

        var authored = viewModel.SelectedNode!.Parameters.Single(parameter => parameter.Key == "target");
        Assert.Equal(GraphValueSourceKind.SceneObject, authored.Binding.SourceKind);
        Assert.Equal("World/Hidden", authored.Binding.SceneObjectPath);
        Assert.Equal("World/Hidden", authored.Value);
    }

    [Fact]
    public void GraphClipboardCommands_CopyPasteSelectionAndSelectNewNodes()
    {
        var viewModel = CreateInitializedViewModel();
        var originalNodeIds = viewModel.Nodes.Select(node => node.Id).ToList();
        var originalNodeCount = viewModel.Nodes.Count;

        viewModel.SelectGraphNodes(originalNodeIds, originalNodeIds[0]);
        viewModel.CopyGraphSelection();
        viewModel.PasteGraphClipboard(900, 500);

        Assert.True(viewModel.CanPasteGraphClipboard);
        Assert.Equal(originalNodeCount * 2, viewModel.Nodes.Count);
        Assert.Equal(originalNodeCount, viewModel.SelectedNodeIds.Count);
        Assert.All(viewModel.SelectedNodeIds, id => Assert.DoesNotContain(originalNodeIds, originalId => originalId == id));
        Assert.Contains("Pasted", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void GraphClipboardCommands_CutDeletesOnlyAfterCopyAndCanPaste()
    {
        var viewModel = CreateInitializedViewModel();
        var originalCount = viewModel.Nodes.Count;
        var cutNode = viewModel.Nodes.First();

        viewModel.SelectGraphNode(cutNode);
        viewModel.CutGraphSelection();

        Assert.True(viewModel.CanPasteGraphClipboard);
        Assert.Equal(originalCount - 1, viewModel.Nodes.Count);
        Assert.DoesNotContain(viewModel.Nodes, node => node.Id == cutNode.Id);
        Assert.Contains("Cut", viewModel.StatusText, StringComparison.Ordinal);

        viewModel.PasteGraphClipboard(300, 400);

        Assert.Equal(originalCount, viewModel.Nodes.Count);
        Assert.DoesNotContain(cutNode.Id, viewModel.SelectedNodeIds);
        Assert.Single(viewModel.SelectedNodeIds);
    }

    [Fact]
    public void GraphClipboardCommands_CopyWithoutSelectionShowsStatus()
    {
        var viewModel = CreateInitializedViewModel();

        viewModel.SelectGraphNode(null);
        viewModel.CopyGraphSelection();

        Assert.False(viewModel.CanPasteGraphClipboard);
        Assert.Contains("Select nodes or a group to copy.", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowCodeBehind_WiresGraphClipboardShortcutsWithoutTextInputHijack()
    {
        var code = ReadMainWindowCodeBehind();

        Assert.Contains("Key.C", code, StringComparison.Ordinal);
        Assert.Contains("Key.V", code, StringComparison.Ordinal);
        Assert.Contains("Key.X", code, StringComparison.Ordinal);
        Assert.Contains("Key.Delete", code, StringComparison.Ordinal);
        Assert.Contains("Key.Back", code, StringComparison.Ordinal);
        Assert.Contains("CopyGraphSelection", code, StringComparison.Ordinal);
        Assert.Contains("CutGraphSelection", code, StringComparison.Ordinal);
        Assert.Contains("PasteGraphClipboard", code, StringComparison.Ordinal);
        Assert.Contains("DeleteGraphSelection", code, StringComparison.Ordinal);
        Assert.Contains("IsTextEditingTarget", code, StringComparison.Ordinal);
        Assert.Contains("current is TextBox", code, StringComparison.Ordinal);
    }

    [Fact]
    public void RuleGraphCanvas_WiresGraphClipboardShortcutsAndContextPaste()
    {
        var keyboard = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "src", "Vrs.App", "Controls", "RuleGraphCanvas.Keyboard.cs"));
        var contextMenus = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "src", "Vrs.App", "Controls", "RuleGraphCanvas.ContextMenus.cs"));

        Assert.Contains("Key.C", keyboard, StringComparison.Ordinal);
        Assert.Contains("Key.V", keyboard, StringComparison.Ordinal);
        Assert.Contains("Key.X", keyboard, StringComparison.Ordinal);
        Assert.Contains("Key.Delete", keyboard, StringComparison.Ordinal);
        Assert.Contains("Key.Back", keyboard, StringComparison.Ordinal);
        Assert.Contains("CopyGraphSelection", keyboard, StringComparison.Ordinal);
        Assert.Contains("CutGraphSelection", keyboard, StringComparison.Ordinal);
        Assert.Contains("PasteGraphClipboard", keyboard, StringComparison.Ordinal);
        Assert.Contains("CanPasteGraphClipboard", contextMenus, StringComparison.Ordinal);
        Assert.DoesNotContain("paste.IsEnabled = false;", contextMenus, StringComparison.Ordinal);
    }

    private static MainWindowViewModel CreateInitializedViewModel(bool loadSample = true)
    {
        var viewModel = new MainWindowViewModel();
        viewModel.InitializeAsync().GetAwaiter().GetResult();
        if (loadSample)
        {
            viewModel.LoadTimerMessageSampleCommand.Execute(null);
        }

        return viewModel;
    }

    private static ProjectFileItemViewModel ProjectFileItem(string projectRoot, string relativePath, string? extension = null)
    {
        var fullPath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return new ProjectFileItemViewModel(new ProjectFileTreeEntry(
            Name: Path.GetFileName(fullPath),
            FullPath: fullPath,
            ProjectRoot: projectRoot,
            ProjectRelativePath: relativePath,
            Extension: extension ?? Path.GetExtension(fullPath),
            IsDirectory: false));
    }

    private static MainWindowViewModel CreateDeployableViewModel(string projectRoot)
    {
        var viewModel = CreateInitializedViewModel(loadSample: false);
        viewModel.ActiveProjectRoot = projectRoot;
        viewModel.HasLinkedProject = true;
        viewModel.IsCreatorRuntimeReady = true;
        viewModel.SetWindowFocusState(true);
        return viewModel;
    }

    private static void AddCatalogNode(MainWindowViewModel viewModel, string idBase)
    {
        viewModel.SelectedCatalogEntry = viewModel.CatalogEntries.Single(entry => entry.IdBase == idBase);
        viewModel.CanvasAddGraphX = 100;
        viewModel.CanvasAddGraphY = 120;
        viewModel.AddSelectedCatalogNodeAtCanvasPositionCommand.Execute(null);
    }

    private static string CreateTempPolytoriaProject(string prefix)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        var bridgeDirectory = Path.Combine(tempRoot, "addons", "visual-programming-bridge", "bridge");
        Directory.CreateDirectory(bridgeDirectory);
        File.WriteAllText(Path.Combine(tempRoot, "project.ptproj"), "{}");
        File.WriteAllText(
            Path.Combine(bridgeDirectory, "status.json"),
            $"{{\"state\":\"ready\",\"message\":\"test\",\"updatedAtUtc\":\"{DateTimeOffset.UtcNow:O}\"}}");
        return tempRoot;
    }

    private static string ReadMainWindowCodeBehind()
    {
        var viewsDirectory = Path.Combine(TestPaths.RepositoryRoot, "src", "Vrs.App", "Views");
        var files = Directory.GetFiles(viewsDirectory, "MainWindow*.cs").OrderBy(path => path, StringComparer.Ordinal);
        return string.Join(Environment.NewLine, files.Select(File.ReadAllText));
    }

    private static async Task CompleteNextUpsertCommandAsync(string projectRoot)
    {
        var command = await ReadLatestPendingUpsertAsync(projectRoot);
        var bridgeDirectory = Path.Combine(projectRoot, "addons", "visual-programming-bridge", "bridge");
        var resultsPath = Path.Combine(bridgeDirectory, "command-results.json");
        await File.WriteAllTextAsync(
            resultsPath,
            UpsertCommandResultsJson(command.Id, command.TargetPath, command.LinkedFilePath, command.Kind));
        File.SetLastWriteTimeUtc(resultsPath, DateTime.UtcNow.AddSeconds(2));
    }

    private static async Task<BridgeCommand> ReadLatestPendingUpsertAsync(string projectRoot)
    {
        var pendingPath = Path.Combine(projectRoot, "addons", "visual-programming-bridge", "bridge", "pending-commands.json");
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (File.Exists(pendingPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(pendingPath);
                    var envelope = JsonSerializer.Deserialize(json, VrsJsonContext.Default.BridgeCommandEnvelope);
                    var command = envelope?.Commands.LastOrDefault(item => item.Type == "upsert_script");
                    if (command is not null)
                    {
                        return command;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    // The app and bridge tests replace pending-commands.json atomically.
                    // A short retry keeps this helper focused on eventual command content
                    // instead of a transient Windows file-sharing race.
                }
            }

            await Task.Delay(25);
        }

        throw new InvalidOperationException("Timed out waiting for a pending upsert_script command.");
    }

    private static string UpsertCommandResultsJson(string commandId, string targetPath, string linkedFilePath, string scriptKind)
    {
        return $$"""
        {
          "version": 1,
          "results": [
            {
              "ok": true,
              "path": "{{targetPath}}",
              "index": 1,
              "id": "{{commandId}}",
              "message": "upsert_script created {{scriptKind}} linked to {{linkedFilePath}}",
              "action": "upsert_script",
              "skipped": false,
              "details": {
                "created": true,
                "linkedFilePath": "{{linkedFilePath}}",
                "scriptKind": "{{scriptKind}}",
                "targetPath": "{{targetPath}}"
              },
              "handledAtUtc": "2026-06-20T12:00:01Z"
            }
          ],
          "createdAtUtc": "2026-06-20T12:00:01Z",
          "format": "visual-programming-bridge-command-results",
          "createdBy": "[VRS]Visual Programming Bridge",
          "runtimeVersion": "0.8.15"
        }
        """;
    }
}
