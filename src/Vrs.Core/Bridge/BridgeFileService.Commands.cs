using System.Text.Json;
using Vrs.Core.Persistence;
using Vrs.Graph.Model;

namespace Vrs.Core.Bridge;

public sealed partial class BridgeFileService
{
    private static readonly HashSet<string> AllowedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "create_folder",
        "ensure_folder_path",
        "ensure_network_event",
        "upsert_script"
    };

    public async Task<string> QueueCreateFolderAsync(string bridgeDirectory, string parentPath, string name, bool dryRun = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Folder name is required.", nameof(name));
        }

        var commandId = $"create_folder_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        var envelope = new BridgeCommandEnvelope
        {
            CommandId = commandId,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            DryRun = dryRun,
            Commands =
            [
                new BridgeCommand
                {
                    Id = commandId,
                    Type = "create_folder",
                    ParentPath = string.IsNullOrWhiteSpace(parentPath) ? "Root" : parentPath,
                    Name = name,
                    Kind = "Folder",
                    DryRun = dryRun
                }
            ]
        };

        await WriteCommandsAsync(bridgeDirectory, envelope, cancellationToken).ConfigureAwait(false);
        return commandId;
    }

    public async Task<string> QueueEnsureFolderPathAsync(string bridgeDirectory, string path, bool dryRun = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Folder path is required.", nameof(path));
        }

        var normalizedPath = NormalizeBridgePath(path);
        var commandId = $"ensure_folder_path_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        var envelope = new BridgeCommandEnvelope
        {
            CommandId = commandId,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            DryRun = dryRun,
            Commands =
            [
                new BridgeCommand
                {
                    Id = commandId,
                    Type = "ensure_folder_path",
                    Path = normalizedPath,
                    Kind = "Folder",
                    DryRun = dryRun,
                    TargetPath = normalizedPath
                }
            ]
        };

        await WriteCommandsAsync(bridgeDirectory, envelope, cancellationToken).ConfigureAwait(false);
        return commandId;
    }

    public async Task<string> QueueEnsureNetworkEventsAsync(
        string bridgeDirectory,
        string parentPath,
        IEnumerable<string> eventNames,
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new ArgumentException("Parent path is required.", nameof(parentPath));
        }

        var normalizedParent = NormalizeBridgePath(parentPath);
        var names = eventNames
            .Select(name => (name ?? "").Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count == 0)
        {
            throw new ArgumentException("At least one NetworkEvent name is required.", nameof(eventNames));
        }

        var commandSuffix = Guid.NewGuid().ToString("N")[..8];
        var batchId = $"ensure_network_events_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{commandSuffix}";
        var commands = new List<BridgeCommand>
        {
            new()
            {
                Id = $"{batchId}_ensure_folder_path",
                Type = "ensure_folder_path",
                Path = normalizedParent,
                Kind = "Folder",
                DryRun = dryRun,
                TargetPath = normalizedParent
            }
        };

        commands.AddRange(names.Select(name => new BridgeCommand
        {
            Id = $"{batchId}_ensure_network_event_{StableCommandName(name)}",
            Type = "ensure_network_event",
            ParentPath = normalizedParent,
            Name = name,
            Kind = "NetworkEvent",
            DryRun = dryRun,
            Path = CombineBridgePath(normalizedParent, name),
            TargetPath = CombineBridgePath(normalizedParent, name)
        }));

        var envelope = new BridgeCommandEnvelope
        {
            CommandId = batchId,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            DryRun = dryRun,
            Commands = commands
        };

        await WriteCommandsAsync(bridgeDirectory, envelope, cancellationToken).ConfigureAwait(false);
        return batchId;
    }

    public async Task<string> QueueUpsertScriptAsync(
        string bridgeDirectory,
        string parentPath,
        string name,
        GraphScriptKind scriptKind,
        string content,
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Script name is required.", nameof(name));
        }

        var normalizedParent = string.IsNullOrWhiteSpace(parentPath) ? "Root" : parentPath.Trim();
        var normalizedName = name.Trim();
        var commandId = $"upsert_script_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        var envelope = new BridgeCommandEnvelope
        {
            CommandId = commandId,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            DryRun = dryRun,
            Commands =
            [
                new BridgeCommand
                {
                    Id = commandId,
                    Type = "upsert_script",
                    ParentPath = normalizedParent,
                    Name = normalizedName,
                    Kind = ScriptObjectKind(scriptKind),
                    DryRun = dryRun,
                    ScriptKind = scriptKind.ToString(),
                    Content = content,
                    TargetPath = CombineBridgePath(normalizedParent, normalizedName)
                }
            ]
        };

        await WriteCommandsAsync(bridgeDirectory, envelope, cancellationToken).ConfigureAwait(false);
        return commandId;
    }

    public async Task<string> QueueUpsertLinkedScriptAsync(
        string bridgeDirectory,
        string parentPath,
        string name,
        GraphScriptKind scriptKind,
        string content,
        string linkedFilePath,
        bool fileAlreadyWritten,
        bool exactParent = false,
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(linkedFilePath))
        {
            throw new ArgumentException("Linked script path is required.", nameof(linkedFilePath));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Script name is required.", nameof(name));
        }

        var normalizedParent = string.IsNullOrWhiteSpace(parentPath) ? "Root" : parentPath.Trim();
        var normalizedName = name.Trim();
        var normalizedLinkedPath = linkedFilePath.Replace('\\', '/').Trim();
        var commandId = $"upsert_script_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        var envelope = new BridgeCommandEnvelope
        {
            CommandId = commandId,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            DryRun = dryRun,
            Commands =
            [
                new BridgeCommand
                {
                    Id = commandId,
                    Type = "upsert_script",
                    ParentPath = normalizedParent,
                    Name = normalizedName,
                    Kind = ScriptObjectKind(scriptKind),
                    DryRun = dryRun,
                    ScriptKind = scriptKind.ToString(),
                    Content = content,
                    Source = content,
                    LinkedFilePath = normalizedLinkedPath,
                    FileAlreadyWritten = fileAlreadyWritten,
                    ExactParent = exactParent,
                    TargetPath = CombineBridgePath(normalizedParent, normalizedName)
                }
            ]
        };

        await WriteCommandsAsync(bridgeDirectory, envelope, cancellationToken).ConfigureAwait(false);
        return commandId;
    }

    public async Task<string> QueueEnsureFolderPathAndUpsertLinkedScriptAsync(
        string bridgeDirectory,
        string ensureFolderPath,
        string parentPath,
        string name,
        GraphScriptKind scriptKind,
        string content,
        string linkedFilePath,
        bool fileAlreadyWritten,
        bool exactParent = false,
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ensureFolderPath))
        {
            throw new ArgumentException("Folder path is required.", nameof(ensureFolderPath));
        }

        if (string.IsNullOrWhiteSpace(linkedFilePath))
        {
            throw new ArgumentException("Linked script path is required.", nameof(linkedFilePath));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Script name is required.", nameof(name));
        }

        var normalizedEnsurePath = NormalizeBridgePath(ensureFolderPath);
        var normalizedParent = string.IsNullOrWhiteSpace(parentPath) ? "Root" : parentPath.Trim();
        var normalizedName = name.Trim();
        var normalizedLinkedPath = linkedFilePath.Replace('\\', '/').Trim();
        var commandSuffix = Guid.NewGuid().ToString("N")[..8];
        var batchId = $"deploy_script_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{commandSuffix}";
        var ensureCommandId = $"{batchId}_ensure_folder_path";
        var upsertCommandId = $"{batchId}_upsert_script";
        var envelope = new BridgeCommandEnvelope
        {
            CommandId = batchId,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            DryRun = dryRun,
            Commands =
            [
                new BridgeCommand
                {
                    Id = ensureCommandId,
                    Type = "ensure_folder_path",
                    Path = normalizedEnsurePath,
                    Kind = "Folder",
                    DryRun = dryRun,
                    TargetPath = normalizedEnsurePath
                },
                new BridgeCommand
                {
                    Id = upsertCommandId,
                    Type = "upsert_script",
                    ParentPath = normalizedParent,
                    Name = normalizedName,
                    Kind = ScriptObjectKind(scriptKind),
                    DryRun = dryRun,
                    ScriptKind = scriptKind.ToString(),
                    Content = content,
                    Source = content,
                    LinkedFilePath = normalizedLinkedPath,
                    FileAlreadyWritten = fileAlreadyWritten,
                    ExactParent = exactParent,
                    TargetPath = CombineBridgePath(normalizedParent, normalizedName)
                }
            ]
        };

        await WriteCommandsAsync(bridgeDirectory, envelope, cancellationToken).ConfigureAwait(false);
        return upsertCommandId;
    }

    /// <summary>
    /// Writes pending-commands.json. The Creator addon watches this file and
    /// decides whether and how to apply the requested operation.
    /// </summary>
    public async Task WriteCommandsAsync(string bridgeDirectory, BridgeCommandEnvelope envelope, CancellationToken cancellationToken = default)
    {
        foreach (var command in envelope.Commands)
        {
            if (!AllowedCommands.Contains(command.Type))
            {
                throw new InvalidOperationException($"Bridge command is not allowed in v1: {command.Type}");
            }
        }

        var json = JsonSerializer.Serialize(envelope, VrsJsonContext.Default.BridgeCommandEnvelope);
        await WriteTextFileByReplaceAsync(Path.Combine(bridgeDirectory, "pending-commands.json"), json, cancellationToken).ConfigureAwait(false);
    }

    private static string ScriptObjectKind(GraphScriptKind scriptKind)
    {
        return scriptKind switch
        {
            GraphScriptKind.Local => "ClientScript",
            GraphScriptKind.Module => "ModuleScript",
            _ => "ServerScript"
        };
    }

    private static string CombineBridgePath(string parentPath, string name)
    {
        var parent = string.IsNullOrWhiteSpace(parentPath) ? "Root" : parentPath.Trim().TrimEnd('/', '\\');
        return parent.Equals("Root", StringComparison.OrdinalIgnoreCase)
            ? name.Trim()
            : $"{parent}/{name.Trim()}";
    }

    private static string NormalizeBridgePath(string path)
    {
        return path.Replace('\\', '/').Trim().Trim('/');
    }

    private static string StableCommandName(string value)
    {
        var chars = value
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .ToArray();
        return chars.Length == 0 ? "event" : new string(chars);
    }
}
