# Addons

This folder contains source and installable packages for Creator addons required
by VisualRuleSystem.

Only distributable addon artifacts belong here. Do not commit project-local
runtime folders such as `bridge/`, live snapshots, pending commands, logs, or
user-specific Polytoria project bindings.

Current addon:

- `visual-programming-bridge/` - Polytoria Creator bridge used by VRS to export
  scene snapshots, apply queued commands, link saved VRS scripts, and ensure VRS
  runtime helper objects such as input `NetworkEvent` instances.
