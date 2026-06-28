using Vrs.Graph.Model;

namespace Vrs.Core.Bridge;

/// <summary>
/// Persisted pointer to the Polytoria project currently linked to the editor.
/// </summary>
/// <remarks>
/// This is app-owned local state, not a Creator command. The bridge and deploy
/// workflows use the root to resolve scripts, metadata files, and pending commands.
/// </remarks>
public sealed class ActivePolytoriaProjectConfig
{
    public string Format { get; set; } = "visual-rule-system-active-polytoria-project";
    public int Version { get; set; } = 1;
    public string ProjectRoot { get; set; } = "";
}

/// <summary>
/// Legacy flat scene snapshot shape produced by older bridge flows.
/// </summary>
/// <remarks>
/// Newer Creator snapshots can be hierarchical; <see cref="SceneSnapshotReader"/>
/// normalizes both shapes into <see cref="SceneSnapshotReadResult"/>.
/// </remarks>
public sealed class SceneSnapshot
{
    public string Format { get; set; } = "visual-programming-bridge-scene-snapshot";
    public int Version { get; set; } = 1;
    public string CreatedAtUtc { get; set; } = "";
    public List<SceneObject> Objects { get; set; } = [];
}

/// <summary>
/// Bounds and filters used while reading Creator snapshots into the hierarchy UI.
/// </summary>
/// <remarks>
/// Limits are clamped by the reader so a malformed or huge snapshot cannot make
/// the editor enumerate unbounded scene data.
/// </remarks>
public sealed class SceneSnapshotReadOptions
{
    /// <summary>Maximum number of displayed objects after filtering.</summary>
    public int MaxObjects { get; set; } = 250;

    /// <summary>Maximum hierarchy depth to keep, where root objects start at depth zero.</summary>
    public int MaxDepth { get; set; } = 5;

    /// <summary>Case-insensitive text filter matched against names, types, and paths.</summary>
    public string Search { get; set; } = "";

    /// <summary>Includes bridge-owned helper objects that are hidden by default.</summary>
    public bool IncludeBridgeTrash { get; set; }

    /// <summary>Stops parsing display objects once <see cref="MaxObjects"/> has been reached.</summary>
    public bool StopAfterDisplayLimit { get; set; } = true;
}

/// <summary>
/// Normalized Creator snapshot plus counters explaining what the bounded reader skipped.
/// </summary>
public sealed class SceneSnapshotReadResult
{
    public string Format { get; set; } = "";
    public int Version { get; set; }
    public string CreatedAtUtc { get; set; } = "";
    public long FileBytes { get; set; }
    public int SnapshotVersion { get; set; }
    public string RuntimeVersion { get; set; } = "";
    public int DiagnosticObjectCount { get; set; }
    public int DiagnosticWatcherCount { get; set; }
    public int DiagnosticTruncatedCount { get; set; }
    public int DiagnosticMaxObservedDepth { get; set; }
    public int ObservedObjects { get; set; }
    public int SkippedByDisplayLimit { get; set; }
    public int SkippedByDepth { get; set; }
    public int SkippedBySearch { get; set; }
    public int SkippedByBridgeTrash { get; set; }
    public int PrunedSubtrees { get; set; }
    public List<SceneObject> Objects { get; set; } = [];

    /// <summary>True when the result is intentionally incomplete because a filter or limit removed data.</summary>
    public bool WasLimited =>
        SkippedByDisplayLimit > 0 ||
        SkippedByDepth > 0 ||
        SkippedBySearch > 0 ||
        SkippedByBridgeTrash > 0 ||
        PrunedSubtrees > 0;
}

/// <summary>
/// File contract written by the editor for the Creator bridge to consume.
/// </summary>
/// <remarks>
/// One envelope can carry multiple commands that share one command id and dry-run
/// mode, making it possible for Creator to report a single result batch.
/// </remarks>
public sealed class BridgeCommandEnvelope
{
    public string Format { get; set; } = "visual-programming-bridge-pending-commands";
    public int Version { get; set; } = 1;
    public string CommandId { get; set; } = "";
    public string CreatedAtUtc { get; set; } = "";
    public bool DryRun { get; set; } = true;
    public List<BridgeCommand> Commands { get; set; } = [];
}

/// <summary>
/// Single bridge operation understood by the Creator addon.
/// </summary>
/// <remarks>
/// The editor writes project files first for deploy flows, then sends an
/// <c>upsert_script</c> command with <see cref="LinkedFilePath"/> and
/// <see cref="FileAlreadyWritten"/> so Creator can link the hierarchy object to
/// the already-saved source file.
/// </remarks>
public sealed class BridgeCommand
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string ParentPath { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Folder";
    public bool DryRun { get; set; } = true;
    public string ScriptKind { get; set; } = "";
    public string Content { get; set; } = "";
    public string Source { get; set; } = "";
    public string LinkedFilePath { get; set; } = "";
    public bool FileAlreadyWritten { get; set; }
    public string Path { get; set; } = "";

    /// <summary>
    /// Tells Creator to use <see cref="ParentPath"/> exactly instead of moving
    /// scripts to a default service root such as ScriptService.
    /// </summary>
    public bool ExactParent { get; set; }
    public string TargetPath { get; set; } = "";
}

/// <summary>
/// Result of writing or updating a typed Luau script file and its companion metadata file.
/// </summary>
public sealed class LinkedScriptFileWriteResult
{
    public string ScriptPath { get; set; } = "";
    public string ProjectRelativeScriptPath { get; set; } = "";
    public string MetaPath { get; set; } = "";
    public bool ScriptAlreadyExisted { get; set; }
    public bool MetaCreated { get; set; }
}

/// <summary>
/// Result batch written by the Creator bridge after it processes a command envelope.
/// </summary>
public sealed class CommandResults
{
    public string Format { get; set; } = "visual-programming-bridge-command-results";
    public int Version { get; set; } = 1;
    public string CommandId { get; set; } = "";
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string CreatedAtUtc { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string RuntimeVersion { get; set; } = "";
    public List<CommandResultEntry> Results { get; set; } = [];
}

/// <summary>
/// Single Creator-side command result from command-results.json.
/// </summary>
public sealed class CommandResultEntry
{
    public string Id { get; set; } = "";
    public string Action { get; set; } = "";
    public bool Ok { get; set; }
    public bool Skipped { get; set; }
    public int Index { get; set; }
    public string Path { get; set; } = "";
    public string Message { get; set; } = "";
    public string HandledAtUtc { get; set; } = "";
    public CommandResultDetails Details { get; set; } = new();
}

/// <summary>
/// Known structured details emitted by the Creator addon for script upserts.
/// </summary>
public sealed class CommandResultDetails
{
    public bool Created { get; set; }
    public string LinkedFilePath { get; set; } = "";
    public string ScriptKind { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public string SourceWarning { get; set; } = "";
    public string LinkRegistryWarning { get; set; } = "";
}

/// <summary>
/// Latest Creator bridge health state read by the editor status service.
/// </summary>
public sealed class BridgeStatus
{
    public string Format { get; set; } = "visual-programming-bridge-status";
    public int Version { get; set; } = 1;
    public string State { get; set; } = "unknown";
    public string Message { get; set; } = "";
    public string UpdatedAtUtc { get; set; } = "";
}

/// <summary>
/// Small editor state summary published for Creator-side diagnostics.
/// </summary>
/// <remarks>
/// The Creator addon reads this as status context only. Scene mutations still
/// flow through explicit bridge commands handled by Creator.
/// </remarks>
public sealed class BridgeAppState
{
    public string Format { get; set; } = "visual-programming-bridge-app-state";
    public int Version { get; set; } = 1;
    public string App { get; set; } = "VisualRuleSystem";
    public string SessionId { get; set; } = "";
    public bool Focused { get; set; }
    public int ProcessId { get; set; }
    public string UpdatedAtUtc { get; set; } = "";
    public string ActiveProjectName { get; set; } = "";
    public string ProjectUiMode { get; set; } = "";
    public bool ProjectLinked { get; set; }
    public bool CreatorReady { get; set; }
    public string ScriptKind { get; set; } = "";
    public string AuthorScriptName { get; set; } = "";
    public string CreatorScriptName { get; set; } = "";
    public string ProjectRelativeScriptPath { get; set; } = "";
    public string CreatorObjectPath { get; set; } = "";
    public string SelectedCreatorObjectPath { get; set; } = "";
    public string DeployParentPath { get; set; } = "";
    public int NodeCount { get; set; }
    public int ValidationMessageCount { get; set; }
    public int ValidationErrorCount { get; set; }
    public int ValidationWarningCount { get; set; }
    public string BridgeBeatText { get; set; } = "";
    public string BridgeBeatDetail { get; set; } = "";
    public string SnapshotStatus { get; set; } = "";
    public string StatusText { get; set; } = "";
    public string LuauPreviewSummary { get; set; } = "";
    public List<string> RecentLogs { get; set; } = [];
}

/// <summary>
/// Snapshot request written by the editor and handled by the Creator addon.
/// </summary>
public sealed class SnapshotRequest
{
    public string Format { get; set; } = "visual-programming-bridge-snapshot-request";
    public int Version { get; set; } = 1;
    public string RequestId { get; set; } = "";
    public string CreatedAtUtc { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Mode { get; set; } = "full";
    public string SessionId { get; set; } = "";
}

/// <summary>
/// Heartbeat written by the editor so bridge-side tools can detect the active VRS session.
/// </summary>
public sealed class AppHeartbeat
{
    public string Format { get; set; } = "visual-programming-bridge-app-heartbeat";
    public int Version { get; set; } = 1;
    public string App { get; set; } = "VisualRuleSystem";
    public string SessionId { get; set; } = "";
    public bool Active { get; set; }
    public bool Focused { get; set; }
    public int ProcessId { get; set; }
    public string UpdatedAtUtc { get; set; } = "";
    public long UpdatedAtUnixSeconds { get; set; }
    public long ExpiresAtUnixSeconds { get; set; }
}
