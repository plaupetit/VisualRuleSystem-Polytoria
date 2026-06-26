# Build Guide

This guide covers the desktop VRS app and the Polytoria Creator bridge addon.

## Requirements

- Windows.
- .NET SDK 10 compatible with this solution.
- Polytoria Creator 2.0 for testing the Creator bridge addon.

## Build The Desktop App

From the repository root:

```powershell
dotnet restore VisualRuleSystem.Polytoria.slnx
dotnet build VisualRuleSystem.Polytoria.slnx -nr:false -v:minimal
dotnet test VisualRuleSystem.Polytoria.slnx -nr:false -v:minimal
```

Run the app from source:

```powershell
dotnet run --project src\Vrs.App\Vrs.App.csproj
```

## Build The Local Launcher

The root `VisualRuleSystem.exe` launcher is generated locally and ignored by Git.

```powershell
.\tools\Build-Launcher.ps1
```

This produces `VisualRuleSystem.exe` at the repository root. The launcher
publishes/reuses the app runtime when it starts.

## Build The Creator Addon Package

The Creator addon source lives here:

```text
addons/visual-programming-bridge/src/visual_programming_bridge.server.luau
```

The installable addon package lives here:

```text
addons/visual-programming-bridge/package/VisualProgrammingBridge.ptaddon
```

Polytoria `.ptaddon` files are zip-format addon packages. The VRS script below
updates the package metadata and replaces the packaged server script with the
current source file:

```powershell
.\tools\Build-Creator-Addon.ps1
```

Install the rebuilt package into the local Polytoria Creator addon folder:

```powershell
.\tools\Build-Creator-Addon.ps1 -Install
```

Restart Polytoria Creator after installing so it reloads the addon metadata and
shows `[VRS]Visual Programming Bridge` under `Tools > Addons`.

## Fresh Clone Checklist

1. Build the app with `dotnet build`.
2. Install `addons/visual-programming-bridge/package/VisualProgrammingBridge.ptaddon`.
3. Copy `active-polytoria-project.example.json` to
   `active-polytoria-project.json` and point it at a local Polytoria project.
4. Run VisualRuleSystem and confirm the bridge badge reports the addon heartbeat.

Do not commit generated `bin/`, `obj/`, `publish/`, `logs/`, `bridge/`, local
project bindings, or runtime snapshots.
