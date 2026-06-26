using System.Text.Json;
using Vrs.Core.Persistence;
using Vrs.Graph.Model;

namespace Vrs.Core.ProjectInputs;

public sealed partial class ProjectInputManagerService
{
    private static async Task<VrsManagedInputRegistry> ReadRegistryAsync(string registryPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(registryPath))
        {
            return new VrsManagedInputRegistry();
        }

        await using var stream = File.OpenRead(registryPath);
        return await JsonSerializer.DeserializeAsync(stream, VrsJsonContext.Default.VrsManagedInputRegistry, cancellationToken).ConfigureAwait(false)
            ?? new VrsManagedInputRegistry();
    }

    private static VrsManagedInputRecord? FindRegistryRecord(VrsManagedInputRegistry registry, string name, VrsInputActionType type)
    {
        return registry.Actions.FirstOrDefault(record =>
            record.Type == type &&
            record.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static void UpsertRegistryRecord(
        VrsManagedInputRegistry registry,
        VrsInputActionDefinition action,
        string generatedHash,
        string projectRelativeScriptPath,
        Rule rule,
        string now)
    {
        var record = FindRegistryRecord(registry, action.Name, action.Type);
        if (record is null)
        {
            record = new VrsManagedInputRecord
            {
                Name = action.Name,
                Type = action.Type,
                Source = "ScriptUsage",
                CreatedAtUtc = now
            };
            registry.Actions.Add(record);
        }

        if (string.IsNullOrWhiteSpace(record.Source))
        {
            record.Source = "ScriptUsage";
        }

        record.GeneratedHash = generatedHash;
        record.UpdatedAtUtc = now;
        AddOrUpdateUsage(record, action, projectRelativeScriptPath, rule, now);
    }

    private static void UpsertPresetRegistryRecord(
        VrsManagedInputRegistry registry,
        VrsInputPreset preset,
        string generatedHash,
        string now)
    {
        var record = FindRegistryRecord(registry, preset.Name, preset.Type);
        if (record is null)
        {
            record = new VrsManagedInputRecord
            {
                Name = preset.Name,
                Type = preset.Type,
                CreatedAtUtc = now
            };
            registry.Actions.Add(record);
        }

        record.Source = "Preset";
        record.GeneratedHash = generatedHash;
        record.UpdatedAtUtc = now;
    }

    private static bool IsPresetRegistryRecord(VrsManagedInputRecord record)
        => record.Source.Equals("Preset", StringComparison.OrdinalIgnoreCase);

    private static void AddOrUpdateUsage(
        VrsManagedInputRecord record,
        VrsInputActionDefinition action,
        string projectRelativeScriptPath,
        Rule rule,
        string now)
    {
        var normalizedScriptPath = NormalizeProjectPath(projectRelativeScriptPath);
        var usage = record.Usages.FirstOrDefault(item =>
            NormalizeProjectPath(item.ScriptPath).Equals(normalizedScriptPath, StringComparison.OrdinalIgnoreCase));

        if (usage is null)
        {
            usage = new VrsManagedInputUsage { ScriptPath = normalizedScriptPath };
            record.Usages.Add(usage);
        }

        usage.RuleId = rule.Id;
        usage.RuleName = rule.Name;
        usage.NodeIds = action.NodeIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        usage.UpdatedAtUtc = now;
    }

    private static bool RemoveCurrentScriptUsage(VrsManagedInputRegistry registry, string projectRelativeScriptPath)
    {
        var normalizedScriptPath = NormalizeProjectPath(projectRelativeScriptPath);
        if (string.IsNullOrWhiteSpace(normalizedScriptPath))
        {
            return false;
        }

        var changed = false;
        foreach (var record in registry.Actions)
        {
            var removed = record.Usages.RemoveAll(usage =>
                NormalizeProjectPath(usage.ScriptPath).Equals(normalizedScriptPath, StringComparison.OrdinalIgnoreCase));
            changed |= removed > 0;
        }

        return changed;
    }

    private static bool PruneMissingScriptUsages(VrsManagedInputRegistry registry, string projectRoot)
    {
        var changed = false;
        foreach (var record in registry.Actions)
        {
            var removed = record.Usages.RemoveAll(usage =>
            {
                var scriptPath = NormalizeProjectPath(usage.ScriptPath);
                if (string.IsNullOrWhiteSpace(scriptPath))
                {
                    return true;
                }

                var absolutePath = Path.Combine(projectRoot, scriptPath.Replace('/', Path.DirectorySeparatorChar));
                return !File.Exists(absolutePath);
            });
            changed |= removed > 0;
        }

        return changed;
    }

    private static string NormalizeProjectPath(string value)
    {
        return value.Replace('\\', '/').Trim();
    }
}
