namespace Vrs.Core.Bridge;

public static partial class SceneSnapshotReader
{
    private static SceneSnapshotReadOptions Normalize(SceneSnapshotReadOptions? options)
    {
        return new SceneSnapshotReadOptions
        {
            MaxObjects = Math.Clamp(options?.MaxObjects ?? 250, 1, 5_000),
            MaxDepth = Math.Clamp(options?.MaxDepth ?? 5, 0, 32),
            Search = options?.Search?.Trim() ?? "",
            IncludeBridgeTrash = options?.IncludeBridgeTrash ?? false,
            StopAfterDisplayLimit = options?.StopAfterDisplayLimit ?? true
        };
    }
}
