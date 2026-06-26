using System.Net;
using System.Text;

namespace Vrs.Core.Export;

public static class LuauHtmlExporter
{
    public static string ExportToHtml(string luau, LuauSyntaxTheme? theme = null, string title = "Generated Luau")
    {
        theme ??= LuauSyntaxTheme.PolytoriaLike;
        var encodedTitle = WebUtility.HtmlEncode(title);
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("  <meta charset=\"utf-8\">");
        builder.AppendLine($"  <title>{encodedTitle}</title>");
        builder.AppendLine("  <style>");
        builder.AppendLine($"    body {{ margin: 0; background: {theme.BackgroundHex}; color: {theme.NormalHex}; }}");
        builder.AppendLine("    pre { margin: 0; padding: 18px 22px; overflow: auto; font: 13px/1.55 Consolas, 'Cascadia Mono', 'JetBrains Mono', monospace; tab-size: 4; }");
        builder.AppendLine("  </style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.Append("<pre>");
        foreach (var token in LuauSyntaxClassifier.Classify(luau))
        {
            var encoded = WebUtility.HtmlEncode(token.Text);
            if (token.Kind == LuauSyntaxTokenKind.Whitespace)
            {
                builder.Append(encoded);
                continue;
            }

            builder.Append("<span style=\"color:");
            builder.Append(theme.ColorFor(token.Kind));
            builder.Append("\">");
            builder.Append(encoded);
            builder.Append("</span>");
        }

        builder.AppendLine("</pre>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }
}
