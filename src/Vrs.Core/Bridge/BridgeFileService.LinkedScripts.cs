using Vrs.Graph.Model;

namespace Vrs.Core.Bridge;

public sealed partial class BridgeFileService
{
    public static string LinkedScriptProjectRelativePath(string scriptName, GraphScriptKind scriptKind)
    {
        var safeName = SafeScriptFileName(scriptName);
        return ToProjectRelativePath("scripts", "VRS", ScriptKindDirectory(scriptKind), $"{safeName}{ScriptKindFileSuffix(scriptKind)}");
    }

    /// <summary>
    /// Writes the deployed script file under scripts/VRS and preserves an
    /// existing .meta sidecar so Creator keeps the same linked script identity.
    /// </summary>
    public async Task<LinkedScriptFileWriteResult> WriteLinkedScriptFileAsync(
        string projectRoot,
        string scriptName,
        GraphScriptKind scriptKind,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new ArgumentException("Project root is required.", nameof(projectRoot));
        }

        if (string.IsNullOrWhiteSpace(scriptName))
        {
            throw new ArgumentException("Script name is required.", nameof(scriptName));
        }

        var relativePath = LinkedScriptProjectRelativePath(scriptName, scriptKind);
        var scriptPath = Path.Combine(Path.GetFullPath(projectRoot), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var metaPath = $"{scriptPath}.meta";
        var scriptAlreadyExisted = File.Exists(scriptPath);
        var metaCreated = !File.Exists(metaPath);

        await WriteTextFileByReplaceAsync(scriptPath, content, cancellationToken).ConfigureAwait(false);
        if (metaCreated)
        {
            var metaJson = $"{{{Environment.NewLine}  \"id\": \"{Guid.NewGuid():N}\"{Environment.NewLine}}}";
            await WriteTextFileByReplaceAsync(metaPath, metaJson, cancellationToken).ConfigureAwait(false);
        }

        return new LinkedScriptFileWriteResult
        {
            ScriptPath = scriptPath,
            ProjectRelativeScriptPath = relativePath,
            MetaPath = metaPath,
            ScriptAlreadyExisted = scriptAlreadyExisted,
            MetaCreated = metaCreated
        };
    }

    private static string ScriptKindDirectory(GraphScriptKind scriptKind)
    {
        return scriptKind switch
        {
            GraphScriptKind.Local => "client",
            GraphScriptKind.Module => "module",
            _ => "server"
        };
    }

    private static string ScriptKindFileSuffix(GraphScriptKind scriptKind)
    {
        return scriptKind switch
        {
            GraphScriptKind.Local => ".client.luau",
            GraphScriptKind.Module => ".module.luau",
            _ => ".server.luau"
        };
    }

    private static string SafeScriptFileName(string scriptName)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var cleaned = new string(scriptName.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        cleaned = string.Join(" ", cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(cleaned) ? "VisualRuleScript" : cleaned;
    }

    private static string ToProjectRelativePath(params string[] segments)
    {
        return string.Join("/", segments.Select(segment => segment.Trim('/', '\\')));
    }
}
