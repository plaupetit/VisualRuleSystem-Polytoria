using CommunityToolkit.Mvvm.Input;
using Vrs.Core.Catalog;
using Vrs.Core.Persistence;
using System.Diagnostics;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private void ReloadCatalog()
    {
        var stopwatch = Stopwatch.StartNew();
        ApplyCatalog(catalogIndexService.GetCatalog(paths.CatalogRoot));
        stopwatch.Stop();
        SetStatus($"Reloaded {catalog.Nodes.Count} catalog node(s) in {stopwatch.ElapsedMilliseconds} ms.");
    }

    private void ApplyCatalog(NodeCatalogData loadedCatalog)
    {
        catalog = loadedCatalog;
        if (graphRefresh.BackfillCatalogParameters(graph, catalog.Nodes))
        {
            documentStore.MarkDirty(GraphDocumentSection.Rules);
        }

        RefreshCatalogList();
        RefreshSelectedNodeParameters();
        foreach (var warning in catalog.Warnings)
        {
            Logs.Add(warning);
        }
    }

    [RelayCommand]
    private void AddSelectedCatalogNode()
    {
        AddSelectedCatalogNodeAtCanvasPosition();
    }

    [RelayCommand]
    private void AddSelectedCatalogNodeAtCanvasPosition()
    {
        if (SelectedCatalogEntry is null || !NodeCatalogService.IsAddable(SelectedCatalogEntry))
        {
            StatusText = "Select an addable node from the catalog first.";
            return;
        }

        var rule = EnsureRule();
        var node = NodeCatalogService.CreateNode(SelectedCatalogEntry, CanvasAddGraphX, CanvasAddGraphY);
        var result = editor.AddNode(rule, node);
        if (result.Success)
        {
            documentStore.MarkDirty(GraphDocumentSection.Rules);
        }

        RefreshAll(result.Message, includePreviewDiffInStatus: result.Success && result.Changed);
        if (result.Success)
        {
            SelectNodeById(node.Id);
        }
    }
}
