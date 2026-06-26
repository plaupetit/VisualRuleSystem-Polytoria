using Vrs.Core.Export;
using Vrs.Graph.Model;

namespace Vrs.App.Services;

public enum ScriptGraphLoadStatus
{
    Loaded,
    FileMissing,
    MissingMetadata,
    InvalidMetadata,
    NotLuauFile
}

public sealed record ScriptGraphLoadResult(
    ScriptGraphLoadStatus Status,
    RuleGraph? Graph,
    string ScriptPath,
    string ProjectRelativePath,
    GraphScriptKind ScriptKind,
    string ScriptName,
    string StatusText)
{
    public bool Succeeded => Status == ScriptGraphLoadStatus.Loaded && Graph is not null;
}

/// <summary>
/// Loads VRS graph metadata from project Luau files without deciding whether
/// the current editor graph should be replaced. UI and view-model flows share
/// this service so scene scripts and file-browser scripts report the same
/// failure states.
/// </summary>
public sealed class ScriptGraphLoadService
{
    private const string MetadataBeginMarker = "VRS_GRAPH_BEGIN base64-json";

    public async Task<ScriptGraphLoadResult> LoadAsync(
        string projectRoot,
        string scriptPath,
        CancellationToken cancellationToken = default)
    {
        var resolved = ResolveScriptPath(projectRoot, scriptPath);
        var kind = InferScriptKind(resolved.ProjectRelativePath, resolved.ScriptPath);
        var name = InferScriptName(resolved.ProjectRelativePath, resolved.ScriptPath);

        if (!Path.GetExtension(resolved.ScriptPath).Equals(".luau", StringComparison.OrdinalIgnoreCase))
        {
            return Result(
                ScriptGraphLoadStatus.NotLuauFile,
                null,
                resolved,
                kind,
                name,
                "Cannot load this file as a VRS graph.");
        }

        if (!File.Exists(resolved.ScriptPath))
        {
            return Result(
                ScriptGraphLoadStatus.FileMissing,
                null,
                resolved,
                kind,
                name,
                $"Linked script file was not found: {resolved.ScriptPath}");
        }

        var luau = await File.ReadAllTextAsync(resolved.ScriptPath, cancellationToken).ConfigureAwait(false);
        if (!luau.Contains(MetadataBeginMarker, StringComparison.Ordinal))
        {
            return Result(
                ScriptGraphLoadStatus.MissingMetadata,
                null,
                resolved,
                kind,
                name,
                "No VRS graph metadata found in this script.");
        }

        if (!LuauExporter.TryExtractGraphMetadata(luau, out var graph) || graph is null)
        {
            return Result(
                ScriptGraphLoadStatus.InvalidMetadata,
                null,
                resolved,
                kind,
                name,
                "Cannot load this file as a VRS graph.");
        }

        return Result(
            ScriptGraphLoadStatus.Loaded,
            graph,
            resolved,
            kind,
            name,
            $"Loaded VRS graph from: {resolved.DisplayPath}");
    }

    private static ScriptGraphLoadResult Result(
        ScriptGraphLoadStatus status,
        RuleGraph? graph,
        ResolvedScriptPath resolved,
        GraphScriptKind kind,
        string name,
        string statusText)
    {
        return new ScriptGraphLoadResult(
            status,
            graph,
            resolved.ScriptPath,
            resolved.ProjectRelativePath,
            kind,
            name,
            statusText);
    }

    private static ResolvedScriptPath ResolveScriptPath(string projectRoot, string scriptPath)
    {
        var normalizedProjectRoot = string.IsNullOrWhiteSpace(projectRoot)
            ? ""
            : Path.GetFullPath(projectRoot);
        var normalizedInput = scriptPath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.IsPathRooted(normalizedInput)
            ? Path.GetFullPath(normalizedInput)
            : Path.GetFullPath(Path.Combine(normalizedProjectRoot, normalizedInput));
        var relativePath = TryGetProjectRelativePath(normalizedProjectRoot, fullPath, normalizedInput);
        return new ResolvedScriptPath(fullPath, relativePath, string.IsNullOrWhiteSpace(relativePath) ? fullPath : relativePath);
    }

    private static string TryGetProjectRelativePath(string projectRoot, string fullPath, string fallback)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return NormalizeProjectPath(fallback);
        }

        var root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeProjectPath(fallback);
        }

        return NormalizeProjectPath(Path.GetRelativePath(projectRoot, fullPath));
    }

    private static GraphScriptKind InferScriptKind(string projectRelativePath, string scriptPath)
    {
        var descriptor = $"{projectRelativePath} {scriptPath}";
        if (descriptor.Contains(".module.luau", StringComparison.OrdinalIgnoreCase) ||
            descriptor.Contains("/module/", StringComparison.OrdinalIgnoreCase) ||
            descriptor.Contains("\\module\\", StringComparison.OrdinalIgnoreCase))
        {
            return GraphScriptKind.Module;
        }

        if (descriptor.Contains(".client.luau", StringComparison.OrdinalIgnoreCase) ||
            descriptor.Contains("/client/", StringComparison.OrdinalIgnoreCase) ||
            descriptor.Contains("\\client\\", StringComparison.OrdinalIgnoreCase))
        {
            return GraphScriptKind.Local;
        }

        return GraphScriptKind.Server;
    }

    private static string InferScriptName(string projectRelativePath, string scriptPath)
    {
        var source = string.IsNullOrWhiteSpace(projectRelativePath) ? scriptPath : projectRelativePath;
        var fileName = Path.GetFileName(source.Replace('/', Path.DirectorySeparatorChar));
        return fileName
            .Replace(".server.luau", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".client.luau", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".module.luau", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".luau", "", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProjectPath(string path)
    {
        return path == "."
            ? ""
            : path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private sealed record ResolvedScriptPath(string ScriptPath, string ProjectRelativePath, string DisplayPath);
}
