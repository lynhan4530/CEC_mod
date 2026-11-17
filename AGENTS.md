# Sea Power CEC (`CecMod`) — AI Agent Knowledge Base & Rules

> **Auto-Loaded Workspace Entry Point**: Read this file first in every session. Follow the routing table below to read only the specific `docs/kb/` document needed for the current task.

## 1. Project Identity & Core Vision ("Why We Made This Mod")
- **Mod**: `Sea Power CEC` (`CecMod.dll`, GUID `com.cecmod.seapower`, version `0.4.1`)
- **Target Game**: *Sea Power: Naval Combat in the Missile Age* (`Seapower-Scripts.dll`, Unity 2022.3 Mono, .NET Standard 2.1, BepInEx 5.4.21, Harmony 2.2, Anchor Chain Workshop ID `3380210757`)
- **Why This Mod Exists**:
  - The *Sea Power* Steam Workshop has many modern mods (such as *Pacific 2027 - Dynamic Campaign*, *Euromod*, and modern ship/aircraft/weapon packs) featuring platforms, radars, and weapons that can perform **Cooperative Engagement Capability (CEC)** in the real world.
  - However, *Sea Power*'s vanilla engine was designed around 1980s Cold War local-FCR combat (plus a 1.0 nmi land-SAM radar truck stub) and lacks true ship-to-ship and airborne cooperative guidance.
  - `CecMod` fills this gap with **zero `.ini` file overrides**, so all compatible vanilla and Workshop units perform authentic networked warfare out of the box.

## 2. Non-Negotiable Rules (HARD GATES)

1. **Strict Real-World Capability First**:
   - Every platform, radar, datalink, and munition **must** reflect its verified real-world capability and military doctrine.
   - Cold War units/missiles stay restricted to historical local FCR / Link 11 behavior (Tier 3). Link 16 / HN-900 / Fort-TVM units (Tier 2) and USG-2/3 CEC / NIFC-CA / JSIDLS "A-Shoot-B-Guide" units (Tier 1) behave strictly as they do in reality.
2. **Zero Assumptions & Zero Fabrication**:
   - **Under NO circumstances** is the AI allowed to assume, guess, or fabricate knowledge, real-world military specifications, sensor/weapon data, or game engine code behavior.
   - If the AI does not know or is not 100% certain, it is explicitly allowed and required to **ask the user as many questions, and as many times, as needed**.
3. **Realism vs. Game Engine / CPU Trade-Off Protocol**:
   - *Sea Power* is still a game with Unity/Mono and per-frame CPU budget limitations (e.g., zero heap allocations in hot physics/render loops, `O(1)` static lookups).
   - Whenever real-world physics or doctrine collides with a game engine limitation or computational constraint, the AI **must stop, explain the constraint, and ask the user with concrete options/suggestions on which direction to go next** before implementing.
4. **Self-Maintaining Knowledge Base**:
   - Whenever code in `CecMod/`, datasets in `data/cec_tables/`, or scripts in `tools/` are modified, the AI **must** update the corresponding `docs/kb/*.md` file(s) and `CHANGELOG.md` in the same task before claiming completion.

## 3. Knowledge Base Routing Table (Read Only What You Need)

| If your task involves... | Read this file |
|---|---|
| Doctrine, 4-Tier model, 10x10 datalink matrix, Forward Pass SARH vs. ARH, stealth flight isolation, EMCON, SATCOM | [01_VISION_AND_DOCTRINE.md](file:///G:/Project/CEC_mod/docs/kb/01_VISION_AND_DOCTRINE.md) |
| C# architecture, `CecCapability`, `CecDataTables.g.cs`, Harmony patches (`LaunchTimeCecPatch`, `RemoteEngagementPatch`, `HandoffPatch`, `DatalinkUiPatch`), F10 debug overlay | [02_MOD_ARCHITECTURE.md](file:///G:/Project/CEC_mod/docs/kb/02_MOD_ARCHITECTURE.md) |
| Decompiled `Seapower-Scripts.dll` classes in `scratch/`, vanilla pre-launch/guidance/LOS/channel mechanics, vanilla engine bugs | [03_GAME_ENGINE_REFERENCE.md](file:///G:/Project/CEC_mod/docs/kb/03_GAME_ENGINE_REFERENCE.md) |
| Anchor Chain loader (`AnchorChainEntry.cs`), `ExtendedGuidance.dll` reflection bridge (`EgBridge.cs`), `Pacific 2027` normalization, `eu_GPS_Receiver` | [04_ECOSYSTEM_AND_BRIDGES.md](file:///G:/Project/CEC_mod/docs/kb/04_ECOSYSTEM_AND_BRIDGES.md) |
| Editing `data/cec_tables/*.csv`, running `tools/generate_cec_tables.py`, building & staging (`stage_workshop.ps1`), active roadmap (`TASK_DYNAMIC_RCS.md`) | [05_WORKFLOWS_AND_ROADMAP.md](file:///G:/Project/CEC_mod/docs/kb/05_WORKFLOWS_AND_ROADMAP.md) |

## 4. Repository Quick Map

- `CecMod/` — C# mod source (`Plugin.cs`, `AnchorChainEntry.cs`, `CecCapability.cs`, `CecDataTables.g.cs`, `EgBridge.cs`, `CecDebugOverlay.cs`, `Patches/*.cs`)
- `data/cec_tables/` — Verified CSV source-of-truth datasets (`00_Glossary.csv` through `06_Missiles.csv`)
- `tools/` — Python table generator (`generate_cec_tables.py`)
- `docs/kb/` — Current AI knowledge base (`01` through `05`)
- `docs/archive/` — Historical completed plans/specs (do **not** treat as current implementation)
- `scratch/` — Decompiled `Seapower-Scripts.dll` C# source & `Decompiler/` tool
- `workshop/` & `dist/` — Steam Workshop metadata/assets (`_info.ini`, `preview.jpg`, `WORKSHOP_DESCRIPTION.bbcode`) and packaged build output
- [stage_workshop.ps1](file:///G:/Project/CEC_mod/stage_workshop.ps1) — Release build, `dist/CecMod` packaging, and `StreamingAssets` staging script
- [CHANGELOG.md](file:///G:/Project/CEC_mod/CHANGELOG.md) — Version history
- [TASK_DYNAMIC_RCS.md](file:///G:/Project/CEC_mod/TASK_DYNAMIC_RCS.md) — Approved blueprint for the upcoming Dynamic RCS module (not yet implemented in `CecMod/`)
