using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static bool IsInputEventRuntimeNode(string nodeType)
        => nodeType.Equals("SendInputEvent", StringComparison.OrdinalIgnoreCase) ||
            nodeType.Equals("SendInputTextEvent", StringComparison.OrdinalIgnoreCase) ||
            nodeType.Equals("OnVrsInputEvent", StringComparison.OrdinalIgnoreCase);

    private static void AppendReadableInputEventRuntime(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("local function vrsInputEventName(actionName)");
        builder.AppendLine("    local text = tostring(actionName or \"\")");
        builder.AppendLine("    text = string.gsub(text, \"[/\\\\]\", \" \")");
        builder.AppendLine("    text = string.gsub(text, \"^%s*(.-)%s*$\", \"%1\")");
        builder.AppendLine("    if text == \"\" then");
        builder.AppendLine("        return \"Input\"");
        builder.AppendLine("    end");
        builder.AppendLine("    return text");
        builder.AppendLine("end");
        builder.AppendLine();
        builder.AppendLine("local function vrsFindInputChild(parent, name)");
        builder.AppendLine("    if parent == nil then");
        builder.AppendLine("        return nil");
        builder.AppendLine("    end");
        builder.AppendLine("    if parent.FindChild ~= nil then");
        builder.AppendLine("        local ok, child = pcall(function()");
        builder.AppendLine("            return parent:FindChild(name)");
        builder.AppendLine("        end)");
        builder.AppendLine("        if ok and child ~= nil then");
        builder.AppendLine("            return child");
        builder.AppendLine("        end");
        builder.AppendLine("    end");
        builder.AppendLine("    if parent.WaitChild ~= nil then");
        builder.AppendLine("        local ok, child = pcall(function()");
        builder.AppendLine("            return parent:WaitChild(name, 5)");
        builder.AppendLine("        end)");
        builder.AppendLine("        if ok then");
        builder.AppendLine("            return child");
        builder.AppendLine("        end");
        builder.AppendLine("    end");
        builder.AppendLine("    return nil");
        builder.AppendLine("end");
        builder.AppendLine();
        builder.AppendLine("local function vrsResolveInputNetworkEvent(actionName)");
        builder.AppendLine("    local inputEventName = vrsInputEventName(actionName)");
        builder.AppendLine("    for _, pathSegments in ipairs({");
        builder.AppendLine("        { \"VRS\", \"Events\", \"User Input (NetworkEvent)\", \"Input Manager\", inputEventName },");
        builder.AppendLine("        { \"VRS\", \"Events\", \"User Input (NetworkEvent)\", inputEventName },");
        builder.AppendLine("        { \"VRS\", \"Events\", \"Input\", inputEventName }");
        builder.AppendLine("    }) do");
        builder.AppendLine("        local current = Hidden");
        builder.AppendLine("        for _, segment in ipairs(pathSegments) do");
        builder.AppendLine("            current = vrsFindInputChild(current, segment)");
        builder.AppendLine("            if current == nil then");
        builder.AppendLine("                break");
        builder.AppendLine("            end");
        builder.AppendLine("        end");
        builder.AppendLine("        if current ~= nil then");
        builder.AppendLine("            return current");
        builder.AppendLine("        end");
        builder.AppendLine("    end");
        builder.AppendLine("    return nil");
        builder.AppendLine("end");
    }

    private static void AppendReadableSendInputEventAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var actionName = ParameterExpression(rule, action, nodesById, "inputAction", "String", "Jump");
        var payload = action.Type.Equals("SendInputTextEvent", StringComparison.OrdinalIgnoreCase)
            ? ParameterExpression(rule, action, nodesById, "payload", "String", "")
            : null;
        builder.AppendLine($"{indent}local inputActionName = tostring({actionName.Code})");
        builder.AppendLine($"{indent}local inputEvent = vrsResolveInputNetworkEvent(inputActionName)");
        builder.AppendLine($"{indent}if inputEvent == nil or inputEvent.InvokeServer == nil then");
        builder.AppendLine($"{indent}    print(\"Send Input Event stopped: NetworkEvent missing for input \" .. inputActionName .. \". Run VRS Input Manager first.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if NetMessage == nil or NetMessage.New == nil then");
        builder.AppendLine($"{indent}    print(\"Send Input Event stopped: NetMessage is not available.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local inputMessage = NetMessage:New()");
        if (payload is not null)
        {
            builder.AppendLine($"{indent}if inputMessage.AddString ~= nil then");
            builder.AppendLine($"{indent}    inputMessage:AddString(tostring({payload.Code}))");
            builder.AppendLine($"{indent}else");
            builder.AppendLine($"{indent}    print(\"Send Input Text Event warning: NetMessage:AddString is not available; sending an empty message.\")");
            builder.AppendLine($"{indent}end");
        }

        builder.AppendLine($"{indent}inputEvent:InvokeServer(inputMessage)");
    }

    private static void AppendReadableVrsInputEventTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var actionName = ParameterExpression(rule, trigger, nodesById, "inputAction", "String", "Jump");
        builder.AppendLine($"local function {functionName}()");
        builder.AppendLine($"    local inputActionName = tostring({actionName.Code})");
        builder.AppendLine("    local inputEvent = vrsResolveInputNetworkEvent(inputActionName)");
        builder.AppendLine("    if inputEvent == nil or inputEvent.InvokedServer == nil or inputEvent.InvokedServer.Connect == nil then");
        builder.AppendLine("        print(\"On VRS Input Event trigger stopped: NetworkEvent missing for input \" .. inputActionName .. \". Run VRS Input Manager first.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    inputEvent.InvokedServer:Connect(function(player, inputMessage)");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 2);
        builder.AppendLine("        local triggerContext = { object = triggerObject, player = player, inputAction = inputActionName, inputMessage = inputMessage, message = inputMessage }");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
        if (!emitted)
        {
            builder.AppendLine("        print(\"On VRS Input Event trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("    end)");
        builder.AppendLine("end");
    }
}
