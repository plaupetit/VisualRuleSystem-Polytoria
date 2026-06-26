using Vrs.Launcher;

namespace Vrs.Tests;

public sealed class LauncherPublishFingerprintTests
{
    [Fact]
    public void PublishFingerprint_RuntimeMissingTriggersRebuild()
    {
        var repoRoot = CreateTempRepoRoot();
        var currentRuntime = Path.Combine(repoRoot, "publish", "current");

        try
        {
            var decision = PublishFingerprintService.Evaluate(repoRoot, currentRuntime, "Vrs.App.dll", "metadata.json", forceRebuild: false);

            Assert.True(decision.ShouldPublish);
            Assert.Contains("runtime missing", decision.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public void PublishFingerprint_MatchingMetadataReusesRuntime()
    {
        var repoRoot = CreateTempRepoRoot();
        var currentRuntime = Path.Combine(repoRoot, "publish", "current");

        try
        {
            Directory.CreateDirectory(currentRuntime);
            File.WriteAllText(Path.Combine(currentRuntime, "Vrs.App.dll"), "published");
            var fingerprint = PublishFingerprintService.BuildFingerprint(repoRoot);
            PublishFingerprintService.WriteMetadata(
                currentRuntime,
                "metadata.json",
                new PublishMetadata(fingerprint, DateTimeOffset.UtcNow, "Vrs.App.dll"));

            var decision = PublishFingerprintService.Evaluate(repoRoot, currentRuntime, "Vrs.App.dll", "metadata.json", forceRebuild: false);

            Assert.False(decision.ShouldPublish);
            Assert.Contains("current", decision.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public void PublishFingerprint_SourceChangeTriggersRebuild()
    {
        var repoRoot = CreateTempRepoRoot();
        var currentRuntime = Path.Combine(repoRoot, "publish", "current");

        try
        {
            Directory.CreateDirectory(currentRuntime);
            File.WriteAllText(Path.Combine(currentRuntime, "Vrs.App.dll"), "published");
            var fingerprint = PublishFingerprintService.BuildFingerprint(repoRoot);
            PublishFingerprintService.WriteMetadata(
                currentRuntime,
                "metadata.json",
                new PublishMetadata(fingerprint, DateTimeOffset.UtcNow, "Vrs.App.dll"));

            var sourcePath = Path.Combine(repoRoot, "src", "Vrs.App", "Program.cs");
            File.WriteAllText(sourcePath, "changed source");
            File.SetLastWriteTimeUtc(sourcePath, DateTime.UtcNow.AddSeconds(5));

            var decision = PublishFingerprintService.Evaluate(repoRoot, currentRuntime, "Vrs.App.dll", "metadata.json", forceRebuild: false);

            Assert.True(decision.ShouldPublish);
            Assert.Contains("fingerprint changed", decision.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public void PublishFingerprint_LauncherSourceChangeDoesNotRebuildAppRuntime()
    {
        var repoRoot = CreateTempRepoRoot();
        var currentRuntime = Path.Combine(repoRoot, "publish", "current");

        try
        {
            Directory.CreateDirectory(currentRuntime);
            File.WriteAllText(Path.Combine(currentRuntime, "Vrs.App.dll"), "published");
            var fingerprint = PublishFingerprintService.BuildFingerprint(repoRoot);
            PublishFingerprintService.WriteMetadata(
                currentRuntime,
                "metadata.json",
                new PublishMetadata(fingerprint, DateTimeOffset.UtcNow, "Vrs.App.dll"));

            var launcherPath = Path.Combine(repoRoot, "src", "Vrs.Launcher", "Program.cs");
            File.WriteAllText(launcherPath, "changed launcher source");
            File.SetLastWriteTimeUtc(launcherPath, DateTime.UtcNow.AddSeconds(5));

            var decision = PublishFingerprintService.Evaluate(repoRoot, currentRuntime, "Vrs.App.dll", "metadata.json", forceRebuild: false);

            Assert.False(decision.ShouldPublish);
            Assert.Contains("current", decision.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public void PublishFingerprint_ForceRebuildOverridesMatchingMetadata()
    {
        var repoRoot = CreateTempRepoRoot();
        var currentRuntime = Path.Combine(repoRoot, "publish", "current");

        try
        {
            Directory.CreateDirectory(currentRuntime);
            File.WriteAllText(Path.Combine(currentRuntime, "Vrs.App.dll"), "published");
            var fingerprint = PublishFingerprintService.BuildFingerprint(repoRoot);
            PublishFingerprintService.WriteMetadata(
                currentRuntime,
                "metadata.json",
                new PublishMetadata(fingerprint, DateTimeOffset.UtcNow, "Vrs.App.dll"));

            var decision = PublishFingerprintService.Evaluate(repoRoot, currentRuntime, "Vrs.App.dll", "metadata.json", forceRebuild: true);

            Assert.True(decision.ShouldPublish);
            Assert.Contains("force", decision.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.True(PublishFingerprintService.IsForceRebuildRequested("1"));
            Assert.True(PublishFingerprintService.IsForceRebuildRequested("true"));
            Assert.False(PublishFingerprintService.IsForceRebuildRequested(""));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public void LauncherConcurrentLaunch_DoesNotShowBlockingErrorDialog()
    {
        var launcherSource = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot, "src", "Vrs.Launcher", "Program.cs"));

        Assert.Contains("leaving it to finish without showing a modal error", launcherSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Wait for the current launch/update to finish", launcherSource, StringComparison.Ordinal);
    }

    private static string CreateTempRepoRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"vrs-launcher-cache-{Guid.NewGuid():N}");
        WriteFile(repoRoot, "VisualRuleSystem.Polytoria.slnx", "solution");
        WriteFile(repoRoot, "src/Vrs.App/Vrs.App.csproj", "<Project />");
        WriteFile(repoRoot, "src/Vrs.App/Program.cs", "source");
        WriteFile(repoRoot, "src/Vrs.Launcher/Program.cs", "launcher source");
        WriteFile(repoRoot, "data/catalog/TestNode/manifest.vrs-node.json", "{}");
        WriteFile(repoRoot, "src/Vrs.App/bin/Debug/ignored.dll", Guid.NewGuid().ToString("N"));
        return repoRoot;
    }

    private static void WriteFile(string root, string relativePath, string content)
    {
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
}
