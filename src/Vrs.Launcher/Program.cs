using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Vrs.Launcher;

internal static class Program
{
    private const string SolutionFileName = "VisualRuleSystem.Polytoria.slnx";
    private const string AppProjectRelativePath = "src/Vrs.App/Vrs.App.csproj";
    private const string AppAssemblyName = "Vrs.App.dll";
    private const string AppExecutableName = "Vrs.App.exe";
    private const string AppPidFileName = "vrs-app.pid";
    private const string PublishMetadataFileName = "vrs-publish-metadata.json";
    private const string ForceRebuildEnvironmentVariable = "VRS_FORCE_REBUILD";
    private const string LauncherMutexName = "Local\\VisualRuleSystem.Polytoria.Launcher";
    private static readonly TimeSpan LauncherMutexWait = TimeSpan.FromSeconds(1);

    [STAThread]
    private static int Main()
    {
        string? logPath = null;
        using var launcherMutex = new Mutex(false, LauncherMutexName);
        var ownsLauncherMutex = false;
        try
        {
            var repoRoot = FindRepoRoot();
            logPath = Path.Combine(repoRoot, "logs", "launcher.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            Log(logPath, "Starting VisualRuleSystem launcher.");

            var publishRoot = Path.Combine(repoRoot, "publish");
            var currentRuntime = Path.Combine(publishRoot, "current");
            var legacyRuntime = Path.Combine(publishRoot, "win-x64");
            var stagingRuntime = Path.Combine(publishRoot, $"staging-{DateTime.UtcNow:yyyyMMddHHmmss}-{Environment.ProcessId}");

            ownsLauncherMutex = launcherMutex.WaitOne(LauncherMutexWait);
            if (!ownsLauncherMutex)
            {
                if (TryActivateTrackedAppProcess(currentRuntime, logPath))
                {
                    return 0;
                }

                Log(logPath, "Another VisualRuleSystem launch/update is already active; leaving it to finish without showing a modal error.");
                return 0;
            }

            EnsureDirectoryIsInside(repoRoot, publishRoot);
            EnsureDirectoryIsInside(repoRoot, currentRuntime);
            EnsureDirectoryIsInside(repoRoot, legacyRuntime);
            EnsureDirectoryIsInside(repoRoot, stagingRuntime);

            var forceRebuild = PublishFingerprintService.IsForceRebuildRequested(Environment.GetEnvironmentVariable(ForceRebuildEnvironmentVariable)) ||
                Environment.GetCommandLineArgs().Any(argument => argument.Equals("--force-rebuild", StringComparison.OrdinalIgnoreCase));
            var publishDecisionStopwatch = Stopwatch.StartNew();
            var publishDecision = PublishFingerprintService.Evaluate(
                repoRoot,
                currentRuntime,
                AppAssemblyName,
                PublishMetadataFileName,
                forceRebuild);
            publishDecisionStopwatch.Stop();
            Log(logPath, $"Publish decision in {publishDecisionStopwatch.ElapsedMilliseconds} ms: {(publishDecision.ShouldPublish ? "rebuild" : "reuse")} ({publishDecision.Reason}).");

            CleanupOldStagingDirectories(repoRoot, publishRoot, logPath);
            DeleteDirectoryIfExists(legacyRuntime, logPath);

            if (!publishDecision.ShouldPublish)
            {
                if (TryActivateTrackedAppProcess(currentRuntime, logPath))
                {
                    return 0;
                }

                LaunchApp(currentRuntime, logPath);
                return 0;
            }

            StopExistingAppProcesses(repoRoot, currentRuntime, logPath);

            var publish = PublishApp(repoRoot, stagingRuntime, logPath);
            if (publish.ExitCode != 0)
            {
                DeleteDirectoryIfExists(stagingRuntime, logPath);
                Fail(logPath, "VisualRuleSystem could not publish the latest app build.", publish.Output);
                return publish.ExitCode;
            }

            var stagedApp = Path.Combine(stagingRuntime, AppAssemblyName);
            if (!File.Exists(stagedApp))
            {
                DeleteDirectoryIfExists(stagingRuntime, logPath);
                Fail(logPath, $"Publish succeeded but {AppAssemblyName} was not produced.", publish.Output);
                return 1;
            }

            DeleteDirectoryIfExists(currentRuntime, logPath);
            Directory.Move(stagingRuntime, currentRuntime);
            PublishFingerprintService.WriteMetadata(
                currentRuntime,
                PublishMetadataFileName,
                new PublishMetadata(publishDecision.Fingerprint, DateTimeOffset.UtcNow, AppAssemblyName));
            Log(logPath, $"Promoted latest app runtime to {currentRuntime}.");

            LaunchApp(currentRuntime, logPath);
            return 0;
        }
        catch (Exception ex)
        {
            var message = $"VisualRuleSystem launcher failed.{Environment.NewLine}{Environment.NewLine}{ex.Message}";
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                Log(logPath, ex.ToString());
                message += $"{Environment.NewLine}{Environment.NewLine}Log: {logPath}";
            }

            ShowError(message);
            return 1;
        }
        finally
        {
            if (ownsLauncherMutex)
            {
                launcherMutex.ReleaseMutex();
            }
        }
    }

    private static string FindRepoRoot()
    {
        var startPoints = new[]
        {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? ""
        };

        foreach (var start in startPoints.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var current = new DirectoryInfo(start);
            while (current is not null)
            {
                var solution = Path.Combine(current.FullName, SolutionFileName);
                var appProject = Path.Combine(current.FullName, AppProjectRelativePath);
                if (File.Exists(solution) && File.Exists(appProject))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new InvalidOperationException($"Could not find {SolutionFileName}. Put VisualRuleSystem.exe at the repository root.");
    }

    private static PublishResult PublishApp(string repoRoot, string outputPath, string logPath)
    {
        Directory.CreateDirectory(outputPath);
        var artifactsPath = Path.Combine(Path.GetTempPath(), $"vrs-app-artifacts-{DateTime.UtcNow:yyyyMMddHHmmss}-{Environment.ProcessId}");
        var arguments = new[]
        {
            "publish",
            AppProjectRelativePath,
            "-c",
            "Release",
            "-r",
            "win-x64",
            "--self-contained",
            "false",
            "-p:UseAppHost=false",
            "--artifacts-path",
            artifactsPath,
            "-o",
            outputPath
        };

        Log(logPath, $"Running: dotnet {string.Join(' ', arguments.Select(QuoteIfNeeded))}");
        var result = RunProcess("dotnet", arguments, repoRoot);
        Log(logPath, result.Output);
        SafeDeleteDirectory(artifactsPath, logPath);
        return result;
    }

    private static PublishResult RunProcess(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var output = new StringBuilder();
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                output.AppendLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                output.AppendLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        process.WaitForExit();
        return new PublishResult(process.ExitCode, output.ToString());
    }

    private static Process LaunchApp(string currentRuntime, string logPath)
    {
        var launchStopwatch = Stopwatch.StartNew();
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = currentRuntime,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(AppAssemblyName);

        var appProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {AppAssemblyName}.");
        File.WriteAllText(Path.Combine(currentRuntime, AppPidFileName), appProcess.Id.ToString());
        launchStopwatch.Stop();
        Log(logPath, $"Launched {AppAssemblyName} via dotnet process {appProcess.Id} in {launchStopwatch.ElapsedMilliseconds} ms.");
        return appProcess;
    }

    private static bool TryActivateTrackedAppProcess(string currentRuntime, string logPath)
    {
        var pidPath = Path.Combine(currentRuntime, AppPidFileName);
        if (!File.Exists(pidPath) || !int.TryParse(File.ReadAllText(pidPath).Trim(), out var processId))
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                return false;
            }

            if (!process.ProcessName.Equals("dotnet", StringComparison.OrdinalIgnoreCase) &&
                !process.ProcessName.Equals(Path.GetFileNameWithoutExtension(AppExecutableName), StringComparison.OrdinalIgnoreCase))
            {
                Log(logPath, $"Tracked pid {processId} belongs to {process.ProcessName}; launching a new app instance.");
                return false;
            }

            if (process.MainWindowHandle != IntPtr.Zero)
            {
                _ = ShowWindow(process.MainWindowHandle, 9);
                _ = SetForegroundWindow(process.MainWindowHandle);
            }

            Log(logPath, $"Reused running app process {processId} without publish.");
            return true;
        }
        catch (ArgumentException)
        {
            Log(logPath, $"Tracked app process {processId} is no longer running.");
            return false;
        }
        catch (Exception ex)
        {
            Log(logPath, $"Could not activate tracked app process {processId}: {ex.Message}");
            return false;
        }
    }

    private static void StopExistingAppProcesses(string repoRoot, string currentRuntime, string logPath)
    {
        StopTrackedDotnetAppProcess(currentRuntime, logPath);

        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(AppExecutableName)))
        {
            try
            {
                var processPath = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(processPath) || !IsInside(repoRoot, processPath))
                {
                    continue;
                }

                StopProcess(process, logPath, $"existing app process {process.Id}: {processPath}");
            }
            catch (Exception ex)
            {
                Log(logPath, $"Could not stop process {process.Id}: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }

        foreach (var process in Process.GetProcessesByName("dotnet"))
        {
            try
            {
                if (!process.MainWindowTitle.Contains("VisualRuleSystem", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                StopProcess(process, logPath, $"existing VisualRuleSystem dotnet process {process.Id}");
            }
            catch (Exception ex)
            {
                Log(logPath, $"Could not inspect dotnet process {process.Id}: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void StopTrackedDotnetAppProcess(string currentRuntime, string logPath)
    {
        var pidPath = Path.Combine(currentRuntime, AppPidFileName);
        if (!File.Exists(pidPath) || !int.TryParse(File.ReadAllText(pidPath).Trim(), out var processId))
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.ProcessName.Equals("dotnet", StringComparison.OrdinalIgnoreCase) &&
                !process.ProcessName.Equals(Path.GetFileNameWithoutExtension(AppExecutableName), StringComparison.OrdinalIgnoreCase))
            {
                Log(logPath, $"Skipping tracked pid {processId}; it now belongs to {process.ProcessName}.");
                return;
            }

            StopProcess(process, logPath, $"tracked app process {processId}");
        }
        catch (ArgumentException)
        {
            Log(logPath, $"Tracked app process {processId} is no longer running.");
        }
        catch (Exception ex)
        {
            Log(logPath, $"Could not stop tracked app process {processId}: {ex.Message}");
        }
    }

    private static void StopProcess(Process process, string logPath, string reason)
    {
        Log(logPath, $"Stopping {reason}.");
        if (!process.CloseMainWindow() || !process.WaitForExit(3000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
    }

    private static void CleanupOldStagingDirectories(string repoRoot, string publishRoot, string logPath)
    {
        if (!Directory.Exists(publishRoot))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(publishRoot, "staging-*"))
        {
            EnsureDirectoryIsInside(repoRoot, directory);
            SafeDeleteDirectory(directory, logPath);
        }
    }

    private static void DeleteDirectoryIfExists(string path, string logPath)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        var lastError = "";
        for (var attempt = 1; attempt <= 6; attempt++)
        {
            try
            {
                Log(logPath, $"Deleting {path}.");
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                lastError = ex.Message;
                Thread.Sleep(500);
            }
            catch (IOException ex)
            {
                lastError = ex.Message;
                Thread.Sleep(500);
            }
        }

        throw new IOException($"Could not delete {path}. Last error: {lastError}");
    }

    private static void SafeDeleteDirectory(string path, string logPath)
    {
        try
        {
            DeleteDirectoryIfExists(path, logPath);
        }
        catch (Exception ex)
        {
            Log(logPath, $"Could not delete {path}: {ex.Message}");
        }
    }

    private static void EnsureDirectoryIsInside(string root, string candidate)
    {
        if (!IsInside(root, candidate))
        {
            throw new InvalidOperationException($"Refusing to modify a path outside the repository: {candidate}");
        }
    }

    private static bool IsInside(string root, string candidate)
    {
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidateFull = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return candidateFull.Equals(rootFull, StringComparison.OrdinalIgnoreCase) ||
               candidateFull.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               candidateFull.StartsWith(rootFull + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void Fail(string logPath, string title, string details)
    {
        Log(logPath, title);
        Log(logPath, details);
        ShowError($"{title}{Environment.NewLine}{Environment.NewLine}{TrimForDialog(details)}{Environment.NewLine}{Environment.NewLine}Log: {logPath}");
    }

    private static string TrimForDialog(string details)
    {
        const int maxLength = 1800;
        if (details.Length <= maxLength)
        {
            return details;
        }

        return details[^maxLength..];
    }

    private static string QuoteIfNeeded(string value)
    {
        return value.Contains(' ') ? $"\"{value}\"" : value;
    }

    private static void Log(string logPath, string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private static void ShowError(string message)
    {
        _ = MessageBoxW(IntPtr.Zero, message, "VisualRuleSystem Launcher", 0x00000010);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private sealed record PublishResult(int ExitCode, string Output);
}
