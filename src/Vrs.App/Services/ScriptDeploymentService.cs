using System.Text.Json;
using Vrs.Core.Bridge;
using Vrs.Core.ProjectInputs;
using Vrs.Graph.Model;

namespace Vrs.App.Services;

/// <summary>
/// Coordinates project-file deployment and Creator bridge commands while
/// leaving the view model responsible only for UI state and status mapping.
/// </summary>
public sealed class ScriptDeploymentService
{
    private const string VisualScriptNamePrefix = "VRS ";
    private readonly BridgeFileService bridge;
    private readonly ProjectInputManagerService inputManager;

    public ScriptDeploymentService(BridgeFileService bridge, ProjectInputManagerService? inputManager = null)
    {
        this.bridge = bridge;
        this.inputManager = inputManager ?? new ProjectInputManagerService();
    }

    public string ResolveScriptName(string requestedScriptName, Rule? rule)
    {
        var scriptName = "";
        if (!string.IsNullOrWhiteSpace(requestedScriptName))
        {
            scriptName = requestedScriptName.Trim();
        }
        else
        {
            scriptName = string.IsNullOrWhiteSpace(rule?.Name) ? "VisualRuleScript" : rule.Name.Trim();
        }

        return ToVisualScriptName(scriptName);
    }

    public bool IsSavedDeployScriptTarget(ScriptDeploymentTargetQuery query)
    {
        var scriptName = ToVisualScriptName(query.ScriptName);
        var expectedLinkedPath = NormalizeProjectPath(BridgeFileService.LinkedScriptProjectRelativePath(scriptName, query.ScriptKind));
        var targetPath = CombineBridgePath(query.ParentPath, scriptName);

        if (query.SceneObjects.Any(item =>
            item.Path.Equals(targetPath, StringComparison.OrdinalIgnoreCase) &&
            NormalizeProjectPath(item.LinkedScriptPath).Equals(expectedLinkedPath, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return LinkRegistryContainsTarget(bridge.ResolveBridgeDirectory(query.ProjectRoot), targetPath, expectedLinkedPath);
    }

    public SavedScriptFileStatus GetSavedScriptFileStatus(
        string? projectRoot,
        string scriptName,
        GraphScriptKind scriptKind)
    {
        var visualScriptName = ToVisualScriptName(scriptName);
        var projectRelativePath = BridgeFileService.LinkedScriptProjectRelativePath(visualScriptName, scriptKind);
        return GetSavedScriptFileStatus(projectRoot, projectRelativePath);
    }

    public static SavedScriptFileStatus GetSavedScriptFileStatus(string? projectRoot, string projectRelativePath)
    {
        if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(projectRelativePath))
        {
            return new SavedScriptFileStatus(
                ProjectRelativePath: projectRelativePath,
                AbsolutePath: "",
                Exists: false,
                IsEmpty: false,
                IsReady: false,
                Length: 0,
                StatusText: "File missing");
        }

        var scriptPath = Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(scriptPath))
        {
            return new SavedScriptFileStatus(
                ProjectRelativePath: projectRelativePath,
                AbsolutePath: scriptPath,
                Exists: false,
                IsEmpty: false,
                IsReady: false,
                Length: 0,
                StatusText: "File missing");
        }

        var length = new FileInfo(scriptPath).Length;
        return new SavedScriptFileStatus(
            ProjectRelativePath: projectRelativePath,
            AbsolutePath: scriptPath,
            Exists: true,
            IsEmpty: length == 0,
            IsReady: length > 0,
            Length: length,
            StatusText: length == 0 ? "File empty" : "File ready");
    }

    public async Task<ScriptProjectFileDeploymentResult> DeployProjectScriptFileAsync(
        ScriptProjectFileDeploymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var bridgeDirectory = bridge.ResolveBridgeDirectory(request.ProjectRoot);
        var scriptName = ToVisualScriptName(request.ScriptName);
        var expectedLinkedPath = BridgeFileService.LinkedScriptProjectRelativePath(scriptName, request.ScriptKind);
        var inputPlan = inputManager.BuildGenerationPlan(request.Rule);
        if (inputPlan.HasConflicts)
        {
            return BlockedFileResult(
                bridgeDirectory,
                expectedLinkedPath,
                $"Cannot deploy script: {inputPlan.Conflicts[0]}");
        }

        if (inputPlan.HasInputActions && request.ScriptKind != GraphScriptKind.Local)
        {
            return BlockedFileResult(
                bridgeDirectory,
                expectedLinkedPath,
                "Cannot deploy script: Input nodes require a ClientScript. Change ScriptKind to Local before deploying.");
        }

        var inputSync = await inputManager.SyncForDeployAsync(
            request.ProjectRoot,
            request.Rule,
            expectedLinkedPath,
            cancellationToken).ConfigureAwait(false);
        if (!inputSync.Succeeded)
        {
            return BlockedFileResult(
                bridgeDirectory,
                expectedLinkedPath,
                inputSync.FormatStatusFragment());
        }

        Directory.CreateDirectory(bridgeDirectory);
        var linkedFile = await bridge.WriteLinkedScriptFileAsync(
            request.ProjectRoot,
            scriptName,
            request.ScriptKind,
            request.Content,
            cancellationToken).ConfigureAwait(false);

        await bridge.WriteHeartbeatAsync(
            bridgeDirectory,
            active: true,
            focused: request.IsVrsFocused,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var actionText = linkedFile.ScriptAlreadyExisted ? "Updated VRS script file" : "Deployed VRS script file";
        var inputText = inputSync.HasStatus ? $". {inputSync.FormatStatusFragment()}" : "";
        return new ScriptProjectFileDeploymentResult(
            BridgeDirectory: bridgeDirectory,
            ProjectRelativeScriptPath: linkedFile.ProjectRelativeScriptPath,
            ScriptAlreadyExisted: linkedFile.ScriptAlreadyExisted,
            MetaCreated: linkedFile.MetaCreated,
            Blocked: false,
            StatusText: $"{actionText}: {linkedFile.ProjectRelativeScriptPath}{inputText}");
    }

    public async Task<ScriptInstanceDeploymentResult> DeployScriptInstanceAsync(
        ScriptInstanceDeploymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var bridgeDirectory = bridge.ResolveBridgeDirectory(request.ProjectRoot);
        var scriptName = ToVisualScriptName(request.ScriptName);
        var fileStatus = GetSavedScriptFileStatus(request.ProjectRoot, scriptName, request.ScriptKind);
        var expectedLinkedPath = fileStatus.ProjectRelativePath;
        if (!fileStatus.IsReady)
        {
            return BlockedInstanceResult(
                bridgeDirectory,
                expectedLinkedPath,
                $"Deploy File first: {expectedLinkedPath}");
        }

        var linkedFileContent = await File.ReadAllTextAsync(fileStatus.AbsolutePath, cancellationToken).ConfigureAwait(false);

        Directory.CreateDirectory(bridgeDirectory);
        var wasExistingTarget = IsSavedDeployScriptTarget(new ScriptDeploymentTargetQuery(
            request.SceneObjects,
            request.ProjectRoot,
            request.ParentPath,
            scriptName,
            request.ScriptKind));

        // Instance deploy intentionally queues only the Creator hierarchy link.
        // The source file must already exist from Deploy File.
        var commandId = await bridge.QueueUpsertLinkedScriptAsync(
            bridgeDirectory,
            request.ParentPath,
            scriptName,
            request.ScriptKind,
            content: linkedFileContent,
            linkedFilePath: expectedLinkedPath,
            fileAlreadyWritten: true,
            exactParent: true,
            dryRun: request.DryRun,
            cancellationToken).ConfigureAwait(false);

        await bridge.WriteHeartbeatAsync(
            bridgeDirectory,
            active: true,
            focused: request.IsVrsFocused,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var actionText = wasExistingTarget ? "Queued saved script instance" : "Queued new script instance";
        var dryRunText = request.DryRun ? "Creator dry-run queued" : "Creator deploy queued";
        return new ScriptInstanceDeploymentResult(
            BridgeDirectory: bridgeDirectory,
            ProjectRelativeScriptPath: expectedLinkedPath,
            CommandId: commandId,
            WasExistingTarget: wasExistingTarget,
            Blocked: false,
            StatusText: $"{actionText}: {expectedLinkedPath}. {dryRunText}: {commandId}");
    }

    private static ScriptProjectFileDeploymentResult BlockedFileResult(string bridgeDirectory, string projectRelativeScriptPath, string statusText)
    {
        return new ScriptProjectFileDeploymentResult(
            BridgeDirectory: bridgeDirectory,
            ProjectRelativeScriptPath: projectRelativeScriptPath,
            ScriptAlreadyExisted: false,
            MetaCreated: false,
            Blocked: true,
            StatusText: statusText);
    }

    private static ScriptInstanceDeploymentResult BlockedInstanceResult(string bridgeDirectory, string projectRelativeScriptPath, string statusText)
    {
        return new ScriptInstanceDeploymentResult(
            BridgeDirectory: bridgeDirectory,
            ProjectRelativeScriptPath: projectRelativeScriptPath,
            CommandId: "",
            WasExistingTarget: false,
            Blocked: true,
            StatusText: statusText);
    }

    private static bool LinkRegistryContainsTarget(string bridgeDirectory, string targetPath, string expectedLinkedPath)
    {
        var registryPath = Path.Combine(bridgeDirectory, "script-links.json");
        if (!File.Exists(registryPath))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(registryPath));
            if (!document.RootElement.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var link in links.EnumerateObject())
            {
                var value = link.Value;
                if (value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var objectPath = ReadJsonString(value, "objectPath") ?? link.Name;
                var linkedFilePath = NormalizeProjectPath(ReadJsonString(value, "linkedFilePath") ?? "");
                if (objectPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase) &&
                    linkedFilePath.Equals(expectedLinkedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }

        return false;
    }

    private static string? ReadJsonString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string NormalizeProjectPath(string value)
    {
        return value.Replace('\\', '/').Trim();
    }

    private static string ToVisualScriptName(string scriptName)
    {
        var trimmed = string.IsNullOrWhiteSpace(scriptName) ? "VisualRuleScript" : scriptName.Trim();
        return trimmed.StartsWith(VisualScriptNamePrefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{VisualScriptNamePrefix}{trimmed}";
    }

    private static string CombineBridgePath(string parentPath, string name)
    {
        var parent = string.IsNullOrWhiteSpace(parentPath) ? "Root" : parentPath.Trim().TrimEnd('/', '\\');
        return parent.Equals("Root", StringComparison.OrdinalIgnoreCase)
            ? name.Trim()
            : $"{parent}/{name.Trim()}";
    }
}

/// <summary>
/// Read-only deploy target probe used to decide whether the UI should show a
/// new deploy or saved-script deploy action.
/// </summary>
public sealed record ScriptDeploymentTargetQuery(
    IReadOnlyList<SceneObject> SceneObjects,
    string ProjectRoot,
    string ParentPath,
    string ScriptName,
    GraphScriptKind ScriptKind);

/// <summary>
/// Single source of truth for the saved scripts/VRS file readiness used by
/// context menus, previews, and instance deploy blocking.
/// </summary>
public sealed record SavedScriptFileStatus(
    string ProjectRelativePath,
    string AbsolutePath,
    bool Exists,
    bool IsEmpty,
    bool IsReady,
    long Length,
    string StatusText);

/// <summary>
/// Intent to write the generated Luau file into scripts/VRS without mutating
/// the Creator hierarchy.
/// </summary>
public sealed record ScriptProjectFileDeploymentRequest(
    string ProjectRoot,
    string ScriptName,
    Rule Rule,
    GraphScriptKind ScriptKind,
    string Content,
    bool IsVrsFocused = false);

/// <summary>
/// File side effects produced by Deploy File, already formatted for the view
/// model status line.
/// </summary>
public sealed record ScriptProjectFileDeploymentResult(
    string BridgeDirectory,
    string ProjectRelativeScriptPath,
    bool ScriptAlreadyExisted,
    bool MetaCreated,
    bool Blocked,
    string StatusText);

/// <summary>
/// Intent to link an existing scripts/VRS file into the Creator hierarchy.
/// </summary>
public sealed record ScriptInstanceDeploymentRequest(
    IReadOnlyList<SceneObject> SceneObjects,
    string ProjectRoot,
    string ParentPath,
    string ScriptName,
    GraphScriptKind ScriptKind,
    bool DryRun,
    bool IsVrsFocused = false);

/// <summary>
/// Bridge command side effects produced by Deploy Instance, already formatted
/// for the view model status line.
/// </summary>
public sealed record ScriptInstanceDeploymentResult(
    string BridgeDirectory,
    string ProjectRelativeScriptPath,
    string CommandId,
    bool WasExistingTarget,
    bool Blocked,
    string StatusText);
