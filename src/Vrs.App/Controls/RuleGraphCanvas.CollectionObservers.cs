using System.Collections.Specialized;
using Avalonia;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    // Keep collection subscriptions beside the property-change hook so binding
    // refreshes do not leak observers when Avalonia swaps collection instances.
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == NodesProperty)
        {
            ReplaceCollectionObserver(ref observedNodes, change.NewValue as INotifyCollectionChanged);
        }
        else if (change.Property == ConnectionsProperty)
        {
            ReplaceCollectionObserver(ref observedConnections, change.NewValue as INotifyCollectionChanged);
        }
        else if (change.Property == FragmentsProperty)
        {
            ReplaceCollectionObserver(ref observedFragments, change.NewValue as INotifyCollectionChanged);
        }
        else if (change.Property == NodeGroupsProperty)
        {
            ReplaceCollectionObserver(ref observedNodeGroups, change.NewValue as INotifyCollectionChanged);
        }
        else if (change.Property == WireReroutesProperty)
        {
            ReplaceCollectionObserver(ref observedWireReroutes, change.NewValue as INotifyCollectionChanged);
        }
        else if (change.Property == SelectedNodeIdsProperty)
        {
            ReplaceCollectionObserver(ref observedSelectedNodeIds, change.NewValue as INotifyCollectionChanged);
        }
    }
}
