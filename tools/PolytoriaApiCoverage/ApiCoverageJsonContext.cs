using System.Text.Json.Serialization;
using Vrs.Core.Catalog;

namespace Vrs.Tools.PolytoriaApiCoverage;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ApiCoverageResult))]
[JsonSerializable(typeof(NodeCatalogApiReference))]
public sealed partial class ApiCoverageJsonContext : JsonSerializerContext;
