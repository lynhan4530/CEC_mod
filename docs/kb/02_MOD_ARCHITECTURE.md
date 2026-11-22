# 02 — `CecMod` Code Architecture (`v0.4.1`)

All C# source lives in [CecMod/](file:///G:/Project/CEC_mod/CecMod) targeting `.NET Standard 2.1` (`netstandard2.1`), Unity 2022.3 Mono, BepInEx 5, and Harmony 2.

## 1. File Inventory

| File | Responsibility |
|---|---|
| [CecMod.csproj](file:///G:/Project/CEC_mod/CecMod/CecMod.csproj) | Build config referencing `AnchorChain`, `BepInEx`, `0Harmony`, `Seapower-Scripts`, `UnityEngine*`, `UniRx` (`<Private>false</Private>`). Zero compile-time dependency on `ExtendedGuidance.dll`. |
| [Plugin.cs](file:///G:/Project/CEC_mod/CecMod/Plugin.cs) | `CecPlugin` (`com.cecmod.seapower`, v`0.4.1`). `InitializeCore` is idempotent: patches assembly via Harmony, initializes `EgBridge`, spawns persistent `CecDebugHost` (`CecDebugOverlay`). |
| [AnchorChainEntry.cs](file:///G:/Project/CEC_mod/CecMod/AnchorChainEntry.cs) | `AnchorChainEntry : IAnchorChainMod` (`[ACPlugin]`). Checks `IsEnabledInModMenu()` and attaches `CecPlugin` to `Chainloader.ManagerObject`. |
| [CecDataTables.g.cs](file:///G:/Project/CEC_mod/CecMod/CecDataTables.g.cs) | Auto-generated static lookup tables (1,240 lines) from `tools/generate_cec_tables.py`. Contains enums, profile classes, `Platforms` (478: 344 ships, 134 aircraft including `usn_fa-18e_blk3` & `usn_fa-18f_blk3c`), `Radars` (287), `Missiles` (232) + P2027 aliases, and `DatalinkMatrix` (10x10). |
| [CecCapability.cs](file:///G:/Project/CEC_mod/CecMod/CecCapability.cs) | Core qualification, datalink resolution, horizon/EMCON physics, launch-time radar injection, ammo illuminator sync, handoff search, and UI formatters. |
| [EgBridge.cs](file:///G:/Project/CEC_mod/CecMod/EgBridge.cs) | Zero-dependency reflection bridge + 13 runtime Harmony hooks into `ExtendedGuidance.dll` (`euromod.extended-guidance`), including `ExtendedGuidance.Runtime.Illuminator` scoping/EMCON release. |
| [CecDebugOverlay.cs](file:///G:/Project/CEC_mod/CecMod/CecDebugOverlay.cs) | F10 Developer Menu (`DM`) integration and live diagnostic IMGUI overlay (`Ctrl+F10`, `DM.MenuWindow` `"CEC Datalink"`, `DM.DoToolbarWindow` `"CEC"`). |
| [LaunchTimeCecPatch.cs](file:///G:/Project/CEC_mod/CecMod/Patches/LaunchTimeCecPatch.cs) | Replaces `CheckExternalRadarGuidance`; enforces EMCON protection across `SensorSystem.Enable`, `ObjectBase.HandleEngageTasks`, `WeaponSystemLauncher.OnUpdate`, `activatePreLaunchGuidance`, and `CheckTargetIsDetectableByFCR`; rebinds `ActivateNonLaserGuidanceSystem` on launch and syncs EG illuminators/logs for direct remote binds. |
| [RemoteEngagementPatch.cs](file:///G:/Project/CEC_mod/CecMod/Patches/RemoteEngagementPatch.cs) | `AttackingStateUpdateDisplayedRangePatch` (hover telemetry) + `RemoteEngagementPatch` (`CanGuideAmmo` including `ActiveEgIlluminatorMissile` gating, pre-launch visibility/FCR/channel/aiming postfixes + EMCON-protected `HasFreeGuidingSensors` / `hasFreeWeaponChannel` hooks). |
| [HandoffPatch.cs](file:///G:/Project/CEC_mod/CecMod/Patches/HandoffPatch.cs) | `_launchPlatform` aliasing in `Missile.OnFixedUpdate` & `Missile.OnUpdateEveryFrame`, `LocalTerminalOnly` terminal pitbull respect, silent self-handoff channel repair, mid-flight rescue & Forward Pass terminal promotion, and `WeaponBase.IsUnguidableOrphan` protection. |
| [DatalinkUiPatch.cs](file:///G:/Project/CEC_mod/CecMod/Patches/DatalinkUiPatch.cs) | Bottom-bar missile status UI (`ObjectMissileControlViewModel.GetState` & `ObjectState` setter) showing active guider/illuminator and tier/protocol. |

## 2. `CecCapability.cs` Architecture (7 Sections)

1. **Profile Resolvers & Lookups (`O(1)`)**:
   - `NormalizeRadarKey` strips trailing `" (P2027)"` suffix.
   - `GetPlatformProfile`, `GetRadarProfile`, `GetMissileProfile`, `GetSarhFamily`, `GetRequiredSarhFamily`, `GetSensorNetwork`, `GetPlatformNetwork`. All default to `DefaultTier3` when missing.
2. **Pairwise Interaction & Flight Isolation**:
   - `ResolveBestDatalinkPair` / `EvaluateInteraction` — computes best `InteractionTier` across bitmask flags, handles `Tier1_IntraFlightOnly` (live `Formation` / `ActiveFormationLeader` checks in `IsSameFlightGroup`), cross-flight stealth gateway bridging & `GatewayRequired` (`TaskforceHasActiveGateway`), and non-stealth Tier 1 Link 16 upgrades (excluding `Madl`/`Ifdl` platforms).
3. **Range & Horizon Physics**:
   - `ArePlatformsInDatalinkReach`, `DatalinkMastHeight`, `RadarCanSee` (range, `RadarCalculator.getRadarHorizon`, and FOV enforcement for `DirectedSearch` / non-AWACS aircraft radars), `RadarCanReachMissile`, `IsPlatformInAirRadarEmcon`, `IsPlatformInSurfaceRadarEmcon`, `HasLocalRadarSight` (skipping `IsSyntheticDatalinkSensor` and `IsNonAirSearchHelperSensor`).
4. **Strict 4-Tier Gating Engine**:
   - `CanPerformLaunchOnRemote` -> dispatches to `HasTrackingCooperatorForRemoteLaunch` (Tier 1 ARH/IR/ARM/Command) or `HasForwardPassIlluminatorCoverage` (SARH/TVM).
   - `CanRadarIlluminateSarh` strictly filters for physical SARH targeting/CW illuminators matching `RequiredSarhFamily` (excluding AWACS, synthetic datalinks, and `DirectedSearch` radars such as `SPY-1D(V) (P2027)`), while `ConsortHasIlluminatorForTarget` / `FindConsortIlluminatorForTarget` skip consorts in air-radar EMCON.
   - `CanPerformMidCourseHandoff` & `FindHandoffRadar(missile, requireTerminalIlluminator)` — picks the qualifying cooperator radar closest to `target` (enforcing `CanRadarIlluminateSarh` when terminal illumination is required).
5. **Launch-Time Pool Injection & Sync**:
   - `RefreshConsortRadars(WeaponSystem ws)` & `EnsureConsortRadarsFresh(WeaponSystem ws, AmmunitionParameters ap)` — injects active (non-EMCON) Tier 1 consort radars (plus deferred idle `Targeting` CW illuminators, excluding `IsNonAirSearchHelperSensor` such as `GPS_Receiver` / `GPS_Link` / `GuidancePod`) into `ws._vwp._associatedSensors` and `ws._vwp._externalGuidingSystems`, tracked via `ConditionalWeakTable` sets (`_injectedSensors`, `_injectedExternalNames`) so dead, inoperable, EMCON, downgraded, or out-of-reach sensors are pruned cleanly; uses `0.25s` fast retry until `HasActiveRemoteAssociatedRadar(ws)` becomes true and `5.0s` steady-state cadence thereafter.
   - `SyncConsortIlluminatorsToAmmo(WeaponSystem ws, AmmunitionParameters ap)` — purges injected consort sensors if `ap` is Tier 3/0; for SARH, strips incompatible SARH families and syncs matching consort targeting/CW radars into `ap._associatedSensors`; and for all Tier 1/2 missiles with non-empty `ap._compatibleGuidanceSystems`, adds compatible injected remote radar `_systemName` entries (passing `CanSensorGuideAmmo`) so `SensorSystem.CanGuideAmmo(ap)` succeeds.
   - `EnsureMidCourseChannels(r)` — expands `_targetChannels` / `_weaponChannels` to at least `32` (`< 32`) on AWACS or MFR radars (overriding vanilla `SensorRecord`'s default of `1`).
   - `ResolveWeaponAmmunitionParameters(WeaponSystem ws)` — resolves `_ammoForEngage?._ap`, hardpoint `_weapons[i]?._ap`, or `_ammunitionOnWeapon?._ap`.
6. **Situational Awareness & Sensor Filtering**:
   - `CanSensorGuideAmmo`, `HasCooperativeAirTrack`, `HasCooperativeTrack`, `HasRemoteFcrCoverage`, `CooperatorSeesTarget` (skips EMCON cooperators for air targets, `IsSyntheticDatalinkSensor`, and `IsNonAirSearchHelperSensor`; checks Search/DirectedSearch/Targeting radars, organic `_spottedObjects`/`_detectedObjects`, standard `GetRange`, and unjammed `SensorRangeOptions` fallback to work around vanilla's self-screen jammer / HOJ noise bug). Also `ActivateNonLaserGuidanceSystem_Postfix` skips `IsNonAirSearchHelperSensor` on air-target launches.
7. **Hover Telemetry & UI Formatters**:
   - `FindBestTrackingCooperator` (ranks by `GetEffectiveInteractionRank`, then Tier 1 capability, then closest distance), `FindConsortIlluminatorForTarget`, `GetHoverTelemetryTag`, `FormatMissileDatalinkUiString`.

## 3. Harmony Patch Flow Across the Weapon Lifecycle

```
Pre-Launch Targeting & Hover:
  AttackingState.UpdateDisplayedRange [Postfix] -> Hover tag if !HasLocalRadarSight
  ObjectBase.GetLocalAirTargetVisibilityRange / AnySensorSeesAirTargetAt [Postfix]
  ObjectBase.GetMaxRangeForFCR / GetSensorRange [Postfix] -> RefreshAndSyncConsortRadars + CanPerformLaunchOnRemote
  WeaponSystem.CanSearchSensorsSeeTarget / IsVisionRequirementMet / FCRDetectionRange / isAimedAtObject [Postfix]
  WeaponSystem.CheckTargetIsDetectableByFCR [Prefix] -> Checks radar.CanGuideAmmo + CanPerformLaunchOnRemote

Pre-Launch Alignment & Rail Release:
  ObjectBase.HandleEngageTasks / WeaponSystemLauncher.OnUpdate [Prefix Priority.First / Finalizer Priority.Last]
    -> Sets [ThreadStatic] _emconProtectedUnit so SensorSystem.Enable(..., turnOffAfterEngage: true) is blocked during EMCON CEC/Forward Pass launches
  WeaponSystem.HasFreeGuidingSensors / hasFreeWeaponChannel [Prefix Priority.First / Postfix / Finalizer]
    -> EnsureConsortRadarsFresh + CapturePreLaunchEmconState/RestorePreLaunchEmconState (prevents _autoEnableSensor from breaking EMCON) + CanPerformLaunchOnRemote fallback
  WeaponSystem.HasFreeWeaponChannels [Postfix Priority default] -> CanPerformLaunchOnRemote fallback
  WeaponSystem.CheckExternalRadarGuidance [Prefix] -> EnsureConsortRadarsFresh (0.25s missing / 5.0s steady-state; skips vanilla Clear)
  WeaponSystemLauncher/Hardpoint.activatePreLaunchGuidance [Prefix Priority.First / Postfix / Finalizer]
    -> Runs ahead of ExtendedGuidance.Hooks.RemotePrelaunch (Priority.High)
    -> EnsureConsortRadarsFresh
    -> Zeroes _horizonRange when CanPerformLaunchOnRemote; cancels queued asynchronous local radar enables (_wasSetOn / _sensorsToTurnOff) and restores Emcon.Value; calls EnsureRemotePreLaunchTargetAssigned (skipping local/EMCON sensors)
  WeaponSystem.isAimedAtObject [Postfix Priority.First] -> Ensures remote consort target alignment & EMCON clearance
  WeaponBase.ActivateNonLaserGuidanceSystem [Postfix]
    -> Rebinds OTH/EMCON/blind shots (never retaining local/EMCON DatalinkCarriers when shooter is in air-radar EMCON) to best remote radar, or syncs EG illuminator & logs direct remote binds (and preserves non-EMCON EG DatalinkCarrier if healthy)

In-Flight Guidance & Handoff:
  Missile.OnFixedUpdate [Prefix Priority.First / Postfix+Finalizer Priority.Last]
    -> Sets IsCheckingRadioConnection = true; syncs consort illuminators; batch-aliases _launchPlatform to active guider
  ExtendedGuidance.Runtime.Illuminator [Prefix / Finalizer] + SensorSystem.CanGuideAmmo [Postfix]
    -> Scopes ActiveEgIlluminatorMissile, releases any Tier 1/2 EMCON terminal illuminator, and rejects non-SARH/DirectedSearch radars, EMCON hosts, and non-qualifying/out-of-reach consorts
  Missile.OnUpdateEveryFrame [Prefix/Postfix/Finalizer]
    -> Aliases _launchPlatform during CheckRadioConnection
    -> Skips mid-course handoff for LocalTerminalOnly missiles already in FlightStage.TerminalApproach (pitbull)
    -> Repairs same-radar channel/link state quietly without logging X -> X self-handoffs
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
