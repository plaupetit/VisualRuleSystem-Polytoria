using Vrs.Graph.Model;

namespace Vrs.Core.Export;

/// <summary>
/// Controls optional generated-script sections without changing the authored graph.
/// </summary>
public sealed class LuauExportOptions
{
    /// <summary>
    /// Includes human-readable VRS/User comments around generated blocks.
    /// </summary>
    public bool IncludeComments { get; set; } = true;

    /// <summary>
    /// Includes legacy structural comments used by older flow export paths.
    /// </summary>
    public bool IncludeStructureComments { get; set; } = true;

    /// <summary>
    /// Appends the base64 graph metadata block used for script-to-graph round trips.
    /// </summary>
    public bool IncludeGraphMetadata { get; set; } = true;

    /// <summary>
    /// Forces a generated script role while leaving the persisted rule unchanged.
    /// </summary>
    public GraphScriptKind? ScriptKindOverride { get; set; }
}

/// <summary>
/// One generated Luau file, including the project filename suffix and human role label.
/// </summary>
public sealed class ExportedLuauFile
{
    public string Suffix { get; set; } = ".server.luau";
    public string Role { get; set; } = "Server";
    public string Content { get; set; } = "";
}
