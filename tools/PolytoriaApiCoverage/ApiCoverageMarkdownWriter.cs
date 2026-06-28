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
        builder.AppendLine($"| Gameplay API types | {result.Summary.GameplayApiTypes} |");
        builder.AppendLine($"| Gameplay API types covered | {result.Summary.GameplayApiTypesWithCoverage} |");
        builder.AppendLine($"| Creator API types | {result.Summary.CreatorApiTypes} |");
        builder.AppendLine($"| Creator API types covered | {result.Summary.CreatorApiTypesWithCoverage} |");
        builder.AppendLine($"| VRS target runtime API types | {result.Summary.TargetRuntimeTypes} |");
        builder.AppendLine($"| VRS target runtime types covered | {result.Summary.TargetRuntimeTypesWithCoverage} |");
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
        builder.AppendLine($"| Gameplay API coverage | {Percent(result.Summary.GameplayApiCoveragePercent)} | {result.Summary.GameplayApiTypesWithCoverage}/{result.Summary.GameplayApiTypes} |");
        builder.AppendLine($"| Gameplay API still uncovered | {Percent(result.Summary.GameplayApiUncoveredPercent)} | {result.Summary.GameplayApiTypesWithoutCoverage}/{result.Summary.GameplayApiTypes} |");
        builder.AppendLine($"| Creator API coverage | {Percent(result.Summary.CreatorApiCoveragePercent)} | {result.Summary.CreatorApiTypesWithCoverage}/{result.Summary.CreatorApiTypes} |");
        builder.AppendLine($"| Creator API still uncovered | {Percent(result.Summary.CreatorApiUncoveredPercent)} | {result.Summary.CreatorApiTypesWithoutCoverage}/{result.Summary.CreatorApiTypes} |");
        builder.AppendLine($"| VRS target runtime coverage | {Percent(result.Summary.TargetRuntimeCoveragePercent)} | {result.Summary.TargetRuntimeTypesWithCoverage}/{result.Summary.TargetRuntimeTypes} |");
        builder.AppendLine($"| VRS target runtime still uncovered | {Percent(result.Summary.TargetRuntimeUncoveredPercent)} | {result.Summary.TargetRuntimeTypesWithoutCoverage}/{result.Summary.TargetRuntimeTypes} |");
        builder.AppendLine($"| Low-confidence / inferred catalog nodes | {Percent(result.Summary.LowConfidenceNodePercent)} | {result.Summary.LowConfidenceNodeCount}/{result.Catalog.TotalNodes} |");
        builder.AppendLine($"| Catalog nodes without API metadata | {Percent(result.Summary.NodesWithoutApiReferencePercent)} | {result.Summary.NodesWithoutApiReference}/{result.Catalog.TotalNodes} |");
        builder.AppendLine();
        builder.AppendLine("The official API percentage is type-level coverage, not member-level coverage. `Gameplay API` means runtime/player-facing APIs, including game UI. `Creator API` means editor, addon, tooling, or non-gameplay infrastructure APIs.");
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
        AppendSurfaceTable(builder, result);
        AppendCategoryTable(builder, result);
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
        AppendRoadmap(builder, result.Roadmap);
        AppendNonTargetGameplayInfrastructureSection(builder, result.TypeRows);
        AppendCreatorApiSection(builder, result.TypeRows.Where(row => row.ApiSurface.Equals("Creator", StringComparison.OrdinalIgnoreCase)));
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

        builder.AppendLine("| API type | Surface | Category | Coverage | Confidence | VRS nodes |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var row in list)
        {
            var nodes = row.Nodes.Count == 0 ? "" : string.Join("<br>", row.Nodes.Take(8).Select(Escape));
            if (row.Nodes.Count > 8)
            {
                nodes += $"<br>... {row.Nodes.Count - 8} more";
            }

            builder.AppendLine($"| `{Escape(row.Type)}` | {Escape(row.ApiSurface)} | {Escape(row.Category)} | {Escape(row.Coverage)} | {Escape(row.Confidence)} | {nodes} |");
        }

        builder.AppendLine();
    }

    private static void AppendSurfaceTable(StringBuilder builder, ApiCoverageResult result)
    {
        builder.AppendLine("## Coverage By API Surface");
        builder.AppendLine();
        builder.AppendLine("| Surface | Official types | Covered | Uncovered | Coverage |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: |");
        foreach (var group in result.TypeRows.GroupBy(row => row.ApiSurface).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var rows = group.ToList();
            var covered = rows.Count(row => !row.Coverage.Equals("Uncovered", StringComparison.OrdinalIgnoreCase));
            var uncovered = rows.Count - covered;
            builder.AppendLine($"| {Escape(group.Key)} | {rows.Count} | {covered} | {uncovered} | {Percent(covered * 100.0 / rows.Count)} |");
        }

        builder.AppendLine();
    }

    private static void AppendCategoryTable(StringBuilder builder, ApiCoverageResult result)
    {
        builder.AppendLine("## Coverage By API Family");
        builder.AppendLine();
        builder.AppendLine("| Family | Official types | Covered | Uncovered | VRS target |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: |");
        foreach (var group in result.TypeRows.GroupBy(row => row.Category).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var rows = group.ToList();
            var covered = rows.Count(row => !row.Coverage.Equals("Uncovered", StringComparison.OrdinalIgnoreCase));
            var uncovered = rows.Count - covered;
            var target = rows.Count(row => row.IsVrsTargetRuntime);
            builder.AppendLine($"| {Escape(group.Key)} | {rows.Count} | {covered} | {uncovered} | {target} |");
        }

        builder.AppendLine();
    }

    private static void AppendRoadmap(StringBuilder builder, IReadOnlyList<ApiCoverageRoadmapItem> roadmap)
    {
        builder.AppendLine("## VRS Node Coverage Roadmap");
        builder.AppendLine();
        if (roadmap.Count == 0)
        {
            builder.AppendLine("No uncovered target-runtime API types are currently prioritized.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("This generated dry-run list is limited to uncovered Gameplay API types. Creator/editor/addon APIs are tracked separately and are not part of the normal user-node roadmap.");
        builder.AppendLine();
        builder.AppendLine("| Priority | API type | Family | Suggested pack | Suggested node | Reason |");
        builder.AppendLine("| ---: | --- | --- | --- | --- | --- |");
        foreach (var item in roadmap.Take(60))
        {
            builder.AppendLine($"| {item.Priority} | `{Escape(item.Type)}` | {Escape(item.Category)} | {Escape(item.SuggestedNodePack)} | {Escape(item.SuggestedLabel)} ({Escape(item.SuggestedNodeKind)}) | {Escape(item.Reason)} |");
        }

        if (roadmap.Count > 60)
        {
            builder.AppendLine($"| ... | ... | ... | ... | ... | {roadmap.Count - 60} more rows in `api-coverage.generated.json` |");
        }

        builder.AppendLine();
    }

    private static void AppendNonTargetGameplayInfrastructureSection(StringBuilder builder, IEnumerable<TypeCoverageRow> rows)
    {
        var list = rows
            .Where(row =>
                row.ApiSurface.Equals("Gameplay", StringComparison.OrdinalIgnoreCase) &&
                row.Category.Equals("Infrastructure", StringComparison.OrdinalIgnoreCase) &&
                !row.IsVrsTargetRuntime &&
                row.Coverage.Equals("Uncovered", StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => row.Type, StringComparer.OrdinalIgnoreCase)
            .ToList();
        builder.AppendLine("## Gameplay Infrastructure Not Prioritized");
        builder.AppendLine();
        builder.AppendLine("These official Gameplay-surface types are intentionally excluded from the normal VRS target-runtime score. They are global services, containers, platform features, or advanced integration points rather than core artist-facing node graph building blocks.");
        builder.AppendLine();
        builder.AppendLine("Do not chase these rows just to raise the broad Gameplay percentage. Add them only when there is a concrete workflow, safe runtime behavior, and a clear non-scripter UX.");
        builder.AppendLine();
        if (list.Count == 0)
        {
            builder.AppendLine("None.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| API type | Coverage | Why it is not in the priority score |");
        builder.AppendLine("| --- | --- | --- |");
        foreach (var row in list)
        {
            builder.AppendLine($"| `{Escape(row.Type)}` | {Escape(row.Coverage)} | {Escape(NonTargetGameplayInfrastructureReason(row.Type))} |");
        }

        builder.AppendLine();
    }

    private static string NonTargetGameplayInfrastructureReason(string typeName)
    {
        return typeName switch
        {
            "AchievementsService" => "Useful later, but needs an achievement setup workflow before artist-facing nodes are safe.",
            "AssetsService" => "Asset object nodes are covered; service lookup nodes need a clearer file-link workflow.",
            "CaptureService" => "Screenshot/photo capture is a specialized platform feature, not core gameplay logic.",
            "Hidden" or "HiddenBase" => "Important Creator hierarchy container used for VRS paths, including managed input NetworkEvents under World/Hidden/VRS/Events/User Input (NetworkEvent)/Input Manager; direct Hidden wrapper nodes would mostly duplicate scene hierarchy selection.",
            "ServerHidden" or "Temporary" => "Server-side hidden containers are useful infrastructure, but current VRS input events use World/Hidden; direct wrapper nodes need a concrete workflow first.",
            "HttpService" => "Network requests are advanced and can be unsafe/noisy for a beginner palette without allowlists and error handling UX.",
            "InsertService" => "Runtime insertion can be useful, but needs placement, ownership, and failure handling UX first.",
            "PresenceService" => "Client presence is useful later, but it is not core rule/action gameplay behavior.",
            "PurchasesService" => "Monetization nodes need careful UX, testing, and guardrails before being exposed.",
            "ScriptService" => "Script storage is handled by VRS deploy/link workflows, not normal gameplay nodes.",
            "SocialService" or "WorldsService" => "Documented as WIP or platform-level flow; keep out until a concrete safe workflow exists.",
            _ => "Infrastructure or platform-level API; add only with a concrete workflow."
        };
    }

    private static void AppendCreatorApiSection(StringBuilder builder, IEnumerable<TypeCoverageRow> rows)
    {
        var list = rows.OrderBy(row => row.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Type, StringComparer.OrdinalIgnoreCase)
            .ToList();
        builder.AppendLine("## Creator / Non-Gameplay APIs");
        builder.AppendLine();
        builder.AppendLine("These official types are tracked for transparency, but they are not part of the normal user-node roadmap unless a future Creator/tooling workflow needs them.");
        builder.AppendLine();
        if (list.Count == 0)
        {
            builder.AppendLine("None.");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("| API type | Family | Coverage | Confidence |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var row in list)
        {
            builder.AppendLine($"| `{Escape(row.Type)}` | {Escape(row.Category)} | {Escape(row.Coverage)} | {Escape(row.Confidence)} |");
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
