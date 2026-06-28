using Vrs.Core.Catalog;

namespace Vrs.Tools.PolytoriaApiCoverage;

public sealed record CoverageToolOptions(
    string RepositoryRoot,
    string CatalogRoot,
    string OutputDirectory,
    string CacheDirectory,
    string? SourceDirectory = null);

public sealed record PolytoriaApiSourceSnapshot(
    string DocsSource,
    string DocsCommit,
    DateTimeOffset? DocsCommitDate,
    string LuaDefinitionsSource,
    string LuaDefinitionsCommit,
    DateTimeOffset? LuaDefinitionsCommitDate,
    IReadOnlyList<PolytoriaApiType> Types,
    IReadOnlyList<PolytoriaApiEnum> Enums,
    IReadOnlyList<PolytoriaApiGlobal> Globals);

public sealed record PolytoriaApiType(
    string Name,
    IReadOnlyList<PolytoriaApiMember> Properties,
    IReadOnlyList<PolytoriaApiMember> Methods,
    IReadOnlyList<PolytoriaApiMember> Events);

public sealed record PolytoriaApiMember(string Name, string Kind);

public sealed record PolytoriaApiEnum(string Name, string InternalName, IReadOnlyList<string> Options);

public sealed record PolytoriaApiGlobal(string Name, string Kind);

public sealed record ApiCoverageResult(
    PolytoriaApiSourceSnapshot Source,
    DateTimeOffset GeneratedAtUtc,
    CatalogSummary Catalog,
    CoverageSummary Summary,
    IReadOnlyList<TypeCoverageRow> TypeRows,
    IReadOnlyList<ApiCoverageRoadmapItem> Roadmap,
    IReadOnlyList<CatalogNodeCoverageRow> NodeRows,
    IReadOnlyList<string> CatalogWarnings);

public sealed record CatalogSummary(
    int TotalNodes,
    IReadOnlyDictionary<string, int> NodesByKind);

public sealed record CoverageSummary(
    int OfficialTypes,
    int OfficialEnums,
    int OfficialGlobals,
    int TypesWithAnyCoverage,
    int TypesWithoutCoverage,
    int DirectTypeCount,
    int PartialTypeCount,
    int IndirectOrSyntheticTypeCount,
    int InferredTypeCount,
    int TargetRuntimeTypes,
    int TargetRuntimeTypesWithCoverage,
    int TargetRuntimeTypesWithoutCoverage,
    int GameplayApiTypes,
    int GameplayApiTypesWithCoverage,
    int GameplayApiTypesWithoutCoverage,
    int CreatorApiTypes,
    int CreatorApiTypesWithCoverage,
    int CreatorApiTypesWithoutCoverage,
    int LowConfidenceNodeCount,
    int NodesWithoutApiReference,
    double TypesWithAnyCoveragePercent,
    double TypesWithoutCoveragePercent,
    double DirectTypePercent,
    double PartialTypePercent,
    double IndirectOrSyntheticTypePercent,
    double InferredTypePercent,
    double TargetRuntimeCoveragePercent,
    double TargetRuntimeUncoveredPercent,
    double GameplayApiCoveragePercent,
    double GameplayApiUncoveredPercent,
    double CreatorApiCoveragePercent,
    double CreatorApiUncoveredPercent,
    double LowConfidenceNodePercent,
    double NodesWithoutApiReferencePercent);

public sealed record TypeCoverageRow(
    string Type,
    string Coverage,
    string Confidence,
    string Category,
    string ApiSurface,
    int Priority,
    bool IsVrsTargetRuntime,
    string SuggestedNodePack,
    IReadOnlyList<string> Nodes);

public sealed record ApiCoverageRoadmapItem(
    string Type,
    string Category,
    int Priority,
    string SuggestedNodePack,
    string SuggestedNodeKind,
    string SuggestedLabel,
    string Reason);

public sealed record CatalogNodeCoverageRow(
    string NodeId,
    string NodeKind,
    string Label,
    string ApiType,
    IReadOnlyList<NodeCatalogApiReference> References,
    string Coverage,
    string Confidence,
    string Reason);
