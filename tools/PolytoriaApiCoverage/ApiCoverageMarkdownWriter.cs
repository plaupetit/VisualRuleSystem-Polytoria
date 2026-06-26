using System.Text;
using System.Globalization;

namespace Vrs.Tools.PolytoriaApiCoverage;

public static class ApiCoverageMarkdownWriter
{
    public static string Write(ApiCoverageResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Polytoria API Coverage");
        builder.AppendLine();
        builder.AppendLine("This report is generated from public Polytoria API sources and the VRS node catalog.");
        builder.AppendLine("Do not treat the percentages as a promise that every runtime behavior is implemented or tested.");
        builder.AppendLine();
        builder.AppendLine("## Sources");
        builder.AppendLine();
        builder.AppendLine($"- Generated UTC: `{result.GeneratedAtUtc:O}`");
        builder.AppendLine($"- Docs-v2: `{ShortSha(result.Source.DocsCommit)}`{DateSuffix(result.Source.DocsCommitDate)} from {result.Source.DocsSource}");
        builder.AppendLine($"- lua-definitions: `{ShortSha(result.Source.LuaDefinitionsCommit)}`{DateSuffix(result.Source.LuaDefinitionsCommitDate)} from {result.Source.LuaDefinitionsSource}");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine("| Metric | Count |");
        builder.AppendLine("| --- | ---: |");
        builder.AppendLine($"| Official API types | {result.Summary.OfficialTypes} |");
        builder.AppendLine($"| Official API enums | {result.Summary.OfficialEnums} |");
        builder.AppendLine($"| Official globals | {result.Summary.OfficialGlobals} |");
        builder.AppendLine($"| VRS catalog nodes | {result.Catalog.TotalNodes} |");
        builder.AppendLine($"| API types with any VRS coverage | {result.Summary.TypesWithAnyCoverage} |");
        builder.AppendLine($"| API types without VRS coverage | {result.Summary.TypesWithoutCoverage} |");
        builder.AppendLine($"| Low-confidence / inferred catalog nodes | {result.Summary.LowConfidenceNodeCount} |");
        builder.AppendLine($"| Catalog nodes without API metadata | {result.Summary.NodesWithoutApiReference} |");
        builder.AppendLine();
        builder.AppendLine("## Coverage Percentages");
        builder.AppendLine();
        builder.AppendLine("| Metric | Percentage | Fraction |");
        builder.AppendLine("| --- | ---: | ---: |");
        builder.AppendLine($"| API types with any VRS coverage | {Percent(result.Summary.TypesWithAnyCoveragePercent)} | {result.Summary.TypesWithAnyCoverage}/{result.Summary.OfficialTypes} |");
        builder.AppendLine($"| API types without VRS coverage | {Percent(result.Summary.TypesWithoutCoveragePercent)} | {result.Summary.TypesWithoutCoverage}/{result.Summary.OfficialTypes} |");
        builder.AppendLine($"| Direct API type coverage | {Percent(result.Summary.DirectTypePercent)} | {result.Summary.DirectTypeCount}/{result.Summary.OfficialTypes} |");
        builder.AppendLine($"| Partial API type coverage | {Percent(result.Summary.PartialTypePercent)} | {result.Summary.PartialTypeCount}/{result.Summary.OfficialTypes} |");
        builder.AppendLine($"| Indirect or synthetic API type coverage | {Percent(result.Summary.IndirectOrSyntheticTypePercent)} | {result.Summary.IndirectOrSyntheticTypeCount}/{result.Summary.OfficialTypes} |");
        builder.AppendLine($"| Inferred API type coverage | {Percent(result.Summary.InferredTypePercent)} | {result.Summary.InferredTypeCount}/{result.Summary.OfficialTypes} |");
        builder.AppendLine($"| Low-confidence / inferred catalog nodes | {Percent(result.Summary.LowConfidenceNodePercent)} | {result.Summary.LowConfidenceNodeCount}/{result.Catalog.TotalNodes} |");
        builder.AppendLine($"| Catalog nodes without API metadata | {Percent(result.Summary.NodesWithoutApiReferencePercent)} | {result.Summary.NodesWithoutApiReference}/{result.Catalog.TotalNodes} |");
        builder.AppendLine();
        builder.AppendLine("The main coverage percentage is type-level coverage, not member-level coverage. It does not yet count every property, method, or event separately.");
        builder.AppendLine();
        builder.AppendLine("## Catalog Nodes By Kind");
        builder.AppendLine();
        builder.AppendLine("| Kind | Count |");
        builder.AppendLine("| --- | ---: |");
        foreach (var item in result.Catalog.NodesByKind.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"| {Escape(item.Key)} | {item.Value} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Type Coverage");
        builder.AppendLine();
        builder.AppendLine("| Coverage | Types |");
        builder.AppendLine("| --- | ---: |");
        builder.AppendLine($"| Direct | {result.Summary.DirectTypeCount} |");
        builder.AppendLine($"| Partial | {result.Summary.PartialTypeCount} |");
        builder.AppendLine($"| Indirect or synthetic | {result.Summary.IndirectOrSyntheticTypeCount} |");
        builder.AppendLine($"| Inferred from `apiType` | {result.Summary.InferredTypeCount} |");
        builder.AppendLine($"| Uncovered | {result.Summary.TypesWithoutCoverage} |");
        builder.AppendLine();
        builder.AppendLine("Coverage categories:");
        builder.AppendLine();
        builder.AppendLine("- `Direct`: a node explicitly maps to a documented type/member.");
        builder.AppendLine("- `Partial`: a node covers only part of the official behavior.");
        builder.AppendLine("- `Indirect` / `Synthetic`: a node is a VRS workflow/helper, not a 1:1 API wrapper.");
        builder.AppendLine("- `Inferred`: the report could not match `apiType` to an official or synthetic reference and kept it as a weak guess.");
        builder.AppendLine("- `Uncovered`: no VRS node currently maps to that official type.");
        builder.AppendLine();
        builder.AppendLine("Confidence labels:");
        builder.AppendLine();
        builder.AppendLine("- `Explicit`: the catalog node has hand-written `apiReferences`.");
        builder.AppendLine("- `AutoVerified`: `apiType` matched an official Docs-v2 type, method, property, event, enum, or global.");
        builder.AppendLine("- `AutoClassified`: `apiType` was intentionally classified as a Lua primitive or VRS helper, not official Polytoria API coverage.");
        builder.AppendLine("- `Mixed`: the type row combines multiple confidence levels.");
        builder.AppendLine("- `Low` / `Inferred`: the node needs manual annotation or correction.");
        builder.AppendLine();
        AppendTypeTable(builder, "Covered Types", result.TypeRows.Where(row => row.Coverage != "Uncovered"));
        AppendTypeTable(builder, "Uncovered Types", result.TypeRows.Where(row => row.Coverage == "Uncovered"));

        builder.AppendLine("## Low Confidence / Needs Annotation");
        builder.AppendLine();
        var lowConfidence = result.NodeRows
            .Where(row => row.Confidence is "Low" or "Inferred")
            .OrderBy(row => row.NodeId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (lowConfidence.Count == 0)
        {
            builder.AppendLine("No low-confidence nodes were found.");
        }
        else
        {
            builder.AppendLine("These nodes should gain explicit `apiReferences` over time. The JSON report contains the full machine-readable list.");
            builder.AppendLine();
            builder.AppendLine("| Node | Kind | Coverage | Reason |");
            builder.AppendLine("| --- | --- | --- | --- |");
            foreach (var row in lowConfidence.Take(80))
            {
                builder.AppendLine($"| `{Escape(row.NodeId)}` {Escape(row.Label)} | {Escape(row.NodeKind)} | {Escape(row.Coverage)} | {Escape(row.Reason)} |");
            }

            if (lowConfidence.Count > 80)
            {
                builder.AppendLine($"| ... | ... | ... | {lowConfidence.Count - 80} more rows in `api-coverage.generated.json` |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Notes");
        builder.AppendLine();
        builder.AppendLine("- VRS nodes are human workflow nodes. One node can touch several API members, and one API member can require several nodes.");
        builder.AppendLine("- The report uses GitHub-hosted Polytoria documentation sources because the rendered documentation site may be protected by browser checks.");
        builder.AppendLine("- Regenerate this file after catalog changes or when Polytoria updates its public API documentation.");
        return builder.ToString();
    }

    private static void AppendTypeTable(StringBuilder builder, string title, IEnumerable<TypeCoverageRow> rows)
    {
        var list = rows.OrderBy(row => row.Type, StringComparer.OrdinalIgnoreCase).ToList();
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        if (list.Count == 0)
        {
            builder.AppendLine("None.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| API type | Coverage | Confidence | VRS nodes |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var row in list)
        {
            var nodes = row.Nodes.Count == 0 ? "" : string.Join("<br>", row.Nodes.Take(8).Select(Escape));
            if (row.Nodes.Count > 8)
            {
                nodes += $"<br>... {row.Nodes.Count - 8} more";
            }

            builder.AppendLine($"| `{Escape(row.Type)}` | {Escape(row.Coverage)} | {Escape(row.Confidence)} | {nodes} |");
        }

        builder.AppendLine();
    }

    private static string Percent(double value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{value:0.##}%");
    }

    private static string DateSuffix(DateTimeOffset? date)
    {
        return date is null ? "" : $" ({date:yyyy-MM-dd})";
    }

    private static string ShortSha(string sha)
    {
        return sha.Length <= 12 ? sha : sha[..12];
    }

    private static string Escape(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }
}
