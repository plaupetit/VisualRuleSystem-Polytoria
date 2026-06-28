# VRS Workflows

This page describes common tasks in VisualRuleSystem Polytoria.

## Create a New Script

1. Click `New Script`.
2. Choose the script kind with `Project Server`, `Project Client`, or
   `Project Module`.
3. Rename the script target from the Creator Hierarchy menu if the default name
   is not useful.
4. Add nodes on the graph canvas.
5. Click `Deploy File` to write the saved VRS Luau file.

## Add a Trigger and Action

1. Right-click the graph canvas.
2. Choose `Add Node Here`.
3. Search for a trigger, for example `start`, `touch`, or `input`.
4. Add an action node, for example `Show Message`, `Kill Player`, or
   `Set Object Visible`.
5. Connect the trigger flow output to the action flow input.
6. Select the action node and configure its parameters in the Inspector.

## Choose a Value Source

1. Select a node with a configurable parameter.
2. In the Inspector, open `Choose Value Source`.
3. Pick a compatible source such as `Triggering Player`, `Self`, `Scene Object`,
   `Variable`, or a property node.
4. Keep manual values for simple text, numbers, and booleans when that is the
   clearest option.

For player parameters that come from a trigger, prefer `Triggering Player` over
an empty manual value.

## Export Luau Preview

1. Build or edit the graph.
2. Open `Output: F11`.
3. Use the `Script Code Preview` tab to inspect generated Luau.
4. Use `Validation` to check graph problems.
5. Enable node `Debug logs` when a trigger or action needs runtime diagnosis.

## Deploy File

`Deploy File` writes the current VRS script into the Polytoria project under
`scripts/VRS/<kind>/`.

1. Confirm the script kind and script name.
2. Click `Deploy File`.
3. Check the `File:` chip for the saved path.
4. If the file is missing or empty, deploy the file again before deploying an
   instance.

This step creates or updates the saved `.luau` file. It does not create the
Creator script instance.

## Deploy Saved Script Instance Here

`Deploy Saved Script Instance Here` links the already-saved VRS file into the
Creator hierarchy.

1. Run `Deploy File` first.
2. In `Creator Hierarchy`, right-click the target object or folder.
3. Choose `Deploy Saved Script Instance Here`.
4. Use `Dry Run Saved Instance Here` if you want to queue a preview action
   without applying the normal instance deploy.

The instance deploy uses the current graph script name and kind. It does not use
the clicked item name as the script name.

## Load an Existing VRS Graph

1. Open the `File Browser`.
2. Find a generated VRS `.luau` file under `scripts/VRS/`.
3. Right-click it.
4. Choose `Load VRS Graph From File`.
5. Confirm the replacement if the current graph already contains nodes.

Only VRS-generated scripts with embedded graph metadata can be loaded. Plain
Luau files are not imported as graphs.

## Use the Input Manager

1. Click `Input Manager`.
2. Let VRS ensure common input presets such as `Jump`, `Interact`, `Sprint`,
   `Attack`, and movement axes.
3. Let the bridge ensure `NetworkEvent` objects under
   `World/Hidden/VRS/Events/User Input (NetworkEvent)/Input Manager`.
4. In a client script, use `On Input Button Down` and `Send Input Event`.
5. In a server script, use `On VRS Input Event`.

Use dropdown filters when choosing inputs so you can show presets, project
inputs, or both.

## Typical Touch Hazard

1. Create a server script.
2. Add `On Player Touched Object`.
3. Set the touched object to the hazard object in the scene.
4. Add `Kill Player`.
5. Keep the `Player` parameter on `Triggering Player`.
6. Enable `Debug logs` on the trigger while testing.
7. Click `Deploy File`.
8. Right-click the hazard object in Creator Hierarchy and choose
   `Deploy Saved Script Instance Here`.
