using System.Text;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static void AppendReadableEssentialsRuntime(StringBuilder builder, bool includeClockAndPlayer)
    {
        if (includeClockAndPlayer)
        {
            builder.AppendLine();
            builder.AppendLine("local function vrsNow()");
            builder.AppendLine("    if os ~= nil and os.clock ~= nil then");
            builder.AppendLine("        return os.clock()");
            builder.AppendLine("    end");
            builder.AppendLine("    if time ~= nil then");
            builder.AppendLine("        return time()");
            builder.AppendLine("    end");
            builder.AppendLine("    return 0");
            builder.AppendLine("end");
            builder.AppendLine();
            builder.AppendLine("local function vrsPlayerKey(player)");
            builder.AppendLine("    if player == nil then");
            builder.AppendLine("        return \"\"");
            builder.AppendLine("    end");
            builder.AppendLine("    if player.UserID ~= nil then");
            builder.AppendLine("        return tostring(player.UserID)");
            builder.AppendLine("    end");
            builder.AppendLine("    if player.UserId ~= nil then");
            builder.AppendLine("        return tostring(player.UserId)");
            builder.AppendLine("    end");
            builder.AppendLine("    if player.Name ~= nil then");
            builder.AppendLine("        return tostring(player.Name)");
            builder.AppendLine("    end");
            builder.AppendLine("    return tostring(player)");
            builder.AppendLine("end");
        }

        builder.AppendLine();
        builder.AppendLine("local function vrsValueAxis(value, upperName, lowerName, fallback)");
        builder.AppendLine("    if value == nil then");
        builder.AppendLine("        return fallback");
        builder.AppendLine("    end");
        builder.AppendLine("    if value[upperName] ~= nil then");
        builder.AppendLine("        return value[upperName]");
        builder.AppendLine("    end");
        builder.AppendLine("    if value[lowerName] ~= nil then");
        builder.AppendLine("        return value[lowerName]");
        builder.AppendLine("    end");
        builder.AppendLine("    return fallback");
        builder.AppendLine("end");
        builder.AppendLine();
        builder.AppendLine("local function vrsDistanceBetweenPositions(first, second)");
        builder.AppendLine("    local dx = vrsValueAxis(first, \"X\", \"x\", 0) - vrsValueAxis(second, \"X\", \"x\", 0)");
        builder.AppendLine("    local dy = vrsValueAxis(first, \"Y\", \"y\", 0) - vrsValueAxis(second, \"Y\", \"y\", 0)");
        builder.AppendLine("    local dz = vrsValueAxis(first, \"Z\", \"z\", 0) - vrsValueAxis(second, \"Z\", \"z\", 0)");
        builder.AppendLine("    return math.sqrt((dx * dx) + (dy * dy) + (dz * dz))");
        builder.AppendLine("end");
    }

    private static void AppendReadableObbyPlayerStateRuntime(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("local function vrsNow()");
        builder.AppendLine("    if os ~= nil and os.clock ~= nil then");
        builder.AppendLine("        return os.clock()");
        builder.AppendLine("    end");
        builder.AppendLine("    if time ~= nil then");
        builder.AppendLine("        return time()");
        builder.AppendLine("    end");
        builder.AppendLine("    return 0");
        builder.AppendLine("end");
        builder.AppendLine();
        builder.AppendLine("local function vrsPlayerKey(player)");
        builder.AppendLine("    if player == nil then");
        builder.AppendLine("        return \"\"");
        builder.AppendLine("    end");
        builder.AppendLine("    if player.UserID ~= nil then");
        builder.AppendLine("        return tostring(player.UserID)");
        builder.AppendLine("    end");
        builder.AppendLine("    if player.Name ~= nil then");
        builder.AppendLine("        return tostring(player.Name)");
        builder.AppendLine("    end");
        builder.AppendLine("    return tostring(player)");
        builder.AppendLine("end");
        builder.AppendLine();
        builder.AppendLine("local function vrsPlayerData(player)");
        builder.AppendLine("    local key = vrsPlayerKey(player)");
        builder.AppendLine("    if key == \"\" then");
        builder.AppendLine("        return nil");
        builder.AppendLine("    end");
        builder.AppendLine("    if VRS.playerState[key] == nil then");
        builder.AppendLine("        VRS.playerState[key] = { numbers = {}, texts = {}, flags = {}, collectibles = {} }");
        builder.AppendLine("    end");
        builder.AppendLine("    return VRS.playerState[key]");
        builder.AppendLine("end");
    }

    private static void AppendReadableObbyTouchResolver(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("local function vrsResolveTouchingPlayer(hit)");
        builder.AppendLine("    if hit == nil then");
        builder.AppendLine("        return nil");
        builder.AppendLine("    end");
        builder.AppendLine("    if hit.UserID ~= nil or hit.Respawn ~= nil or hit.Jump ~= nil then");
        builder.AppendLine("        return hit");
        builder.AppendLine("    end");
        builder.AppendLine("    if hit.Player ~= nil then");
        builder.AppendLine("        return hit.Player");
        builder.AppendLine("    end");
        builder.AppendLine("    if hit.Owner ~= nil and (hit.Owner.UserID ~= nil or hit.Owner.Respawn ~= nil or hit.Owner.Jump ~= nil) then");
        builder.AppendLine("        return hit.Owner");
        builder.AppendLine("    end");
        builder.AppendLine("    local parent = hit.Parent");
        builder.AppendLine("    if parent ~= nil then");
        builder.AppendLine("        if parent.UserID ~= nil or parent.Respawn ~= nil or parent.Jump ~= nil then");
        builder.AppendLine("            return parent");
        builder.AppendLine("        end");
        builder.AppendLine("        if parent.Player ~= nil then");
        builder.AppendLine("            return parent.Player");
        builder.AppendLine("        end");
        builder.AppendLine("    end");
        builder.AppendLine("    return nil");
        builder.AppendLine("end");
    }

    private static void AppendReadableObbyObjectPositionRuntime(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("local function vrsObjectPosition(object)");
        builder.AppendLine("    if object == nil then");
        builder.AppendLine("        return nil");
        builder.AppendLine("    end");
        builder.AppendLine("    if object.Position ~= nil then");
        builder.AppendLine("        return object.Position");
        builder.AppendLine("    end");
        builder.AppendLine("    if object.Parent ~= nil and object.Parent.Position ~= nil then");
        builder.AppendLine("        return object.Parent.Position");
        builder.AppendLine("    end");
        builder.AppendLine("    return nil");
        builder.AppendLine("end");
    }

    private static void AppendReadableVectorFactory(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("local function makeVector2(x, y)");
        builder.AppendLine("    if Vector2 ~= nil and Vector2.New ~= nil then");
        builder.AppendLine("        return Vector2.New(x, y)");
        builder.AppendLine("    end");
        builder.AppendLine("    if Vector2 ~= nil and Vector2.new ~= nil then");
        builder.AppendLine("        return Vector2.new(x, y)");
        builder.AppendLine("    end");
        builder.AppendLine("    return { X = x, Y = y }");
        builder.AppendLine("end");
        builder.AppendLine();
        builder.AppendLine("local function makeVector3(x, y, z)");
        builder.AppendLine("    if Vector3 ~= nil and Vector3.New ~= nil then");
        builder.AppendLine("        return Vector3.New(x, y, z)");
        builder.AppendLine("    end");
        builder.AppendLine("    if Vector3 ~= nil and Vector3.new ~= nil then");
        builder.AppendLine("        return Vector3.new(x, y, z)");
        builder.AppendLine("    end");
        builder.AppendLine("    return { X = x, Y = y, Z = z }");
        builder.AppendLine("end");
    }

    private static void AppendReadableVectorTweenRuntime(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("local function vrsVectorAxis(value, upperName, lowerName, fallback)");
        builder.AppendLine("    if value == nil then");
        builder.AppendLine("        return fallback");
        builder.AppendLine("    end");
        builder.AppendLine("    if value[upperName] ~= nil then");
        builder.AppendLine("        return value[upperName]");
        builder.AppendLine("    end");
        builder.AppendLine("    if value[lowerName] ~= nil then");
        builder.AppendLine("        return value[lowerName]");
        builder.AppendLine("    end");
        builder.AppendLine("    return fallback");
        builder.AppendLine("end");
        builder.AppendLine();
        builder.AppendLine("local function vrsLerpNumber(startValue, endValue, alpha)");
        builder.AppendLine("    return startValue + ((endValue - startValue) * alpha)");
        builder.AppendLine("end");
        builder.AppendLine();
        builder.AppendLine("local function vrsLerpVector3(startValue, endValue, alpha)");
        builder.AppendLine("    return makeVector3(");
        builder.AppendLine("        vrsLerpNumber(vrsVectorAxis(startValue, \"X\", \"x\", 0), vrsVectorAxis(endValue, \"X\", \"x\", 0), alpha),");
        builder.AppendLine("        vrsLerpNumber(vrsVectorAxis(startValue, \"Y\", \"y\", 0), vrsVectorAxis(endValue, \"Y\", \"y\", 0), alpha),");
        builder.AppendLine("        vrsLerpNumber(vrsVectorAxis(startValue, \"Z\", \"z\", 0), vrsVectorAxis(endValue, \"Z\", \"z\", 0), alpha)");
        builder.AppendLine("    )");
        builder.AppendLine("end");
        builder.AppendLine();
        builder.AppendLine("local function vrsEaseIn(alpha, smoothing)");
        builder.AppendLine("    if smoothing == \"Sine\" then");
        builder.AppendLine("        return 1 - math.cos((alpha * 3.141592653589793) / 2)");
        builder.AppendLine("    elseif smoothing == \"Quad\" then");
        builder.AppendLine("        return alpha * alpha");
        builder.AppendLine("    elseif smoothing == \"Cubic\" then");
        builder.AppendLine("        return alpha * alpha * alpha");
        builder.AppendLine("    end");
        builder.AppendLine("    return alpha");
        builder.AppendLine("end");
        builder.AppendLine();
        builder.AppendLine("local function vrsEase(alpha, smoothing, direction)");
        builder.AppendLine("    if direction == \"In\" then");
        builder.AppendLine("        return vrsEaseIn(alpha, smoothing)");
        builder.AppendLine("    elseif direction == \"Out\" then");
        builder.AppendLine("        return 1 - vrsEaseIn(1 - alpha, smoothing)");
        builder.AppendLine("    end");
        builder.AppendLine("    if alpha < 0.5 then");
        builder.AppendLine("        return vrsEaseIn(alpha * 2, smoothing) / 2");
        builder.AppendLine("    end");
        builder.AppendLine("    return 1 - (vrsEaseIn((1 - alpha) * 2, smoothing) / 2)");
        builder.AppendLine("end");
        builder.AppendLine();
        builder.AppendLine("local function vrsRunVectorTween(readValue, writeValue, endValue, duration, smoothing, direction)");
        builder.AppendLine("    if duration == nil or duration <= 0 then");
        builder.AppendLine("        writeValue(endValue)");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine("    local startValue = readValue()");
        builder.AppendLine("    local elapsed = 0");
        builder.AppendLine("    local stepSeconds = 0.03");
        builder.AppendLine("    while elapsed < duration do");
        builder.AppendLine("        wait(stepSeconds)");
        builder.AppendLine("        elapsed = elapsed + stepSeconds");
        builder.AppendLine("        local alpha = math.min(elapsed / duration, 1)");
        builder.AppendLine("        writeValue(vrsLerpVector3(startValue, endValue, vrsEase(alpha, smoothing, direction)))");
        builder.AppendLine("    end");
        builder.AppendLine("    writeValue(endValue)");
        builder.AppendLine("end");
    }

    private static void AppendReadableTargetResolver(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine("local function resolveTarget(triggerObject, targetName)");
        builder.AppendLine("    if targetName == nil or targetName == \"\" or targetName == \"Self\" or targetName == \"Target\" then");
        builder.AppendLine("        return triggerObject");
        builder.AppendLine("    end");
        builder.AppendLine();
        builder.AppendLine("    local current = nil");
        builder.AppendLine("    for segment in string.gmatch(targetName, \"[^/]+\") do");
        builder.AppendLine("        if current == nil then");
        builder.AppendLine("            if segment == \"World\" and World ~= nil then");
        builder.AppendLine("                current = World");
        builder.AppendLine("            elseif segment == \"Environment\" and Environment ~= nil then");
        builder.AppendLine("                current = Environment");
        builder.AppendLine("            elseif segment == \"Hidden\" and Hidden ~= nil then");
        builder.AppendLine("                current = Hidden");
        builder.AppendLine("            elseif segment == \"ScriptService\" and ScriptService ~= nil then");
        builder.AppendLine("                current = ScriptService");
        builder.AppendLine("            elseif triggerObject ~= nil and triggerObject.FindChild ~= nil then");
        builder.AppendLine("                current = triggerObject:FindChild(segment)");
        builder.AppendLine("            elseif World ~= nil and World.FindChild ~= nil then");
        builder.AppendLine("                current = World:FindChild(segment)");
        builder.AppendLine("            end");
        builder.AppendLine("        elseif current.FindChild ~= nil then");
        builder.AppendLine("            current = current:FindChild(segment)");
        builder.AppendLine("        else");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        builder.AppendLine();
        builder.AppendLine("        if current == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        builder.AppendLine("    end");
        builder.AppendLine();
        builder.AppendLine("    return current");
        builder.AppendLine("end");
    }
}
