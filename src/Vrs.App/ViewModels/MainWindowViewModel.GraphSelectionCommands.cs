using Vrs.Core.Persistence;
using Vrs.Graph.Modeling;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    // Clipboard and deletion commands operate on the current canvas selection,
    // while GraphInteractionHost stays focused on adapting canvas gestures.
    public bool CanPasteGraphClipboard => graphClipboard.HasClipboard;

    public void CopyGraphSelection()
    {
        var result = graphClipboard.Copy(EnsureRule(), CurrentGraphClipboardSelection());
        OnPropertyChanged(nameof(CanPasteGraphClipboard));
        SetStatus(result.Message);
    }

    public void CutGraphSelection()
    {
        var result = graphClipboard.Copy(EnsureRule(), CurrentGraphClipboardSelection());
        OnPropertyChanged(nameof(CanPasteGraphClipboard));
        if (!result.Success)
        {
            SetStatus(result.Message);
            return;
        }

        DeleteGraphSelection();
        SetStatus($"Cut {result.NodeCount} node(s), {result.GroupCount} group(s).");
    }

    public void PasteGraphClipboard(float graphX, float graphY)
    {
        var pasteX = float.IsNaN(graphX) || float.IsInfinity(graphX) ? CanvasAddGraphX : graphX;
        var pasteY = float.IsNaN(graphY) || float.IsInfinity(graphY) ? CanvasAddGraphY : graphY;
        var result = graphClipboard.Paste(EnsureRule(), pasteX, pasteY);
        if (!result.Success)
        {
            SetStatus(result.Message);
            return;
        }

        documentStore.MarkDirty(GraphDocumentSection.Rules);
        documentStore.MarkDirty(GraphDocumentSection.ViewState);
        RefreshAll(result.Message, includePreviewDiffInStatus: result.Changed);

        if (result.NodeIds.Count > 0)
        {
            SelectGraphNodes(result.NodeIds, result.NodeIds[0]);
        }
        else if (result.GroupIds.Count > 0)
        {
            SelectGroupById(result.GroupIds[0]);
        }
    }

    public void DeleteGraphSelection()
    {
        if (!string.IsNullOrWhiteSpace(SelectedWireRerouteId))
        {
            var result = editor.RemoveWireReroute(EnsureRule(), SelectedWireRerouteId);
            if (result.Success)
            {
                documentStore.MarkDirty(GraphDocumentSection.ViewState);
                SelectedWireRerouteId = "";
            }

            RefreshAll(result.Message);
            return;
        }

        if (!string.IsNullOrWhiteSpace(SelectedGroupId))
        {
            var result = editor.RemoveGroup(EnsureRule(), SelectedGroupId);
            if (result.Success)
            {
                documentStore.MarkDirty(GraphDocumentSection.ViewState);
                SelectedGroupId = "";
            }

            RefreshAll(result.Message);
            return;
        }

        if (SelectedNodeIds.Count > 1)
        {
            var selectedIds = SelectedNodeIds.ToList();
            var results = selectedIds.Select(nodeId => editor.RemoveNode(EnsureRule(), nodeId)).ToList();
            var changed = results.Any(result => result.Success && result.Changed);
            if (changed)
            {
                documentStore.MarkDirty(GraphDocumentSection.Rules);
            }

            SelectedNodeIds.Clear();
            SelectedNode = null;
            SelectedConnectionIndex = -1;
            RefreshAll(changed ? $"Deleted {results.Count(result => result.Success)} selected node(s)." : "No selected nodes were deleted.", includePreviewDiffInStatus: changed);
            return;
        }

        var deleteSelection = graphInteraction.DeleteSelection(EnsureRule(), SelectedFragmentId, SelectedConnectionIndex, SelectedNode);
        if (deleteSelection.ClearFragmentSelection)
        {
            SelectedFragmentId = "";
        }

        if (deleteSelection.ClearConnectionSelection)
        {
            SelectedConnectionIndex = -1;
        }

        if (deleteSelection.ClearNodeSelection)
        {
            SelectedNode = null;
            SelectedNodeIds.Clear();
        }

        if (deleteSelection.Result.Success && deleteSelection.Result.Changed)
        {
            documentStore.MarkDirty(GraphDocumentSection.Rules);
        }

        RefreshAll(
            deleteSelection.Result.Message,
            includePreviewDiffInStatus: deleteSelection.IncludePreviewDiffInStatus && deleteSelection.Result.Success && deleteSelection.Result.Changed);
    }

    private GraphClipboardSelection CurrentGraphClipboardSelection()
    {
        var nodeIds = SelectedNodeIds.Count > 0
            ? SelectedNodeIds.ToList()
            : SelectedNode is not null ? [SelectedNode.Id] : [];

        return new GraphClipboardSelection(
            nodeIds,
            SelectedGroupId,
            SelectedFragmentId,
            SelectedConnectionIndex);
    }
}
