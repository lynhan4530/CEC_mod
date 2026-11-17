# 03 — Sea Power Game Engine Reference (`Seapower-Scripts.dll`)

This document replaces the legacy v0.1 root `knowledge.md` and indexes the decompiled `Seapower-Scripts.dll` sources stored locally in [scratch/](file:///G:/Project/CEC_mod/scratch).

## 1. Decompiler & Decompiled Source Index

The .NET 8 `ICSharpCode.Decompiler` CLI tool is in [scratch/Decompiler/](file:///G:/Project/CEC_mod/scratch/Decompiler). Key decompiled game classes in [scratch/](file:///G:/Project/CEC_mod/scratch):

| Area | Decompiled Files |
|---|---|
| **Weapons & Guidance** | [Missile.cs](file:///G:/Project/CEC_mod/scratch/Missile.cs), [WeaponBase.cs](file:///G:/Project/CEC_mod/scratch/WeaponBase.cs), [WeaponSystem.cs](file:///G:/Project/CEC_mod/scratch/WeaponSystem.cs), [WeaponSystemLauncher.cs](file:///G:/Project/CEC_mod/scratch/WeaponSystemLauncher.cs), [AmmunitionParameters.cs](file:///G:/Project/CEC_mod/scratch/AmmunitionParameters.cs), [WeaponParameters.cs](file:///G:/Project/CEC_mod/scratch/WeaponParameters.cs), [SeekerBase.cs](file:///G:/Project/CEC_mod/scratch/SeekerBase.cs), [ActiveRadarSeeker.cs](file:///G:/Project/CEC_mod/scratch/ActiveRadarSeeker.cs), [SemiActiveRadarSeeker.cs](file:///G:/Project/CEC_mod/scratch/SemiActiveRadarSeeker.cs), [MissileSimulator.cs](file:///G:/Project/CEC_mod/scratch/MissileSimulator.cs), [FireControl.cs](file:///G:/Project/CEC_mod/scratch/FireControl.cs) |
| **Sensors & RCS** | [SensorSystem.cs](file:///G:/Project/CEC_mod/scratch/SensorSystem.cs), [SensorSystemRadar.cs](file:///G:/Project/CEC_mod/scratch/SensorSystemRadar.cs), [SensorSystemsLink.cs](file:///G:/Project/CEC_mod/scratch/SensorSystemsLink.cs), [Radar.cs](file:///G:/Project/CEC_mod/scratch/Radar.cs), [SearchRadar.cs](file:///G:/Project/CEC_mod/scratch/SearchRadar.cs), [RadarParameters.cs](file:///G:/Project/CEC_mod/scratch/RadarParameters.cs), [RadarCalculator.cs](file:///G:/Project/CEC_mod/scratch/RadarCalculator.cs), [SensorUtils.cs](file:///G:/Project/CEC_mod/scratch/SensorUtils.cs), [SensorData.cs](file:///G:/Project/CEC_mod/scratch/SensorData.cs), [RCSValues.cs](file:///G:/Project/CEC_mod/scratch/RCSValues.cs) |
| **Platforms & Taskforces** | [ObjectBase.cs](file:///G:/Project/CEC_mod/scratch/ObjectBase.cs), [Aircraft.cs](file:///G:/Project/CEC_mod/scratch/Aircraft.cs), [AircraftLoader.cs](file:///G:/Project/CEC_mod/scratch/AircraftLoader.cs), [Taskforce.cs](file:///G:/Project/CEC_mod/scratch/Taskforce.cs), [UnitFormation.cs](file:///G:/Project/CEC_mod/scratch/UnitFormation.cs) |
| **UI & Engagement Tasks** | [ObjectMissileControlViewModel.cs](file:///G:/Project/CEC_mod/scratch/ObjectMissileControlViewModel.cs), [MapUnitViewModel.cs](file:///G:/Project/CEC_mod/scratch/MapUnitViewModel.cs), [AttackingState.cs](file:///G:/Project/CEC_mod/scratch/AttackingState.cs), [EngageTask.cs](file:///G:/Project/CEC_mod/scratch/EngageTask.cs), [AttackTask.cs](file:///G:/Project/CEC_mod/scratch/AttackTask.cs) |

## 2. Engine Units, Constants & Sea Power 0.8.7 Conventions
- **World Distance Scale**: `1 nautical mile (nmi) = 27.559496f` Unity units (`1 Unity unit = 0.036285132f nmi`, as used in `CecCapability.cs` and `CecDebugOverlay.cs`).
- **Altitude Scale**: `transform.position.y * 220.47266f` gives feet in `CecDebugOverlay.cs`.
- **Simulation Clock**: Sea Power 0.8.7 uses `GameTime.missionElapsedTime` (`double`) for timers (`WeaponSystem._lastSearchForSensorTime`, `Missile._lostGuidanceTime`) — do **not** use `Time.time` (`float`).
- **Mid-Course Correction API (0.8.7+)**: Check `ap._midCourseCorrection.AllowsLauncherGuidance` and `ap._midCourseCorrection.IsDatalink` rather than raw enum values.
- **Radar Horizon**: `RadarCalculator.getRadarHorizon(heightA, heightB, visual: false, terrainAlt)` computes 4/3-Earth horizon distance.

## 3. Vanilla Guidance Lifecycle & Where `CecMod` Hooks In

1. **Channel Allocation (`SensorSystem` / `WeaponBase`)**:
   - At launch, `WeaponBase.ActivateNonLaserGuidanceSystem()` scans `_weaponSystem._vwp._associatedSensors` in list order for a compatible `SensorSystemRadar` with free channels (`HasFreeTargetChannels` / `HasFreeWeaponChannels`), binding `_guidingRadarSystem` via `TryToAddTarget` and `TryToAddWeapon`.
   - Because local sensors appear first in `_associatedSensors`, vanilla blindly binds a local radar even if it is horizon-blind or was auto-enabled from EMCON. `LaunchTimeCecPatch.ActivateNonLaserGuidanceSystem_Postfix` detects this and rebinds to the best remote CEC radar.
2. **External Radar Stub (`WeaponSystem`)**:
   - Designed only for land-based SAMs (e.g., HAWK, SA-6) within `_externalSensorSearchRadius` (`~27.559f` / `1.0 nmi`).
   - Vanilla `CheckExternalRadarGuidance()` calls `_associatedSensors.Clear()` and requires `_vwp._externalGuidingSystems.Count > 0`. `LaunchTimeCecPatch` skips vanilla via prefix (`return false`) and runs `CecCapability.RefreshConsortRadars`.
3. **In-Flight Connection & LOS (`Missile`)**:
   - `Missile.OnUpdateEveryFrame()` / `CheckRadioConnection()` verify `_guidingRadarSystem.IsOn.Value`, compute `RadarCalculator.getRadarHorizon`, and run `Physics.RaycastNonAlloc` from `_guidingRadarSystem._mountObject.transform.position` toward the target.
4. **UI Telemetry (`ObjectMissileControlViewModel`)**:
   - Displays missile datalink telemetry via `ObjectMissileControlViewModel.ObjectState` (populated by `GetState` / `GoodConnection` / `BadConnection` / `LostConnection`).

## 4. Known Vanilla Bugs Patched Around by `CecMod`

1. **Consort Hull Raycast & Alignment Bug (`Missile.CheckRadioConnection`)**:
   - Vanilla exempts `_launchPlatform`'s hull colliders and checks `_launchPlatform` alignment/range instead of `_guidingRadarSystem._baseObject`. When a consort guides a missile, its own hull blocks its raycasts unless `_launchPlatform` is temporarily aliased to the guiding unit during `OnUpdateEveryFrame` and `OnFixedUpdate` (`HandoffPatch`).
2. **Standby Illuminator Unity Frame Delay**:
   - Calling `r.Enable(allowMessage: false, turnOffAfterEngage: true)` on an idle/standby `Targeting` radar (`SPG-62`) does not flip `r.IsOn.Value = true` until the next Unity frame, causing immediate channel allocation on handoff to fail. `CecCapability.ForceEnableRadarSynchronous` sets `r.IsOn.Value = true` and `r._wasSetOn = true` synchronously.
3. **Sunken Launcher Orphan Destruction (`WeaponBase.IsUnguidableOrphan`)**:
   - Vanilla marks airborne missiles as unguidable orphans (leading to self-destruct) when `_launchPlatform` is destroyed, even if a live consort or AWACS is still guiding them. `HandoffPatch.IsUnguidableOrphan_Postfix` sets `__result = false` while a live guider exists.
4. **Synchronous `emcon` Auto-Enable**:
   - `WeaponSystemLauncher.activatePreLaunchGuidance` and `WeaponSystemHardpoint.activatePreLaunchGuidance` can auto-enable local radars (`_sensorsToTurnOff`) when `_autoEnableSensor` is true, breaking ship/aircraft EMCON even when remote CEC coverage is active. `LaunchTimeCecPatch` records off-radars before launch and shuts back off any radars vanilla tried to auto-enable.
