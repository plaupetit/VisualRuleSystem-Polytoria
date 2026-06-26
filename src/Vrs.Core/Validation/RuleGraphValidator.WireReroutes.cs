using Vrs.Graph.Model;

namespace Vrs.Core.Validation;

public sealed partial class RuleGraphValidator
{
    private static void ValidateWireReroutes(Rule rule, ValidationResult result)
    {
        var duplicateIds = rule.WireReroutes
            .Where(reroute => !string.IsNullOrWhiteSpace(reroute.Id))
            .GroupBy(reroute => reroute.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);
        foreach (var duplicate in duplicateIds)
        {
            Add(result, ValidationSeverity.Error, rule.Name, $"Duplicate wire reroute id: {duplicate.Key}");
        }

        var rerouteIds = rule.WireReroutes
            .Select(reroute => reroute.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var reroute in rule.WireReroutes)
        {
            if (!WireRerouteDirection.IsValid(reroute.InputDirection))
            {
                Add(result, ValidationSeverity.Error, rule.Name, $"Wire reroute has invalid input direction: {reroute.Id}");
            }

            if (!WireRerouteDirection.IsValid(reroute.OutputDirection))
            {
                Add(result, ValidationSeverity.Error, rule.Name, $"Wire reroute has invalid output direction: {reroute.Id}");
            }
        }

        var referencedReroutes = new List<string>();
        foreach (var connection in rule.Connections)
        {
            foreach (var missingRerouteId in connection.RerouteIds.Where(id => !rerouteIds.Contains(id)))
            {
                Add(result, ValidationSeverity.Error, rule.Name, $"Connection references a missing wire reroute: {missingRerouteId}");
            }

            foreach (var duplicateReference in connection.RerouteIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1))
            {
                Add(result, ValidationSeverity.Error, rule.Name, $"Connection references the same wire reroute more than once: {duplicateReference.Key}");
            }

            referencedReroutes.AddRange(connection.RerouteIds.Where(id => !string.IsNullOrWhiteSpace(id)));
        }

        foreach (var sharedReroute in referencedReroutes
            .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            Add(result, ValidationSeverity.Error, rule.Name, $"Wire reroute belongs to more than one connection: {sharedReroute.Key}");
        }

        foreach (var unused in rerouteIds.Where(id => !referencedReroutes.Contains(id, StringComparer.OrdinalIgnoreCase)))
        {
            Add(result, ValidationSeverity.Warning, rule.Name, $"Wire reroute is not attached to a connection: {unused}");
        }
    }
}
