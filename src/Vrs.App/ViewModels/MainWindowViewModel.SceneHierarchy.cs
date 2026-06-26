using Vrs.App.Services;
using Vrs.Core.Bridge;
using Vrs.Core.Persistence;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    // Scene hierarchy clicks are the primary deploy/load surface: left-click
    // selects a target and loads linked VRS scripts, right-click exposes actions.
    public async Task SelectSceneHierarchyItemAsync(SceneHierarchyItemViewModel item)
    {
        await SelectSceneHierarchyItemAsync(item, replaceCurrentGraph: true).ConfigureAwait(true);
    }

    public async Task SelectSceneHierarchyItemAsync(SceneHierarchyItemViewModel item, bool replaceCurrentGraph)
    {
        ApplySceneDeploymentTarget(item.SceneObject);
        if (item.IsScriptLike && await TryLoadGraphFromSceneScriptAsync(item.SceneObject, replaceCurrentGraph).ConfigureAwait(true))
        {
            return;
        }

        SetStatus(item.IsScriptLike
            ? $"Selected script target: {BridgeParentPath}/{BridgeScriptName}."
            : $"Selected deploy parent: {BridgeParentPath}.");
    }

    public async Task<bool> LoadGraphFromSceneScriptAsync(SceneHierarchyItemViewModel item)
    {
        return await LoadGraphFromSceneScriptAsync(item, replaceCurrentGraph: true).ConfigureAwait(true);
    }

    public async Task<bool> LoadGraphFromSceneScriptAsync(SceneHierarchyItemViewModel item, bool replaceCurrentGraph)
    {
        if (!replaceCurrentGraph && ShouldConfirmGraphLoadReplacement)
        {
            SetStatus("Load cancelled.");
            return false;
        }

        ApplySceneDeploymentTarget(item.SceneObject);
        if (!await TryLoadGraphFromSceneScriptAsync(item.SceneObject, replaceCurrentGraph).ConfigureAwait(true))
        {
            return false;
        }

        return true;
    }

    public async Task DeployScriptToSceneItemAsync(SceneHierarchyItemViewModel item, bool dryRun)
    {
        await DeployScriptToSceneItemAsync(
            item,
            GetDefaultDeployScriptKindForSceneItem(item),
            GetDefaultDeployScriptNameForSceneItem(item),
            dryRun).ConfigureAwait(true);
    }

    public async Task DeployScriptToSceneItemAsync(
        SceneHierarchyItemViewModel item,
        GraphScriptKind scriptKind,
        string scriptName,
        bool dryRun)
    {
        await DeployScriptInstanceToSceneItemAsync(item, scriptKind, scriptName, dryRun).ConfigureAwait(true);
    }

    public GraphScriptKind GetDefaultDeployScriptKindForSceneItem(SceneHierarchyItemViewModel item)
    {
        return item.IsScriptLike ? ScriptKindFromSceneObject(item.SceneObject) : SelectedScriptKind;
    }

    public string GetDefaultDeployScriptNameForSceneItem(SceneHierarchyItemViewModel item)
    {
        return item.IsScriptLike ? NormalizeDraftScriptName(SceneScriptName(item.SceneObject)) : DraftScriptName;
    }

    public string GetDeployParentPathForSceneItem(SceneHierarchyItemViewModel item)
    {
        return DeploymentParentPath(item.SceneObject);
    }

    public SavedScriptInstanceDeployPreview BuildSavedScriptInstanceDeployPreview(SceneHierarchyItemViewModel item)
    {
        var preview = BuildScriptDeployPreview(graph.Script.ScriptName, graph.Script.ScriptKind);
        var parentPath = GetDeployParentPathForSceneItem(item);
        var targetPath = string.IsNullOrWhiteSpace(parentPath)
            ? preview.CreatorScriptName
            : $"{parentPath.TrimEnd('/')}/{preview.CreatorScriptName}";
        var blockReason = !CanUseCreatorBridgeCommands
            ? "Creator bridge is not ready."
            : preview.ProjectFileReady
                ? ""
                : $"Deploy File first: {preview.ProjectRelativePath}";

        return new SavedScriptInstanceDeployPreview(
            preview.CreatorScriptName,
            preview.ScriptKind,
            preview.CreatorObjectKind,
            preview.ProjectRelativePath,
            preview.ProjectFileReady,
            preview.ProjectFileStatusText,
            parentPath,
            targetPath,
            string.IsNullOrWhiteSpace(blockReason),
            blockReason);
    }

    public ScriptDeployPreview BuildScriptDeployPreview(string scriptName, GraphScriptKind scriptKind)
    {
        var authorName = NormalizeDraftScriptName(scriptName);
        var creatorName = scriptDeployment.ResolveScriptName(authorName, graph.Rules.FirstOrDefault());
        var fileStatus = scriptDeployment.GetSavedScriptFileStatus(ActiveProjectRoot, creatorName, scriptKind);
        return new ScriptDeployPreview(
            authorName,
            creatorName,
            scriptKind,
            ScriptObjectKind(scriptKind),
            fileStatus.ProjectRelativePath,
            fileStatus.Exists,
            fileStatus.IsReady,
            fileStatus.StatusText);
    }

    private async Task<bool> TryLoadGraphFromSceneScriptAsync(SceneObject sceneObject, bool replaceCurrentGraph)
    {
        if (!replaceCurrentGraph && ShouldConfirmGraphLoadReplacement)
        {
            SetStatus("Load cancelled.");
            return false;
        }

        if (!TryResolveSceneScriptFile(sceneObject, out var scriptPath))
        {
            SetStatus("Cannot load this file as a VRS graph.");
            return false;
        }

        var result = await scriptGraphLoader.LoadAsync(ActiveProjectRoot, scriptPath).ConfigureAwait(true);
        if (!result.Succeeded)
        {
            SetStatus(result.StatusText);
            return false;
        }

        return ApplyLoadedScriptGraph(
            result,
            ScriptKindFromSceneObject(sceneObject),
            SceneScriptName(sceneObject),
            "SceneMetadata",
            sceneObject.Path,
            sceneObject.LinkedScriptPath);
    }

    private bool ApplyLoadedScriptGraph(
        ScriptGraphLoadResult result,
        GraphScriptKind scriptKind,
        string scriptName,
        string source,
        string creatorObjectPath = "",
        string linkedScriptPath = "")
    {
        if (!result.Succeeded || result.Graph is null)
        {
            SetStatus(result.StatusText);
            return false;
        }

        var liveSceneObjects = graph.SceneObjects.ToList();
        graph = result.Graph;
        graph.SceneObjects = liveSceneObjects;
        ApplyScriptBinding(
            scriptKind,
            scriptName,
            source,
            lockScriptKind: true,
            creatorObjectPath: creatorObjectPath,
            linkedScriptPath: linkedScriptPath);
        graph.Script.ProjectRelativePath = result.ProjectRelativePath;
        if (string.IsNullOrWhiteSpace(graph.Script.LinkedScriptPath))
        {
            graph.Script.LinkedScriptPath = result.ProjectRelativePath;
        }

        BridgeScriptName = scriptName;
        DraftScriptName = NormalizeDraftScriptName(scriptName);
        GraphAutosaveEnabled = graph.Script.AutosaveEnabled;
        documentStore.MarkDirty([GraphDocumentSection.Metadata, GraphDocumentSection.Rules, GraphDocumentSection.ViewState]);
        RefreshAll(result.StatusText);
        return true;
    }

    private void ApplySceneDeploymentTarget(SceneObject sceneObject)
    {
        if (IsScriptSceneObject(sceneObject))
        {
            BridgeParentPath = DeploymentParentPath(sceneObject);
            BridgeScriptName = SceneScriptName(sceneObject);
            if (!GraphHasAuthoredContent())
            {
                ApplyScriptBinding(
                    ScriptKindFromSceneObject(sceneObject),
                    BridgeScriptName,
                    "SceneTarget",
                    lockScriptKind: true,
                    creatorObjectPath: sceneObject.Path,
                    linkedScriptPath: sceneObject.LinkedScriptPath);
                documentStore.MarkDirty(GraphDocumentSection.Metadata);
            }
            else
            {
                SetStatus("Selected script target. Script type is unchanged because this graph already has content.");
            }

            NotifyDeployScriptPropertiesChanged();
            return;
        }

        if (!string.IsNullOrWhiteSpace(sceneObject.Path))
        {
            BridgeParentPath = sceneObject.Path;
            NotifyDeployScriptPropertiesChanged();
        }
    }

    private bool TryResolveSceneScriptFile(SceneObject sceneObject, out string scriptPath)
    {
        scriptPath = "";
        var projectRoot = ActiveProjectRoot;
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(sceneObject.LinkedScriptPath))
        {
            var linkedPath = sceneObject.LinkedScriptPath.Replace('/', Path.DirectorySeparatorChar);
            scriptPath = Path.IsPathRooted(linkedPath)
                ? linkedPath
                : Path.Combine(projectRoot, linkedPath);
            return true;
        }

        if (!IsScriptSceneObject(sceneObject))
        {
            return false;
        }

        var relativePath = BridgeFileService.LinkedScriptProjectRelativePath(
            SceneScriptName(sceneObject),
            ScriptKindFromSceneObject(sceneObject));
        scriptPath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return true;
    }

    private static bool IsScriptSceneObject(SceneObject sceneObject)
    {
        return sceneObject.IsLinkedScript ||
            sceneObject.IsVisualScriptName ||
            sceneObject.Kind.Contains("Script", StringComparison.OrdinalIgnoreCase) ||
            sceneObject.LinkedScriptPath.EndsWith(".luau", StringComparison.OrdinalIgnoreCase);
    }

    private static GraphScriptKind ScriptKindFromSceneObject(SceneObject sceneObject)
    {
        var descriptor = $"{sceneObject.Kind} {sceneObject.LinkedScriptPath}";
        if (descriptor.Contains("Module", StringComparison.OrdinalIgnoreCase) ||
            descriptor.Contains(".module.luau", StringComparison.OrdinalIgnoreCase))
        {
            return GraphScriptKind.Module;
        }

        if (descriptor.Contains("Client", StringComparison.OrdinalIgnoreCase) ||
            descriptor.Contains("Local", StringComparison.OrdinalIgnoreCase) ||
            descriptor.Contains(".client.luau", StringComparison.OrdinalIgnoreCase))
        {
            return GraphScriptKind.Local;
        }

        return GraphScriptKind.Server;
    }

    private static string DeploymentParentPath(SceneObject sceneObject)
    {
        if (IsScriptSceneObject(sceneObject))
        {
            return ParentPath(sceneObject.Path);
        }

        return string.IsNullOrWhiteSpace(sceneObject.Path) ? "World" : sceneObject.Path;
    }

    private static string ScriptObjectKind(GraphScriptKind scriptKind)
    {
        return scriptKind switch
        {
            GraphScriptKind.Local => "ClientScript",
            GraphScriptKind.Module => "ModuleScript",
            _ => "ServerScript"
        };
    }

    private static string SceneScriptName(SceneObject sceneObject)
    {
        if (!string.IsNullOrWhiteSpace(sceneObject.Name))
        {
            return sceneObject.Name;
        }

        var path = string.IsNullOrWhiteSpace(sceneObject.LinkedScriptPath)
            ? sceneObject.Path
            : sceneObject.LinkedScriptPath;
        var fileName = Path.GetFileName(path.Replace('/', Path.DirectorySeparatorChar));
        return fileName
            .Replace(".server.luau", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".client.luau", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".module.luau", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".luau", "", StringComparison.OrdinalIgnoreCase);
    }

    private static string ParentPath(string scenePath)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            return "World";
        }

        var normalized = scenePath.Replace('\\', '/').Trim('/');
        var slashIndex = normalized.LastIndexOf('/');
        return slashIndex <= 0 ? "World" : normalized[..slashIndex];
    }
}

/// <summary>
/// Local preview contract for deploy confirmation UI. It mirrors the bridge
/// naming rules without letting the code-behind mutate the graph while typing.
/// </summary>
public sealed record ScriptDeployPreview(
    string AuthorScriptName,
    string CreatorScriptName,
    GraphScriptKind ScriptKind,
    string CreatorObjectKind,
    string ProjectRelativePath,
    bool ProjectFileExists,
    bool ProjectFileReady,
    string ProjectFileStatusText);

public sealed record SavedScriptInstanceDeployPreview(
    string CreatorScriptName,
    GraphScriptKind ScriptKind,
    string CreatorObjectKind,
    string ProjectRelativePath,
    bool ProjectFileReady,
    string ProjectFileStatusText,
    string ParentPath,
    string TargetPath,
    bool CanDeploy,
    string BlockReason);
