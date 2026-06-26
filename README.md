# VisualRuleSystem Polytoria C#

VisualRuleSystem Polytoria C# is an unofficial fan/hobby visual scripting tool
for Polytoria 2.0. It provides a human-readable node graph editor that exports
Luau scripts and coordinates with Polytoria Creator through a small bridge
workflow.

This project is not affiliated with, endorsed by, sponsored by, or maintained by
Polytoria.

The maintainer's current intent is personal, hobby, and non-commercial use.
That statement is only context about the project origin; it does not add any
non-commercial restriction to the license. The rights and obligations for this
source tree are governed by the MPL-2.0 license and by the third-party notices
listed below.

This project was created with AI assistance. AI-assisted generation does not
change the license terms applied to files in this repository or to third-party
materials. During AI-assisted work, the maintainer used available controls to
disable AI training/learning on the project content where the tools provided
such settings.

## Purpose and Expectations

VRS is aimed at artists, level designers, and non-scripters who want a more
human-readable way to create simple gameplay logic for Polytoria 2.0. It can
help people script through a node graph, but it has limitations and is not a
replacement for an experienced human scripter.

It can also help text-first scripters inspect generated Luau patterns that work
with Polytoria's APIs and project workflow, even when the visual graph becomes
large, verbose, or messy.

In theory, VRS can be used to build complex gameplay logic. In practice, that
is not the main purpose that defines the project: VRS is primarily about making
common logic easier to express, inspect, test, and iterate through a visual
workflow.

The maintainer personally relies on visual programming workflows and does not
currently write code directly. Even when experienced developers describe text
programming as easy, it is not equally accessible to everyone. This project
exists from that perspective: visual scripting can make logic creation more
approachable without pretending that it removes the value of skilled
programmers.

The maintainer cannot guarantee that every feature or generated script works
perfectly in every project. The goal is to test the tool, ask other users to
test it, document known problems, and fix issues progressively as they are
found.

## Status

This is a working prototype, not a finished product. The current editor includes:

- An Avalonia/.NET 10 desktop app.
- A rule graph model with triggers, checks, actions, reroutes, groups, and
  graph metadata.
- A data-driven node catalog under `data/catalog/`.
- Searchable node palettes and parameter/value-source pickers.
- Readable Luau export with embedded VRS graph metadata.
- Project file deploy into `scripts/VRS/<kind>/`.
- Creator bridge commands for linking saved scripts, refreshing project state,
  and ensuring VRS helper instances such as input `NetworkEvent` objects.
- A VRS Input Manager flow for `input.json` presets plus Hidden runtime events.
- Regression tests for catalog, JSON, validation, export, bridge file helpers,
  and app ViewModels.

## Requirements

- .NET SDK 10 preview compatible with this solution.
- Windows for the current launcher and Polytoria Creator workflow.
- Polytoria Creator 2.0 for real project/bridge integration.

## Commands

```powershell
dotnet build VisualRuleSystem.Polytoria.slnx
dotnet test VisualRuleSystem.Polytoria.slnx
dotnet run --project src\Vrs.App\Vrs.App.csproj
```

Full app, launcher, and Creator addon build steps are documented in
[`BUILD.md`](BUILD.md).

The local root `VisualRuleSystem.exe` launcher is a developer convenience and is
ignored by Git. Source users should build or run from the solution/project.

## Fresh Clone Setup

A fresh clone does not include personal project bindings, generated graph state,
launcher binaries, published runtimes, logs, or bridge runtime files. That is
intentional; those files are local machine state.

To run from source:

```powershell
dotnet restore VisualRuleSystem.Polytoria.slnx
dotnet run --project src\Vrs.App\Vrs.App.csproj
```

To create the local double-click launcher:

```powershell
.\tools\Build-Launcher.ps1
```

This generates `VisualRuleSystem.exe` at the repository root. The file is
ignored by Git because every user can rebuild it locally, and public binary
downloads should be attached to GitHub Releases instead of committed to source.

To bind the app to a local Polytoria project, copy the example config and edit
the path:

```powershell
Copy-Item active-polytoria-project.example.json active-polytoria-project.json
notepad active-polytoria-project.json
```

`active-polytoria-project.json` is ignored because it contains a user-specific
absolute path.

## Creator Addon Setup

The Creator bridge addon is versioned with the repository because the VRS app is
not fully useful without it:

```text
addons/visual-programming-bridge/
  README.md
  src/visual_programming_bridge.server.luau
  package/VisualProgrammingBridge.ptaddon
```

Install `addons/visual-programming-bridge/package/VisualProgrammingBridge.ptaddon`
into the Polytoria Creator addon folder, then reopen Creator. The addon creates
runtime bridge files inside the active Polytoria project; those generated
`bridge/*.json` files are local state and are intentionally not committed.

## Project Layout

- `src/Vrs.Core/` - graph model, catalog loading, validation, Luau export,
  project input helpers, and bridge file helpers.
- `src/Vrs.App/` - Avalonia UI and ViewModels.
- `tests/Vrs.Tests/` - regression tests and smoke behavior.
- `data/catalog/` - built-in node packages.
- `addons/visual-programming-bridge/` - Polytoria Creator bridge addon source
  and installable `.ptaddon` package.
- `tools/` - launcher, smoke test, and maintenance utilities.
- `active-polytoria-project.example.json` - example local project binding.

Generated output, local project bindings, launcher binaries, publish folders,
logs, and temporary third-party source snapshots are ignored by `.gitignore`.

## Polytoria API Boundary

VRS targets the public Polytoria 2.0 scripting API and project workflow. The
implementation is based on public documentation/API behavior and compatibility
research, not on bundling Polytoria itself.

Polytoria's own source repository is MPL-2.0 unless otherwise noted, while
Polytoria brand assets, logos, names, and trademarks are not licensed for reuse
by that source license. If a future VRS file copies or modifies Polytoria source
code, keep the upstream MPL notice on that file and record the provenance in
`THIRD_PARTY_NOTICES.md`.

## Creator Bridge Boundary

VRS writes project files and bridge command files. Polytoria Creator remains the
authority for applying those commands to a live Creator project. This keeps the
workflow explicit:

- `Deploy File` writes or updates the saved Luau file under `scripts/VRS/`.
- `Deploy Saved Script Instance Here` links or updates a Creator script instance
  from that saved file.
- The bridge addon applies instance/project changes inside Creator.

## License

The main VisualRuleSystem Polytoria C# source tree is licensed under the Mozilla
Public License 2.0. See `LICENSE`.

Known third-party notices and practical publication reminders are recorded in
`THIRD_PARTY_NOTICES.md`.

This repository does not grant rights to Polytoria trademarks or brand assets.
