using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Vrs.App.Services;
using Vrs.Core.Authoring;
using Vrs.Core.Catalog;
using Vrs.Graph.Interaction;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;
using Vrs.Graph.Theming;

namespace Vrs.App.Controls;

/// <summary>
/// Prototype canvas control composed from partial slices for rendering,
/// hit-testing, palette UI, input handling, and context menus. Graph mutations
/// must still go through IGraphInteractionHost so this visual control never
/// becomes the owner of graph behavior.
/// </summary>
public sealed partial class RuleGraphCanvas : Control
{
    public static readonly StyledProperty<IList?> NodesProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, IList?>(nameof(Nodes));

    public static readonly StyledProperty<IList?> ConnectionsProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, IList?>(nameof(Connections));

    public static readonly StyledProperty<IList?> FragmentsProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, IList?>(nameof(Fragments));

    public static readonly StyledProperty<IList?> NodeGroupsProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, IList?>(nameof(NodeGroups));

    public static readonly StyledProperty<IList?> WireReroutesProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, IList?>(nameof(WireReroutes));

    public static readonly StyledProperty<IList?> CatalogEntriesProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, IList?>(nameof(CatalogEntries));

    public static readonly StyledProperty<GraphScriptKind> SelectedScriptKindProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, GraphScriptKind>(nameof(SelectedScriptKind), GraphScriptKind.Server);

    public static readonly StyledProperty<RuleNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, RuleNode?>(nameof(SelectedNode), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<IList?> SelectedNodeIdsProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, IList?>(nameof(SelectedNodeIds));

    public static readonly StyledProperty<int> SelectedConnectionIndexProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, int>(nameof(SelectedConnectionIndex), -1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string> SelectedFragmentIdProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, string>(nameof(SelectedFragmentId), "", defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string> SelectedGroupIdProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, string>(nameof(SelectedGroupId), "", defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string> SelectedWireRerouteIdProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, string>(nameof(SelectedWireRerouteId), "", defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<GraphViewMode> ViewModeProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, GraphViewMode>(nameof(ViewMode), GraphViewMode.StateMachine);

    public static readonly StyledProperty<bool> ShowAdvancedPinsProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, bool>(nameof(ShowAdvancedPins));

    public static readonly StyledProperty<int> RenderRevisionProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, int>(nameof(RenderRevision));

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, double>(nameof(Zoom), 1.0, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> PanXProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, double>(nameof(PanX), 0.0, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> PanYProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, double>(nameof(PanY), 0.0, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<IGraphInteractionHost?> HostProperty =
        AvaloniaProperty.Register<RuleGraphCanvas, IGraphInteractionHost?>(nameof(Host));

    private static readonly GraphTheme GraphThemeDefinition = GraphTheme.Default;
    private static readonly NodePaletteQueryService NodePaletteQuery = new();
    private static readonly NodeCanvasPresentationService NodeCanvasPresentation = new();
    private readonly RuleGraphGeometryService geometry = new();
    private INotifyCollectionChanged? observedNodes;
    private INotifyCollectionChanged? observedConnections;
    private INotifyCollectionChanged? observedFragments;
    private INotifyCollectionChanged? observedNodeGroups;
    private INotifyCollectionChanged? observedWireReroutes;
    private INotifyCollectionChanged? observedSelectedNodeIds;
    private string draggedNodeId = "";
    private GraphPoint dragStartGraph;
    private GraphPoint draggedNodeStart;
    private readonly Dictionary<string, GraphPoint> draggedNodeStarts = new(StringComparer.OrdinalIgnoreCase);
    private string draggedGroupId = "";
    private readonly Dictionary<string, GraphPoint> draggedGroupStarts = new(StringComparer.OrdinalIgnoreCase);
    private bool resizingGroup;
    private string resizingGroupId = "";
    private float resizedGroupStartWidth;
    private float resizedGroupStartHeight;
    private string draggedWireRerouteId = "";
    private GraphPoint draggedWireRerouteStart;
    private readonly Dictionary<string, GraphPoint> draggedWireRerouteStarts = new(StringComparer.OrdinalIgnoreCase);
    private bool selectingNodes;
    private Point selectionStartPoint;
    private Point selectionCurrentPoint;
    private GraphPoint selectionStartGraph;
    private bool panning;
    private bool spacePanning;
    private Point panStartPointer;
    private double panStartX;
    private double panStartY;
    private GraphPinHit? pendingOutputPin;
    private GraphPoint pendingGraphPoint;
    private bool nodePaletteOpen;
    private Point nodePalettePoint;
    private GraphPoint nodePaletteGraphPoint;
    private GraphPinHit? nodePaletteConnectFrom;
    private string nodePaletteSearch = "";
    private int nodePaletteSelectedIndex;
    private int nodePaletteScrollIndex;
    private bool nodePaletteCompatibleOnly = true;
    private string nodePaletteCurrentIntentKey = "";
    private readonly List<string> nodePaletteCurrentDomainPath = [];
    private Point nodePalettePointerPoint;
    private bool nodePalettePointerInside;
    private string hoveredNodeId = "";
    private Point nodeTooltipPointerPoint;

    static RuleGraphCanvas()
    {
        AffectsRender<RuleGraphCanvas>(
            NodesProperty,
            ConnectionsProperty,
            FragmentsProperty,
            NodeGroupsProperty,
            WireReroutesProperty,
            CatalogEntriesProperty,
            SelectedScriptKindProperty,
            SelectedNodeProperty,
            SelectedNodeIdsProperty,
            SelectedConnectionIndexProperty,
            SelectedFragmentIdProperty,
            SelectedGroupIdProperty,
            SelectedWireRerouteIdProperty,
            ViewModeProperty,
            ShowAdvancedPinsProperty,
            RenderRevisionProperty,
            ZoomProperty,
            PanXProperty,
            PanYProperty);
    }

    public RuleGraphCanvas()
    {
        Focusable = true;
    }

    public IList? Nodes
    {
        get => GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    public IList? Connections
    {
        get => GetValue(ConnectionsProperty);
        set => SetValue(ConnectionsProperty, value);
    }

    public IList? Fragments
    {
        get => GetValue(FragmentsProperty);
        set => SetValue(FragmentsProperty, value);
    }

    public IList? NodeGroups
    {
        get => GetValue(NodeGroupsProperty);
        set => SetValue(NodeGroupsProperty, value);
    }

    public IList? WireReroutes
    {
        get => GetValue(WireReroutesProperty);
        set => SetValue(WireReroutesProperty, value);
    }

    public IList? CatalogEntries
    {
        get => GetValue(CatalogEntriesProperty);
        set => SetValue(CatalogEntriesProperty, value);
    }

    public GraphScriptKind SelectedScriptKind
    {
        get => GetValue(SelectedScriptKindProperty);
        set => SetValue(SelectedScriptKindProperty, value);
    }

    public RuleNode? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public IList? SelectedNodeIds
    {
        get => GetValue(SelectedNodeIdsProperty);
        set => SetValue(SelectedNodeIdsProperty, value);
    }

    public int SelectedConnectionIndex
    {
        get => GetValue(SelectedConnectionIndexProperty);
        set => SetValue(SelectedConnectionIndexProperty, value);
    }

    public string SelectedFragmentId
    {
        get => GetValue(SelectedFragmentIdProperty);
        set => SetValue(SelectedFragmentIdProperty, value);
    }

    public string SelectedGroupId
    {
        get => GetValue(SelectedGroupIdProperty);
        set => SetValue(SelectedGroupIdProperty, value);
    }

    public string SelectedWireRerouteId
    {
        get => GetValue(SelectedWireRerouteIdProperty);
        set => SetValue(SelectedWireRerouteIdProperty, value);
    }

    public GraphViewMode ViewMode
    {
        get => GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    public bool ShowAdvancedPins
    {
        get => GetValue(ShowAdvancedPinsProperty);
        set => SetValue(ShowAdvancedPinsProperty, value);
    }

    public int RenderRevision
    {
        get => GetValue(RenderRevisionProperty);
        set => SetValue(RenderRevisionProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double PanX
    {
        get => GetValue(PanXProperty);
        set => SetValue(PanXProperty, value);
    }

    public double PanY
    {
        get => GetValue(PanYProperty);
        set => SetValue(PanYProperty, value);
    }

    public IGraphInteractionHost? Host
    {
        get => GetValue(HostProperty);
        set => SetValue(HostProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        Host?.UpdateGraphViewport(bounds.Width, bounds.Height);

        using (context.PushClip(bounds))
        {
            context.DrawRectangle(new SolidColorBrush(Color.Parse("#10161f")), null, bounds);
            DrawGrid(context);

            var fragments = FragmentList();
            var groups = GroupList();
            var nodes = VisibleNodeList(NodeList(), fragments);
            var connections = ConnectionList();
            var reroutes = WireRerouteList();
            var nodesById = nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
            var reroutesById = reroutes.ToDictionary(reroute => reroute.Id, StringComparer.OrdinalIgnoreCase);
            var visibleNodeIds = nodes.Select(node => node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            DrawGroups(context, groups);
            DrawCollapsedFragments(context, fragments);

            for (var index = 0; index < connections.Count; index++)
            {
                if (!ConnectionVisible(connections[index], visibleNodeIds))
                {
                    continue;
                }

                DrawConnection(context, connections[index], nodesById, reroutesById, index == SelectedConnectionIndex);
            }

            DrawPendingConnection(context, nodes, nodesById);
            DrawWireReroutes(context, reroutes, VisibleWireRerouteIds(connections, visibleNodeIds));

            foreach (var node in nodes)
            {
                DrawNode(context, node, ReferenceEquals(node, SelectedNode) || SelectedNodeIdSet().Contains(node.Id));
            }

            DrawSelectionRectangle(context);
            DrawNodePalette(context);
            DrawNodeHoverTooltip(context, nodes);
        }
    }

}
