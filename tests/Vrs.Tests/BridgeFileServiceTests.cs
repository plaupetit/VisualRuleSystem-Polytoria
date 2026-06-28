using System.Text.Json;
using Vrs.Core.Bridge;
using Vrs.Core.Persistence;
using Vrs.Core.RuntimeEvents;
using Vrs.Graph.Model;

namespace Vrs.Tests;

public sealed class BridgeFileServiceTests
{
    [Fact]
    public async Task SaveActiveProject_WritesLoadableConfigAtomically()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-active-project-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var configPath = Path.Combine(tempRoot, "active-polytoria-project.json");
            var projectRoot = Path.Combine(tempRoot, "SuperTest");
            Directory.CreateDirectory(projectRoot);

            var service = new BridgeFileService();
            await service.SaveActiveProjectAsync(configPath, projectRoot);
            var loaded = await service.LoadActiveProjectAsync(configPath);

            Assert.NotNull(loaded);
            Assert.Equal(Path.GetFullPath(projectRoot), loaded!.ProjectRoot);
            Assert.Equal("visual-rule-system-active-polytoria-project", loaded.Format);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task QueueCreateFolder_WritesPendingCommandAndHeartbeat()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-bridge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var service = new BridgeFileService();
            var commandId = await service.QueueCreateFolderAsync(tempRoot, "Hidden", "VRS_Test", dryRun: true);
            await service.WriteHeartbeatAsync(tempRoot, active: true, focused: true);

            var commandJson = await File.ReadAllTextAsync(Path.Combine(tempRoot, "pending-commands.json"));
            var envelope = JsonSerializer.Deserialize(commandJson, VrsJsonContext.Default.BridgeCommandEnvelope);

            Assert.NotNull(envelope);
            Assert.Equal(commandId, envelope!.CommandId);
            Assert.True(envelope.DryRun);
            Assert.Equal(commandId, envelope.Commands.Single().Id);
            Assert.Equal("create_folder", envelope.Commands.Single().Type);
            Assert.True(envelope.Commands.Single().DryRun);
            Assert.True(File.Exists(Path.Combine(tempRoot, "app-heartbeat.json")));

            var heartbeatJson = await File.ReadAllTextAsync(Path.Combine(tempRoot, "app-heartbeat.json"));
            var heartbeat = JsonSerializer.Deserialize(heartbeatJson, VrsJsonContext.Default.AppHeartbeat);

            Assert.NotNull(heartbeat);
            Assert.True(heartbeat!.Active);
            Assert.False(string.IsNullOrWhiteSpace(heartbeat.SessionId));
            Assert.True(heartbeat.ExpiresAtUnixSeconds > heartbeat.UpdatedAtUnixSeconds);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAppState_WritesReadableWorkspaceSummary()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-app-state-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var service = new BridgeFileService();
            await service.WriteAppStateAsync(
                tempRoot,
                new BridgeAppState
                {
                    Focused = true,
                    ActiveProjectName = "BridgeTest",
                    ProjectUiMode = "Project linked; Creator not running",
                    ProjectLinked = true,
                    ScriptKind = "Server",
                    AuthorScriptName = "TimerMessage",
                    CreatorScriptName = "VRS TimerMessage",
                    ProjectRelativeScriptPath = "scripts/VRS/server/VRS TimerMessage.server.luau",
                    SelectedCreatorObjectPath = "World/Environment/Baseplate",
                    DeployParentPath = "World/Environment",
                    NodeCount = 3,
                    ValidationMessageCount = 1,
                    ValidationWarningCount = 1,
                    BridgeBeatText = "Addon pending | VRS focus",
                    StatusText = "Ready",
                    LuauPreviewSummary = "-- preview",
                    RecentLogs = ["12:00 Ready"]
                });

            var appStatePath = Path.Combine(tempRoot, "app-state.json");
            var appState = JsonSerializer.Deserialize(
                await File.ReadAllTextAsync(appStatePath),
                VrsJsonContext.Default.BridgeAppState);

            Assert.NotNull(appState);
            Assert.Equal("visual-programming-bridge-app-state", appState!.Format);
            Assert.Equal("VisualRuleSystem", appState.App);
            Assert.False(string.IsNullOrWhiteSpace(appState.SessionId));
            Assert.True(appState.ProcessId > 0);
            Assert.False(string.IsNullOrWhiteSpace(appState.UpdatedAtUtc));
            Assert.True(appState.Focused);
            Assert.Equal("VRS TimerMessage", appState.CreatorScriptName);
            Assert.Equal("scripts/VRS/server/VRS TimerMessage.server.luau", appState.ProjectRelativeScriptPath);
            Assert.Equal("World/Environment/Baseplate", appState.SelectedCreatorObjectPath);
            Assert.Equal(3, appState.NodeCount);
            Assert.Equal(1, appState.ValidationWarningCount);

            var loaded = await service.ReadAppStateAsync(tempRoot);
            Assert.Equal(appState.CreatorScriptName, loaded!.CreatorScriptName);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task WriteSnapshotRequest_WritesRequestForCreator()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-snapshot-request-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var service = new BridgeFileService();
            var requestId = await service.WriteSnapshotRequestAsync(tempRoot, "manual-test", "full");
            var request = JsonSerializer.Deserialize(
                await File.ReadAllTextAsync(Path.Combine(tempRoot, "snapshot-request.json")),
                VrsJsonContext.Default.SnapshotRequest);

            Assert.NotNull(request);
            Assert.Equal("visual-programming-bridge-snapshot-request", request!.Format);
            Assert.Equal(requestId, request.RequestId);
            Assert.StartsWith("snapshot_request_", request.RequestId, StringComparison.Ordinal);
            Assert.Equal("manual-test", request.Reason);
            Assert.Equal("full", request.Mode);
            Assert.False(string.IsNullOrWhiteSpace(request.SessionId));
            Assert.False(string.IsNullOrWhiteSpace(request.CreatedAtUtc));

            var loaded = await service.ReadSnapshotRequestAsync(tempRoot);
            Assert.Equal(requestId, loaded!.RequestId);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task WriteCommands_RejectsUnsupportedCommand()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-bridge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var service = new BridgeFileService();
            var envelope = new BridgeCommandEnvelope
            {
                CommandId = "bad",
                Commands = [new BridgeCommand { Type = "delete", Name = "Unsafe" }]
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.WriteCommandsAsync(tempRoot, envelope));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task QueueEnsureFolderPath_WritesCreatorPathCommand()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-bridge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var service = new BridgeFileService();
            var commandId = await service.QueueEnsureFolderPathAsync(
                tempRoot,
                VrsRuntimeEventPaths.ManagedUserInputNetworkEventsPath,
                dryRun: false);

            var commandJson = await File.ReadAllTextAsync(Path.Combine(tempRoot, "pending-commands.json"));
            var envelope = JsonSerializer.Deserialize(commandJson, VrsJsonContext.Default.BridgeCommandEnvelope);
            var command = envelope!.Commands.Single();

            Assert.Equal(commandId, envelope.CommandId);
            Assert.False(envelope.DryRun);
            Assert.Equal(commandId, command.Id);
            Assert.Equal("ensure_folder_path", command.Type);
            Assert.Equal(VrsRuntimeEventPaths.ManagedUserInputNetworkEventsPath, command.Path);
            Assert.Equal(VrsRuntimeEventPaths.ManagedUserInputNetworkEventsPath, command.TargetPath);
            Assert.False(command.DryRun);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task QueueEnsureNetworkEvents_WritesFolderAndNetworkEventCommands()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-bridge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var service = new BridgeFileService();
            var commandId = await service.QueueEnsureNetworkEventsAsync(
                tempRoot,
                VrsRuntimeEventPaths.ManagedUserInputNetworkEventsPath,
                ["Jump", "Interact"],
                dryRun: false);

            var commandJson = await File.ReadAllTextAsync(Path.Combine(tempRoot, "pending-commands.json"));
            var envelope = JsonSerializer.Deserialize(commandJson, VrsJsonContext.Default.BridgeCommandEnvelope);

            Assert.Equal(commandId, envelope!.CommandId);
            Assert.False(envelope.DryRun);
            Assert.Equal(3, envelope.Commands.Count);
            Assert.Equal("ensure_folder_path", envelope.Commands[0].Type);
            Assert.Equal(VrsRuntimeEventPaths.ManagedUserInputNetworkEventsPath, envelope.Commands[0].Path);

            var jump = envelope.Commands.Single(command => command.Type == "ensure_network_event" && command.Name == "Jump");
            Assert.Equal("NetworkEvent", jump.Kind);
            Assert.Equal(VrsRuntimeEventPaths.ManagedUserInputNetworkEventsPath, jump.ParentPath);
            Assert.Equal($"{VrsRuntimeEventPaths.ManagedUserInputNetworkEventsPath}/Jump", jump.TargetPath);
            Assert.False(jump.DryRun);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ReadCommandResults_ParsesCreatorResultObjects()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-command-results-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(tempRoot, "command-results.json"),
                """
                {
                  "version": 1,
                  "results": [
                    {
                      "ok": true,
                      "path": "World/Environment/CodexBeacon/VRS TimerMessage",
                      "index": 1,
                      "id": "upsert_script_20260620173612378",
                      "message": "upsert_script created ServerScript linked to scripts/VRS/server/VRS TimerMessage.server.luau",
                      "action": "upsert_script",
                      "skipped": false,
                      "details": {
                        "created": true,
                        "linkedFilePath": "scripts/VRS/server/VRS TimerMessage.server.luau",
                        "scriptKind": "ServerScript",
                        "targetPath": "World/Environment/CodexBeacon/VRS TimerMessage"
                      },
                      "handledAtUtc": "2026-06-20T17:36:13Z"
                    }
                  ],
                  "createdAtUtc": "2026-06-20T17:36:13Z",
                  "format": "visual-programming-bridge-command-results",
                  "createdBy": "[VRS]Visual Programming Bridge",
                  "runtimeVersion": "0.8.15"
                }
                """);

            var service = new BridgeFileService();
            var commandResults = await service.ReadCommandResultsAsync(tempRoot);
            var result = Assert.Single(commandResults!.Results);

            Assert.Equal("visual-programming-bridge-command-results", commandResults.Format);
            Assert.Equal("upsert_script_20260620173612378", result.Id);
            Assert.True(result.Ok);
            Assert.Equal("upsert_script", result.Action);
            Assert.Equal("scripts/VRS/server/VRS TimerMessage.server.luau", result.Details.LinkedFilePath);
            Assert.Equal("ServerScript", result.Details.ScriptKind);
            Assert.Equal("World/Environment/CodexBeacon/VRS TimerMessage", result.Details.TargetPath);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task QueueUpsertScript_WritesScriptKindAndContent()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-bridge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var service = new BridgeFileService();
            var commandId = await service.QueueUpsertScriptAsync(
                tempRoot,
                "World/Hidden",
                "ClientTimer",
                GraphScriptKind.Local,
                "print(\"client\")",
                dryRun: false);

            var commandJson = await File.ReadAllTextAsync(Path.Combine(tempRoot, "pending-commands.json"));
            var envelope = JsonSerializer.Deserialize(commandJson, VrsJsonContext.Default.BridgeCommandEnvelope);
            var command = envelope!.Commands.Single();

            Assert.Equal(commandId, envelope.CommandId);
            Assert.False(envelope.DryRun);
            Assert.Equal(commandId, command.Id);
            Assert.Equal("upsert_script", command.Type);
            Assert.Equal("ClientScript", command.Kind);
            Assert.False(command.DryRun);
            Assert.Equal("Local", command.ScriptKind);
            Assert.Equal("print(\"client\")", command.Content);
            Assert.False(command.ExactParent);
            Assert.Equal("World/Hidden/ClientTimer", command.TargetPath);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task WriteLinkedScriptFile_WritesTypedProjectFileAndPreservesMeta()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-bridge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var service = new BridgeFileService();
            var first = await service.WriteLinkedScriptFileAsync(
                tempRoot,
                "Timer Message",
                GraphScriptKind.Server,
                "print(\"first\")");

            Assert.Equal("scripts/VRS/server/Timer Message.server.luau", first.ProjectRelativeScriptPath);
            Assert.False(first.ScriptAlreadyExisted);
            Assert.True(first.MetaCreated);
            Assert.True(File.Exists(first.ScriptPath));
            Assert.True(File.Exists(first.MetaPath));

            var originalMeta = await File.ReadAllTextAsync(first.MetaPath);
            var second = await service.WriteLinkedScriptFileAsync(
                tempRoot,
                "Timer Message",
                GraphScriptKind.Server,
                "print(\"second\")");

            Assert.True(second.ScriptAlreadyExisted);
            Assert.False(second.MetaCreated);
            Assert.Equal("print(\"second\")", await File.ReadAllTextAsync(second.ScriptPath));
            Assert.Equal(originalMeta, await File.ReadAllTextAsync(second.MetaPath));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task QueueUpsertLinkedScript_WritesLinkedFileFieldsForCreator()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-bridge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var service = new BridgeFileService();
            var commandId = await service.QueueUpsertLinkedScriptAsync(
                tempRoot,
                "World/Hidden",
                "TimerMessage",
                GraphScriptKind.Server,
                "print(\"server\")",
                "scripts/VRS/server/TimerMessage.server.luau",
                fileAlreadyWritten: true,
                dryRun: true);

            var commandJson = await File.ReadAllTextAsync(Path.Combine(tempRoot, "pending-commands.json"));
            var envelope = JsonSerializer.Deserialize(commandJson, VrsJsonContext.Default.BridgeCommandEnvelope);
            var command = envelope!.Commands.Single();

            Assert.Equal(commandId, envelope.CommandId);
            Assert.True(envelope.DryRun);
            Assert.Equal("upsert_script", command.Type);
            Assert.Equal("ServerScript", command.Kind);
            Assert.True(command.DryRun);
            Assert.Equal("Server", command.ScriptKind);
            Assert.Equal("print(\"server\")", command.Content);
            Assert.Equal("print(\"server\")", command.Source);
            Assert.Equal("scripts/VRS/server/TimerMessage.server.luau", command.LinkedFilePath);
            Assert.True(command.FileAlreadyWritten);
            Assert.False(command.ExactParent);
            Assert.Equal("World/Hidden/TimerMessage", command.TargetPath);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task QueueEnsureFolderPathAndUpsertLinkedScript_WritesUniqueBatchCommands()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-bridge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var service = new BridgeFileService();
            var commandId = await service.QueueEnsureFolderPathAndUpsertLinkedScriptAsync(
                tempRoot,
                VrsRuntimeEventPaths.ManagedUserInputNetworkEventsPath,
                "World/Hidden",
                "VRS Input Script",
                GraphScriptKind.Local,
                "print(\"client\")",
                "scripts/VRS/client/VRS Input Script.client.luau",
                fileAlreadyWritten: true,
                exactParent: true,
                dryRun: true);

            var commandJson = await File.ReadAllTextAsync(Path.Combine(tempRoot, "pending-commands.json"));
            var envelope = JsonSerializer.Deserialize(commandJson, VrsJsonContext.Default.BridgeCommandEnvelope);

            Assert.NotNull(envelope);
            Assert.Equal(2, envelope!.Commands.Count);
            Assert.Equal("ensure_folder_path", envelope.Commands[0].Type);
            Assert.Equal("upsert_script", envelope.Commands[1].Type);
            Assert.NotEqual(envelope.Commands[0].Id, envelope.Commands[1].Id);
            Assert.Equal(commandId, envelope.Commands[1].Id);
            Assert.Equal(VrsRuntimeEventPaths.ManagedUserInputNetworkEventsPath, envelope.Commands[0].Path);
            Assert.Equal("ClientScript", envelope.Commands[1].Kind);
            Assert.Equal("scripts/VRS/client/VRS Input Script.client.luau", envelope.Commands[1].LinkedFilePath);
            Assert.True(envelope.Commands[1].ExactParent);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ReadSceneSnapshotObjects_LoadsBoundedTreeAndSkipsBridgeTrash()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-bridge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "scene-snapshot.json"), SampleSceneSnapshotJson);

            var service = new BridgeFileService();
            var result = await service.ReadSceneSnapshotObjectsAsync(
                tempRoot,
                new SceneSnapshotReadOptions
                {
                    MaxObjects = 10,
                    MaxDepth = 4
                });

            Assert.NotNull(result);
            Assert.Equal(6, result!.DiagnosticObjectCount);
            Assert.Equal(42, result.SnapshotVersion);
            Assert.Contains(result.Objects, item => item.Path == "World/Hidden/VRS");
            Assert.Contains(result.Objects, item => item.LinkedScriptPath == "scripts/VRS/server/VRS Coin.server.luau");
            Assert.DoesNotContain(result.Objects, item => item.Path.Contains("VisualBridgeTrash", StringComparison.OrdinalIgnoreCase));
            Assert.True(result.PrunedSubtrees > 0);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ReadSceneSnapshotObjects_StopsAfterDisplayLimit()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-bridge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "scene-snapshot.json"), SampleSceneSnapshotJson);

            var service = new BridgeFileService();
            var result = await service.ReadSceneSnapshotObjectsAsync(
                tempRoot,
                new SceneSnapshotReadOptions
                {
                    MaxObjects = 2,
                    MaxDepth = 8,
                    IncludeBridgeTrash = true
                });

            Assert.NotNull(result);
            Assert.Equal(2, result!.Objects.Count);
            Assert.True(result.WasLimited);
            Assert.True(result.PrunedSubtrees > 0 || result.SkippedByDisplayLimit > 0);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ReadSceneSnapshotObjects_ClampsInvalidLimits()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-bridge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "scene-snapshot.json"), SampleSceneSnapshotJson);

            var service = new BridgeFileService();
            var result = await service.ReadSceneSnapshotObjectsAsync(
                tempRoot,
                new SceneSnapshotReadOptions
                {
                    MaxObjects = 0,
                    MaxDepth = -10,
                    IncludeBridgeTrash = true
                });

            Assert.NotNull(result);
            var sceneObject = Assert.Single(result!.Objects);

            Assert.Equal("World", sceneObject.Path);
            Assert.True(result.PrunedSubtrees > 0);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ReadSceneSnapshotObjects_SupportsObjectAndArrayChildren()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-bridge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "scene-snapshot.json"), MixedChildrenSnapshotJson);

            var service = new BridgeFileService();
            var result = await service.ReadSceneSnapshotObjectsAsync(
                tempRoot,
                new SceneSnapshotReadOptions
                {
                    MaxObjects = 10,
                    MaxDepth = 5
                });

            Assert.NotNull(result);
            Assert.Equal(5, result!.Objects.Count);
            Assert.Contains(result.Objects, item => item.Path == "World/ObjectParent/PartA");
            Assert.Contains(result.Objects, item => item.Path == "World/ArrayParent/PartB");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private const string SampleSceneSnapshotJson = """
        {
          "format": "visual-programming-bridge-scene-snapshot",
          "version": 1,
          "createdAtUtc": "2026-06-17T16:51:54Z",
          "snapshotVersion": 42,
          "runtimeVersion": "0.8.14",
          "diagnostics": {
            "objectCount": 6,
            "watcherCount": 6,
            "truncatedCount": 0,
            "maxObservedDepth": 5
          },
          "root": {
            "path": "World",
            "objectId": "world",
            "children": [
              {
                "path": "World/Hidden",
                "objectId": "hidden",
                "children": [
                  {
                    "path": "World/Hidden/VRS",
                    "objectId": "vrs",
                    "children": {},
                    "name": "VRS",
                    "className": "Folder",
                    "depth": 2
                  }
                ],
                "name": "Hidden",
                "className": "Folder",
                "depth": 1
              },
              {
                "path": "World/Environment/Coin/VRS Coin",
                "linkedScriptPath": "scripts/VRS/server/VRS Coin.server.luau",
                "objectId": "coin-script",
                "children": {},
                "isLinkedScript": true,
                "isVisualScriptName": true,
                "name": "VRS Coin",
                "className": "ServerScript",
                "depth": 3
              },
              {
                "path": "World/ServerHidden/VisualBridgeTrash",
                "objectId": "trash",
                "children": [
                  {
                    "path": "World/ServerHidden/VisualBridgeTrash/HeavyPart",
                    "objectId": "heavy",
                    "children": {},
                    "name": "HeavyPart",
                    "className": "Part",
                    "depth": 3
                  }
                ],
                "name": "VisualBridgeTrash",
                "className": "Folder",
                "depth": 2
              }
            ],
            "name": "World",
            "className": "World",
            "depth": 0
          }
        }
        """;

    private const string MixedChildrenSnapshotJson = """
        {
          "format": "visual-programming-bridge-scene-snapshot",
          "version": 1,
          "createdAtUtc": "2026-06-17T16:51:54Z",
          "root": {
            "path": "World",
            "name": "World",
            "className": "World",
            "depth": 0,
            "children": {
              "objectParent": {
                "path": "World/ObjectParent",
                "name": "ObjectParent",
                "className": "Folder",
                "depth": 1,
                "children": {
                  "partA": {
                    "path": "World/ObjectParent/PartA",
                    "name": "PartA",
                    "className": "Part",
                    "depth": 2,
                    "children": {}
                  }
                }
              },
              "arrayParent": {
                "path": "World/ArrayParent",
                "name": "ArrayParent",
                "className": "Folder",
                "depth": 1,
                "children": [
                  {
                    "path": "World/ArrayParent/PartB",
                    "name": "PartB",
                    "className": "Part",
                    "depth": 2,
                    "children": {}
                  }
                ]
              }
            }
          }
        }
        """;
}
