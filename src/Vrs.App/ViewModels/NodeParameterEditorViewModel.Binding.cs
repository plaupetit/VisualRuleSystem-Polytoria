using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

public sealed partial class NodeParameterEditorViewModel
{
    // Binding normalization helpers that keep old parameter data compatible with the inspector source model.
    private static void EnsureBinding(RuleParameter parameter, NodeCatalogParameterDefinition? definition)
    {
        if (string.IsNullOrWhiteSpace(parameter.Binding.DataType))
        {
            parameter.Binding.DataType = definition?.Type ?? "String";
        }

        if (parameter.Binding.SourceKind == GraphValueSourceKind.TriggeringPlayer)
        {
            parameter.Value = "Triggering Player";
            parameter.ValueSource = SourceKindLabel(GraphValueSourceKind.TriggeringPlayer);
            parameter.Binding.ConstantValue = "";
            parameter.Binding.DisplayText = "Triggering Player";
            return;
        }

        if (string.IsNullOrWhiteSpace(parameter.Binding.ConstantValue))
        {
            parameter.Binding.ConstantValue = parameter.Value;
        }

        if (parameter.Binding.SourceKind == GraphValueSourceKind.Constant)
        {
            parameter.Binding.DisplayText = parameter.Binding.ConstantValue;
        }
    }

    private static void NormalizeInspectorSourceKind(RuleParameter parameter)
    {
        if (parameter.Binding.SourceKind != GraphValueSourceKind.ConnectedPort)
        {
            return;
        }

        parameter.Binding.SourceKind = GraphValueSourceKind.Constant;
        parameter.ValueSource = SourceKindLabel(GraphValueSourceKind.Constant);
        if (string.IsNullOrWhiteSpace(parameter.Binding.ConstantValue))
        {
            parameter.Binding.ConstantValue = parameter.Value;
        }

        parameter.Binding.DisplayText = parameter.Binding.ConstantValue;
    }

    private static bool IsInspectorSourceKind(GraphValueSourceKind sourceKind)
    {
        return sourceKind != GraphValueSourceKind.ConnectedPort;
    }

    private static string SourceKindLabel(GraphValueSourceKind sourceKind)
    {
        return sourceKind switch
        {
            GraphValueSourceKind.Constant => "Manual Value",
            GraphValueSourceKind.LocalVariable => "Local Variable",
            GraphValueSourceKind.GlobalVariable => "Global Variable",
            GraphValueSourceKind.Self => "Self",
            GraphValueSourceKind.Target => "Target",
            GraphValueSourceKind.TriggeringPlayer => "Triggering Player",
            GraphValueSourceKind.SceneObject => "Scene Object Picker",
            GraphValueSourceKind.ConnectedPort => "Connected Port",
            GraphValueSourceKind.CatalogValue => "Build Value",
            _ => sourceKind.ToString()
        };
    }

}
