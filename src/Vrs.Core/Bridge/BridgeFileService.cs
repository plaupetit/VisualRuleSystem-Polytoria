using System.Diagnostics;

namespace Vrs.Core.Bridge;

/// <summary>
/// File bridge helper. Creator remains the authority that applies commands;
/// this service only writes local intent files and reads bridge-owned status
/// files. The partials keep each bridge contract isolated: active project
/// config, linked scripts, pending commands, and runtime status files.
/// </summary>
public sealed partial class BridgeFileService
{
    private readonly string sessionId = $"app-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

    public string ResolveBridgeDirectory(string projectRoot)
    {
        return Path.Combine(projectRoot, "addons", "visual-programming-bridge", "bridge");
    }

    public static async Task WriteTextFileByReplaceAsync(string targetPath, string text, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(targetPath)) ?? ".");
        var tempPath = $"{targetPath}.{Process.GetCurrentProcess().Id}.{Guid.NewGuid():N}.tmp";

        await File.WriteAllTextAsync(tempPath, text, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, targetPath, overwrite: true);
    }
}
