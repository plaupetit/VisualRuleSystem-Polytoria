using Vrs.Core.Catalog;
using Vrs.Graph.Model;

namespace Vrs.App.ViewModels;

public sealed partial class NodeParameterEditorViewModel
{
    // Catalog value recipes are edited inline under the owning parameter. They
    // deliberately reuse catalog-created parameters so save/load and export keep
    // the same semantics as legacy property nodes.
    private void RefreshRecipeParameters()
    {
        RecipeParameters.Clear();
        if (SourceKind != GraphValueSourceKind.CatalogValue)
        {
            return;
        }

        var entry = FindRecipeEntry(parameter.Binding.CatalogId);
        if (entry is null)
        {
            return;
        }

        EnsureRecipeParameters(entry);
        foreach (var recipeParameter in parameter.Binding.CatalogParameters)
        {
            var recipeDefinition = entry.Parameters.FirstOrDefault(item =>
                item.Key.Equals(recipeParameter.Key, StringComparison.OrdinalIgnoreCase));
            RecipeParameters.Add(new NodeParameterEditorViewModel(
                recipeParameter,
                recipeDefinition,
                sceneObjects,
                catalogEntries,
                valueChanged,
                recipeDepth + 1));
        }
    }

    private void ApplyRecipeSelection(string catalogId)
    {
        var entry = FindRecipeEntry(catalogId);
        parameter.Binding.CatalogId = catalogId;
        parameter.SourceCatalogId = catalogId;
        parameter.Binding.CatalogType = entry?.Type ?? "";
        parameter.Binding.DataType = entry is null ? Type : RecipeOutputDataType(entry);
        if (entry is not null)
        {
            EnsureRecipeParameters(entry);
        }
        else
        {
            parameter.Binding.CatalogParameters.Clear();
        }

        SyncValueFromBinding();
        RefreshRecipeParameters();
    }

    private void EnsureRecipeParameters(NodeCatalogEntry entry)
    {
        if (parameter.Binding.CatalogParameters.Count > 0 &&
            parameter.Binding.CatalogType.Equals(entry.Type, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var recipeNode = NodeCatalogService.CreateNode(entry);
        parameter.Binding.CatalogParameters.Clear();
        parameter.Binding.CatalogParameters.AddRange(recipeNode.Parameters);
    }

    private NodeCatalogEntry? FindRecipeEntry(string catalogId)
    {
        if (string.IsNullOrWhiteSpace(catalogId))
        {
            return null;
        }

        return catalogEntries.FirstOrDefault(entry =>
            entry.IdBase.Equals(catalogId, StringComparison.OrdinalIgnoreCase) ||
            entry.Type.Equals(catalogId, StringComparison.OrdinalIgnoreCase));
    }

    private string RecipeLabel(string catalogId)
    {
        return FindRecipeEntry(catalogId)?.Label ?? catalogId;
    }
}
