# 01 — Vision, Doctrine & Qualification Model

## 1. Why v0.4+ Replaced the v0.3 Blanket Rule
- In `CecMod` v0.3.0 and earlier, CEC membership was inferred broadly from `HasDataLink = True` in `sensors.ini` and prefix/dictionary sensor matching. Because vanilla *Sea Power* sets `HasDataLink = True` on most search radars, Cold War ships (*Knox*, *Oliver Hazard Perry*, *Spruance*, *Udaloy*) and legacy missiles (`RIM-66A` SM-1, `RIM-7E` Sea Sparrow, `SA-N-4`) could blind-fire Over-The-Horizon (OTH) and hand off mid-flight.
- Beginning in `v0.4.0` (`CecMod` v0.4.1 current), all capability checks run through a **4-Tier qualification engine** and a **10x10 pairwise datalink compatibility matrix** backed by verified CSV datasets (`data/cec_tables/`).
- **Strict Tier 3 Default Fallback**: Any unindexed platform, radar, or missile safely resolves to `DefaultTier3` (`PlatformProfile.DefaultTier3`, `RadarProfile.DefaultTier3`, `MissileProfile.DefaultTier3`) — meaning local FCR guidance only, never accidental modern CEC.

## 2. The 4-Tier Capability Model (`CecTier`)

| Tier | Enum | Real-World Equivalent | Launch-on-Remote (OTH/EMCON) | Mid-Course Handoff / Rescue |
|---|---|---|---|---|
| **Tier 1** | `CecTier.Tier1` | Full CEC / NIFC-CA (`USG-2/3`), `TTNT`, `JSIDLS` ("A-Shoot-B-Guide") | **Allowed** (ARH blind OTH; SARH only with Forward Pass CW illuminator) | **Allowed** |
| **Tier 2** | `CecTier.Tier2` | Mid-Course Cooperative (`Link 16`, `HN-900`, `RuNaval / Fort-TVM`) | **Blocked** (local radar sight required to fire) | **Allowed** (consort rescue on EMCON turn-away, jamming, horizon loss, or sunk launcher) |
| **Tier 3** | `CecTier.Tier3` | Local Guidance Only (`Link 11 / TADIL-A`, legacy systems) | **Blocked** | **Blocked** (100% self-contained local FCR from launch to impact; track relay only) |
| **Tier 0** | `CecTier.Tier0` | Standalone (no datalink, IR / unguided munitions) | **Blocked** | **Blocked** |

## 3. Pairwise Datalink Matrix (`DatalinkStandard` & `InteractionTier`)

### Supported Datalink Flags (`DatalinkStandard` `[Flags]`)
10 standards indexed in the 10x10 matrix (`02_Datalink_Compatibility_Matrix.csv`):
1. `Usg2Cec` — USG-2/3 CEC
2. `Ttnt` — TTNT
3. `Link16` — Link 16 (MIDS / JTIDS / Meteor Datalink / K-JTDLS / TIDLS / JDCS)
4. `Link11` — Link 11 (TADIL-A)
5. `Ifdl` — IFDL (F-22)
6. `Madl` — MADL (F-35)
7. `Jsidls` — JSIDLS (PLA / PLAN)
8. `Hn900` — HN-900 / Data Link 4 (PLAN)
9. `RuFortTvm` — RuNaval / Fort-TVM
10. `Tks2Apd518` — TKS-2 / APD-518 / Shmel / S-111 (Russian Air)

Plus 2 non-matrix special flags handled in `CecDataTables.QueryDatalinkMatrix`:
- `RuNavalTactical` — RuNaval Tactical / C4I (evaluates to `Tier3_TrackRelay` when paired with `RuNavalTactical`, `RuFortTvm`, or `Tks2Apd518`; otherwise `Incompatible`).
- `SatcomUplink` — SATCOM / GPS uplink (e.g., `eu_GPS_Receiver`); evaluates to `InteractionTier.SatcomGlobal`.

### Resolution Rules (`CecCapability.ResolveBestDatalinkPair`)
Platforms often carry multiple datalinks (stored as a bitmask). For any `(shooter, consort)` pair, `ResolveBestDatalinkPair` evaluates every `(s1, s2)` standard combination and chooses the highest `InteractionTier` rank:
- Rank order: `SatcomGlobal (6)` > `Tier1_FullCec (5)` > `Tier1_IntraFlightOnly (4)` > `Tier2_MidcourseRescue (3)` > `Tier2_CueOnly (2)` > `Tier3_TrackRelay (1)` > `Incompatible (0)`.
- **Verified Matrix & Runtime Upgrades**:
  - `Usg2Cec` / `Ttnt` <-> `Link16` is `Tier2_MidcourseRescue` in `02_Datalink_Compatibility_Matrix.csv` (updated in v0.4.1).
  - **Tier 1 Link 16 Upgrade**: When `raw == Tier2_MidcourseRescue` over `Link16 <-> Link16` and **both** `shooter` and `consort` have `CecTier == CecTier.Tier1` (e.g., GlobalEye, Eurofighter, Rafale Late, JAS-39, KF-21), `ResolveBestDatalinkPair` upgrades the pair to `Tier1_FullCec` **only when neither platform carries stealth directional links (`Madl` / `Ifdl`)**, preventing `F-35` / `F-22` pairs from bypassing intra-flight isolation or gateway rules via Link 16.
  - **Stealth Intra-Flight Isolation (`Tier1_IntraFlightOnly`)**:
    - `Ifdl` (F-22) and `Madl` (F-35) share Tier 1 targeting only within the same flight group (`IsSameFlightGroup`).
    - `IsSameFlightGroup` inspects live `Formation` (`!_isDisbanded`) and `ActiveFormationLeader` / `LeaderStation` directly (without stale caching), returns `InteractionTier.Tier1_IntraFlightOnly` for same-flight pairs, and bridges cross-flight `Madl <-> Madl` / `Ifdl <-> Ifdl` pairs to `Tier1_FullCec` (`MADL (GW)` / `IFDL (GW)`) when an active gateway is present (otherwise `Incompatible`, leaving only Link 16 `Tier2_MidcourseRescue` cueing).
    - `Tks2Apd518` (`Tier1_IntraFlightOnly` between fighters) upgrades to `Tier1_FullCec` across flight groups when either platform carries an AWACS radar (`HasAwacsRadar`, e.g., A-50U).
  - **Gateway Bridging (`GatewayRequired`)**:
    - When `raw == GatewayRequired` (e.g., `Madl` <-> `Usg2Cec` / `Ttnt` / `Link16`, or `Ifdl` <-> `Link16`), it upgrades to `Tier1_FullCec` if `TaskforceHasActiveGateway(shooter._taskforce, shooter, consort)` is true (e.g., `usn_e-2d`, `fr_e2d`, `usn_fa-18f_blk3`, `usn_fa-18e_blk3`, `usn_fa-18f_blk3c`, or any platform with `"gateway"` in CSV fields / `"e-2d"` in display name) AND the gateway is an active, airborne (`Aircraft._isInFlight`) platform within radio-horizon and instrumented datalink reach of both `shooter` and `consort` (`IsGatewayInRadioReach`, using `DatalinkMastHeight(..., requireRadarOn: false)` so a gateway flying in search-radar EMCON still relays radio traffic); otherwise it degrades to `Tier2_CueOnly`.

### Doctrinal Networks (`CecCapability.CecNetwork`)
Used to group sensors and platforms into Western (`WesternJoint`), PLA (`PlaJoint`), and Russian (`RuJoint`) architectural families when checking network presence.

## 4. Munition Gating, Forward Pass SARH & AWACS Scope
Implemented in `CecCapability.CanPerformLaunchOnRemote`:
- **Shooter Gate**: Shooter must be `CecTier.Tier1` (or carry `DatalinkStandard.SatcomUplink`). Missile must not be `Tier0` or `Tier3`.
- **Tier 1 ARH / IR / ARM / Command (`!IsSarhMissile && missileProfile.AllowLaunchOnRemote`)**:
  - Allowed blind OTH or in EMCON if **any** Tier 1 compatible cooperator in the taskforce tracks the target (`HasTrackingCooperatorForRemoteLaunch`).
  - **AWACS Scope**: AWACS radars (`IsAwacsRadar`) can only guide non-SARH missiles (`CanSensorGuideAmmo` returns `false` for AWACS on SARH), provided `CecTier` is Tier 1/2 and `ap._midCourseCorrection.AllowsLauncherGuidance` is true.
- **Tier 2 SARH / TVM (`IsSarhMissile`) — Forward Pass (Project Mountain Top)**:
  - AWACS have zero CW illuminators and cannot illuminate SARH terminals.
  - A Tier 1 shooter may Launch-on-Remote a SARH/TVM missile **only if** `HasForwardPassIlluminatorCoverage` finds a Tier 1 consort with a compatible physical targeting/CW illuminator (`SarhIllumFamily`: `UsNatoXBandCw`, `RuFortTvm`, `RuShtilCw`, `PlanHhq9`, `PlanHhq16`, `RuZaslonPesa`) that has free channels and current radar LOS (`RadarCanSee`) to the target.

## 5. Datalink Reach & Physics Invariants
Implemented in `CecCapability.ArePlatformsInDatalinkReach`:
1. **Ship-to-Ship (`Vessel` <-> `Vessel`)**: Returns `true` unconditionally (v0.4.1 naval SATCOM rule).
2. **SATCOM Transceivers (`HasSatcomTransceiver`)**: Returns `true` unconditionally (checks `SatcomUplink` flag or `"GPS"` / `"SATCOM"` in sensor `_systemName`).
3. **Airborne / Surface-to-Air Links**: Bounded by `Mathf.Min(horizon, maxInstrumented)`:
   - `horizon = RadarCalculator.getRadarHorizon(Mathf.Max(h1, 20f), Mathf.Max(h2, 20f), visual: false, 0f)`
   - `GetMaxDatalinkRange`: `300 nmi` (`300f * 27.559496f`) if `Aircraft` or `Helicopter`; `150 nmi` (`150f * 27.559496f`) for surface units.
   - `DatalinkMastHeight(consort, requireRadarOn: true)` requires the remote cooperator to have an operational, active (`IsOn.Value`) datalink-capable radar.
4. **Kinematics Invariant**: `CecMod` never modifies missile aerodynamics, drag, thrust, or `MissileSimulator.EstimateShot`.

## 6. Contextual Attack Hover Telemetry (Zero Map Clutter)
Implemented in `AttackingStateUpdateDisplayedRangePatch` (`RemoteEngagementPatch.cs`) and `CecCapability.GetHoverTelemetryTag`:
- Permanent map contact labels (`ContactInfoLine1..4`) are left untouched.
- Telemetry is appended to `MapUnitViewModel.ContactMouseoverInfo` **only** during `AttackingState.UpdateDisplayedRange` when `!CecCapability.HasLocalRadarSight(shooter, target)`:
  - **Green** (`#81C784`): `Forward Pass: {shipName} [{radarName}]`
  - **Cyan** (`#4FC3F7`): `CEC Track: {guiderName} [{protocol} | Tier 1]`
  - **Yellow** (`#FFF176`): `Cued via {protocol} (Local radar lock required to fire)`
