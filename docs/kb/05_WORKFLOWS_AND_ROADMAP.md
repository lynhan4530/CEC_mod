# 05 — Developer Workflows & Roadmap

## 1. Updating Unit, Radar, Missile, or Matrix Data

The authoritative real-world capability datasets live in [data/cec_tables/](file:///G:/Project/CEC_mod/data/cec_tables):
- `00_Glossary.csv` — Reference definitions
- `01_Core_CEC_matrix.csv` — Core architecture reference
- `02_Datalink_Compatibility_Matrix.csv` — 10x10 standard-to-standard interaction matrix
- `03_Ships.csv` — 344 ships
- `04_Aircraft.csv` — 132 aircraft
- `05_Radars.csv` — 287 radars
- `06_Missiles.csv` — 232 missiles

### Workflow
1. Edit the CSV files in `data/cec_tables/` (**never** edit [CecDataTables.g.cs](file:///G:/Project/CEC_mod/CecMod/CecDataTables.g.cs) manually).
2. Validate CSV parsing and matrix completeness:
   ```powershell
   python G:\Project\CEC_mod\tools\generate_cec_tables.py --verify
   ```
   Confirm `Verification: 0 unmapped entities, 0 missing matrix pairs.`
3. Regenerate `CecMod/CecDataTables.g.cs`:
   ```powershell
   python G:\Project\CEC_mod\tools\generate_cec_tables.py
   ```
4. **Known Generator Pitfall**: In `tools/generate_cec_tables.py` (`parse_guidance_family`), `"SARH"` **must** be checked before `"ARH"` because `"ARH"` is a substring of `"SARH"` (fixed in v0.4.1).

## 2. Building & Staging the Mod

- **Release Compile Only**:
  ```powershell
  dotnet build G:\Project\CEC_mod\CecMod\CecMod.csproj -c Release
  ```
  Override game path if needed via `-p:GameDir="..."` (default: `G:\Program Files (x86)\Steam\steamapps\common\Sea Power`).
- **Build + Package `dist\CecMod\` + Stage to Sea Power `StreamingAssets`**:
  ```powershell
  powershell -ExecutionPolicy Bypass -File G:\Project\CEC_mod\stage_workshop.ps1
  ```
  `stage_workshop.ps1` copies `CecMod.dll`, `CecMod.pdb`, `workshop/_info.ini`, and `workshop/preview.jpg` (enforcing Sea Power's `< 1,000,000` byte Workshop preview limit) into `dist/CecMod` and `<GameDir>\Sea Power_Data\StreamingAssets\CecMod`.

## 3. In-Game Verification & Logging
- Open Sea Power's Developer Menu (`F10`) and click **CEC Datalink** (or **CEC** on the top developer toolbar, or press `Ctrl + F10`) to open `CecDebugOverlay` and inspect live Shooter Tier, Datalinks, EMCON state, Consort reach, and Hover Telemetry.
- Inspect `BepInEx/LogOutput.log` for `[CEC]` runtime entries (`Launch-on-Remote`, `Handoff`, `Terminal-Illum Handoff`, `Detected ExtendedGuidance assembly`).

## 4. Roadmap & Active Specs
- **Current Release (`v0.4.1`)**: 4-Tier CEC qualification engine, 10x10 datalink matrix, Project Mountain Top Forward Pass SARH, ship-to-ship SATCOM reach, full `ExtendedGuidance` pre-launch/mid-course/terminal compatibility bridge, contextual hover telemetry, and `CecDebugOverlay`.
- **Planned / Approved Next Module (Not Yet Implemented in `CecMod/`)**:
  - [TASK_DYNAMIC_RCS.md](file:///G:/Project/CEC_mod/TASK_DYNAMIC_RCS.md) — **Dynamic Radar Cross Section (RCS) Engine**: Event-driven external weapon & pylon RCS delta on aircraft, incremental RCS shedding on weapon release, residual pylon floor RCS, 3D yaw/pitch/bank aspect projection, stealth weapon bay door bloom, and FCR aspect correction.
