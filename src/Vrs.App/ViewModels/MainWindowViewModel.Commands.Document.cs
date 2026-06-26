using CommunityToolkit.Mvvm.Input;
using Vrs.Core.Persistence;
using Vrs.Core.Samples;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task SaveGraph()
    {
        RuleGraphDocumentNormalizer.NormalizeScriptBinding(graph);
        await documentStore.SaveIfDirtyAsync(graph, paths.GraphSavePath, force: true).ConfigureAwait(true);
        RefreshAll($"Saved graph: {paths.GraphSavePath}");
    }

    [RelayCommand]
    private async Task LoadGraph()
    {
        if (!File.Exists(paths.GraphSavePath))
        {
            StatusText = "No saved graph exists yet.";
            return;
        }

        try
        {
            graph = await RuleGraphJson.LoadAsync(paths.GraphSavePath).ConfigureAwait(true);
            GraphAutosaveEnabled = graph.Script.AutosaveEnabled;
            RefreshAll($"Loaded graph: {paths.GraphSavePath}");
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException)
        {
            var liveSceneObjects = graph.SceneObjects.ToList();
            graph = SampleGraphFactory.CreateEmptyDraftGraph();
            graph.SceneObjects = liveSceneObjects;
            GraphAutosaveEnabled = graph.Script.AutosaveEnabled;
            documentStore.MarkDirty([GraphDocumentSection.Metadata, GraphDocumentSection.Rules, GraphDocumentSection.ViewState]);
            RefreshAll($"Saved graph could not be loaded; opened empty draft instead. {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExportPortableGraph()
    {
        RuleGraphDocumentNormalizer.NormalizeScriptBinding(graph);
        await PortableScriptGraphService.SaveAsync(graph, paths.PortableGraphPath).ConfigureAwait(true);
        RefreshAll($"Exported portable graph: {paths.PortableGraphPath}");
    }

    [RelayCommand]
    private async Task ImportPortableGraph()
    {
        if (!File.Exists(paths.PortableGraphPath))
        {
            StatusText = "No portable graph export exists yet.";
            return;
        }

        try
        {
            var liveSceneObjects = graph.SceneObjects.ToList();
            var result = await PortableScriptGraphService.LoadAsync(paths.PortableGraphPath).ConfigureAwait(true);
            graph = result.Graph;
            if (graph.SceneObjects.Count == 0)
            {
                graph.SceneObjects = liveSceneObjects;
            }

            GraphAutosaveEnabled = graph.Script.AutosaveEnabled;
            documentStore.MarkDirty([GraphDocumentSection.Metadata, GraphDocumentSection.Rules, GraphDocumentSection.ViewState]);
            var warningText = result.Warnings.Count == 0 ? "" : $" Warnings: {string.Join(" ", result.Warnings)}";
            RefreshAll($"Imported portable graph: {paths.PortableGraphPath}.{warningText}");
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or InvalidOperationException)
        {
            SetStatus($"Portable graph import failed: {ex.Message}");
        }
    }

    public async Task RunGraphAutosaveAsync()
    {
        if (isGraphAutosaveRunning || !GraphAutosaveEnabled)
        {
            return;
        }

        isGraphAutosaveRunning = true;
        try
        {
            RuleGraphDocumentNormalizer.NormalizeScriptBinding(graph);
            var result = await documentStore.SaveIfDirtyAsync(graph, paths.GraphSavePath).ConfigureAwait(true);
            if (result.Saved)
            {
                SetStatus("Autosaved graph.");
            }
        }
        finally
        {
            isGraphAutosaveRunning = false;
        }
    }
}
