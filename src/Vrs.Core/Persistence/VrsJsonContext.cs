using System.Text.Json.Serialization;
using Vrs.Core.Bridge;
using Vrs.Core.Catalog;
using Vrs.Core.ProjectInputs;
using Vrs.Core.Validation;
using Vrs.Graph.Model;

namespace Vrs.Core.Persistence;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(RuleGraph))]
[JsonSerializable(typeof(NodeCatalogEntry))]
[JsonSerializable(typeof(NodeCatalogApiReference))]
[JsonSerializable(typeof(NodeCatalogData))]
[JsonSerializable(typeof(NodeCatalogParameterVisibilityCondition))]
[JsonSerializable(typeof(ValidationResult))]
[JsonSerializable(typeof(ActivePolytoriaProjectConfig))]
[JsonSerializable(typeof(SceneSnapshot))]
[JsonSerializable(typeof(BridgeCommandEnvelope))]
[JsonSerializable(typeof(CommandResults))]
[JsonSerializable(typeof(CommandResultEntry))]
[JsonSerializable(typeof(CommandResultDetails))]
[JsonSerializable(typeof(BridgeStatus))]
[JsonSerializable(typeof(AppHeartbeat))]
[JsonSerializable(typeof(VrsManagedInputRegistry))]
[JsonSerializable(typeof(VrsManagedInputRecord))]
[JsonSerializable(typeof(VrsManagedInputUsage))]
public partial class VrsJsonContext : JsonSerializerContext;
