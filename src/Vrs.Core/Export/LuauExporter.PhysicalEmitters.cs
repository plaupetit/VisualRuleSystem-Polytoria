using System.Text;
using Vrs.Graph.Model;

namespace Vrs.Core.Export;

public sealed partial class LuauExporter
{
    private static readonly HashSet<string> PhysicalEventTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnTouchObject",
        "OnObjectTouchEnded",
        "OnObjectHoverStarted",
        "OnObjectHoverEnded",
        "OnObjectClicked",
        "OnExplosionTouched",
        "OnObjectGrabbed",
        "OnObjectReleased"
    };

    private static readonly HashSet<string> RigidBodyWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnRigidBodyGravityEnabled",
        "OnRigidBodyMassReached",
        "OnRigidBodyFrictionReached",
        "OnRigidBodyDragReached",
        "OnRigidBodyAngularDragReached",
        "OnRigidBodyBouncinessReached"
    };

    private static bool IsRigidBodyWatcherTrigger(string triggerType)
        => RigidBodyWatcherTriggerTypes.Contains(triggerType);

    private static readonly HashSet<string> GrabbableWatcherTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnGrabForceReached",
        "OnGrabMaxRangeReached",
        "OnGrabPickupRangeReached"
    };

    private static bool IsGrabbableWatcherTrigger(string triggerType)
        => GrabbableWatcherTriggerTypes.Contains(triggerType);

    private static bool TryAppendReadablePhysicalActionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (action.Type.Equals("MoveObjectWithPhysics", StringComparison.OrdinalIgnoreCase))
        {
            AppendPhysicalVectorMethodAction(builder, rule, action, plan, nodesById, indentLevel, "MovePosition", "Move Object With Physics");
            return true;
        }

        if (action.Type.Equals("TurnObjectWithPhysics", StringComparison.OrdinalIgnoreCase))
        {
            AppendPhysicalVectorMethodAction(builder, rule, action, plan, nodesById, indentLevel, "MoveRotation", "Turn Object With Physics");
            return true;
        }

        if (action.Type.Equals("SetObjectVelocity", StringComparison.OrdinalIgnoreCase))
        {
            AppendPhysicalVectorPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Velocity", "Set Object Velocity");
            return true;
        }

        if (action.Type.Equals("SetObjectSpinVelocity", StringComparison.OrdinalIgnoreCase))
        {
            AppendPhysicalVectorPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "AngularVelocity", "Set Object Spin Velocity");
            return true;
        }

        if (action.Type.Equals("SetRigidBodyGravity", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "UseGravity", "enabled", "Boolean", "true", "Set Rigid Body Gravity");
            return true;
        }

        if (action.Type.Equals("SetRigidBodyMass", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Mass", "mass", "Number", "1", "Set Rigid Body Mass");
            return true;
        }

        if (action.Type.Equals("SetRigidBodyFriction", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Friction", "friction", "Number", "0.5", "Set Rigid Body Friction");
            return true;
        }

        if (action.Type.Equals("SetRigidBodyDrag", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Drag", "drag", "Number", "0", "Set Rigid Body Drag");
            return true;
        }

        if (action.Type.Equals("SetRigidBodyAngularDrag", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "AngularDrag", "angularDrag", "Number", "0", "Set Rigid Body Angular Drag");
            return true;
        }

        if (action.Type.Equals("SetRigidBodyBounciness", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Bounciness", "bounciness", "Number", "0", "Set Rigid Body Bounciness");
            return true;
        }

        if (action.Type.Equals("SetExplosionRadius", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Radius", "radius", "Number", "12", "Set Explosion Radius");
            return true;
        }

        if (action.Type.Equals("SetExplosionForce", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Force", "force", "Number", "500", "Set Explosion Force");
            return true;
        }

        if (action.Type.Equals("SetExplosionDamage", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Damage", "damage", "Number", "100", "Set Explosion Damage");
            return true;
        }

        if (action.Type.Equals("SetExplosionAffectAnchored", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "AffectAnchored", "enabled", "Boolean", "false", "Set Explosion Affect Anchored");
            return true;
        }

        if (action.Type.Equals("SetGrabForce", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "Force", "force", "Number", "500", "Set Grab Force");
            return true;
        }

        if (action.Type.Equals("SetGrabMaxRange", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "MaxRange", "range", "Number", "30", "Set Grab Drag Range");
            return true;
        }

        if (action.Type.Equals("SetGrabPickupRange", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "MaxGrabbableRange", "range", "Number", "15", "Set Grab Pickup Range");
            return true;
        }

        if (action.Type.Equals("SetGrabUsesDragForce", StringComparison.OrdinalIgnoreCase))
        {
            AppendSimpleObjectPropertyAction(builder, rule, action, plan, nodesById, indentLevel, "UseDragForce", "enabled", "Boolean", "true", "Set Grab Uses Drag Force");
            return true;
        }

        if (action.Type.Equals("SetGrabPermissionMode", StringComparison.OrdinalIgnoreCase))
        {
            AppendGrabPermissionModeAction(builder, rule, action, plan, nodesById, indentLevel);
            return true;
        }

        return false;
    }

    private static bool TryAppendReadablePhysicalConditionBody(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        if (condition.Type.Equals("RigidBodyGravityEnabled", StringComparison.OrdinalIgnoreCase))
        {
            AppendRigidBodyBooleanPropertyCondition(builder, plan, condition, indentLevel, "UseGravity", true);
            return true;
        }

        if (condition.Type.Equals("RigidBodyMassAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            AppendRigidBodyNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "Mass", "mass", "1");
            return true;
        }

        if (condition.Type.Equals("RigidBodyFrictionAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            AppendRigidBodyNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "Friction", "friction", "0.5");
            return true;
        }

        if (condition.Type.Equals("RigidBodyDragAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            AppendRigidBodyNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "Drag", "drag", "0");
            return true;
        }

        if (condition.Type.Equals("RigidBodyAngularDragAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            AppendRigidBodyNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "AngularDrag", "angularDrag", "0");
            return true;
        }

        if (condition.Type.Equals("RigidBodyBouncinessAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            AppendRigidBodyNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "Bounciness", "bounciness", "0");
            return true;
        }

        if (condition.Type.Equals("GrabForceAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            AppendRigidBodyNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "Force", "force", "500");
            return true;
        }

        if (condition.Type.Equals("GrabMaxRangeAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            AppendRigidBodyNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "MaxRange", "range", "30");
            return true;
        }

        if (condition.Type.Equals("GrabPickupRangeAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            AppendRigidBodyNumberPropertyCondition(builder, rule, condition, plan, nodesById, indentLevel, "MaxGrabbableRange", "range", "15");
            return true;
        }

        if (condition.Type.Equals("GrabUsesDragForce", StringComparison.OrdinalIgnoreCase))
        {
            AppendRigidBodyBooleanPropertyCondition(builder, plan, condition, indentLevel, "UseDragForce", true);
            return true;
        }

        if (condition.Type.Equals("GrabPermissionModeIs", StringComparison.OrdinalIgnoreCase))
        {
            AppendGrabPermissionModeCondition(builder, rule, condition, plan, nodesById, indentLevel);
            return true;
        }

        if (!condition.Type.Equals("ObjectIsMoving", StringComparison.OrdinalIgnoreCase) &&
            !condition.Type.Equals("ObjectSpeedAtLeast", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var indent = IndentText(indentLevel);
        var parameterKey = condition.Type.Equals("ObjectIsMoving", StringComparison.OrdinalIgnoreCase)
            ? "minimumSpeed"
            : "speed";
        var fallback = condition.Type.Equals("ObjectIsMoving", StringComparison.OrdinalIgnoreCase) ? "0.1" : "10";
        var speed = ParameterExpression(rule, condition, nodesById, parameterKey, "Number", fallback);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.Velocity == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local currentSpeed = vrsDistanceBetweenPositions(targetObject.Velocity, makeVector3(0, 0, 0))");
        builder.AppendLine($"{indent}return currentSpeed >= {speed.Code}");
        return true;
    }

    private static void AppendReadableRigidBodyWatcherTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var definition = RigidBodyWatcherDefinition.For(trigger.Type);
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");
        var threshold = definition.ParameterKey is null
            ? null
            : ParameterExpression(rule, trigger, nodesById, definition.ParameterKey, "Number", definition.FallbackLimit);

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        if (threshold is not null)
        {
            builder.AppendLine($"    local watchedLimit = tonumber({threshold.Code}) or {definition.FallbackLimit}");
        }

        builder.AppendLine("    local function readWatchedValue()");
        builder.AppendLine("        if triggerObject == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        builder.AppendLine($"        if triggerObject.{definition.PropertyName} == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        if (definition.ValueKind == RigidBodyWatcherValueKind.Boolean)
        {
            builder.AppendLine($"        return triggerObject.{definition.PropertyName} == true");
        }
        else
        {
            builder.AppendLine($"        return tonumber(triggerObject.{definition.PropertyName}) or {definition.FallbackCurrent}");
        }

        builder.AppendLine("    end");
        builder.AppendLine("    local function readMatched()");
        builder.AppendLine("        local currentValue = readWatchedValue()");
        builder.AppendLine("        if currentValue == nil then");
        builder.AppendLine("            return false, nil");
        builder.AppendLine("        end");
        builder.AppendLine($"        return {definition.MatchExpression}, currentValue");
        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, $", {definition.ContextField} = currentValue", 1);
        builder.AppendLine("end");
    }

    private static void AppendReadableGrabbableWatcherTransitionTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var definition = GrabbableWatcherDefinition.For(trigger.Type);
        var interval = ParameterExpression(rule, trigger, nodesById, "interval", "Number", "0.25");
        var threshold = ParameterExpression(rule, trigger, nodesById, definition.ParameterKey, "Number", definition.FallbackLimit);

        builder.AppendLine($"local function {functionName}()");
        AppendReadableTriggerObjectResolution(builder, plan, trigger, 1);
        builder.AppendLine($"    local watchedLimit = tonumber({threshold.Code}) or {definition.FallbackLimit}");
        builder.AppendLine("    local function readWatchedValue()");
        builder.AppendLine("        if triggerObject == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        builder.AppendLine($"        if triggerObject.{definition.PropertyName} == nil then");
        builder.AppendLine("            return nil");
        builder.AppendLine("        end");
        builder.AppendLine($"        return tonumber(triggerObject.{definition.PropertyName}) or 0");
        builder.AppendLine("    end");
        builder.AppendLine("    local function readMatched()");
        builder.AppendLine("        local currentValue = readWatchedValue()");
        builder.AppendLine("        if currentValue == nil then");
        builder.AppendLine("            return false, nil");
        builder.AppendLine("        end");
        builder.AppendLine("        return currentValue >= watchedLimit, currentValue");
        builder.AppendLine("    end");
        AppendReadableTrueTransitionLoop(builder, rule, trigger, plan, nodesById, visited, reachedNodeIds, interval.Code, $", {definition.ContextField} = currentValue", 1);
        builder.AppendLine("end");
    }

    private static LuauExpression? TryResolveReadablePhysicalPropertyExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        return node.Type switch
        {
            "ObjectVelocity" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Velocity", "Vector3", "Object Velocity", "return targetObject.Velocity"),
            "ObjectSpinVelocity" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "AngularVelocity", "Vector3", "Object Spin Velocity", "return targetObject.AngularVelocity"),
            "RigidBodyGravityEnabledValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "UseGravity", "Boolean", "Rigid Body Gravity Enabled", "return targetObject.UseGravity == true"),
            "RigidBodyMassValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Mass", "Number", "Rigid Body Mass", "return tonumber(targetObject.Mass) or 0"),
            "RigidBodyFrictionValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Friction", "Number", "Rigid Body Friction", "return tonumber(targetObject.Friction) or 0"),
            "RigidBodyDragValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Drag", "Number", "Rigid Body Drag", "return tonumber(targetObject.Drag) or 0"),
            "RigidBodyAngularDragValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "AngularDrag", "Number", "Rigid Body Angular Drag", "return tonumber(targetObject.AngularDrag) or 0"),
            "RigidBodyBouncinessValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Bounciness", "Number", "Rigid Body Bounciness", "return tonumber(targetObject.Bounciness) or 0"),
            "ExplosionRadiusValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Radius", "Number", "Explosion Radius", "return tonumber(targetObject.Radius) or 0"),
            "ExplosionForceValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Force", "Number", "Explosion Force", "return tonumber(targetObject.Force) or 0"),
            "ExplosionDamageValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Damage", "Number", "Explosion Damage", "return tonumber(targetObject.Damage) or 0"),
            "ExplosionAffectAnchoredValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "AffectAnchored", "Boolean", "Explosion Affect Anchored", "return targetObject.AffectAnchored == true"),
            "GrabForceValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Force", "Number", "Grab Force", "return tonumber(targetObject.Force) or 0"),
            "GrabMaxRangeValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "MaxRange", "Number", "Grab Drag Range", "return tonumber(targetObject.MaxRange) or 0"),
            "GrabPickupRangeValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "MaxGrabbableRange", "Number", "Grab Pickup Range", "return tonumber(targetObject.MaxGrabbableRange) or 0"),
            "GrabUsesDragForceValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "UseDragForce", "Boolean", "Grab Uses Drag Force", "return targetObject.UseDragForce == true"),
            "GrabPermissionModeValue" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "PermissionMode", "String", "Grab Permission", "return tostring(targetObject.PermissionMode or \"\")"),
            "CurrentGrabber" => ObjectValueExpression(rule, node, nodesById, visitedNodeIds, "Dragger", "Player", "Current Grabber", "return targetObject.Dragger"),
            "ObjectSpeed" => ObjectSpeedExpression(rule, node, nodesById, visitedNodeIds),
            "TouchingObjectCount" => TouchingObjectCountExpression(rule, node, nodesById, visitedNodeIds),
            _ => null
        };
    }

    private static void AppendReadablePhysicalEventTrigger(
        StringBuilder builder,
        Rule rule,
        RuleNode trigger,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visited,
        HashSet<string> reachedNodeIds,
        string functionName)
    {
        var definition = PhysicalEventDefinition.For(trigger.Type);
        builder.AppendLine($"local function {functionName}()");
        builder.AppendLine("    local scriptParent = script.Parent");
        builder.AppendLine($"    local listenObject = {ReadableTriggerTargetExpression(plan, trigger, "scriptParent")}");
        builder.AppendLine("    if listenObject == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target was not found.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine($"    if listenObject.{definition.EventName} == nil or listenObject.{definition.EventName}.Connect == nil then");
        builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: target has no {definition.EventName} event.\")");
        builder.AppendLine("        return");
        builder.AppendLine("    end");
        builder.AppendLine($"    listenObject.{definition.EventName}:Connect(function({definition.ParameterList})");
        builder.AppendLine("        local triggerObject = listenObject");
        builder.AppendLine($"        local triggerContext = {{ object = triggerObject{definition.ContextFields} }}");
        var emitted = AppendReadableFlowFromNode(builder, rule, plan, nodesById, trigger.Id, visited, reachedNodeIds, "triggerObject", "triggerContext", 2, 0);
        if (!emitted)
        {
            builder.AppendLine($"        print(\"{EscapeForDoubleQuotedString(trigger.Label)} trigger stopped: no connected action or condition.\")");
        }

        builder.AppendLine("    end)");
        builder.AppendLine("end");
    }

    private static void AppendPhysicalVectorMethodAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string methodName,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        var value = ParameterVectorExpression(rule, action, nodesById, "vector", "0", "0", "0");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.{methodName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not support {methodName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}targetObject:{methodName}({value.Code})");
    }

    private static void AppendPhysicalVectorPropertyAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string readableName)
    {
        var indent = IndentText(indentLevel);
        var value = ParameterVectorExpression(rule, action, nodesById, "vector", "0", "0", "0");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.{propertyName} == nil then");
        builder.AppendLine($"{indent}    print(\"{readableName} stopped: target does not expose {propertyName}.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}targetObject.{propertyName} = {value.Code}");
    }

    private static void AppendGrabPermissionModeAction(
        StringBuilder builder,
        Rule rule,
        RuleNode action,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var mode = ParameterExpression(rule, action, nodesById, "mode", "String", "Everyone");
        AppendResolvedTargetVariable(builder, plan, action, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    print(\"Set Grab Permission stopped: target was not found.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}if targetObject.PermissionMode == nil then");
        builder.AppendLine($"{indent}    print(\"Set Grab Permission stopped: target does not expose PermissionMode.\")");
        builder.AppendLine($"{indent}    return");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local grabPermissionModeName = tostring({mode.Code} or \"Everyone\")");
        builder.AppendLine($"{indent}if Enums ~= nil and Enums.GrabbablePermissionMode ~= nil and Enums.GrabbablePermissionMode[grabPermissionModeName] ~= nil then");
        builder.AppendLine($"{indent}    targetObject.PermissionMode = Enums.GrabbablePermissionMode[grabPermissionModeName]");
        builder.AppendLine($"{indent}else");
        builder.AppendLine($"{indent}    targetObject.PermissionMode = grabPermissionModeName");
        builder.AppendLine($"{indent}end");
    }

    private static void AppendGrabPermissionModeCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel)
    {
        var indent = IndentText(indentLevel);
        var mode = ParameterExpression(rule, condition, nodesById, "mode", "String", "Everyone");
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return tostring(targetObject.PermissionMode or \"\") == tostring({mode.Code})");
    }

    private static LuauExpression ObjectSpeedExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Self", visitedNodeIds);
        var code = $"(function() local targetObject = resolveTarget(triggerObject, {target.Code}); if targetObject == nil then print(\"Object Speed stopped: target was not found.\"); return 0 end; if targetObject.Velocity == nil then print(\"Object Speed stopped: target does not expose Velocity.\"); return 0 end; return vrsDistanceBetweenPositions(targetObject.Velocity, makeVector3(0, 0, 0)) end)()";
        return new LuauExpression(code, "Number");
    }

    private static LuauExpression TouchingObjectCountExpression(
        Rule rule,
        RuleNode node,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        HashSet<string> visitedNodeIds)
    {
        var target = PropertyParameterExpression(rule, node, nodesById, "target", "String", "Self", visitedNodeIds);
        var code = $"(function() local targetObject = resolveTarget(triggerObject, {target.Code}); if targetObject == nil then print(\"Touching Object Count stopped: target was not found.\"); return 0 end; if targetObject.GetTouching == nil then print(\"Touching Object Count stopped: target does not support GetTouching.\"); return 0 end; local touchingObjects = targetObject:GetTouching(); if touchingObjects == nil then return 0 end; return #touchingObjects end)()";
        return new LuauExpression(code, "Number");
    }

    private static void AppendRigidBodyBooleanPropertyCondition(
        StringBuilder builder,
        ReadableExportPlan plan,
        RuleNode condition,
        int indentLevel,
        string propertyName,
        bool expected)
    {
        var indent = IndentText(indentLevel);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}return (targetObject.{propertyName} == true) == {expected.ToString().ToLowerInvariant()}");
    }

    private static void AppendRigidBodyNumberPropertyCondition(
        StringBuilder builder,
        Rule rule,
        RuleNode condition,
        ReadableExportPlan plan,
        IReadOnlyDictionary<string, RuleNode> nodesById,
        int indentLevel,
        string propertyName,
        string parameterKey,
        string fallback)
    {
        var indent = IndentText(indentLevel);
        var threshold = ParameterExpression(rule, condition, nodesById, parameterKey, "Number", fallback);
        AppendResolvedTargetVariable(builder, plan, condition, indentLevel, "targetObject");
        builder.AppendLine($"{indent}if targetObject == nil then");
        builder.AppendLine($"{indent}    return false");
        builder.AppendLine($"{indent}end");
        builder.AppendLine($"{indent}local currentNumber = tonumber(targetObject.{propertyName}) or 0");
        builder.AppendLine($"{indent}local expectedNumber = tonumber({threshold.Code}) or 0");
        builder.AppendLine($"{indent}return currentNumber >= expectedNumber");
    }

    private enum RigidBodyWatcherValueKind
    {
        Boolean,
        Number
    }

    private sealed record RigidBodyWatcherDefinition(
        string PropertyName,
        RigidBodyWatcherValueKind ValueKind,
        string ContextField,
        string MatchExpression,
        string? ParameterKey = null,
        string FallbackLimit = "0",
        string FallbackCurrent = "0")
    {
        public static RigidBodyWatcherDefinition For(string triggerType)
        {
            return triggerType switch
            {
                "OnRigidBodyGravityEnabled" => new("UseGravity", RigidBodyWatcherValueKind.Boolean, "gravityEnabled", "currentValue == true"),
                "OnRigidBodyMassReached" => NumberThreshold("Mass", "mass", "1", "rigidBodyMass"),
                "OnRigidBodyFrictionReached" => NumberThreshold("Friction", "friction", "0.5", "rigidBodyFriction"),
                "OnRigidBodyDragReached" => NumberThreshold("Drag", "drag", "0", "rigidBodyDrag"),
                "OnRigidBodyAngularDragReached" => NumberThreshold("AngularDrag", "angularDrag", "0", "rigidBodyAngularDrag"),
                "OnRigidBodyBouncinessReached" => NumberThreshold("Bounciness", "bounciness", "0", "rigidBodyBounciness"),
                _ => throw new ArgumentOutOfRangeException(nameof(triggerType), triggerType, "Unknown rigid body watcher trigger.")
            };
        }

        private static RigidBodyWatcherDefinition NumberThreshold(string propertyName, string parameterKey, string fallback, string contextField)
        {
            return new RigidBodyWatcherDefinition(
                propertyName,
                RigidBodyWatcherValueKind.Number,
                contextField,
                "currentValue >= watchedLimit",
                parameterKey,
                fallback);
        }
    }

    private sealed record GrabbableWatcherDefinition(
        string PropertyName,
        string ParameterKey,
        string FallbackLimit,
        string ContextField)
    {
        public static GrabbableWatcherDefinition For(string triggerType)
        {
            return triggerType switch
            {
                "OnGrabForceReached" => new("Force", "force", "500", "grabForce"),
                "OnGrabMaxRangeReached" => new("MaxRange", "range", "30", "grabMaxRange"),
                "OnGrabPickupRangeReached" => new("MaxGrabbableRange", "range", "15", "grabPickupRange"),
                _ => throw new ArgumentOutOfRangeException(nameof(triggerType), triggerType, "Unknown grabbable watcher trigger.")
            };
        }
    }

    private sealed record PhysicalEventDefinition(string EventName, string ParameterList, string ContextFields)
    {
        public static PhysicalEventDefinition For(string triggerType)
        {
            return triggerType switch
            {
                "OnTouchObject" => new("Touched", "hit", ", touchObject = hit, touchObjectSource = triggerObject"),
                "OnObjectTouchEnded" => new("TouchEnded", "hit", ", touchObject = hit, touchObjectSource = triggerObject"),
                "OnObjectHoverStarted" => new("MouseEnter", "", ""),
                "OnObjectHoverEnded" => new("MouseExit", "", ""),
                "OnObjectClicked" => new("Clicked", "player", ", player = player"),
                "OnExplosionTouched" => new("Touched", "hit", ", explosion = triggerObject, touchObject = hit, affectedObject = hit, touchObjectSource = triggerObject"),
                "OnObjectGrabbed" => new("Grabbed", "player", ", player = player, grabber = player, grabbable = triggerObject"),
                "OnObjectReleased" => new("Released", "player", ", player = player, grabber = player, grabbable = triggerObject"),
                _ => throw new ArgumentOutOfRangeException(nameof(triggerType), triggerType, "Unknown physical event trigger.")
            };
        }
    }
}
