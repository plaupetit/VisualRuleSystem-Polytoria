using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.App.Services;

/// <summary>
/// Builds beginner-friendly palette rows without depending on Avalonia drawing
/// types. The canvas only renders the returned presentation records.
/// </summary>
public sealed class NodePaletteQueryService
{
    public IReadOnlyList<NodePaletteBrowserRow> Browse(
        IEnumerable<NodeCatalogEntry> entries,
        NodePaletteBrowserQueryOptions options)
    {
        var searchActive = !string.IsNullOrWhiteSpace(options.Search);
        var candidates = BuildCandidates(entries, options.IncompatibilityReason);

        if (searchActive)
        {
            return candidates
                .Select(candidate => WithSearchResult(candidate, options.Search))
                .Where(candidate => candidate.SearchScore > 0)
                .Where(candidate => !options.CompatibleOnly || candidate.IsCompatible)
                .GroupBy(candidate => candidate.Entry.IdBase, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(candidate => candidate.SearchScore)
                    .ThenBy(candidate => candidate.IsAlias)
                    .ThenBy(candidate => candidate.PathLabel, StringComparer.OrdinalIgnoreCase)
                    .First())
                .OrderByDescending(candidate => candidate.SearchScore)
                .ThenBy(candidate => NodeCatalogPresentationService.GetIntent(candidate.Entry.Kind).Order)
                .ThenBy(candidate => candidate.PathLabel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Entry.Label, StringComparer.OrdinalIgnoreCase)
                .Select(NodePaletteBrowserRowFactory.CreateNodeRow)
                .Select((row, index) => row with { Index = index })
                .ToList();
        }

        if (string.IsNullOrWhiteSpace(options.CurrentIntentKey))
        {
            return CreateIntentRows(candidates.ToList(), options.CompatibleOnly);
        }

        var intentCandidates = candidates
            .Where(candidate => candidate.IntentKey.Equals(options.CurrentIntentKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return CreatePathRows(intentCandidates, options.CurrentDomainPath, options.CompatibleOnly);
    }

    public IReadOnlyList<NodePaletteDisplayEntry> Query(
        IEnumerable<NodeCatalogEntry> entries,
        NodePaletteQueryOptions options)
    {
        return QueryInternal(entries, options, options.DomainFilter)
            .Where(entry => !options.CompatibleOnly || entry.IsCompatible)
            .Select((entry, index) => entry with { Index = index })
            .ToList();
    }

    public IReadOnlyList<string> GetDomainOptions(
        IEnumerable<NodeCatalogEntry> entries,
        NodePaletteQueryOptions options,
        int maxDomains = 6)
    {
        return QueryInternal(entries, options with { DomainFilter = "", CompatibleOnly = false }, "")
            .Where(entry => !options.CompatibleOnly || entry.IsCompatible)
            .GroupBy(entry => entry.DomainLabel, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, maxDomains))
            .Select(group => group.Key)
            .ToList();
    }

    private static IEnumerable<NodePaletteDisplayEntry> QueryInternal(
        IEnumerable<NodeCatalogEntry> entries,
        NodePaletteQueryOptions options,
        string domainFilter)
    {
        var searchActive = !string.IsNullOrWhiteSpace(options.Search);
        var displayEntries = entries
            .Where(NodeCatalogService.IsAddable)
            .Where(NodeCatalogPresentationService.IsDefaultPaletteSurface)
            .Select(entry => BuildDisplayEntry(entry, options.IncompatibilityReason))
            .Where(entry => options.EnabledIntentKeys.Count == 0 ||
                options.EnabledIntentKeys.Any(key => key.Equals(entry.IntentKey, StringComparison.OrdinalIgnoreCase)))
            .Where(entry => string.IsNullOrWhiteSpace(domainFilter) ||
                entry.DomainLabel.Equals(domainFilter, StringComparison.OrdinalIgnoreCase));

        if (searchActive)
        {
            return displayEntries
                .Select(entry => new
                {
                    Entry = entry,
                    Search = NodeCatalogSearchService.Match(entry.Entry, options.Search)
                })
                .Where(item => item.Search.IsMatch)
                .OrderByDescending(item => item.Search.Score)
                .ThenBy(item => NodeCatalogPresentationService.GetIntent(item.Entry.Entry.Kind).Order)
                .ThenBy(item => item.Entry.DomainLabel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Entry.Entry.Label, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.Entry);
        }

        return displayEntries
            .OrderBy(entry => NodeCatalogPresentationService.GetIntent(entry.Entry.Kind).Order)
            .ThenBy(entry => entry.DomainLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Entry.Label, StringComparer.OrdinalIgnoreCase);
    }

    private static NodePaletteDisplayEntry BuildDisplayEntry(
        NodeCatalogEntry entry,
        Func<NodeCatalogEntry, string?> incompatibilityReason)
    {
        var intent = NodeCatalogPresentationService.GetIntent(entry.Kind);
        var reason = incompatibilityReason(entry) ?? "";
        return new NodePaletteDisplayEntry(
            Entry: entry,
            Index: 0,
            IntentKey: intent.Key,
            IntentLabel: intent.Label,
            IntentDescription: intent.Description,
            DomainLabel: NodeCatalogPresentationService.GetDomain(entry),
            BeginnerSummary: NodeCatalogPresentationService.GetBeginnerSummary(entry),
            RuntimeLabel: NodeCatalogPresentationService.GetRuntimeLabel(entry),
            Surface: NodeCatalogPresentationService.GetSurface(entry),
            IsCompatible: string.IsNullOrWhiteSpace(reason),
            IncompatibilityReason: reason);
    }

    private static IReadOnlyList<NodePaletteBrowserRow> CreateIntentRows(
        IReadOnlyList<NodePaletteCandidate> candidates,
        bool compatibleOnly)
    {
        return candidates
            .GroupBy(candidate => candidate.IntentKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                var compatibleCount = group.Count(candidate => candidate.IsCompatible);
                var totalCount = group.Count();
                return NodePaletteBrowserRowFactory.CreateFolderRow(
                    key: first.IntentKey,
                    label: first.IntentLabel,
                    intentKey: first.IntentKey,
                    domainPath: [],
                    compatibleCount,
                    totalCount,
                    order: NodeCatalogPresentationService.GetIntent(first.Entry.Kind).Order);
            })
            .Where(row => !compatibleOnly || row.CompatibleCount > 0)
            .OrderBy(row => row.SortOrder)
            .Select((row, index) => row with { Index = index })
            .ToList();
    }

    private static IReadOnlyList<NodePaletteBrowserRow> CreatePathRows(
        IReadOnlyList<NodePaletteCandidate> candidates,
        IReadOnlyList<string> currentPath,
        bool compatibleOnly)
    {
        var normalizedPath = currentPath.Select(NormalizePathSegment).Where(part => !string.IsNullOrWhiteSpace(part)).ToList();
        var scoped = candidates
            .Where(candidate => PathStartsWith(candidate.PalettePath, normalizedPath))
            .ToList();
        var folderRows = scoped
            .Where(candidate => candidate.PalettePath.Count > normalizedPath.Count)
            .GroupBy(candidate => candidate.PalettePath[normalizedPath.Count], StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var label = group.Key;
                var compatibleCount = group.Count(candidate => candidate.IsCompatible);
                var totalCount = group.Count();
                return NodePaletteBrowserRowFactory.CreateFolderRow(
                    key: $"{group.First().IntentKey}:{string.Join("/", normalizedPath.Append(label))}",
                    label,
                    intentKey: group.First().IntentKey,
                    domainPath: normalizedPath.Append(label).ToList(),
                    compatibleCount,
                    totalCount,
                    order: 0);
            })
            .Where(row => !compatibleOnly || row.CompatibleCount > 0)
            .OrderBy(row => row.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var nodeRows = scoped
            .Where(candidate => candidate.PalettePath.Count == normalizedPath.Count)
            .Where(candidate => !compatibleOnly || candidate.IsCompatible)
            .OrderBy(candidate => candidate.Entry.Label, StringComparer.OrdinalIgnoreCase)
            .Select(NodePaletteBrowserRowFactory.CreateNodeRow)
            .ToList();

        return folderRows
            .Concat(nodeRows)
            .Select((row, index) => row with { Index = index })
            .ToList();
    }

    private static IEnumerable<NodePaletteCandidate> BuildCandidates(
        IEnumerable<NodeCatalogEntry> entries,
        Func<NodeCatalogEntry, string?> incompatibilityReason)
    {
        return entries
            .Where(NodeCatalogService.IsAddable)
            .Where(NodeCatalogPresentationService.IsDefaultPaletteSurface)
            .SelectMany(entry =>
            {
                var intent = NodeCatalogPresentationService.GetIntent(entry.Kind);
                var reason = incompatibilityReason(entry) ?? "";
                return NodeCatalogPresentationService.GetPalettePaths(entry)
                    .Select((path, index) => new NodePaletteCandidate(
                        Entry: entry,
                        IntentKey: intent.Key,
                        IntentLabel: intent.Label,
                        IntentDescription: intent.Description,
                        PalettePath: path,
                        PathLabel: string.Join(" / ", path),
                        BeginnerSummary: NodeCatalogPresentationService.GetBeginnerSummary(entry),
                        RuntimeLabel: NodeCatalogPresentationService.GetRuntimeLabel(entry),
                        IsCompatible: string.IsNullOrWhiteSpace(reason),
                        IncompatibilityReason: reason,
                        IsAlias: index > 0,
                        SearchScore: 0,
                        MatchSummary: ""));
            });
    }

    private static NodePaletteCandidate WithSearchResult(NodePaletteCandidate candidate, string search)
    {
        var result = NodeCatalogSearchService.Match(candidate.Entry, search);
        return candidate with
        {
            SearchScore = result.IsMatch ? result.Score : 0,
            MatchSummary = result.MatchSummary
        };
    }

    private static bool PathStartsWith(IReadOnlyList<string> path, IReadOnlyList<string> prefix)
    {
        if (prefix.Count > path.Count)
        {
            return false;
        }

        for (var index = 0; index < prefix.Count; index++)
        {
            if (!path[index].Equals(prefix[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizePathSegment(string value)
    {
        return value.Trim();
    }
}
