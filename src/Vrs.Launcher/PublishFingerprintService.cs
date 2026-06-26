using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vrs.Launcher;

public sealed record PublishMetadata(string Fingerprint, DateTimeOffset CreatedAtUtc, string AppAssemblyName);

public sealed record PublishDecision(bool ShouldPublish, string Reason, string Fingerprint);

public static class PublishFingerprintService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly string[] SourceRoots =
    [
        "src/Vrs.App",
        "src/Vrs.Core",
        "src/Vrs.Graph",
        "data/catalog"
    ];

    private static readonly string[] SourceFileNames =
    [
        "VisualRuleSystem.Polytoria.slnx",
        "active-polytoria-project.example.json",
        "global.json",
        "Directory.Build.props",
        "Directory.Build.targets"
    ];

    private static readonly HashSet<string> SkippedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        "publish",
        "logs",
        ".git",
        ".vs"
    };

    public static PublishDecision Evaluate(
        string repoRoot,
        string currentRuntime,
        string appAssemblyName,
        string metadataFileName,
        bool forceRebuild)
    {
        var fingerprint = BuildFingerprint(repoRoot);
        if (forceRebuild)
        {
            return new PublishDecision(true, "force rebuild requested", fingerprint);
        }

        var appPath = Path.Combine(currentRuntime, appAssemblyName);
        if (!File.Exists(appPath))
        {
            return new PublishDecision(true, $"runtime missing {appAssemblyName}", fingerprint);
        }

        var metadata = ReadMetadata(currentRuntime, metadataFileName);
        if (metadata is null)
        {
            return new PublishDecision(true, "publish metadata missing", fingerprint);
        }

        if (!metadata.AppAssemblyName.Equals(appAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return new PublishDecision(true, "published app assembly changed", fingerprint);
        }

        if (!metadata.Fingerprint.Equals(fingerprint, StringComparison.Ordinal))
        {
            return new PublishDecision(true, "source fingerprint changed", fingerprint);
        }

        return new PublishDecision(false, "published runtime is current", fingerprint);
    }

    public static string BuildFingerprint(string repoRoot)
    {
        var normalizedRoot = Path.GetFullPath(repoRoot);
        var lines = EnumerateBuildInputFiles(normalizedRoot)
            .Select(path => BuildFingerprintLine(normalizedRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase);
        var payload = string.Join('\n', lines);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    public static bool IsForceRebuildRequested(string? value)
    {
        return value is not null &&
            (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
             value.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    public static void WriteMetadata(string currentRuntime, string metadataFileName, PublishMetadata metadata)
    {
        Directory.CreateDirectory(currentRuntime);
        var path = Path.Combine(currentRuntime, metadataFileName);
        var json = JsonSerializer.Serialize(metadata, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static PublishMetadata? ReadMetadata(string currentRuntime, string metadataFileName)
    {
        var path = Path.Combine(currentRuntime, metadataFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PublishMetadata>(json);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static IEnumerable<string> EnumerateBuildInputFiles(string repoRoot)
    {
        foreach (var fileName in SourceFileNames)
        {
            var path = Path.Combine(repoRoot, fileName);
            if (File.Exists(path))
            {
                yield return path;
            }
        }

        foreach (var sourceRoot in SourceRoots)
        {
            var root = Path.Combine(repoRoot, sourceRoot);
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var path in EnumerateFilesSkippingBuildOutputs(root))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> EnumerateFilesSkippingBuildOutputs(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var childDirectory in SafeEnumerateDirectories(directory))
            {
                if (!SkippedDirectoryNames.Contains(Path.GetFileName(childDirectory)))
                {
                    pending.Push(childDirectory);
                }
            }

            foreach (var file in SafeEnumerateFiles(directory))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return [];
        }
    }

    private static string BuildFingerprintLine(string repoRoot, string path)
    {
        var info = new FileInfo(path);
        var relativePath = Path.GetRelativePath(repoRoot, path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        return $"{relativePath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
    }
}
