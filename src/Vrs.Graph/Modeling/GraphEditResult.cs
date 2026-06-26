namespace Vrs.Graph.Modeling;

public sealed class GraphEditResult
{
    public bool Success { get; set; }
    public bool Changed { get; set; }
    public string Message { get; set; } = "";

    public static GraphEditResult Ok(string message, bool changed = true)
    {
        return new GraphEditResult
        {
            Success = true,
            Changed = changed,
            Message = message
        };
    }

    public static GraphEditResult Fail(string message)
    {
        return new GraphEditResult
        {
            Success = false,
            Message = message
        };
    }
}
