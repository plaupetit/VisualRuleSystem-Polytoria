namespace Vrs.Core.ProjectInputs;

public enum VrsInputActionType
{
    Button,
    Axis,
    Vector2
}

public sealed class VrsInputActionDefinition
{
    public string Name { get; init; } = "";
    public VrsInputActionType Type { get; init; }
    public List<string> NodeIds { get; init; } = [];
}

public sealed record VrsInputActionChoice(
    string Name,
    VrsInputActionType Type,
    string Source,
    string EventName,
    string EventPath);

public sealed record VrsInputPreset(
    string Name,
    VrsInputActionType Type,
    IReadOnlyList<int> Buttons,
    IReadOnlyList<int> Negative,
    IReadOnlyList<int> Positive,
    IReadOnlyList<int> Up,
    IReadOnlyList<int> Down,
    IReadOnlyList<int> Left,
    IReadOnlyList<int> Right);

public sealed record VrsInputGenerationPlan(
    IReadOnlyList<VrsInputActionDefinition> RequiredActions,
    IReadOnlyList<string> Conflicts)
{
    public bool HasInputActions => RequiredActions.Count > 0;
    public bool HasConflicts => Conflicts.Count > 0;
}

public sealed class VrsManagedInputRegistry
{
    public string Format { get; set; } = "visual-rule-system-input-manager";
    public int Version { get; set; } = 1;
    public List<VrsManagedInputRecord> Actions { get; set; } = [];
}

public sealed class VrsManagedInputRecord
{
    public string Name { get; set; } = "";
    public VrsInputActionType Type { get; set; }
    public string Source { get; set; } = "ScriptUsage";
    public string GeneratedHash { get; set; } = "";
    public string CreatedAtUtc { get; set; } = "";
    public string UpdatedAtUtc { get; set; } = "";
    public List<VrsManagedInputUsage> Usages { get; set; } = [];
}

public sealed class VrsManagedInputUsage
{
    public string ScriptPath { get; set; } = "";
    public string RuleId { get; set; } = "";
    public string RuleName { get; set; } = "";
    public List<string> NodeIds { get; set; } = [];
    public string UpdatedAtUtc { get; set; } = "";
}

public sealed record VrsInputSyncResult(
    bool Succeeded,
    bool HasInputNodes,
    int EnsuredCount,
    int CreatedCount,
    int RemovedCount,
    int ManualConflictCount,
    int ExistingManualCount,
    int ModifiedManagedPreservedCount,
    IReadOnlyList<string> Conflicts,
    string Error)
{
    public static VrsInputSyncResult Empty { get; } = new(
        Succeeded: true,
        HasInputNodes: false,
        EnsuredCount: 0,
        CreatedCount: 0,
        RemovedCount: 0,
        ManualConflictCount: 0,
        ExistingManualCount: 0,
        ModifiedManagedPreservedCount: 0,
        Conflicts: [],
        Error: "");

    public bool ShouldEnsureManagerFolder => Succeeded && HasInputNodes;

    public bool HasStatus =>
        HasInputNodes ||
        EnsuredCount > 0 ||
        CreatedCount > 0 ||
        RemovedCount > 0 ||
        ManualConflictCount > 0 ||
        ExistingManualCount > 0 ||
        ModifiedManagedPreservedCount > 0;

    public string FormatStatusFragment()
    {
        if (!Succeeded)
        {
            return string.IsNullOrWhiteSpace(Error)
                ? "Input Manager: failed"
                : $"Input Manager: failed - {Error}";
        }

        if (!HasStatus)
        {
            return "Input Manager: no input actions needed";
        }

        var parts = new List<string>();
        if (EnsuredCount > 0)
        {
            parts.Add($"{EnsuredCount} ensured");
        }

        if (RemovedCount > 0)
        {
            parts.Add($"{RemovedCount} removed");
        }

        if (ManualConflictCount > 0)
        {
            parts.Add($"{ManualConflictCount} manual conflict preserved");
        }

        if (ExistingManualCount > 0)
        {
            parts.Add($"{ExistingManualCount} manual action reused");
        }

        if (ModifiedManagedPreservedCount > 0)
        {
            parts.Add($"{ModifiedManagedPreservedCount} modified VRS action preserved");
        }

        return $"Input Manager: {string.Join(", ", parts)}";
    }
}

public sealed record VrsInputPresetSyncResult(
    bool Succeeded,
    int EnsuredCount,
    int CreatedCount,
    int ExistingManualCount,
    int ManualConflictCount,
    int ModifiedManagedPreservedCount,
    IReadOnlyList<string> Conflicts,
    string Error)
{
    public string FormatStatusFragment()
    {
        if (!Succeeded)
        {
            return string.IsNullOrWhiteSpace(Error)
                ? "Input Manager: failed"
                : $"Input Manager: failed - {Error}";
        }

        var parts = new List<string>
        {
            $"{EnsuredCount} presets ready"
        };

        if (CreatedCount > 0)
        {
            parts.Add($"{CreatedCount} created");
        }

        if (ExistingManualCount > 0)
        {
            parts.Add($"{ExistingManualCount} existing preserved");
        }

        if (ManualConflictCount > 0)
        {
            parts.Add($"{ManualConflictCount} conflict preserved");
        }

        if (ModifiedManagedPreservedCount > 0)
        {
            parts.Add($"{ModifiedManagedPreservedCount} modified preserved");
        }

        return $"Input Manager: {string.Join(", ", parts)}";
    }
}
