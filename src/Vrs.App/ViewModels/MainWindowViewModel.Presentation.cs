using Vrs.Core.Bridge;
using Vrs.Core.Persistence;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

public partial class MainWindowViewModel
{
    // Computed bindable properties for inspector, fragments, deploy label, and
    // state-rule mode stay separate from raw ObservableProperty backing state.
    public bool HasSelectedNode => SelectedNode is not null;
    public bool ShowsGenericInspectorHeader => SelectedNode is null && !HasSelectedFragment && !HasSelectedGroup && !HasSelectedWireReroute;
    public bool HasSelectedFragment => SelectedFragment is not null;
    public bool HasSelectedGroup => SelectedGroup is not null;
    public bool HasSelectedWireReroute => SelectedWireReroute is not null;
    public bool HasNoActiveProject => !HasLinkedProject;
    public bool CanUseCreatorBridgeCommands => HasLinkedProject && IsCreatorRuntimeReady;
    public bool ShowsStateRuleBuilder => CurrentViewMode == GraphViewMode.Simple;
    public bool ShowsFragmentTools => CurrentViewMode == GraphViewMode.Advanced;
    public string ScriptBindingSummary => graph.AuthoringMode == GraphAuthoringMode.CreatorLinked
        ? IsCreatorRuntimeReady
            ? $"Project {SelectedScriptKind}"
            : $"Project {SelectedScriptKind} · Creator off"
        : $"Draft {SelectedScriptKind}";
    public string ScriptBindingTooltip => graph.AuthoringMode == GraphAuthoringMode.CreatorLinked
        ? $"Project linked to {BridgeParentPath}/{ScriptCreatorPreviewName}. Creator bridge commands require Creator to be running."
        : "Draft without a linked Polytoria project. Create or link a project to deploy.";
    public string ScriptCreatorPreviewName => scriptDeployment.ResolveScriptName(DraftScriptName, graph.Rules.FirstOrDefault());
    public string ScriptFilePreviewPath => BridgeFileService.LinkedScriptProjectRelativePath(ScriptCreatorPreviewName, SelectedScriptKind);
    public string ScriptCreatorPreviewText => $"Creator: {ScriptCreatorPreviewName}";
    public string ScriptFilePreviewText => $"File: {ScriptFilePreviewPath}";
    public string GraphAutosaveTooltip => $"Autosaves the editable graph to:{Environment.NewLine}{paths.GraphSavePath}";
    public bool IsPolyCreatorLessDraft => graph.AuthoringMode == GraphAuthoringMode.PolyCreatorLessDraft;
    public bool ShouldConfirmGraphLoadReplacement => GraphHasAuthoredContent();
    public string SelectedNodeKindText => BuildSelectedNodePresentation().KindText;
    public string SelectedNodeTypeText => BuildSelectedNodePresentation().TypeText;
    public string SelectedNodeBlockTitle => BuildSelectedNodePresentation().BlockTitle;
    public string SelectedNodeHumanVerb => BuildSelectedNodePresentation().HumanVerb;
    public string SelectedNodeBlockBadge => BuildSelectedNodePresentation().BlockBadge;
    public string SelectedNodeBlockAccentHex => BuildSelectedNodePresentation().AccentHex;
    public string SelectedNodeBlockFillHex => BuildSelectedNodePresentation().FillHex;
    public string SelectedNodeBlockSubtitle => BuildSelectedNodePresentation().BlockSubtitle;
    public string SelectedNodeConfiguredSummary => BuildSelectedNodePresentation().ConfiguredSummary;

    public bool SelectedNodeDetailsOpen
    {
        get => SelectedNode?.DetailsOpen ?? false;
        set
        {
            if (SelectedNode is null || SelectedNode.DetailsOpen == value)
            {
                return;
            }

            SelectedNode.DetailsOpen = value;
            OnPropertyChanged();
            OnSelectedNodeConfigurationChanged(value ? "Opened node details." : "Collapsed node details.");
        }
    }

    public GraphScriptKind SelectedScriptKind
    {
        get => graph.Script.ScriptKind;
        set
        {
            if (graph.Script.ScriptKind == value)
            {
                return;
            }

            if (!TryChangeScriptKind(value))
            {
                OnPropertyChanged();
            }
        }
    }

    public GraphFragment? SelectedFragment => selectionInspector.FindSelectedFragment(EnsureRule(), SelectedFragmentId);
    public string SelectedFragmentKindText => SelectedFragment?.Kind.ToString() ?? "";
    public RuleNodeGroup? SelectedGroup => EnsureRule().NodeGroups.FirstOrDefault(group => string.Equals(group.Id, SelectedGroupId, StringComparison.OrdinalIgnoreCase));
    public RuleWireReroute? SelectedWireReroute => EnsureRule().WireReroutes.FirstOrDefault(reroute => string.Equals(reroute.Id, SelectedWireRerouteId, StringComparison.OrdinalIgnoreCase));
    public string SelectedWireReroutePositionText => SelectedWireReroute is null ? "" : $"{SelectedWireReroute.GraphX:0}, {SelectedWireReroute.GraphY:0}";
    public string SelectedWireRerouteWireText
    {
        get
        {
            var reroute = SelectedWireReroute;
            if (reroute is null)
            {
                return "";
            }

            var connection = EnsureRule().Connections.FirstOrDefault(item => item.RerouteIds.Contains(reroute.Id, StringComparer.OrdinalIgnoreCase));
            return connection is null
                ? "Not attached to a wire"
                : $"{connection.From.NodeId}.{connection.From.PortId} -> {connection.To.NodeId}.{connection.To.PortId}";
        }
    }

    public string SelectedWireRerouteInputDirection
    {
        get => SelectedWireReroute?.InputDirection ?? WireRerouteDirection.Left;
        set
        {
            var reroute = SelectedWireReroute;
            if (reroute is null || string.Equals(reroute.InputDirection, value, StringComparison.Ordinal))
            {
                return;
            }

            ApplyWireRerouteEdit(editor.SetWireRerouteDirections(EnsureRule(), reroute.Id, value, reroute.OutputDirection));
        }
    }

    public string SelectedWireRerouteOutputDirection
    {
        get => SelectedWireReroute?.OutputDirection ?? WireRerouteDirection.Right;
        set
        {
            var reroute = SelectedWireReroute;
            if (reroute is null || string.Equals(reroute.OutputDirection, value, StringComparison.Ordinal))
            {
                return;
            }

            ApplyWireRerouteEdit(editor.SetWireRerouteDirections(EnsureRule(), reroute.Id, reroute.InputDirection, value));
        }
    }

    public int SelectedGroupMemberCount => SelectedGroup?.MemberNodeIds.Count ?? 0;
    public string SelectedGroupSizeText => SelectedGroup is null ? "" : $"{SelectedGroup.Width:0} x {SelectedGroup.Height:0}";
    public string SelectedGroupParentText
    {
        get
        {
            var group = SelectedGroup;
            if (group is null || string.IsNullOrWhiteSpace(group.ParentGroupId))
            {
                return "No parent group";
            }

            return EnsureRule().NodeGroups.FirstOrDefault(item => string.Equals(item.Id, group.ParentGroupId, StringComparison.OrdinalIgnoreCase))?.Name
                ?? group.ParentGroupId;
        }
    }

    public string SelectedGroupName
    {
        get => SelectedGroup?.Name ?? "";
        set
        {
            var group = SelectedGroup;
            if (group is null || string.Equals(group.Name, value, StringComparison.Ordinal))
            {
                return;
            }

            ApplyGroupEdit(editor.RenameGroup(EnsureRule(), group.Id, value));
        }
    }

    public string SelectedGroupColor
    {
        get => SelectedGroup?.Color ?? "Teal";
        set
        {
            var group = SelectedGroup;
            if (group is null || string.Equals(group.Color, value, StringComparison.Ordinal))
            {
                return;
            }

            ApplyGroupEdit(editor.SetGroupColor(EnsureRule(), group.Id, value));
        }
    }

    public bool SelectedFragmentCollapsed
    {
        get => SelectedFragment?.Collapsed ?? false;
        set
        {
            var fragment = SelectedFragment;
            if (fragment is null || fragment.Collapsed == value)
            {
                return;
            }

            var result = editor.SetFragmentCollapsed(EnsureRule(), fragment.Id, value);
            if (result.Success)
            {
                documentStore.MarkDirty(GraphDocumentSection.ViewState);
            }

            RefreshAll(result.Message);
        }
    }

    public bool SelectedNodeEnabled
    {
        get => SelectedNode?.Enabled ?? false;
        set
        {
            if (SelectedNode is null || SelectedNode.Enabled == value)
            {
                return;
            }

            SelectedNode.Enabled = value;
            OnPropertyChanged();
            OnSelectedNodeConfigurationChanged(value ? "Enabled selected node." : "Disabled selected node.");
        }
    }

    public bool SelectedNodeDebugEnabled
    {
        get => SelectedNode?.DebugEnabled ?? false;
        set
        {
            if (SelectedNode is null || SelectedNode.DebugEnabled == value)
            {
                return;
            }

            SelectedNode.DebugEnabled = value;
            OnPropertyChanged();
            OnSelectedNodeConfigurationChanged(value ? "Enabled debug logs." : "Disabled debug logs.");
        }
    }

    public string SelectedNodeFallbackMode
    {
        get => SelectedNode?.FallbackMode ?? "";
        set
        {
            if (SelectedNode is null || SelectedNode.FallbackMode == value)
            {
                return;
            }

            SelectedNode.FallbackMode = value;
            OnPropertyChanged();
            OnSelectedNodeConfigurationChanged("Updated node fallback mode.");
        }
    }

    public string SelectedNodeUserComment
    {
        get => SelectedNode?.UserComment ?? "";
        set
        {
            if (SelectedNode is null || SelectedNode.UserComment == value)
            {
                return;
            }

            SelectedNode.UserComment = value;
            OnPropertyChanged();
            OnSelectedNodeConfigurationChanged("Updated author note.");
        }
    }
}
