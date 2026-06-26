namespace Vrs.Core.Export;

/// <summary>
/// Centralizes generated Luau comment ownership so exported scripts can clearly
/// separate editor-generated context from author-written notes.
/// </summary>
public static class LuauCommentTags
{
    public const string Vsr = "[VSR]";
    public const string User = "[User]";

    public static string VsrComment(string text)
    {
        return CommentLines(0, Vsr, text);
    }

    public static string UserComment(string text)
    {
        return CommentLines(0, User, text);
    }

    public static string IndentedVsrComment(int indentLevel, string text)
    {
        return CommentLines(indentLevel, Vsr, text);
    }

    public static string IndentedUserComment(int indentLevel, string text)
    {
        return CommentLines(indentLevel, User, text);
    }

    private static string CommentLines(int indentLevel, string tag, string text)
    {
        var indent = Indent(indentLevel);
        var normalized = (text ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n');
        return string.Join(Environment.NewLine, lines.Select(line => $"{indent}-- {tag} {line}"));
    }

    private static string Indent(int indentLevel)
    {
        return new string(' ', Math.Max(0, indentLevel) * 4);
    }
}
