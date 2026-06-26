using System.Text.Json;
using Vrs.Core.Persistence;

namespace Vrs.Core.Bridge;

public sealed partial class BridgeFileService
{
    /// <summary>
    /// Reads the editor-side active project pointer. This file is owned by VRS,
    /// not by Creator, and only stores the selected Polytoria project root.
    /// </summary>
    public async Task<ActivePolytoriaProjectConfig?> LoadActiveProjectAsync(string configPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(configPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(json, VrsJsonContext.Default.ActivePolytoriaProjectConfig);
    }

    public async Task SaveActiveProjectAsync(string configPath, string projectRoot, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new ArgumentException("Project root is required.", nameof(projectRoot));
        }

        var config = new ActivePolytoriaProjectConfig
        {
            ProjectRoot = Path.GetFullPath(projectRoot)
        };
        var json = JsonSerializer.Serialize(config, VrsJsonContext.Default.ActivePolytoriaProjectConfig);
        await WriteTextFileByReplaceAsync(configPath, json, cancellationToken).ConfigureAwait(false);
    }
}
