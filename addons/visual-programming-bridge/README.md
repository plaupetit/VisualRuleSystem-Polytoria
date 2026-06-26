# [VRS]Visual Programming Bridge

Polytoria Creator 2.0 addon for the VisualRuleSystem standalone app.

Status: smart live prototype.

## Purpose

This bridge lets Polytoria Creator remain the authority while a local visual
programming app reads a hierarchy snapshot and queues safe commands.

The bridge is intentionally file-based:

```text
Polytoria Creator addon <-> addons/visual-programming-bridge/bridge/*.json <-> VisualRuleSystem
```

It is not an injection workflow, not a hidden automation workflow, and not a
permission bypass.

## Creator Tools

Open `Tools > Addons > [VRS]Visual Programming Bridge`.

- `Export Visual Snapshot`: writes `bridge/scene-snapshot.json`.
- `Apply Visual Commands`: reads `bridge/pending-commands.json` and applies V1 commands.
- `Visual Bridge Status`: prints bridge paths and writes `bridge/status.json`.
- `Toggle Visual Auto Bridge`: toggles the optional heartbeat-aware loop.

In smart live mode, Creator only works while VisualRuleSystem writes a fresh
`app-heartbeat.json`. The addon writes one startup snapshot, then watches scene
changes and exports again only after the hierarchy becomes dirty. Manual
snapshot requests through `snapshot-request.json` remain available.

## Repository Files

```text
addons/visual-programming-bridge/
  README.md
  src/visual_programming_bridge.server.luau
  package/VisualProgrammingBridge.ptaddon
```

After installation in a Polytoria project, Creator and VRS generate local
runtime files beside the addon. These are machine/project state and should not
be committed:

```text
addons/visual-programming-bridge/bridge/scene-snapshot.json
addons/visual-programming-bridge/bridge/settings.json
addons/visual-programming-bridge/bridge/app-heartbeat.json
addons/visual-programming-bridge/bridge/app-state.json
addons/visual-programming-bridge/bridge/snapshot-request.json
addons/visual-programming-bridge/bridge/pending-commands.json
addons/visual-programming-bridge/bridge/command-results.json
addons/visual-programming-bridge/bridge/status.json
addons/visual-programming-bridge/bridge/command-history.jsonl
```

VisualRuleSystem writes `bridge/app-state.json` while it is visible. That file
is intentionally small and throttled: it contains selected hierarchy/script/node
state, validation counts, a truncated Luau preview, bridge status, and a short
live-log tail. It is for diagnosis, not for storing project data.

## Current Commands

Implemented:

- `export_hierarchy`
- `create_folder`
- `ensure_folder_path`
- `ensure_network_event`
- `create_server_script`
- `create_client_script`
- `create_module_script`
- `upsert_script`
- `deploy_vrs_script`
- `rename`
- `set_parent`
- `delete`
- `set_script_source`
- `repair_linked_script`
- `export_script_source`

Script deployment is VRS linked-file-first. The normal workflow is now:

1. Create or edit a local VRS script file in VisualRuleSystem.
2. Use `Deploy File` to store that file under the project `scripts/VRS/...`
   library.
3. Right-click a Creator hierarchy object in VisualRuleSystem.
4. Choose `Deploy Saved Script Instance Here`.

This creates a Creator script instance under the selected parent and links it to
the shared VRS file:

```text
scripts/VRS/server/VRS Name.server.luau
scripts/VRS/client/VRS Name.client.luau
scripts/VRS/module/VRS Name.module.luau
```

`deploy_vrs_script` creates or finds the Creator script, calls
`Script:LinkWithScriptFile(...)`, and only then mirrors `Source` when the current
Creator build exposes it. The default is a shared linked file: several Creator
script instances may point to the same VRS file, so editing that file affects all
linked instances. Per-object instance configuration is planned separately.

If linking fails, the script object is kept and the result includes a specific
status such as `created_but_unlinked`, `parent_not_found`, or `link_failed`.
`scripts/generated/` remains only a legacy/manual-copy fallback.

`repair_linked_script` is non-destructive: it attaches the expected VRS file to
an existing Creator script without overwriting the file. Use it when the project
file exists but the Creator snapshot reports an empty or mismatched
`LinkedScript`.

Snapshots export script link metadata (`linkedScriptPath`, `isLinkedScript`, and
`isVisualScriptName`) so VisualRuleSystem can show `Unlinked Script` warnings in
the hierarchy and selected-script panel.

Unsupported commands produce a clear result instead of silently doing nothing.

`ensure_folder_path` is idempotent. It creates missing `Folder` segments only,
returns `already_exists` for an existing path, and fails clearly if a non-folder
object blocks the requested path.

`ensure_network_event` is idempotent. It creates a `NetworkEvent` under an
already ensured parent folder, returns `already_exists` when the event is
present, and fails clearly if another object type blocks the event name.

Documented for later, but unsupported right now:

- `select`
- `create_model`
- `create_part`
- `create_bindable_event`
- `move_child`
- `set_transform`
- `set_property`
- `edit_linked_script`

## Smart Live Mode

Smart live mode is heartbeat, request, and dirty-event based.

`bridge/settings.json` controls it:

```json
{
  "format": "visual-programming-bridge-settings",
  "version": 1,
  "autoMode": true,
  "applyIntervalSeconds": 1,
  "exportIntervalSeconds": 10,
  "snapshotMode": "request",
  "autoExport": true,
  "autoApply": true
}
```

When enabled, the addon first checks that VisualRuleSystem is alive through
`app-heartbeat.json`. It then attaches guarded scene watchers to the current
Creator hierarchy. `ChildAdded`, `ChildRemoved`, delete, rename, property, and
tree-change signals are used when available. Watch callbacks only mark the
scene dirty; the full snapshot is exported once after a short debounce.

The addon still applies pending commands quickly, exports after commands, and
keeps manual buttons as the fallback.

Move commands include the expected source name, class, and old parent. If the
scene changed between the app snapshot and Creator applying the command, the
addon rejects the move instead of moving the wrong object.

## Command Example

```json
{
  "format": "visual-programming-bridge-commands",
  "version": 1,
  "commands": [
    {
      "id": "create-folder-example",
      "action": "create_folder",
      "parentPath": "Environment",
      "name": "VisualFolder",
      "dryRun": false
    }
  ]
}
```

## Safety Rules

- Live sync is request based, not full polling.
- Automatic apply only runs while the app heartbeat is alive.
- Duplicate command IDs are skipped using `command-history.jsonl`.
- Future destructive commands must require `dryRun = true` first or
  `confirm = true` before real application.
- If IO permission fails, the addon reports a visible status and stays loaded.

## Install

Copy the generated package to the Creator addon folder:

```text
%APPDATA%\PolytoriaClient\creator\addons\VisualProgrammingBridge.ptaddon
```

Then reopen Polytoria Creator.

To rebuild this package from the repository source, run from the repository
root:

```powershell
.\tools\Build-Creator-Addon.ps1
```

To rebuild and install it locally in one step:

```powershell
.\tools\Build-Creator-Addon.ps1 -Install
```

## First Manual Test

1. Install `package/VisualProgrammingBridge.ptaddon`.
2. Reopen Creator.
3. Open VisualRuleSystem.
4. Confirm `bridge/app-heartbeat.json` becomes active.
5. Confirm `bridge/scene-snapshot.json` changes after startup request.
6. Queue a folder with `Queue Create Folder`.
7. Press `Apply Visual Commands` in Creator.
8. Confirm `bridge/command-results.json` reports success.

## Current Smart Sync Limit

The scene watcher uses documented public events, but exact Creator-addon event
coverage still needs manual runtime confirmation. If a Creator action does not
trigger an event, use `Request Creator Snapshot` or the manual `Export Visual
Snapshot` button as the fallback.
