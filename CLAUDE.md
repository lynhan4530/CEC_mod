# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A mod for the game *Sea Power: Naval Combat in the Missile Age* that adds true ship-to-ship
**Cooperative Engagement Capability (CEC)**: letting one vessel guide a SARH/command-guided
missile (e.g. SM-2) fired by another datalink-capable vessel in the same task force.

The mod is a **BepInEx 5 plugin using Harmony**, implemented in `CecMod/` and working on this
machine. The rest of the repo is the design doc, a reverse-engineering knowledge base, and the
decompiler tooling used to study the game's assemblies.

## Build and deploy

Requires the game installed (the csproj references game DLLs directly). The game path defaults to
`D:\SteamLibrary\steamapps\common\Sea Power`; override with `-p:GameDir=...`.

```powershell
dotnet build CecMod\CecMod.csproj -c Release
# deploy: copy the DLL into the BepInEx plugins folder
Copy-Item CecMod\bin\Release\CecMod.dll "D:\SteamLibrary\steamapps\common\Sea Power\BepInEx\plugins\CecMod\"
```

The build emits MSB3277 version-conflict warnings (System.IO.Compression / System.Net.Http facades
vs. game assemblies) — these are benign and expected. There are no tests; verification is running
the game and watching the BepInEx console/log for the plugin load line and `CEC handoff:` messages.

## Repository layout

- `CecMod/` — the plugin source (netstandard2.1). `Plugin.cs` (BepInEx entry, `harmony.PatchAll()`),
  `CecCapability.cs` (shared consort-radar qualification logic), `Patches/` (three Harmony patches).
- `CEC_mod.md` — the **original design spec**. The implementation diverged from it in important
  ways (see below); treat the code as authoritative and the spec as background.
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

## How the implementation works (and where it diverged from the spec)

All consort-qualification logic lives in `CecCapability`: same task force, inside the datalink
net, an on/operable fire-control radar of matching illuminator type with a free guidance channel.
Net membership is a *ship-level* property: each ship needs an active radar with
`_rp._hasDataLink` (its "DDS terminal"). Critically, in stock game data `HasDataLink=True` only
appears on search radars (it means "shares tracks with the task force") — every `Type=Targeting`
illuminator is False, so the illuminator itself must NOT be required to have a datalink or the
mod is inert on all non-Aegis ships. The net is LOS-limited: ship-to-ship membership uses the
engine's own `RadarCalculator.getRadarHorizon()` (4/3-earth, 1 engine Y unit = 67.2 m) between
the two ships' datalink antennas, capped at 50 nmi (1 nmi ≈ 27.5595 engine units).

No game-data (ini) changes are needed: stock SAM ammo (SM-1/Terrier/Sea Sparrow = SARH
`GuidanceType=2`; SM-2/SA-N-1/SA-N-6 = `GuidanceType=0` + `MidCourseCorrection=1` RadioCommand)
already matches what the patches hook, and `CompatibleGuidanceSystems` is absent from SAM inis
(empty ⇒ no name filter; the handoff gate therefore also requires `RadarType.Targeting`/
`DirectedSearch`). Vanilla `ExternalGuidingSystems` exists only in `land_units/*.ini`.

- **Launch-time CEC** (`Patches/LaunchTimeCecPatch.cs`): prefix on
  `WeaponSystem.CheckExternalRadarGuidance()`. The spec's plan to patch
  `SearchForExternalGuidance()` was **not** used — instead `CecCapability.RefreshConsortRadars()`
  injects qualifying consort radars directly into `_vwp._associatedSensors`, so the stock
  `WeaponBase.activateGuidingSystem()` channel-allocation logic picks a consort illuminator with
  no further patching. Tracking of which sensors *we* injected uses a `ConditionalWeakTable` so a
  refresh never removes the ship's native sensors. Ships only run CEC when no local sensor is free;
  land SAMs keep vanilla behavior; scans reuse the engine's `_lastSearchForSensorTime` ~30s throttle.
- **Mid-flight handoff** (`Patches/HandoffPatch.cs`, not in the spec): postfix on
  `Missile.OnUpdateEveryFrame()`. The engine assigns `_guidingRadarSystem` once at launch and never
  replaces it; this patch detects a recoverable connection loss, waits out a randomized 3-8 s
  re-acquisition window (net latency model; the window is voided if the original link recovers),
  then finds a consort via `CecCapability.FindHandoffRadar()` and transfers radar channels
  claim-new-then-release-old (with rollback if the weapon-channel claim fails). Candidate radars
  are filtered on the ammo's `_compatibleGuidanceSystems` **and** on live geometry mirroring
  `Missile.CheckRadioConnection()`: the radar must see the *target* (illumination) and the
  *missile* (uplink / SARH rear-reference), each via min(max range, radar horizon) on y-flattened
  distance; the winner is the qualifier closest to the target, not to the launcher. Downstream
  LOS/horizon math reads off `_guidingRadarSystem`, so flight adapts for free.
- **UI datalink display** (`Patches/DatalinkUiPatch.cs`): postfixes on
  `ObjectMissileControlViewModel.{GoodConnection,BadConnection,LostConnection}` rewrite the panel
  string to `launcher -> guiding ship` when the guider differs from `_launchPlatform`. Careful:
  `GoodConnection` uses `_launchPlatform.getName()` but Bad/Lost use `_launchPlatform.Name.Value` —
  the string replacement must match the right one.

When patch logic depends on a game field/method, verify it against the decompiled source in
`scratch/` (or re-decompile) rather than trusting names — these are reverse-engineered internals
that change between game versions.

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

## Game data layout (this machine — discovered, not hardcoded)

- Game: `D:\SteamLibrary\steamapps\common\Sea Power`
- BepInEx plugins: `…\Sea Power\BepInEx\plugins\` (the mod deploys to `CecMod\` there)
- Base data: `…\Sea Power_Data\StreamingAssets\original` (ammunition 386, vessels 448,
  systems 5: weapons/sensors/modules/cargo/wip_cargo, templates 2, ships 0)
- User overrides: `…\StreamingAssets\user`
- Workshop mods: `D:\SteamLibrary\steamapps\workshop\content\1286220\<modId>\` (each mirrors the
  StreamingAssets layout)
- Mod load order: `C:\Users\<user>\AppData\LocalLow\Triassic Games\Sea Power\usersettings.ini`,
  `[LoadOrder]` section (discovered from the OS home dir).
- `seapower-parser.config.json`, `presets.json`, `presets.warnings.log` are gitignored
  (machine-local / generated).
