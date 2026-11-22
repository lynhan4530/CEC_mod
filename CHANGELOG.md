# Changelog

All notable changes to **Sea Power CEC (`CecMod`)** are documented in this file.

## [Unreleased]

### Fixed
- Added Own-Ship Terminal Illumination CEC Launch-on-Remote (LOR), unlocked OTH pursuit/attack cursors, and cleaned up AWACS hover naming (`TC-07`):
  - Implemented `CecCapability.FindOwnShipTerminalIlluminatorForLor` / `HasOwnShipTerminalIlluminationForLor` for Tier 1 ships firing SARH+Datalink missiles (`RIM-66M-5`, etc.) on AWACS CEC tracks when no forward consort illuminator covers the target: uses Sea Power's analytical intercept time (`Missile.CalculateMissileInterceptTimeAnalytical`), horizontal-only velocity projection at observed `VisibleAltitude`, and terrain-aware `RadarCalculator.getRadarHorizon` across the terminal illumination window (`tIllumStart` through `tIntercept`).
  - Added the 15th `ExtendedGuidance.Runtime` hook (`EgBridge.ForecastRemoteIllumination_Postfix`) to align EG's SARH remote-illumination forecast with `CecCapability.CanPerformLaunchOnRemote`, plus organic local air/FCR horizon fallbacks (`CecCapability.GetLocalOrganicAirHorizonRange` / `GetLocalOrganicFcrHorizonRange`) in `PlanningLaunchVisibility`, `PlanningLaunchCantSee`, `PlanningMatchCantSee`, and `FcrRange` so targets below own CW horizon at intercept keep the attack cursor unlocked for pursue/engage orders.
  - Updated `LaunchTimeCecPatch`, `RemoteEngagementPatch`, `HandoffPatch`, and `EgBridge` (`_allowEmconSyntheticDatalink`, `allowOwnDatalinkLor`, `allowOwnTerminalLor`, and in-flight `ReadNativeContact_Prefix`) so own-ship synthetic `DatalinkCarrier` (`eu_AEGIS_BL5`) mid-course updates and own-ship terminal `SPG-62` illumination work in both normal and EMCON Own-Ship LOR launches.
  - Updated `CecCapability.GetHoverTelemetryTag` with Cyan `CEC LOR: {tracker} [{protocol} | Own {illum} Terminal]` and Yellow `Cued via {protocol}: {tracker} (Below own CW horizon at intercept)` hover tags, and fixed duplicate unit names (e.g. `VAW-113 E-2D VAW-113`) in `DatalinkUiPatch.FormatCleanUnitName` via substring `IndexOf`.
- Fixed misclassified modern USN Aegis CEC ship variants and `ExtendedGuidance.Runtime.RemoteLaunch` air-target bypass (`TC-02`):
  - Upgraded 18 real-world `AN/USG-2` / `AN/USG-2B` CEC USN Aegis variants in `data/cec_tables/03_Ships.csv` (`usn_ddg_burke_f3_concept`, `usn_cg_bunker_hill_vls_2014`, `usn_ddg_burke_f1_051`, `051_late`, `052_late`, `054_late`, `f2_072_late`, `f2a_079_late`, `080_late`, `084_late`, `085`, `088_laser`, `089`, `091_early`, `091`, `097_early`, `097`, `119`) from `Tier 2` (`Link 16 / Link 11`) to `Tier 1` (`USG-2 CEC / Link 16`) and regenerated `CecMod/CecDataTables.g.cs`.
  - Added the 14th `ExtendedGuidance.Runtime` hook (`EgBridge.RemoteLaunch_Postfix`) so air-target remote launches without local radar sight must pass `CecCapability.CanPerformLaunchOnRemote`.
- Enforced airborne (`Aircraft._isInFlight`) and radio-horizon/instrumented reach checks (`CecCapability.IsGatewayInRadioReach`, using `DatalinkMastHeight(..., requireRadarOn: false)` so an EMCON `E-2D` / gateway still relays radio links) in `CecCapability.TaskforceHasActiveGateway` (`TC-14`).
- Fixed stealth `MADL` / `IFDL` intra-flight isolation, helper-sensor leaks, and missing Block III Super Hornet entries (`TC-13`):
  - Excluded stealth `MADL` / `IFDL` platforms (`F-35` / `F-22`) from the `Link 16 <-> Link 16` Tier 1 upgrade in `CecCapability.ResolveBestDatalinkPair`, returned `Tier1_IntraFlightOnly` for same-flight pairs, and bridged cross-flight `MADL` / `IFDL` pairs to `Tier1_FullCec` (`(GW)`) when an active gateway is present.
  - Replaced stale `_flightGroupLeaders` caching in `CecCapability.IsSameFlightGroup` with live `Formation` / `ActiveFormationLeader` checks.
  - Added `CecCapability.IsNonAirSearchHelperSensor` (`GPS_Receiver`, `GPS_Link`, `GuidancePod`) alongside EMCON and synthetic-datalink guards across `HasLocalRadarSight`, `CooperatorSeesTarget`, `RefreshConsortRadars` (including pruning EMCON, downgraded, or out-of-reach injected sensors), and `LaunchTimeCecPatch.ActivateNonLaserGuidanceSystem_Postfix`.
  - Updated `CecCapability.FindBestTrackingCooperator` to prioritize interaction rank and Tier 1 capability before distance.
  - Added `usn_fa-18e_blk3` (`F/A-18E Block III`) and `usn_fa-18f_blk3c` (`F/A-18F Block III (CFT)`) Tier 1 gateway entries to `data/cec_tables/04_Aircraft.csv` and regenerated `CecMod/CecDataTables.g.cs`.
- Fixed SARH Forward Pass shooters breaking EMCON or keeping local guidance (`TC-06`):
  - Added `[ThreadStatic] _emconProtectedUnit` and `SensorSystem.Enable` (`Priority.First`) guards across `ObjectBase.HandleEngageTasks`, `WeaponSystemLauncher.OnUpdate`, `WeaponSystem.HasFreeGuidingSensors`, `WeaponSystem.hasFreeWeaponChannel`, and `Priority.First` `activatePreLaunchGuidance` (ahead of `ExtendedGuidance.Hooks.RemotePrelaunch`), blocking automatic `turnOffAfterEngage` local radar wake-ups on EMCON CEC shooters.
  - Updated `LaunchTimeCecPatch.ActivateNonLaserGuidanceSystem_Postfix` and `EnsureRemotePreLaunchTargetAssigned` to check `IsPlatformInAirRadarEmcon` so EMCON shooters never retain or slew always-ON local `Targeting` / `DatalinkCarrier` sensors (`eu_AEGIS_BL9`, `SPG-62`), ranking `DatalinkCarrier` ahead of terminal CW illuminators under `ExtendedGuidance` and syncing `EgBridge.SetActiveEgIlluminator` only for valid illuminators.
  - Added `CecCapability.CanRadarIlluminateSarh` (rejecting AWACS, synthetic datalinks, and `DirectedSearch` radars such as `SPY-1D(V) (P2027)`) across `ConsortHasIlluminatorForTarget`, `FindConsortIlluminatorForTarget`, `CanPerformMidCourseHandoff`, `FindHandoffRadar`, and `HandoffPatch.HasOwnShipIlluminatorForTarget`, and guarded EMCON consorts out of SARH illumination.
  - Added the 13th `ExtendedGuidance.Runtime` hook on `Illuminator` (`EgBridge.Illuminator_Prefix` / `Finalizer`) and matching `RemoteEngagementPatch.CanGuideAmmo_Postfix` scoping so `ExtendedGuidance` releases EMCON terminal illuminators and rejects non-SARH, EMCON, or out-of-reach radars.
- Fixed EMCON `WeaponSystemHardpoint` launch-hold where Tier 1 aircraft (e.g., `F/A-18F Block III` firing `AIM-174B` via `E-2D` `AN/APY-9`) stayed in Pursuit without firing:
  - Updated `CecCapability.EnsureMidCourseChannels` to expand AWACS and MFR radars with `< 32` target/weapon channels (overriding `SensorRecord`'s default of `1`), and ensured `SyncConsortIlluminatorsToAmmo` syncs `_compatibleGuidanceSystems` for all Tier 1/2 missiles with whitelists.
  - Added `CecCapability.EnsureConsortRadarsFresh` (`0.25s` retry while awaiting an active remote radar, `5.0s` steady-state refresh once injected) and hardened `CooperatorSeesTarget` against self-screen jammer / HOJ false negatives.
  - Added EMCON-protected `WeaponSystem.HasFreeGuidingSensors(Ammunition, ObjectBase, bool)`, `WeaponSystem.hasFreeWeaponChannel()`, and `WeaponSystem.HasFreeWeaponChannels(AmmunitionParameters)` hooks in `RemoteEngagementPatch`, boosted `IsAimedAtObject_Postfix` to `Priority.First`, and extended `CapturePreLaunchEmconState` to scan all local radars in `LaunchTimeCecPatch`.
  - Updated `EgBridge.Initialize` to bind eagerly via `_ = IsActive;` at startup and hooked `ExtendedGuidance.Runtime.PlanningLaunchCantSee`, `PlanningMatchCantSeeMemo`, and `PlanningMatchCantSee` directly to defeat Mono JIT inlining.
- Updated `LaunchTimeCecPatch.ActivateNonLaserGuidanceSystem_Postfix` to sync `EgBridge.SetActiveEgIlluminator` and log `[CEC] Launch-on-Remote` even when vanilla binds directly to an already-injected remote radar.
- Fixed 1 Hz `[CEC] Handoff: 'E-2D' -> 'E-2D'` log spam during terminal approach (`Bug 2`):
  - Updated `HandoffPatch.OnUpdateEveryFrame_Postfix` to return early for `LocalTerminalOnly=True` missiles (e.g., `AIM-174B`, `AIM-120D3`) once in `FlightStage.TerminalApproach` instead of fighting intentional pitbull datalink severance.
  - Updated `HandoffPatch.OnUpdateEveryFrame_Postfix` to repair same-radar channel/link state quietly without logging fake `X -> X` self-handoffs, and ensured own-ship MFR channel expansion in `HasOwnShipIlluminatorForTarget`.
- Added `CommandTracking_Postfix` in `EgBridge` (and bound `ExtendedGuidance.State.Missile`) so Tier-1/Tier-2 datalink-capable missiles with `MidCourseCorrection=1` (e.g., `AIM-174B`, `AIM-120D3`) guided by an active remote AWACS/consort radar pass `ExtendedGuidance.Runtime.CommandTracking` instead of falling into immediate `INS | DL LOST` and 1 Hz self-handoff loops.
- Updated `LaunchTimeCecPatch` (`ActivatePreLaunchGuidance_Prefix`/`Postfix` and `ActivatePreLaunchGuidanceHardpoint_Prefix`/`Postfix`) to capture `Emcon.Value` and cancel queued asynchronous `_requestEnable`/`_wasSetOn` radar activations synchronously in the postfix, preventing pre-launch EMCON radar blinks during remote engagements.
- Added `RemoteTransport`, `FcrRange`, `LauncherObservation`, `RemotePrelaunchReady`, and `CanRadarSee` postfixes to `EgBridge` (`ExtendedGuidance.Runtime` hooks) so aircraft and ships in EMCON/OTH align, pass pre-launch checks, and fire cleanly on remote AWACS/consort tracks under `ExtendedGuidance`.
- Added `WeaponSystem.isAimedAtObject` postfix (`RemoteEngagementPatch`) and `EnsureRemotePreLaunchTargetAssigned` (`LaunchTimeCecPatch`) so launchers/hardpoints align on injected remote AWACS/consort radars without breaking local EMCON.
- Updated `HandoffPatch.OnUpdateEveryFrame_Postfix` to sync `EgBridge.SetActiveEgIlluminator` and return early when `FindHandoffRadar` selects the already-guiding radar, and simplified `EgBridge.IsDatalinkStaleOrLost` to check `!LinkAvailable`.

## [0.4.1] - 2026-11-14

### Fixed
- Fixed `tools/generate_cec_tables.py` (`parse_guidance_family`) substring ordering (`"SARH"` checked before `"ARH"`) so SARH missiles in `CecDataTables.g.cs` are classified as `GuidanceFamily.SARH` instead of `ARH`, restoring Tier 1 Forward Pass (Project Mountain Top) SARH gating and hover telemetry.
- Expanded `SyncConsortIlluminatorsToAmmo` in `CecCapability.cs` to register consort illuminator `_systemName` entries into `ap._compatibleGuidanceSystems` and track per-ammo injected sensors via `_injectedAmmoSensors` (`ConditionalWeakTable`), ensuring `ExtendedGuidance` terminal illumination and Forward Pass consort selection succeed reliably.
- Added `WeaponSystemHardpoint.activatePreLaunchGuidance` prefix/postfix hooks in `LaunchTimeCecPatch.cs` for aircraft EMCON and OTH launches.

### Changed
- Upgraded `USG-2/3 CEC` and `TTNT` vs. `Link 16 (MIDS/JTIDS)` in `data/cec_tables/02_Datalink_Compatibility_Matrix.csv` from `Tier 2 - Cue Only` to `Tier 2 - Midcourse Rescue`.
- Marked `KLC-500 (P2027)` and `KLC-600 (P2027)` as `AWACS: True` in `data/cec_tables/05_Radars.csv`.
- Granted unlimited ship-to-ship (`Vessel` <-> `Vessel`) datalink reach in `CecCapability.ArePlatformsInDatalinkReach` (naval SATCOM), and upgraded Tier 1 `Link 16` pairs and Russian `TKS-2 / APD-518` AWACS links in `ResolveBestDatalinkPair`.

## [0.4.0] - 2026-11-09

### Added
- **Real-World Multi-Tier CEC & Datalink Qualification Engine**:
  - Replaced the legacy v0.3.0 broad-network system (`HasDataLink = True` blanket rule) with a strict, 4-tier qualification engine grounded in verified naval and air combat datalink datasets (`data/cec_tables/`).
  - Added auto-generated zero-allocation C# static lookup tables (`CecDataTables.g.cs`) generated by Python CLI tool (`tools/generate_cec_tables.py`) indexing 476 platforms (344 ships, 132 aircraft), 287 radars, and 232 missiles with a 10x10 pairwise protocol interaction matrix.
  - Strict Tier 3 default rule: any unindexed platform, radar, or munition defaults safely to Tier 3 (Local FCR only / Link 11).
  - **4-Tier Capability Engine**:
    - **Tier 1 (Full CEC / NIFC-CA / PLAN "A-Shoot-B-Guide")**: Blind Launch-on-Remote (OTH/EMCON), third-party mid-course & terminal handoff, composite track sharing.
    - **Tier 2 (Mid-Course Cooperative / Link 16 / HN-900 / Fort-TVM)**: Launch-on-Remote strictly blocked (shooter requires local radar lock to fire); supports mid-course handoff and consort track rescue if shooter enters EMCON, is jammed, or loses radar sight.
    - **Tier 3 (Local Guidance Only / Link 11 / Track Relay)**: Launch-on-Remote and mid-course handoffs strictly blocked. Missiles guided exclusively by launching ship's own FCR. Datalink acts as situational track relay only.
    - **Tier 0 (Standalone)**: No datalink, IR / unguided munitions.
  - **10x10 Pairwise Protocol Maximization**:
    - Pairwise interactions between platforms evaluated dynamically across 10 standards (`USG-2/3 CEC`, `TTNT`, `Link 16`, `Link 11`, `IFDL`, `MADL`, `JSIDLS`, `HN-900`, `RuFortTvm`, `TKS-2/APD-518`).
    - Handled stealth intra-flight isolation (IFDL / MADL / APD-518) via formation leadership tracking (`ConditionalWeakTable<Aircraft, ObjectBase>`), requiring active gateway platforms (E-2D Hawkeye) for cross-flight sharing.
  - **Physics-Derived Datalink Reach**:
    - Replaced flat range caps with 4/3-Earth radar horizon physics (`getRadarHorizon`) bounded by instrumented datalink reach and mast/flight altitudes, supporting global reach for SATCOM uplinks (`eu_GPS_Receiver`).
  - **Contextual Attack Hover Telemetry (Zero Map Clutter)**:
    - Zero map clutter during normal map navigation (permanent contact tags `ContactInfoLine1..4` left 100% clean).
    - Datalink telemetry displayed strictly when Attack Hover is active in `AttackingState` (`UpdateContactMouseoverInfo`):
      - Tier 2 Cue Only: Yellow `<Run Foreground="#FFF176">Cued via Link 16 (Local radar lock required to fire)</Run><LineBreak/>`
      - Tier 1 Blind OTH: Cyan `<Run Foreground="#4FC3F7">CEC Track: {Consort} [{Protocol} | Tier 1]</Run><LineBreak/>`
      - Tier 1 Forward Pass: Green `<Run Foreground="#81C784">Forward Pass: {Consort} [{Radar}]</Run><LineBreak/>`
      - Local Shot: Clean (no tag).
    - Preserved vanilla attack cursor (`attackunit`) for Tier 2 Cue Only contacts so players can issue intercept orders without obstruction.
  - **Deliberate Tactical Handoff & Survivor Takeover**:
    - Standby CW illuminators (e.g. `SPG-62`) immediately promoted and energized on consort takeovers, resolving Unity frame delays and channel allocation aborts.
    - Recovered dead-reckoning missiles on stale datalink (`DL STALE`) when the shooting ship is destroyed, jammed, or enters radar EMCON, transferring guidance mid-course to healthy cooperators.
    - Intercepted `ExtendedGuidance.Runtime.Illuminator` to enforce real-world CEC Forward Pass (Mountain Top / NIFC-CA) and prevent ExtendedGuidance from forcefully illuminating with a distant shooter or a shooter in radar EMCON (`Emcon.Value == true`), ensuring forward consorts illuminate when closer to the target.
  - **HUD Missile Control Panel Telemetry**:
    - Upgraded datalink telemetry to include both contact ID and clean platform model/name (e.g. `From 1001 (DDG-51 Arleigh Burke) / 1.7 s` and `Illum: 1004 (CG-67 Shiloh)`).
    - Preserved native Sea Power formatting when ExtendedGuidance is inactive (`Link to [1005] E-2D VAW-113 [NIFC-CA / USG-3 | Tier 1]`).
  - **Autonomous Employment ("Engage Free" / "Weapons Free")**:
    - Unified execution pipeline ensures Tier 1 units autonomously engage remote tracks OTH while Tier 2 units hold fire until local radar horizon crossing.
    - Verified full compliance with Sea Power's positive identification ROE rules before autonomous missile release.

## [0.3.0] - 2026-11-07

### Compatibility
- Added native **Anchor Chain (`IAnchorChainMod` / `[ACPlugin]`)** Workshop entry point (`AnchorChainEntry`) so the mod loads directly from Steam Workshop (`3380210757`) and respects the in-game Sea Power Mod Manager checkbox, while retaining standalone `BepInEx/plugins` support.
- Added full runtime compatibility bridge (`EgBridge`) for Anchor Chain's **`ExtendedGuidance` (`euromod.extended-guidance` v0.10.27, Workshop ID `3784474738`)** with zero compile-time DLL dependency:
  - Dynamically detects `ExtendedGuidance` whether loaded before or after `CecPlugin.Awake()` (`AppDomain.AssemblyLoad` + live `MissileStatusApi.Active` check) and adapts automatically if toggled via Anchor Chain preferences.
  - Preserves `ExtendedGuidance` mid-course `DatalinkCarrier` (`eu_AEGIS_BL9`, `SYR-1`, etc.) and delayed time-shared terminal CW/IWI illumination (`Runtime.Illuminator`) when `ExtendedGuidance` is active, while retaining strict synthetic-datalink SARH channel enforcement when `ExtendedGuidance` is disabled.
- Added full runtime compatibility for **`Pacific 2027 - Dynamic Campaign` (Workshop ID `3820272150`)** with zero `.ini` overrides:
  - Added `CecCapability.NormalizeRadarKey` to transparently strip trailing `" (P2027)"` balance-fork sensor suffixes during doctrinal network, AWACS, multifunction radar, synthetic-datalink, pure-CW illuminator, and SARH family classification while preserving raw `_systemName` bindings for Sea Power's engine.
  - Added full CEC, AWACS, SARH family, and orphaned-ammo network mappings for `Pacific 2027` and allied submod platforms, sensors, and munitions (`Shmel-M`, `KJ-3000 AESA`, `Type 346/A/B`, `Type 382`, `LJQ-368 054B`, `LJG-XX-054B-S`, `LJG-318`, `SLC-14 search`, `SPS-550K`, `AN/MPQ-53`/`65`, `Top Dome`, `91N6E`, `5P-27 Furke`, `MR-760 Fregat`, `Podberezovik`, `rfn_3r90m`, `AN/APG-77(V)1`, `KF-21 AESA`, `B-21 AESA`, `J/APG-2`, `AN/APG-63(V)1`, `EL/M-2032`, `weeb_aesa_radar`, `KLJ-5 (Type 1475)`, `J-36 AESA`, and `jp_*` / `fork_*` munitions).

### Added
- **Hybrid CEC + `ExtendedGuidance` Cooperative Realism**:
  - Pre-launch OTH & EMCON gates bridged across `WeaponSystem.CanSearchSensorsSeeTarget`, `WeaponSystem.FCRDetectionRange`, `WeaponSystem.CheckTargetIsDetectableByFCR`, and `ExtendedGuidance.Runtime.PlanningLaunchVisibility` (`FcrAcquisitionAdapter`).
  - Shared taskforce `PlottingTable` radar measurements bridged into `ExtendedGuidance.Runtime.ReadNativeContact` so OTH, EMCON, and remote-guider launches receive realistic cooperative radar updates during initial INS cueing and mid-course flight.
  - Consort SARH illuminators (including deferred idle `Targeting` illuminators on non-EMCON consorts) synced to per-vessel `AmmunitionParameters._associatedSensors` and promoted via `HandoffPatch` when terminal illumination is due and own-ship illuminators are horizon-masked, saturated, in EMCON, or sunk.
  - Batch physics-tick `_launchPlatform` aliasing in `Missile.OnFixedUpdate` (`Priority.First` / `Priority.Last`) so `ExtendedGuidance.Runtime.Prepare` and `RefreshExternal` survive OTH, EMCON, and sunken-launcher scenarios across multi-missile salvos.

### Changed
- **Unified Guider-Only Bottom-Bar UI (`DatalinkUiPatch`)**:
  - Replaced `GoodConnection`-only postfix with `ObjectMissileControlViewModel.GetState` (`Priority.First`) + `ObjectState` setter hooks so the active guiding unit is shown across both vanilla Sea Power strings (`GoodConnection`, `BadConnection`, `LostConnection`) and `ExtendedGuidance` 0.25s telemetry rows (including terminal consort illuminator reservations).
  - Updated display format from `Launcher -> Guider` to show **only the active guider's name**, skipping redundant `PropertyChanged` events during 0.25s refreshes.

### Fixed
- Updated `RemoteEngagementPatch.CanGuideAmmo_Postfix` to preserve upstream `false` results (respecting `_isDatalinkGuidance` on non-MCC weapons and `_compatibleGuidanceSystems` restrictions) while filtering `true` results through `CecCapability.CanSensorGuideAmmo`.
- Preserved consort SARH illuminator entries across mixed ARH/SARH salvos (`SM-2` + `SM-6`) by inspecting full launcher/magazine guidance needs in `CecCapability.RefreshConsortRadars`.

## [0.2.0] - 2026-11-07

### Compatibility
- Updated for **Sea Power 0.8.7 (Build 261021 Public Release)**:
  - Migrated timers from `Time.time` (`float`) to `GameTime.missionElapsedTime` (`double`) to match 0.8.7's `WeaponSystem._lastSearchForSensorTime` and `Missile._lostGuidanceTime`.
  - Updated `AmmunitionParameters._midCourseCorrection` checks to use 0.8.7's `AllowsLauncherGuidance` extension instead of raw enum comparisons.
  - Updated `DatalinkUiPatch` to hook `SeapowerUI.ViewModels.ObjectMissileControlViewModel.GoodConnection` (replacing the removed `Missile.GetDataLinkInformation` method).
  - Removed obsolete `Assembly-CSharp` reference from `CecMod.csproj`.

### Added
- **4 Real-World Doctrinal CEC Networks**:
  - **US / Allied CEC & NIFC-CA** (Aegis Baselines, NTU, Flight III SPY-6, Australian CEAFAR, Japanese FCS-3A/OPY-1/2, E-2C/D Hawkeye, E-3, E-7A Wedgetail, F-35, F-22, F/A-18E/F, F-15C/EX).
  - **NATO / European PAAMS & APAR** (Horizon/FREMM EMPAR/Herakles, De Zeven Provinciën/Sachsen/Iver Huitfeldt APAR, Type 45 SAMPSON, Type 26 Artisan, Eurofighter/Rafale/Gripen, Erieye).
  - **Soviet / Russian Fort, Poliment & Zaslon** (Slava/Kirov S-300F Fort, Gorshkov Poliment, Shtil-1, A-50U, Ka-31, MiG-31BM, Su-35S, Su-57).
  - **Chinese PLA / PLAN "A-Shoot-B-Guide"** (Type 052C/052D/055 Type 346/A/B, Type 054A/054B, KJ-500, KJ-600, KJ-2000, J-20, J-16, J-15B/T, J-35).
- **Doctrinal SARH Illuminator Families**: Enforces realistic X/C-band CW vs. IWI vs. TVM compatibility so incompatible illuminators never cross-guide SARH missiles.
- **Engage-on-Remote / Launch-on-Remote (OTH & EMCON)** (`RemoteEngagementPatch`, `LaunchTimeCecPatch`):
  - Ships and aircraft can launch beyond their local mast radar horizon or while keeping their own air-search radars switched OFF (EMCON) when an allied AWACS or forward CEC consort tracks the target and provides FCR/datalink coverage.
  - Missiles spawned during OTH or EMCON engagements bind directly to the remote CEC/AWACS radar at launch without forcing local radars out of EMCON.
- **ARH SAM & Air-to-Air CEC Mid-Course Datalink Handoff**:
  - Added mid-course datalink handoff and AWACS/MFR guidance support for ARH+MCC SAMs (`SM-6`, `SM-2 Blk IIIC`, `ESSM Blk 2`, `Aster 15/30`, `CAMM`, `40N6`, `9M96`, `HHQ-9B`) and CEC-capable AAMs (`AIM-120D`, `AIM-260`, `AIM-174B`, `Meteor`, `R-33`/`R-37M`, `PL-15`/`PL-17`).

### Fixed
- Fixed two vanilla consort-guidance bugs in `Missile.CheckRadioConnection` where a guiding consort's own hull colliders blocked its guidance raycast and range/alignment checks evaluated the original `_launchPlatform` instead of the guiding unit.
- Prevented missiles actively guided by a live consort/AWACS from becoming unguidable orphans (`WeaponBase.IsUnguidableOrphan`) when their original launch platform is sunk.
- Added `CanSensorGuideAmmo` safeguard so synthetic datalink dummy sensors (such as Euromod's `eu_AEGIS_BL9` when `ExtendedGuidance.dll` is disabled) cannot bypass SARH illuminator channel limits.

## [0.1.0] - 2026-06-10

### Added
- Initial release with basic ship-to-ship consort radar injection (`LaunchTimeCecPatch`), mid-flight SARH/command handoff (`HandoffPatch`), and bottom-bar `Launcher -> Guider` UI indicator (`DatalinkUiPatch`).
