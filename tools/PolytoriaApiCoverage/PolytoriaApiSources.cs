using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Vrs.Tools.PolytoriaApiCoverage;

public interface IPolytoriaApiSource
{
    Task<PolytoriaApiSourceSnapshot> LoadAsync(CancellationToken cancellationToken);
}

public sealed class GitHubPolytoriaApiSource : IPolytoriaApiSource
{
    private const string DocsRepo = "Polytoria/Docs-v2";
    private const string LuaDefinitionsRepo = "Polytoria/lua-definitions";

    private readonly HttpClient httpClient;

    public GitHubPolytoriaApiSource(HttpClient? httpClient = null)
    {
        this.httpClient = httpClient ?? new HttpClient();
        if (!this.httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            this.httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("VisualRuleSystem-Polytoria-ApiCoverage");
        }
    }

    public async Task<PolytoriaApiSourceSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        var docsCommit = await GetCommitAsync(DocsRepo, cancellationToken);
        var luaCommit = await GetCommitAsync(LuaDefinitionsRepo, cancellationToken);
        var typeItems = await GetContentItemsAsync(DocsRepo, "yaml/types", cancellationToken);
        var enumItems = await GetContentItemsAsync(DocsRepo, "yaml/enums", cancellationToken);

        var types = await DownloadAndParseAsync(typeItems, PolytoriaYamlParser.ParseType, cancellationToken);
        var enums = await DownloadAndParseAsync(enumItems, PolytoriaYamlParser.ParseEnum, cancellationToken);
        var globalsText = await httpClient.GetStringAsync("https://raw.githubusercontent.com/Polytoria/lua-definitions/main/globals.d.luau", cancellationToken);

        return new PolytoriaApiSourceSnapshot(
            "https://github.com/Polytoria/Docs-v2",
            docsCommit.Sha,
            docsCommit.Date,
            "https://github.com/Polytoria/lua-definitions",
            luaCommit.Sha,
            luaCommit.Date,
            types.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            enums.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            PolytoriaLuaDefinitionsParser.ParseGlobals(globalsText));
    }

    private async Task<IReadOnlyList<T>> DownloadAndParseAsync<T>(
        IReadOnlyList<GitHubContentItem> items,
        Func<string, T> parse,
        CancellationToken cancellationToken)
    {
        var results = new List<T>();
        var throttler = new SemaphoreSlim(8);
        var tasks = items
            .Where(item => item.DownloadUrl.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            .Select(async item =>
            {
                await throttler.WaitAsync(cancellationToken);
                try
                {
                    var text = await httpClient.GetStringAsync(item.DownloadUrl, cancellationToken);
                    return parse(text);
                }
                finally
                {
                    throttler.Release();
                }
            });

        foreach (var task in tasks)
        {
            results.Add(await task);
        }

        return results;
    }

    private async Task<IReadOnlyList<GitHubContentItem>> GetContentItemsAsync(string repo, string path, CancellationToken cancellationToken)
    {
        using var stream = await httpClient.GetStreamAsync($"https://api.github.com/repos/{repo}/contents/{path}?ref=main", cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement
            .EnumerateArray()
            .Select(item => new GitHubContentItem(
                item.GetProperty("name").GetString() ?? "",
                item.GetProperty("download_url").GetString() ?? ""))
            .Where(item => !string.IsNullOrWhiteSpace(item.DownloadUrl))
            .ToList();
    }

    private async Task<GitHubCommitInfo> GetCommitAsync(string repo, CancellationToken cancellationToken)
    {
        using var stream = await httpClient.GetStreamAsync($"https://api.github.com/repos/{repo}/commits/main", cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var sha = document.RootElement.GetProperty("sha").GetString() ?? "";
        var date = document.RootElement
            .GetProperty("commit")
            .GetProperty("committer")
            .GetProperty("date")
            .GetDateTimeOffset();
        return new GitHubCommitInfo(sha, date);
    }

    private sealed record GitHubContentItem(string Name, string DownloadUrl);

    private sealed record GitHubCommitInfo(string Sha, DateTimeOffset Date);
}

public sealed class LocalPolytoriaApiSource : IPolytoriaApiSource
{
    private readonly string sourceDirectory;

    public LocalPolytoriaApiSource(string sourceDirectory)
    {
        this.sourceDirectory = sourceDirectory;
    }

    public Task<PolytoriaApiSourceSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        var typesRoot = Path.Combine(sourceDirectory, "yaml", "types");
        var enumsRoot = Path.Combine(sourceDirectory, "yaml", "enums");
        var globalsPath = Path.Combine(sourceDirectory, "globals.d.luau");
        if (!Directory.Exists(typesRoot) || !Directory.Exists(enumsRoot) || !File.Exists(globalsPath))
        {
            throw new DirectoryNotFoundException("Local source must contain yaml/types, yaml/enums, and globals.d.luau.");
        }

        var types = Directory.EnumerateFiles(typesRoot, "*.yaml")
            .Select(path => PolytoriaYamlParser.ParseType(File.ReadAllText(path)))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var enums = Directory.EnumerateFiles(enumsRoot, "*.yaml")
            .Select(path => PolytoriaYamlParser.ParseEnum(File.ReadAllText(path)))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var globals = PolytoriaLuaDefinitionsParser.ParseGlobals(File.ReadAllText(globalsPath));

        return Task.FromResult(new PolytoriaApiSourceSnapshot(
            sourceDirectory,
            "local",
            null,
            sourceDirectory,
            "local",
            null,
            types,
            enums,
            globals));
    }
}

public static class PolytoriaYamlParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(PascalCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static PolytoriaApiType ParseType(string yaml)
    {
        var dto = Deserializer.Deserialize<ApiTypeDto>(yaml) ?? new ApiTypeDto();
        return new PolytoriaApiType(
            dto.Name,
            ToMembers(dto.Properties, "Property"),
            ToMembers(dto.Methods, "Method"),
            ToMembers(dto.Events, "Event"));
    }

    public static PolytoriaApiEnum ParseEnum(string yaml)
    {
        var dto = Deserializer.Deserialize<ApiEnumDto>(yaml) ?? new ApiEnumDto();
        return new PolytoriaApiEnum(
            dto.Name,
            dto.InternalName,
            dto.Options.Select(option => option.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToList());
    }

    private static IReadOnlyList<PolytoriaApiMember> ToMembers(List<ApiMemberDto>? members, string kind)
    {
        return members?
            .Where(member => !string.IsNullOrWhiteSpace(member.Name))
            .Select(member => new PolytoriaApiMember(member.Name, kind))
            .ToList() ?? [];
    }

    private sealed class ApiTypeDto
    {
        public string Name { get; set; } = "";
        public List<ApiMemberDto>? Properties { get; set; }
        public List<ApiMemberDto>? Methods { get; set; }
        public List<ApiMemberDto>? Events { get; set; }
    }

    private sealed class ApiMemberDto
    {
        public string Name { get; set; } = "";
    }

    private sealed class ApiEnumDto
    {
        public string Name { get; set; } = "";
        public string InternalName { get; set; } = "";
        public List<ApiEnumOptionDto> Options { get; set; } = [];
    }

    private sealed class ApiEnumOptionDto
    {
        public string Name { get; set; } = "";
    }
}

public static partial class PolytoriaLuaDefinitionsParser
{
    public static IReadOnlyList<PolytoriaApiGlobal> ParseGlobals(string text)
    {
        var globals = new List<PolytoriaApiGlobal>();
        foreach (var line in text.Split('\n'))
        {
            var functionMatch = DeclareFunctionRegex().Match(line);
            if (functionMatch.Success)
            {
                globals.Add(new PolytoriaApiGlobal(functionMatch.Groups["name"].Value, "Function"));
                continue;
            }

            var valueMatch = DeclareValueRegex().Match(line);
            if (valueMatch.Success)
            {
                globals.Add(new PolytoriaApiGlobal(valueMatch.Groups["name"].Value, "Value"));
            }
        }

        return globals
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    [GeneratedRegex(@"^\s*declare\s+function\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b")]
    private static partial Regex DeclareFunctionRegex();

    [GeneratedRegex(@"^\s*declare\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:")]
    private static partial Regex DeclareValueRegex();
}
