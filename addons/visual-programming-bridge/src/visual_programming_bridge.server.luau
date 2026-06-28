--!nocheck
--[[
Polytoria 2.0 Creator Addon: [VRS]Visual Programming Bridge

This addon is the Creator-side authority for a local visual programming bridge.
It does not inject, hook, or silently modify Creator. It exchanges readable
project-relative JSON files with the standalone VisualRuleSystem app:

- scene-snapshot.json: full hierarchy snapshot exported from Creator
- scene-events/: small sequenced hierarchy event chunks for focused smart sync
- scene-delta.json: legacy small versioned hierarchy changes for focused smart sync
- settings.json: auto bridge settings controlled by the visual app
- pending-commands.json: commands queued by the visual app
- command-results.json: visible result of the last manual apply
- status.json: quick diagnostics for permission and parsing failures
- app-state.json: VisualRuleSystem workspace summary for status diagnostics
- command-history.jsonl: one JSON result line per handled command id

By default, Creator only applies commands when the user presses
"Apply Visual Commands". Auto mode can be enabled intentionally through
settings.json or the visible addon toggle.
]]

local ADDON_ID = "peterlewis.visual-programming-bridge"
local ADDON_NAME = "[VRS]Visual Programming Bridge"
local SNAPSHOT_TOOL_LABEL = "Export Visual Snapshot"
local APPLY_TOOL_LABEL = "Apply Visual Commands"
local STATUS_TOOL_LABEL = "Visual Bridge Status"
local AUTO_TOOL_LABEL = "Toggle Visual Auto Bridge"

local BRIDGE_DIR = "addons/visual-programming-bridge/bridge"
local SNAPSHOT_PATH = BRIDGE_DIR .. "/scene-snapshot.json"
local DELTA_PATH = BRIDGE_DIR .. "/scene-delta.json"
local SCENE_EVENTS_DIR = BRIDGE_DIR .. "/scene-events"
local SCENE_EVENTS_MANIFEST_PATH = SCENE_EVENTS_DIR .. "/manifest.json"
local SETTINGS_PATH = BRIDGE_DIR .. "/settings.json"
local APP_HEARTBEAT_PATH = BRIDGE_DIR .. "/app-heartbeat.json"
local APP_STATE_PATH = BRIDGE_DIR .. "/app-state.json"
local SNAPSHOT_REQUEST_PATH = BRIDGE_DIR .. "/snapshot-request.json"
local PENDING_COMMANDS_PATH = BRIDGE_DIR .. "/pending-commands.json"
local COMMAND_RESULTS_PATH = BRIDGE_DIR .. "/command-results.json"
local STATUS_PATH = BRIDGE_DIR .. "/status.json"
local COMMAND_HISTORY_PATH = BRIDGE_DIR .. "/command-history.jsonl"
local SCRIPT_LINKS_PATH = BRIDGE_DIR .. "/script-links.json"

local FORMAT_SNAPSHOT = "visual-programming-bridge-scene-snapshot"
local FORMAT_DELTA = "visual-programming-bridge-scene-delta"
local FORMAT_EVENT_MANIFEST = "visual-programming-bridge-scene-event-manifest"
local FORMAT_EVENT_CHUNK = "visual-programming-bridge-scene-event-chunk"
local FORMAT_COMMANDS = "visual-programming-bridge-commands"
local FORMAT_RESULTS = "visual-programming-bridge-command-results"
local FORMAT_STATUS = "visual-programming-bridge-status"
local FORMAT_SETTINGS = "visual-programming-bridge-settings"
local FORMAT_HEARTBEAT = "visual-programming-bridge-app-heartbeat"
local FORMAT_SNAPSHOT_REQUEST = "visual-programming-bridge-snapshot-request"
local FORMAT_SCRIPT_LINKS = "visual-programming-bridge-script-links"
local BRIDGE_VERSION = 1
local BRIDGE_RUNTIME_VERSION = "0.8.15"
local MAX_TREE_DEPTH = 14
local MAX_CHILDREN_PER_NODE = 300
local MAX_WATCHED_INSTANCES = 2500
local DIRTY_DELTA_DEBOUNCE_SECONDS = 0.5
local TRUNCATED_PATH_SAMPLE_LIMIT = 20
local MAX_EVENTS_PER_CHUNK = 50
local MAX_EVENT_CHUNK_BYTES = 32768

local EXPLORER_VISIBLE_ROOT_NAMES = {
	Environment = true,
	Lighting = true,
	Players = true,
	ScriptService = true,
	Hidden = true,
	ServerHidden = true,
	PlayerDefaults = true,
	PlayerGUI = true,
	Achievements = true,
	CoreUI = true,
	Stats = true,
	Teams = true,
	Capture = true,
}

local connections = {}
local sceneWatchConnections = {}
local watchedInstances = {}
local autoBridgeLoopRunning = true
local lastSnapshotRequestId = nil
local sceneWatchActive = false
local sceneWatchCount = 0
local sceneDirty = false
local sceneDirtySince = nil
local lastSceneDirtyAt = nil
local lastSnapshotWrittenAt = nil
local lastDeltaWrittenAt = nil
local sceneSnapshotVersion = 0
local sceneDirtyReasons = {}
local sceneDirtyInstances = {}
local sceneRemovedRecords = {}
local sceneEventStreamId = ""
local sceneEventLatestSequence = 0
local sceneEventLatestChunk = 0

local function rememberConnection(connection)
	if connection ~= nil then
		table.insert(connections, connection)
	end
end

local function cleanupConnections()
	autoBridgeLoopRunning = false

	for _, connection in ipairs(sceneWatchConnections) do
		if connection ~= nil then
			connection:Disconnect()
		end
	end

	sceneWatchConnections = {}
	watchedInstances = {}
	sceneWatchActive = false

	for _, connection in ipairs(connections) do
		if connection ~= nil then
			connection:Disconnect()
		end
	end

	connections = {}
end

local function safeNowText()
	local ok, value = pcall(function()
		return os.date("!%Y-%m-%dT%H:%M:%SZ")
	end)

	if ok then
		return value
	end

	return "unknown"
end

local function safeUnixSeconds()
	local ok, value = pcall(function()
		return os.time()
	end)

	if ok and type(value) == "number" then
		return value
	end

	return nil
end

local function safeClockSeconds()
	local ok, value = pcall(function()
		return os.clock()
	end)

	if ok and type(value) == "number" then
		return value
	end

	return safeUnixSeconds() or 0
end

local function jsonEncode(payload, fallback)
	if json ~= nil then
		local ok, text = pcall(function()
			return json.serialize(payload)
		end)

		if ok then
			return text
		end
	end

	return fallback
end

local function jsonDecode(text)
	if json == nil then
		return nil, "json API is not available"
	end

	if text ~= nil and string.byte(text, 1) == 239 and string.byte(text, 2) == 187 and string.byte(text, 3) == 191 then
		text = string.sub(text, 4)
	end

	local ok, value = pcall(function()
		return json.parse(text)
	end)

	if ok then
		return value, nil
	end

	return nil, tostring(value)
end

local function readText(path)
	if IO == nil then
		return nil, "IO API is not available"
	end

	local ok, value = pcall(function()
		return IO:ReadTextFromPath(path)
	end)

	if ok then
		return value, nil
	end

	return nil, tostring(value)
end

local function writeText(path, text)
	if IO == nil then
		return false, "IO API is not available"
	end

	local ok, err = pcall(function()
		IO:WriteTextToPath(path, text)
	end)

	if ok then
		return true, nil
	end

	return false, tostring(err)
end

local function eventChunkPath(chunkIndex)
	return SCENE_EVENTS_DIR .. "/chunk-" .. string.format("%06d", tonumber(chunkIndex) or 0) .. ".json"
end

local function makeSceneEventStreamId(baseSnapshotVersion)
	return "stream-" .. tostring(safeUnixSeconds()) .. "-" .. tostring(baseSnapshotVersion or 0)
end

local function writeSceneEventManifest()
	local payload = {
		format = FORMAT_EVENT_MANIFEST,
		version = BRIDGE_VERSION,
		runtimeVersion = BRIDGE_RUNTIME_VERSION,
		streamId = sceneEventStreamId,
		baseSnapshotVersion = sceneSnapshotVersion,
		latestSequence = sceneEventLatestSequence,
		latestChunk = sceneEventLatestChunk,
		chunkPattern = "chunk-%06d.json",
		chunkEventLimit = MAX_EVENTS_PER_CHUNK,
		chunkByteLimit = MAX_EVENT_CHUNK_BYTES,
		updatedAtUtc = safeNowText(),
		updatedAtUnixSeconds = safeUnixSeconds(),
	}
	local text = jsonEncode(payload, "{\"format\":\"" .. FORMAT_EVENT_MANIFEST .. "\",\"version\":1}")
	return writeText(SCENE_EVENTS_MANIFEST_PATH, text)
end

local function resetSceneEventStream(baseSnapshotVersion)
	sceneEventStreamId = makeSceneEventStreamId(baseSnapshotVersion)
	sceneEventLatestSequence = 0
	sceneEventLatestChunk = 0
	local ok, err = writeSceneEventManifest()
	if not ok then
		writeStatus("event_manifest_error", tostring(err), { path = SCENE_EVENTS_MANIFEST_PATH })
	end
	return ok, err
end

local function writeStatus(state, message, extra)
	local payload = {
		format = FORMAT_STATUS,
		version = BRIDGE_VERSION,
		runtimeVersion = BRIDGE_RUNTIME_VERSION,
		state = state,
		message = message,
		updatedAtUtc = safeNowText(),
	}

	if extra ~= nil then
		payload.extra = extra
	end

	writeText(STATUS_PATH, jsonEncode(payload, "{\"format\":\"" .. FORMAT_STATUS .. "\",\"state\":\"" .. tostring(state) .. "\"}"))
end

local function clampNumber(value, minValue, maxValue)
	local number = tonumber(value)
	if number == nil then
		return minValue
	end
	if number < minValue then
		return minValue
	end
	if number > maxValue then
		return maxValue
	end
	return number
end

local function defaultBridgeSettings()
	return {
		format = FORMAT_SETTINGS,
		version = BRIDGE_VERSION,
		autoMode = true,
		intervalSeconds = 10,
		applyIntervalSeconds = 1,
		exportIntervalSeconds = 10,
		syncMode = "delta-preferred",
		snapshotPolicy = "focus-gated",
		snapshotMode = "request",
		autoExport = true,
		autoApply = true,
	}
end

local function readBridgeSettings()
	local fallback = defaultBridgeSettings()
	local text = readText(SETTINGS_PATH)
	if text == nil or text == "" then
		return fallback
	end

	local payload = jsonDecode(text)
	if payload == nil then
		return fallback
	end

	return {
		format = FORMAT_SETTINGS,
		version = BRIDGE_VERSION,
		autoMode = payload.autoMode == true,
		intervalSeconds = clampNumber(payload.intervalSeconds, 5, 30),
		applyIntervalSeconds = clampNumber(payload.applyIntervalSeconds, 1, 10),
		exportIntervalSeconds = clampNumber(payload.exportIntervalSeconds or payload.intervalSeconds, 5, 30),
		syncMode = tostring(payload.syncMode or "delta-preferred"),
		snapshotPolicy = tostring(payload.snapshotPolicy or "focus-gated"),
		snapshotMode = tostring(payload.snapshotMode or "request"),
		autoExport = payload.autoExport ~= false,
		autoApply = payload.autoApply ~= false,
	}
end

local function writeBridgeSettings(settings)
	local payload = settings or defaultBridgeSettings()
	payload.format = FORMAT_SETTINGS
	payload.version = BRIDGE_VERSION
	writeText(SETTINGS_PATH, jsonEncode(payload, "{\"format\":\"" .. FORMAT_SETTINGS .. "\",\"autoMode\":false,\"intervalSeconds\":5}"))
end

local function ensureBridgeSettingsFile()
	local text = readText(SETTINGS_PATH)
	if text == nil or text == "" then
		writeBridgeSettings(defaultBridgeSettings())
	end
end

local writeSnapshot

local function readAppHeartbeat()
	local text = readText(APP_HEARTBEAT_PATH)
	if text == nil or text == "" then
		return false, "app heartbeat missing", nil
	end

	local payload = jsonDecode(text)
	if payload == nil then
		return false, "app heartbeat parse failed", nil
	end

	if payload.active ~= true then
		return false, "app heartbeat inactive", payload
	end

	local expiresAt = tonumber(payload.expiresAtUnixSeconds)
	local now = safeUnixSeconds()
	if expiresAt ~= nil and now ~= nil and expiresAt < now then
		return false, "app heartbeat expired", payload
	end

	return true, "app heartbeat active", payload
end

local function appendSummaryField(parts, label, value)
	if value == nil then
		return
	end

	local text = tostring(value)
	if text ~= "" then
		table.insert(parts, label .. "=" .. text)
	end
end

local function readAppStateSummary()
	local text = readText(APP_STATE_PATH)
	if text == nil or text == "" then
		return nil
	end

	local payload = jsonDecode(text)
	if payload == nil or type(payload) ~= "table" then
		return "app-state parse failed"
	end

	local parts = {}
	local scriptText = tostring(payload.scriptKind or "")
	local creatorScriptName = tostring(payload.creatorScriptName or "")
	if creatorScriptName ~= "" then
		scriptText = scriptText == "" and creatorScriptName or (scriptText .. " " .. creatorScriptName)
	end

	appendSummaryField(parts, "project", payload.activeProjectName)
	appendSummaryField(parts, "mode", payload.projectUiMode)
	appendSummaryField(parts, "script", scriptText)
	appendSummaryField(parts, "file", payload.projectRelativeScriptPath)
	appendSummaryField(parts, "selected", payload.selectedCreatorObjectPath)
	appendSummaryField(parts, "deployParent", payload.deployParentPath)
	appendSummaryField(parts, "nodes", payload.nodeCount)
	appendSummaryField(parts, "validation", tostring(payload.validationErrorCount or 0) .. " error(s), " .. tostring(payload.validationWarningCount or 0) .. " warning(s)")
	appendSummaryField(parts, "beat", payload.bridgeBeatText)

	if #parts == 0 then
		return "app-state present"
	end

	return table.concat(parts, "; ")
end

local function handleSnapshotRequest(quiet)
	local text = readText(SNAPSHOT_REQUEST_PATH)
	if text == nil or text == "" then
		return false
	end

	local payload = jsonDecode(text)
	if payload == nil then
		if not quiet then
			print("[Visual Bridge] Snapshot request JSON parse failed.")
		end
		writeStatus("snapshot_request_parse_error", "Snapshot request JSON parse failed.", { path = SNAPSHOT_REQUEST_PATH })
		return false
	end

	local requestId = tostring(payload.requestId or "")
	if requestId == "" or requestId == lastSnapshotRequestId then
		return false
	end

	lastSnapshotRequestId = requestId
	local ok, message, path = writeSnapshot(true, {
		requestId = requestId,
		reason = tostring(payload.reason or ""),
		sessionId = tostring(payload.sessionId or ""),
	})
	writeStatus(ok and "snapshot_request_handled" or "snapshot_request_failed", tostring(message), {
		requestId = requestId,
		path = path or SNAPSHOT_PATH,
	})
	return ok
end

local function safeRead(instance, propertyName)
	if instance == nil then
		return nil
	end

	local ok, value = pcall(function()
		return instance[propertyName]
	end)

	if ok then
		return value
	end

	return nil
end

local function safeWrite(instance, propertyName, value)
	if instance == nil then
		return false, "missing instance"
	end

	local ok, err = pcall(function()
		instance[propertyName] = value
	end)

	if ok then
		return true, nil
	end

	return false, tostring(err)
end

local function safeCall(callback)
	local ok, value = pcall(callback)
	if ok then
		return value
	end

	return nil
end

local function readChildren(instance)
	local children = safeCall(function()
		return instance:GetChildren()
	end)

	if children ~= nil then
		return children
	end

	return {}
end

local function isA(instance, className)
	local ok, value = pcall(function()
		return instance:IsA(className)
	end)

	return ok and value == true
end

local function shouldExposeInExplorerSnapshot(instance)
	-- World:GetChildren() can expose API/service roots that are not shown in
	-- Creator's Explorer. The visual app mirrors the Explorer by default so the
	-- user does not have to reason about hidden runtime/API plumbing.
	if instance == nil then
		return false
	end

	local root = world or World
	if root == nil then
		return true
	end

	if safeRead(instance, "Parent") ~= root then
		return true
	end

	local name = tostring(safeRead(instance, "Name") or "")
	return EXPLORER_VISIBLE_ROOT_NAMES[name] == true
end

local function isRootOrVisibleServiceRoot(instance)
	if instance == nil then
		return false
	end

	local root = world or World
	if instance == root then
		return true
	end

	if root ~= nil and safeRead(instance, "Parent") == root then
		local name = tostring(safeRead(instance, "Name") or "")
		return EXPLORER_VISIBLE_ROOT_NAMES[name] == true
	end

	return false
end

local function scriptClassNameLooksScript(className)
	className = tostring(className or "")
	return className == "Script"
		or className == "ServerScript"
		or className == "ClientScript"
		or className == "ModuleScript"
end

local function vectorToTable(value)
	if value == nil then
		return nil
	end

	return {
		x = value.X or 0,
		y = value.Y or 0,
		z = value.Z or 0,
	}
end

local function buildPath(instance)
	local names = {}
	local current = instance

	for _ = 1, 96 do
		if current == nil then
			break
		end

		table.insert(names, 1, tostring(safeRead(current, "Name") or "Unnamed"))
		current = safeRead(current, "Parent")
	end

	return table.concat(names, "/")
end

local function readScriptLinkRegistry()
	local text = readText(SCRIPT_LINKS_PATH)
	if text == nil or text == "" then
		return {
			format = FORMAT_SCRIPT_LINKS,
			version = BRIDGE_VERSION,
			runtimeVersion = BRIDGE_RUNTIME_VERSION,
			links = {},
		}
	end

	local payload = jsonDecode(text)
	if type(payload) ~= "table" then
		return {
			format = FORMAT_SCRIPT_LINKS,
			version = BRIDGE_VERSION,
			runtimeVersion = BRIDGE_RUNTIME_VERSION,
			links = {},
		}
	end

	if type(payload.links) ~= "table" then
		payload.links = {}
	end

	return payload
end

local function writeScriptLinkRegistry(registry)
	if type(registry) ~= "table" then
		registry = {}
	end

	if type(registry.links) ~= "table" then
		registry.links = {}
	end

	registry.format = FORMAT_SCRIPT_LINKS
	registry.version = BRIDGE_VERSION
	registry.runtimeVersion = BRIDGE_RUNTIME_VERSION
	registry.updatedAtUtc = safeNowText()

	return writeText(SCRIPT_LINKS_PATH, jsonEncode(registry, "{\"format\":\"" .. FORMAT_SCRIPT_LINKS .. "\",\"version\":1,\"links\":{}}"))
end

local function rememberScriptLink(script, linkedFilePath)
	local objectPath = buildPath(script)
	if objectPath == "" or linkedFilePath == nil or tostring(linkedFilePath) == "" then
		return false, "missing script link information"
	end

	local registry = readScriptLinkRegistry()
	registry.links[objectPath] = {
		objectPath = objectPath,
		linkedFilePath = tostring(linkedFilePath),
		name = tostring(safeRead(script, "Name") or ""),
		className = tostring(safeRead(script, "ClassName") or ""),
		updatedAtUtc = safeNowText(),
	}

	-- Creator can expose LinkedScript as an opaque FileLinkAsset or hide it from
	-- addon reads. This registry records successful bridge-created links so the
	-- next snapshot can still report an actionable path to VisualRuleSystem.
	return writeScriptLinkRegistry(registry)
end

local function bridgeKnownLinkedPath(objectPath)
	local registry = readScriptLinkRegistry()
	local entry = registry.links[tostring(objectPath or "")]
	if type(entry) == "table" and entry.linkedFilePath ~= nil and tostring(entry.linkedFilePath) ~= "" then
		return tostring(entry.linkedFilePath)
	end

	return ""
end

local function rememberSceneWatchConnection(connection)
	if connection ~= nil then
		table.insert(sceneWatchConnections, connection)
	end
end

local function clearSceneDirty()
	sceneDirty = false
	sceneDirtySince = nil
	lastSceneDirtyAt = nil
	sceneDirtyReasons = {}
	sceneDirtyInstances = {}
	sceneRemovedRecords = {}
end

local function rememberRemovedSceneRecord(instance)
	if instance == nil then
		return
	end

	local record = {
		id = tostring(safeRead(instance, "ObjectID") or safeRead(instance, "NetworkedObjectID") or ""),
		path = buildPath(instance),
		name = tostring(safeRead(instance, "Name") or ""),
		className = tostring(safeRead(instance, "ClassName") or ""),
	}
	table.insert(sceneRemovedRecords, record)
end

local function markSceneDirty(reason, instance, mode)
	mode = tostring(mode or "tree")
	if mode == "object" and isRootOrVisibleServiceRoot(instance) then
		return
	end
	if mode ~= "removed" and isRootOrVisibleServiceRoot(instance) then
		return
	end

	local now = safeClockSeconds()
	sceneDirty = true
	lastSceneDirtyAt = now
	if sceneDirtySince == nil then
		sceneDirtySince = now
	end

	if instance ~= nil then
		if mode == "removed" then
			rememberRemovedSceneRecord(instance)
		else
			local currentMode = sceneDirtyInstances[instance]
			if currentMode ~= "tree" then
				sceneDirtyInstances[instance] = mode
			end
		end
	end

	-- Keep only a tiny reason sample; the actual source of truth is the next delta or snapshot.
	if #sceneDirtyReasons < 6 then
		local path = instance ~= nil and buildPath(instance) or "unknown"
		table.insert(sceneDirtyReasons, tostring(reason) .. ": " .. path)
	end
end

local function disconnectSceneWatchers()
	for _, connection in ipairs(sceneWatchConnections) do
		if connection ~= nil then
			connection:Disconnect()
		end
	end

	sceneWatchConnections = {}
	watchedInstances = {}
	sceneWatchActive = false
	sceneWatchCount = 0
	clearSceneDirty()
end

local function connectOptionalSignal(instance, signalName, callback)
	local ok, signal = pcall(function()
		return instance[signalName]
	end)

	if not ok or signal == nil then
		return false
	end

	local connected, connection = pcall(function()
		return signal:Connect(callback)
	end)

	if connected and connection ~= nil then
		rememberSceneWatchConnection(connection)
		return true
	end

	return false
end

local attachSceneWatcherTree

local function attachSceneWatcher(instance)
	if instance == nil or watchedInstances[instance] == true then
		return
	end

	if sceneWatchCount >= MAX_WATCHED_INSTANCES then
		return
	end

	watchedInstances[instance] = true
	sceneWatchCount = sceneWatchCount + 1

	connectOptionalSignal(instance, "ChildAdded", function(child)
		markSceneDirty("child added", child or instance, "tree")
		attachSceneWatcherTree(child)
	end)

	connectOptionalSignal(instance, "ChildRemoved", function(child)
		markSceneDirty("child removed", child or instance, "removed")
	end)

	connectOptionalSignal(instance, "ChildDeleting", function(child)
		markSceneDirty("child deleting", child or instance, "removed")
	end)

	connectOptionalSignal(instance, "ChildDeleted", function(child)
		markSceneDirty("child deleted", child or instance, "removed")
	end)

	-- These NetworkedObject-style signals are optional in Creator. Guard them so
	-- older builds keep working and fall back to command/request snapshots.
	connectOptionalSignal(instance, "Renamed", function()
		markSceneDirty("renamed", instance, "tree")
	end)

	connectOptionalSignal(instance, "PropertyChanged", function()
		markSceneDirty("property changed", instance, "object")
	end)

	connectOptionalSignal(instance, "TreeEntered", function()
		markSceneDirty("tree entered", instance, "tree")
	end)

	connectOptionalSignal(instance, "TreeExited", function()
		markSceneDirty("tree exited", instance, "removed")
	end)
end

function attachSceneWatcherTree(instance)
	if instance == nil or not shouldExposeInExplorerSnapshot(instance) then
		return
	end

	attachSceneWatcher(instance)
	if sceneWatchCount >= MAX_WATCHED_INSTANCES then
		return
	end

	for _, child in ipairs(readChildren(instance)) do
		if shouldExposeInExplorerSnapshot(child) then
			attachSceneWatcherTree(child)
		end
		if sceneWatchCount >= MAX_WATCHED_INSTANCES then
			break
		end
	end
end

local function ensureSceneWatchers(quiet)
	if sceneWatchActive == true then
		return true
	end

	local root = world or World
	if root == nil then
		if not quiet then
			print("[Visual Bridge] Scene watcher unavailable: world root is not available.")
		end
		writeStatus("scene_watch_unavailable", "Scene watcher unavailable: world root is not available.", nil)
		return false
	end

	disconnectSceneWatchers()
	sceneWatchActive = true
	attachSceneWatcherTree(root)

	local message = "Scene watcher active on " .. tostring(sceneWatchCount) .. " object(s)."
	if sceneWatchCount >= MAX_WATCHED_INSTANCES then
		message = message .. " Watch cap reached; manual/request snapshots remain available."
	end

	if not quiet then
		print("[Visual Bridge] " .. message)
	end
	writeStatus("scene_watch_active", message, {
		watchedCount = sceneWatchCount,
		maxWatchedInstances = MAX_WATCHED_INSTANCES,
	})
	return true
end

local function collectInstanceRecord(instance, depth)
	local children = readChildren(instance)
	local name = safeRead(instance, "Name") or "Unnamed"
	local className = safeRead(instance, "ClassName") or "Unknown"
	local record = {
		name = name,
		className = className,
		path = buildPath(instance),
		depth = depth,
		childCount = #children,
		objectId = safeRead(instance, "ObjectID"),
		networkedObjectId = safeRead(instance, "NetworkedObjectID"),
		isDynamic = isA(instance, "Dynamic"),
		isFolder = isA(instance, "Folder"),
		isModel = isA(instance, "Model"),
		isPart = isA(instance, "Part"),
		children = {},
	}

	if scriptClassNameLooksScript(className) then
		local linkedScript = safeRead(instance, "LinkedScript")
		local linkedScriptPath = linkedScript == nil and "" or tostring(linkedScript)
		local bridgeLinkedPath = bridgeKnownLinkedPath(record.path)
		if linkedScriptPath == "" and bridgeLinkedPath ~= "" then
			linkedScriptPath = bridgeLinkedPath
			record.linkEvidence = "bridgeRegistry"
		elseif linkedScriptPath == "FileLinkAsset" and bridgeLinkedPath ~= "" then
			linkedScriptPath = bridgeLinkedPath
			record.linkEvidence = "bridgeRegistryWithOpaqueCreatorLink"
		elseif linkedScriptPath ~= "" then
			record.linkEvidence = "creatorLinkedScript"
		end

		record.linkedScriptPath = linkedScriptPath
		record.isLinkedScript = linkedScriptPath ~= ""
		record.isVisualScriptName = string.sub(tostring(name), 1, 4) == "VRS "
	end

	if record.isDynamic then
		record.transform = {
			position = vectorToTable(safeRead(instance, "Position")),
			rotation = vectorToTable(safeRead(instance, "Rotation")),
			size = vectorToTable(safeRead(instance, "Size")),
		}
	end

	return record, children
end

local function collectSingleInstanceRecord(instance, depth)
	local record = collectInstanceRecord(instance, depth)
	if record == nil then
		return nil
	end

	record.children = {}
	record.childCount = 0
	return record
end

local function rememberTruncatedPath(diagnostics, path, reason)
	diagnostics.truncatedCount = (tonumber(diagnostics.truncatedCount) or 0) + 1

	-- Keep diagnostics compact; VRS only needs a readable sample to explain why
	-- the hierarchy may be incomplete.
	if #diagnostics.truncatedPaths < TRUNCATED_PATH_SAMPLE_LIMIT then
		table.insert(diagnostics.truncatedPaths, tostring(path) .. " [" .. tostring(reason) .. "]")
	end
end

local function appendTree(instance, depth, seen, diagnostics)
	if instance == nil or seen[instance] or not shouldExposeInExplorerSnapshot(instance) then
		return nil
	end

	seen[instance] = true
	local record, children = collectInstanceRecord(instance, depth)
	diagnostics.objectCount = (tonumber(diagnostics.objectCount) or 0) + 1
	if depth > (tonumber(diagnostics.maxObservedDepth) or 0) then
		diagnostics.maxObservedDepth = depth
	end

	if depth >= MAX_TREE_DEPTH then
		record.truncated = "max-depth"
		rememberTruncatedPath(diagnostics, record.path, "max-depth")
		return record
	end

	local added = 0
	for _, child in ipairs(children) do
		if shouldExposeInExplorerSnapshot(child) then
			added = added + 1
			if added > MAX_CHILDREN_PER_NODE then
				record.truncated = "max-children"
				rememberTruncatedPath(diagnostics, record.path, "max-children")
				break
			end

			local childRecord = appendTree(child, depth + 1, seen, diagnostics)
			if childRecord ~= nil then
				table.insert(record.children, childRecord)
			end
		end
	end

	record.childCount = #record.children

	return record
end

local function getSelectedPaths()
	if Selections == nil then
		return {}
	end

	local selected = safeCall(function()
		return Selections:GetSelected()
	end)

	if selected == nil then
		return {}
	end

	local paths = {}
	for _, instance in ipairs(selected) do
		table.insert(paths, buildPath(instance))
	end

	return paths
end

local function collectSnapshot(requestContext)
	local root = world or World
	if root == nil then
		return nil, "world root is not available"
	end
	local nextSnapshotVersion = sceneSnapshotVersion + 1

	local diagnostics = {
		runtimeVersion = BRIDGE_RUNTIME_VERSION,
		snapshotVersion = nextSnapshotVersion,
		objectCount = 0,
		maxTreeDepth = MAX_TREE_DEPTH,
		maxChildrenPerNode = MAX_CHILDREN_PER_NODE,
		maxWatchedInstances = MAX_WATCHED_INSTANCES,
		maxObservedDepth = 0,
		truncatedCount = 0,
		truncatedPaths = {},
		watcherCount = sceneWatchCount,
		watcherCapReached = sceneWatchCount >= MAX_WATCHED_INSTANCES,
	}

	local tree = appendTree(root, 0, {}, diagnostics)
	if tree == nil then
		return nil, "failed to collect world tree"
	end

	local payload = {
		format = FORMAT_SNAPSHOT,
		version = BRIDGE_VERSION,
		runtimeVersion = BRIDGE_RUNTIME_VERSION,
		snapshotVersion = nextSnapshotVersion,
		snapshotMode = "explorer-visible",
		createdBy = ADDON_NAME,
		createdAtUtc = safeNowText(),
		createdAtUnixSeconds = safeUnixSeconds(),
		selectedPaths = getSelectedPaths(),
		diagnostics = diagnostics,
		root = tree,
	}

	if requestContext ~= nil then
		payload.requestId = tostring(requestContext.requestId or "")
		payload.requestReason = tostring(requestContext.reason or "")
		payload.requestSessionId = tostring(requestContext.sessionId or "")
		diagnostics.requestId = payload.requestId
	end

	return payload, nil
end

function writeSnapshot(quiet, requestContext)
	local payload, err = collectSnapshot(requestContext)
	if payload == nil then
		if not quiet then
			print("[Visual Bridge] Snapshot failed: " .. tostring(err))
		end
		writeStatus("snapshot_error", tostring(err), nil)
		return false, tostring(err), nil
	end

	local text = jsonEncode(payload, "{\"format\":\"" .. FORMAT_SNAPSHOT .. "\",\"version\":1}")
	local ok, writeErr = writeText(SNAPSHOT_PATH, text)
	if not ok then
		if not quiet then
			print("[Visual Bridge] Snapshot write failed: " .. tostring(writeErr))
		end
		writeStatus("snapshot_error", tostring(writeErr), { path = SNAPSHOT_PATH })
		return false, tostring(writeErr), nil
	end

	if not quiet then
		print("[Visual Bridge] Snapshot written to " .. SNAPSHOT_PATH)
	end
	sceneSnapshotVersion = tonumber(payload.snapshotVersion) or (sceneSnapshotVersion + 1)
	lastSnapshotWrittenAt = safeUnixSeconds()
	resetSceneEventStream(sceneSnapshotVersion)
	clearSceneDirty()
	writeStatus(quiet and "auto_snapshot_written" or "snapshot_written", quiet and "Auto scene snapshot exported." or "Scene snapshot exported.", {
		path = SNAPSHOT_PATH,
		requestId = payload.requestId,
		diagnostics = payload.diagnostics,
	})
	return true, "scene snapshot exported", SNAPSHOT_PATH
end

local function collectDeltaPayload()
	if sceneSnapshotVersion <= 0 then
		return nil, "missing full snapshot baseline"
	end

	local baseVersion = sceneSnapshotVersion
	local targetVersion = baseVersion + 1
	local diagnostics = {
		runtimeVersion = BRIDGE_RUNTIME_VERSION,
		baseSnapshotVersion = baseVersion,
		snapshotVersion = targetVersion,
		objectCount = 0,
		maxTreeDepth = MAX_TREE_DEPTH,
		maxChildrenPerNode = MAX_CHILDREN_PER_NODE,
		maxWatchedInstances = MAX_WATCHED_INSTANCES,
		maxObservedDepth = 0,
		truncatedCount = 0,
		truncatedPaths = {},
		watcherCount = sceneWatchCount,
		watcherCapReached = sceneWatchCount >= MAX_WATCHED_INSTANCES,
	}
	local changed = {}
	local changedByPath = {}

	for instance, mode in pairs(sceneDirtyInstances) do
		if instance ~= nil and shouldExposeInExplorerSnapshot(instance) and not isRootOrVisibleServiceRoot(instance) then
			local record = nil
			if mode == "object" then
				record = collectSingleInstanceRecord(instance, 0)
				diagnostics.objectCount = (tonumber(diagnostics.objectCount) or 0) + 1
			else
				record = appendTree(instance, 0, {}, diagnostics)
			end
			if record ~= nil then
				local key = tostring(record.objectId or record.networkedObjectId or record.path or "")
				if key ~= "" and changedByPath[key] ~= true then
					changedByPath[key] = true
					table.insert(changed, record)
				end
			end
		end
	end

	local removed = {}
	local removedByKey = {}
	for _, record in ipairs(sceneRemovedRecords) do
		local key = tostring(record.id or "") .. "|" .. tostring(record.path or "")
		if key ~= "|" and removedByKey[key] ~= true then
			removedByKey[key] = true
			table.insert(removed, record)
		end
	end
	if #changed == 0 and #removed == 0 then
		return nil, "no delta changes"
	end

	return {
		format = FORMAT_DELTA,
		version = BRIDGE_VERSION,
		runtimeVersion = BRIDGE_RUNTIME_VERSION,
		baseSnapshotVersion = baseVersion,
		snapshotVersion = targetVersion,
		snapshotMode = "delta",
		createdBy = ADDON_NAME,
		createdAtUtc = safeNowText(),
		createdAtUnixSeconds = safeUnixSeconds(),
		reasons = sceneDirtyReasons,
		changed = changed,
		removed = removed,
		diagnostics = diagnostics,
	}, nil
end

local function writeDelta(quiet)
	local payload, err = collectDeltaPayload()
	if payload == nil then
		if tostring(err) == "missing full snapshot baseline" then
			return writeSnapshot(true)
		end
		if tostring(err) == "no delta changes" then
			clearSceneDirty()
		end
		return false, tostring(err), nil
	end

	local text = jsonEncode(payload, "{\"format\":\"" .. FORMAT_DELTA .. "\",\"version\":1}")
	local ok, writeErr = writeText(DELTA_PATH, text)
	if not ok then
		if not quiet then
			print("[Visual Bridge] Delta write failed: " .. tostring(writeErr))
		end
		writeStatus("delta_error", tostring(writeErr), { path = DELTA_PATH })
		return false, tostring(writeErr), nil
	end

	sceneSnapshotVersion = tonumber(payload.snapshotVersion) or (sceneSnapshotVersion + 1)
	lastDeltaWrittenAt = safeClockSeconds()
	clearSceneDirty()
	writeStatus("dirty_delta_written", "Smart scene delta exported.", {
		path = DELTA_PATH,
		baseSnapshotVersion = payload.baseSnapshotVersion,
		snapshotVersion = payload.snapshotVersion,
		changedCount = #payload.changed,
		removedCount = #payload.removed,
		bytes = string.len(text),
	})

	if not quiet then
		print("[Visual Bridge] Smart scene delta written to " .. DELTA_PATH)
	end

	return true, "scene delta exported", DELTA_PATH
end

local function collectSceneEventPayloads()
	if sceneSnapshotVersion <= 0 then
		return nil, "missing full snapshot baseline"
	end
	if sceneEventStreamId == "" then
		resetSceneEventStream(sceneSnapshotVersion)
	end

	local events = {}
	local changedByKey = {}
	for instance, mode in pairs(sceneDirtyInstances) do
		if instance ~= nil and shouldExposeInExplorerSnapshot(instance) and not isRootOrVisibleServiceRoot(instance) then
			local record = nil
			local eventType = "upsert_subtree"
			if mode == "object" then
				record = collectSingleInstanceRecord(instance, 0)
				eventType = "upsert_object"
			else
				record = appendTree(instance, 0, {}, {
					objectCount = 0,
					maxObservedDepth = 0,
					truncatedCount = 0,
					truncatedPaths = {},
				})
			end

			if record ~= nil then
				local key = tostring(record.objectId or record.networkedObjectId or record.path or "")
				if key ~= "" and changedByKey[key] ~= true then
					changedByKey[key] = true
					table.insert(events, {
						type = eventType,
						id = tostring(record.objectId or record.networkedObjectId or ""),
						path = tostring(record.path or ""),
						pathPrefix = tostring(record.path or ""),
						object = record,
					})
				end
			end
		end
	end

	local removedByKey = {}
	for _, record in ipairs(sceneRemovedRecords) do
		local path = tostring(record.path or "")
		local key = tostring(record.id or "") .. "|" .. path
		if key ~= "|" and removedByKey[key] ~= true then
			removedByKey[key] = true
			table.insert(events, {
				type = "remove_subtree",
				id = tostring(record.id or ""),
				path = path,
				pathPrefix = path,
			})
		end
	end

	if #events == 0 then
		return nil, "no scene events"
	end

	return events, nil
end

local function writeSceneEventChunk(events)
	sceneEventLatestChunk = sceneEventLatestChunk + 1
	local firstSequence = nil
	local lastSequence = nil
	for _, event in ipairs(events) do
		sceneEventLatestSequence = sceneEventLatestSequence + 1
		event.sequence = sceneEventLatestSequence
		if firstSequence == nil then
			firstSequence = event.sequence
		end
		lastSequence = event.sequence
	end

	local payload = {
		format = FORMAT_EVENT_CHUNK,
		version = BRIDGE_VERSION,
		runtimeVersion = BRIDGE_RUNTIME_VERSION,
		streamId = sceneEventStreamId,
		baseSnapshotVersion = sceneSnapshotVersion,
		chunkIndex = sceneEventLatestChunk,
		firstSequence = firstSequence or 0,
		lastSequence = lastSequence or 0,
		createdBy = ADDON_NAME,
		createdAtUtc = safeNowText(),
		createdAtUnixSeconds = safeUnixSeconds(),
		events = events,
	}
	local text = jsonEncode(payload, "{\"format\":\"" .. FORMAT_EVENT_CHUNK .. "\",\"version\":1}")
	local path = eventChunkPath(sceneEventLatestChunk)
	local ok, err = writeText(path, text)
	if not ok then
		sceneEventLatestChunk = sceneEventLatestChunk - 1
		sceneEventLatestSequence = (firstSequence or (sceneEventLatestSequence + 1)) - 1
		return false, tostring(err), path, string.len(text)
	end
	return true, "scene event chunk exported", path, string.len(text)
end

local function writeSceneEvents(quiet)
	local events, err = collectSceneEventPayloads()
	if events == nil then
		if tostring(err) == "missing full snapshot baseline" then
			return writeSnapshot(true)
		end
		if tostring(err) == "no scene events" then
			clearSceneDirty()
		end
		return false, tostring(err), nil
	end

	local totalEvents = 0
	local totalBytes = 0
	local chunkEvents = {}
	local lastPath = nil
	for _, event in ipairs(events) do
		table.insert(chunkEvents, event)
		local probe = jsonEncode({ events = chunkEvents }, "{}")
		if #chunkEvents >= MAX_EVENTS_PER_CHUNK or string.len(probe) >= MAX_EVENT_CHUNK_BYTES then
			local ok, message, path, bytes = writeSceneEventChunk(chunkEvents)
			if not ok then
				writeStatus("event_chunk_error", tostring(message), { path = path })
				return false, tostring(message), path
			end
			lastPath = path
			totalBytes = totalBytes + (tonumber(bytes) or 0)
			totalEvents = totalEvents + #chunkEvents
			chunkEvents = {}
		end
	end

	if #chunkEvents > 0 then
		local ok, message, path, bytes = writeSceneEventChunk(chunkEvents)
		if not ok then
			writeStatus("event_chunk_error", tostring(message), { path = path })
			return false, tostring(message), path
		end
		lastPath = path
		totalBytes = totalBytes + (tonumber(bytes) or 0)
		totalEvents = totalEvents + #chunkEvents
	end

	local manifestOk, manifestErr = writeSceneEventManifest()
	if not manifestOk then
		writeStatus("event_manifest_error", tostring(manifestErr), { path = SCENE_EVENTS_MANIFEST_PATH })
		return false, tostring(manifestErr), SCENE_EVENTS_MANIFEST_PATH
	end

	lastDeltaWrittenAt = safeClockSeconds()
	clearSceneDirty()
	writeStatus("dirty_events_written", "Smart scene events exported.", {
		path = lastPath,
		manifestPath = SCENE_EVENTS_MANIFEST_PATH,
		streamId = sceneEventStreamId,
		baseSnapshotVersion = sceneSnapshotVersion,
		latestSequence = sceneEventLatestSequence,
		latestChunk = sceneEventLatestChunk,
		eventCount = totalEvents,
		bytes = totalBytes,
	})

	if not quiet then
		print("[Visual Bridge] Smart scene events written: " .. tostring(totalEvents))
	end

	return true, "scene events exported", lastPath
end

local function splitPath(path)
	local parts = {}
	for part in string.gmatch(tostring(path or ""), "[^/]+") do
		table.insert(parts, part)
	end
	return parts
end

local function findChildByName(parent, name)
	if parent == nil then
		return nil
	end

	local child = safeCall(function()
		return parent:FindChild(name)
	end)

	if child ~= nil then
		return child
	end

	for _, candidate in ipairs(readChildren(parent)) do
		if tostring(safeRead(candidate, "Name")) == tostring(name) then
			return candidate
		end
	end

	return nil
end

local function findUniqueChildByName(parent, name)
	if parent == nil then
		return nil, "parent missing"
	end

	local matches = {}
	for _, candidate in ipairs(readChildren(parent)) do
		if tostring(safeRead(candidate, "Name")) == tostring(name) then
			table.insert(matches, candidate)
		end
	end

	if #matches == 1 then
		return matches[1], nil
	end

	if #matches > 1 then
		return nil, "ambiguous child name: " .. tostring(name)
	end

	return nil, "child not found: " .. tostring(name)
end

local function rootByName(name)
	local roots = {
		World = world or World,
		world = world or World,
		Environment = Environment,
		Lighting = Lighting,
		ScriptService = ScriptService,
		Hidden = Hidden,
		ServerHidden = ServerHidden,
		PlayerGUI = PlayerGUI,
	}

	return roots[tostring(name)]
end

local function resolvePath(path)
	if path == nil or path == "" then
		return Environment or world or World
	end

	local parts = splitPath(path)
	if #parts == 0 then
		return Environment or world or World
	end

	local current = rootByName(parts[1])
	local index = 2
	if current == nil then
		current = world or World
		index = 1
	end
	if tostring(parts[1]) == "World" and parts[2] ~= nil then
		local serviceRoot = rootByName(parts[2])
		if serviceRoot ~= nil then
			-- Snapshots use World/ServiceName paths, while Creator also exposes
			-- several services as direct globals. Resolve both forms the same way.
			current = serviceRoot
			index = 3
		end
	end

	while current ~= nil and index <= #parts do
		current = findChildByName(current, parts[index])
		index = index + 1
	end

	return current
end

local function resolvePathStrict(path)
	-- Strict lookup avoids silently choosing the first sibling when Creator has
	-- duplicate names. Ambiguous paths must fail loudly for bridge commands.
	if path == nil or path == "" then
		local root = Environment or world or World
		if root == nil then
			return nil, "default root is not available"
		end
		return root, nil
	end

	local parts = splitPath(path)
	if #parts == 0 then
		return nil, "path has no parts"
	end

	local current = rootByName(parts[1])
	local index = 2
	if current == nil then
		current = world or World
		index = 1
	end
	if tostring(parts[1]) == "World" and parts[2] ~= nil then
		local serviceRoot = rootByName(parts[2])
		if serviceRoot ~= nil then
			-- Keep strict command lookup compatible with snapshot paths such as
			-- World/PlayerGUI while still failing on ambiguous children below it.
			current = serviceRoot
			index = 3
		end
	end

	if current == nil then
		return nil, "world root is not available"
	end

	while current ~= nil and index <= #parts do
		local child, err = findUniqueChildByName(current, parts[index])
		if child == nil then
			return nil, tostring(err) .. " under " .. buildPath(current)
		end
		current = child
		index = index + 1
	end

	return current, nil
end

local function isAncestorOf(candidateAncestor, object)
	local current = safeRead(object, "Parent")
	for _ = 1, 96 do
		if current == nil then
			return false
		end
		if current == candidateAncestor then
			return true
		end
		current = safeRead(current, "Parent")
	end
	return false
end

local function validateExpectedIdentity(instance, command, prefix)
	-- The app sends identity hints from its last snapshot. If Creator changed
	-- since then, rejecting the command is safer than moving the wrong object.
	local expectedName = command[prefix .. "ExpectedName"]
	if prefix == "" and expectedName == nil then
		expectedName = command.expectedName
	end
	if expectedName ~= nil and tostring(expectedName) ~= "" then
		local actualName = tostring(safeRead(instance, "Name") or "")
		if actualName ~= tostring(expectedName) then
			return false, "expected name mismatch: " .. tostring(expectedName) .. " but found " .. actualName
		end
	end

	local expectedClassName = command[prefix .. "ExpectedClassName"]
	if prefix == "" and expectedClassName == nil then
		expectedClassName = command.expectedClassName
	end
	if expectedClassName ~= nil and tostring(expectedClassName) ~= "" then
		local actualClassName = tostring(safeRead(instance, "ClassName") or "")
		if actualClassName ~= tostring(expectedClassName) then
			return false, "expected class mismatch: " .. tostring(expectedClassName) .. " but found " .. actualClassName
		end
	end

	return true, nil
end

local function createInstance(className, parent)
	if Instance == nil then
		return nil, "Instance API is not available"
	end

	local ok, obj = pcall(function()
		return Instance.New(className, parent)
	end)

	if ok and obj ~= nil then
		return obj, nil
	end

	return nil, tostring(obj)
end

local function readHandledCommandIds()
	local handled = {}
	local historyText = readText(COMMAND_HISTORY_PATH)
	if historyText == nil or historyText == "" then
		return handled
	end

	for line in string.gmatch(historyText, "[^\r\n]+") do
		local payload = jsonDecode(line)
		if payload ~= nil and payload.id ~= nil then
			handled[tostring(payload.id)] = true
		end
	end

	return handled
end

local function appendCommandHistory(result)
	local line = jsonEncode(result, "{\"id\":\"" .. tostring(result.id or "") .. "\"}")
	local previous = readText(COMMAND_HISTORY_PATH) or ""
	writeText(COMMAND_HISTORY_PATH, previous .. line .. "\n")
end

local function commandCreateFolder(command)
	local parent = resolvePath(command.parentPath)
	if parent == nil then
		return false, "parent not found: " .. tostring(command.parentPath), nil
	end

	local folderName = tostring(command.name or "VisualFolder")
	if folderName == "" then
		return false, "folder name is empty", nil
	end

	if command.dryRun == true then
		return true, "dry-run create_folder ok: " .. folderName, tostring(command.parentPath or "")
	end

	local folder, err = createInstance("Folder", parent)
	if folder == nil then
		return false, "create_folder failed: " .. tostring(err), nil
	end

	safeWrite(folder, "Name", folderName)
	return true, "created Folder", buildPath(folder)
end

local function commandEnsureFolderPath(command)
	local path = tostring(command.path or "")
	local parts = splitPath(path)
	if #parts == 0 then
		return false, "ensure_folder_path failed: path is empty", nil
	end

	local current = rootByName(parts[1])
	local index = 2
	if current == nil then
		current = world or World
		index = 1
	end
	if tostring(parts[1]) == "World" and parts[2] ~= nil then
		local serviceRoot = rootByName(parts[2])
		if serviceRoot ~= nil then
			-- The app may send either Hidden/VRS or World/Hidden/VRS. Treat both
			-- forms identically so the preset stays stable across snapshot formats.
			current = serviceRoot
			index = 3
		end
	end

	if current == nil then
		return false, "ensure_folder_path failed: root not found for " .. path, nil
	end

	if command.dryRun == true then
		return true, "dry-run ensure_folder_path ok: " .. path, buildPath(current)
	end

	local createdAny = false
	while index <= #parts do
		local name = tostring(parts[index] or "")
		if name == "" then
			return false, "ensure_folder_path failed: empty path segment in " .. path, buildPath(current)
		end

		local child, childErr = findUniqueChildByName(current, name)
		if child == nil then
			if childErr ~= "child not found: " .. name then
				return false, "ensure_folder_path failed: " .. tostring(childErr), buildPath(current)
			end
			local folder, createErr = createInstance("Folder", current)
			if folder == nil then
				return false, "ensure_folder_path failed: " .. tostring(createErr), buildPath(current)
			end
			safeWrite(folder, "Name", name)
			child = folder
			createdAny = true
		else
			local className = tostring(safeRead(child, "ClassName") or "")
			if className ~= "Folder" and not isA(child, "Folder") then
				return false, "ensure_folder_path blocked by non-Folder segment: " .. buildPath(child), buildPath(child)
			end
		end

		current = child
		index = index + 1
	end

	if createdAny then
		return true, "created", buildPath(current)
	end

	return true, "already_exists", buildPath(current)
end

local function commandEnsureNetworkEvent(command)
	local parentPath = tostring(command.parentPath or "")
	local eventName = tostring(command.name or "")
	if parentPath == "" then
		return false, "ensure_network_event failed: parent path is empty", nil
	end
	if eventName == "" then
		return false, "ensure_network_event failed: event name is empty", nil
	end

	local parent = resolvePath(parentPath)
	if parent == nil then
		return false, "ensure_network_event failed: parent not found: " .. parentPath, nil
	end

	local existing, existingErr = findUniqueChildByName(parent, eventName)
	if existing ~= nil then
		local className = tostring(safeRead(existing, "ClassName") or "")
		if className ~= "NetworkEvent" and not isA(existing, "NetworkEvent") then
			return false, "ensure_network_event blocked by non-NetworkEvent child: " .. buildPath(existing), buildPath(existing)
		end

		return true, "already_exists", buildPath(existing)
	end

	if existingErr ~= "child not found: " .. eventName then
		return false, "ensure_network_event failed: " .. tostring(existingErr), buildPath(parent)
	end

	if command.dryRun == true then
		return true, "dry-run ensure_network_event ok: " .. eventName, buildPath(parent)
	end

	local event, createErr = createInstance("NetworkEvent", parent)
	if event == nil then
		return false, "ensure_network_event failed: " .. tostring(createErr), buildPath(parent)
	end

	safeWrite(event, "Name", eventName)
	return true, "created NetworkEvent", buildPath(event)
end

local function defaultScriptSource(className, scriptName)
	return "--!nocheck\n" ..
		"-- Generated by [VRS]Visual Programming Bridge.\n" ..
		"-- Script type: " .. tostring(className) .. "\n" ..
		"-- Name: " .. tostring(scriptName) .. "\n\n" ..
		"print(\"[VisualBridge] " .. tostring(scriptName) .. " ready\")\n"
end

local function isScriptInstance(instance)
	local className = tostring(safeRead(instance, "ClassName") or "")
	if scriptClassNameLooksScript(className) then
		return true
	end

	-- Creator builds may expose a shared Script base type. Use IsA when the
	-- runtime supports it, but keep class-name checks above as the stable path.
	return isA(instance, "Script")
end

local function scriptFileSuffix(className)
	if className == "ServerScript" then
		return ".server.luau"
	end
	if className == "ClientScript" then
		return ".client.luau"
	end
	if className == "ModuleScript" then
		return ".module.luau"
	end

	return ".luau"
end

local function safeScriptFileSegment(text)
	local value = tostring(text or "Script")
	value = string.gsub(value, "[^%w_%-%.]+", "_")
	if value == "" then
		return "Script"
	end

	return value
end

local function generatedScriptPathFor(script)
	local className = tostring(safeRead(script, "ClassName") or "Script")
	local pathSegment = safeScriptFileSegment(buildPath(script))
	return "scripts/generated/" .. pathSegment .. scriptFileSuffix(className)
end

local function commandLinkedScriptPath(command, script)
	local linkedPath = tostring(command.linkedFilePath or command.scriptPath or "")
	if linkedPath ~= "" then
		return linkedPath
	end

	return generatedScriptPathFor(script)
end

local function callLinkWithScriptFile(script, scriptPath)
	local okMethod, method = pcall(function()
		return script.LinkWithScriptFile
	end)

	if not okMethod or type(method) ~= "function" then
		return false, "LinkWithScriptFile method is not available"
	end

	local ok, err = pcall(function()
		-- Use the Creator API in method-call form so the Script instance stays
		-- bound exactly as Creator expects for linked file references.
		script:LinkWithScriptFile(scriptPath)
	end)

	if ok then
		return true, nil
	end

	return false, tostring(err)
end

local function attachScriptSource(script, source, linkedFilePath, fileAlreadyWritten)
	local text = tostring(source or "")
	local scriptPath = tostring(linkedFilePath or generatedScriptPathFor(script))
	local fileWarning = nil
	if fileAlreadyWritten == true then
		-- Deploy Instance must never rewrite the VRS file. The standalone app
		-- already produced the linked source; here the addon only verifies and
		-- attaches it so an empty command payload cannot erase the script.
		local existingText, existingErr = readText(scriptPath)
		if existingText == nil then
			return false, "linked file read failed: " .. tostring(existingErr), scriptPath, {
				linkedFilePath = scriptPath,
				warning = "missing_linked_file",
			}
		end
		if tostring(existingText) == "" then
			return false, "linked file is empty; deploy the VRS file first", scriptPath, {
				linkedFilePath = scriptPath,
				warning = "empty_linked_file",
			}
		end
		if text == "" then
			text = tostring(existingText)
		end
	else
		local fileOk, fileErr = writeText(scriptPath, text)
		if not fileOk then
			return false, "linked file write failed: " .. tostring(fileErr), scriptPath, {
				linkedFilePath = scriptPath,
				warning = "unlinked_script",
			}
		end
	end

	local linkOk, linkErr = callLinkWithScriptFile(script, scriptPath)
	if not linkOk then
		-- A plain Source write can leave Creator with "Script does not have file
		-- reference". For visual scripting, the linked file is the durable source
		-- of truth, so failed linking must remain visible to the app.
		return false, "linked file attach failed: " .. tostring(linkErr), scriptPath, {
			linkedFilePath = scriptPath,
			warning = "unlinked_script",
			fileWriteWarning = fileWarning,
		}
	end

	-- Some Creator builds also expose Source. Keep it synchronized when
	-- available, but do not depend on it for the editable-file workflow.
	local sourceOk, sourceErr = safeWrite(script, "Source", text)
	local registryOk, registryErr = rememberScriptLink(script, scriptPath)

	return true, nil, scriptPath, {
		linkedFilePath = scriptPath,
		fileWriteWarning = fileWarning,
		linkRegistryWarning = registryOk and nil or tostring(registryErr),
		sourceWarning = sourceOk and nil or tostring(sourceErr),
	}
end

local function readScriptSource(script, fallbackLinkedPath)
	local linkedPath = safeRead(script, "LinkedScript")
	if linkedPath ~= nil and tostring(linkedPath) ~= "" then
		local text, err = readText(tostring(linkedPath))
		if text ~= nil then
			return text, tostring(linkedPath), nil
		end
	end

	local fallbackPath = tostring(fallbackLinkedPath or "")
	local fallbackErr = nil
	if fallbackPath ~= "" then
		-- The standalone app can know the canonical VRS file before Creator
		-- confirms LinkedScript in the snapshot. Read that file as a repair
		-- aid, but still report the real origin so stale-link bugs stay visible.
		local text, err = readText(fallbackPath)
		if text ~= nil then
			return text, fallbackPath, nil
		end
		fallbackErr = err
	end

	local source = safeRead(script, "Source")
	if source ~= nil then
		return tostring(source), "Source", nil
	end

	local generatedPath = generatedScriptPathFor(script)
	local text, err = readText(generatedPath)
	if text ~= nil then
		return text, generatedPath, nil
	end

	return nil, generatedPath, err or fallbackErr
end

local function startsWith(text, prefix)
	text = tostring(text or "")
	prefix = tostring(prefix or "")
	return string.sub(text, 1, #prefix) == prefix
end

local function pathIsInsideRoot(path, rootName)
	path = tostring(path or "")
	rootName = tostring(rootName or "")
	local worldRoot = "World/" .. rootName

	return path == rootName
		or path == worldRoot
		or startsWith(path, rootName .. "/")
		or startsWith(path, worldRoot .. "/")
end

local function defaultScriptParentPath(className)
	if className == "ClientScript" then
		return "PlayerGUI"
	end

	return "ScriptService"
end

local function allowedScriptRoots(className)
	if className == "ClientScript" then
		return { "PlayerGUI" }
	end

	if className == "ServerScript" then
		return { "ScriptService", "ServerHidden" }
	end

	if className == "ModuleScript" then
		return { "ScriptService", "ServerHidden", "Hidden", "PlayerGUI" }
	end

	return {}
end

local function normalizeScriptParent(className, parent, requestedParentPath, exactParent)
	if exactParent == true then
		-- A context-menu command means the user deliberately asked for a child
		-- script under this exact object. If Creator rejects that parent, the
		-- create call will fail visibly instead of silently moving the script.
		return parent, nil
	end

	local requestedPath = tostring(requestedParentPath or "")
	local currentPath = parent ~= nil and buildPath(parent) or requestedPath

	for _, rootName in ipairs(allowedScriptRoots(className)) do
		if pathIsInsideRoot(currentPath, rootName) or pathIsInsideRoot(requestedPath, rootName) then
			return parent, nil
		end
	end

	local fallbackPath = defaultScriptParentPath(className)
	local fallbackParent = resolvePath(fallbackPath)
	if fallbackParent ~= nil then
		-- The app should normally send the right parent, but this guard keeps a
		-- stale or external command from creating scripts directly under World.
		return fallbackParent, "parent adjusted from " .. tostring(requestedParentPath or "") .. " to " .. fallbackPath
	end

	return nil, "script parent invalid and fallback missing: " .. fallbackPath
end

local function scriptClassNameFromCommand(command)
	local raw = tostring(command.className or command.kind or command.scriptKind or "")
	if raw == "Server" or raw == "ServerScript" then
		return "ServerScript"
	end
	if raw == "Local" or raw == "LocalScript" or raw == "Client" or raw == "ClientScript" then
		return "ClientScript"
	end
	if raw == "Module" or raw == "ModuleScript" then
		return "ModuleScript"
	end

	return "ServerScript"
end

local function commandCreateScript(command, className)
	local parent = resolvePath(command.parentPath)
	if parent == nil then
		return false, "parent not found: " .. tostring(command.parentPath), nil
	end

	local normalizedParent, parentNote = normalizeScriptParent(className, parent, command.parentPath, command.exactParent == true)
	if normalizedParent == nil then
		return false, tostring(parentNote), nil
	end
	parent = normalizedParent

	local scriptName = tostring(command.name or className)
	if scriptName == "" then
		return false, "script name is empty", nil
	end

	if command.dryRun == true then
		local dryRunMessage = "dry-run " .. className .. " ok: " .. scriptName
		if parentNote ~= nil then
			dryRunMessage = dryRunMessage .. " (" .. parentNote .. ")"
		end
		return true, dryRunMessage, buildPath(parent)
	end

	local script, err = createInstance(className, parent)
	if script == nil then
		return false, "create " .. className .. " failed: " .. tostring(err), nil
	end

	safeWrite(script, "Name", scriptName)
	local linkedFilePath = commandLinkedScriptPath(command, script)
	local sourceOk, sourceErr, generatedPath, sourceDetails = attachScriptSource(
		script,
		command.source or defaultScriptSource(className, scriptName),
		linkedFilePath,
		command.fileAlreadyWritten == true)
	if not sourceOk then
		return false, "created " .. className .. " but source attach failed: " .. tostring(sourceErr), buildPath(script), sourceDetails
	end
	local message = "created " .. className
	if parentNote ~= nil then
		message = message .. " (" .. parentNote .. ")"
	end
	return true, message .. " linked to " .. tostring(generatedPath), buildPath(script), sourceDetails
end

local function commandDeployVrsScript(command)
	local className = tostring(command.className or "")
	if className ~= "ServerScript" and className ~= "ClientScript" and className ~= "ModuleScript" then
		return false, "deploy_vrs_script failed: unsupported className " .. className, nil, {
			deployStatus = "unsupported_class",
		}
	end

	-- Deploy uses the same safe creation/linking path as script creation, but
	-- returns bridge-specific status values so the external app can explain
	-- whether the hierarchy object exists, whether the file link succeeded, and
	-- whether the script was created only partially.
	local ok, message, path, details = commandCreateScript(command, className)
	details = details or {}
	details.deployStatus = ok and "linked" or "link_failed"
	details.visualScript = command.visualScript == true
	details.sharedFile = command.sharedFile == true
	details.linkedFilePath = command.linkedFilePath

	if not ok then
		local messageText = tostring(message or "")
		if string.find(messageText, "parent not found", 1, true) ~= nil then
			details.deployStatus = "parent_not_found"
		elseif string.find(messageText, "source attach failed", 1, true) ~= nil then
			details.deployStatus = "created_but_unlinked"
		end
		return false, messageText, path, details
	end

	return true, "deployed shared VRS script: " .. tostring(command.name or className), path, details
end

local function commandUpsertScript(command)
	local className = scriptClassNameFromCommand(command)
	local parent = resolvePath(command.parentPath)
	if parent == nil then
		return false, "upsert_script failed: parent not found: " .. tostring(command.parentPath), nil
	end

	local normalizedParent, parentNote = normalizeScriptParent(className, parent, command.parentPath, command.exactParent == true)
	if normalizedParent == nil then
		return false, "upsert_script failed: " .. tostring(parentNote), nil
	end
	parent = normalizedParent

	local scriptName = tostring(command.name or className)
	if scriptName == "" then
		return false, "upsert_script failed: script name is empty", nil
	end

	local existing, existingErr = findUniqueChildByName(parent, scriptName)
	if existing ~= nil then
		local existingClassName = tostring(safeRead(existing, "ClassName") or "")
		if existingClassName ~= className then
			return false, "upsert_script blocked: existing child has class " .. existingClassName .. " instead of " .. className, buildPath(existing)
		end
	elseif existingErr ~= "child not found: " .. scriptName then
		return false, "upsert_script failed: " .. tostring(existingErr), buildPath(parent)
	end

	if command.dryRun == true then
		local dryRunMessage = existing ~= nil and "dry-run update " .. className .. " ok: " or "dry-run create " .. className .. " ok: "
		dryRunMessage = dryRunMessage .. scriptName
		if parentNote ~= nil then
			dryRunMessage = dryRunMessage .. " (" .. parentNote .. ")"
		end
		return true, dryRunMessage, existing ~= nil and buildPath(existing) or buildPath(parent)
	end

	local script = existing
	local created = false
	if script == nil then
		local createErr = nil
		script, createErr = createInstance(className, parent)
		if script == nil then
			return false, "upsert_script create " .. className .. " failed: " .. tostring(createErr), nil
		end
		safeWrite(script, "Name", scriptName)
		created = true
	end

	local source = command.content or command.source or defaultScriptSource(className, scriptName)
	local linkedFilePath = commandLinkedScriptPath(command, script)
	local sourceOk, sourceErr, generatedPath, sourceDetails = attachScriptSource(
		script,
		source,
		linkedFilePath,
		command.fileAlreadyWritten == true)
	if not sourceOk then
		return false, "upsert_script source attach failed: " .. tostring(sourceErr), buildPath(script), sourceDetails
	end

	sourceDetails = sourceDetails or {}
	sourceDetails.created = created
	sourceDetails.scriptKind = className
	sourceDetails.targetPath = command.targetPath
	if parentNote ~= nil then
		sourceDetails.parentNote = parentNote
	end

	local actionText = created and "created" or "updated"
	return true, "upsert_script " .. actionText .. " " .. className .. " linked to " .. tostring(generatedPath), buildPath(script), sourceDetails
end

local function commandSetScriptSource(command)
	local objectPath = command.objectPath or command.path or command.targetPath
	local script, scriptErr = resolvePathStrict(objectPath)
	if script == nil then
		return false, "script not found: " .. tostring(objectPath) .. " (" .. tostring(scriptErr) .. ")", nil
	end

	local okIdentity, identityErr = validateExpectedIdentity(script, command, "")
	if not okIdentity then
		return false, "set_script_source rejected: target " .. tostring(identityErr), buildPath(script)
	end

	if not isScriptInstance(script) then
		return false, "set_script_source rejected: target is not a script", buildPath(script)
	end

	local source = command.source
	if source == nil then
		return false, "set_script_source failed: source field is missing", buildPath(script)
	end

	if command.dryRun == true then
		return true, "dry-run set_script_source ok: " .. tostring(objectPath), buildPath(script)
	end

	-- Prefer the linked VRS file when Source is not exposed by the current
	-- Creator build. This keeps scripts editable from Creator and the project
	-- file browser instead of trapping them in a bridge-only command.
	local linkedFilePath = commandLinkedScriptPath(command, script)
	local ok, err, generatedPath, sourceDetails = attachScriptSource(script, source, linkedFilePath, command.fileAlreadyWritten == true)
	if not ok then
		return false, "set_script_source failed: " .. tostring(err), buildPath(script), sourceDetails
	end

	return true, "updated script source via " .. tostring(generatedPath), buildPath(script), sourceDetails
end

local function commandRepairLinkedScript(command)
	local objectPath = command.objectPath or command.path or command.targetPath
	local script, scriptErr = resolvePathStrict(objectPath)
	if script == nil then
		return false, "repair_linked_script failed: script not found: " .. tostring(objectPath) .. " (" .. tostring(scriptErr) .. ")", nil
	end

	local okIdentity, identityErr = validateExpectedIdentity(script, command, "")
	if not okIdentity then
		return false, "repair_linked_script rejected: target " .. tostring(identityErr), buildPath(script)
	end

	if not isScriptInstance(script) then
		return false, "repair_linked_script rejected: target is not a script", buildPath(script)
	end

	local linkedFilePath = commandLinkedScriptPath(command, script)
	if linkedFilePath == "" then
		return false, "repair_linked_script failed: linkedFilePath is missing", buildPath(script)
	end

	if command.dryRun == true then
		return true, "dry-run repair_linked_script ok: " .. linkedFilePath, buildPath(script), {
			linkedFilePath = linkedFilePath,
		}
	end

	local sourceText, sourceErr = readText(linkedFilePath)
	local linkOk, linkErr = callLinkWithScriptFile(script, linkedFilePath)
	if not linkOk then
		return false, "repair_linked_script failed: " .. tostring(linkErr), buildPath(script), {
			linkedFilePath = linkedFilePath,
			warning = "unlinked_script",
			fileReadWarning = sourceErr,
		}
	end

	local sourceWarning = nil
	if sourceText ~= nil then
		local sourceOk, sourceWriteErr = safeWrite(script, "Source", sourceText)
		if not sourceOk then
			-- Linking is the durable workflow. Source synchronization is useful
			-- when available, but some Creator builds expose only LinkedScript.
			sourceWarning = tostring(sourceWriteErr)
		end
	else
		sourceWarning = "linked file could not be read after link: " .. tostring(sourceErr)
	end

	local registryOk, registryErr = rememberScriptLink(script, linkedFilePath)

	return true, "repaired linked script file: " .. linkedFilePath, buildPath(script), {
		linkedFilePath = linkedFilePath,
		linkRegistryWarning = registryOk and nil or tostring(registryErr),
		sourceWarning = sourceWarning,
	}
end

local function commandExportScriptSource(command)
	local objectPath = command.objectPath or command.path or command.targetPath
	local script, scriptErr = resolvePathStrict(objectPath)
	if script == nil then
		return false, "script not found: " .. tostring(objectPath) .. " (" .. tostring(scriptErr) .. ")", nil, nil
	end

	local okIdentity, identityErr = validateExpectedIdentity(script, command, "")
	if not okIdentity then
		return false, "export_script_source rejected: target " .. tostring(identityErr), buildPath(script), nil
	end

	if not isScriptInstance(script) then
		return false, "export_script_source rejected: target is not a script", buildPath(script), nil
	end

	-- Reading Source is intentionally a bridge command instead of a silent app
	-- assumption. Creator remains the authority for the script currently stored
	-- in the project, and the visual app reconstructs nodes only from this text.
	local fallbackLinkedPath = command.linkedFilePath or command.scriptPath
	local source, sourceOrigin, sourceErr = readScriptSource(script, fallbackLinkedPath)
	if source == nil then
		return false, "export_script_source failed: " .. tostring(sourceErr), buildPath(script), nil
	end

	return true, "exported script source from " .. tostring(sourceOrigin), buildPath(script), {
		source = tostring(source),
		sourceOrigin = tostring(sourceOrigin),
	}
end

local function commandRename(command)
	local object = resolvePath(command.objectPath or command.path)
	if object == nil then
		return false, "object not found: " .. tostring(command.objectPath or command.path), nil
	end

	local newName = tostring(command.newName or command.name or "")
	if newName == "" then
		return false, "new name is empty", nil
	end

	if command.dryRun == true then
		return true, "dry-run rename ok: " .. tostring(command.objectPath or command.path) .. " -> " .. newName, buildPath(object)
	end

	local ok, err = safeWrite(object, "Name", newName)
	if not ok then
		return false, "rename failed: " .. tostring(err), buildPath(object)
	end

	return true, "renamed object", buildPath(object)
end

local function commandSetParent(command)
	local objectPath = command.objectPath or command.path
	local parentPath = command.parentPath or command.newParentPath
	local object, objectErr = resolvePathStrict(objectPath)
	if object == nil then
		return false, "object not found: " .. tostring(objectPath) .. " (" .. tostring(objectErr) .. ")", nil
	end

	local okIdentity, identityErr = validateExpectedIdentity(object, command, "")
	if not okIdentity then
		return false, "move rejected: source " .. tostring(identityErr), buildPath(object)
	end

	local expectedParentPath = tostring(command.expectedParentPath or "")
	if expectedParentPath ~= "" then
		local currentParent = safeRead(object, "Parent")
		local currentParentPath = currentParent ~= nil and buildPath(currentParent) or ""
		-- Guard against stale snapshots: drag/drop should only apply if the
		-- object is still where the app thought it was when the command was made.
		if currentParentPath ~= expectedParentPath then
			return false, "move rejected: source parent changed from " .. expectedParentPath .. " to " .. currentParentPath, buildPath(object)
		end
	end

	local parent, parentErr = resolvePathStrict(parentPath)
	if parent == nil then
		return false, "parent not found: " .. tostring(parentPath) .. " (" .. tostring(parentErr) .. ")", buildPath(object)
	end

	local okParentIdentity, parentIdentityErr = validateExpectedIdentity(parent, command, "parent")
	if not okParentIdentity then
		return false, "move rejected: target parent " .. tostring(parentIdentityErr), buildPath(object)
	end

	if object == parent then
		return false, "set_parent rejected: object cannot parent itself", buildPath(object)
	end

	if isAncestorOf(object, parent) then
		return false, "set_parent rejected: object cannot move inside its own child", buildPath(object)
	end

	local oldPath = buildPath(object)
	if command.dryRun == true then
		return true, "dry-run set_parent ok: " .. oldPath .. " -> " .. buildPath(parent), oldPath
	end

	local ok, err = pcall(function()
		object:SetParent(parent)
	end)
	if ok then
		return true, "moved object: " .. oldPath .. " -> " .. buildPath(object), buildPath(object)
	end

	ok, err = pcall(function()
		object:Reparent(parent)
	end)
	if ok then
		return true, "moved object with Reparent: " .. oldPath .. " -> " .. buildPath(object), buildPath(object)
	end

	ok, err = safeWrite(object, "Parent", parent)
	if ok then
		return true, "moved object by writing Parent: " .. oldPath .. " -> " .. buildPath(object), buildPath(object)
	end

	return false, "set_parent failed: " .. tostring(err), buildPath(object)
end

local function callNoArgInstanceMethod(object, methodName)
	local ok, method = pcall(function()
		return object[methodName]
	end)

	if not ok or type(method) ~= "function" then
		return false, methodName .. " method is not available"
	end

	local callOk, err = pcall(function()
		method(object)
	end)
	if callOk then
		return true, nil
	end

	return false, tostring(err)
end

local function ensureTrashFolder()
	local root = ServerHidden or Hidden or Environment or world or World
	if root == nil then
		return nil, "no root available for trash fallback"
	end

	local trash = findChildByName(root, "VisualBridgeTrash")
	if trash ~= nil then
		return trash, nil
	end

	local created, err = createInstance("Folder", root)
	if created == nil then
		return nil, "trash folder create failed: " .. tostring(err)
	end

	safeWrite(created, "Name", "VisualBridgeTrash")
	return created, nil
end

local function commandDelete(command)
	local objectPath = command.objectPath or command.path
	local parts = splitPath(objectPath)
	if #parts <= 2 then
		return false, "delete rejected for root/service path: " .. tostring(objectPath), tostring(objectPath)
	end

	local object = resolvePath(objectPath)
	if object == nil then
		return false, "object not found: " .. tostring(objectPath), nil
	end

	if command.dryRun == true then
		return true, "dry-run delete ok: " .. tostring(objectPath), buildPath(object)
	end

	if command.confirm ~= true then
		return false, "delete requires confirm=true or dryRun=true", buildPath(object)
	end

	-- Early live delete defaults to trash movement. Direct destructive methods
	-- remain guarded while Creator API behavior is still being validated.
	if command.trashOnly ~= true then
		for _, methodName in ipairs({ "Delete", "Destroy", "Remove" }) do
			local ok, err = callNoArgInstanceMethod(object, methodName)
			if ok then
				return true, "deleted object with " .. methodName, tostring(objectPath)
			end
		end
	end

	local trash, trashErr = ensureTrashFolder()
	if trash == nil then
		return false, "delete fallback failed: " .. tostring(trashErr), buildPath(object)
	end

	local ok, err = pcall(function()
		object:SetParent(trash)
	end)
	if ok then
		return true, "delete fallback: moved object to VisualBridgeTrash", buildPath(object)
	end

	ok, err = pcall(function()
		object:Reparent(trash)
	end)
	if ok then
		return true, "delete fallback: reparented object to VisualBridgeTrash", buildPath(object)
	end

	return false, "delete API and trash fallback failed: " .. tostring(err), buildPath(object)
end

local function applyCommand(command)
	local action = tostring(command.action or command.type or "")

	if action == "export_hierarchy" then
		return writeSnapshot(false)
	end

	if action == "create_folder" then
		return commandCreateFolder(command)
	end

	if action == "ensure_folder_path" then
		return commandEnsureFolderPath(command)
	end

	if action == "ensure_network_event" then
		return commandEnsureNetworkEvent(command)
	end

	if action == "create_server_script" then
		return commandCreateScript(command, "ServerScript")
	end

	if action == "create_client_script" then
		return commandCreateScript(command, "ClientScript")
	end

	if action == "create_module_script" then
		return commandCreateScript(command, "ModuleScript")
	end

	if action == "deploy_vrs_script" then
		return commandDeployVrsScript(command)
	end

	if action == "upsert_script" then
		return commandUpsertScript(command)
	end

	if action == "set_script_source" or action == "edit_script_source" then
		return commandSetScriptSource(command)
	end

	if action == "repair_linked_script" then
		return commandRepairLinkedScript(command)
	end

	if action == "export_script_source" then
		return commandExportScriptSource(command)
	end

	if action == "rename" then
		return commandRename(command)
	end

	if action == "set_parent" or action == "move" then
		return commandSetParent(command)
	end

	if action == "delete" then
		return commandDelete(command)
	end

	return false, "unsupported action in V1: " .. action, nil
end

local function writeResults(results)
	local payload = {
		format = FORMAT_RESULTS,
		version = BRIDGE_VERSION,
		runtimeVersion = BRIDGE_RUNTIME_VERSION,
		createdBy = ADDON_NAME,
		createdAtUtc = safeNowText(),
		results = results,
	}

	writeText(COMMAND_RESULTS_PATH, jsonEncode(payload, "{\"format\":\"" .. FORMAT_RESULTS .. "\",\"version\":1,\"results\":[]}"))
end

local function clearPendingCommands()
	local empty = {
		format = FORMAT_COMMANDS,
		version = BRIDGE_VERSION,
		commands = {},
		clearedAtUtc = safeNowText(),
		clearedBy = ADDON_NAME,
	}

	writeText(PENDING_COMMANDS_PATH, jsonEncode(empty, "{\"format\":\"" .. FORMAT_COMMANDS .. "\",\"version\":1,\"commands\":[]}"))
end

local function readPendingCommandCount()
	local text, err = readText(PENDING_COMMANDS_PATH)
	if text == nil or text == "" then
		return 0, tostring(err)
	end

	local payload = jsonDecode(text)
	if payload == nil or payload.commands == nil then
		return 0, "pending command JSON could not be parsed"
	end

	return #payload.commands, nil
end

local function applyPendingCommands(quiet)
	local text, err = readText(PENDING_COMMANDS_PATH)
	if text == nil or text == "" then
		if not quiet then
			print("[Visual Bridge] No pending command file found: " .. tostring(err))
		end
		writeStatus("no_pending_commands", tostring(err), { path = PENDING_COMMANDS_PATH })
		return
	end

	local payload, parseErr = jsonDecode(text)
	if payload == nil then
		if not quiet then
			print("[Visual Bridge] Pending command JSON parse failed: " .. tostring(parseErr))
		end
		writeStatus("command_parse_error", tostring(parseErr), { path = PENDING_COMMANDS_PATH })
		return
	end

	local commands = payload.commands or {}
	if #commands == 0 then
		if not quiet then
			print("[Visual Bridge] Pending command file has no commands.")
		end
		writeStatus("no_pending_commands", "Pending command file has no commands.", nil)
		return
	end

	local handled = readHandledCommandIds()
	local results = {}
	local successCount = 0

	for index, command in ipairs(commands) do
		local id = tostring(command.id or "")
		local action = command.action or command.type
		local ok = false
		local message = "missing command id"
		local path = nil
		local details = nil
		local skipped = false

		if id ~= "" and handled[id] == true then
			ok = true
			message = "skipped duplicate command id"
			skipped = true
		elseif id ~= "" then
			ok, message, path, details = applyCommand(command)
			handled[id] = true
			if ok then
				successCount = successCount + 1
			end
		end

		local result = {
			index = index,
			id = command.id,
			action = action,
			ok = ok,
			message = message,
			path = path,
			details = details,
			skipped = skipped,
			handledAtUtc = safeNowText(),
		}
		table.insert(results, result)
		appendCommandHistory(result)
	end

	writeResults(results)
	clearPendingCommands()
	writeSnapshot(quiet)
	if not quiet then
		print("[Visual Bridge] Applied " .. tostring(successCount) .. "/" .. tostring(#commands) .. " visual commands.")
	end
	writeStatus(quiet and "auto_commands_applied" or "commands_applied", "Applied " .. tostring(successCount) .. "/" .. tostring(#commands) .. " commands.", {
		successCount = successCount,
		totalCount = #commands,
		resultsPath = COMMAND_RESULTS_PATH,
	})
end

local function flushDirtySnapshotIfReady(settings, quiet)
	if sceneDirty ~= true or settings.autoExport ~= true then
		return false
	end

	local now = safeClockSeconds()
	if lastSceneDirtyAt == nil then
		return false
	end

	if now - lastSceneDirtyAt < DIRTY_DELTA_DEBOUNCE_SECONDS then
		return false
	end

	if lastDeltaWrittenAt ~= nil and now - lastDeltaWrittenAt < DIRTY_DELTA_DEBOUNCE_SECONDS then
		return false
	end

	local reasons = sceneDirtyReasons
	local dirtySince = sceneDirtySince
	local ok, message, path = writeSceneEvents(true)
	writeStatus(ok and "dirty_events_written" or "dirty_events_failed", tostring(message), {
		path = path or SCENE_EVENTS_MANIFEST_PATH,
		dirtySince = dirtySince,
		reasons = reasons,
	})

	if not quiet then
		print("[Visual Bridge] Dirty scene events: " .. tostring(message))
	end

	return ok
end

local function printBridgeStatus()
	print("[Visual Bridge] Snapshot path: " .. SNAPSHOT_PATH)
	print("[Visual Bridge] Delta path: " .. DELTA_PATH)
	print("[Visual Bridge] Scene events manifest path: " .. SCENE_EVENTS_MANIFEST_PATH)
	print("[Visual Bridge] Settings path: " .. SETTINGS_PATH)
	print("[Visual Bridge] App heartbeat path: " .. APP_HEARTBEAT_PATH)
	print("[Visual Bridge] App state path: " .. APP_STATE_PATH)
	print("[Visual Bridge] Snapshot request path: " .. SNAPSHOT_REQUEST_PATH)
	print("[Visual Bridge] Pending commands path: " .. PENDING_COMMANDS_PATH)
	print("[Visual Bridge] Command results path: " .. COMMAND_RESULTS_PATH)
	print("[Visual Bridge] Status path: " .. STATUS_PATH)
	print("[Visual Bridge] History path: " .. COMMAND_HISTORY_PATH)
	local appStateSummary = readAppStateSummary()
	if appStateSummary ~= nil then
		print("[Visual Bridge] App state: " .. appStateSummary)
	end
	writeStatus("status_printed", "Bridge paths printed in Creator console.", {
		appStatePath = APP_STATE_PATH,
		appStateSummary = appStateSummary,
	})
end

local function toggleAutoBridge()
	local settings = readBridgeSettings()
	settings.autoMode = not settings.autoMode
	writeBridgeSettings(settings)
	print("[Visual Bridge] Auto Bridge " .. (settings.autoMode and "enabled." or "disabled."))
	writeStatus(settings.autoMode and "auto_enabled" or "auto_disabled", settings.autoMode and "Auto Bridge enabled." or "Auto Bridge disabled.", {
		intervalSeconds = settings.intervalSeconds,
		autoExport = settings.autoExport,
		autoApply = settings.autoApply,
	})
end

local function startAutoBridgeLoop()
	if spawn == nil or wait == nil then
		print("[Visual Bridge] Auto Bridge unavailable: spawn/wait globals are missing.")
		writeStatus("auto_unavailable", "Auto Bridge unavailable: spawn/wait globals are missing.", nil)
		return
	end

	spawn(function()
		local applyTicks = 0
		local appWasActive = false
		local loopDelaySeconds = 0.5
		while autoBridgeLoopRunning do
			local settings = readBridgeSettings()
			local appActive, appReason = readAppHeartbeat()
			local applyInterval = clampNumber(settings.applyIntervalSeconds, 1, 10)
			local snapshotRequestHandled = false

			if appActive == true then
				-- Explicit requests are allowed even when background dirty
				-- watching is disabled. The VRS heartbeat still gates this so
				-- Creator does not snapshot while the user is focused in Creator.
				snapshotRequestHandled = handleSnapshotRequest(true)
			end

			if settings.autoMode == true and appActive == true then
				if not appWasActive then
					writeStatus("app_connected", "VisualRuleSystem heartbeat detected. Bridge is active.", nil)
					appWasActive = true
					ensureSceneWatchers(true)
					if snapshotRequestHandled ~= true then
						writeSnapshot(true)
					end
				end

				if settings.autoExport == true then
					flushDirtySnapshotIfReady(settings, true)
				end

				if settings.autoApply == true and applyTicks <= 0 then
					local pendingCount = readPendingCommandCount()
					if pendingCount > 0 then
						applyPendingCommands(true)
					end
					applyTicks = applyInterval
				end
			elseif appWasActive then
				disconnectSceneWatchers()
				writeStatus("app_disconnected", "VisualRuleSystem heartbeat stopped: " .. tostring(appReason), nil)
				appWasActive = false
			end

			applyTicks = math.max(0, applyTicks - loopDelaySeconds)
			wait(loopDelaySeconds)
		end
	end)
end

local function requestPermissions(addon)
	if Enums == nil or Enums.AddonPermission == nil then
		print("[Visual Bridge] Addon permission enum was not found; IO may fail.")
		return
	end

	local attempts = {
		function()
			addon:RequestPermissions({ Enums.AddonPermission.IORead, Enums.AddonPermission.IOWrite })
		end,
		function()
			addon:RequestPermissions({ "IORead", "IOWrite" })
		end,
		function()
			addon:RequestPermissions(Enums.AddonPermission.IORead)
			addon:RequestPermissions(Enums.AddonPermission.IOWrite)
		end,
		function()
			addon:RequestPermissions("IORead")
			addon:RequestPermissions("IOWrite")
		end,
	}

	for _, attempt in ipairs(attempts) do
		local ok = pcall(attempt)
		if ok then
			print("[Visual Bridge] Requested IORead/IOWrite permissions.")
			return
		end
	end

	print("[Visual Bridge] IO permission request failed; tools will report IO errors if access is missing.")
end

if Addons == nil then
	print("[Visual Bridge] Addons API is not available. Install this ServerScript as a Creator addon.")
	return
end

local addon = Addons:Register(ADDON_ID)
addon.AddonName = ADDON_NAME
requestPermissions(addon)

local snapshotTool = addon:CreateToolItem(SNAPSHOT_TOOL_LABEL)
rememberConnection(snapshotTool.Pressed:Connect(function()
	writeSnapshot(false)
end))

local applyTool = addon:CreateToolItem(APPLY_TOOL_LABEL)
rememberConnection(applyTool.Pressed:Connect(function()
	applyPendingCommands(false)
end))

local statusTool = addon:CreateToolItem(STATUS_TOOL_LABEL)
rememberConnection(statusTool.Pressed:Connect(function()
	printBridgeStatus()
end))

local autoTool = addon:CreateToolItem(AUTO_TOOL_LABEL)
rememberConnection(autoTool.Pressed:Connect(function()
	toggleAutoBridge()
end))

rememberConnection(addon.CleanupReceived:Connect(function()
	cleanupConnections()
end))

ensureBridgeSettingsFile()
startAutoBridgeLoop()
writeStatus("addon_loaded", ADDON_NAME .. " " .. BRIDGE_RUNTIME_VERSION .. " loaded. Waiting for VisualRuleSystem heartbeat.", nil)
