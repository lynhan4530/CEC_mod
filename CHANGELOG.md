# Changelog

All notable changes to **Sea Power CEC (`CecMod`)** are documented in this file.

## [0.3.0] - 2026-11-07

### Compatibility
- Added full runtime compatibility bridge (`EgBridge`) for Anchor Chain's **`ExtendedGuidance` (`euromod.extended-guidance` v0.10.27, Workshop ID `3784474738`)** with zero compile-time DLL dependency:
  - Dynamically detects `ExtendedGuidance` whether loaded before or after `CecPlugin.Awake()` (`AppDomain.AssemblyLoad` + live `MissileStatusApi.Active` check) and adapts automatically if toggled via Anchor Chain preferences.
  - Preserves `ExtendedGuidance` mid-course `DatalinkCarrier` (`eu_AEGIS_BL9`, `SYR-1`, etc.) and delayed time-shared terminal CW/IWI illumination (`Runtime.Illuminator`) when `ExtendedGuidance` is active, while retaining strict synthetic-datalink SARH channel enforcement when `ExtendedGuidance` is disabled.

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
