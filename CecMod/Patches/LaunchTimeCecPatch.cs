using System;
using System.Collections.Generic;
using HarmonyLib;
using SeaPower;

namespace CecMod.Patches
{
    /// <summary>
    /// Replaces and extends the vanilla <see cref="WeaponSystem.CheckExternalRadarGuidance"/>
    /// stub so any CEC-capable launcher (Vessel or Aircraft) can draw on compatible
    /// consort FCRs and AWACS datalink radars across all 4 doctrinal networks,
    /// including OTH and EMCON (Launch-on-Remote / Engage-on-Remote) engagements.
    /// </summary>
    [HarmonyPatch]
    internal static class LaunchTimeCecPatch
    {
        private const double SearchCadenceSeconds = 5.0;

        private static readonly AccessTools.FieldRef<WeaponSystem, float> HorizonRangeRef =
            AccessTools.FieldRefAccess<WeaponSystem, float>("_horizonRange");

        /// <summary>
        /// Replaces vanilla <see cref="WeaponSystem.CheckExternalRadarGuidance"/>.
        /// Uses <see cref="GameTime.missionElapsedTime"/> and <see cref="WeaponSystem._lastSearchForSensorTime"/>
        /// (<c>double</c> in 0.8.7) to throttle consort/AWACS injection to once every 5s.
        /// </summary>
        [HarmonyPatch(typeof(WeaponSystem), nameof(WeaponSystem.CheckExternalRadarGuidance))]
        [HarmonyPrefix]
        private static bool CheckExternalRadarGuidance_Prefix(WeaponSystem __instance)
        {
            try
            {
                if (__instance == null || __instance._vwp == null)
                    return false;

                ObjectBase self = __instance._baseObject;
                if (!(self is Vessel) && !(self is Aircraft))
                    return false;

                double now = GameTime.missionElapsedTime;
                if (now < __instance._lastSearchForSensorTime)
                    return false;
                __instance._lastSearchForSensorTime = now + SearchCadenceSeconds;

                CecCapability.RefreshConsortRadars(__instance);
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] CheckExternalRadarGuidance_Prefix exception: {ex}");
            }

            // Skip vanilla implementation (which does dangerous _associatedSensors.Clear()).
            return false;
        }

        /// <summary>
        /// In <see cref="WeaponSystemLauncher.OnUpdate"/>, <c>activatePreLaunchGuidance</c> runs
        /// BEFORE <c>CheckExternalRadarGuidance</c>. Ensure consort radars are populated first,
        /// and record whether local radars were intentionally off (EMCON) when remote coverage exists.
        /// </summary>
        [HarmonyPatch(typeof(WeaponSystemLauncher), nameof(WeaponSystemLauncher.activatePreLaunchGuidance))]
        [HarmonyPrefix]
        private static void ActivatePreLaunchGuidance_Prefix(
            WeaponSystemLauncher __instance,
            ObjectBase targetObject,
            out List<SensorSystemRadar> __state)
        {
            __state = null;
            try
            {
                if (__instance == null || targetObject == null || __instance._vwp == null)
                    return;

                ObjectBase self = __instance._baseObject;
                if (self == null)
                    return;

                double now = GameTime.missionElapsedTime;
                if (now >= __instance._lastSearchForSensorTime)
                {
                    __instance._lastSearchForSensorTime = now + SearchCadenceSeconds;
                    CecCapability.RefreshConsortRadars(__instance);
                }

                AmmunitionParameters ap = __instance._ammoForEngage?._ap;
                CecCapability.SyncConsortIlluminatorsToAmmo(__instance, ap);
                if (ap != null
                    && CecCapability.IsPlatformInAirRadarEmcon(self)
                    && CecCapability.CanPerformLaunchOnRemote(self, ap, targetObject))
                {
                    var offLocalRadars = new List<SensorSystemRadar>();
                    foreach (SensorSystem s in __instance._vwp._associatedSensors)
                    {
                        if (s is SensorSystemRadar radar && radar._baseObject == self && !radar.IsOn.Value)
                            offLocalRadars.Add(radar);
                    }
                    if (offLocalRadars.Count > 0)
                        __state = offLocalRadars;
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] ActivatePreLaunchGuidance_Prefix exception: {ex}");
            }
        }

        /// <summary>
        /// Prevents local mast horizon (<c>_horizonRange</c>) from clamping
        /// <c>_currentMaxLaunchRange</c> during OTH remote engagements, and preserves EMCON
        /// if a remote consort/AWACS radar is already guiding.
        /// </summary>
        [HarmonyPatch(typeof(WeaponSystemLauncher), nameof(WeaponSystemLauncher.activatePreLaunchGuidance))]
        [HarmonyPostfix]
        private static void ActivatePreLaunchGuidance_Postfix(
            WeaponSystemLauncher __instance,
            ObjectBase targetObject,
            List<SensorSystemRadar> __state)
        {
            try
            {
                if (__instance == null || targetObject == null)
                    return;

                ObjectBase self = __instance._baseObject;
                AmmunitionParameters ap = __instance._ammoForEngage?._ap;

                if (self != null && ap != null && CecCapability.CanPerformLaunchOnRemote(self, ap, targetObject))
                {
                    // Do not clamp _currentMaxLaunchRange to the firing ship's local radar horizon
                    // when remote CEC/AWACS or Mountain Top Forward Pass coverage is valid.
                    HorizonRangeRef(__instance) = 0f;
                }

                if (__state != null)
                {
                    foreach (SensorSystemRadar r in __state)
                    {
                        if (r != null && r.IsOn.Value && self?._sensorsToTurnOff != null && self._sensorsToTurnOff.Contains(r))
                        {
                            r.Disable(allowMessage: false);
                            self._sensorsToTurnOff.Remove(r);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] ActivatePreLaunchGuidance_Postfix exception: {ex}");
            }
        }

        [HarmonyPatch(typeof(WeaponSystemHardpoint), nameof(WeaponSystemHardpoint.activatePreLaunchGuidance))]
        [HarmonyPrefix]
        private static void ActivatePreLaunchGuidanceHardpoint_Prefix(
            WeaponSystemHardpoint __instance,
            ObjectBase targetObject,
            out List<SensorSystemRadar> __state)
        {
            __state = null;
            try
            {
                if (__instance == null || targetObject == null || __instance._vwp == null)
                    return;

                ObjectBase self = __instance._baseObject;
                if (self == null)
                    return;

                double now = GameTime.missionElapsedTime;
                if (now >= __instance._lastSearchForSensorTime)
                {
                    __instance._lastSearchForSensorTime = now + SearchCadenceSeconds;
                    CecCapability.RefreshConsortRadars(__instance);
                }

                AmmunitionParameters ap = __instance._ammoForEngage?._ap;
                if (ap == null && __instance._weapons != null && __instance._weapons.Count > 0)
                    ap = __instance._weapons[0]?._ap;
                if (ap == null)
                    ap = __instance._ammunitionOnWeapon?._ap;

                CecCapability.SyncConsortIlluminatorsToAmmo(__instance, ap);
                if (ap != null
                    && CecCapability.IsPlatformInAirRadarEmcon(self)
                    && CecCapability.CanPerformLaunchOnRemote(self, ap, targetObject))
                {
                    var offLocalRadars = new List<SensorSystemRadar>();
                    foreach (SensorSystem s in __instance._vwp._associatedSensors)
                    {
                        if (s is SensorSystemRadar radar && radar._baseObject == self && !radar.IsOn.Value)
                            offLocalRadars.Add(radar);
                    }
                    if (offLocalRadars.Count > 0)
                        __state = offLocalRadars;
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] ActivatePreLaunchGuidanceHardpoint_Prefix exception: {ex}");
            }
        }

        [HarmonyPatch(typeof(WeaponSystemHardpoint), nameof(WeaponSystemHardpoint.activatePreLaunchGuidance))]
        [HarmonyPostfix]
        private static void ActivatePreLaunchGuidanceHardpoint_Postfix(
            WeaponSystemHardpoint __instance,
            ObjectBase targetObject,
            List<SensorSystemRadar> __state)
        {
            try
            {
                if (__instance == null || targetObject == null)
                    return;

                ObjectBase self = __instance._baseObject;
                AmmunitionParameters ap = __instance._ammoForEngage?._ap;
                if (ap == null && __instance._weapons != null && __instance._weapons.Count > 0)
                    ap = __instance._weapons[0]?._ap;
                if (ap == null)
                    ap = __instance._ammunitionOnWeapon?._ap;

                if (self != null && ap != null && CecCapability.CanPerformLaunchOnRemote(self, ap, targetObject))
                {
                    HorizonRangeRef(__instance) = 0f;
                }

                if (__state != null)
                {
                    foreach (SensorSystemRadar r in __state)
                    {
                        if (r != null && r.IsOn.Value && self?._sensorsToTurnOff != null && self._sensorsToTurnOff.Contains(r))
                        {
                            r.Disable(allowMessage: false);
                            self._sensorsToTurnOff.Remove(r);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] ActivatePreLaunchGuidanceHardpoint_Postfix exception: {ex}");
            }
        }

        /// <summary>
        /// Ensures <c>WeaponSystem.CheckTargetIsDetectableByFCR</c> checks
        /// <see cref="SensorSystem.CanGuideAmmo"/> on each associated radar, so a synthetic
        /// AWACS datalink radar in <c>_associatedSensors</c> only satisfies FCR checks for
        /// weapons it can actually guide (e.g. ARH+MCC like SM-6, not SARH like SM-2MR).
        /// </summary>
        [HarmonyPatch(typeof(WeaponSystem), "CheckTargetIsDetectableByFCR")]
        [HarmonyPrefix]
        private static bool CheckTargetIsDetectableByFCR_Prefix(
            WeaponSystem __instance,
            ObjectBase.SensorRangeOptions options,
            ref bool __result)
        {
            try
            {
                if (__instance == null || __instance._vwp == null || __instance._targetObject == null)
                    return true;

                AmmunitionParameters ap = __instance._ammoForEngage?._ap;
                if (ap == null && __instance is WeaponSystemHardpoint hp && hp._weapons != null && hp._weapons.Count > 0)
                    ap = hp._weapons[0]?._ap;
                __result = false;
                foreach (SensorSystem associatedSensor in __instance._vwp._associatedSensors)
                {
                    if (associatedSensor is SensorSystemRadar radar
                        && (ap == null || radar.CanGuideAmmo(ap))
                        && radar.HasFreeTargetChannels(__instance._targetObject)
                        && radar.HasFreeWeaponChannels()
                        && __instance.CheckTargetIsDetectableByRadar(radar, __instance._targetObject, options))
                    {
                        __result = true;
                        break;
                    }
                }

                if (!__result
                    && ap != null
                    && __instance._baseObject != null
                    && CecCapability.CanPerformLaunchOnRemote(__instance._baseObject, ap, __instance._targetObject))
                {
                    __result = true;
                }

                return false;
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] CheckTargetIsDetectableByFCR_Prefix exception: {ex}");
                return true;
            }
        }

        /// <summary>
        /// When a missile spawns and runs <c>WeaponBase.ActivateNonLaserGuidanceSystem</c>,
        /// vanilla iterates <c>_associatedSensors</c> in order (local sensors first) without
        /// checking radar LOS to the target or EMCON state. If a local radar was chosen that
        /// cannot see the OTH target (or was only auto-enabled from EMCON while a remote CEC
        /// radar is already tracking the target), rebind immediately to the best remote CEC radar.
        /// Under <c>ExtendedGuidance</c>, preserves active mid-course <c>DatalinkCarrier</c> bindings
        /// (<c>Hooks.PreferDatalink</c>) whenever the carrier wasn't auto-enabled from EMCON and has
        /// LOS to the missile.
        /// </summary>
        [HarmonyPatch(typeof(WeaponBase), "ActivateNonLaserGuidanceSystem")]
        [HarmonyPostfix]
        private static void ActivateNonLaserGuidanceSystem_Postfix(WeaponBase __instance)
        {
            try
            {
                if (!(__instance is Missile m))
                    return;

                ObjectBase target = m.CurrentIntendedTargetObject;
                if (target == null || m._ap == null || m._weaponSystem?._vwp?._associatedSensors == null)
                    return;

                var mProfile = CecCapability.GetMissileProfile(m._ap);
                if (mProfile.CecTier == CecTier.Tier3 || mProfile.CecTier == CecTier.Tier0)
                    return;

                CecCapability.SyncConsortIlluminatorsToAmmo(m._weaponSystem, m._ap);

                SensorSystemRadar current = m._guidingRadarSystem;
                ObjectBase launcher = m._launchPlatform;

                bool currentIsLocal = current != null && current._baseObject == launcher;
                bool currentWasAutoEnabledFromEmcon = currentIsLocal
                    && launcher?._sensorsToTurnOff != null
                    && launcher._sensorsToTurnOff.Contains(current);

                if (EgBridge.IsActive
                    && current != null
                    && !current.IsDestroyed
                    && !current.Inoperable.Value
                    && current.IsOn.Value
                    && !currentWasAutoEnabledFromEmcon
                    && EgBridge.IsDatalinkCarrier(current)
                    && current.CanGuideAmmo(m._ap)
                    && CecCapability.RadarCanReachMissile(current, m))
                {
                    // Keep the mid-course DatalinkCarrier (e.g. eu_AEGIS_BL9) even if target is OTH;
                    // ExtendedGuidance.Runtime.Illuminator / HandoffPatch will reserve terminal CW illumination when due.
                    return;
                }

                bool currentCanSee = current != null
                    && !current.IsDestroyed
                    && !current.Inoperable.Value
                    && current.IsOn.Value
                    && current.CanGuideAmmo(m._ap)
                    && CecCapability.RadarCanSee(current, target);

                if (current != null && currentCanSee && !currentWasAutoEnabledFromEmcon)
                    return;

                SensorSystemRadar bestRemote = null;
                float bestDistSq = float.MaxValue;

                foreach (SensorSystem s in m._weaponSystem._vwp._associatedSensors)
                {
                    if (!(s is SensorSystemRadar candidate) || candidate == current)
                        continue;
                    if (!CecCapability.CanRadarGuideWeapon(candidate))
                        continue;
                    if (!candidate.CanGuideAmmo(m._ap))
                        continue;
                    if (!candidate.HasFreeTargetChannels(target) || !candidate.HasFreeWeaponChannels())
                        continue;
                    if (!CecCapability.RadarCanSee(candidate, target))
                        continue;

                    float distSq = (candidate._baseObject.transform.position - target.transform.position).sqrMagnitude;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestRemote = candidate;
                    }
                }

                if (bestRemote == null)
                    bestRemote = CecCapability.FindHandoffRadar(m);

                if (bestRemote == null)
                    return;

                if (!bestRemote.IsOn.Value)
                    CecCapability.ForceEnableRadarSynchronous(bestRemote);

                if (!bestRemote.IsAimedAtTarget(target))
                    bestRemote.TryToAddTarget(target);
                if (!bestRemote.TryToAddWeapon(m))
                    return;

                if (current != null)
                {
                    current.FreeChannels(target, m);
                    if (currentWasAutoEnabledFromEmcon && !current.isBusy())
                    {
                        current.Disable(allowMessage: false);
                        launcher._sensorsToTurnOff.Remove(current);
                    }
                }

                m._guidingRadarSystem = bestRemote;
                bestRemote._isGuiding = true;
                bestRemote._restPositionReached = false;
                bestRemote._preLaunchTargetObject = null;
                bestRemote.setTargetObject(target);
                m._seeker?.setGuidingSystem(bestRemote);
                m._seeker?.setCurrentTarget(target);
                EgBridge.SetActiveEgIlluminator(m, bestRemote);
                m.ConnectionLost.Value = false;
                m.ConnectionLostForever.Value = false;

                CecPlugin.Log.LogInfo(
                    $"[CEC] Launch-on-Remote: '{m.getObjectIniName()}' from '{launcher?.getName() ?? "?"}' bound directly to " +
                    $"'{bestRemote._baseObject?.getName() ?? "?"}' ({bestRemote._systemName}).");
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] ActivateNonLaserGuidanceSystem_Postfix exception: {ex}");
            }
        }
    }
}
