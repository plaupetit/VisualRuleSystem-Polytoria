namespace Vrs.Core.Export;

public enum LuauSyntaxTokenKind
{
    Comment,
    VsrTag,
    UserTag,
    Keyword,
    String,
    Number,
    ApiName,
    Identifier,
    Operator,
    Whitespace
}

public readonly record struct LuauSyntaxToken(LuauSyntaxTokenKind Kind, int Start, int Length, string Text);

/// <summary>
/// Lightweight Luau lexer shared by the Avalonia preview and HTML export.
/// It is deliberately small and non-validating: highlighting must never alter
/// or reject executable script text.
/// </summary>
public static class LuauSyntaxClassifier
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "and",
        "break",
        "do",
        "else",
        "elseif",
        "end",
        "false",
        "for",
        "function",
        "if",
        "in",
        "local",
        "nil",
        "not",
        "or",
        "repeat",
        "return",
        "then",
        "true",
        "until",
        "while"
    };

    public static IReadOnlyList<LuauSyntaxToken> Classify(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var tokens = new List<LuauSyntaxToken>();
        var index = 0;
        while (index < text.Length)
        {
            var ch = text[index];
            if (char.IsWhiteSpace(ch))
            {
                index = AddRun(text, tokens, index, LuauSyntaxTokenKind.Whitespace, char.IsWhiteSpace);
                continue;
            }

            if (ch == '-' && index + 1 < text.Length && text[index + 1] == '-')
            {
                index = AddComment(text, tokens, index);
                continue;
            }

            if (ch == '"' || ch == '\'')
            {
                index = AddString(text, tokens, index, ch);
                continue;
            }

            if (char.IsDigit(ch))
            {
                index = AddNumber(text, tokens, index);
                continue;
            }

            if (IsIdentifierStart(ch))
            {
                index = AddIdentifier(text, tokens, index);
                continue;
            }

            tokens.Add(new LuauSyntaxToken(LuauSyntaxTokenKind.Operator, index, 1, text.Substring(index, 1)));
            index++;
        }

        return tokens;
    }

    private static int AddComment(string text, List<LuauSyntaxToken> tokens, int start)
    {
        var end = start;
        while (end < text.Length && text[end] != '\r' && text[end] != '\n')
        {
            end++;
        }

        var tagStart = FindTagStart(text, start, end);
        if (tagStart < 0)
        {
            tokens.Add(new LuauSyntaxToken(LuauSyntaxTokenKind.Comment, start, end - start, text[start..end]));
            return end;
        }

        if (tagStart > start)
        {
            tokens.Add(new LuauSyntaxToken(LuauSyntaxTokenKind.Comment, start, tagStart - start, text[start..tagStart]));
        }

        var tag = text.AsSpan(tagStart).StartsWith(LuauCommentTags.Vsr, StringComparison.Ordinal)
            ? LuauCommentTags.Vsr
            : LuauCommentTags.User;
        tokens.Add(new LuauSyntaxToken(
            tag == LuauCommentTags.Vsr ? LuauSyntaxTokenKind.VsrTag : LuauSyntaxTokenKind.UserTag,
            tagStart,
            tag.Length,
            tag));

        var remainderStart = tagStart + tag.Length;
        if (remainderStart < end)
        {
            tokens.Add(new LuauSyntaxToken(LuauSyntaxTokenKind.Comment, remainderStart, end - remainderStart, text[remainderStart..end]));
        }

        return end;
    }

    private static int FindTagStart(string text, int start, int end)
    {
        var comment = text.AsSpan(start, end - start);
        var vsr = comment.IndexOf(LuauCommentTags.Vsr, StringComparison.Ordinal);
        var user = comment.IndexOf(LuauCommentTags.User, StringComparison.Ordinal);
        return (vsr, user) switch
        {
            (>= 0, >= 0) => start + Math.Min(vsr, user),
            (>= 0, _) => start + vsr,
            (_, >= 0) => start + user,
            _ => -1
        };
    }

    private static int AddString(string text, List<LuauSyntaxToken> tokens, int start, char quote)
    {
        var end = start + 1;
        var escaped = false;
        while (end < text.Length)
        {
            var ch = text[end++];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (ch == quote)
            {
                break;
            }
        }

        tokens.Add(new LuauSyntaxToken(LuauSyntaxTokenKind.String, start, end - start, text[start..end]));
        return end;
    }

    private static int AddNumber(string text, List<LuauSyntaxToken> tokens, int start)
    {
        var end = start;
        var hasDot = false;
        while (end < text.Length)
        {
            var ch = text[end];
            if (char.IsDigit(ch) || ch == '_')
            {
                end++;
                continue;
            }

            if (ch == '.' && !hasDot && end + 1 < text.Length && char.IsDigit(text[end + 1]))
            {
                hasDot = true;
                end++;
                continue;
            }

            break;
        }

        tokens.Add(new LuauSyntaxToken(LuauSyntaxTokenKind.Number, start, end - start, text[start..end]));
        return end;
    }

    private static int AddIdentifier(string text, List<LuauSyntaxToken> tokens, int start)
    {
        var end = start + 1;
        while (end < text.Length && IsIdentifierPart(text[end]))
        {
            end++;
        }

        var value = text[start..end];
        var kind = Keywords.Contains(value)
            ? LuauSyntaxTokenKind.Keyword
            : IsApiName(text, start, value)
                ? LuauSyntaxTokenKind.ApiName
                : LuauSyntaxTokenKind.Identifier;
        tokens.Add(new LuauSyntaxToken(kind, start, end - start, value));
        return end;
    }

    private static bool IsApiName(string text, int start, string value)
    {
        var previous = PreviousNonWhitespace(text, start);
        return previous is '.' or ':' || char.IsUpper(value[0]);
    }

    private static char PreviousNonWhitespace(string text, int start)
    {
        for (var index = start - 1; index >= 0; index--)
        {
            if (!char.IsWhiteSpace(text[index]))
            {
                return text[index];
            }
        }

        return '\0';
    }

    private static int AddRun(string text, List<LuauSyntaxToken> tokens, int start, LuauSyntaxTokenKind kind, Func<char, bool> predicate)
    {
        var end = start + 1;
        while (end < text.Length && predicate(text[end]))
        {
            end++;
        }

        tokens.Add(new LuauSyntaxToken(kind, start, end - start, text[start..end]));
        return end;
    }

    private static bool IsIdentifierStart(char ch)
    {
        return char.IsLetter(ch) || ch == '_';
    }

    private static bool IsIdentifierPart(char ch)
    {
        return char.IsLetterOrDigit(ch) || ch == '_';
    }
}
