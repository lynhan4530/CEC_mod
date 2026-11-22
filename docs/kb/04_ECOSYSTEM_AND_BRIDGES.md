# 04 — Workshop Ecosystem & Compatibility Bridges

A core goal of `CecMod` is **zero `.ini` overrides** and seamless runtime compatibility with major *Sea Power* Workshop mods.

## 1. Anchor Chain Entry Point (`AnchorChainEntry.cs`)
- **Target Mod**: Anchor Chain (`io.github.seapower_modders.anchorchain`, Workshop ID `3380210757`).
- **Dual Loading**:
  - Workshop / `StreamingAssets`: Loaded by Anchor Chain via `[ACPlugin(CecPlugin.PluginGuid, CecPlugin.PluginName, CecPlugin.PluginVersion)]` on `AnchorChainEntry : IAnchorChainMod`.
  - Standalone `BepInEx/plugins`: Loaded directly by BepInEx via `CecPlugin : BaseUnityPlugin` (`Awake()`).
- **Mod Manager Respect & Deduplication**:
  - `IsEnabledInModMenu()` inspects `Singleton<FileManager>.Instance.Directories` to confirm the user enabled the mod in Sea Power's Mod Manager (checking across both `StreamingAssets` staging and Steam Workshop paths if both exist).
  - `CecPlugin.IsLoaded` + `Chainloader.PluginInfos` checks prevent double-initialization if both copies are present.

## 2. `ExtendedGuidance.dll` Runtime Bridge (`EgBridge.cs`)
- **Target Mod**: `ExtendedGuidance` (`euromod.extended-guidance`, Workshop ID `3784474738`).
- **Zero Compile-Time Dependency**: `CecMod.csproj` does **not** reference `ExtendedGuidance.dll`. `CecMod` compiles and runs identically whether or not `ExtendedGuidance` is installed or enabled in Anchor Chain preferences.
- **Eager & Dynamic Detection**:
  - `EgBridge.Initialize(harmony)` watches `AppDomain.CurrentDomain.AssemblyLoad` for `"ExtendedGuidance"` and immediately probes `_ = IsActive;` at `CecPlugin.Awake()` so runtime Harmony hooks are installed before Mono JIT-compiles/inlines `ExtendedGuidance` wrapper methods (important because Anchor Chain loads `ExtendedGuidance` before `CecMod`).
  - Probes every `2000` ticks (`ProbeIntervalTicks`) for `ExtendedGuidance.MissileStatusApi.Active` or `Harmony.HasAnyPatches("euromod.extended-guidance")` and binds reflection delegates/field refs into `ExtendedGuidance.Runtime` (`DatalinkCarrier`, `CanIlluminate`, `Get(Missile, bool)`, `TerminalIlluminationDue(State, double)`) and `ExtendedGuidance.State` (`Missile`, `Illuminator`, `LinkAvailable`, `SupportUpdated`).

### 14 Dynamic Harmony Hooks Installed on `ExtendedGuidance.Runtime` (`ApplyRuntimeHooks`)
1. `PlanningLaunchVisibility` (Postfix): Lifts `PlanningLaunchCantSee` (`< 20.669622f`) to `Mathf.Max(distToTarget + 50f, 25f)` when `CanPerformLaunchOnRemote` succeeds.
2. `PlanningLaunchCantSee` (Postfix): Clears `CantSeeTarget` (`false`) directly on the planning wrapper when `CanPerformLaunchOnRemote` succeeds, defeating Mono JIT inlining of `PlanningLaunchVisibility`.
3. `PlanningMatchCantSeeMemo` (Postfix): Clears CantSee (`false`) directly on the memoized ammo-matching wrapper when `CanPerformLaunchOnRemote` succeeds.
4. `PlanningMatchCantSee` (Postfix): Clears CantSee (`false`) directly on the un-memoized ammo-matching wrapper when `CanPerformLaunchOnRemote` succeeds.
5. `ReadNativeContact` (Prefix): Sets `guide = null; weapon = null;` when a missile is guided remotely or launched OTH/EMCON by a qualified CEC shooter so `ExtendedGuidance` uses the shared taskforce `PlottingTable` radar track instead of rejecting the missile for lacking a local `ContactSensorAssignment`.
6. `ReadyRemoteGuide` (Postfix): Permits injected Tier 1 consort radars in `weapon._vwp._associatedSensors` (and idle `Targeting` illuminators when `_autoEnableSensor` is true) after verifying `CanGuideAmmo`, Tier 1 interaction, datalink reach, and free channels.
7. `RemoteTransport` (Postfix): Allows active network radars with `_hasDataLink == true` (e.g., AWACS `AN/APY-9`) to act as remote transport carriers.
8. `FcrRange` (Postfix): Returns `float.MaxValue` for air targets when `CanPerformLaunchOnRemote` succeeds.
9. `LauncherObservation` (Postfix): Returns `true` for air targets when `CanPerformLaunchOnRemote` succeeds.
10. `RemotePrelaunchReady` (Postfix): Checks `_ammoForEngage`, hardpoint `_weapons[0]`, and `_ammunitionOnWeapon` and returns `true` for air targets when `CanPerformLaunchOnRemote` succeeds.
11. `CanRadarSee` (Postfix): Falls back to `CecCapability.RadarCanSee(radar, target)` for active radars and air targets.
12. `CommandTracking` (Postfix): Returns `true` for Tier-1/Tier-2 datalink-capable missiles when guided by an active, operable radar that passes `CanGuideAmmo` and `RadarCanReachMissile`, preventing local `ContactSensorAssignment` checks from rejecting remote AWACS/consort guiders on `MidCourseCorrection=1` missiles.
13. `Illuminator` (Prefix + Finalizer): Sets `[ThreadStatic] ActiveEgIlluminatorMissile` so `RemoteEngagementPatch.CanGuideAmmo_Postfix` rejects non-SARH/`DirectedSearch` radars (e.g., `SPY-1D(V) (P2027)`), EMCON hosts, and non-qualifying/out-of-reach consorts, and releases any currently reserved terminal illuminator on a Tier 1/2 platform in air-radar EMCON via `ReleaseIlluminator`.
14. `RemoteLaunch` (Postfix): Gates air targets when `!HasLocalRadarSight` through `CecCapability.CanPerformLaunchOnRemote`, preventing Tier 2/3 platforms with `TrackSource=Auto` datalink carriers (e.g., `eu_AEGIS_BL5`/`BL9`/`BL10`) from bypassing `CecMod`'s Launch-on-Remote rules while leaving surface/land targets and local-sight shots untouched.

### Cross-Mod Physics & Terminal Illumination Synchronization
- **Batch Physics-Tick Aliasing (`HandoffPatch.OnFixedUpdate_Prefix`)**: `ExtendedGuidance.Runtime.Prepare` batch-processes all airborne missiles on the first `Missile.OnFixedUpdate` of each tick (`GameTime.missionElapsedTime`). `HandoffPatch` aliases `_launchPlatform` across all remotely guided missiles on that first call and restores them in `OnFixedUpdate_Postfix` / `Finalizer`.
- **Delayed Consort Terminal Illumination (`SyncConsortIlluminatorsToAmmo`)**: `ExtendedGuidance.Runtime.Illuminator` discovers terminal CW/IWI dishes by scanning `missile._ap._associatedSensors` and `_compatibleGuidanceSystems` (not `ws._vwp._associatedSensors`). `CecCapability.SyncConsortIlluminatorsToAmmo` registers compatible consort illuminators into `ap._associatedSensors` and `ap._compatibleGuidanceSystems` (tracked per `ap` in `_injectedAmmoSensors`).
- **Forward Pass Terminal Takeover (`HandoffPatch.OnUpdateEveryFrame_Postfix`)**: When `IsTerminalIlluminationDue` triggers and `EgBridge.GetActiveEgIlluminator` is null (e.g., distant shooter or shooter in EMCON), `HandoffPatch` calls `FindHandoffRadar(requireTerminalIlluminator: true)` to assign the closest forward consort illuminator and syncs it via `EgBridge.SetActiveEgIlluminator`.
- **Mid-Course `DatalinkCarrier` Preservation (`LaunchTimeCecPatch.ActivateNonLaserGuidanceSystem_Postfix`)**: Preserves non-EMCON `DatalinkCarrier` bindings (e.g., `eu_AEGIS_BL9`) when `RadarCanReachMissile` is true, letting `ExtendedGuidance` reserve terminal illumination later; refuses local `DatalinkCarrier` bindings when `IsPlatformInAirRadarEmcon(launcher)` is true (since `BaseSystem.init()` leaves `eu_AEGIS_BL9` ON during EMCON), rebinding immediately to the remote consort's `DatalinkCarrier` or SARH illuminator.

## 3. `Pacific 2027 - Dynamic Campaign` Compatibility (Workshop ID `3820272150`)
- Balance-fork sensors and units in *Pacific 2027* append `" (P2027)"` to INI entity names.
- `CecCapability.NormalizeRadarKey` and `tools/generate_cec_tables.py` (`normalize_key`) strip `" (P2027)"` during table generation and runtime lookups while leaving raw `_systemName` untouched for Sea Power's engine.
- Both `05_Radars.csv` (e.g., `KLC-500 (P2027)` / `KLC-600 (P2027)` marked `AWACS: True` in v0.4.1) and normalized fallback aliases cover P2027 units automatically.

## 4. Euromod SATCOM / GPS Weapons (Workshop ID `3629144864`)
- Sensors such as `eu_GPS_Receiver`, `F16_GPS_Link`, `IAF_GPS_Link`, and `GPS_Link` map to `DatalinkStandard.SatcomUplink` in `generate_cec_tables.py`.
- In `CecCapability`, `SatcomUplink` resolves to `InteractionTier.SatcomGlobal` and `HasSatcomTransceiver` grants planetary datalink reach across horizon limits.
