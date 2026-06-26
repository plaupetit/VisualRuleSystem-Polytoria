using System.Text.Json;
using Vrs.Core.Persistence;
using Vrs.Core.ProjectInputs;
using Vrs.Graph.Model;

namespace Vrs.Tests;

public sealed class ProjectInputManagerServiceTests
{
    [Fact]
    public async Task EnsurePresetActions_CreatesStandardPresetsWithoutOverwritingExistingActions()
    {
        var tempRoot = CreateTempProjectRoot();

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(tempRoot, "input.json"),
                """
                {
                  "Actions": [
                    {
                      "Type": "Axis",
                      "Name": "Horizontal",
                      "Negative": [{ "KeyCode": 65 }],
                      "Positive": [{ "KeyCode": 68 }]
                    }
                  ]
                }
                """);

            var service = new ProjectInputManagerService();
            var result = await service.EnsurePresetActionsAsync(tempRoot);

            Assert.True(result.Succeeded);
            Assert.Equal(12, result.EnsuredCount);
            Assert.Equal(11, result.CreatedCount);
            Assert.Equal(1, result.ExistingManualCount);

            using var inputDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(tempRoot, "input.json")));
            var actions = inputDocument.RootElement.GetProperty("Actions").EnumerateArray().ToList();

            Assert.Contains(actions, action => action.GetProperty("Name").GetString() == "Interact");
            var jump = actions.Single(action => action.GetProperty("Name").GetString() == "Jump");
            Assert.Equal("Button", jump.GetProperty("Type").GetString());
            Assert.Contains(jump.GetProperty("Buttons").EnumerateArray(), button => button.GetInt32() == 32);

            var horizontal = actions.Single(action => action.GetProperty("Name").GetString() == "Horizontal");
            Assert.Single(horizontal.GetProperty("Negative").EnumerateArray());
            Assert.Single(horizontal.GetProperty("Positive").EnumerateArray());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ReadInputActionChoices_MergesProjectActionsAndVrsPresetsForDropdowns()
    {
        var tempRoot = CreateTempProjectRoot();

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(tempRoot, "input.json"),
                """
                {
                  "Actions": [
                    { "Type": "Button", "Name": "Jump", "Buttons": [{ "KeyCode": 32 }] },
                    { "Type": "Button", "Name": "CustomDance", "Buttons": [] }
                  ]
                }
                """);

            var service = new ProjectInputManagerService();
            var choices = await service.ReadInputActionChoicesAsync(tempRoot);

            var jump = choices.Single(choice => choice.Name == "Jump" && choice.Type == VrsInputActionType.Button);
            var custom = choices.Single(choice => choice.Name == "CustomDance" && choice.Type == VrsInputActionType.Button);

            Assert.Equal("Project+Preset", jump.Source);
            Assert.Equal("Project", custom.Source);
            Assert.Equal("World/Hidden/VRS/Events/Input/Jump", jump.EventPath);
            Assert.Contains(choices, choice => choice.Name == "Interact" && choice.Source == "Preset");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SyncForDeploy_DoesNotRemoveInputManagerPresetActions()
    {
        var tempRoot = CreateTempProjectRoot();

        try
        {
            var service = new ProjectInputManagerService();
            await service.EnsurePresetActionsAsync(tempRoot);

            var cleanup = await service.SyncForDeployAsync(
                tempRoot,
                new Rule { Id = "RULE_Empty", Name = "Input Rule", ScriptKind = GraphScriptKind.Local },
                "scripts/VRS/client/VRS Input Rule.client.luau");

            Assert.True(cleanup.Succeeded);
            Assert.Equal(0, cleanup.RemovedCount);

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
    public async Task SyncForDeploy_PreservesManualActionsAndCreatesMissingVrsAction()
    {
        var tempRoot = CreateTempProjectRoot();

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(tempRoot, "input.json"),
                """
                {
                  "Actions": [
                    {
                      "Type": "Axis",
                      "Name": "Horizontal",
                      "Negative": [{ "KeyCode": 65 }],
                      "Positive": [{ "KeyCode": 68 }]
                    }
                  ]
                }
                """);

            var service = new ProjectInputManagerService();
            var rule = RuleWithNodes(
                InputNode("PROP_Axis", "InputAxisValue", "Horizontal"),
                InputNode("TRIG_Button", "OnInputButtonDown", "Interact"));

            var result = await service.SyncForDeployAsync(
                tempRoot,
                rule,
                "scripts/VRS/client/VRS Input Rule.client.luau");

            Assert.True(result.Succeeded);
            Assert.Equal(2, result.EnsuredCount);
            Assert.Equal(1, result.CreatedCount);
            Assert.Equal(1, result.ExistingManualCount);

            using var inputDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(tempRoot, "input.json")));
            var actions = inputDocument.RootElement.GetProperty("Actions").EnumerateArray().ToList();

            Assert.Equal(2, actions.Count);
            var horizontal = actions.Single(action => action.GetProperty("Name").GetString() == "Horizontal");
            Assert.Equal("Axis", horizontal.GetProperty("Type").GetString());
            Assert.Single(horizontal.GetProperty("Negative").EnumerateArray());

            var interact = actions.Single(action => action.GetProperty("Name").GetString() == "Interact");
            Assert.Equal("Button", interact.GetProperty("Type").GetString());
            Assert.Equal(JsonValueKind.Array, interact.GetProperty("Buttons").ValueKind);

            var registry = await ReadRegistry(tempRoot);
            var record = Assert.Single(registry.Actions);

            Assert.Equal("Interact", record.Name);
            Assert.Equal(VrsInputActionType.Button, record.Type);
            Assert.Equal("TRIG_Button", Assert.Single(Assert.Single(record.Usages).NodeIds));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SyncForDeploy_PreservesManualTypeConflictsWithoutOverwriting()
    {
        var tempRoot = CreateTempProjectRoot();

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(tempRoot, "input.json"),
                """
                {
                  "Actions": [
                    { "Type": "Axis", "Name": "Interact", "Negative": [], "Positive": [] }
                  ]
                }
                """);

            var service = new ProjectInputManagerService();
            var result = await service.SyncForDeployAsync(
                tempRoot,
                RuleWithNodes(InputNode("TRIG_Button", "OnInputButtonDown", "Interact")),
                "scripts/VRS/client/VRS Input Rule.client.luau");

            Assert.True(result.Succeeded);
            Assert.Equal(1, result.ManualConflictCount);
            Assert.Equal(0, result.CreatedCount);
            Assert.False(File.Exists(Path.Combine(tempRoot, ".vrs", "input-manager.json")));

            using var inputDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(tempRoot, "input.json")));
            var action = Assert.Single(inputDocument.RootElement.GetProperty("Actions").EnumerateArray());

            Assert.Equal("Axis", action.GetProperty("Type").GetString());
            Assert.Equal("Interact", action.GetProperty("Name").GetString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SyncForDeploy_RemovesOnlyUnmodifiedOrphanedVrsActions()
    {
        var tempRoot = CreateTempProjectRoot();

        try
        {
            var service = new ProjectInputManagerService();
            var scriptPath = "scripts/VRS/client/VRS Input Rule.client.luau";

            var first = await service.SyncForDeployAsync(
                tempRoot,
                RuleWithNodes(InputNode("TRIG_Button", "OnInputButtonDown", "Interact")),
                scriptPath);

            Assert.True(first.Succeeded);
            Assert.Equal(1, first.CreatedCount);

            var second = await service.SyncForDeployAsync(
                tempRoot,
                new Rule { Id = "RULE_Empty", Name = "Input Rule", ScriptKind = GraphScriptKind.Local },
                scriptPath);

            Assert.True(second.Succeeded);
            Assert.Equal(1, second.RemovedCount);

            using var inputDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(tempRoot, "input.json")));
            Assert.Empty(inputDocument.RootElement.GetProperty("Actions").EnumerateArray());

            var registry = await ReadRegistry(tempRoot);
            Assert.Empty(registry.Actions);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SyncForDeploy_PreservesModifiedManagedActionDuringCleanup()
    {
        var tempRoot = CreateTempProjectRoot();

        try
        {
            var service = new ProjectInputManagerService();
            var scriptPath = "scripts/VRS/client/VRS Input Rule.client.luau";

            await service.SyncForDeployAsync(
                tempRoot,
                RuleWithNodes(InputNode("TRIG_Button", "OnInputButtonDown", "Interact")),
                scriptPath);

            await File.WriteAllTextAsync(
                Path.Combine(tempRoot, "input.json"),
                """
                {
                  "Actions": [
                    {
                      "Type": "Button",
                      "Name": "Interact",
                      "Buttons": [{ "KeyCode": 69 }]
                    }
                  ]
                }
                """);

            var cleanup = await service.SyncForDeployAsync(
                tempRoot,
                new Rule { Id = "RULE_Empty", Name = "Input Rule", ScriptKind = GraphScriptKind.Local },
                scriptPath);

            Assert.True(cleanup.Succeeded);
            Assert.Equal(0, cleanup.RemovedCount);
            Assert.Equal(1, cleanup.ModifiedManagedPreservedCount);

            using var inputDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(tempRoot, "input.json")));
            var action = Assert.Single(inputDocument.RootElement.GetProperty("Actions").EnumerateArray());

            Assert.Equal("Interact", action.GetProperty("Name").GetString());
            Assert.Single(action.GetProperty("Buttons").EnumerateArray());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void BuildGenerationPlan_DetectsGraphActionNameTypeConflicts()
    {
        var service = new ProjectInputManagerService();

        var plan = service.BuildGenerationPlan(RuleWithNodes(
            InputNode("TRIG_Button", "OnInputButtonDown", "Move"),
            InputNode("PROP_Axis", "InputAxisValue", "Move")));

        Assert.True(plan.HasConflicts);
        Assert.Contains("Move", plan.Conflicts[0], StringComparison.Ordinal);
    }

    private static string CreateTempProjectRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vrs-input-manager-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(Path.Combine(tempRoot, "project.ptproj"), "{}");
        return tempRoot;
    }

    private static async Task<VrsManagedInputRegistry> ReadRegistry(string projectRoot)
    {
        var json = await File.ReadAllTextAsync(Path.Combine(projectRoot, ".vrs", "input-manager.json"));
        return JsonSerializer.Deserialize(json, VrsJsonContext.Default.VrsManagedInputRegistry)!;
    }

    private static Rule RuleWithNodes(params RuleNode[] nodes)
    {
        return new Rule
        {
            Id = "RULE_Input",
            Name = "Input Rule",
            ScriptKind = GraphScriptKind.Local,
            Nodes = nodes.ToList()
        };
    }

    private static RuleNode InputNode(string id, string type, string actionName)
    {
        return new RuleNode
        {
            Id = id,
            Type = type,
            Kind = type.StartsWith("On", StringComparison.OrdinalIgnoreCase) ? NodeKind.Trigger : NodeKind.Property,
            Value = $"actionName={actionName}",
            Parameters =
            [
                new RuleParameter
                {
                    Key = "actionName",
                    Value = actionName,
                    Binding = new GraphValueBinding
                    {
                        SourceKind = GraphValueSourceKind.Constant,
                        ConstantValue = actionName
                    }
                }
            ]
        };
    }
}
