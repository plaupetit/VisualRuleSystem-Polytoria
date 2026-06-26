using Vrs.App.Services;
using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.Tests;

public sealed class NodePaletteQueryServiceTests
{
    private readonly NodePaletteQueryService service = new();

    [Fact]
    public void Browse_RootShowsBeginnerIntentFolders()
    {
        var catalog = LoadCatalog();

        var rows = service.Browse(catalog.Nodes, BrowserOptions());

        Assert.Equal([
            "When",
            "Do",
            "Check",
            "Value"
        ], rows.Select(row => row.Label).ToArray());
        Assert.All(rows, row => Assert.Equal(NodePaletteBrowserRowKind.Folder, row.Kind));
    }

    [Fact]
    public void Browse_RowsExposeIconsAndTooltipText()
    {
        var catalog = LoadCatalog();

        var rootRows = service.Browse(catalog.Nodes, BrowserOptions());
        Assert.All(rootRows, AssertPalettePresentation);
        Assert.Equal("▶", rootRows.Single(row => row.Label == "When").IconGlyph);
        Assert.Equal("◆", rootRows.Single(row => row.Label == "Do").IconGlyph);
        Assert.Equal("?", rootRows.Single(row => row.Label == "Check").IconGlyph);
        Assert.Contains("Trigger node category", rootRows.Single(row => row.Label == "When").TooltipText, StringComparison.Ordinal);
        Assert.Contains("Action node category", rootRows.Single(row => row.Label == "Do").TooltipText, StringComparison.Ordinal);
        Assert.Contains("Condition node category", rootRows.Single(row => row.Label == "Check").TooltipText, StringComparison.Ordinal);
        Assert.Contains("Starts a rule", rootRows.Single(row => row.Label == "When").Description, StringComparison.Ordinal);
        Assert.Contains("Runs an action", rootRows.Single(row => row.Label == "Do").Description, StringComparison.Ordinal);
        Assert.Contains("Tests whether", rootRows.Single(row => row.Label == "Check").Description, StringComparison.Ordinal);

        var timingNodes = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "When", currentDomainPath: ["Flow & Timing", "Timing"]));
        var timer = timingNodes.Single(row => row.Entry?.IdBase == "EV_OnTimerTick");

        AssertPalettePresentation(timer);
        Assert.Contains("Runs on:", timer.TooltipText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Compatible", timer.TooltipText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Browse_FolderRowsExposeGuidanceCopy()
    {
        var catalog = LoadCatalog();

        var whenFolders = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "When"));
        AssertFolderGuide(whenFolders.Single(row => row.Label == "Flow & Timing"), "startup flow");

        var flowFolders = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "When", currentDomainPath: ["Flow & Timing"]));
        AssertFolderGuide(flowFolders.Single(row => row.Label == "Timing"), "time controls");

        var variableWatcherFolders = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "When", currentDomainPath: ["Variables"]));
        AssertFolderGuide(variableWatcherFolders.Single(row => row.Label == "Changes"), "temporary variable changes");

        var doFolders = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "Do"));
        AssertFolderGuide(doFolders.Single(row => row.Label == "Audio"), "play, pause");
        AssertFolderGuide(doFolders.Single(row => row.Label == "Effects"), "visual effects");
        AssertFolderGuide(doFolders.Single(row => row.Label == "Lighting"), "world lighting");
        AssertFolderGuide(doFolders.Single(row => row.Label == "Scene Object"), "objects placed");
        AssertFolderGuide(doFolders.Single(row => row.Label == "UI & Feedback"), "show messages");
        AssertFolderGuide(doFolders.Single(row => row.Label == "Obby"), "checkpoints");

        var effectsFolders = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "Do", currentDomainPath: ["Effects"]));
        AssertFolderGuide(effectsFolders.Single(row => row.Label == "Particles"), "particle effects");

        var feedbackFolders = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "Do", currentDomainPath: ["UI & Feedback"]));
        AssertFolderGuide(feedbackFolders.Single(row => row.Label == "Built-In UI"), "built-in player interface");
        AssertFolderGuide(feedbackFolders.Single(row => row.Label == "Custom UI"), "UI objects");

        var lightingFolders = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "Do", currentDomainPath: ["Lighting"]));
        AssertFolderGuide(lightingFolders.Single(row => row.Label == "Light Settings"), "brightness");
        AssertFolderGuide(lightingFolders.Single(row => row.Label == "Light Reach"), "far and wide");

        var sceneFolders = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "Do", currentDomainPath: ["Scene Object"]));
        AssertFolderGuide(sceneFolders.Single(row => row.Label == "3D Image Display"), "world images");
        AssertFolderGuide(sceneFolders.Single(row => row.Label == "3D Image Texture"), "texture");
        AssertFolderGuide(sceneFolders.Single(row => row.Label == "3D Text Content"), "3D text");
        AssertFolderGuide(sceneFolders.Single(row => row.Label == "3D Text Display"), "outline");
        AssertFolderGuide(sceneFolders.Single(row => row.Label == "Animation"), "smoothly animate");
        AssertFolderGuide(sceneFolders.Single(row => row.Label == "Dimensions"), "width");
        AssertFolderGuide(sceneFolders.Single(row => row.Label == "Lifecycle"), "lifecycle moment");
        AssertFolderGuide(sceneFolders.Single(row => row.Label == "Mesh Animation"), "mesh objects");
        AssertFolderGuide(sceneFolders.Single(row => row.Label == "NPC Health"), "NPC health");
        AssertFolderGuide(sceneFolders.Single(row => row.Label == "NPC Movement"), "move NPCs");
        AssertFolderGuide(sceneFolders.Single(row => row.Label == "Seats"), "sitting");
        AssertFolderGuide(sceneFolders.Single(row => row.Label == "Scaling"), "stretch");
        AssertFolderGuide(sceneFolders.Single(row => row.Label == "Transparency"), "see-through");

        var zoneFolders = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "When", currentDomainPath: ["Scene Object"]));
        AssertFolderGuide(zoneFolders.Single(row => row.Label == "Zones"), "watched area");

        var playerFolders = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "Do", currentDomainPath: ["Players"]));
        AssertFolderGuide(playerFolders.Single(row => row.Label == "Default Health"), "health");
        AssertFolderGuide(playerFolders.Single(row => row.Label == "Default Movement"), "default player movement");
        AssertFolderGuide(playerFolders.Single(row => row.Label == "Game Team"), "game team assigned");
        AssertFolderGuide(playerFolders.Single(row => row.Label == "Tools"), "react to tools");

        var checkFolders = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "Check"));
        AssertFolderGuide(checkFolders.Single(row => row.Label == "Math"), "numeric values");
        AssertFolderGuide(checkFolders.Single(row => row.Label == "Text"), "text values");
        AssertFolderGuide(checkFolders.Single(row => row.Label == "Obby"), "checkpoints");
    }

    [Fact]
    public void Browse_ValueThenMathShowsNestedFoldersAndNodes()
    {
        var catalog = LoadCatalog();

        var valueDomains = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "Value"));
        Assert.Contains(valueDomains, row => row.Kind == NodePaletteBrowserRowKind.Folder && row.Label == "Math");
        Assert.Contains(valueDomains, row => row.Kind == NodePaletteBrowserRowKind.Folder && row.Label == "Text");
        Assert.Contains(valueDomains, row => row.Kind == NodePaletteBrowserRowKind.Folder && row.Label == "Variables");

        var mathFolders = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "Value", currentDomainPath: ["Math"]));
        Assert.Contains(mathFolders, row => row.Kind == NodePaletteBrowserRowKind.Folder && row.Label == "Arithmetic");
        Assert.Contains(mathFolders, row => row.Kind == NodePaletteBrowserRowKind.Folder && row.Label == "Range");
        Assert.Contains(mathFolders, row => row.Kind == NodePaletteBrowserRowKind.Folder && row.Label == "Vector");

        var arithmeticNodes = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "Value", currentDomainPath: ["Math", "Arithmetic"]));
        Assert.Contains(arithmeticNodes, row => row.Kind == NodePaletteBrowserRowKind.Node && row.Entry?.IdBase == "PROP_AddNumbers");
        Assert.Contains(arithmeticNodes, row => row.Kind == NodePaletteBrowserRowKind.Node && row.Entry?.IdBase == "PROP_MultiplyNumbers");

        var rangeNodes = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "Value", currentDomainPath: ["Math", "Range"]));
        Assert.Contains(rangeNodes, row => row.Kind == NodePaletteBrowserRowKind.Node && row.Entry?.IdBase == "PROP_ClampNumber");
    }

    [Fact]
    public void Browse_EssentialsAliasesAppearAsFoldersAndSearchDeduplicatesNodes()
    {
        var catalog = LoadCatalog();

        var doFolders = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "Do"));
        Assert.Contains(doFolders, row => row.Kind == NodePaletteBrowserRowKind.Folder && row.Label == "Essentials");

        var essentialsFolders = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "Do", currentDomainPath: ["Essentials"]));
        Assert.Contains(essentialsFolders, row => row.Kind == NodePaletteBrowserRowKind.Folder && row.Label == "Flow & Timing");
        Assert.Contains(essentialsFolders, row => row.Kind == NodePaletteBrowserRowKind.Folder && row.Label == "Game Rules");
        Assert.Contains(essentialsFolders, row => row.Kind == NodePaletteBrowserRowKind.Folder && row.Label == "Scene Object");

        var flowNodes = service.Browse(catalog.Nodes, BrowserOptions(currentIntentKey: "Do", currentDomainPath: ["Essentials", "Flow & Timing"]));
        Assert.Contains(flowNodes, row => row.Kind == NodePaletteBrowserRowKind.Node && row.Entry?.IdBase == "ACT_StartCooldown");
        Assert.Contains(flowNodes, row => row.Kind == NodePaletteBrowserRowKind.Node && row.Entry?.IdBase == "ACT_WaitSeconds");

        var searchRows = service.Browse(catalog.Nodes, BrowserOptions(search: "essentials"));
        var cooldown = Assert.Single(searchRows, row => row.Entry?.IdBase == "ACT_StartCooldown");
        Assert.False(string.IsNullOrWhiteSpace(cooldown.MatchSummary));
    }

    [Fact]
    public void Browse_SearchRanksVocabularyMatchesAndExposesMatchSummary()
    {
        var catalog = LoadCatalog();

        var rows = service.Browse(catalog.Nodes, BrowserOptions(search: "kll player"));
        var killIndex = rows
            .Select((row, index) => new { Row = row, Index = index })
            .Single(item => item.Row.Entry?.IdBase == "ACT_KillPlayer")
            .Index;
        var kill = rows[killIndex];

        Assert.InRange(killIndex, 0, 5);
        Assert.False(string.IsNullOrWhiteSpace(kill.MatchSummary));
        Assert.Contains("Match:", kill.TooltipText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Browse_SearchKilPlayerRanksKillPlayerFirstAndAvoidsWillNoise()
    {
        var catalog = LoadCatalog();

        var rows = service.Browse(catalog.Nodes, BrowserOptions(search: "kil player"));

        Assert.Equal("ACT_KillPlayer", rows.First(row => row.Kind == NodePaletteBrowserRowKind.Node).Entry?.IdBase);
        Assert.DoesNotContain(rows.Take(10), row => row.MatchSummary.Contains("kil -> will", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Browse_SearchUsesSynonymsForTouchAndCollision()
    {
        var catalog = LoadCatalog();

        var rows = service.Browse(catalog.Nodes, BrowserOptions(search: "hit player"));

        Assert.Contains(rows.Take(10), row => row.Entry?.IdBase is "EV_OnPlayerTouchedObject" or "EV_OnTouchObject");
        Assert.All(rows.Where(row => row.Kind == NodePaletteBrowserRowKind.Node), row => Assert.False(string.IsNullOrWhiteSpace(row.MatchSummary)));
    }

    [Fact]
    public void Browse_CanvasModeHidesValueRecipes()
    {
        var catalog = LoadCatalog();
        var canvasEntries = catalog.Nodes.Where(entry => entry.Kind != NodeKind.Property).ToList();

        var root = service.Browse(canvasEntries, BrowserOptions());
        var addSearch = service.Browse(canvasEntries, BrowserOptions(search: "add numbers"));
        var randomColorSearch = service.Browse(canvasEntries, BrowserOptions(search: "random color"));
        var variableActions = service.Browse(canvasEntries, BrowserOptions(currentIntentKey: "Do", currentDomainPath: ["Variables", "Script"]));
        var variableNumberActions = service.Browse(canvasEntries, BrowserOptions(currentIntentKey: "Do", currentDomainPath: ["Variables", "Script Numbers"]));

        Assert.Equal(["When", "Do", "Check"], root.Select(row => row.Label).ToArray());
        Assert.DoesNotContain(addSearch, row => row.Entry?.Kind == NodeKind.Property);
        Assert.Empty(randomColorSearch);
        Assert.Contains(variableActions, row => row.Kind == NodePaletteBrowserRowKind.Node && row.Entry?.IdBase == "ACT_SetScriptVariable");
        Assert.Contains(variableNumberActions, row => row.Kind == NodePaletteBrowserRowKind.Node && row.Entry?.IdBase == "ACT_IncrementScriptNumber");
    }

    [Theory]
    [InlineData("print", "ACT_ShowMessage")]
    [InlineData("variable", "ACT_SetScriptVariable")]
    [InlineData("color", "ACT_SetObjectColor")]
    [InlineData("server", "EV_OnTimerTick")]
    [InlineData("boolean", "COND_BooleanCheck")]
    [InlineData("spin", "ACT_RotateObject")]
    [InlineData("hide", "ACT_SetObjectVisible")]
    [InlineData("score", "ACT_IncrementScriptNumber")]
    [InlineData("random color", "PROP_RandomColor")]
    public void Browse_SearchIsGlobal(string search, string expectedId)
    {
        var catalog = LoadCatalog();

        var rows = service.Browse(catalog.Nodes, BrowserOptions(search: search));

        Assert.Contains(rows, row => row.Kind == NodePaletteBrowserRowKind.Node && row.Entry?.IdBase == expectedId);
    }

    [Fact]
    public void Browse_CompatibleOnlyCanHideOrExplainInvalidNodes()
    {
        var catalog = LoadCatalog();

        var visible = service.Browse(catalog.Nodes, BrowserOptions(
            search: "show message",
            compatibleOnly: false,
            incompatibilityReason: entry => entry.IdBase == "ACT_ShowMessage" ? "Needs Client script" : ""));
        var compatibleOnly = service.Browse(catalog.Nodes, BrowserOptions(
            search: "show message",
            compatibleOnly: true,
            incompatibilityReason: entry => entry.IdBase == "ACT_ShowMessage" ? "Needs Client script" : ""));

        var disabled = visible.Single(row => row.Entry?.IdBase == "ACT_ShowMessage");
        Assert.False(disabled.IsCompatible);
        Assert.Equal("Needs Client script", disabled.IncompatibilityReason);
        Assert.Equal("!", disabled.IconGlyph);
        Assert.False(string.IsNullOrWhiteSpace(disabled.MatchSummary));
        Assert.Contains("Needs Client script", disabled.TooltipText, StringComparison.Ordinal);
        Assert.DoesNotContain(compatibleOnly, row => row.Entry?.IdBase == "ACT_ShowMessage");
    }

    [Fact]
    public void Source_NodePaletteSearchRenderingShowsResultCountAndMatchSummary()
    {
        var sourcePath = Path.Combine(TestPaths.RepositoryRoot, "src", "Vrs.App", "Controls", "RuleGraphCanvas.NodePalette.Rendering.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("Search Results ·", source, StringComparison.Ordinal);
        Assert.Contains("MatchSummary", source, StringComparison.Ordinal);
        Assert.Contains("synonyms", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Source_NodePaletteSearchDoesNotPreLimitCatalogBeforeSearch()
    {
        var sourcePath = Path.Combine(TestPaths.RepositoryRoot, "src", "Vrs.App", "Controls", "RuleGraphCanvas.NodePalette.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain(".Take(260)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_GroupsByBeginnerIntentAndDomain()
    {
        var catalog = LoadCatalog();

        var rows = service.Query(catalog.Nodes, Options());

        var timer = rows.Single(row => row.Entry.IdBase == "EV_OnTimerTick");
        var showMessage = rows.Single(row => row.Entry.IdBase == "ACT_ShowMessage");
        var textContains = rows.Single(row => row.Entry.IdBase == "COND_TextContains");

        Assert.Equal("When", timer.IntentLabel);
        Assert.Equal("Flow & Timing", timer.DomainLabel);
        Assert.Equal("When / Flow & Timing", timer.GroupHeader);
        Assert.Equal("Do", showMessage.IntentLabel);
        Assert.Equal("UI & Feedback", showMessage.DomainLabel);
        Assert.Equal("Check", textContains.IntentLabel);
        Assert.Equal("Text", textContains.DomainLabel);
    }

    [Fact]
    public void Query_GroupsGameplayApiDomainsAndRespectsClientOnlyInput()
    {
        var catalog = LoadCatalog();

        var serverRows = service.Query(catalog.Nodes, Options(
            incompatibilityReason: entry => NodeCatalogService.IsCompatibleWithScriptKind(entry, GraphScriptKind.Server) ? "" : "Needs Server script"));
        Assert.Equal("Players", serverRows.Single(row => row.Entry.IdBase == "EV_OnPlayerJoined").DomainLabel);
        Assert.Equal("UI & Feedback", serverRows.Single(row => row.Entry.IdBase == "ACT_BroadcastChatMessage").DomainLabel);
        Assert.Equal("Players", serverRows.Single(row => row.Entry.IdBase == "ACT_SetWalkSpeed").DomainLabel);
        Assert.Equal("Scene Object", serverRows.Single(row => row.Entry.IdBase == "COND_ObjectHasTag").DomainLabel);
        Assert.Equal("Scene Object", serverRows.Single(row => row.Entry.IdBase == "PROP_FindChild").DomainLabel);
        Assert.Equal("Scene Object", serverRows.Single(row => row.Entry.IdBase == "ACT_TweenObjectPosition").DomainLabel);
        Assert.DoesNotContain(serverRows, row => row.Entry.IdBase == "EV_OnInputButtonDown");

        var localRows = service.Query(catalog.Nodes, Options(
            scriptKind: GraphScriptKind.Local,
            incompatibilityReason: entry => NodeCatalogService.IsCompatibleWithScriptKind(entry, GraphScriptKind.Local) ? "" : "Needs Local script"));
        Assert.Equal("Players", localRows.Single(row => row.Entry.IdBase == "EV_OnInputButtonDown").DomainLabel);
        Assert.Equal("Players", localRows.Single(row => row.Entry.IdBase == "PROP_InputAxisValue").DomainLabel);
    }

    [Fact]
    public void Query_IncludesValueNodesByDefault()
    {
        var catalog = LoadCatalog();

        var rows = service.Query(catalog.Nodes, Options());

        var addNumbers = rows.Single(row => row.Entry.IdBase == "PROP_AddNumbers");
        Assert.Equal("Value", addNumbers.IntentLabel);
        Assert.Equal("Math", addNumbers.DomainLabel);
        Assert.Contains("add two numbers", addNumbers.BeginnerSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Query_FiltersByIntentAndDomain()
    {
        var catalog = LoadCatalog();
        var valueOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Value" };

        var rows = service.Query(catalog.Nodes, Options(enabledIntentKeys: valueOnly, domainFilter: "Math"));

        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            Assert.Equal("Value", row.IntentKey);
            Assert.Equal("Math", row.DomainLabel);
        });
        Assert.Contains(rows, row => row.Entry.IdBase == "PROP_AddNumbers");
    }

    [Fact]
    public void Query_CanExposeDisabledChoicesWithReasons()
    {
        var catalog = LoadCatalog();

        var visible = service.Query(catalog.Nodes, Options(compatibleOnly: false, incompatibilityReason: entry =>
            entry.IdBase == "ACT_ShowMessage" ? "Needs Client script" : ""));
        var compatibleOnly = service.Query(catalog.Nodes, Options(compatibleOnly: true, incompatibilityReason: entry =>
            entry.IdBase == "ACT_ShowMessage" ? "Needs Client script" : ""));

        var disabled = visible.Single(row => row.Entry.IdBase == "ACT_ShowMessage");
        Assert.False(disabled.IsCompatible);
        Assert.Equal("Needs Client script", disabled.IncompatibilityReason);
        Assert.DoesNotContain(compatibleOnly, row => row.Entry.IdBase == "ACT_ShowMessage");
    }

    [Fact]
    public void Query_ReportsFriendlyRuntimeBadges()
    {
        var catalog = LoadCatalog();

        var rows = service.Query(catalog.Nodes, Options());

        Assert.Equal("Server", rows.Single(row => row.Entry.IdBase == "EV_OnTimerTick").RuntimeLabel);
        Assert.Equal("Shared", rows.Single(row => row.Entry.IdBase == "ACT_ShowMessage").RuntimeLabel);
    }

    [Fact]
    public void GetDomainOptions_UsesCurrentSearchIntentAndCompatibility()
    {
        var catalog = LoadCatalog();
        var domains = service.GetDomainOptions(catalog.Nodes, Options(search: "value"));

        Assert.Contains("Math", domains);
        Assert.Contains("Variables", domains);
    }

    private static NodeCatalogData LoadCatalog()
    {
        return new NodeCatalogService().LoadCatalog(TestPaths.CatalogRoot);
    }

    private static void AssertPalettePresentation(NodePaletteBrowserRow row)
    {
        Assert.False(string.IsNullOrWhiteSpace(row.IconGlyph));
        Assert.False(string.IsNullOrWhiteSpace(row.IconAccentHex));
        Assert.False(string.IsNullOrWhiteSpace(row.IconBackgroundHex));
        Assert.False(string.IsNullOrWhiteSpace(row.TooltipTitle));
        Assert.False(string.IsNullOrWhiteSpace(row.TooltipText));
    }

    private static void AssertFolderGuide(NodePaletteBrowserRow row, string expectedText)
    {
        Assert.Equal(NodePaletteBrowserRowKind.Folder, row.Kind);
        Assert.Equal(row.Label, row.TooltipTitle);
        Assert.Contains(expectedText, row.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedText, row.TooltipText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compatible", row.TooltipText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"{row.Label} folder", row.TooltipText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"^\d+ compatible / \d+ total$", row.Description);
    }

    private static NodePaletteQueryOptions Options(
        string search = "",
        bool compatibleOnly = true,
        IReadOnlySet<string>? enabledIntentKeys = null,
        string domainFilter = "",
        Func<NodeCatalogEntry, string?>? incompatibilityReason = null,
        GraphScriptKind scriptKind = GraphScriptKind.Server)
    {
        return new NodePaletteQueryOptions(
            Search: search,
            ScriptKind: scriptKind,
            CompatibleOnly: compatibleOnly,
            EnabledIntentKeys: enabledIntentKeys ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "When", "Do", "Check", "Value" },
            DomainFilter: domainFilter,
            IncompatibilityReason: incompatibilityReason ?? (_ => ""));
    }

    private static NodePaletteBrowserQueryOptions BrowserOptions(
        string search = "",
        bool compatibleOnly = true,
        string currentIntentKey = "",
        IReadOnlyList<string>? currentDomainPath = null,
        Func<NodeCatalogEntry, string?>? incompatibilityReason = null,
        GraphScriptKind scriptKind = GraphScriptKind.Server)
    {
        return new NodePaletteBrowserQueryOptions(
            Search: search,
            ScriptKind: scriptKind,
            CompatibleOnly: compatibleOnly,
            CurrentIntentKey: currentIntentKey,
            CurrentDomainPath: currentDomainPath ?? [],
            IncompatibilityReason: incompatibilityReason ?? (_ => ""));
    }
}
