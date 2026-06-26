using Vrs.Graph.Model;

namespace Vrs.Core.Persistence;

public enum GraphDocumentSection
{
    Metadata,
    SceneObjects,
    Variables,
    Rules,
    ViewState,
    CatalogIndex
}

public sealed class GraphDocumentSaveResult
{
    public bool Saved { get; set; }
    public string Reason { get; set; } = "";
    public string Path { get; set; } = "";
    public List<GraphDocumentSection> Sections { get; set; } = [];
}

/// <summary>
/// Tracks document dirtiness independently from the UI and writes through a
/// temporary file so external readers never observe partial JSON.
/// </summary>
public sealed class GraphDocumentStore
{
    private readonly Func<DateTimeOffset> clock;
    private readonly HashSet<GraphDocumentSection> dirtySections = [];
    private DateTimeOffset lastDirtyAt = DateTimeOffset.MinValue;

    public GraphDocumentStore(TimeSpan? debounceWindow = null, Func<DateTimeOffset>? clock = null)
    {
        DebounceWindow = debounceWindow ?? TimeSpan.FromMilliseconds(350);
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public TimeSpan DebounceWindow { get; }
    public IReadOnlyCollection<GraphDocumentSection> DirtySections => dirtySections;

    public void MarkDirty(GraphDocumentSection section)
    {
        dirtySections.Add(section);
        lastDirtyAt = clock();
    }

    public void MarkDirty(IEnumerable<GraphDocumentSection> sections)
    {
        foreach (var section in sections)
        {
            dirtySections.Add(section);
        }

        if (dirtySections.Count > 0)
        {
            lastDirtyAt = clock();
        }
    }

    public async Task<GraphDocumentSaveResult> SaveIfDirtyAsync(
        RuleGraph graph,
        string path,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (dirtySections.Count == 0 && !force)
        {
            return new GraphDocumentSaveResult
            {
                Saved = false,
                Reason = "No dirty graph sections.",
                Path = path
            };
        }

        if (!force && clock() - lastDirtyAt < DebounceWindow)
        {
            return new GraphDocumentSaveResult
            {
                Saved = false,
                Reason = "Debounce window is still active.",
                Path = path,
                Sections = dirtySections.ToList()
            };
        }

        var sections = dirtySections.ToList();
        await RuleGraphJson.SaveAtomicAsync(graph, path, cancellationToken).ConfigureAwait(false);
        dirtySections.Clear();

        return new GraphDocumentSaveResult
        {
            Saved = true,
            Reason = "Saved graph document.",
            Path = path,
            Sections = sections
        };
    }
}
