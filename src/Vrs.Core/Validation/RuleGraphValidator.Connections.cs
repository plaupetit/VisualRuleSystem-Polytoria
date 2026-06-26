using Vrs.Graph.Model;

namespace Vrs.Core.Validation;

public sealed partial class RuleGraphValidator
{
    // Connection validation stays separate because it depends on edit-service
    // endpoint rules shared with graph authoring commands.
    private void ValidateConnections(Rule rule, IReadOnlySet<string> nodeIds, ValidationResult result)
    {
        foreach (var connection in rule.Connections)
        {
            if (!nodeIds.Contains(connection.From.NodeId))
            {
                Add(result, ValidationSeverity.Error, rule.Name, $"Connection source does not exist: {connection.From.NodeId}");
            }

            if (!nodeIds.Contains(connection.To.NodeId))
            {
                Add(result, ValidationSeverity.Error, rule.Name, $"Connection target does not exist: {connection.To.NodeId}");
            }

            var connectionValidation = editService.ValidateEndpointPair(
                rule,
                connection.From.NodeId,
                connection.From.PortId,
                connection.To.NodeId,
                connection.To.PortId);
            if (!connectionValidation.Success)
            {
                Add(result, ValidationSeverity.Error, rule.Name, connectionValidation.Message);
            }
        }
    }
}
