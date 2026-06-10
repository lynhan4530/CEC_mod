# Sea Power Codebase Knowledge Base

This file documents the structures, relationships, and code logic found within `Seapower-Scripts.dll` for missile guidance, weapon systems, sensor networking, and UI models.

Decompiled source code files for each of these classes are available in the local scratch directory:
`g:\Project\SeaPowerDataExtraction\scratch\`

---

## Class Directory & Index

| Class Name | File Link | Description |
|------------|-----------|-------------|
| `SeaPower.Missile` | [Missile.cs](file:///g:/Project/SeaPowerDataExtraction/scratch/Missile.cs) | Core class for guided missile flight stages, seeking, and update loops. |
| `SeaPower.WeaponBase` | [WeaponBase.cs](file:///g:/Project/SeaPowerDataExtraction/scratch/WeaponBase.cs) | Base class for launched ammunition (missiles, torpedoes, guns). |
| `SeaPower.WeaponSystem` | [WeaponSystem.cs](file:///g:/Project/SeaPowerDataExtraction/scratch/WeaponSystem.cs) | Manages weapon launchers, associated FCR tracking systems, and target alignment. |
| `SeaPower.WeaponSystemLauncher` | [WeaponSystemLauncher.cs](file:///g:/Project/SeaPowerDataExtraction/scratch/WeaponSystemLauncher.cs) | Implements launcher fire loops, salvo controls, and external guidance triggers. |
| `SeaPower.SensorSystem` | [SensorSystem.cs](file:///g:/Project/SeaPowerDataExtraction/scratch/SensorSystem.cs) | Base class for onboard sensors (radars, sonars, ESM, optical). |
| `SeaPower.SensorSystemsLink` | [SensorSystemsLink.cs](file:///g:/Project/SeaPowerDataExtraction/scratch/SensorSystemsLink.cs) | Manages datalink networking, sharing contact returns, and laser designators. |
| `SeaPower.Taskforce` | [Taskforce.cs](file:///g:/Project/SeaPowerDataExtraction/scratch/Taskforce.cs) | Represents task forces, storing collections of vessels, subs, and aircraft. |
| `SeaPower.RadarParameters` | [RadarParameters.cs](file:///g:/Project/SeaPowerDataExtraction/scratch/RadarParameters.cs) | Holds properties parsed from `sensors.ini` for radar systems. |
| `SeaPower.WeaponParameters` | [WeaponParameters.cs](file:///g:/Project/SeaPowerDataExtraction/scratch/WeaponParameters.cs) | Holds weapon properties parsed from unit `.ini` file weapon system sections. |
| `SeapowerUI.ViewModels.ObjectMissileControlViewModel` | [ObjectMissileControlViewModel.cs](file:///g:/Project/SeaPowerDataExtraction/scratch/ObjectMissileControlViewModel.cs) | Viewmodel controlling the HUD missile status panel and datalink name text. |

---

## 1. Weapon Guidance & Channel Allocation

### Guidance Association
- When a weapon (e.g. missile) is launched, the launcher calls `WeaponBase.activateGuidingSystem()` (see [WeaponBase.cs:L740](file:///g:/Project/SeaPowerDataExtraction/scratch/WeaponBase.cs#L740)).
- This method scans `_weaponSystem._vwp._associatedSensors` for a compatible `SensorSystemRadar` (FCR).
- If it finds one with available channels, it registers the FCR in `_guidingRadarSystem` and consumes one target/weapon channel on that radar:
  `_guidingRadarSystem.TryToAddTarget(...)`
  `_guidingRadarSystem.TryToAddWeapon(...)`
- If no compatible radar is found, it marks the weapon as `ConnectionLost = True` and `ConnectionLostForever = True`.

### Channel Saturation Rules
Radars track concurrent guided weapons using:
- `sensorSystemRadar.HasFreeWeaponChannels()` (see [SensorSystem.cs:L1107](file:///g:/Project/SeaPowerDataExtraction/scratch/SensorSystem.cs#L1107)):
  Checks if `_associatedWeapons.Count < _weaponChannels`.
- `sensorSystemRadar.HasFreeTargetChannels()` (see [SensorSystem.cs:L1065](file:///g:/Project/SeaPowerDataExtraction/scratch/SensorSystem.cs#L1065)):
  Checks if `_associatedTargets.Count < _targetChannels`.

---

## 2. Remote / External Guidance Logic

Land-based SAM systems (like HAWK and SA-6) utilize the game's built-in external guidance logic. It checks for guidance radars on separate vehicles (e.g., radar trucks).

- **`CheckExternalRadarGuidance()`** (see [WeaponSystem.cs:L1700](file:///g:/Project/SeaPowerDataExtraction/scratch/WeaponSystem.cs#L1700)):
  - Checks if `_vwp._externalGuidingSystems.Count > 0`.
  - Checks if any current associated sensors are free. If not, calls `SearchForExternalGuidance()`.
- **`SearchForExternalGuidance()`** (see [WeaponSystem.cs:L1722](file:///g:/Project/SeaPowerDataExtraction/scratch/WeaponSystem.cs#L1722)):
  - Loops over `_baseObject._taskforce.TaskforceObjects`.
  - Checks if the object is within `_vwp._externalSensorSearchRadius` (which defaults to `27.559f` or 1.0 nmi).
  - Searches the unit's `_obp._sensorSystems` for a radar matching any name in the launcher's `_externalGuidingSystems` list.
  - Adds the remote radar to the launcher's `_associatedSensors` list.

---

## 3. Missile Flight Update & LOS

During guided flight, `Missile.OnUpdateEveryFrame()` runs checking logic (see [Missile.cs:L1836](file:///g:/Project/SeaPowerDataExtraction/scratch/Missile.cs#L1836)):
- It checks if the guiding radar is on: `_guidingRadarSystem.IsOn.Value`.
- It performs raycasts from the radar mount position to the target to verify line of sight:
  `Physics.RaycastNonAlloc(_guidingRadarSystem._mountObject.transform.position, direction, ...)` (see [Missile.cs:L3857](file:///g:/Project/SeaPowerDataExtraction/scratch/Missile.cs#L3857)).
- Computes radar horizon between the guiding radar and target altitude:
  `RadarCalculator.getRadarHorizon(_guidingRadarSystem._mountObject.transform.position.y, target.VisibleAltitude, ...)` (see [Missile.cs:L3842](file:///g:/Project/SeaPowerDataExtraction/scratch/Missile.cs#L3842)).
- **Result**: Guidance calculations naturally use the *guiding ship's* spatial position and elevation, meaning CEC works correctly out of the box once the sensor system is assigned.

---

## 4. UI Datalink Display

The HUD panel displays datalink telemetry for in-flight missiles using `ObjectMissileControlViewModel` (see [ObjectMissileControlViewModel.cs](file:///g:/Project/SeaPowerDataExtraction/scratch/ObjectMissileControlViewModel.cs)).
- **`GoodConnection(Missile m)`** (see [ObjectMissileControlViewModel.cs:L102](file:///g:/Project/SeaPowerDataExtraction/scratch/ObjectMissileControlViewModel.cs#L102)):
  - Fetches the string `Status_Datalink` (localized "Data Link: ${ParentName}").
  - Replaces `${ParentName}` with the launching ship's name: `m._launchPlatform.getName()`.
- **Mod Target**: Overriding this viewmodel using Harmony allows us to replace `${ParentName}` with a path representation `m._launchPlatform.getName() -> m._guidingRadarSystem._baseObject.getName()`.
