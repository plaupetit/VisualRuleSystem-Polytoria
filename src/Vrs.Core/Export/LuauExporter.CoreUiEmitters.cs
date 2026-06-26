using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static bool TryAppendReadableCoreUiActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (!action.Type.Equals("SetBuiltInUIVisible", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indent = IndentText(indentLevel);
        var feature = ParameterExpression(rule, action, nodesById, "feature", "String", "Chat");
        var enabled = ParameterExpression(rule, action, nodesById, "enabled", "Boolean", "true");
        AppendCoreUiAvailabilityGuard(builder, indentLevel, "Set Built-In UI Visible");
        builder.AppendLine($"{indent}local builtInUiFeature = tostring({feature.Code})");
        builder.AppendLine($"{indent}local builtInUiProperty = ({{");
        builder.AppendLine($"{indent}    Chat = \"UseChat\",");
        builder.AppendLine($"{indent}    Leaderboard = \"UseLeaderboard\",");
        builder.AppendLine($"{indent}    [\"Health Bar\"] = \"UseHealthBar\",");
        builder.AppendLine($"{indent}    Hotbar = \"UseHotbar\",");
        builder.AppendLine($"{indent}    Backpack = \"UseBackpack\",");
        builder.AppendLine($"{indent}    [\"Menu Button\"] = \"UseMenuButton\",");
        builder.AppendLine($"{indent}    [\"Emote Wheel\"] = \"UseEmoteWheel\",");
        builder.AppendLine($"{indent}    [\"User Card\"] = \"UseUserCard\",");
        builder.AppendLine($"{indent}    Respawn = \"CanRespawn\"");
        builder.AppendLine($"{indent}}})[builtInUiFeature]");
        builder.AppendLine($"{indent}if builtInUiProperty == nil then");
        builder.AppendLine($"{indent}    print(\"Set Built-In UI Visible stopped: UI part is not supported.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if CoreUI[builtInUiProperty] == nil then");
        builder.AppendLine($"{indent}    print(\"Set Built-In UI Visible stopped: CoreUI does not expose \" .. builtInUiProperty .. \".\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}CoreUI[builtInUiProperty] = {enabled.Code}");
        return true;
    }

    private static LuauExpression? TryResolveReadableCoreUiPropertyExpression(RuleNode node)
    {
        return node.Type switch
        {
            "BuiltInChatVisible" => CoreUiValueExpression("UseChat", "Chat Is Visible"),
            "BuiltInLeaderboardVisible" => CoreUiValueExpression("UseLeaderboard", "Leaderboard Is Visible"),
            "BuiltInHealthBarVisible" => CoreUiValueExpression("UseHealthBar", "Health Bar Is Visible"),
            "BuiltInHotbarVisible" => CoreUiValueExpression("UseHotbar", "Hotbar Is Visible"),
            "BuiltInBackpackAvailable" => CoreUiValueExpression("UseBackpack", "Backpack Is Available"),
            "BuiltInMenuButtonVisible" => CoreUiValueExpression("UseMenuButton", "Menu Button Is Visible"),
            "BuiltInEmoteWheelVisible" => CoreUiValueExpression("UseEmoteWheel", "Emote Wheel Is Visible"),
            "BuiltInUserCardVisible" => CoreUiValueExpression("UseUserCard", "User Card Is Visible"),
            "PlayerCanRespawn" => CoreUiValueExpression("CanRespawn", "Player Can Respawn"),
            _ => null
        };
    }

    private static void AppendCoreUiAvailabilityGuard(StringBuilder builder, int indentLevel, string readableName)
    {
        var indent = IndentText(indentLevel);
        builder.AppendLine($"{indent}if CoreUI == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: CoreUI is not available.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
    }

    private static LuauExpression CoreUiValueExpression(string propertyName, string readableName)
    {
        var code = $"(function() if CoreUI == nil then print(\"{readableName} stopped: CoreUI is not available.\"); return false end; if CoreUI.{propertyName} == nil then print(\"{readableName} stopped: CoreUI does not expose {propertyName}.\"); return false end; return CoreUI.{propertyName} == true end)()";
        return new LuauExpression(code, "Boolean");
    }
}
