namespace Vrs.Core.Export;

/// <summary>
/// Portable color palette used by both the in-app preview and generated HTML.
/// Colors are inspired by the Polytoria Creator 2.0 code editor screenshot,
/// without copying any official theme resource.
/// </summary>
public sealed class LuauSyntaxTheme
{
    public string BackgroundHex { get; init; } = "#1f1f1f";
    public string GutterHex { get; init; } = "#252526";
    public string NormalHex { get; init; } = "#d7d7d7";
    public string KeywordHex { get; init; } = "#ef7f88";
    public string StringHex { get; init; } = "#e5c07b";
    public string NumberHex { get; init; } = "#d19a66";
    public string CommentHex { get; init; } = "#8b949e";
    public string ApiNameHex { get; init; } = "#d7ba7d";
    public string IdentifierHex { get; init; } = "#d7d7d7";
    public string OperatorHex { get; init; } = "#b8b8b8";
    public string VsrTagHex { get; init; } = "#56cfe1";
    public string UserTagHex { get; init; } = "#8fd694";

    public static LuauSyntaxTheme PolytoriaLike { get; } = new();

    public string ColorFor(LuauSyntaxTokenKind kind)
    {
        return kind switch
        {
            LuauSyntaxTokenKind.Comment => CommentHex,
            LuauSyntaxTokenKind.VsrTag => VsrTagHex,
            LuauSyntaxTokenKind.UserTag => UserTagHex,
            LuauSyntaxTokenKind.Keyword => KeywordHex,
            LuauSyntaxTokenKind.String => StringHex,
            LuauSyntaxTokenKind.Number => NumberHex,
            LuauSyntaxTokenKind.ApiName => ApiNameHex,
            LuauSyntaxTokenKind.Operator => OperatorHex,
            LuauSyntaxTokenKind.Identifier => IdentifierHex,
            _ => NormalHex
        };
    }
}
