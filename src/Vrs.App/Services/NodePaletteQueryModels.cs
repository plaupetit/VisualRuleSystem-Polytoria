using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.App.Services;

public sealed record NodePaletteQueryOptions(
    string Search,
    GraphScriptKind ScriptKind,
    bool CompatibleOnly,
    IReadOnlySet<string> EnabledIntentKeys,
    string DomainFilter,
    Func<NodeCatalogEntry, string?> IncompatibilityReason);

public sealed record NodePaletteDisplayEntry(
    NodeCatalogEntry Entry,
    int Index,
    string IntentKey,
    string IntentLabel,
    string IntentDescription,
    string DomainLabel,
    string BeginnerSummary,
    string RuntimeLabel,
    string Surface,
    bool IsCompatible,
    string IncompatibilityReason)
{
    public string GroupHeader => $"{IntentLabel} / {DomainLabel}";
}

public enum NodePaletteApiSurfaceFilter
{
    Gameplay,
    All,
    Creator
}

public sealed record NodePaletteBrowserQueryOptions(
    string Search,
    GraphScriptKind ScriptKind,
    bool CompatibleOnly,
    NodePaletteApiSurfaceFilter ApiSurfaceFilter,
    string CurrentIntentKey,
    IReadOnlyList<string> CurrentDomainPath,
    Func<NodeCatalogEntry, string?> IncompatibilityReason);

public enum NodePaletteBrowserRowKind
{
    Folder,
    Node
}

public sealed record NodePaletteBrowserRow(
    NodePaletteBrowserRowKind Kind,
    string Key,
    int Index,
    NodeCatalogEntry? Entry,
    string Label,
    string Description,
    string IntentKey,
    string IntentLabel,
    IReadOnlyList<string> DomainPath,
    string DomainLabel,
    string RuntimeLabel,
    string IconGlyph,
    string IconAccentHex,
    string IconBackgroundHex,
    string MatchSummary,
    string TooltipTitle,
    string TooltipText,
    bool IsCompatible,
    string IncompatibilityReason,
    int CompatibleCount,
    int TotalCount,
    int SortOrder);

internal sealed record NodePaletteCandidate(
    NodeCatalogEntry Entry,
    string IntentKey,
    string IntentLabel,
    string IntentDescription,
    IReadOnlyList<string> PalettePath,
    string PathLabel,
    string BeginnerSummary,
    string RuntimeLabel,
    bool IsCompatible,
    string IncompatibilityReason,
    bool IsAlias,
    int SearchScore,
    string MatchSummary);
