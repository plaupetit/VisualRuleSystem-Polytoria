using System.Globalization;
using System.Text;

namespace Vrs.Core.Catalog;

public sealed record NodeCatalogSearchResult(bool IsMatch, int Score, string MatchSummary)
{
    public static NodeCatalogSearchResult NoMatch { get; } = new(false, 0, "");
    public static NodeCatalogSearchResult MatchAll { get; } = new(true, 0, "");
}

/// <summary>
/// Scores node catalog matches using authoring vocabulary instead of a raw
/// substring check. The service stays in Core so every catalog surface ranks
/// nodes with the same synonyms, typo tolerance, and match explanations.
/// </summary>
public static class NodeCatalogSearchService
{
    private static readonly IReadOnlySet<string> NoiseTokens =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a",
            "an",
            "and",
            "are",
            "block",
            "blocks",
            "do",
            "for",
            "is",
            "node",
            "nodes",
            "of",
            "or",
            "polytoria",
            "the",
            "to",
            "vrs"
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Synonyms = BuildSynonyms(
        ["kill", "death", "die", "respawn"],
        ["touch", "hit", "collision", "collide"],
        ["move", "translate", "position"],
        ["rotate", "turn", "look"],
        ["wait", "delay", "timer", "time"],
        ["message", "print", "log", "output"],
        ["score", "scores", "point", "points"],
        ["player", "user"],
        ["idle", "stopped", "stop", "moving", "movement"]);

    public static NodeCatalogSearchResult Match(NodeCatalogEntry entry, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return NodeCatalogSearchResult.MatchAll;
        }

        var query = BuildQuery(search);
        if (query.Tokens.Count == 0)
        {
            return NodeCatalogSearchResult.NoMatch;
        }

        var fields = BuildFields(entry).ToList();
        var score = 0;
        var summaries = new List<(int Score, string Summary)>();

        foreach (var token in query.Tokens)
        {
            var bestMatch = fields
                .Select(field => MatchToken(field, token))
                .Where(match => match is not null)
                .Select(match => match!)
                .OrderByDescending(match => match.Score)
                .FirstOrDefault();

            if (bestMatch is null)
            {
                return NodeCatalogSearchResult.NoMatch;
            }

            score += bestMatch.Score;
            if (!string.IsNullOrWhiteSpace(bestMatch.Summary))
            {
                summaries.Add((bestMatch.Score, bestMatch.Summary));
            }
        }

        var phraseMatch = MatchPhrase(fields, query.NormalizedPhrase);
        if (phraseMatch is not null)
        {
            score += phraseMatch.Score;
            summaries.Add((phraseMatch.Score, phraseMatch.Summary));
        }

        return new NodeCatalogSearchResult(true, score, SelectSummary(summaries));
    }

    private static CatalogSearchQuery BuildQuery(string search)
    {
        var normalized = NormalizeText(search);
        var tokens = SplitTokens(normalized)
            .Where(token => !NoiseTokens.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CatalogSearchQuery(normalized, tokens);
    }

    private static IEnumerable<SearchField> BuildFields(NodeCatalogEntry entry)
    {
        var intent = NodeCatalogPresentationService.GetIntent(entry.Kind);
        yield return CreateField("label", entry.Label, 1200);
        yield return CreateField("label", entry.Type, 950);
        yield return CreateField("category", intent.Label, 850);
        yield return CreateField("category", intent.Description, 650);
        yield return CreateField("category", NodeCatalogPresentationService.GetDomain(entry), 850);
        yield return CreateField("category", string.Join(" / ", NodeCatalogPresentationService.GetPalettePath(entry)), 900);

        foreach (var path in NodeCatalogPresentationService.GetPalettePaths(entry))
        {
            foreach (var part in path)
            {
                yield return CreateField("category", part, 850);
            }

            yield return CreateField("category", string.Join(" / ", path), 900);
        }

        yield return CreateField("description", NodeCatalogPresentationService.GetBeginnerSummary(entry), 650);
        yield return CreateField("description", entry.Description, 550);
        yield return CreateField("description", entry.PreviewTemplate, 500);
        yield return CreateField("runtime", NodeCatalogPresentationService.GetRuntimeLabel(entry), 450);
        yield return CreateField("runtime", entry.RuntimeFamily, 420);
        yield return CreateField("runtime", entry.ApiGroup, 430);
        yield return CreateField("runtime", entry.ApiType, 380);
        yield return CreateField("runtime", entry.ModuleId, 300);
        yield return CreateField("runtime", entry.IdBase, 300);
        yield return CreateField("runtime", entry.Value, 250);
        yield return CreateField("runtime", entry.Surface, 250);
        yield return CreateField("category", entry.Category, 700);
        yield return CreateField("category", entry.Subcategory, 760);
        yield return CreateField("category", entry.FamilyFolder, 650);
        yield return CreateField("category", entry.UtilityLayer, 620);

        foreach (var keyword in entry.SearchKeywords)
        {
            yield return CreateField("keyword", keyword, 1050);
        }

        foreach (var hint in entry.DebugHints)
        {
            yield return CreateField("description", hint, 450);
        }

        foreach (var hint in entry.SelectorHints)
        {
            yield return CreateField("parameter", hint.Key, 700);
            yield return CreateField("parameter", hint.Label, 760);
            yield return CreateField("parameter", hint.Description, 600);
            yield return CreateField("parameter", hint.DataType, 680);
        }

        foreach (var parameter in entry.Parameters)
        {
            yield return CreateField("parameter", parameter.Key, 700);
            yield return CreateField("parameter", parameter.Label, 780);
            yield return CreateField("parameter", parameter.Description, 620);
            yield return CreateField("parameter", parameter.Type, 620);
            yield return CreateField("parameter", parameter.Control, 500);
            yield return CreateField("parameter", parameter.ValueSource, 560);
            yield return CreateField("parameter", parameter.Default, 360);

            foreach (var keyword in parameter.SearchKeywords)
            {
                yield return CreateField("keyword", keyword, 980);
            }

            foreach (var option in parameter.Options)
            {
                yield return CreateField("parameter", option, 640);
            }

            foreach (var detail in parameter.OptionDetails)
            {
                yield return CreateField("parameter", detail.Value, 620);
                yield return CreateField("parameter", detail.Label, 700);
                yield return CreateField("parameter", detail.Category, 620);
                yield return CreateField("parameter", detail.Description, 560);
                foreach (var keyword in detail.SearchKeywords)
                {
                    yield return CreateField("keyword", keyword, 900);
                }
            }

            foreach (var hint in parameter.SelectorHints)
            {
                yield return CreateField("parameter", hint.Key, 640);
                yield return CreateField("parameter", hint.Label, 700);
                yield return CreateField("parameter", hint.Description, 560);
                yield return CreateField("parameter", hint.DataType, 620);
            }

            foreach (var snippet in parameter.Snippets)
            {
                yield return CreateField("parameter", snippet.Label, 660);
                yield return CreateField("parameter", snippet.Description, 520);
                yield return CreateField("parameter", snippet.Code, 320);
            }
        }
    }

    private static SearchField CreateField(string kind, string value, int weight)
    {
        var normalized = NormalizeText(value);
        var tokens = SplitTokens(normalized).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return new SearchField(kind, value.Trim(), weight, normalized, tokens);
    }

    private static TokenMatch? MatchToken(SearchField field, string queryToken)
    {
        if (field.Tokens.Count == 0)
        {
            return null;
        }

        var best = MatchDirect(field, queryToken, queryToken, isSynonym: false);
        foreach (var synonym in ExpandSynonyms(queryToken))
        {
            var synonymMatch = MatchDirect(field, queryToken, synonym, isSynonym: true);
            if (synonymMatch is not null && (best is null || synonymMatch.Score > best.Score))
            {
                best = synonymMatch;
            }
        }

        var fuzzyMatch = MatchFuzzy(field, queryToken);
        if (fuzzyMatch is not null && (best is null || fuzzyMatch.Score > best.Score))
        {
            best = fuzzyMatch;
        }

        return best;
    }

    private static TokenMatch? MatchDirect(SearchField field, string queryToken, string lookupToken, bool isSynonym)
    {
        if (field.Tokens.Contains(lookupToken, StringComparer.OrdinalIgnoreCase))
        {
            var bonus = isSynonym ? 150 : 320;
            return TokenMatch.Found(field.Weight + bonus, SummaryFor(field, queryToken, lookupToken, isSynonym ? "synonym" : "exact"));
        }

        var prefixToken = field.Tokens.FirstOrDefault(token => token.StartsWith(lookupToken, StringComparison.OrdinalIgnoreCase));
        if (prefixToken is not null)
        {
            var bonus = isSynonym ? 110 : 220;
            return TokenMatch.Found(field.Weight + bonus, SummaryFor(field, queryToken, prefixToken, isSynonym ? "synonym" : "prefix"));
        }

        if (lookupToken.Length >= 4)
        {
            var containedToken = field.Tokens.FirstOrDefault(token => token.Contains(lookupToken, StringComparison.OrdinalIgnoreCase));
            if (containedToken is not null || field.Normalized.Contains(lookupToken, StringComparison.OrdinalIgnoreCase))
            {
                var bonus = isSynonym ? 80 : 160;
                return TokenMatch.Found(field.Weight + bonus, SummaryFor(field, queryToken, containedToken ?? lookupToken, isSynonym ? "synonym" : "contains"));
            }
        }

        return null;
    }

    private static TokenMatch? MatchFuzzy(SearchField field, string queryToken)
    {
        var threshold = FuzzyThreshold(queryToken);
        if (threshold <= 0)
        {
            return null;
        }

        foreach (var token in field.Tokens)
        {
            if (Math.Abs(token.Length - queryToken.Length) > threshold)
            {
                continue;
            }

            if (!CanCompareFuzzy(queryToken, token, threshold))
            {
                continue;
            }

            if (LevenshteinDistance(queryToken, token, threshold) <= threshold)
            {
                return TokenMatch.Found(field.Weight + 100, SummaryFor(field, queryToken, token, "fuzzy"));
            }
        }

        return null;
    }

    private static TokenMatch? MatchPhrase(IEnumerable<SearchField> fields, string normalizedPhrase)
    {
        if (string.IsNullOrWhiteSpace(normalizedPhrase) || SplitTokens(normalizedPhrase).Count < 2)
        {
            return null;
        }

        return fields
            .Where(field => field.Normalized.Contains(normalizedPhrase, StringComparison.OrdinalIgnoreCase))
            .Select(field => TokenMatch.Found(field.Weight + 260, SummaryFor(field, normalizedPhrase, normalizedPhrase, "phrase")))
            .OrderByDescending(match => match.Score)
            .FirstOrDefault();
    }

    private static string SelectSummary(IReadOnlyList<(int Score, string Summary)> summaries)
    {
        return summaries
            .Where(item => !string.IsNullOrWhiteSpace(item.Summary))
            .OrderByDescending(item => item.Score)
            .Select(item => item.Summary)
            .FirstOrDefault() ?? "";
    }

    private static string SummaryFor(SearchField field, string queryToken, string matchedToken, string matchKind)
    {
        if (matchKind.Equals("fuzzy", StringComparison.OrdinalIgnoreCase))
        {
            return $"fuzzy: {queryToken} -> {matchedToken}";
        }

        return field.Kind switch
        {
            "label" => $"label: {field.Display}",
            "keyword" => $"keyword: {field.Display}",
            "category" => $"category: {field.Display}",
            "parameter" => $"parameter: {field.Display}",
            "description" => "description match",
            "runtime" => $"runtime: {field.Display}",
            _ => $"{field.Kind}: {field.Display}"
        };
    }

    private static IEnumerable<string> ExpandSynonyms(string token)
    {
        if (!Synonyms.TryGetValue(token, out var synonyms))
        {
            yield break;
        }

        foreach (var synonym in synonyms)
        {
            if (!synonym.Equals(token, StringComparison.OrdinalIgnoreCase))
            {
                yield return synonym;
            }
        }
    }

    private static int FuzzyThreshold(string token)
    {
        if (NoiseTokens.Contains(token) || token.Length < 3)
        {
            return 0;
        }

        if (token.Length == 3)
        {
            return 1;
        }

        if (token.Length <= 6)
        {
            return 1;
        }

        return 2;
    }

    private static bool CanCompareFuzzy(string queryToken, string candidateToken, int threshold)
    {
        if (threshold <= 0 || candidateToken.Length < 4)
        {
            return false;
        }

        if (queryToken.Length <= 6)
        {
            return queryToken[0] == candidateToken[0];
        }

        return true;
    }

    private static int LevenshteinDistance(string left, string right, int maxDistance)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;
            var bestInRow = current[0];
            for (var column = 1; column <= right.Length; column++)
            {
                var cost = left[row - 1] == right[column - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + cost);
                bestInRow = Math.Min(bestInRow, current[column]);
            }

            if (bestInRow > maxDistance)
            {
                return maxDistance + 1;
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildSynonyms(params string[][] groups)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            var normalizedGroup = group
                .Select(NormalizeText)
                .SelectMany(SplitTokens)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var token in normalizedGroup)
            {
                result[token] = normalizedGroup;
            }
        }

        return result;
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length + 8);
        char previous = '\0';
        foreach (var raw in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(raw);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsUpper(raw) && previous != '\0' && (char.IsLower(previous) || char.IsDigit(previous)))
            {
                builder.Append(' ');
            }

            builder.Append(char.IsLetterOrDigit(raw) ? char.ToLowerInvariant(raw) : ' ');
            previous = raw;
        }

        return CollapseSpaces(builder.ToString());
    }

    private static List<string> SplitTokens(string normalized)
    {
        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static string CollapseSpaces(string value)
    {
        var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', tokens);
    }

    private sealed record CatalogSearchQuery(string NormalizedPhrase, IReadOnlyList<string> Tokens);

    private sealed record SearchField(
        string Kind,
        string Display,
        int Weight,
        string Normalized,
        IReadOnlyList<string> Tokens);

    private sealed record TokenMatch(bool IsMatch, int Score, string Summary)
    {
        public static TokenMatch Found(int score, string summary) => new(true, score, summary);
    }
}
