# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A mod for the game *Sea Power: Naval Combat in the Missile Age* that adds true ship-to-ship
**Cooperative Engagement Capability (CEC)**: letting one vessel guide a SARH/command-guided
missile (e.g. SM-2) fired by another datalink-capable vessel in the same task force.

The mod ships as a **BepInEx plugin using Harmony** to patch the game's compiled assemblies at
runtime. **No mod plugin code has been written yet** — this repo currently holds the design, a
reverse-engineering knowledge base, and the tooling/reference source needed to write it.

## Repository layout

- `CEC_mod.md` — the design spec. Defines the three Harmony patches that make up the mod
  (external-guidance search, task-force-wide radar mapping, UI datalink display) with target
  method names and patch types. **Start here before writing any patch.**
- `knowledge.md` — reverse-engineering knowledge base of `Seapower-Scripts.dll`: class index,
  guidance/channel-allocation logic, external-guidance flow, missile flight/LOS, and the UI
  viewmodel. Cites specific methods and line numbers in the decompiled source.
- `scratch/*.cs` — decompiled **reference** game source (namespaces `SeaPower` and
  `SeapowerUI.ViewModels`). Read-only; these are the bodies of the methods being patched, not
  compilable project code. The line numbers cited in `knowledge.md` refer to these files.
- `scratch/Decompiler/` — a .NET 8 console tool (uses `ICSharpCode.Decompiler`) used to produce
  the files in `scratch/` from the game's DLL.

Note: `knowledge.md` links to an older `g:\Project\SeaPowerDataExtraction\scratch\` path; the live
copies of those `.cs` files are in this repo's `scratch/`.

## The Decompiler tool

Use it to inspect or refresh decompiled game code (the game's main assembly is
`Seapower-Scripts.dll`, found in the game's `..._Data/Managed/` install directory).

```bash
cd scratch/Decompiler
dotnet build
# list types (optionally filtered by substring)
dotnet run -- list   <path-to>/Seapower-Scripts.dll [pattern]
# find which types contain a given source substring
dotnet run -- search <path-to>/Seapower-Scripts.dll "TryToAddWeapon"
# decompile one type to a file
dotnet run -- decompile <path-to>/Seapower-Scripts.dll SeaPower.WeaponSystem ../WeaponSystem.cs
```

## Architecture notes for writing the mod

The patch targets and the in-game logic they hook are documented in `CEC_mod.md` and `knowledge.md`;
the key relationships to understand before editing:

- **Guidance association at launch**: `WeaponBase.activateGuidingSystem()` scans
  `_weaponSystem._vwp._associatedSensors` for a compatible `SensorSystemRadar` and consumes a
  target/weapon channel on it. The CEC approach is to inject a *remote* ship's radar into
  `_associatedSensors` so the existing logic picks it up.
- **External guidance is the seam**: the game already supports off-platform guidance for land SAMs
  via `WeaponSystem.CheckExternalRadarGuidance()` → `SearchForExternalGuidance()`, but it is gated
  on `_externalGuidingSystems.Count > 0` and a ~1 nmi search radius. Patches 1 & 2 widen this to a
  task-force-wide scan (`_baseObject._taskforce._taskforceVessels`, ~50 nmi) for radars whose name
  matches the ammo's `_compatibleGuidanceSystems`.
- **Guidance math is position-relative and needs no change**: `Missile.OnUpdateEveryFrame()` already
  computes LOS raycasts and radar horizon from `_guidingRadarSystem._mountObject.transform.position`,
  so once a remote radar is assigned, CEC flight works "for free" using the guiding ship's position.
- **UI**: `ObjectMissileControlViewModel.{GoodConnection,BadConnection,LostConnection}(Missile)`
  build the datalink HUD string from `m._launchPlatform.getName()`. Patch 3 postfixes these to show
  `launcher -> guiding ship` when `m._guidingRadarSystem._baseObject` differs from `m._launchPlatform`.

When the patch logic depends on a game field/method, verify it against the decompiled source in
`scratch/` (or re-decompile) rather than trusting names — these are reverse-engineered internals
that change between game versions.

## Game data layout (this machine — discovered, not hardcoded)

- Game: `D:\SteamLibrary\steamapps\common\Sea Power`
- Base data: `…\Sea Power_Data\StreamingAssets\original` (ammunition 386, vessels 448,
  systems 5: weapons/sensors/modules/cargo/wip_cargo, templates 2, ships 0)
- User overrides: `…\StreamingAssets\user`
- Workshop mods: `D:\SteamLibrary\steamapps\workshop\content\1286220\<modId>\` (each mirrors the
  StreamingAssets layout)
- Mod load order: `C:\Users\<user>\AppData\LocalLow\Triassic Games\Sea Power\usersettings.ini`,
  `[LoadOrder]` section (discovered from the OS home dir).
- `seapower-parser.config.json`, `presets.json`, `presets.warnings.log` are gitignored
  (machine-local / generated).
