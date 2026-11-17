# 02 — `CecMod` Code Architecture (`v0.4.1`)

All C# source lives in [CecMod/](file:///G:/Project/CEC_mod/CecMod) targeting `.NET Standard 2.1` (`netstandard2.1`), Unity 2022.3 Mono, BepInEx 5, and Harmony 2.

## 1. File Inventory

| File | Responsibility |
|---|---|
| [CecMod.csproj](file:///G:/Project/CEC_mod/CecMod/CecMod.csproj) | Build config referencing `AnchorChain`, `BepInEx`, `0Harmony`, `Seapower-Scripts`, `UnityEngine*`, `UniRx` (`<Private>false</Private>`). Zero compile-time dependency on `ExtendedGuidance.dll`. |
| [Plugin.cs](file:///G:/Project/CEC_mod/CecMod/Plugin.cs) | `CecPlugin` (`com.cecmod.seapower`, v`0.4.1`). `InitializeCore` is idempotent: patches assembly via Harmony, initializes `EgBridge`, spawns persistent `CecDebugHost` (`CecDebugOverlay`). |
| [AnchorChainEntry.cs](file:///G:/Project/CEC_mod/CecMod/AnchorChainEntry.cs) | `AnchorChainEntry : IAnchorChainMod` (`[ACPlugin]`). Checks `IsEnabledInModMenu()` and attaches `CecPlugin` to `Chainloader.ManagerObject`. |
| [CecDataTables.g.cs](file:///G:/Project/CEC_mod/CecMod/CecDataTables.g.cs) | Auto-generated static lookup tables (1,237 lines) from `tools/generate_cec_tables.py`. Contains enums, profile classes, `Platforms` (476), `Radars` (287), `Missiles` (232) + P2027 aliases, and `DatalinkMatrix` (10x10). |
| [CecCapability.cs](file:///G:/Project/CEC_mod/CecMod/CecCapability.cs) | Core qualification, datalink resolution, horizon/EMCON physics, launch-time radar injection, ammo illuminator sync, handoff search, and UI formatters (1,799 lines). |
| [EgBridge.cs](file:///G:/Project/CEC_mod/CecMod/EgBridge.cs) | Zero-dependency reflection bridge + 8 runtime Harmony hooks into `ExtendedGuidance.dll` (`euromod.extended-guidance`). |
| [CecDebugOverlay.cs](file:///G:/Project/CEC_mod/CecMod/CecDebugOverlay.cs) | F10 Developer Menu (`DM`) integration and live diagnostic IMGUI overlay (`Ctrl+F10`, `DM.MenuWindow` `"CEC Datalink"`, `DM.DoToolbarWindow` `"CEC"`). |
| [LaunchTimeCecPatch.cs](file:///G:/Project/CEC_mod/CecMod/Patches/LaunchTimeCecPatch.cs) | Replaces `CheckExternalRadarGuidance`, guards pre-launch OTH/EMCON (`activatePreLaunchGuidance`, `CheckTargetIsDetectableByFCR`), rebinds `ActivateNonLaserGuidanceSystem` on launch. |
| [RemoteEngagementPatch.cs](file:///G:/Project/CEC_mod/CecMod/Patches/RemoteEngagementPatch.cs) | `AttackingStateUpdateDisplayedRangePatch` (hover telemetry) + `RemoteEngagementPatch` (`CanGuideAmmo`, pre-launch visibility/FCR/aiming postfixes). |
| [HandoffPatch.cs](file:///G:/Project/CEC_mod/CecMod/Patches/HandoffPatch.cs) | `_launchPlatform` aliasing in `Missile.OnFixedUpdate` & `Missile.OnUpdateEveryFrame`, mid-flight rescue & Forward Pass terminal promotion, and `WeaponBase.IsUnguidableOrphan` protection. |
| [DatalinkUiPatch.cs](file:///G:/Project/CEC_mod/CecMod/Patches/DatalinkUiPatch.cs) | Bottom-bar missile status UI (`ObjectMissileControlViewModel.GetState` & `ObjectState` setter) showing active guider/illuminator and tier/protocol. |

## 2. `CecCapability.cs` Architecture (7 Sections)

1. **Profile Resolvers & Lookups (`O(1)`)**:
   - `NormalizeRadarKey` strips trailing `" (P2027)"` suffix.
   - `GetPlatformProfile`, `GetRadarProfile`, `GetMissileProfile`, `GetSarhFamily`, `GetRequiredSarhFamily`, `GetSensorNetwork`, `GetPlatformNetwork`. All default to `DefaultTier3` when missing.
2. **Pairwise Interaction & Flight Isolation**:
   - `ResolveBestDatalinkPair` / `EvaluateInteraction` — computes best `InteractionTier` across bitmask flags, handles `Tier1_IntraFlightOnly` (`_flightGroupLeaders` `ConditionalWeakTable`), `GatewayRequired` (`TaskforceHasActiveGateway`), and Tier 1 Link 16 upgrades.
3. **Range & Horizon Physics**:
   - `ArePlatformsInDatalinkReach`, `DatalinkMastHeight`, `RadarCanSee` (range, `RadarCalculator.getRadarHorizon`, and FOV enforcement for `DirectedSearch` / non-AWACS aircraft radars), `RadarCanReachMissile`, `IsPlatformInAirRadarEmcon`, `IsPlatformInSurfaceRadarEmcon`, `HasLocalRadarSight`.
4. **Strict 4-Tier Gating Engine**:
   - `CanPerformLaunchOnRemote` -> dispatches to `HasTrackingCooperatorForRemoteLaunch` (Tier 1 ARH/IR/ARM/Command) or `HasForwardPassIlluminatorCoverage` (SARH/TVM).
   - `CanPerformMidCourseHandoff` & `FindHandoffRadar(missile, requireTerminalIlluminator)` — picks the qualifying cooperator radar closest to `target`.
5. **Launch-Time Pool Injection & Sync**:
   - `RefreshConsortRadars(WeaponSystem ws)` — injects Tier 1 consort radars (plus deferred idle `Targeting` CW illuminators) into `ws._vwp._associatedSensors` and `ws._vwp._externalGuidingSystems`, tracked via `ConditionalWeakTable` sets (`_injectedSensors`, `_injectedExternalNames`) so dead/inoperable sensors are pruned cleanly.
   - `SyncConsortIlluminatorsToAmmo(WeaponSystem ws, AmmunitionParameters ap)` — purges injected consort sensors if `ap` is Tier 3/0; for SARH, strips incompatible SARH families and syncs matching consort targeting/CW radars into `ap._associatedSensors` and `ap._compatibleGuidanceSystems` (tracked per `ap` in `_injectedAmmoSensors`) so `ExtendedGuidance.Runtime.Illuminator` can reserve them for terminal illumination.
   - `EnsureMidCourseChannels(r)` — defaults `_targetChannels` / `_weaponChannels` to `32` if `<= 0` on AWACS or MFR radars.
6. **Situational Awareness & Sensor Filtering**:
   - `CanSensorGuideAmmo`, `HasCooperativeAirTrack`, `HasCooperativeTrack`, `HasRemoteFcrCoverage`, `CooperatorSeesTarget`.
7. **Hover Telemetry & UI Formatters**:
   - `FindBestTrackingCooperator`, `FindConsortIlluminatorForTarget`, `GetHoverTelemetryTag`, `FormatMissileDatalinkUiString`.

## 3. Harmony Patch Flow Across the Weapon Lifecycle

```
Pre-Launch Targeting & Hover:
  AttackingState.UpdateDisplayedRange [Postfix] -> Hover tag if !HasLocalRadarSight
  ObjectBase.GetLocalAirTargetVisibilityRange / AnySensorSeesAirTargetAt [Postfix]
  ObjectBase.GetMaxRangeForFCR / GetSensorRange [Postfix] -> RefreshAndSyncConsortRadars + CanPerformLaunchOnRemote
  WeaponSystem.CanSearchSensorsSeeTarget / IsVisionRequirementMet / FCRDetectionRange / isAimedAtObject [Postfix]
  WeaponSystem.CheckTargetIsDetectableByFCR [Prefix] -> Checks radar.CanGuideAmmo + CanPerformLaunchOnRemote

Pre-Launch Alignment & Rail Release:
  WeaponSystem.CheckExternalRadarGuidance [Prefix] -> Throttled 5.0s RefreshConsortRadars (skips vanilla Clear)
  WeaponSystemLauncher/Hardpoint.activatePreLaunchGuidance [Prefix/Postfix]
    -> RefreshConsortRadars + SyncConsortIlluminatorsToAmmo
    -> Zeroes _horizonRange when CanPerformLaunchOnRemote; restores EMCON off-radars; calls EnsureRemotePreLaunchTargetAssigned
  WeaponBase.ActivateNonLaserGuidanceSystem [Postfix]
    -> Rebinds OTH/EMCON/blind shots to best remote radar (or preserves EG DatalinkCarrier if healthy)

In-Flight Guidance & Handoff:
  Missile.OnFixedUpdate [Prefix Priority.First / Postfix+Finalizer Priority.Last]
    -> Sets IsCheckingRadioConnection = true; syncs consort illuminators; batch-aliases _launchPlatform to active guider
  Missile.OnUpdateEveryFrame [Prefix/Postfix/Finalizer]
    -> Aliases _launchPlatform during CheckRadioConnection
    -> On link loss, blind/EMCON guider, or EG terminal promotion window -> FindHandoffRadar (1.0s cooldown)
       and synchronously enables standby CW illuminator via ForceEnableRadarSynchronous
  WeaponBase.IsUnguidableOrphan [Postfix] -> Keeps missile alive if guided by live consort/AWACS

HUD Bottom-Bar Telemetry:
  ObjectMissileControlViewModel.GetState [Prefix Priority.First] -> Maps view -> Missile in ConditionalWeakTable
  ObjectMissileControlViewModel.ObjectState.set [Prefix] -> Formats unit ID + clean name + protocol/tier
```

## 4. `CecDebugOverlay.cs`
- Forces `DM._allowDMHotkey = true` in `Awake()` and `Update()`.
- Toggles on `Ctrl + F10`, or via `"CEC Datalink"` in `DM.MenuWindow` / `"CEC"` in `DM.DoToolbarWindow`.
- Displays Shooter Tier, Datalinks, Network, Gateway flag, Air/Surface EMCON, Radar counts, Target distance/horizon/organic sight, Best Consort reach/interaction tier, Weapon gating (`CanPerformLaunchOnRemote`), and hover tag. Includes `"Log Snapshot to F10 Event Log"` and `"Toggle Ship EMCON"` buttons.
