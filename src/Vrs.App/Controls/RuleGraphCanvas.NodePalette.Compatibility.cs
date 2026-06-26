using Vrs.Core.Catalog;
using Vrs.Graph.Model;
using Vrs.Graph.Modeling;

namespace Vrs.App.Controls;

public sealed partial class RuleGraphCanvas
{
    // Compatibility checks between the current graph pin, candidate catalog nodes, and selected script kind.
    private string NodePaletteIncompatibilityReason(NodeCatalogEntry entry, GraphPinHit? connectFrom)
    {
        if (!NodeCatalogService.IsCompatibleWithScriptKind(entry, SelectedScriptKind))
        {
            return $"Needs {RuntimeBadge(entry)} script";
        }

        if (connectFrom is not null && !CanConnectFromPalettePin(entry, connectFrom))
        {
            return "Wire incompatible";
        }

        return "";
    }

    private bool CanConnectFromPalettePin(NodeCatalogEntry entry, GraphPinHit connectFrom)
    {
        var sourcePort = NodeList()
            .FirstOrDefault(node => string.Equals(node.Id, connectFrom.NodeId, StringComparison.OrdinalIgnoreCase))
            ?.Ports.FirstOrDefault(port => string.Equals(port.Id, connectFrom.PortId, StringComparison.OrdinalIgnoreCase));
        if (sourcePort is null || sourcePort.Direction != NodePortDirection.Output)
        {
            return false;
        }

        var previewNode = NodeCatalogService.CreateNode(entry, stableId: "PALETTE_PREVIEW");
        return previewNode.Ports.Any(port =>
            port.Direction == NodePortDirection.Input &&
            PortsCompatible(sourcePort, port));
    }

    private static bool PortsCompatible(NodePort fromPort, NodePort toPort)
    {
        if (fromPort.PortKind != toPort.PortKind)
        {
            return false;
        }

        return DataTypesCompatible(fromPort.DataType, toPort.DataType);
    }

    private static bool DataTypesCompatible(string from, string to)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return true;
        }

        var normalizedFrom = NormalizePaletteDataType(from);
        var normalizedTo = NormalizePaletteDataType(to);
        return normalizedFrom.Equals("Any", StringComparison.OrdinalIgnoreCase) ||
            normalizedTo.Equals("Any", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedFrom, normalizedTo, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePaletteDataType(string value)
    {
        if (value.Contains("Vector3", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Position", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Rotation", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Scale", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Direction", StringComparison.OrdinalIgnoreCase))
        {
            return "Vector3";
        }

        if (value.Contains("Color", StringComparison.OrdinalIgnoreCase))
        {
            return "Color";
        }

        if (value.Contains("Object", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Instance", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Player", StringComparison.OrdinalIgnoreCase))
        {
            return "SceneObject";
        }

        if (value.Contains("Text", StringComparison.OrdinalIgnoreCase))
        {
            return "String";
        }

        if (value.Equals("Bool", StringComparison.OrdinalIgnoreCase))
        {
            return "Boolean";
        }

        return value.Trim();
    }

}
