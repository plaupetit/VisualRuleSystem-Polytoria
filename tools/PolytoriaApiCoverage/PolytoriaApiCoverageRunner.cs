using System.Text.Json;
using Vrs.Core.Catalog;

namespace Vrs.Tools.PolytoriaApiCoverage;

public sealed class PolytoriaApiCoverageRunner
{
    public async Task<ApiCoverageResult> RunAsync(CoverageToolOptions options, CancellationToken cancellationToken)
    {
        var source = options.SourceDirectory is null
            ? await new GitHubPolytoriaApiSource().LoadAsync(cancellationToken)
            : await new LocalPolytoriaApiSource(options.SourceDirectory).LoadAsync(cancellationToken);

        var result = PolytoriaApiCoverageAnalyzer.Generate(source, options.CatalogRoot, DateTimeOffset.UtcNow);
        Directory.CreateDirectory(options.OutputDirectory);

        var markdownPath = Path.Combine(options.OutputDirectory, "API_COVERAGE.md");
        var jsonPath = Path.Combine(options.OutputDirectory, "api-coverage.generated.json");
        await File.WriteAllTextAsync(markdownPath, ApiCoverageMarkdownWriter.Write(result), cancellationToken);
        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(result, ApiCoverageJsonContext.Default.ApiCoverageResult),
            cancellationToken);

        return result;
    }
}

public static class PolytoriaApiCoverageAnalyzer
{
    public static ApiCoverageResult Generate(PolytoriaApiSourceSnapshot source, string catalogRoot, DateTimeOffset generatedAtUtc)
    {
        var catalog = new NodeCatalogService().LoadCatalog(catalogRoot);
        var officialTypes = source.Types.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var officialEnums = source.Enums.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var officialGlobals = source.Globals.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var rows = catalog.Nodes.Select(node => ToNodeCoverageRow(node, officialTypes, officialEnums, officialGlobals)).ToList();
        var typeRows = BuildTypeRows(source.Types, rows);
        var catalogSummary = new CatalogSummary(
            catalog.Nodes.Count,
            catalog.Nodes
                .GroupBy(node => node.Kind.ToString())
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase));

        var directTypeCount = typeRows.Count(row => row.Coverage.Equals("Direct", StringComparison.OrdinalIgnoreCase));
        var partialTypeCount = typeRows.Count(row => row.Coverage.Equals("Partial", StringComparison.OrdinalIgnoreCase));
        var indirectOrSyntheticTypeCount = typeRows.Count(row => row.Coverage is "Indirect" or "Synthetic");
        var inferredTypeCount = typeRows.Count(row => row.Coverage.Equals("Inferred", StringComparison.OrdinalIgnoreCase));
        var typesWithAnyCoverage = typeRows.Count(row => !row.Coverage.Equals("Uncovered", StringComparison.OrdinalIgnoreCase));
        var typesWithoutCoverage = typeRows.Count(row => row.Coverage.Equals("Uncovered", StringComparison.OrdinalIgnoreCase));
        var lowConfidenceNodeCount = rows.Count(row => row.Confidence.Equals("Low", StringComparison.OrdinalIgnoreCase) || row.Confidence.Equals("Inferred", StringComparison.OrdinalIgnoreCase));
        var nodesWithoutApiReference = rows.Count(row => row.Coverage.Equals("NoReference", StringComparison.OrdinalIgnoreCase));
        var summary = new CoverageSummary(
            source.Types.Count,
            source.Enums.Count,
            source.Globals.Count,
            typesWithAnyCoverage,
            typesWithoutCoverage,
            directTypeCount,
            partialTypeCount,
            indirectOrSyntheticTypeCount,
            inferredTypeCount,
            lowConfidenceNodeCount,
            nodesWithoutApiReference,
            Percent(typesWithAnyCoverage, source.Types.Count),
            Percent(typesWithoutCoverage, source.Types.Count),
            Percent(directTypeCount, source.Types.Count),
            Percent(partialTypeCount, source.Types.Count),
            Percent(indirectOrSyntheticTypeCount, source.Types.Count),
            Percent(inferredTypeCount, source.Types.Count),
            Percent(lowConfidenceNodeCount, catalog.Nodes.Count),
            Percent(nodesWithoutApiReference, catalog.Nodes.Count));

        return new ApiCoverageResult(
            source,
            generatedAtUtc,
            catalogSummary,
            summary,
            typeRows,
            rows.Select(row => MarkUnknownReferences(row, officialTypes)).ToList(),
            catalog.Warnings);
    }

    private static double Percent(int value, int total)
    {
        return total <= 0 ? 0.0 : Math.Round(value * 100.0 / total, 2, MidpointRounding.AwayFromZero);
    }

    private static CatalogNodeCoverageRow ToNodeCoverageRow(
        NodeCatalogEntry node,
        IReadOnlyDictionary<string, PolytoriaApiType> officialTypes,
        IReadOnlyDictionary<string, PolytoriaApiEnum> officialEnums,
        IReadOnlyDictionary<string, PolytoriaApiGlobal> officialGlobals)
    {
        if (node.ApiReferences.Count > 0)
        {
            var coverage = StrongestCoverage(node.ApiReferences.Select(reference => NormalizeCoverage(reference.Coverage)));
            return new CatalogNodeCoverageRow(
                node.IdBase,
                node.Kind.ToString(),
                node.Label,
                node.ApiType,
                node.ApiReferences,
                coverage,
                "Explicit",
                "Uses explicit catalog apiReferences.");
        }

        if (!string.IsNullOrWhiteSpace(node.ApiType))
        {
            var inferred = InferReference(node.ApiType, officialTypes, officialEnums, officialGlobals);
            return new CatalogNodeCoverageRow(
                node.IdBase,
                node.Kind.ToString(),
                node.Label,
                node.ApiType,
                [inferred.Reference],
                inferred.Coverage,
                inferred.Confidence,
                inferred.Reason);
        }

        return new CatalogNodeCoverageRow(
            node.IdBase,
            node.Kind.ToString(),
            node.Label,
            node.ApiType,
            [],
            "NoReference",
            "Low",
            "No apiReferences or apiType metadata.");
    }

    private static CatalogNodeCoverageRow MarkUnknownReferences(
        CatalogNodeCoverageRow row,
        IReadOnlyDictionary<string, PolytoriaApiType> officialTypes)
    {
        if (row.References.Count == 0 || row.Coverage.Equals("NoReference", StringComparison.OrdinalIgnoreCase))
        {
            return row;
        }

        if (row.References.Any(reference =>
            !string.IsNullOrWhiteSpace(reference.Type) &&
            !officialTypes.ContainsKey(reference.Type) &&
            !reference.MemberKind.Equals("Global", StringComparison.OrdinalIgnoreCase) &&
            !reference.MemberKind.Equals("Enum", StringComparison.OrdinalIgnoreCase) &&
            !reference.MemberKind.Equals("Primitive", StringComparison.OrdinalIgnoreCase) &&
            !reference.MemberKind.Equals("VRS", StringComparison.OrdinalIgnoreCase) &&
            !reference.MemberKind.Equals("Helper", StringComparison.OrdinalIgnoreCase)))
        {
            return row with
            {
                Confidence = row.Confidence.Equals("Explicit", StringComparison.OrdinalIgnoreCase) ? "Low" : row.Confidence,
                Reason = $"{row.Reason} One or more referenced types were not found in Docs-v2."
            };
        }

        return row;
    }

    private static IReadOnlyList<TypeCoverageRow> BuildTypeRows(
        IReadOnlyList<PolytoriaApiType> officialTypes,
        IReadOnlyList<CatalogNodeCoverageRow> rows)
    {
        var byType = rows
            .SelectMany(row => row.References.Select(reference => new { Row = row, Reference = reference }))
            .Where(item => !string.IsNullOrWhiteSpace(item.Reference.Type))
            .GroupBy(item => item.Reference.Type, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Row).ToList(),
                StringComparer.OrdinalIgnoreCase);

        return officialTypes
            .Select(type =>
            {
                if (!byType.TryGetValue(type.Name, out var nodeRows))
                {
                    return new TypeCoverageRow(type.Name, "Uncovered", "None", []);
                }

                var coverage = StrongestCoverage(nodeRows.Select(row => row.Coverage));
                var confidence = TypeRowConfidence(nodeRows);
                var nodeLabels = nodeRows
                    .Select(row => $"{row.NodeId} ({row.Label})")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new TypeCoverageRow(type.Name, coverage, confidence, nodeLabels);
            })
            .OrderBy(row => row.Type, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string TypeRowConfidence(IReadOnlyList<CatalogNodeCoverageRow> nodeRows)
    {
        if (nodeRows.Any(row => row.Confidence is "Low" or "Inferred"))
        {
            return "Mixed";
        }

        var distinct = nodeRows
            .Select(row => row.Confidence)
            .Where(confidence => !string.IsNullOrWhiteSpace(confidence))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinct.Count == 1)
        {
            return distinct[0];
        }

        return "Mixed";
    }

    private static InferredApiReference InferReference(
        string apiType,
        IReadOnlyDictionary<string, PolytoriaApiType> officialTypes,
        IReadOnlyDictionary<string, PolytoriaApiEnum> officialEnums,
        IReadOnlyDictionary<string, PolytoriaApiGlobal> officialGlobals)
    {
        var trimmed = apiType.Trim();
        if (TryClassifySynthetic(trimmed, out var syntheticReference))
        {
            return new InferredApiReference(
                syntheticReference,
                "Synthetic",
                "AutoClassified",
                "apiType describes a VRS helper or Lua/graph primitive rather than a Polytoria API member.");
        }

        var parts = apiType.Split('.', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            var typeName = parts[0];
            var memberName = parts[1];
            if (officialTypes.TryGetValue(typeName, out var apiTypeInfo))
            {
                var memberKind = FindMemberKind(apiTypeInfo, memberName);
                if (!string.IsNullOrWhiteSpace(memberKind))
                {
                    return new InferredApiReference(
                        new NodeCatalogApiReference
                        {
                            Type = typeName,
                            Member = memberName,
                            MemberKind = memberKind,
                            Coverage = "Direct"
                        },
                        "Direct",
                        "AutoVerified",
                        "apiType matched a documented Polytoria type member.");
                }

                return new InferredApiReference(
                    new NodeCatalogApiReference
                    {
                        Type = typeName,
                        Member = memberName,
                        MemberKind = "Member",
                        Coverage = "Inferred"
                    },
                    "Inferred",
                    "Low",
                    "apiType matched a documented type, but the member was not found in Docs-v2.");
            }

            return new InferredApiReference(
                new NodeCatalogApiReference
                {
                    Type = typeName,
                    Member = memberName,
                    MemberKind = "Member",
                    Coverage = "Inferred"
                },
                "Inferred",
                "Low",
                "apiType looked like Type.Member, but the type was not found in Docs-v2.");
        }

        if (officialTypes.ContainsKey(trimmed))
        {
            return new InferredApiReference(
                new NodeCatalogApiReference
                {
                    Type = trimmed,
                    MemberKind = "Type",
                    Coverage = "Partial"
                },
                "Partial",
                "AutoVerified",
                "apiType matched a documented Polytoria type; specific member is not annotated yet.");
        }

        if (officialEnums.ContainsKey(trimmed))
        {
            return new InferredApiReference(
                new NodeCatalogApiReference
                {
                    Type = trimmed,
                    MemberKind = "Enum",
                    Coverage = "Partial"
                },
                "Partial",
                "AutoVerified",
                "apiType matched a documented Polytoria enum.");
        }

        if (officialGlobals.ContainsKey(trimmed))
        {
            return new InferredApiReference(
                new NodeCatalogApiReference
                {
                    Type = trimmed,
                    MemberKind = "Global",
                    Coverage = "Direct"
                },
                "Direct",
                "AutoVerified",
                "apiType matched a documented Polytoria Luau global.");
        }

        return new InferredApiReference(
            new NodeCatalogApiReference
            {
                Type = trimmed,
                MemberKind = "Type",
                Coverage = "Inferred"
            },
            "Inferred",
            "Low",
            "apiType could not be matched to Docs-v2, lua-definitions, or known VRS helper categories.");
    }

    private static string FindMemberKind(PolytoriaApiType apiType, string memberName)
    {
        if (apiType.Properties.Any(member => member.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase)))
        {
            return "Property";
        }

        if (apiType.Methods.Any(member => member.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase)))
        {
            return "Method";
        }

        if (apiType.Events.Any(member => member.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase)))
        {
            return "Event";
        }

        return "";
    }

    private static bool TryClassifySynthetic(string apiType, out NodeCatalogApiReference reference)
    {
        var primitiveTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Any",
            "Boolean",
            "Debug",
            "Flow",
            "Number",
            "Object Path",
            "SceneObject",
            "String"
        };

        if (primitiveTypes.Contains(apiType))
        {
            reference = new NodeCatalogApiReference
            {
                Type = "Lua",
                Member = apiType,
                MemberKind = "Primitive",
                Coverage = "Synthetic"
            };
            return true;
        }

        var vrsTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CoreUI",
            "RuntimeState",
            "VRS.PlayerState"
        };

        if (vrsTypes.Contains(apiType))
        {
            reference = new NodeCatalogApiReference
            {
                Type = "VRS",
                Member = apiType,
                MemberKind = "VRS",
                Coverage = "Synthetic"
            };
            return true;
        }

        reference = new NodeCatalogApiReference();
        return false;
    }

    private static string StrongestCoverage(IEnumerable<string> coverages)
    {
        var normalized = coverages.Select(NormalizeCoverage).ToList();
        if (normalized.Contains("Direct", StringComparer.OrdinalIgnoreCase))
        {
            return "Direct";
        }

        if (normalized.Contains("Partial", StringComparer.OrdinalIgnoreCase))
        {
            return "Partial";
        }

        if (normalized.Contains("Indirect", StringComparer.OrdinalIgnoreCase))
        {
            return "Indirect";
        }

        if (normalized.Contains("Synthetic", StringComparer.OrdinalIgnoreCase))
        {
            return "Synthetic";
        }

        if (normalized.Contains("Inferred", StringComparer.OrdinalIgnoreCase))
        {
            return "Inferred";
        }

        return normalized.FirstOrDefault() ?? "Uncovered";
    }

    private static string NormalizeCoverage(string value)
    {
        return value.Trim() switch
        {
            "Direct" => "Direct",
            "Partial" => "Partial",
            "Indirect" => "Indirect",
            "Synthetic" => "Synthetic",
            "Inferred" => "Inferred",
            "" => "Direct",
            var other => other
        };
    }

    private sealed record InferredApiReference(
        NodeCatalogApiReference Reference,
        string Coverage,
        string Confidence,
        string Reason);
}
