# VRS User Guide

This guide explains the main VisualRuleSystem Polytoria interface. VRS is a
prototype, so some workflows are intentionally explicit: save the script file
first, then link the saved script instance into Creator.

## Main Window

- `Project` badge: shows whether VRS is linked to a Polytoria project and
  whether Creator is running.
- `Addon beat` badge: shows the Creator bridge heartbeat, VRS focus state, and
  whether live bridge actions are paused.
- `Change Project`: binds VRS to another local Polytoria project.
- `Reload Catalog`: reloads node manifests from `data/catalog/`.
- `New Script`: starts a new VRS graph.
- `Project Server`: selects the current graph script kind. Use `Server`,
  `Client`, or `Module` before deploying the saved file.
- `File:` chip: shows the saved VRS script path under `scripts/VRS/`.
- `Deploy File`: writes or updates the current Luau file in the project.
- `Input Manager`: creates VRS input presets and bridge-managed input events.
- `Graph`: returns to the graph editor view.
- `Output: F11`: opens the output overlay. Press `F11` to toggle it and `Esc`
  to close it.
- `Auto`: enables automatic refresh/export behavior where supported.

## Graph Canvas

The center canvas is where rules are built.

- Right-click empty space and choose `Add Node Here` to open the node palette.
- Drag nodes to arrange them.
- Drag from a flow output pin to a flow input pin to connect execution.
- Drag from value output pins to parameter pins when advanced pins are visible.
- Select a node to edit it in the Inspector.
- Use `Ctrl+C`, `Ctrl+X`, `Ctrl+V`, `Delete`, and `Backspace` for graph
  selection editing when no text field is focused.
- Use `Create Group` or the context menu group actions to visually group nodes.

The rule/state fragment menu actions are hidden for now because that workflow is
not ready for normal editing.

## Node Palette

The node palette opens from `Add Node Here` or from connection gestures.

- Search by intent, label, keyword, parameter, or common synonyms.
- Search results are ranked; the match reason appears under the node label.
- `Compatible` mode keeps nodes filtered to the current script kind and
  connection context.
- Hover a node row for details about description, parameters, runtime, and
  compatibility.

## Inspector

The Inspector edits the selected node or selected graph item.

- `Enabled` controls whether the node exports into the script.
- `Fallback` controls what happens if a parameter cannot be resolved at runtime.
- `Debug logs` enables generated diagnostic logs for supported nodes.
- Parameters can use manual values, scene objects, trigger context values, graph
  variables, or property nodes depending on compatibility.
- Use `Choose Value Source` to switch a parameter from a manual value to a
  context value such as `Triggering Player`.

## Creator Hierarchy

The Creator Hierarchy mirrors the live Creator project snapshot from the bridge.

- Select objects to inspect or use as deployment targets.
- Right-click an object or folder to deploy the current saved script instance.
- `Set Deploy Target` stores the selected object as the intended target.
- `Rename Script Target...` changes the VRS script name used for future saved
  files.
- `Use "<item>" As Script Name` uses the selected hierarchy item name as the VRS
  script name.

The hierarchy deploy menu uses the current saved VRS file. It does not rename
or recreate the script file during instance deployment.

## File Browser

The File Browser shows project files.

- Use it to inspect generated files under `scripts/VRS/`.
- Right-click a VRS-generated `.luau` file and choose `Load VRS Graph From File`
  to restore a graph that contains VRS metadata.
- VRS only loads scripts that contain embedded `VRS_GRAPH_BEGIN` metadata.

## Output Overlay

The bottom output panel was replaced by a full-window overlay.

- Press `F11` or click `Output: F11` to open it.
- Press `Esc` to close it.
- Use the opacity slider to make the overlay more or less transparent.
- Use the mouse mode toggle to choose interactive overlay controls or
  click-through viewing.
- Tabs show `Script Code Preview`, `Validation`, and `Logs`.

## Input Manager

The Input Manager keeps common input actions available without manually building
every project object.

- It creates or preserves VRS input presets in `input.json`.
- It ensures bridge-managed `NetworkEvent` instances under
  `World/Hidden/VRS/Events/Input`.
- Client scripts can listen to input actions and send input events.
- Server scripts can listen to VRS input events and use trigger context values
  such as the triggering player and action name.
