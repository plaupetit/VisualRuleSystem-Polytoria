using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

public sealed class StateRuleRowViewModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public GraphFragmentKind Kind { get; set; } = GraphFragmentKind.Rule;
    public string TriggerSummary { get; set; } = "On: no trigger";
    public string ConditionSummary { get; set; } = "Is: no conditions";
    public string ActionSummary { get; set; } = "Do: no actions";
    public string Comment { get; set; } = "";
    public bool Collapsed { get; set; }
}
