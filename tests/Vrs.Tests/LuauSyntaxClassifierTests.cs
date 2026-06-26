using Vrs.Core.Export;

namespace Vrs.Tests;

public sealed class LuauSyntaxClassifierTests
{
    [Fact]
    public void Classify_MarksLuauKeywordsTagsStringsNumbersAndComments()
    {
        const string luau = """
        -- [VSR] generated
        local name = "Cube" .. tostring(42)
        if target == nil then
            return
        end
        -- [User] note
        """;

        var tokens = LuauSyntaxClassifier.Classify(luau);

        Assert.Contains(tokens, token => token.Kind == LuauSyntaxTokenKind.VsrTag && token.Text == "[VSR]");
        Assert.Contains(tokens, token => token.Kind == LuauSyntaxTokenKind.UserTag && token.Text == "[User]");
        Assert.Contains(tokens, token => token.Kind == LuauSyntaxTokenKind.Comment && token.Text.Contains("generated", StringComparison.Ordinal));
        Assert.Contains(tokens, token => token.Kind == LuauSyntaxTokenKind.String && token.Text == "\"Cube\"");
        Assert.Contains(tokens, token => token.Kind == LuauSyntaxTokenKind.Number && token.Text == "42");

        foreach (var keyword in new[] { "local", "if", "then", "return", "end" })
        {
            Assert.Contains(tokens, token => token.Kind == LuauSyntaxTokenKind.Keyword && token.Text == keyword);
        }
    }

    [Fact]
    public void ExportToHtml_EscapesCodeAndKeepsColorSpans()
    {
        const string luau = "-- [VSR] <generated>\nlocal s = \"<tag>&\\\"\"";

        var html = LuauHtmlExporter.ExportToHtml(luau, LuauSyntaxTheme.PolytoriaLike, "Luau <Preview>");

        Assert.Contains("Luau &lt;Preview&gt;", html);
        Assert.Contains("&lt;generated&gt;", html);
        Assert.Contains("&lt;tag&gt;&amp;", html);
        Assert.Contains("style=\"color:#56cfe1\"", html);
        Assert.DoesNotContain("<generated>", html);
        Assert.DoesNotContain("<tag>&", html);
    }
}
