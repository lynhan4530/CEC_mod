# Sea Power CEC (`CecMod` v0.4.1) — In-Game Test Case Tracker

## Summary Status

| ID | Group | Test Case | Status | Result / Summary |
|---|---|---|---|---|
| [TC-01](#tc-01--air-to-air-emcon-launch-on-remote-nifc-ca) | A | Air-to-Air EMCON Launch-on-Remote (NIFC-CA) | `PASS` | User confirmed working in-game. |
| [TC-02](#tc-02--ship-to-air-oth-launch-on-remote-arh--awacs) | A | Ship-to-Air OTH Launch-on-Remote (ARH + AWACS) | `PASS` | User confirmed passed in-game (18 modern USN Aegis hulls including `usn_ddg_burke_f3_concept` upgraded to Tier 1 `USG-2 CEC`; cyan `CEC Track` hover and OTH launch verified; `EgBridge` now gates `ExtendedGuidance.Runtime.RemoteLaunch` on air targets). |
| [TC-03](#tc-03--multi-missile-salvo-channel-expansion-32-ch-check) | A | Multi-Missile Salvo Channel Expansion (32-Ch Check) | `PASS` | User confirmed passed in-game. |
| [TC-04](#tc-04--pla-a-shoot-b-guide-jsidls) | A | PLA "A-Shoot-B-Guide" (JSIDLS) | `PASS (Note)` | Cooperative JSIDLS guidance from `KJ-600` confirmed via `ExtendedGuidance` hint on follow-up `F-16` test near ship; `KJ-600` did not detect `Tu-95RT` at `77.6 nm` inside its radar ring. |
| [TC-05](#tc-05--tier-1-link-16-upgrade-euro--allied) | A | Tier 1 Link 16 Upgrade (Euro / Allied) | `PASS` | User confirmed passed in-game. |
| [TC-06](#tc-06--sarh-forward-pass-with-consort-illuminator) | B | SARH Forward Pass with Consort Illuminator | `PASS` | User confirmed passed in-game (`DDG-78 Porter` stays EMCON while forward consort `DDG-75 Donald Cook` guides/illuminates `RIM-66M-5`; keep follow-up note on base `DDG-51` CSV variants). |
| [TC-07](#tc-07--sarh-blocked-when-only-awacs-sees-target) | B | SARH Own-Ship Terminal LOR & Below-Horizon Block (`AWACS`-Only Track) | `PASS` | User confirmed passed in-game (Own-Ship Terminal Illumination CEC LOR fires Tier 1 SARH+datalink missiles OTH on AWACS track once predicted terminal intercept enters own CW horizon; permanently below-horizon targets stay blocked with yellow `Below own CW horizon at intercept` hint while keeping attack cursor unlocked; fixed duplicate `E-2D VAW-113` unit name). |
| [TC-08](#tc-08--sarh-family-isolation-no-cross-band-illumination) | B | SARH Family Isolation (No Cross-Band Illumination) | `PASS` | User confirmed passed in-game. |
| [TC-09](#tc-09--tier-2-awacs-e-3a-blocks-launch-on-remote) | C | Tier 2 AWACS (`E-3A`) Blocks Launch-on-Remote | `PASS` | User confirmed passed in-game. |
| [TC-10](#tc-10--tier-2-link-16-mid-course-rescue-shoot--crankcold) | C | Tier 2 Link 16 Mid-Course Rescue (Shoot & Crank/Cold) | `PASS` | User confirmed passed in-game. |
| [TC-11](#tc-11--tier-3-cold-war-ship--missile-isolation) | C | Tier 3 Cold War Ship / Missile Isolation | `UNSURE` | After turning launcher radar OFF, illuminator remained ON (suspected `ExtendedGuidance` behavior; verify later). |
| [TC-12](#tc-12--tier-0-standalone--ir-munitions) | C | Tier 0 Standalone / IR Munitions | `PASS` | User confirmed passed in-game. |
| [TC-13](#tc-13--stealth-intra-flight-isolation-madlifdl) | D | Stealth Intra-Flight Isolation (`MADL` / `IFDL`) | `PASS` | User confirmed passed in-game (same-flight F-35 fires via `[MADL | Tier 1]`; separate-flight F-35 and non-gateway `F/A-18F` Block II blocked with yellow Link 16 cue; `F/A-18F Block III` fires as gateway). |
| [TC-14](#tc-14--gateway-bridging-e-2d--fa-18f-blk-iii) | D | Gateway Bridging (`E-2D` / `F/A-18F Blk III`) | `PASS` | User confirmed passed in-game (airborne gateway in radio reach bridges cross-flight `MADL` / `(GW)` even with AWACS search radar OFF). |
| [TC-15](#tc-15--datalink-horizon--reach-limits--ship-satcom) | D | Datalink Horizon & Reach Limits + Ship SATCOM | `Pending` | — |
| [TC-16](#tc-16--pitbull-terminal-localterminalonlytrue-clean-log) | E | Pitbull Terminal (`LocalTerminalOnly=True`) Clean Log | `PASS` | User confirmed passed in-game. |
| [TC-17](#tc-17--sunken-launcher-survivor-takeover) | E | Sunken Launcher Survivor Takeover | `PASS` | User confirmed passed in-game. |
| [TC-18](#tc-18--consort-guidance-hull-raycast-check) | E | Consort Guidance Hull Raycast Check | `PASS` | User confirmed passed in-game. |
| [TC-19](#tc-19--extendedguidance-on-vs-off-parity) | F | `ExtendedGuidance` ON vs. OFF Parity | `Pending` | — |
| [TC-20](#tc-20--autonomous-weapons-free--zero-map-clutter--f10-overlay) | F | Autonomous "Weapons Free" + Zero Map Clutter + F10 Overlay | `Pending` | — |

---

## Group A — Tier 1 Full CEC / NIFC-CA (Launch-on-Remote & EMCON)

### TC-01 — Air-to-Air EMCON Launch-on-Remote (NIFC-CA)
- **Status**: `PASS`
- **Setup**: `F/A-18F Block III` (`EMCON`, radar OFF, `AIM-174B`) + `E-2D Hawkeye` (radar ON) vs. `Tu-95RT` at ~100 nmi, 30,000 ft.
- **Steps**: Order `F/A-18F` to fire `AIM-174B` at `Tu-95RT` while keeping `F/A-18F` radar OFF.
- **Expected Result**:
  - Hover shows **Cyan**: `CEC Track: E-2D ... [USG-2/3 CEC | Tier 1]`.
  - `F/A-18F` **fires immediately** without turning `AN/APG-79` on.
  - Missile bottom bar shows `E-2D` guiding.
  - Log shows one `[CEC] Launch-on-Remote:` line.
- **Result / Notes**: User confirmed passed (`2026-11-22`).

---

### TC-02 — Ship-to-Air OTH Launch-on-Remote (ARH + AWACS)
- **Status**: `PASS`
- **Setup**: `DDG-51` / `CG-47` (`SM-6` / `ESSM Blk 2`, `EMCON` or target below ship radar horizon) + `E-2D Hawkeye` (radar ON) vs. low-altitude sea-skimming target beyond ~20–25 nmi.
- **Steps**: Order ship to fire `SM-6` at the below-horizon target.
- **Expected Result**:
  - Hover shows **Cyan** `CEC Track: E-2D`.
  - Ship fires OTH without breaking EMCON.
  - Missile tracks on `E-2D` mid-course and goes active terminal.
- **Result / Notes**:
  - **Re-test after fix (`2026-11-22`)**: User confirmed passed in-game (18 misclassified modern USN Aegis CEC variants including `usn_ddg_burke_f3_concept` upgraded from Tier 2 `Link 16 / Link 11` to Tier 1 `USG-2 CEC / Link 16` in `data/cec_tables/03_Ships.csv`, plus `EgBridge.RemoteLaunch_Postfix` gating `ExtendedGuidance.Runtime.RemoteLaunch` on air targets). Hover shows cyan `CEC Track: E-2D ... [USG-2/3 CEC | Tier 1]` and `RIM-174B` launches OTH cleanly.
  - *Previous pre-fix run (`2026-11-22`, `media_1795290975198_c2489bd3.jpg`)*: `DDG-133 Sam Nunn` (`#1001`, `usn_ddg_burke_f3_concept`) + `#1003 (AEW&C) VAW-113` (`29,998 ft`) vs. `#7001` (`1,085 ft`) successfully fired `RIM-174B` (`Max range: 125.8 nm`, `47 RIM-174B` remaining) via `ExtendedGuidance`, while hover tooltip showed yellow `Cued via Link 16 (Local radar lock required to fire)`.

---

### TC-03 — Multi-Missile Salvo Channel Expansion (32-Ch Check)
- **Status**: `PASS`
- **Setup**: Same as **TC-01** or **TC-02**, with 4+ `AIM-174B` or `SM-6` fired simultaneously via `E-2D` (`AN/APY-9 (P2027)`).
- **Steps**: Fire a salvo of 4+ missiles at 2–3 separate targets while shooter stays in EMCON.
- **Expected Result**:
  - All missiles launch cleanly (proves `AN/APY-9` expanded past vanilla's 1-channel default to 32 channels).
  - All missiles show active datalink from `E-2D`.
- **Result / Notes**: User confirmed passed (`2026-11-22`).

---

### TC-04 — PLA "A-Shoot-B-Guide" (JSIDLS)
- **Status**: `PASS (Note)`
- **Setup**: `Type 055` / `Type 052D` (`HHQ-9B`, EMCON) or `J-20` / `J-16` (`PL-15`/`PL-17`, EMCON) + `KJ-500` / `KJ-600 (P2027)` (radar ON) vs. distant air target.
- **Steps**: Order shooter to fire while radar OFF.
- **Expected Result**:
  - Hover shows **Cyan** `CEC Track` with `JSIDLS`.
  - Shooter fires in EMCON; `KJ-500`/`KJ-600` guides mid-course.
- **Result / Notes**:
  - Tested on `2026-11-22` (see `media_1795293194655_d3e1151b.jpg` & `media_1795293255282_fbc684f1.jpg`) with `#1001` Type 055 `Zunyi (107)`, `#1002` `J-16` (`29,998 ft`), and `#1003 (AEW&C)` `KJ-600` (`30,011 ft`) vs. `Tu-95RT 169th GAP` (`77.6 nm` from `KJ-600`, `78.6 nm` from `Zunyi (107)`, `129.0 nm` from `J-16`).
  - **Passed verification**: Testing directly with an `F-16` near the ship showed `ExtendedGuidance` guidance coming from `KJ-600`, confirming PLA AWACS cooperative guidance works.
  - **Observed anomaly (investigate later)**: `KJ-600` (`30,011 ft`) had its radar emitting (large yellow range ring visible on tactical display), yet `Tu-95RT` did not appear as a detected contact at `77.6 nm` inside `KJ-600`'s radar ring. (Note user suspects a base/mod radar detection issue with `KJ-600`/Chinese AWACS.)

---

### TC-05 — Tier 1 Link 16 Upgrade (Euro / Allied)
- **Status**: `PASS`
- **Setup**: Two Tier 1 Link 16 platforms (e.g., `GlobalEye` + `Eurofighter` / `Rafale` / `JAS-39` / `KF-21` with `Meteor`) in EMCON vs. distant air target.
- **Steps**: Order fighter (radar OFF) to fire `Meteor` using `GlobalEye` track.
- **Expected Result**:
  - Pair upgrades to `Tier1_FullCec` over Link 16.
  - Fighter fires in EMCON and `GlobalEye` guides mid-course.
- **Result / Notes**: User confirmed passed (`2026-11-22`).

---

## Group B — Tier 1 Forward Pass SARH (*Project Mountain Top*)

### TC-06 — SARH Forward Pass with Consort Illuminator
- **Status**: `PASS`
- **Setup**: Rear `DDG-51` (`SM-2MR`, EMCON or OTH) + forward `DDG-51` / `CG-47` (radar ON, within radar horizon of target, `SPG-62` standby/free) vs. low-altitude target.
- **Steps**: Hover target with rear ship's `SM-2`, then fire.
- **Expected Result**:
  - Hover shows **Green**: `Forward Pass: <Consort> [SPG-62]`.
  - Rear ship fires OTH/EMCON.
  - In terminal window (or immediately without EG), forward consort energizes `SPG-62` synchronously and illuminates target to impact.
- **Result / Notes**:
  - Tested on `2026-11-22`:
    - **Re-test after fix**: User confirmed passed in-game (`DDG-78 Porter` stays in EMCON and forward consort `DDG-75 Donald Cook` guides/illuminates `RIM-66M-5` end-to-end).
    - **Previous Run (`DDG-78 Porter` `#1001` + `DDG-75 Donald Cook` `#1002` at `58.96 nm` vs. `#7001 (AEW&C) A-50`, see `media_1795295104694_9fa52080.jpg`)**: Hover tooltip showed green `Forward Pass: DDG-75 Donald Cook [SPG-62]`, but prior to the fix the shooter still turned its own radar ON and guided the missile.
    - **Previous Note (`DDG-51` variants, see `media_1795294346720_079bcd8d.jpg` & `media_1795294616416_26206915.jpg`)**: Run 1 with base `DDG-51 Arleigh Burke` (`RIM-66M-5`) required local LOS to fire SM-2 (check/specify exact unit variants eligible for Tier 1 Forward Pass later). `DDG-51` (2020 variant) with `RIM-66P` fired OTH when forward radar was ON.

---

### TC-07 — SARH Blocked When Only AWACS Sees Target
- **Status**: `PASS`
- **Setup**: `DDG-51` (`SM-2MR`, OTH/EMCON) + `E-2D Hawkeye` (radar ON, tracking target), **no** forward consort with CW illuminator.
- **Steps**: Hover target with `SM-2` beyond own CW horizon (both far OTH and closing into terminal CW horizon) and engage.
- **Expected Result**:
  - **Below own CW horizon at intercept** (no forward consort): Hover shows **Yellow** `Cued via {protocol}: {AWACS} (Below own CW horizon at intercept)`; OTH firing is held, and attack cursor stays **unlocked** (real organic horizon shown) so the player can queue an engage/pursue order.
  - **Closing into own CW horizon by terminal phase**: Hover shows **Cyan** `CEC LOR: {AWACS} [{protocol} | Own {Illum} Terminal]`; ship fires OTH on AWACS CEC track via mid-course datalink (`eu_AEGIS_BL5`) without local radar sight and illuminates with own `SPG-62` in terminal phase.
- **Result / Notes**:
  - **Re-test after fix (`2026-11-22`)**: User confirmed passed in-game (`DDG-78 Porter` + `E-2D VAW-113` vs. low bomber at `446–482 ft`): held fire at `86.83 nm` with unlocked engage order (`Target too far`) and yellow `Cued via USG-2/3 CEC: E-2D VAW-113 (Below own CW horizon at intercept)`, then launched `RIM-66M-5` (`#36` / map `#1003`) via Own-Ship Terminal LOR around `~41–43 nm` (beyond `DDG-78`'s `~35.4–36.3 nm` `SPG-62` horizon) and completed own-ship terminal illumination; duplicate `VAW-113 E-2D VAW-113` naming also fixed.
  - *Previous pre-fix run (`2026-11-22`, `media_1795295410634_32cf8c55.jpg`)*: `DDG-78 Porter` (`#1001`, `RIM-66M-5`) + `#1002 (AEW&C) VAW-113` (`30,000 ft`) vs. `#7001` (`70.22 nm`, `3,922 ft`) blocked SARH LOR with `Sensor Range: 0/68Nmi` (`Can't See Target`) and yellow `Cued via USG-2/3 CEC (Local radar lock required to fire)`.

---

### TC-08 — SARH Family Isolation (No Cross-Band Illumination)
- **Status**: `PASS`
- **Setup**: Rear `DDG-51` (`SM-2MR`, `UsNatoXBandCw`) + forward consort with **incompatible** SARH family (or `Type 052D` / Russian `Slava` in mixed blue/custom test).
- **Steps**: Hover target with `SM-2` while only an incompatible illuminator has LOS.
- **Expected Result**:
  - Forward Pass is **blocked** (cannot illuminate X-Band CW SM-2 with non-matching illuminator family).
- **Result / Notes**: User confirmed passed (`2026-11-22`).

---

## Group C — Tier 2, Tier 3 & Tier 0 Strict Realism Gates

### TC-09 — Tier 2 AWACS (`E-3A`) Blocks Launch-on-Remote
- **Status**: `PASS`
- **Setup**: `F/A-18F` (`EMCON`, radar OFF, `AIM-120D`/`AIM-174B`) or `DDG-51` (EMCON) + `E-3A Sentry` (`Link 16`, radar ON) vs. air target.
- **Steps**: Hover target and attempt to fire while shooter radar is OFF.
- **Expected Result**:
  - Hover shows **Yellow**: `Cued via Link 16 (Local radar lock required to fire)`.
  - Shooter **cannot** fire in EMCON (must turn on its own radar and get local lock first).
- **Result / Notes**: User confirmed passed (`2026-11-22`).

---

### TC-10 — Tier 2 Link 16 Mid-Course Rescue (Shoot & Crank/Cold)
- **Status**: `PASS`
- **Setup**: `F-15C` / `F/A-18` (radar ON, `AIM-120C/D`) + `E-3A Sentry` (radar ON) vs. air target.
- **Steps**:
  1. Fire `AIM-120` using fighter's own radar.
  2. Immediately turn fighter's radar OFF (EMCON) and turn 180° away.
- **Expected Result**:
  - Missile does **not** go dumb (`DL LOST`).
  - `HandoffPatch` transfers mid-course updates to `E-3A` (`[CEC] Handoff: ... -> 'E-3A'`).
  - Bottom bar shows `E-3A [Link 16 Rescue | Tier 2 AWACS]`.
- **Result / Notes**: User confirmed passed (`2026-11-22`).

---

### TC-11 — Tier 3 Cold War Ship / Missile Isolation
- **Status**: `UNSURE`
- **Setup**: Cold War `Knox` / `OHP` / `Spruance` (`RIM-66A` SM-1 / `RIM-7E`) + `E-2D` or `DDG-51` (radar ON) vs. air target.
- **Steps**:
  1. Try OTH/EMCON launch.
  2. Launch with local FCR, then turn launcher radar OFF.
- **Expected Result**:
  - OTH/EMCON launch is **blocked**.
  - No mid-course handoff occurs if launcher shuts off radar (missile loses guidance as in real life).
- **Result / Notes**:
  - Tested on `2026-11-22`: After turning the launcher's radar OFF post-launch, the ship's illuminator stayed ON (user suspects `ExtendedGuidance` behavior). Needs verification later (e.g., re-testing with `ExtendedGuidance` OFF or disabling FCR).

---

### TC-12 — Tier 0 Standalone / IR Munitions
- **Status**: `PASS`
- **Setup**: Fighter with `AIM-9` (IR) or unguided weapon + `E-2D` tracking target.
- **Steps**: Hover and fire.
- **Expected Result**:
  - No CEC hover tag; pure vanilla seeker behavior; zero `[CEC]` handoff activity.
- **Result / Notes**: User confirmed passed (`2026-11-22`).

---

## Group D — Stealth Isolation, Gateways & Datalink Reach Physics

### TC-13 — Stealth Intra-Flight Isolation (`MADL`/`IFDL`)
- **Status**: `PASS`
- **Setup**: Flight 1: 2x `F-35` (lead radar ON, wingman EMCON) + Flight 2: 1x `F-35` (separate flight, EMCON). **No gateway** (`E-2D`) present.
- **Steps**:
  1. Order Flight 1 wingman (EMCON) to shoot using Flight 1 lead's track.
  2. Order Flight 2 `F-35` (EMCON) to shoot.
- **Expected Result**:
  - **Same flight** wingman **succeeds** (`Tier1_IntraFlightOnly`).
  - **Separate flight** `F-35` is **degraded/blocked** (`GatewayRequired` → `Tier2_CueOnly`) without a gateway.
- **Result / Notes**:
  - **Re-test after fix (`2026-11-22`)**: User confirmed all tests passed in-game (same-flight `F-35` wingman fires in EMCON via `[MADL | Tier 1]`; separate-flight `F-35` and non-gateway `F/A-18F` Block II are blocked with yellow Link 16 cue; `F/A-18F Block III` fires in EMCON as its own gateway).
  - *Previous pre-fix run (`2026-11-22`)*: F-35 CEC worked across all F-35s regardless of flight group due to `Link 16` Tier 1 upgrade bypass, and `F/A-18F` (Block II, non-gateway) remained blocked without a gateway.

---

### TC-14 — Gateway Bridging (`E-2D` / `F/A-18F Blk III`)
- **Status**: `PASS`
- **Setup**: Same as **TC-13**, but add an active `E-2D` (or `F/A-18F Block III`) to the taskforce.
- **Steps**: Order Flight 2 `F-35` (EMCON) to shoot at Flight 1 lead's target.
- **Expected Result**:
  - `TaskforceHasActiveGateway` upgrades link to `Tier1_FullCec`.
  - Separate-flight `F-35` can now fire in EMCON; bottom bar shows `(GW)`.
- **Result / Notes**:
  - **Confirmation (`2026-11-22`)**: User confirmed passed in-game following the TC-13/TC-14 fixes (live formation checks in `IsSameFlightGroup`, `MADL (GW)` / `(GW)` gateway protocol formatting, and airborne + radio-horizon reach enforcement via `IsGatewayInRadioReach` with `requireRadarOn: false` so an EMCON `E-2D` / gateway still bridges within radio reach).
  - *Previous pre-fix run (`2026-11-22`, `media_1795297311153_fb70d43b.jpg`)*: Shooter could fire with an `E-2D` present even with AWACS radar OFF, but hover tooltip displayed `CEC Track: F-35C VFA-101 'Grim Reapers'-2 [Link 16 | Tier 1]` instead of `MADL` / `(GW)`.

---

### TC-15 — Datalink Horizon & Reach Limits + Ship SATCOM
- **Status**: `Pending`
- **Setup**:
  1. Air-to-air: `F/A-18F` and `E-2D` separated by **> 300 nmi**.
  2. Ship-to-ship: two `DDG-51`s separated by **> 150 nmi**.
- **Steps**: Check `Ctrl+F10` overlay `Best Consort` reach & attempt remote engagement.
- **Expected Result**:
  - Aircraft pair > 300 nmi: **out of datalink reach** (blocked).
  - Ship-to-ship pair > 150 nmi: **in reach** (unlimited naval SATCOM rule).
  - `eu_GPS_Receiver` / SATCOM unit: planetary reach (`SatcomGlobal`).
- **Result / Notes**: _Not tested yet._

---

## Group E — In-Flight Handoff, Pitbull Terminal & Survivor Takeover

### TC-16 — Pitbull Terminal (`LocalTerminalOnly=True`) Clean Log
- **Status**: `PASS`
- **Setup**: **TC-01** (`F/A-18F` EMCON + `E-2D` + `AIM-174B` or `AIM-120D3`) run all the way to impact.
- **Steps**: Watch missile cross `TerminalApproachDist` (18 nmi for `AIM-174B`) until impact, then check `LogOutput.log`.
- **Expected Result**:
  - Missile goes pitbull (`ARH` active seeker) and hits target.
  - **Zero** `[CEC] Handoff: 'E-2D' -> 'E-2D'` spam lines in `LogOutput.log`.
- **Result / Notes**: User confirmed passed (`2026-11-22`).

---

### TC-17 — Sunken Launcher Survivor Takeover
- **Status**: `PASS`
- **Setup**: Rear `DDG-51` fires `SM-6` / `SM-2` guided by own radar; forward `DDG-51` or `E-2D` also tracks target.
- **Steps**: Destroy/sink the launching `DDG-51` via `F10` Debug Menu while missiles are in mid-flight.
- **Expected Result**:
  - Airborne missiles are **not** destroyed as orphans (`IsUnguidableOrphan = false`).
  - Consort/AWACS takes over guidance (`[CEC] Handoff:`) and missiles hit target.
- **Result / Notes**: User confirmed passed (`2026-11-22`).

---

### TC-18 — Consort Guidance Hull Raycast Check
- **Status**: `PASS`
- **Setup**: Forward `DDG-51` guiding a rear ship's missile while maneuvering at various bearings relative to the target.
- **Steps**: Watch missile link status across multiple headings of the guiding consort.
- **Expected Result**:
  - No false `DL LOST` caused by the guiding consort's own hull colliders (`_launchPlatform` aliasing in `OnFixedUpdate` / `OnUpdateEveryFrame`).
- **Result / Notes**: User confirmed passed (`2026-11-22`).

---

## Group F — Ecosystem, UI & Autonomous AI Checks

### TC-19 — `ExtendedGuidance` ON vs. OFF Parity
- **Status**: `Pending`
- **Setup**: Run **TC-01** (`AIM-174B` EMCON) and **TC-06** (`SM-2` Forward Pass) once with `ExtendedGuidance` **enabled**, and once with `ExtendedGuidance` **disabled**.
- **Steps**: Verify startup log, pre-launch firing, mid-course status bar, and terminal illumination.
- **Expected Result**:
  - **EG ON**: Log shows `[CEC] Detected ExtendedGuidance assembly` + `hybrid CEC + ExtendedGuidance realism bridge enabled` at startup; `SM-2` uses `DatalinkCarrier` mid-course and promotes consort CW illuminator only when terminal illumination is due.
  - **EG OFF**: Clean standalone mode with zero exceptions.
- **Result / Notes**: _Not tested yet._

---

### TC-20 — Autonomous "Weapons Free" + Zero Map Clutter + F10 Overlay
- **Status**: `Pending`
- **Setup**: Tier 1 `DDG-51` (`EMCON`, `Weapons Free`) + `E-2D` (radar ON) vs. positively identified hostile aircraft; plus a Tier 2 ship (`EMCON`, `Weapons Free`).
- **Steps**: Let AI run without manual orders; inspect map labels and press `Ctrl + F10`.
- **Expected Result**:
  - Tier 1 ship autonomously fires OTH/EMCON once target is positively identified.
  - Tier 2 ship holds fire until local radar lock.
  - Normal map contact labels remain **100% clean** (tags appear only on Attack Hover).
  - `Ctrl + F10` overlay shows accurate Tier, Datalinks, EMCON, Consort, and Hover Tag with zero log errors.
- **Result / Notes**: _Not tested yet._
