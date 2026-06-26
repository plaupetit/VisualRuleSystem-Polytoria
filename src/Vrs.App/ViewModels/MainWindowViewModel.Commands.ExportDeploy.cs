using CommunityToolkit.Mvvm.Input;
using Vrs.App.Services;
using Vrs.Core.Bridge;
using Vrs.Core.Catalog;
using Vrs.Core.Export;
using Vrs.Core.Persistence;
using Vrs.Core.ProjectInputs;
using Vrs.Graph.Model;
using Vrs.Graph.Theming;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand(CanExecute = nameof(CanExportLuau))]
    private async Task ExportLuau()
    {
        if (!TryGetExportableRule(out var rule))
        {
            SetStatus("Cannot export Luau: the canvas has no nodes.");
            return;
        }

        Directory.CreateDirectory(paths.ExportDirectory);
        var exported = exporter.ExportRuleToLuauFiles(rule, graph, catalog.Nodes).Single();
        var path = Path.Combine(paths.ExportDirectory, $"{rule.Name}.generated{exported.Suffix}");
        var htmlPath = $"{path}.html";
        var latestPath = Path.Combine(paths.ExportDirectory, $"latest.generated{exported.Suffix}");
        var latestHtmlPath = $"{latestPath}.html";
        await File.WriteAllTextAsync(path, exported.Content).ConfigureAwait(true);
        await File.WriteAllTextAsync(latestPath, exported.Content).ConfigureAwait(true);
        var html = LuauHtmlExporter.ExportToHtml(exported.Content, LuauSyntaxTheme.PolytoriaLike, $"{rule.Name} {exported.Role} Luau");
        await File.WriteAllTextAsync(htmlPath, html).ConfigureAwait(true);
        await File.WriteAllTextAsync(latestHtmlPath, html).ConfigureAwait(true);
        RefreshAll($"Exported {exported.Role} Luau + colored HTML: {path}. Latest script copy: {latestPath}");
    }

    [RelayCommand(CanExecute = nameof(CanQueueCreateFolder))]
    private async Task QueueCreateFolder()
    {
        var bridgeDirectory = await ResolveBridgeDirectory().ConfigureAwait(true);
        var commandId = await bridge.QueueCreateFolderAsync(
            bridgeDirectory,
            BridgeParentPath,
            BridgeFolderName,
            BridgeDryRun).ConfigureAwait(true);

        await bridge.WriteHeartbeatAsync(bridgeDirectory, active: true, focused: IsVrsWindowFocused).ConfigureAwait(true);
        ApplyBridgeBeatPresentation(lastBridgeSyncResult);
        RefreshAll($"Queued create_folder command: {commandId}");
    }

    private bool CanQueueCreateFolder() => CanUseCreatorBridgeCommands;

    [RelayCommand(CanExecute = nameof(CanEnsureInputManager))]
    private async Task EnsureInputManager()
    {
        var projectRoot = await ResolveDeploymentProjectRoot().ConfigureAwait(true);
        if (projectRoot is null)
        {
            SetStatus("Input Manager requires an active Polytoria project.");
            return;
        }

        var presetResult = await inputManager.EnsurePresetActionsAsync(projectRoot).ConfigureAwait(true);
        await RefreshInputActionChoicesAsync().ConfigureAwait(true);
        await RefreshProjectFilesAsync().ConfigureAwait(true);
        var presetStatus = presetResult.FormatStatusFragment();
        if (!presetResult.Succeeded)
        {
            RefreshAll(presetStatus);
            return;
        }

        if (!CanUseCreatorBridgeCommands)
        {
            RefreshAll($"{presetStatus}. Creator bridge required to create Hidden events.");
            return;
        }

        var buttonEvents = VrsInputPresetCatalog.ButtonEventChoices(inputActionChoices).ToList();
        var bridgeDirectory = await ResolveBridgeDirectory().ConfigureAwait(true);
        var commandId = await bridge.QueueEnsureNetworkEventsAsync(
            bridgeDirectory,
            VrsInputPresetCatalog.RuntimeInputEventFolderPath,
            buttonEvents.Select(choice => choice.EventName),
            dryRun: false).ConfigureAwait(true);

        await bridge.WriteHeartbeatAsync(bridgeDirectory, active: true, focused: IsVrsWindowFocused).ConfigureAwait(true);
        ApplyBridgeBeatPresentation(lastBridgeSyncResult);
        RefreshAll($"{presetStatus}, {buttonEvents.Count} events queued.");
        await WaitForCreatorCommandResultAsync(commandId).ConfigureAwait(true);
    }

    private bool CanEnsureInputManager()
        => HasLinkedProject || projectRuntimeStatus.IsValidProjectRoot(ActiveProjectRoot);

    [RelayCommand(CanExecute = nameof(CanDeployScriptFile))]
    private async Task DeployScriptFile()
    {
        await DeployScriptFileAsync(SelectedScriptKind, DraftScriptName).ConfigureAwait(true);
    }

    public async Task DeployScriptFileAsync(GraphScriptKind scriptKind, string scriptName)
    {
        var deploy = await TryPrepareScriptDeployAsync(scriptKind, scriptName, BridgeParentPath, "deploy file").ConfigureAwait(true);
        if (deploy is null)
        {
            return;
        }

        // Deploy File is the explicit point where the authored graph type/name
        // become the generated script file identity.
        ApplyScriptDeployBinding(deploy.Rule, deploy.ScriptKind, deploy.AuthorScriptName, "DeployFile");
        var content = exporter.ExportRuleToLuau(deploy.Rule, graph, catalog.Nodes);
        var fileResult = await scriptDeployment.DeployProjectScriptFileAsync(
            new ScriptProjectFileDeploymentRequest(
                deploy.ProjectRoot,
                deploy.AuthorScriptName,
                deploy.Rule,
                deploy.ScriptKind,
                content,
                IsVrsWindowFocused)).ConfigureAwait(true);

        BridgeDirectory = fileResult.BridgeDirectory;
        graph.Script.ProjectRelativePath = fileResult.ProjectRelativeScriptPath;
        documentStore.MarkDirty(GraphDocumentSection.Metadata);
        RefreshAll(fileResult.StatusText);
    }

    public async Task DeployScriptInstanceToSceneItemAsync(
        SceneHierarchyItemViewModel item,
        GraphScriptKind scriptKind,
        string scriptName,
        bool dryRun)
    {
        var parentPath = GetDeployParentPathForSceneItem(item);
        var deploy = await TryPrepareScriptDeployAsync(scriptKind, scriptName, parentPath, "deploy instance").ConfigureAwait(true);
        if (deploy is null)
        {
            return;
        }

        await DeployPreparedScriptInstanceAsync(deploy, dryRun, updateScriptBinding: true).ConfigureAwait(true);
    }

    public async Task DeployCurrentSavedScriptInstanceToSceneItemAsync(
        SceneHierarchyItemViewModel item,
        bool dryRun)
    {
        var parentPath = GetDeployParentPathForSceneItem(item);
        var deploy = await TryPrepareScriptDeployAsync(
            graph.Script.ScriptKind,
            graph.Script.ScriptName,
            parentPath,
            "deploy saved script instance").ConfigureAwait(true);
        if (deploy is null)
        {
            return;
        }

        await DeployPreparedScriptInstanceAsync(
            deploy,
            dryRun,
            updateScriptBinding: false,
            queuedStatusOverride: $"Queued saved script instance: {TargetPath(deploy.ParentPath, deploy.CreatorScriptName)}").ConfigureAwait(true);
    }

    private async Task DeployPreparedScriptInstanceAsync(
        PreparedScriptDeploy deploy,
        bool dryRun,
        bool updateScriptBinding,
        string? queuedStatusOverride = null)
    {
        var expectedPath = BridgeFileService.LinkedScriptProjectRelativePath(deploy.CreatorScriptName, deploy.ScriptKind);
        var scriptPath = Path.Combine(deploy.ProjectRoot, expectedPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(scriptPath) || new FileInfo(scriptPath).Length == 0)
        {
            SetStatus($"Deploy File first: {expectedPath}");
            return;
        }

        if (updateScriptBinding)
        {
            ApplyScriptDeployBinding(deploy.Rule, deploy.ScriptKind, deploy.AuthorScriptName, "DeployInstance");
        }

        BridgeParentPath = deploy.ParentPath;
        var instanceResult = await scriptDeployment.DeployScriptInstanceAsync(
            new ScriptInstanceDeploymentRequest(
                graph.SceneObjects,
                deploy.ProjectRoot,
                deploy.ParentPath,
                deploy.AuthorScriptName,
                deploy.ScriptKind,
                dryRun,
                IsVrsWindowFocused)).ConfigureAwait(true);

        BridgeDirectory = instanceResult.BridgeDirectory;
        graph.Script.ProjectRelativePath = instanceResult.ProjectRelativeScriptPath;
        graph.Script.CreatorParentPath = deploy.ParentPath;
        graph.Script.CreatorObjectPath = string.IsNullOrWhiteSpace(deploy.ParentPath)
            ? deploy.CreatorScriptName
            : $"{deploy.ParentPath.TrimEnd('/')}/{deploy.CreatorScriptName}";
        documentStore.MarkDirty(GraphDocumentSection.Metadata);
        RefreshAll(instanceResult.Blocked ? instanceResult.StatusText : queuedStatusOverride ?? instanceResult.StatusText);
        if (instanceResult.Blocked || string.IsNullOrWhiteSpace(instanceResult.CommandId))
        {
            return;
        }

        await WaitForCreatorCommandResultAsync(instanceResult.CommandId).ConfigureAwait(true);
    }

    private async Task<PreparedScriptDeploy?> TryPrepareScriptDeployAsync(
        GraphScriptKind scriptKind,
        string scriptName,
        string parentPath,
        string actionName)
    {
        if (!TryGetExportableRule(out var rule))
        {
            SetStatus($"Cannot {actionName}: the canvas has no nodes.");
            return null;
        }

        if (!CanUseCreatorBridgeCommands)
        {
            SetStatus(HasLinkedProject
                ? $"Cannot {actionName}: Polytoria Creator or the bridge is not running."
                : $"Cannot {actionName}: no active Polytoria project is linked.");
            return null;
        }

        var projectRoot = await ResolveDeploymentProjectRoot().ConfigureAwait(true);
        if (projectRoot is null)
        {
            SetStatus($"Cannot {actionName}: no active Polytoria project is linked.");
            return null;
        }

        var authorScriptName = NormalizeDraftScriptName(scriptName);
        if (!TryValidateScriptKindForDeploy(rule, scriptKind, out var blockReason))
        {
            SetStatus(blockReason);
            return null;
        }

        var cleanParentPath = string.IsNullOrWhiteSpace(parentPath) ? BridgeParentPath : parentPath.Trim();
        var creatorScriptName = scriptDeployment.ResolveScriptName(authorScriptName, rule);
        return new PreparedScriptDeploy(rule, projectRoot, cleanParentPath, scriptKind, authorScriptName, creatorScriptName);
    }

    private async Task<string?> ResolveDeploymentProjectRoot()
    {
        if (!string.IsNullOrWhiteSpace(ActiveProjectRoot) &&
            projectRuntimeStatus.IsValidProjectRoot(ActiveProjectRoot))
        {
            return Path.GetFullPath(ActiveProjectRoot);
        }

        return await ResolveActiveProjectRoot().ConfigureAwait(true);
    }

    private void ApplyScriptDeployBinding(Rule rule, GraphScriptKind scriptKind, string authorScriptName, string source)
    {
        var oldScriptName = graph.Script.ScriptName;
        var oldAuthorName = NormalizeDraftScriptName(oldScriptName);
        var oldCreatorName = scriptDeployment.ResolveScriptName(oldScriptName, rule);
        var shouldRenameRule = RuleNameTracksScript(rule, oldScriptName, oldAuthorName, oldCreatorName);

        ApplyScriptBinding(scriptKind, authorScriptName, source, lockScriptKind: true);
        BridgeScriptName = authorScriptName;
        DraftScriptName = authorScriptName;
        if (shouldRenameRule)
        {
            rule.Name = authorScriptName;
        }

        documentStore.MarkDirty([GraphDocumentSection.Metadata, GraphDocumentSection.Rules]);
    }

    private bool TryValidateScriptKindForDeploy(Rule rule, GraphScriptKind scriptKind, out string blockReason)
    {
        var incompatibleNodes = FlattenRuleNodes(rule.Nodes)
            .Select(node => (Node: node, Entry: FindCatalogEntryForNode(node)))
            .Where(item => item.Entry is not null && !NodeCatalogService.IsCompatibleWithScriptKind(item.Entry, scriptKind))
            .Select(item => new ScriptKindMismatch(NodeDisplayName(item.Node), item.Entry!.RuntimeFamily))
            .ToList();

        if (incompatibleNodes.Count == 0)
        {
            blockReason = "";
            return true;
        }

        var shownNodes = string.Join(", ", incompatibleNodes.Take(4).Select(item => $"{item.Label} ({item.RuntimeFamily})"));
        var moreText = incompatibleNodes.Count > 4 ? $" and {incompatibleNodes.Count - 4} more" : "";
        blockReason = $"Cannot deploy as {scriptKind}: {incompatibleNodes.Count} node(s) require another script type: {shownNodes}{moreText}.";
        return false;
    }

    private NodeCatalogEntry? FindCatalogEntryForNode(RuleNode node)
    {
        var catalogId = string.IsNullOrWhiteSpace(node.CatalogId) ? node.Type : node.CatalogId;
        return NodeCatalogService.FindByCatalogId(catalog.Nodes, catalogId);
    }

    private static IEnumerable<RuleNode> FlattenRuleNodes(IEnumerable<RuleNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in FlattenRuleNodes(node.ChildNodes))
            {
                yield return child;
            }
        }
    }

    private static string NodeDisplayName(RuleNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Label))
        {
            return node.Label;
        }

        if (!string.IsNullOrWhiteSpace(node.Type))
        {
            return node.Type;
        }

        return string.IsNullOrWhiteSpace(node.CatalogId) ? node.Id : node.CatalogId;
    }

    private sealed record ScriptKindMismatch(string Label, string RuntimeFamily);

    private static string TargetPath(string parentPath, string creatorScriptName)
    {
        return string.IsNullOrWhiteSpace(parentPath)
            ? creatorScriptName
            : $"{parentPath.TrimEnd('/')}/{creatorScriptName}";
    }

    private sealed record PreparedScriptDeploy(
        Rule Rule,
        string ProjectRoot,
        string ParentPath,
        GraphScriptKind ScriptKind,
        string AuthorScriptName,
        string CreatorScriptName);
}
