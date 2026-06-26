using Vrs.Core.Persistence;
using Vrs.Core.Samples;

namespace Vrs.Tests;

public sealed class GraphDocumentStoreTests
{
    [Fact]
    public async Task SaveIfDirtyAsync_DebouncesThenWritesAtomicJson()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new GraphDocumentStore(TimeSpan.FromSeconds(10), () => now);
        var graph = SampleGraphFactory.CreateTimerMessageGraph();
        var path = Path.Combine(Path.GetTempPath(), "vrs-tests", $"{Guid.NewGuid():N}.vrs-graph.json");

        store.MarkDirty(GraphDocumentSection.Rules);
        var skipped = await store.SaveIfDirtyAsync(graph, path);

        Assert.False(skipped.Saved);
        Assert.False(File.Exists(path));

        var saved = await store.SaveIfDirtyAsync(graph, path, force: true);

        Assert.True(saved.Saved);
        Assert.True(File.Exists(path));
        Assert.Contains("\"version\": 3", await File.ReadAllTextAsync(path));
    }
}
