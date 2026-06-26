using System.Text.Json;
using Vrs.Core.Bridge;
using Vrs.Core.Persistence;
using Vrs.Graph.Model;

namespace Vrs.Core.ProjectInputs;

/// <summary>
/// Synchronizes Polytoria input actions that VRS can prove it owns. Manual
/// actions and user-modified generated actions are preserved by design.
/// </summary>
public sealed partial class ProjectInputManagerService
{
    public const string RegistryRelativePath = ".vrs/input-manager.json";
    public const string ManagerFolderPath = "World/Hidden/VRS Input Manager";

    public VrsInputGenerationPlan BuildGenerationPlan(Rule rule)
    {
        return VrsInputGenerationPlanner.Build(rule);
    }

    public async Task<VrsInputSyncResult> SyncForDeployAsync(
        string projectRoot,
        Rule rule,
        string projectRelativeScriptPath,
        CancellationToken cancellationToken = default)
    {
        var plan = BuildGenerationPlan(rule);
        if (plan.HasConflicts)
        {
            return Failed(plan.Conflicts, plan.Conflicts[0], hasInputNodes: plan.HasInputActions);
        }

        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return Failed([], "Project root is required.", hasInputNodes: plan.HasInputActions);
        }

        var normalizedRoot = Path.GetFullPath(projectRoot);
        if (!Directory.Exists(normalizedRoot))
        {
            return Failed([], $"Project root does not exist: {normalizedRoot}", hasInputNodes: plan.HasInputActions);
        }

        var registryPath = Path.Combine(normalizedRoot, RegistryRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var registryExists = File.Exists(registryPath);
        if (!plan.HasInputActions && !registryExists)
        {
            return VrsInputSyncResult.Empty;
        }

        try
        {
            var registry = await ReadRegistryAsync(registryPath, cancellationToken).ConfigureAwait(false);
            var registryChanged = PruneMissingScriptUsages(registry, normalizedRoot);
            registryChanged |= RemoveCurrentScriptUsage(registry, projectRelativeScriptPath);

            var inputPath = Path.Combine(normalizedRoot, "input.json");
            var inputDocument = await ReadInputDocumentAsync(inputPath, plan.HasInputActions || registry.Actions.Count > 0, cancellationToken).ConfigureAwait(false);
            var actions = GetOrCreateActionsArray(inputDocument);

            var ensured = 0;
            var created = 0;
            var removed = 0;
            var manualConflicts = 0;
            var existingManual = 0;
            var modifiedManagedPreserved = 0;
            var inputChanged = false;
            var now = DateTimeOffset.UtcNow.ToString("O");

            foreach (var requiredAction in plan.RequiredActions)
            {
                var existingAction = FindAction(actions, requiredAction.Name);
                var registryRecord = FindRegistryRecord(registry, requiredAction.Name, requiredAction.Type);
                if (existingAction is null)
                {
                    var generatedAction = CreateGeneratedAction(requiredAction);
                    actions.Add(generatedAction);
                    var generatedHash = HashAction(generatedAction);
                    UpsertRegistryRecord(registry, requiredAction, generatedHash, projectRelativeScriptPath, rule, now);
                    ensured++;
                    created++;
                    inputChanged = true;
                    registryChanged = true;
                    continue;
                }

                if (!TryReadActionType(existingAction, out var existingType) || existingType != requiredAction.Type)
                {
                    manualConflicts++;
                    continue;
                }

                if (registryRecord is null)
                {
                    existingManual++;
                    ensured++;
                    continue;
                }

                AddOrUpdateUsage(registryRecord, requiredAction, projectRelativeScriptPath, rule, now);
                registryRecord.UpdatedAtUtc = now;
                registryChanged = true;
                ensured++;
                if (!HashAction(existingAction).Equals(registryRecord.GeneratedHash, StringComparison.Ordinal))
                {
                    modifiedManagedPreserved++;
                }
            }

            foreach (var record in registry.Actions.ToList())
            {
                if (record.Usages.Count > 0)
                {
                    continue;
                }

                if (IsPresetRegistryRecord(record))
                {
                    continue;
                }

                var existingAction = FindAction(actions, record.Name);
                if (existingAction is null)
                {
                    registry.Actions.Remove(record);
                    registryChanged = true;
                    continue;
                }

                if (!TryReadActionType(existingAction, out var existingType) || existingType != record.Type)
                {
                    modifiedManagedPreserved++;
                    continue;
                }

                if (!HashAction(existingAction).Equals(record.GeneratedHash, StringComparison.Ordinal))
                {
                    modifiedManagedPreserved++;
                    continue;
                }

                RemoveAction(actions, existingAction);
                registry.Actions.Remove(record);
                removed++;
                inputChanged = true;
                registryChanged = true;
            }

            if (inputChanged)
            {
                await BridgeFileService.WriteTextFileByReplaceAsync(
                    inputPath,
                    inputDocument.ToJsonString(PrettyJsonOptions),
                    cancellationToken).ConfigureAwait(false);
            }

            if (registryChanged || created > 0 || removed > 0)
            {
                await BridgeFileService.WriteTextFileByReplaceAsync(
                    registryPath,
                    JsonSerializer.Serialize(registry, VrsJsonContext.Default.VrsManagedInputRegistry),
                    cancellationToken).ConfigureAwait(false);
            }

            return new VrsInputSyncResult(
                Succeeded: true,
                HasInputNodes: plan.HasInputActions,
                EnsuredCount: ensured,
                CreatedCount: created,
                RemovedCount: removed,
                ManualConflictCount: manualConflicts,
                ExistingManualCount: existingManual,
                ModifiedManagedPreservedCount: modifiedManagedPreserved,
                Conflicts: [],
                Error: "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return Failed([], ex.Message, hasInputNodes: plan.HasInputActions);
        }
    }

    public async Task<VrsInputPresetSyncResult> EnsurePresetActionsAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return PresetFailed([], "Project root is required.");
        }

        var normalizedRoot = Path.GetFullPath(projectRoot);
        if (!Directory.Exists(normalizedRoot))
        {
            return PresetFailed([], $"Project root does not exist: {normalizedRoot}");
        }

        try
        {
            var registryPath = Path.Combine(normalizedRoot, RegistryRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var registry = await ReadRegistryAsync(registryPath, cancellationToken).ConfigureAwait(false);
            var inputPath = Path.Combine(normalizedRoot, "input.json");
            var inputDocument = await ReadInputDocumentAsync(inputPath, shouldCreate: true, cancellationToken).ConfigureAwait(false);
            var actions = GetOrCreateActionsArray(inputDocument);
            var now = DateTimeOffset.UtcNow.ToString("O");
            var ensured = 0;
            var created = 0;
            var existingManual = 0;
            var manualConflicts = 0;
            var modifiedManagedPreserved = 0;
            var conflicts = new List<string>();
            var inputChanged = false;
            var registryChanged = false;

            foreach (var preset in VrsInputPresetCatalog.All)
            {
                var existingAction = FindAction(actions, preset.Name);
                if (existingAction is null)
                {
                    var generatedAction = CreateGeneratedAction(preset);
                    actions.Add(generatedAction);
                    UpsertPresetRegistryRecord(registry, preset, HashAction(generatedAction), now);
                    ensured++;
                    created++;
                    inputChanged = true;
                    registryChanged = true;
                    continue;
                }

                if (!TryReadActionType(existingAction, out var existingType) || existingType != preset.Type)
                {
                    manualConflicts++;
                    conflicts.Add($"{preset.Name} already exists as a different input type.");
                    continue;
                }

                var generatedPreset = CreateGeneratedAction(preset);
                var presetHash = HashAction(generatedPreset);
                var registryRecord = FindRegistryRecord(registry, preset.Name, preset.Type);
                var wasPresetRecord = registryRecord is not null && IsPresetRegistryRecord(registryRecord);
                if (registryRecord is null)
                {
                    existingManual++;
                    UpsertPresetRegistryRecord(registry, preset, HashAction(existingAction), now);
                    registryChanged = true;
                    ensured++;
                    continue;
                }

                UpsertPresetRegistryRecord(registry, preset, wasPresetRecord ? presetHash : HashAction(existingAction), now);
                registryChanged = true;
                ensured++;
                if (wasPresetRecord && !HashAction(existingAction).Equals(presetHash, StringComparison.Ordinal))
                {
                    modifiedManagedPreserved++;
                }
            }

            if (inputChanged)
            {
                await BridgeFileService.WriteTextFileByReplaceAsync(
                    inputPath,
                    inputDocument.ToJsonString(PrettyJsonOptions),
                    cancellationToken).ConfigureAwait(false);
            }

            if (registryChanged)
            {
                await BridgeFileService.WriteTextFileByReplaceAsync(
                    registryPath,
                    JsonSerializer.Serialize(registry, VrsJsonContext.Default.VrsManagedInputRegistry),
                    cancellationToken).ConfigureAwait(false);
            }

            return new VrsInputPresetSyncResult(
                Succeeded: true,
                EnsuredCount: ensured,
                CreatedCount: created,
                ExistingManualCount: existingManual,
                ManualConflictCount: manualConflicts,
                ModifiedManagedPreservedCount: modifiedManagedPreserved,
                Conflicts: conflicts,
                Error: "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return PresetFailed([], ex.Message);
        }
    }

    public async Task<IReadOnlyList<VrsInputActionChoice>> ReadInputActionChoicesAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return VrsInputPresetCatalog.DefaultChoices;
        }

        var inputPath = Path.Combine(Path.GetFullPath(projectRoot), "input.json");
        try
        {
            var inputDocument = await ReadInputDocumentAsync(inputPath, shouldCreate: false, cancellationToken).ConfigureAwait(false);
            return ReadInputActionChoices(inputDocument);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return VrsInputPresetCatalog.DefaultChoices;
        }
    }

    private static VrsInputSyncResult Failed(IReadOnlyList<string> conflicts, string error, bool hasInputNodes)
    {
        return new VrsInputSyncResult(
            Succeeded: false,
            HasInputNodes: hasInputNodes,
            EnsuredCount: 0,
            CreatedCount: 0,
            RemovedCount: 0,
            ManualConflictCount: 0,
            ExistingManualCount: 0,
            ModifiedManagedPreservedCount: 0,
            Conflicts: conflicts,
            Error: error);
    }

    private static VrsInputPresetSyncResult PresetFailed(IReadOnlyList<string> conflicts, string error)
    {
        return new VrsInputPresetSyncResult(
            Succeeded: false,
            EnsuredCount: 0,
            CreatedCount: 0,
            ExistingManualCount: 0,
            ManualConflictCount: 0,
            ModifiedManagedPreservedCount: 0,
            Conflicts: conflicts,
            Error: error);
    }
}
