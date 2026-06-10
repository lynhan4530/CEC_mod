# Sea Power: Cooperative Engagement Capability (CEC) Mod

This document outlines the design, C# class structures, and Harmony patches required to implement true ship-to-ship Cooperative Engagement Capability (CEC) in *Sea Power: Naval Combat in the Missile Age*.

---

## 1. Concept of Operation

When Ship A fires a Semi-Active Radar Homing (SARH) or command-guided missile (like the SM-2) and its local Fire Control Radars (FCRs) are either busy, destroyed, or non-existent, a nearby friendly vessel (Ship B) can provide terminal illumination or mid-course command guidance.

### Requirements:
1. **Datalink Requirement**: Both the launching ship (Ship A) and the guiding ship (Ship B) must have active, datalink-capable radars (`_hasDataLink = True` in `sensors.ini`). If a ship is in EMCON (radars off), it cannot participate in CEC.
2. **Range Limitation**: The distance between Ship A and Ship B must be within the task-force datalink net range (default: 50 nautical miles).
3. **UI Visual Feedback**: On the missile control panel, the datalink status should dynamically update to show `[Launching Ship Name] -> [Guiding Ship Name]` (e.g., `CG-47 Ticonderoga -> DDG-51 Arleigh Burke`).

---

## 2. Technical Patches

We will patch the game's assemblies using a BepInEx plugin and Harmony.

### Patch 1: Dynamic External Guidance Search
We will hook `CheckExternalRadarGuidance()` in `SeaPower.WeaponSystem` to bypass the land-unit-only constraint (which checks `_vwp._externalGuidingSystems.Count > 0`) and allow it to run task-force-wide scans for CEC-capable ships.

* **Target Method**: `SeaPower.WeaponSystem.CheckExternalRadarGuidance()`
* **Patch Type**: `HarmonyPrefix`
* **Implementation Logic**:
  * Skip the check for `_vwp._externalGuidingSystems.Count <= 0`.
  * If local sensors are busy or non-existent, call `SearchForExternalGuidance()`.

### Patch 2: Task-Force Wide Radar Mapping
We will hook `SearchForExternalGuidance()` in `SeaPower.WeaponSystem` to scan the entire task force for datalink-capable fire control radars.

* **Target Method**: `SeaPower.WeaponSystem.SearchForExternalGuidance()`
* **Patch Type**: `HarmonyPrefix` (Override)
* **Implementation Logic**:
  1. Verify the launching ship is datalink-capable (has at least one active sensor with `_hasDataLink == true`).
  2. Iterate through all other vessels in `_baseObject._taskforce._taskforceVessels`.
  3. Verify the remote ship is also datalink-capable.
  4. Ensure the distance is within range (default 50 nmi / 1378f in game units).
  5. Locate any `SensorSystemRadar` on the remote ship compatible with the ammunition's `_compatibleGuidanceSystems`.
  6. Add the remote radar to `_vwp._associatedSensors` and its system name to `_vwp._externalGuidingSystems` dynamically.

### Patch 3: UI Datalink Status Display
We will hook the private methods in `SeapowerUI.ViewModels.ObjectMissileControlViewModel` to update the datalink UI string when a missile is being guided by another ship.

* **Target Methods**:
  * `SeapowerUI.ViewModels.ObjectMissileControlViewModel.GoodConnection(Missile m)`
  * `SeapowerUI.ViewModels.ObjectMissileControlViewModel.BadConnection(Missile m)`
  * `SeapowerUI.ViewModels.ObjectMissileControlViewModel.LostConnection(Missile m)`
* **Patch Type**: `HarmonyPostfix`
* **Implementation Logic**:
  * Check if the missile's guiding system (`m._guidingRadarSystem`) is from a different ship than the launcher (`m._launchPlatform`).
  * If true, replace the datalink string's `${ParentName}` representation with:
    `$"{m._launchPlatform.getName()} -> {m._guidingRadarSystem._baseObject.getName()}"`
