using System.Diagnostics;
using System.Text.Json;

namespace Vrs.App.Services;

/// <summary>
/// Owns the Creator/bridge runtime checks that are tied to the desktop app
/// container, keeping process probing and status-file parsing out of the main
/// Avalonia view model.
/// </summary>
public sealed class ProjectRuntimeStatusService
{
    private readonly Func<string, string?> creatorWindowTitleFinder;
    private readonly Func<DateTimeOffset> utcNowProvider;

    public ProjectRuntimeStatusService(
        Func<string, string?>? creatorWindowTitleFinder = null,
        Func<DateTimeOffset>? utcNowProvider = null)
    {
        this.creatorWindowTitleFinder = creatorWindowTitleFinder ?? FindCreatorWindowTitle;
        this.utcNowProvider = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public ProjectRuntimeStatusResult BuildNoProjectStatus(string detail)
    {
        return new ProjectRuntimeStatusResult(
            ActiveProjectName: "",
            ActiveProjectRoot: "",
            ActiveProjectPath: detail,
            ProjectStatusText: "No Project Found",
            ProjectStatusDetail: detail,
            ProjectStatusBackgroundHex: "#421818",
            ProjectStatusBorderHex: "#ef4444",
            ProjectStatusForegroundHex: "#ffd1d1",
            HasLinkedProject: false,
            IsCreatorRuntimeReady: false,
            HasActiveProject: false);
    }

    public ProjectRuntimeStatusResult BuildLinkedProjectStatus(string projectRoot, string bridgeDirectory)
    {
        var fullRoot = Path.GetFullPath(projectRoot);
        var projectName = ProjectNameFromPath(fullRoot);
        var probe = ProbeProjectRuntime(projectName, bridgeDirectory);

        return new ProjectRuntimeStatusResult(
            ActiveProjectName: projectName,
            ActiveProjectRoot: fullRoot,
            ActiveProjectPath: $"Linked: {fullRoot}",
            ProjectStatusText: probe.IsRunning
                ? $"Project {projectName} Is Running"
                : $"Project {projectName} Not Running",
            ProjectStatusDetail: $"{fullRoot}{Environment.NewLine}{probe.Detail}",
            ProjectStatusBackgroundHex: probe.IsRunning ? "#123522" : "#3b2f12",
            ProjectStatusBorderHex: probe.IsRunning ? "#2dd46f" : "#e7b93f",
            ProjectStatusForegroundHex: probe.IsRunning ? "#c8ffd9" : "#ffe7a8",
            HasLinkedProject: true,
            IsCreatorRuntimeReady: probe.IsRunning,
            HasActiveProject: probe.IsRunning);
    }

    public bool IsValidProjectRoot(string projectRoot)
    {
        return !string.IsNullOrWhiteSpace(projectRoot) &&
            Directory.Exists(projectRoot) &&
            File.Exists(Path.Combine(projectRoot, "project.ptproj"));
    }

    public static string ProjectNameFromPath(string projectRoot)
    {
        var trimmed = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.IsNullOrWhiteSpace(trimmed)
            ? "Unknown"
            : Path.GetFileName(trimmed);
    }

    private ProjectRuntimeProbe ProbeProjectRuntime(string projectName, string bridgeDirectory)
    {
        var creatorWindowTitle = creatorWindowTitleFinder(projectName);
        var bridgeProbe = ProbeBridgeRuntime(bridgeDirectory);

        if (!string.IsNullOrWhiteSpace(creatorWindowTitle))
        {
            return new ProjectRuntimeProbe(
                true,
                $"Creator window found: {creatorWindowTitle}{Environment.NewLine}{bridgeProbe.Detail}");
        }

        if (bridgeProbe.IsRunning)
        {
            return new ProjectRuntimeProbe(true, bridgeProbe.Detail);
        }

        return new ProjectRuntimeProbe(
            false,
            $"Creator window not found for project '{projectName}'.{Environment.NewLine}{bridgeProbe.Detail}");
    }

    private ProjectRuntimeProbe ProbeBridgeRuntime(string bridgeDirectory)
    {
        var statusPath = Path.Combine(bridgeDirectory, "status.json");
        if (!File.Exists(statusPath))
        {
            return new ProjectRuntimeProbe(false, $"Bridge status missing: {statusPath}");
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(statusPath));
            var root = document.RootElement;
            var state = ReadJsonString(root, "state") ?? "unknown";
            var message = ReadJsonString(root, "message") ?? "";
            var updatedAtText = ReadJsonString(root, "updatedAtUtc") ?? ReadJsonString(root, "UpdatedAtUtc") ?? "";

            if (!DateTimeOffset.TryParse(updatedAtText, out var updatedAt))
            {
                return new ProjectRuntimeProbe(false, $"Bridge status has no readable updatedAtUtc. State: {state}");
            }

            var age = utcNowProvider() - updatedAt.ToUniversalTime();
            var isFresh = age <= TimeSpan.FromMinutes(2);
            var detail = $"Bridge status: {state}; age: {FormatAge(age)}; message: {message}";
            return new ProjectRuntimeProbe(isFresh, detail);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new ProjectRuntimeProbe(false, $"Bridge status unreadable: {ex.Message}");
        }
    }

    private static string? FindCreatorWindowTitle(string projectName)
    {
        foreach (var process in Process.GetProcesses())
        {
            string title;
            try
            {
                title = process.MainWindowTitle;
            }
            catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            if (title.Contains("Polytoria Creator", StringComparison.OrdinalIgnoreCase) &&
                title.Contains(projectName, StringComparison.OrdinalIgnoreCase))
            {
                return title;
            }
        }

        return null;
    }

    private static string? ReadJsonString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age < TimeSpan.Zero)
        {
            return "future timestamp";
        }

        if (age.TotalSeconds < 90)
        {
            return $"{Math.Max(0, (int)age.TotalSeconds)}s";
        }

        if (age.TotalMinutes < 90)
        {
            return $"{(int)age.TotalMinutes}m";
        }

        return $"{(int)age.TotalHours}h";
    }

    private sealed record ProjectRuntimeProbe(bool IsRunning, string Detail);
}

/// <summary>
/// UI-ready status snapshot for the linked Polytoria project. The service
/// computes these values together so colors, labels, and running state cannot
/// drift apart inside the view model.
/// </summary>
public sealed record ProjectRuntimeStatusResult(
    string ActiveProjectName,
    string ActiveProjectRoot,
    string ActiveProjectPath,
    string ProjectStatusText,
    string ProjectStatusDetail,
    string ProjectStatusBackgroundHex,
    string ProjectStatusBorderHex,
    string ProjectStatusForegroundHex,
    bool HasLinkedProject,
    bool IsCreatorRuntimeReady,
    bool HasActiveProject);
