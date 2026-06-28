using Vrs.Tools.PolytoriaApiCoverage;

namespace Vrs.Tests;

public sealed class PolytoriaApiCoverageTests
{
    [Fact]
    public void Parser_LoadsOfficialStylePlayerYamlWithMembers()
    {
        const string yaml = """
Name: Player
Description: Player represents a user playing the game.
Properties:
  - Name: UserID
    Type: number
Methods:
  - Name: Respawn
    ReturnType: nil
Events:
  - Name: Chatted
    Parameters: []
""";

        var type = PolytoriaYamlParser.ParseType(yaml);

        Assert.Equal("Player", type.Name);
        Assert.Contains(type.Properties, member => member.Name == "UserID" && member.Kind == "Property");
        Assert.Contains(type.Methods, member => member.Name == "Respawn" && member.Kind == "Method");
        Assert.Contains(type.Events, member => member.Name == "Chatted" && member.Kind == "Event");
    }

    [Fact]
    public void Parser_LoadsOfficialStyleEnumYaml()
    {
        const string yaml = """
Name: KeyCode
InternalName: KeyCodeEnum
Options:
  - Name: Space
  - Name: A
""";

        var apiEnum = PolytoriaYamlParser.ParseEnum(yaml);

        Assert.Equal("KeyCode", apiEnum.Name);
        Assert.Equal("KeyCodeEnum", apiEnum.InternalName);
        Assert.Contains("Space", apiEnum.Options);
        Assert.Contains("A", apiEnum.Options);
    }

    [Fact]
    public void Analyzer_ClassifiesExplicitReferencesAndApiTypeFallbacks()
    {
        var root = CreateTempDirectory();
        try
        {
            var catalogRoot = Path.Combine(root, "catalog");
            WriteManifest(
                catalogRoot,
                "Actions",
                "PlayerRespawn",
                """
{
  "moduleId": "test",
  "kind": "Action",
  "idBase": "ACT_PlayerRespawn",
  "type": "PlayerRespawn",
  "label": "Respawn Player",
  "apiType": "Player.Respawn",
  "apiReferences": [
    { "type": "Player", "memberKind": "Method", "member": "Respawn", "coverage": "Direct" }
  ]
}
""");
            WriteManifest(
                catalogRoot,
                "Properties",
                "NetworkEventValue",
                """
{
  "moduleId": "test",
  "kind": "Property",
  "idBase": "PROP_NetworkEvent",
  "type": "NetworkEventValue",
  "label": "Network Event Value",
  "apiType": "NetworkEvent"
}
""");

            var result = PolytoriaApiCoverageAnalyzer.Generate(CreateSampleSource(), catalogRoot, DateTimeOffset.UnixEpoch);

            Assert.Equal(2, result.Catalog.TotalNodes);
            Assert.Equal(1, result.Catalog.NodesByKind["Action"]);
            Assert.Equal(1, result.Catalog.NodesByKind["Property"]);
            Assert.Equal(66.67, result.Summary.TypesWithAnyCoveragePercent);
            Assert.Equal(33.33, result.Summary.DirectTypePercent);
            Assert.Equal(3, result.Summary.TargetRuntimeTypes);
            Assert.Equal(2, result.Summary.TargetRuntimeTypesWithCoverage);
            Assert.Equal(66.67, result.Summary.TargetRuntimeCoveragePercent);
            Assert.Equal(3, result.Summary.GameplayApiTypes);
            Assert.Equal(2, result.Summary.GameplayApiTypesWithCoverage);
            Assert.Equal(0, result.Summary.CreatorApiTypes);
            Assert.Equal(0.0, result.Summary.LowConfidenceNodePercent);
            Assert.Contains(result.TypeRows, row => row.Type == "Player" && row.Coverage == "Direct");
            Assert.Contains(result.TypeRows, row => row.Type == "Player" && row.ApiSurface == "Gameplay");
            Assert.Contains(result.TypeRows, row => row.Type == "NetworkEvent" && row.Coverage == "Partial" && row.Category == "Input/Network");
            Assert.Contains(result.TypeRows, row => row.Type == "NetworkEvent" && row.Confidence == "AutoVerified");
            Assert.Contains(result.Roadmap, row => row.Type == "UncoveredType" && row.SuggestedNodePack.Contains("Camera/World", StringComparison.Ordinal));
            Assert.Contains(result.NodeRows, row => row.NodeId == "PROP_NetworkEvent" && row.Confidence == "AutoVerified");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Analyzer_SplitsGameplayAndCreatorApiSurfaces()
    {
        var root = CreateTempDirectory();
        try
        {
            var catalogRoot = Path.Combine(root, "catalog");
            WriteManifest(
                catalogRoot,
                "Actions",
                "SetGuiVisible",
                """
{
  "moduleId": "test",
  "kind": "Action",
  "idBase": "ACT_SetGuiVisible",
  "type": "SetGuiVisible",
  "label": "Set UI Visible",
  "apiReferences": [
    { "type": "GUI", "memberKind": "Property", "member": "Visible", "coverage": "Direct" }
  ]
}
""");
            WriteManifest(
                catalogRoot,
                "Actions",
                "AddonBridgeTool",
                """
{
  "moduleId": "test",
  "kind": "Action",
  "idBase": "ACT_AddonBridgeTool",
  "type": "AddonBridgeTool",
  "label": "Addon Bridge Tool",
  "apiReferences": [
    { "type": "AddonBridge", "memberKind": "Method", "member": "Run", "coverage": "Direct" }
  ]
}
""");

            var result = PolytoriaApiCoverageAnalyzer.Generate(CreateSurfaceSampleSource(), catalogRoot, DateTimeOffset.UnixEpoch);

            Assert.Equal(2, result.Summary.GameplayApiTypes);
            Assert.Equal(1, result.Summary.GameplayApiTypesWithCoverage);
            Assert.Equal(1, result.Summary.CreatorApiTypes);
            Assert.Equal(1, result.Summary.CreatorApiTypesWithCoverage);
            Assert.Contains(result.TypeRows, row => row.Type == "GUI" && row.ApiSurface == "Gameplay" && row.Category == "UI");
            Assert.Contains(result.TypeRows, row => row.Type == "AddonBridge" && row.ApiSurface == "Creator" && row.Category == "Creator/Addons");
            Assert.DoesNotContain(result.Roadmap, item => item.Type == "AddonBridge");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MarkdownReport_IncludesSourcesTotalsAndLowConfidenceSection()
    {
        var root = CreateTempDirectory();
        try
        {
            var catalogRoot = Path.Combine(root, "catalog");
            WriteManifest(
                catalogRoot,
                "Actions",
                "NoApiMetadata",
                """
{
  "moduleId": "test",
  "kind": "Action",
  "idBase": "ACT_NoApiMetadata",
  "type": "NoApiMetadata",
  "label": "No Api Metadata"
}
""");

            var result = PolytoriaApiCoverageAnalyzer.Generate(CreateSampleSource(), catalogRoot, DateTimeOffset.UnixEpoch);
            var markdown = ApiCoverageMarkdownWriter.Write(result);

            Assert.Contains("# Polytoria API Coverage", markdown, StringComparison.Ordinal);
            Assert.Contains("Official API types", markdown, StringComparison.Ordinal);
            Assert.Contains("Coverage Percentages", markdown, StringComparison.Ordinal);
            Assert.Contains("API types with any VRS coverage", markdown, StringComparison.Ordinal);
            Assert.Contains("Gameplay API coverage", markdown, StringComparison.Ordinal);
            Assert.Contains("Creator API coverage", markdown, StringComparison.Ordinal);
            Assert.Contains("VRS target runtime coverage", markdown, StringComparison.Ordinal);
            Assert.Contains("Coverage By API Surface", markdown, StringComparison.Ordinal);
            Assert.Contains("Coverage By API Family", markdown, StringComparison.Ordinal);
            Assert.Contains("Gameplay Infrastructure Not Prioritized", markdown, StringComparison.Ordinal);
            Assert.Contains("Do not chase these rows just to raise the broad Gameplay percentage", markdown, StringComparison.Ordinal);
            Assert.Contains("Creator / Non-Gameplay APIs", markdown, StringComparison.Ordinal);
            Assert.Contains("VRS Node Coverage Roadmap", markdown, StringComparison.Ordinal);
            Assert.Contains("Confidence labels", markdown, StringComparison.Ordinal);
            Assert.Contains("AutoVerified", markdown, StringComparison.Ordinal);
            Assert.Contains("Low Confidence / Needs Annotation", markdown, StringComparison.Ordinal);
            Assert.Contains("ACT_NoApiMetadata", markdown, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static PolytoriaApiSourceSnapshot CreateSampleSource()
    {
        return new PolytoriaApiSourceSnapshot(
            "test-docs",
            "docs-sha",
            DateTimeOffset.UnixEpoch,
            "test-lua",
            "lua-sha",
            DateTimeOffset.UnixEpoch,
            [
                new PolytoriaApiType(
                    "Player",
                    [new PolytoriaApiMember("UserID", "Property")],
                    [new PolytoriaApiMember("Respawn", "Method")],
                    [new PolytoriaApiMember("Chatted", "Event")]),
                new PolytoriaApiType("NetworkEvent", [], [new PolytoriaApiMember("InvokeServer", "Method")], []),
                new PolytoriaApiType("UncoveredType", [], [], [])
            ],
            [new PolytoriaApiEnum("KeyCode", "KeyCodeEnum", ["Space"])],
            [new PolytoriaApiGlobal("world", "Value")]);
    }

    private static PolytoriaApiSourceSnapshot CreateSurfaceSampleSource()
    {
        return new PolytoriaApiSourceSnapshot(
            "test-docs",
            "docs-sha",
            DateTimeOffset.UnixEpoch,
            "test-lua",
            "lua-sha",
            DateTimeOffset.UnixEpoch,
            [
                new PolytoriaApiType("Player", [], [], []),
                new PolytoriaApiType("GUI", [new PolytoriaApiMember("Visible", "Property")], [], []),
                new PolytoriaApiType("AddonBridge", [], [new PolytoriaApiMember("Run", "Method")], [])
            ],
            [],
            []);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "vrs-api-coverage-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteManifest(string catalogRoot, string kindFolder, string name, string json)
    {
        var directory = Path.Combine(catalogRoot, kindFolder, "Test", name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "manifest.vrs-node.json"), json);
    }
}
