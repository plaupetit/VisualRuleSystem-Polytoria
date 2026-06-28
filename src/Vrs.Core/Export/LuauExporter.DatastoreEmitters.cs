using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static bool TryAppendReadableDatastoreActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (action.Type.Equals("SaveDatastoreValue", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableDatastoreActionHeader(builder, rule, action, nodesById, indentLevel, "Save Saved Value", "SetAsync");
            var indent = IndentText(indentLevel);
            var value = ParameterExpression(rule, action, nodesById, "value", "String", "0");
            builder.AppendLine($"{indent}savedStore:SetAsync(tostring(savedEntryName), {value.Code})");
            AppendReadableDatastoreDisconnect(builder, indent);
            return true;
        }

        if (action.Type.Equals("RemoveDatastoreValue", StringComparison.OrdinalIgnoreCase))
        {
            AppendReadableDatastoreActionHeader(builder, rule, action, nodesById, indentLevel, "Remove Saved Value", "RemoveAsync");
            var indent = IndentText(indentLevel);
            builder.AppendLine($"{indent}savedStore:RemoveAsync(tostring(savedEntryName))");
            AppendReadableDatastoreDisconnect(builder, indent);
            return true;
        }

        return false;
    }

    private static void AppendReadableDatastoreActionHeader(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string readableName,
        string requiredMethod)
    {
        var indent = IndentText(indentLevel);
        var storeKey = ParameterExpression(rule, action, nodesById, "storeKey", "String", "PlayerData");
        var entryKey = ParameterExpression(rule, action, nodesById, "entryKey", "String", "Coins");

        builder.AppendLine($"{indent}if Datastore == nil or Datastore.GetDatastore == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: saved data is not available.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local savedStoreName = tostring({storeKey.Code})");
        builder.AppendLine($"{indent}local savedEntryName = tostring({entryKey.Code})");
        builder.AppendLine($"{indent}local savedStore = Datastore:GetDatastore(savedStoreName)");
        builder.AppendLine($"{indent}if savedStore == nil or savedStore.{requiredMethod} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: saved data store could not be opened.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private static LuauExpression? TryResolveReadableDatastorePropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        if (node.Type.Equals("DatastoreValue", StringComparison.OrdinalIgnoreCase))
        {
            var storeKey = PropertyParameterExpression(rule, node, nodesById, "storeKey", "String", "PlayerData", visitedNodeIds);
            var entryKey = PropertyParameterExpression(rule, node, nodesById, "entryKey", "String", "Coins", visitedNodeIds);
            var fallback = PropertyParameterExpression(rule, node, nodesById, "fallbackValue", "String", "", visitedNodeIds);
            var code = $"(function() if Datastore == nil or Datastore.GetDatastore == nil then return {fallback.Code} end local savedStore = Datastore:GetDatastore(tostring({storeKey.Code})); if savedStore == nil or savedStore.GetAsync == nil then return {fallback.Code} end local savedValue = savedStore:GetAsync(tostring({entryKey.Code})); if savedStore.Disconnect ~= nil then savedStore:Disconnect() end; if savedValue == nil then return {fallback.Code} end; return savedValue end)()";
            return new LuauExpression(code, "Any");
        }

        if (node.Type.Equals("DatastoreKey", StringComparison.OrdinalIgnoreCase))
        {
            var storeKey = PropertyParameterExpression(rule, node, nodesById, "storeKey", "String", "PlayerData", visitedNodeIds);
            var code = $"(function() if Datastore == nil or Datastore.GetDatastore == nil then return \"\" end local savedStore = Datastore:GetDatastore(tostring({storeKey.Code})); if savedStore == nil then return \"\" end local savedStoreKey = tostring(savedStore.Key or \"\"); if savedStore.Disconnect ~= nil then savedStore:Disconnect() end; return savedStoreKey end)()";
            return new LuauExpression(code, "String");
        }

        return null;
    }

    private static void AppendReadableDatastoreDisconnect(StringBuilder builder, string indent)
    {
        builder.AppendLine($"{indent}if savedStore.Disconnect ~= nil then");
        builder.AppendLine($"{indent}    savedStore:Disconnect()");
        builder.AppendLine($"{indent}end");
    }
}
