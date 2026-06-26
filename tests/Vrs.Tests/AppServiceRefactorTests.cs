using System.Text.Json;
using Vrs.App.Services;
using Vrs.App.ViewModels;
using Vrs.Core.Bridge;
using Vrs.Core.Catalog;
using Vrs.Core.Persistence;
using Vrs.Core.ProjectInputs;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.Tests;

public sealed class AppServiceRefactorTests
{
    [Fact]
    public void ProjectRuntimeStatus_DetectsInvalidProjectAndMissingBridgeStatus()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-project-status-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var now = DateTimeOffset.Parse("2026-06-20T12:00:00Z");
            var service = new ProjectRuntimeStatusService(_ => null, () => now);

            Assert.False(service.IsValidProjectRoot(tempRoot));

            File.WriteAllText(Path.Combine(tempRoot, "project.ptproj"), "{}");
            var bridgeDirectory = Path.Combine(tempRoot, "addons", "visual-programming-bridge", "bridge");
            var status = service.BuildLinkedProjectStatus(tempRoot, bridgeDirectory);

            Assert.True(service.IsValidProjectRoot(tempRoot));
            Assert.True(status.HasLinkedProject);
            Assert.False(status.IsCreatorRuntimeReady);
            Assert.False(status.HasActiveProject);
            Assert.Contains("Not Running", status.ProjectStatusText, StringComparison.Ordinal);
            Assert.Contains("Bridge status missing", status.ProjectStatusDetail, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ProjectRuntimeStatus_UsesFreshBridgeStatusAndCreatorWindowFallback()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-project-status-{Guid.NewGuid():N}");
        var bridgeDirectory = Path.Combine(tempRoot, "addons", "visual-programming-bridge", "bridge");
        Directory.CreateDirectory(bridgeDirectory);
        File.WriteAllText(Path.Combine(tempRoot, "project.ptproj"), "{}");

        try
        {
            var now = DateTimeOffset.Parse("2026-06-20T12:00:00Z");
            var freshService = new ProjectRuntimeStatusService(_ => null, () => now);
            File.WriteAllText(
                Path.Combine(bridgeDirectory, "status.json"),
                """{"state":"ready","message":"watching","updatedAtUtc":"2026-06-20T11:59:30Z"}""");

            var fresh = freshService.BuildLinkedProjectStatus(tempRoot, bridgeDirectory);

            Assert.True(fresh.HasActiveProject);
            Assert.True(fresh.HasLinkedProject);
            Assert.True(fresh.IsCreatorRuntimeReady);
            Assert.Contains("Bridge status: ready", fresh.ProjectStatusDetail, StringComparison.Ordinal);

            File.WriteAllText(
                Path.Combine(bridgeDirectory, "status.json"),
                """{"state":"ready","message":"old","updatedAtUtc":"2026-06-20T11:55:00Z"}""");
            var stale = freshService.BuildLinkedProjectStatus(tempRoot, bridgeDirectory);

            Assert.False(stale.HasActiveProject);
            Assert.True(stale.HasLinkedProject);
            Assert.False(stale.IsCreatorRuntimeReady);

            var creatorService = new ProjectRuntimeStatusService(_ => "Polytoria Creator - SuperTest", () => now);
            var creatorRunning = creatorService.BuildLinkedProjectStatus(tempRoot, bridgeDirectory);

            Assert.True(creatorRunning.HasActiveProject);
            Assert.True(creatorRunning.HasLinkedProject);
            Assert.True(creatorRunning.IsCreatorRuntimeReady);
            Assert.Contains("Creator window found", creatorRunning.ProjectStatusDetail, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task BridgeSync_WritesHeartbeatAndDetectsChangedFilesOnce()
    {
        var bridgeDirectory = Path.Combine(Path.GetTempPath(), $"vrs-bridge-sync-{Guid.NewGuid():N}");
        Directory.CreateDirectory(bridgeDirectory);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(bridgeDirectory, "scene-snapshot.json"), MinimalSceneSnapshotJson);
            await File.WriteAllTextAsync(Path.Combine(bridgeDirectory, "command-results.json"), UpsertCommandResultsJson("upsert_script_1"));
            await File.WriteAllTextAsync(
                Path.Combine(bridgeDirectory, "status.json"),
                "{\"state\":\"ready\",\"message\":\"watching\",\"updatedAtUtc\":\"2026-06-20T11:59:58Z\"}");
            var service = new BridgeSyncService(new BridgeFileService(), () => DateTimeOffset.Parse("2026-06-20T12:00:00Z"));

            var first = await service.SyncAsync(bridgeDirectory, new SceneSnapshotReadOptions(), isVrsFocused: false);

            Assert.True(first.HeartbeatWritten);
            Assert.False(first.VrsFocused);
            Assert.NotNull(first.HeartbeatWrittenAtUtc);
            Assert.Equal(TimeSpan.FromSeconds(10), first.HeartbeatTimeToLive);
            Assert.True(first.AddonStatus.Exists);
            Assert.True(first.AddonStatus.IsReadable);
            Assert.True(first.AddonStatus.IsFresh);
            Assert.Equal("ready", first.AddonStatus.State);
            Assert.Equal("watching", first.AddonStatus.Message);
            Assert.True(first.AddonStatus.Age.HasValue);
            Assert.Equal(TimeSpan.FromSeconds(2), first.AddonStatus.Age.Value);
            Assert.True(first.SnapshotChanged);
            Assert.True(first.CommandResultsChanged);
            Assert.Equal(2, first.Snapshot!.Objects.Count);
            Assert.Equal("upsert_script_1", first.LatestCommandResult!.Id);
            Assert.Equal("scripts/VRS/server/VRS TimerMessage.server.luau", first.LatestCommandResult.Details.LinkedFilePath);

            var heartbeatJson = await File.ReadAllTextAsync(Path.Combine(bridgeDirectory, "app-heartbeat.json"));
            var heartbeat = JsonSerializer.Deserialize(heartbeatJson, VrsJsonContext.Default.AppHeartbeat);

            Assert.NotNull(heartbeat);
            Assert.True(heartbeat!.Active);
            Assert.False(heartbeat.Focused);
            Assert.True(heartbeat.ExpiresAtUnixSeconds > heartbeat.UpdatedAtUnixSeconds);

            var second = await service.SyncAsync(bridgeDirectory, new SceneSnapshotReadOptions(), isVrsFocused: true);

            Assert.True(second.HeartbeatWritten);
            Assert.True(second.VrsFocused);
            Assert.False(second.SnapshotChanged);
            Assert.False(second.CommandResultsChanged);

            var secondHeartbeatJson = await File.ReadAllTextAsync(Path.Combine(bridgeDirectory, "app-heartbeat.json"));
            var secondHeartbeat = JsonSerializer.Deserialize(secondHeartbeatJson, VrsJsonContext.Default.AppHeartbeat);

            Assert.NotNull(secondHeartbeat);
            Assert.True(secondHeartbeat!.Focused);
        }
        finally
        {
            Directory.Delete(bridgeDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task BridgeSync_MissingBridgeFilesFailSoft()
    {
        var bridgeDirectory = Path.Combine(Path.GetTempPath(), $"vrs-bridge-sync-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(bridgeDirectory);

        try
        {
            var service = new BridgeSyncService(new BridgeFileService());

            var result = await service.SyncAsync(bridgeDirectory, new SceneSnapshotReadOptions());

            Assert.True(result.HeartbeatWritten);
            Assert.False(result.SnapshotChanged);
            Assert.False(result.CommandResultsChanged);
            Assert.Null(result.Snapshot);
            Assert.Null(result.LatestCommandResult);
        }
        finally
        {
            Directory.Delete(bridgeDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task BridgeSync_ReportsAddonStatusHealth()
    {
        var bridgeDirectory = Path.Combine(Path.GetTempPath(), $"vrs-bridge-status-health-{Guid.NewGuid():N}");
        Directory.CreateDirectory(bridgeDirectory);

        try
        {
            var service = new BridgeSyncService(new BridgeFileService(), () => DateTimeOffset.Parse("2026-06-20T12:00:00Z"));

            var missing = await service.SyncAsync(bridgeDirectory, new SceneSnapshotReadOptions(), isVrsFocused: false);

            Assert.True(missing.HeartbeatWritten);
            Assert.False(missing.AddonStatus.Exists);
            Assert.False(missing.AddonStatus.IsFresh);
            Assert.Equal("missing", missing.AddonStatus.State);
            Assert.Contains("status missing", missing.AddonStatus.Error, StringComparison.Ordinal);

            await File.WriteAllTextAsync(
                Path.Combine(bridgeDirectory, "status.json"),
                "{\"state\":\"ready\",\"message\":\"idle\",\"updatedAtUtc\":\"2026-06-20T11:57:00Z\"}");

            var stale = await service.SyncAsync(bridgeDirectory, new SceneSnapshotReadOptions(), isVrsFocused: true);

            Assert.True(stale.VrsFocused);
            Assert.True(stale.AddonStatus.Exists);
            Assert.True(stale.AddonStatus.IsReadable);
            Assert.False(stale.AddonStatus.IsFresh);
            Assert.Equal("ready", stale.AddonStatus.State);
            Assert.Equal("idle", stale.AddonStatus.Message);
            Assert.True(stale.AddonStatus.Age.HasValue);
            Assert.Equal(TimeSpan.FromMinutes(3), stale.AddonStatus.Age.Value);
        }
        finally
        {
            Directory.Delete(bridgeDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task BridgeSync_DetectsSceneSnapshotContentChangeWhenFileStampIsStable()
    {
        var bridgeDirectory = Path.Combine(Path.GetTempPath(), $"vrs-bridge-stable-stamp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(bridgeDirectory);

        try
        {
            var snapshotPath = Path.Combine(bridgeDirectory, "scene-snapshot.json");
            var stamp = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);
            var firstSnapshot = SingleChildSceneSnapshotJson("Alpha");
            var secondSnapshot = SingleChildSceneSnapshotJson("Bravo");
            Assert.Equal(firstSnapshot.Length, secondSnapshot.Length);

            await File.WriteAllTextAsync(snapshotPath, firstSnapshot);
            File.SetLastWriteTimeUtc(snapshotPath, stamp);

            var service = new BridgeSyncService(new BridgeFileService());
            var first = await service.SyncAsync(bridgeDirectory, new SceneSnapshotReadOptions());

            Assert.True(first.SnapshotChanged);
            Assert.Contains(first.Snapshot!.Objects, item => item.Path == "World/Alpha");

            await File.WriteAllTextAsync(snapshotPath, secondSnapshot);
            File.SetLastWriteTimeUtc(snapshotPath, stamp);

            var second = await service.SyncAsync(bridgeDirectory, new SceneSnapshotReadOptions());

            Assert.True(second.SnapshotChanged);
            Assert.Contains(second.Snapshot!.Objects, item => item.Path == "World/Bravo");
            Assert.DoesNotContain(second.Snapshot.Objects, item => item.Path == "World/Alpha");
        }
        finally
        {
            Directory.Delete(bridgeDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task BridgeFileService_PreservesCreatorSiblingOrderFromSnapshot()
    {
        var bridgeDirectory = Path.Combine(Path.GetTempPath(), $"vrs-bridge-order-{Guid.NewGuid():N}");
        Directory.CreateDirectory(bridgeDirectory);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(bridgeDirectory, "scene-snapshot.json"), OrderedEnvironmentSceneSnapshotJson);

            var service = new BridgeFileService();
            var result = await service.ReadSceneSnapshotObjectsAsync(
                bridgeDirectory,
                new SceneSnapshotReadOptions
                {
                    MaxObjects = 100,
                    MaxDepth = 5
                });

            Assert.NotNull(result);
            Assert.Equal(
                new[]
                {
                    "Part3",
                    "Part4",
                    "Part5",
                    "Part6",
                    "Part7",
                    "Part8",
                    "VisualFolder",
                    "Coin",
                    "Trigger Part",
                    "Part9",
                    "Part10",
                    "Model",
                    "Kill Brick"
                },
                result!.Objects
                    .Where(item => IsDirectEnvironmentChild(item.Path))
                    .Select(item => item.Name));
        }
        finally
        {
            Directory.Delete(bridgeDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ViewModelBridgeSync_RefreshesSceneTreeWithoutReloadingGraph()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), $"vrs-viewmodel-sync-{Guid.NewGuid():N}");
        var bridgeDirectory = Path.Combine(projectRoot, "addons", "visual-programming-bridge", "bridge");
        Directory.CreateDirectory(bridgeDirectory);
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "project.ptproj"), "{}");
        await File.WriteAllTextAsync(Path.Combine(bridgeDirectory, "scene-snapshot.json"), LinkedScriptSceneSnapshotJson);
        await File.WriteAllTextAsync(
            Path.Combine(bridgeDirectory, "status.json"),
            $"{{\"state\":\"ready\",\"message\":\"watching\",\"updatedAtUtc\":\"{DateTimeOffset.UtcNow:O}\"}}");

        try
        {
            var viewModel = new MainWindowViewModel
            {
                ActiveProjectRoot = projectRoot,
                BridgeParentPath = "World/Environment/CodexBeacon",
                BridgeScriptName = "TimerMessage"
            };
            viewModel.SetWindowFocusState(false);

            while (viewModel.Nodes.Count > 0)
            {
                viewModel.SelectedNode = viewModel.Nodes[0];
                viewModel.DeleteSelectionCommand.Execute(null);
            }

            await viewModel.SynchronizeBridgeOnceAsync();

            Assert.Empty(viewModel.Nodes);
            Assert.Equal("File: scripts/VRS/server/VRS TimerMessage.server.luau", viewModel.ScriptFilePreviewText);
            Assert.Contains(viewModel.SceneObjects, sceneObject =>
                sceneObject.Path == "World/Environment/CodexBeacon/VRS TimerMessage" &&
                sceneObject.IsLinkedScript);
            Assert.Contains("Synced Creator snapshot", viewModel.StatusText, StringComparison.Ordinal);
            Assert.Contains("Addon beat", viewModel.BridgeBeatText, StringComparison.Ordinal);
            Assert.Contains("Live paused", viewModel.BridgeBeatText, StringComparison.Ordinal);
            Assert.Contains("Live mutation requires VRS focus", viewModel.BridgeBeatDetail, StringComparison.Ordinal);

            var heartbeatJson = await File.ReadAllTextAsync(Path.Combine(bridgeDirectory, "app-heartbeat.json"));
            var heartbeat = JsonSerializer.Deserialize(heartbeatJson, VrsJsonContext.Default.AppHeartbeat);
            Assert.NotNull(heartbeat);
            Assert.False(heartbeat!.Focused);

            viewModel.SetWindowFocusState(true);
            Assert.Contains("VRS focus", viewModel.BridgeBeatText, StringComparison.Ordinal);

            var commandResultsPath = Path.Combine(bridgeDirectory, "command-results.json");
            await File.WriteAllTextAsync(commandResultsPath, UpsertCommandResultsJson("upsert_script_2"));
            File.SetLastWriteTimeUtc(commandResultsPath, DateTime.UtcNow.AddSeconds(2));

            await viewModel.SynchronizeBridgeOnceAsync();

            Assert.Contains("Creator applied", viewModel.StatusText, StringComparison.Ordinal);
            Assert.Contains("World/Environment/CodexBeacon/VRS TimerMessage", viewModel.StatusText, StringComparison.Ordinal);
            Assert.Empty(viewModel.Nodes);
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ViewModelBridgeSync_RebuildsHierarchyWhenSnapshotContentChangesWithStableFileStamp()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), $"vrs-viewmodel-stable-stamp-{Guid.NewGuid():N}");
        var bridgeDirectory = Path.Combine(projectRoot, "addons", "visual-programming-bridge", "bridge");
        Directory.CreateDirectory(bridgeDirectory);
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "project.ptproj"), "{}");

        try
        {
            var snapshotPath = Path.Combine(bridgeDirectory, "scene-snapshot.json");
            var stamp = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);
            var firstSnapshot = SingleChildSceneSnapshotJson("Alpha");
            var secondSnapshot = SingleChildSceneSnapshotJson("Bravo");
            Assert.Equal(firstSnapshot.Length, secondSnapshot.Length);

            await File.WriteAllTextAsync(snapshotPath, firstSnapshot);
            File.SetLastWriteTimeUtc(snapshotPath, stamp);

            var viewModel = new MainWindowViewModel
            {
                ActiveProjectRoot = projectRoot,
                SnapshotMaxDepth = 8,
                SnapshotMaxObjects = 100
            };

            await viewModel.SynchronizeBridgeOnceAsync();

            Assert.NotNull(FindSceneItemOrNull(viewModel.SceneTreeRoots, "World/Alpha"));
            Assert.Null(FindSceneItemOrNull(viewModel.SceneTreeRoots, "World/Bravo"));

            await File.WriteAllTextAsync(snapshotPath, secondSnapshot);
            File.SetLastWriteTimeUtc(snapshotPath, stamp);

            await viewModel.SynchronizeBridgeOnceAsync();

            Assert.Null(FindSceneItemOrNull(viewModel.SceneTreeRoots, "World/Alpha"));
            Assert.NotNull(FindSceneItemOrNull(viewModel.SceneTreeRoots, "World/Bravo"));
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ViewModelSceneFilter_DoesNotPersistSearchExpandedAncestors()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), $"vrs-viewmodel-filter-{Guid.NewGuid():N}");
        var bridgeDirectory = Path.Combine(projectRoot, "addons", "visual-programming-bridge", "bridge");
        Directory.CreateDirectory(bridgeDirectory);
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "project.ptproj"), "{}");
        await File.WriteAllTextAsync(Path.Combine(bridgeDirectory, "scene-snapshot.json"), LinkedScriptSceneSnapshotJson);

        try
        {
            var viewModel = new MainWindowViewModel
            {
                ActiveProjectRoot = projectRoot,
                SnapshotMaxDepth = 8,
                SnapshotMaxObjects = 100
            };

            await viewModel.SynchronizeBridgeOnceAsync();

            Assert.True(FindSceneItem(viewModel.SceneTreeRoots, "World").IsExpanded);
            Assert.False(FindSceneItem(viewModel.SceneTreeRoots, "World/Environment").IsExpanded);

            viewModel.SceneFilter = "TimerMessage";

            Assert.True(FindSceneItem(viewModel.SceneTreeRoots, "World/Environment").IsExpanded);
            Assert.True(FindSceneItem(viewModel.SceneTreeRoots, "World/Environment/CodexBeacon").IsExpanded);

            viewModel.SceneFilter = "";

            Assert.True(FindSceneItem(viewModel.SceneTreeRoots, "World").IsExpanded);
            Assert.False(FindSceneItem(viewModel.SceneTreeRoots, "World/Environment").IsExpanded);
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void SceneTreeBuilder_FiltersAndPreservesCreatorOrderWithoutViewModels()
    {
        var service = new SceneTreeBuilderService();
        var sceneObjects = new[]
        {
            Scene("World/PlayerGUI/MainHud", "MainHud", "ScreenGui"),
            Scene("World/Hidden/VRS_Demo", "VRS_Demo", "Folder", "scripts/VRS/server/TimerMessage.server.luau"),
            Scene("World/Hidden", "Hidden", "Folder"),
            Scene("World/Environment", "Environment", "Folder"),
            Scene("World/PlayerGUI", "PlayerGUI", "Folder"),
            Scene("World", "World", "World"),
            Scene("World/Hidden/VRS_Demo", "DuplicateIgnored", "Folder")
        };

        var unfiltered = service.Build(sceneObjects, "", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "World/Hidden" }, hasPreviousExpansionState: true);
        var world = Assert.Single(unfiltered.Roots);

        Assert.Equal("World", world.SceneObject.Name);
        Assert.True(world.IsExpanded);
        Assert.Equal(new[] { "Hidden", "Environment", "PlayerGUI" }, world.Children.Select(child => child.SceneObject.Name));
        Assert.True(world.Children.Single(child => child.SceneObject.Path == "World/Hidden").IsExpanded);

        var filtered = service.Build(sceneObjects, "TimerMessage.server", new HashSet<string>(StringComparer.OrdinalIgnoreCase), hasPreviousExpansionState: false);
        var filteredWorld = Assert.Single(filtered.Roots);
        var filteredHidden = Assert.Single(filteredWorld.Children);

        Assert.Equal("Hidden", filteredHidden.SceneObject.Name);
        Assert.Equal("VRS_Demo", Assert.Single(filteredHidden.Children).SceneObject.Name);
        Assert.True(filteredWorld.IsExpanded);
        Assert.True(filteredHidden.IsExpanded);
    }

    [Fact]
    public void SceneTreeBuilder_PreservesCreatorSiblingOrder()
    {
        var service = new SceneTreeBuilderService();
        var sceneObjects = new[]
        {
            Scene("World/Environment/Part3", "Part3", "Part"),
            Scene("World/Environment/Part4", "Part4", "Part"),
            Scene("World/Environment/Part5", "Part5", "Part"),
            Scene("World/Environment/Part6", "Part6", "Part"),
            Scene("World/Environment/Part7", "Part7", "Part"),
            Scene("World/Environment/Part8", "Part8", "Part"),
            Scene("World/Environment/VisualFolder", "VisualFolder", "Folder"),
            Scene("World/Environment/Coin", "Coin", "Part"),
            Scene("World/Environment/Trigger Part", "Trigger Part", "Part"),
            Scene("World/Environment/Part9", "Part9", "Part"),
            Scene("World/Environment/Part10", "Part10", "Part"),
            Scene("World/Environment/Model", "Model", "Model"),
            Scene("World/Environment/Kill Brick", "Kill Brick", "Part"),
            Scene("World/Environment", "Environment", "Environment"),
            Scene("World", "World", "World")
        };

        var result = service.Build(sceneObjects, "", new HashSet<string>(StringComparer.OrdinalIgnoreCase), hasPreviousExpansionState: false);
        var world = Assert.Single(result.Roots);
        var environment = Assert.Single(world.Children);

        Assert.Equal(
            new[]
            {
                "Part3",
                "Part4",
                "Part5",
                "Part6",
                "Part7",
                "Part8",
                "VisualFolder",
                "Coin",
                "Trigger Part",
                "Part9",
                "Part10",
                "Model",
                "Kill Brick"
            },
            environment.Children.Select(child => child.SceneObject.Name));
    }

    [Fact]
    public void SceneTreeBuilder_KeepsVisualScriptParentsCollapsedByDefault()
    {
        var service = new SceneTreeBuilderService();
        var sceneObjects = new[]
        {
            Scene("World", "World", "World"),
            Scene("World/Environment", "Environment", "Environment"),
            Scene("World/Environment/Baseplate", "Baseplate", "Part"),
            Scene("World/Environment/CodexBeacon", "CodexBeacon", "Part"),
            Scene("World/Environment/CodexBeacon/VRS TimerMessage", "VRS TimerMessage", "ServerScript", "scripts/VRS/server/VRS TimerMessage.server.luau")
        };

        var result = service.Build(sceneObjects, "", new HashSet<string>(StringComparer.OrdinalIgnoreCase), hasPreviousExpansionState: false);
        var world = Assert.Single(result.Roots);
        var environment = Assert.Single(world.Children);
        var codexBeacon = environment.Children.First(child => child.SceneObject.Name == "CodexBeacon");

        Assert.True(world.IsExpanded);
        Assert.False(environment.IsExpanded);
        Assert.False(codexBeacon.IsExpanded);
        Assert.Equal("VRS TimerMessage", Assert.Single(codexBeacon.Children).SceneObject.Name);
    }

    [Fact]
    public void SceneTreeBuilder_PreservesManualExpansionAndUsesTemporaryFilterExpansion()
    {
        var service = new SceneTreeBuilderService();
        var sceneObjects = new[]
        {
            Scene("World", "World", "World"),
            Scene("World/Environment", "Environment", "Environment"),
            Scene("World/Environment/CodexBeacon", "CodexBeacon", "Part"),
            Scene("World/Environment/CodexBeacon/VRS TimerMessage", "VRS TimerMessage", "ServerScript", "scripts/VRS/server/VRS TimerMessage.server.luau")
        };

        var manual = service.Build(sceneObjects, "", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "World/Environment" }, hasPreviousExpansionState: true);
        var manualWorld = Assert.Single(manual.Roots);
        var manualEnvironment = Assert.Single(manualWorld.Children);
        var manualCodexBeacon = Assert.Single(manualEnvironment.Children);

        Assert.True(manualWorld.IsExpanded);
        Assert.True(manualEnvironment.IsExpanded);
        Assert.False(manualCodexBeacon.IsExpanded);

        var filtered = service.Build(sceneObjects, "TimerMessage", new HashSet<string>(StringComparer.OrdinalIgnoreCase), hasPreviousExpansionState: false);
        var filteredWorld = Assert.Single(filtered.Roots);
        var filteredEnvironment = Assert.Single(filteredWorld.Children);
        var filteredCodexBeacon = Assert.Single(filteredEnvironment.Children);

        Assert.True(filteredWorld.IsExpanded);
        Assert.True(filteredEnvironment.IsExpanded);
        Assert.True(filteredCodexBeacon.IsExpanded);

        var cleared = service.Build(sceneObjects, "", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "World" }, hasPreviousExpansionState: true);
        var clearedWorld = Assert.Single(cleared.Roots);
        var clearedEnvironment = Assert.Single(clearedWorld.Children);

        Assert.True(clearedWorld.IsExpanded);
        Assert.False(clearedEnvironment.IsExpanded);
    }

    [Fact]
    public void GraphRefreshService_KeepsEmptyRuleNonExportableAndPreviewEmpty()
    {
        var service = new GraphRefreshService();
        var graph = new RuleGraph
        {
            Rules =
            [
                new Rule
                {
                    Id = "RULE_Empty",
                    Name = "Empty"
                }
            ]
        };

        Assert.False(service.HasExportableRule(graph));
        Assert.False(service.TryGetExportableRule(graph, out _));

        var preview = service.BuildLuauPreview(graph, [], previousPreview: "-- old preview", trackVisualDiff: true);

        Assert.Equal("", preview.PreviewText);
    }

    [Fact]
    public void GraphRefreshService_NormalizesHumanFlowPortsAndRemovesInvalidFlowConnections()
    {
        var service = new GraphRefreshService();
        var trigger = new RuleNode
        {
            Id = "TRIG_Start",
            Kind = NodeKind.Trigger,
            Ports =
            [
                new NodePort
                {
                    Id = "legacy_out",
                    Label = "Legacy",
                    Direction = NodePortDirection.Output,
                    PortKind = NodePortKind.Flow,
                    DataType = "Flow"
                }
            ]
        };
        var action = new RuleNode
        {
            Id = "ACT_Log",
            Kind = NodeKind.Action,
            Ports = GraphPortDefaults.CreateDefaultPorts(NodeKind.Action)
        };
        var graph = new RuleGraph
        {
            Rules =
            [
                new Rule
                {
                    Id = "RULE_Normalize",
                    Name = "Normalize",
                    Nodes = [trigger, action],
                    Connections =
                    [
                        new GraphConnection
                        {
                            Id = "CONN_Invalid",
                            ConnectionKind = GraphConnectionKind.Value,
                            From = new GraphEndpoint { NodeId = trigger.Id, PortId = "legacy_out" },
                            To = new GraphEndpoint { NodeId = action.Id, PortId = GraphPortDefaults.FlowIn }
                        }
                    ]
                }
            ]
        };

        var changed = service.NormalizeHumanFlowPorts(graph);

        Assert.True(changed);
        Assert.Equal(GraphPortDefaults.CreateDefaultPorts(NodeKind.Trigger).Select(port => port.Id), trigger.Ports.Select(port => port.Id));
        Assert.Empty(graph.Rules.Single().Connections);
    }

    [Fact]
    public void GraphViewportService_FitEmptyGraphResetsView()
    {
        var service = new GraphViewportService();

        var state = service.Fit([], viewportWidth: 900, viewportHeight: 520);

        Assert.Equal(1.0, state.Zoom);
        Assert.Equal(0.0, state.PanX);
        Assert.Equal(0.0, state.PanY);
        Assert.Equal("Reset empty canvas view.", state.StatusText);
    }

    [Fact]
    public void GraphViewportService_FitGraphUsesBoundedZoomAndCentersNodes()
    {
        var service = new GraphViewportService();
        var nodes = new[]
        {
            new RuleNode { Kind = NodeKind.Trigger, GraphX = 100, GraphY = 80 },
            new RuleNode { Kind = NodeKind.Action, GraphX = 500, GraphY = 260 }
        };
        var viewportWidth = 900.0;
        var viewportHeight = 520.0;
        var graphWidth = 500.0 + RuleGraphGeometryService.NodeWidth - 100.0;
        var graphHeight = 260.0 + RuleGraphGeometryService.NodeHeightFor(nodes[1]) - 80.0;
        var expectedZoom = Math.Clamp(Math.Min((viewportWidth - 160.0) / graphWidth, (viewportHeight - 160.0) / graphHeight), 0.35, 1.6);
        var expectedPanX = ((viewportWidth - (graphWidth * expectedZoom)) / 2.0) - (100.0 * expectedZoom);
        var expectedPanY = ((viewportHeight - (graphHeight * expectedZoom)) / 2.0) - (80.0 * expectedZoom);

        var state = service.Fit(nodes, viewportWidth, viewportHeight);

        Assert.InRange(state.Zoom, 0.35, 1.6);
        AssertClose(expectedZoom, state.Zoom);
        AssertClose(expectedPanX, state.PanX);
        AssertClose(expectedPanY, state.PanY);
        Assert.Equal("Framed graph in canvas.", state.StatusText);
    }

    [Fact]
    public void GraphViewportService_FitGraphIncludesNodeGroups()
    {
        var service = new GraphViewportService();
        var nodes = new[]
        {
            new RuleNode { Kind = NodeKind.Trigger, GraphX = 100, GraphY = 100 }
        };
        var groups = new[]
        {
            new RuleNodeGroup { GraphX = 40, GraphY = 30, Width = 620, Height = 360 }
        };

        var state = service.Fit(nodes, viewportWidth: 900, viewportHeight: 520, groups);

        Assert.Equal("Framed graph in canvas.", state.StatusText);
        Assert.True(state.PanX > -100);
        Assert.True(state.PanY > -100);
    }

    [Fact]
    public void GraphViewportService_FitGraphIncludesWireReroutes()
    {
        var service = new GraphViewportService();
        var nodes = new[]
        {
            new RuleNode { Kind = NodeKind.Trigger, GraphX = 100, GraphY = 100 }
        };
        var reroutes = new[]
        {
            new RuleWireReroute { Id = "REROUTE_Far", GraphX = 900, GraphY = 420 }
        };

        var nodeOnly = service.Fit(nodes, viewportWidth: 900, viewportHeight: 520);
        var state = service.Fit(nodes, viewportWidth: 900, viewportHeight: 520, reroutes: reroutes);

        Assert.Equal("Framed graph in canvas.", state.StatusText);
        Assert.True(state.Zoom < nodeOnly.Zoom);
    }

    [Fact]
    public void RuleGraphGeometryService_BuildsSegmentedConnectionThroughReroutes()
    {
        var geometry = new RuleGraphGeometryService();
        var trigger = new RuleNode
        {
            Id = "TRG_Start",
            Kind = NodeKind.Trigger,
            GraphX = 100,
            GraphY = 100,
            Ports = GraphPortDefaults.CreateDefaultPorts(NodeKind.Trigger)
        };
        var action = new RuleNode
        {
            Id = "ACT_Show",
            Kind = NodeKind.Action,
            GraphX = 500,
            GraphY = 100,
            Ports = GraphPortDefaults.CreateDefaultPorts(NodeKind.Action)
        };
        var connection = new GraphConnection
        {
            From = new GraphEndpoint { NodeId = trigger.Id, PortId = GraphPortDefaults.FlowOut },
            To = new GraphEndpoint { NodeId = action.Id, PortId = GraphPortDefaults.FlowIn },
            RerouteIds = ["REROUTE_A"]
        };
        var reroute = new RuleWireReroute
        {
            Id = "REROUTE_A",
            GraphX = 340,
            GraphY = 180,
            InputDirection = WireRerouteDirection.Up,
            OutputDirection = WireRerouteDirection.Down
        };

        var ok = geometry.TryGetConnectionPathSegments(
            connection,
            new Dictionary<string, RuleNode>(StringComparer.OrdinalIgnoreCase) { [trigger.Id] = trigger, [action.Id] = action },
            new Dictionary<string, RuleWireReroute>(StringComparer.OrdinalIgnoreCase) { [reroute.Id] = reroute },
            out var segments);

        Assert.True(ok);
        Assert.Equal(2, segments.Count);
        Assert.Equal(WireRerouteDirection.Up, segments[0].ToDirection);
        Assert.Equal(WireRerouteDirection.Down, segments[1].FromDirection);
        Assert.NotNull(geometry.HitTestConnection([connection], new Dictionary<string, RuleNode>(StringComparer.OrdinalIgnoreCase) { [trigger.Id] = trigger, [action.Id] = action }, new Dictionary<string, RuleWireReroute>(StringComparer.OrdinalIgnoreCase) { [reroute.Id] = reroute }, new GraphPoint(340, 180)));
    }

    [Fact]
    public void GraphViewportService_ZoomInAndOutRespectBounds()
    {
        var service = new GraphViewportService();

        var maxed = service.ZoomIn(currentZoom: 10.0, currentPanX: 42.0, currentPanY: -8.0);
        var mined = service.ZoomOut(currentZoom: 0.01, currentPanX: 42.0, currentPanY: -8.0);

        Assert.Equal(2.5, maxed.Zoom);
        Assert.Equal(0.25, mined.Zoom);
        Assert.Equal(42.0, maxed.PanX);
        Assert.Equal(-8.0, maxed.PanY);
        Assert.Equal(42.0, mined.PanX);
        Assert.Equal(-8.0, mined.PanY);
    }

    [Fact]
    public void SelectionInspectorService_BuildsNodePresentationAndInspectorSummaries()
    {
        var service = new SelectionInspectorService();
        var action = new RuleNode
        {
            Id = "ACT_Show",
            CatalogId = "ACT_ShowMessage",
            Kind = NodeKind.Action,
            Type = "ShowMessage",
            Label = "Show Message",
            Description = "Prints a message.",
            UserComment = "debug note",
            Parameters =
            [
                new RuleParameter
                {
                    Key = "message",
                    Value = "Hello",
                    Binding = new GraphValueBinding
                    {
                        SourceKind = GraphValueSourceKind.Constant,
                        ConstantValue = "Hello",
                        DisplayText = "Constant: Hello"
                    }
                }
            ]
        };
        var rule = new Rule
        {
            Nodes = [action],
            Connections =
            [
                new GraphConnection
                {
                    From = new GraphEndpoint { NodeId = "TRIG_Start", PortId = GraphPortDefaults.FlowOut },
                    To = new GraphEndpoint { NodeId = action.Id, PortId = GraphPortDefaults.FlowIn }
                }
            ],
            Fragments =
            [
                new GraphFragment
                {
                    Id = "FRAG_Debug",
                    Name = "Debug Flow",
                    Kind = GraphFragmentKind.State,
                    NodeIds = [action.Id],
                    Comment = "group note"
                }
            ],
            NodeGroups =
            [
                new RuleNodeGroup
                {
                    Id = "GROUP_Debug",
                    Name = "Debug Group",
                    MemberNodeIds = ["TRIG_Start", action.Id],
                    Comment = "visual note"
                }
            ]
        };
        var catalog = new[]
        {
            new NodeCatalogEntry
            {
                IdBase = "ACT_ShowMessage",
                PreviewTemplate = "Do Show Message: ${message}"
            }
        };

        rule.WireReroutes.Add(new RuleWireReroute { Id = "REROUTE_Debug", GraphX = 240, GraphY = 120 });
        rule.Connections.Single().RerouteIds.Add("REROUTE_Debug");

        var presentation = service.BuildNodePresentation(action, catalog);
        var nodeSummary = service.BuildInspectorSummary(rule, action, "", "", "", -1);
        var groupSummary = service.BuildInspectorSummary(rule, null, "GROUP_Debug", "", "", -1);
        var rerouteSummary = service.BuildInspectorSummary(rule, null, "", "REROUTE_Debug", "", -1);
        var fragmentSummary = service.BuildInspectorSummary(rule, null, "", "", "FRAG_Debug", -1);
        var wireSummary = service.BuildInspectorSummary(rule, null, "", "", "", 0);

        Assert.Equal("Action", presentation.KindText);
        Assert.Equal("ACTION", presentation.BlockBadge);
        Assert.Equal("Do", presentation.HumanVerb);
        Assert.Equal("Action / ShowMessage", presentation.BlockSubtitle);
        Assert.Equal("Do Show Message: Hello", presentation.ConfiguredSummary);
        Assert.Equal("Show Message", nodeSummary.Title);
        Assert.Equal("debug note", nodeSummary.Detail);
        Assert.Equal("Debug Group", groupSummary.Title);
        Assert.Contains("Visual group", groupSummary.Description, StringComparison.Ordinal);
        Assert.Equal("Wire Reroute", rerouteSummary.Title);
        Assert.Equal("Debug Flow", fragmentSummary.Title);
        Assert.Contains("State fragment", fragmentSummary.Description, StringComparison.Ordinal);
        Assert.Equal("Selected Wire", wireSummary.Title);
        Assert.Contains("TRIG_Start", wireSummary.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectionInspectorService_BuildsSyntheticStateRowsFromRuleNodes()
    {
        var service = new SelectionInspectorService();
        var rule = new Rule
        {
            Name = "Main Rule",
            Comment = "rule note",
            Nodes =
            [
                new RuleNode { Id = "TRIG_Start", Kind = NodeKind.Trigger, Type = "OnStart", Label = "On Start" },
                new RuleNode { Id = "COND_Check", Kind = NodeKind.Condition, Type = "Compare", Label = "Number Compare" },
                new RuleNode { Id = "ACT_Color", Kind = NodeKind.Action, Type = "SetColor", Label = "Set Object Color" }
            ]
        };

        var row = Assert.Single(service.BuildStateRuleRows(rule));

        Assert.Equal("RULE_All", row.Id);
        Assert.Equal("Main Rule", row.Name);
        Assert.Equal(GraphFragmentKind.Rule, row.Kind);
        Assert.Equal("On: On Start", row.TriggerSummary);
        Assert.Equal("Is: Number Compare", row.ConditionSummary);
        Assert.Equal("Do: Set Object Color", row.ActionSummary);
        Assert.Equal("rule note", row.Comment);
    }

    [Fact]
    public void GraphInteractionService_AddCatalogNodeConnectsFromPendingWire()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var triggerEntry = catalog.Nodes.Single(node => node.IdBase == "EV_OnTimerTick");
        var trigger = NodeCatalogService.CreateNode(triggerEntry, 100, 100, "TRIG_Start");
        var rule = new Rule
        {
            Nodes = [trigger]
        };
        var service = new GraphInteractionService();

        var add = service.AddCatalogNode(
            rule,
            catalog.Nodes,
            "ACT_ShowMessage",
            graphX: 320,
            graphY: 100,
            connectFromNodeId: trigger.Id,
            connectFromPortId: GraphPortDefaults.FlowOut);

        Assert.True(add.Result.Success);
        Assert.NotNull(add.CreatedNode);
        Assert.True(add.IncludePreviewDiffInStatus);
        Assert.Equal("Added and connected node: Show Message", add.StatusText);
        Assert.Equal(2, rule.Nodes.Count);
        var connection = Assert.Single(rule.Connections);
        Assert.Equal(trigger.Id, connection.From.NodeId);
        Assert.Equal(add.CreatedNode!.Id, connection.To.NodeId);
        Assert.Equal(GraphPortDefaults.FlowIn, connection.To.PortId);
    }

    [Fact]
    public void GraphRefreshService_BackfillsMissingCatalogParametersAcrossGraph()
    {
        var catalog = new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
        var joined = catalog.Nodes.Single(node => node.IdBase == "EV_OnPlayerJoined");
        var oldTrigger = new RuleNode
        {
            Id = "TRG_Joined",
            Kind = NodeKind.Trigger,
            Type = joined.Type,
            Label = joined.Label,
            CatalogId = joined.IdBase
        };
        var graph = new RuleGraph
        {
            Rules =
            [
                new Rule
                {
                    Id = "RULE_Old",
                    Nodes = [oldTrigger]
                }
            ]
        };
        var service = new GraphRefreshService();

        var changed = service.BackfillCatalogParameters(graph, catalog.Nodes);

        Assert.True(changed);
        var target = Assert.Single(oldTrigger.Parameters, parameter => parameter.Key == "target");
        Assert.Equal("Self", target.Value);
        Assert.Equal("Target Context", target.ValueSource);
    }

    [Fact]
    public void GraphInteractionService_SelectsFragmentNodeIdsFromNodeOrWire()
    {
        var service = new GraphInteractionService();
        var trigger = new RuleNode { Id = "TRIG_Start", Kind = NodeKind.Trigger };
        var action = new RuleNode { Id = "ACT_Show", Kind = NodeKind.Action };
        var rule = new Rule
        {
            Nodes = [trigger, action],
            Connections =
            [
                new GraphConnection
                {
                    From = new GraphEndpoint { NodeId = trigger.Id, PortId = GraphPortDefaults.FlowOut },
                    To = new GraphEndpoint { NodeId = action.Id, PortId = GraphPortDefaults.FlowIn }
                }
            ]
        };

        Assert.Equal([action.Id], service.SelectedNodeIdsForFragment(rule, action, selectedConnectionIndex: 0));
        Assert.Equal([trigger.Id, action.Id], service.SelectedNodeIdsForFragment(rule, selectedNode: null, selectedConnectionIndex: 0));
        Assert.Empty(service.SelectedNodeIdsForFragment(rule, selectedNode: null, selectedConnectionIndex: 99));
    }

    [Fact]
    public void GraphInteractionService_DeleteSelectionReportsSelectionClears()
    {
        var service = new GraphInteractionService();
        var trigger = new RuleNode { Id = "TRIG_Start", Kind = NodeKind.Trigger };
        var action = new RuleNode { Id = "ACT_Show", Kind = NodeKind.Action };
        var rule = new Rule
        {
            Nodes = [trigger, action],
            Connections =
            [
                new GraphConnection
                {
                    From = new GraphEndpoint { NodeId = trigger.Id, PortId = GraphPortDefaults.FlowOut },
                    To = new GraphEndpoint { NodeId = action.Id, PortId = GraphPortDefaults.FlowIn }
                }
            ],
            Fragments =
            [
                new GraphFragment
                {
                    Id = "FRAG_State",
                    Kind = GraphFragmentKind.State,
                    NodeIds = [trigger.Id]
                }
            ]
        };

        var fragmentDelete = service.DeleteSelection(rule, "FRAG_State", selectedConnectionIndex: -1, selectedNode: null);
        Assert.True(fragmentDelete.Result.Success);
        Assert.True(fragmentDelete.ClearFragmentSelection);
        Assert.Empty(rule.Fragments);

        var connectionDelete = service.DeleteSelection(rule, "", selectedConnectionIndex: 0, selectedNode: null);
        Assert.True(connectionDelete.Result.Success);
        Assert.True(connectionDelete.ClearConnectionSelection);
        Assert.Empty(rule.Connections);

        var nodeDelete = service.DeleteSelection(rule, "", selectedConnectionIndex: -1, selectedNode: action);
        Assert.True(nodeDelete.Result.Success);
        Assert.True(nodeDelete.ClearNodeSelection);
        Assert.DoesNotContain(rule.Nodes, node => node.Id == action.Id);
    }

    [Fact]
    public async Task ScriptDeploymentService_DeploysProjectFileThenQueuesInstanceCommand()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-script-deploy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "project.ptproj"), "{}");

        try
        {
            var service = new ScriptDeploymentService(new BridgeFileService());
            var rule = new Rule
            {
                Id = "RULE_Timer",
                Name = "TimerMessage",
                ScriptKind = GraphScriptKind.Server,
                Nodes = [new RuleNode { Id = "TRIG_Start", Type = "OnTimerTick", Kind = NodeKind.Trigger }]
            };

            var first = await service.DeployProjectScriptFileAsync(
                new ScriptProjectFileDeploymentRequest(
                    tempRoot,
                    "TimerMessage",
                    rule,
                    GraphScriptKind.Server,
                    "print(\"server\")"));

            Assert.False(first.Blocked);
            Assert.False(first.ScriptAlreadyExisted);
            Assert.True(first.MetaCreated);
            Assert.StartsWith("Deployed VRS script file", first.StatusText, StringComparison.Ordinal);
            Assert.Equal("scripts/VRS/server/VRS TimerMessage.server.luau", first.ProjectRelativeScriptPath);

            var scriptPath = Path.Combine(tempRoot, "scripts", "VRS", "server", "VRS TimerMessage.server.luau");
            var metaPath = $"{scriptPath}.meta";
            var originalMeta = await File.ReadAllTextAsync(metaPath);
            Assert.Equal("print(\"server\")", await File.ReadAllTextAsync(scriptPath));
            Assert.False(File.Exists(Path.Combine(first.BridgeDirectory, "pending-commands.json")));

            var firstHeartbeatJson = await File.ReadAllTextAsync(Path.Combine(first.BridgeDirectory, "app-heartbeat.json"));
            var firstHeartbeat = JsonSerializer.Deserialize(firstHeartbeatJson, VrsJsonContext.Default.AppHeartbeat);
            Assert.NotNull(firstHeartbeat);
            Assert.False(firstHeartbeat!.Focused);

            var second = await service.DeployProjectScriptFileAsync(
                new ScriptProjectFileDeploymentRequest(
                    tempRoot,
                    "TimerMessage",
                    rule,
                    GraphScriptKind.Server,
                    "print(\"updated\")",
                    IsVrsFocused: true));

            Assert.False(second.Blocked);
            Assert.True(second.ScriptAlreadyExisted);
            Assert.False(second.MetaCreated);
            Assert.StartsWith("Updated VRS script file", second.StatusText, StringComparison.Ordinal);
            Assert.Equal(originalMeta, await File.ReadAllTextAsync(metaPath));
            Assert.Equal("print(\"updated\")", await File.ReadAllTextAsync(scriptPath));
            Assert.False(File.Exists(Path.Combine(second.BridgeDirectory, "pending-commands.json")));

            var secondHeartbeatJson = await File.ReadAllTextAsync(Path.Combine(second.BridgeDirectory, "app-heartbeat.json"));
            var secondHeartbeat = JsonSerializer.Deserialize(secondHeartbeatJson, VrsJsonContext.Default.AppHeartbeat);
            Assert.NotNull(secondHeartbeat);
            Assert.True(secondHeartbeat!.Focused);

            var instance = await service.DeployScriptInstanceAsync(
                new ScriptInstanceDeploymentRequest(
                    SceneObjects: [],
                    ProjectRoot: tempRoot,
                    ParentPath: "World/Hidden",
                    ScriptName: "TimerMessage",
                    ScriptKind: GraphScriptKind.Server,
                    DryRun: true));

            Assert.False(instance.Blocked);
            Assert.False(instance.WasExistingTarget);
            Assert.StartsWith("Queued new script instance", instance.StatusText, StringComparison.Ordinal);

            var commandJson = await File.ReadAllTextAsync(Path.Combine(instance.BridgeDirectory, "pending-commands.json"));
            var envelope = JsonSerializer.Deserialize(commandJson, VrsJsonContext.Default.BridgeCommandEnvelope)!;
            var command = envelope.Commands.Single();

            Assert.Equal("upsert_script", command.Type);
            Assert.Equal("ServerScript", command.Kind);
            Assert.Equal("VRS TimerMessage", command.Name);
            Assert.Equal("print(\"updated\")", command.Content);
            Assert.Equal("print(\"updated\")", command.Source);
            Assert.Equal("scripts/VRS/server/VRS TimerMessage.server.luau", command.LinkedFilePath);
            Assert.True(command.FileAlreadyWritten);
            Assert.True(command.ExactParent);
            Assert.True(command.DryRun);
            Assert.Equal("World/Hidden/VRS TimerMessage", command.TargetPath);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ScriptDeploymentService_SyncsClientInputBeforeInstanceCommand()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-script-input-deploy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "project.ptproj"), "{}");

        try
        {
            var service = new ScriptDeploymentService(new BridgeFileService(), new ProjectInputManagerService());
            var rule = new Rule
            {
                Id = "RULE_Input",
                Name = "Input Rule",
                ScriptKind = GraphScriptKind.Local,
                Nodes =
                [
                    new RuleNode
                    {
                        Id = "TRIG_Input",
                        Type = "OnInputButtonDown",
                        Kind = NodeKind.Trigger,
                        Parameters =
                        [
                            new RuleParameter
                            {
                                Key = "actionName",
                                Value = "Interact",
                                Binding = new GraphValueBinding
                                {
                                    SourceKind = GraphValueSourceKind.Constant,
                                    ConstantValue = "Interact"
                                }
                            }
                        ]
                    }
                ]
            };

            var result = await service.DeployProjectScriptFileAsync(
                new ScriptProjectFileDeploymentRequest(
                    tempRoot,
                    "Input Rule",
                    rule,
                    GraphScriptKind.Local,
                    "print(\"client\")"));

            Assert.False(result.Blocked);
            Assert.Contains("Input Manager", result.StatusText, StringComparison.Ordinal);
            Assert.Equal("scripts/VRS/client/VRS Input Rule.client.luau", result.ProjectRelativeScriptPath);

            var inputJson = await File.ReadAllTextAsync(Path.Combine(tempRoot, "input.json"));
            using var inputDocument = JsonDocument.Parse(inputJson);
            var action = Assert.Single(inputDocument.RootElement.GetProperty("Actions").EnumerateArray());
            Assert.Equal("Button", action.GetProperty("Type").GetString());
            Assert.Equal("Interact", action.GetProperty("Name").GetString());
            Assert.Equal(JsonValueKind.Array, action.GetProperty("Buttons").ValueKind);

            var registryJson = await File.ReadAllTextAsync(Path.Combine(tempRoot, ".vrs", "input-manager.json"));
            var registry = JsonSerializer.Deserialize(registryJson, VrsJsonContext.Default.VrsManagedInputRegistry);
            var record = Assert.Single(registry!.Actions);
            Assert.Equal("Interact", record.Name);
            Assert.Equal(VrsInputActionType.Button, record.Type);
            Assert.Equal("scripts/VRS/client/VRS Input Rule.client.luau", Assert.Single(record.Usages).ScriptPath);
            Assert.False(File.Exists(Path.Combine(result.BridgeDirectory, "pending-commands.json")));

            var instance = await service.DeployScriptInstanceAsync(
                new ScriptInstanceDeploymentRequest(
                    SceneObjects: [],
                    ProjectRoot: tempRoot,
                    ParentPath: "World/Hidden",
                    ScriptName: "Input Rule",
                    ScriptKind: GraphScriptKind.Local,
                    DryRun: true));

            var commandJson = await File.ReadAllTextAsync(Path.Combine(instance.BridgeDirectory, "pending-commands.json"));
            var envelope = JsonSerializer.Deserialize(commandJson, VrsJsonContext.Default.BridgeCommandEnvelope);

            Assert.NotNull(envelope);
            var command = Assert.Single(envelope!.Commands);
            Assert.Equal("upsert_script", command.Type);
            Assert.Equal(instance.CommandId, command.Id);
            Assert.Equal("ClientScript", command.Kind);
            Assert.Equal("scripts/VRS/client/VRS Input Rule.client.luau", command.LinkedFilePath);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ScriptDeploymentService_BlocksServerInputNodesBeforeWritingScript()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-script-input-block-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "project.ptproj"), "{}");

        try
        {
            var service = new ScriptDeploymentService(new BridgeFileService(), new ProjectInputManagerService());
            var rule = new Rule
            {
                Id = "RULE_ServerInput",
                Name = "Server Input",
                ScriptKind = GraphScriptKind.Server,
                Nodes =
                [
                    new RuleNode
                    {
                        Id = "TRIG_Input",
                        Type = "OnInputButtonDown",
                        Kind = NodeKind.Trigger,
                        Value = "actionName=Interact"
                    }
                ]
            };

            var result = await service.DeployProjectScriptFileAsync(
                new ScriptProjectFileDeploymentRequest(
                    tempRoot,
                    "Server Input",
                    rule,
                    GraphScriptKind.Server,
                    "print(\"server\")"));

            Assert.True(result.Blocked);
            Assert.Contains("ClientScript", result.StatusText, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(tempRoot, "input.json")));
            Assert.False(File.Exists(Path.Combine(tempRoot, "scripts", "VRS", "server", "VRS Server Input.server.luau")));
            Assert.False(File.Exists(Path.Combine(result.BridgeDirectory, "pending-commands.json")));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ScriptDeploymentService_ReportsSavedScriptFileStatus()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-script-file-status-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempRoot, "scripts", "VRS", "server"));

        try
        {
            var service = new ScriptDeploymentService(new BridgeFileService());
            var missing = service.GetSavedScriptFileStatus(tempRoot, "TimerMessage", GraphScriptKind.Server);

            Assert.Equal("scripts/VRS/server/VRS TimerMessage.server.luau", missing.ProjectRelativePath);
            Assert.False(missing.Exists);
            Assert.False(missing.IsEmpty);
            Assert.False(missing.IsReady);
            Assert.Equal("File missing", missing.StatusText);

            var scriptPath = Path.Combine(tempRoot, "scripts", "VRS", "server", "VRS TimerMessage.server.luau");
            File.WriteAllText(scriptPath, "");

            var empty = service.GetSavedScriptFileStatus(tempRoot, "VRS TimerMessage", GraphScriptKind.Server);

            Assert.True(empty.Exists);
            Assert.True(empty.IsEmpty);
            Assert.False(empty.IsReady);
            Assert.Equal(0, empty.Length);
            Assert.Equal("File empty", empty.StatusText);

            File.WriteAllText(scriptPath, "print(\"ready\")");

            var ready = service.GetSavedScriptFileStatus(tempRoot, "TimerMessage", GraphScriptKind.Server);

            Assert.True(ready.Exists);
            Assert.False(ready.IsEmpty);
            Assert.True(ready.IsReady);
            Assert.True(ready.Length > 0);
            Assert.Equal(scriptPath, ready.AbsolutePath);
            Assert.Equal("File ready", ready.StatusText);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ScriptDeploymentService_BlocksInstanceWhenProjectFileIsMissing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-script-instance-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "project.ptproj"), "{}");

        try
        {
            var service = new ScriptDeploymentService(new BridgeFileService());

            var result = await service.DeployScriptInstanceAsync(
                new ScriptInstanceDeploymentRequest(
                    SceneObjects: [],
                    ProjectRoot: tempRoot,
                    ParentPath: "World/Hidden",
                    ScriptName: "TimerMessage",
                    ScriptKind: GraphScriptKind.Server,
                    DryRun: false));

            Assert.True(result.Blocked);
            Assert.Equal("", result.CommandId);
            Assert.Contains("Deploy File first", result.StatusText, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(result.BridgeDirectory, "pending-commands.json")));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ScriptDeploymentService_BlocksInstanceWhenProjectFileIsEmpty()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-script-instance-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempRoot, "scripts", "VRS", "server"));
        File.WriteAllText(Path.Combine(tempRoot, "project.ptproj"), "{}");
        File.WriteAllText(Path.Combine(tempRoot, "scripts", "VRS", "server", "VRS TimerMessage.server.luau"), "");

        try
        {
            var service = new ScriptDeploymentService(new BridgeFileService());

            var result = await service.DeployScriptInstanceAsync(
                new ScriptInstanceDeploymentRequest(
                    SceneObjects: [],
                    ProjectRoot: tempRoot,
                    ParentPath: "World/Hidden",
                    ScriptName: "TimerMessage",
                    ScriptKind: GraphScriptKind.Server,
                    DryRun: false));

            Assert.True(result.Blocked);
            Assert.Equal("", result.CommandId);
            Assert.Contains("Deploy File first", result.StatusText, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(result.BridgeDirectory, "pending-commands.json")));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ScriptDeploymentService_DetectsSavedTargetFromSceneObjectsAndLinkRegistry()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-script-link-{Guid.NewGuid():N}");
        var bridgeDirectory = Path.Combine(tempRoot, "addons", "visual-programming-bridge", "bridge");
        Directory.CreateDirectory(bridgeDirectory);

        try
        {
            var service = new ScriptDeploymentService(new BridgeFileService());
            var sceneLinkedQuery = new ScriptDeploymentTargetQuery(
                SceneObjects:
                [
                    new SceneObject
                    {
                        Path = "World/Hidden/VRS TimerMessage",
                        LinkedScriptPath = "scripts/VRS/server/VRS TimerMessage.server.luau"
                    }
                ],
                ProjectRoot: tempRoot,
                ParentPath: "World/Hidden",
                ScriptName: "TimerMessage",
                ScriptKind: GraphScriptKind.Server);

            Assert.True(service.IsSavedDeployScriptTarget(sceneLinkedQuery));

            File.WriteAllText(
                Path.Combine(bridgeDirectory, "script-links.json"),
                """
                {
                    "links": {
                    "World/Hidden/VRS RegistryMessage": {
                      "linkedFilePath": "scripts/VRS/server/VRS RegistryMessage.server.luau"
                    }
                  }
                }
                """);
            var registryQuery = sceneLinkedQuery with
            {
                SceneObjects = [],
                ScriptName = "RegistryMessage"
            };

            Assert.True(service.IsSavedDeployScriptTarget(registryQuery));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static SceneObject Scene(string path, string name, string kind, string linkedScriptPath = "")
    {
        return new SceneObject
        {
            Path = path,
            Name = name,
            Kind = kind,
            LinkedScriptPath = linkedScriptPath
        };
    }

    private static bool IsDirectEnvironmentChild(string path)
    {
        const string environmentPrefix = "World/Environment/";
        return path.StartsWith(environmentPrefix, StringComparison.OrdinalIgnoreCase) &&
            !path[environmentPrefix.Length..].Contains('/', StringComparison.Ordinal);
    }

    private static SceneHierarchyItemViewModel FindSceneItem(IEnumerable<SceneHierarchyItemViewModel> items, string path)
    {
        foreach (var item in items)
        {
            if (item.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }

            var found = FindSceneItemOrNull(item.Children, path);
            if (found is not null)
            {
                return found;
            }
        }

        throw new InvalidOperationException($"Scene item was not found: {path}");
    }

    private static SceneHierarchyItemViewModel? FindSceneItemOrNull(IEnumerable<SceneHierarchyItemViewModel> items, string path)
    {
        foreach (var item in items)
        {
            if (item.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }

            var found = FindSceneItemOrNull(item.Children, path);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static string UpsertCommandResultsJson(string commandId)
    {
        return $$"""
        {
          "version": 1,
          "results": [
            {
              "ok": true,
              "path": "World/Environment/CodexBeacon/VRS TimerMessage",
              "index": 1,
              "id": "{{commandId}}",
              "message": "upsert_script created ServerScript linked to scripts/VRS/server/VRS TimerMessage.server.luau",
              "action": "upsert_script",
              "skipped": false,
              "details": {
                "created": true,
                "linkedFilePath": "scripts/VRS/server/VRS TimerMessage.server.luau",
                "scriptKind": "ServerScript",
                "targetPath": "World/Environment/CodexBeacon/VRS TimerMessage"
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

    private const string MinimalSceneSnapshotJson = """
    {
      "format": "visual-programming-bridge-scene-snapshot",
      "version": 1,
      "createdAtUtc": "2026-06-20T12:00:00Z",
      "snapshotVersion": 1,
      "root": {
        "path": "World",
        "name": "World",
        "className": "World",
        "depth": 0,
        "children": [
          {
            "path": "World/Environment",
            "name": "Environment",
            "className": "Environment",
            "depth": 1,
            "children": {}
          }
        ]
      }
    }
    """;

    private static string SingleChildSceneSnapshotJson(string childName) => $$"""
    {
      "format": "visual-programming-bridge-scene-snapshot",
      "version": 1,
      "createdAtUtc": "2026-06-20T12:00:00Z",
      "snapshotVersion": 1,
      "root": {
        "path": "World",
        "name": "World",
        "className": "World",
        "depth": 0,
        "children": [
          {
            "path": "World/{{childName}}",
            "name": "{{childName}}",
            "className": "Folder",
            "depth": 1,
            "children": {}
          }
        ]
      }
    }
    """;

    private const string OrderedEnvironmentSceneSnapshotJson = """
    {
      "format": "visual-programming-bridge-scene-snapshot",
      "version": 1,
      "createdAtUtc": "2026-06-20T12:00:00Z",
      "snapshotVersion": 1,
      "root": {
        "path": "World",
        "name": "World",
        "className": "World",
        "depth": 0,
        "children": [
          {
            "path": "World/Environment",
            "name": "Environment",
            "className": "Environment",
            "depth": 1,
            "children": [
              { "path": "World/Environment/Part3", "name": "Part3", "className": "Part", "depth": 2, "children": {} },
              { "path": "World/Environment/Part4", "name": "Part4", "className": "Part", "depth": 2, "children": {} },
              { "path": "World/Environment/Part5", "name": "Part5", "className": "Part", "depth": 2, "children": {} },
              { "path": "World/Environment/Part6", "name": "Part6", "className": "Part", "depth": 2, "children": {} },
              { "path": "World/Environment/Part7", "name": "Part7", "className": "Part", "depth": 2, "children": {} },
              { "path": "World/Environment/Part8", "name": "Part8", "className": "Part", "depth": 2, "children": {} },
              { "path": "World/Environment/VisualFolder", "name": "VisualFolder", "className": "Folder", "depth": 2, "children": {} },
              { "path": "World/Environment/Coin", "name": "Coin", "className": "Part", "depth": 2, "children": {} },
              { "path": "World/Environment/Trigger Part", "name": "Trigger Part", "className": "Part", "depth": 2, "children": {} },
              { "path": "World/Environment/Part9", "name": "Part9", "className": "Part", "depth": 2, "children": {} },
              { "path": "World/Environment/Part10", "name": "Part10", "className": "Part", "depth": 2, "children": {} },
              { "path": "World/Environment/Model", "name": "Model", "className": "Model", "depth": 2, "children": {} },
              { "path": "World/Environment/Kill Brick", "name": "Kill Brick", "className": "Part", "depth": 2, "children": {} }
            ]
          }
        ]
      }
    }
    """;

    private const string LinkedScriptSceneSnapshotJson = """
    {
      "format": "visual-programming-bridge-scene-snapshot",
      "version": 1,
      "createdAtUtc": "2026-06-20T12:00:00Z",
      "snapshotVersion": 2,
      "root": {
        "path": "World",
        "name": "World",
        "className": "World",
        "depth": 0,
        "children": [
          {
            "path": "World/Environment",
            "name": "Environment",
            "className": "Environment",
            "depth": 1,
            "children": [
              {
                "path": "World/Environment/CodexBeacon",
                "name": "CodexBeacon",
                "className": "Part",
                "depth": 2,
                "children": [
                  {
                    "path": "World/Environment/CodexBeacon/VRS TimerMessage",
                    "name": "VRS TimerMessage",
                    "className": "ServerScript",
                    "depth": 3,
                    "linkedScriptPath": "scripts/VRS/server/VRS TimerMessage.server.luau",
                    "isLinkedScript": true,
                    "isVisualScriptName": true,
                    "children": {}
                  }
                ]
              }
            ]
          }
        ]
      }
    }
    """;

    private static void AssertClose(double expected, double actual)
    {
        Assert.True(Math.Abs(expected - actual) <= 0.000001, $"Expected {expected}, got {actual}.");
    }
}
