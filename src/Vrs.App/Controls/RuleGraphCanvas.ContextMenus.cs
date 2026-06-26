using Avalonia.Controls;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    // Context menu glue only: translate canvas selections into host commands without owning graph behavior.
    private void OpenContextMenu(GraphPoint graphPoint)
    {
        var fragments = FragmentList();
        var groups = GroupList();
        var nodes = VisibleNodeList(NodeList(), fragments);
        var connections = ConnectionList();
        var visibleNodeIds = nodes.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var visibleRerouteIds = VisibleWireRerouteIds(connections, visibleNodeIds);
        var reroute = HitTestWireReroute(WireRerouteList(), visibleRerouteIds, graphPoint);
        if (reroute is not null)
        {
            Host?.SelectGraphNode(null);
            Host?.SelectGraphConnection(-1);
            Host?.SelectGraphFragment("");
            Host?.SelectGraphGroup("");
            Host?.SelectGraphWireReroute(reroute.Id);
            OpenWireRerouteContextMenu(reroute.Id);
            return;
        }

        var node = geometry.HitTestNode(nodes, graphPoint);
        if (node is not null)
        {
            if (!SelectedNodeIdSet().Contains(node.Id))
            {
                Host?.SelectGraphNode(node);
            }

            Host?.ShowNodeContextMenu(node.Id, graphPoint.X, graphPoint.Y);
            OpenNodeContextMenu(node.Id);
            return;
        }

        var nodesById = nodes.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var reroutesById = WireRerouteList().ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var connectionHit = HitTestVisibleConnection(connections, nodesById, reroutesById, visibleNodeIds, graphPoint);
        if (connectionHit is not null)
        {
            Host?.SelectGraphNode(null);
            Host?.SelectGraphConnection(connectionHit.ConnectionIndex);
            Host?.SelectGraphFragment("");
            Host?.SelectGraphGroup("");
            Host?.SelectGraphWireReroute("");
            OpenConnectionContextMenu(connectionHit.ConnectionIndex, graphPoint, connectionHit.SegmentIndex);
            return;
        }

        var fragment = HitTestFragment(fragments, graphPoint);
        if (fragment is not null)
        {
            Host?.SelectGraphNode(null);
            Host?.SelectGraphConnection(-1);
            Host?.SelectGraphFragment(fragment.Id);
            Host?.SelectGraphGroup("");
            Host?.SelectGraphWireReroute("");
            OpenFragmentContextMenu(fragment.Id);
            return;
        }

        var group = HitTestGroup(groups, graphPoint);
        if (group is not null)
        {
            Host?.SelectGraphNode(null);
            Host?.SelectGraphConnection(-1);
            Host?.SelectGraphFragment("");
            Host?.SelectGraphGroup(group.Id);
            Host?.SelectGraphWireReroute("");
            OpenGroupContextMenu(group.Id);
            return;
        }

        Host?.ShowCanvasContextMenu(graphPoint.X, graphPoint.Y);
        OpenCanvasContextMenu(graphPoint);
    }

    private void OpenNodeContextMenu(string nodeId)
    {
        var menu = new ContextMenu();
        menu.Items.Add(MenuItem("Delete", () =>
        {
            var node = NodeList().FirstOrDefault(item => item.Id == nodeId);
            Host?.SelectGraphNode(node);
            Host?.DeleteGraphSelection();
        }));
        menu.Items.Add(MenuItem("Duplicate", () => Host?.DuplicateGraphNode(nodeId)));
        menu.Items.Add(MenuItem("Disconnect", () => Host?.DisconnectGraphNode(nodeId)));
        menu.Items.Add(MenuItem("Enable / Disable", () => Host?.ToggleGraphNodeEnabled(nodeId)));
        menu.Items.Add(MenuItem("Debug Marker", () => Host?.ToggleGraphNodeDebug(nodeId)));
        menu.Items.Add(MenuItem("Breakpoint Marker", () => Host?.ToggleGraphNodeBreakpoint(nodeId)));
        menu.Items.Add(MenuItem("Create Node Group", () => Host?.CreateGroupFromSelection()));
        menu.Items.Add(MenuItem("Create Rule Fragment", () => Host?.CreateFragmentFromSelection(GraphFragmentKind.Rule)));
        menu.Items.Add(MenuItem("Create State Fragment", () => Host?.CreateFragmentFromSelection(GraphFragmentKind.State)));
        menu.Items.Add(MenuItem("Edit Comment", () =>
        {
            var node = NodeList().FirstOrDefault(item => item.Id == nodeId);
            Host?.SelectGraphNode(node);
        }));
        menu.Open(this);
    }

    private void OpenConnectionContextMenu(int connectionIndex, GraphPoint graphPoint, int segmentIndex)
    {
        var connections = ConnectionList();
        if (connectionIndex < 0 || connectionIndex >= connections.Count)
        {
            return;
        }

        var connection = connections[connectionIndex];
        var menu = new ContextMenu();
        menu.Items.Add(MenuItem("Add Reroute Here", () =>
        {
            Host?.AddWireRerouteToConnection(connectionIndex, graphPoint.X, graphPoint.Y, segmentIndex);
        }));
        menu.Items.Add(MenuItem("Delete Cable", () =>
        {
            Host?.SelectGraphConnection(connectionIndex);
            Host?.DeleteGraphSelection();
        }));
        menu.Items.Add(MenuItem("Disconnect Source Node", () => Host?.DisconnectGraphNode(connection.From.NodeId)));
        menu.Items.Add(MenuItem("Disconnect Target Node", () => Host?.DisconnectGraphNode(connection.To.NodeId)));
        menu.Open(this);
    }

    private void OpenWireRerouteContextMenu(string rerouteId)
    {
        var menu = new ContextMenu();
        menu.Items.Add(MenuItem("Delete Reroute", () =>
        {
            Host?.SelectGraphWireReroute(rerouteId);
            Host?.DeleteGraphSelection();
        }));
        menu.Items.Add(MenuItem("Frame All", () => Host?.FrameGraphView()));
        menu.Open(this);
    }

    private void OpenFragmentContextMenu(string fragmentId)
    {
        var menu = new ContextMenu();
        menu.Items.Add(MenuItem("Expand Fragment", () => Host?.ExpandGraphFragment(fragmentId)));
        menu.Items.Add(MenuItem("Frame All", () => Host?.FrameGraphView()));
        menu.Open(this);
    }

    private void OpenGroupContextMenu(string groupId)
    {
        var menu = new ContextMenu();
        menu.Items.Add(MenuItem("Create Parent Group From Selection", () => Host?.CreateGroupFromSelection()));
        menu.Items.Add(MenuItem("Delete Group Only", () =>
        {
            Host?.SelectGraphGroup(groupId);
            Host?.DeleteGraphSelection();
        }));
        menu.Items.Add(MenuItem("Frame All", () => Host?.FrameGraphView()));
        menu.Open(this);
    }

    private void OpenCanvasContextMenu(GraphPoint graphPoint)
    {
        var menu = new ContextMenu();
        menu.Items.Add(MenuItem(
            "Add Node Here",
            () => OpenNodePalette(graphPoint),
            "Open the node palette and place the chosen node at this canvas position."));
        menu.Items.Add(MenuItem(
            "Create Empty Node Group Here",
            () => Host?.CreateEmptyGroupAtGraphPoint(graphPoint.X, graphPoint.Y),
            "Create an empty visual group at this canvas position."));
        menu.Items.Add(MenuItem(
            "Add Rule",
            () => Host?.AddRuleFragmentAtGraphPoint(graphPoint.X, graphPoint.Y),
            "Create a rule fragment at this canvas position."));
        menu.Items.Add(MenuItem(
            "Add State",
            () => Host?.AddStateFragmentAtGraphPoint(graphPoint.X, graphPoint.Y),
            "Create a state fragment at this canvas position."));
        menu.Items.Add(MenuItem(
            "Create Rule Fragment From Selection",
            () => Host?.CreateFragmentFromSelection(GraphFragmentKind.Rule),
            "Wrap the current selection in a rule fragment."));
        menu.Items.Add(MenuItem(
            "Create State Fragment From Selection",
            () => Host?.CreateFragmentFromSelection(GraphFragmentKind.State),
            "Wrap the current selection in a state fragment."));
        menu.Items.Add(MenuItem(
            "Create Node Group From Selection",
            () => Host?.CreateGroupFromSelection(),
            "Group selected nodes and reroutes visually without changing generated Luau."));
        menu.Items.Add(MenuItem(
            "Frame All",
            () => Host?.FrameGraphView(),
            "Zoom and pan the canvas to show the full graph."));
        var paste = MenuItem(
            "Paste",
            () => Host?.PasteGraphClipboard(graphPoint.X, graphPoint.Y),
            Host?.CanPasteGraphClipboard == true
                ? "Paste copied graph items at this canvas position."
                : "Copy or cut graph items before pasting here.");
        paste.IsEnabled = Host?.CanPasteGraphClipboard == true;
        menu.Items.Add(paste);
        menu.Open(this);
    }

    private static MenuItem MenuItem(string header, Action action, string? tooltip = null)
    {
        var item = new MenuItem { Header = header };
        if (!string.IsNullOrWhiteSpace(tooltip))
        {
            ToolTip.SetTip(item, tooltip);
            ToolTip.SetShowOnDisabled(item, true);
        }

        item.Click += (_, _) => action();
        return item;
    }
}
