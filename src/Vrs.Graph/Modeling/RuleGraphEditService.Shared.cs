using Vrs.Graph.Model;

namespace Vrs.Graph.Modeling;

public sealed partial class RuleGraphEditService
{
    // Small identifier helpers shared by node, connection, and fragment mutations.
    private static string SanitizeId(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray();
        var sanitized = new string(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "Node" : sanitized;
    }
}
