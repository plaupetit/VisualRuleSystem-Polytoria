namespace Vrs.Core.Validation;

public sealed partial class RuleGraphValidator
{
    private static void Add(ValidationResult result, ValidationSeverity severity, string scope, string message)
    {
        result.Messages.Add(new ValidationMessage
        {
            Severity = severity,
            Scope = string.IsNullOrWhiteSpace(scope) ? "Graph" : scope,
            Message = message
        });
    }
}
