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
        internal struct PreLaunchEmconState
        {
            public List<SensorSystemRadar> OffLocalRadars;
            public bool WasEmcon;
        }

        private static readonly AccessTools.FieldRef<WeaponSystem, float> HorizonRangeRef =
            AccessTools.FieldRefAccess<WeaponSystem, float>("_horizonRange");

        /// <summary>
        /// Replaces vanilla <see cref="WeaponSystem.CheckExternalRadarGuidance"/>.
        /// Uses <see cref="CecCapability.EnsureConsortRadarsFresh"/> (0.25s retry while awaiting an
        /// active remote radar, 5.0s steady-state refresh once injected).
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

                CecCapability.EnsureConsortRadarsFresh(__instance);
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
            out PreLaunchEmconState __state)
        {
            __state = default;
            try
            {
                if (__instance == null || targetObject == null || __instance._vwp == null)
                    return;

                ObjectBase self = __instance._baseObject;
                if (self == null)
                    return;

                AmmunitionParameters ap = CecCapability.ResolveWeaponAmmunitionParameters(__instance);
                CecCapability.EnsureConsortRadarsFresh(__instance, ap);
                __state = CapturePreLaunchEmconState(__instance, self, ap, targetObject);
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
            PreLaunchEmconState __state)
        {
            try
            {
                if (__instance == null || targetObject == null)
                    return;

                ObjectBase self = __instance._baseObject;
                AmmunitionParameters ap = CecCapability.ResolveWeaponAmmunitionParameters(__instance);

                if (self != null && ap != null && CecCapability.CanPerformLaunchOnRemote(self, ap, targetObject))
                {
                    // Do not clamp _currentMaxLaunchRange to the firing ship's local radar horizon
                    // when remote CEC/AWACS or Mountain Top Forward Pass coverage is valid.
                    HorizonRangeRef(__instance) = 0f;
                }

                RestorePreLaunchEmconState(self, __state);

                if (self != null && ap != null && CecCapability.CanPerformLaunchOnRemote(self, ap, targetObject))
                {
                    EnsureRemotePreLaunchTargetAssigned(__instance, targetObject, ap);
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
            out PreLaunchEmconState __state)
        {
            __state = default;
            try
            {
                if (__instance == null || targetObject == null || __instance._vwp == null)
                    return;

                ObjectBase self = __instance._baseObject;
                if (self == null)
                    return;

                AmmunitionParameters ap = CecCapability.ResolveWeaponAmmunitionParameters(__instance);
                CecCapability.EnsureConsortRadarsFresh(__instance, ap);
                __state = CapturePreLaunchEmconState(__instance, self, ap, targetObject);
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
            PreLaunchEmconState __state)
        {
            try
            {
                if (__instance == null || targetObject == null)
                    return;

                ObjectBase self = __instance._baseObject;
                AmmunitionParameters ap = CecCapability.ResolveWeaponAmmunitionParameters(__instance);

                if (self != null && ap != null && CecCapability.CanPerformLaunchOnRemote(self, ap, targetObject))
                {
                    HorizonRangeRef(__instance) = 0f;
                }

                RestorePreLaunchEmconState(self, __state);

                if (self != null && ap != null && CecCapability.CanPerformLaunchOnRemote(self, ap, targetObject))
                {
                    EnsureRemotePreLaunchTargetAssigned(__instance, targetObject, ap);
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] ActivatePreLaunchGuidanceHardpoint_Postfix exception: {ex}");
            }
        }

        internal static PreLaunchEmconState CapturePreLaunchEmconState(
            WeaponSystem ws,
            ObjectBase self,
            AmmunitionParameters ap,
            ObjectBase targetObject)
        {
            var state = default(PreLaunchEmconState);
            if (ap == null)
                ap = CecCapability.ResolveWeaponAmmunitionParameters(ws);

            if (ap == null
                || self == null
                || (ws?._vwp?._associatedSensors == null && self._obp?._sensorSystems == null)
                || !CecCapability.IsPlatformInAirRadarEmcon(self)
                || !CecCapability.CanPerformLaunchOnRemote(self, ap, targetObject))
            {
                return state;
            }

            var offLocalRadars = new List<SensorSystemRadar>();
            if (ws?._vwp?._associatedSensors != null)
            {
                foreach (SensorSystem s in ws._vwp._associatedSensors)
                {
                    if (s is SensorSystemRadar radar
                        && radar._baseObject == self
                        && !radar.IsOn.Value
                        && !radar._wasSetOn
                        && !offLocalRadars.Contains(radar))
                    {
                        offLocalRadars.Add(radar);
                    }
                }
            }

            if (self._obp?._sensorSystems != null)
            {
                foreach (SensorSystem s in self._obp._sensorSystems)
                {
                    if (s is SensorSystemRadar radar
                        && radar._baseObject == self
                        && !radar.IsOn.Value
                        && !radar._wasSetOn
                        && !offLocalRadars.Contains(radar))
                    {
                        offLocalRadars.Add(radar);
                    }
                }
            }

            if (offLocalRadars.Count > 0)
            {
                state.OffLocalRadars = offLocalRadars;
                state.WasEmcon = self.Emcon.Value;
            }

            return state;
        }

        internal static void RestorePreLaunchEmconState(ObjectBase self, PreLaunchEmconState state)
        {
            if (state.OffLocalRadars != null)
            {
                foreach (SensorSystemRadar r in state.OffLocalRadars)
                {
                    if (r != null && (r.IsOn.Value || r._wasSetOn || (self?._sensorsToTurnOff != null && self._sensorsToTurnOff.Contains(r))))
                    {
                        r.Disable(allowMessage: false);
                        r.IsOn.Value = false;
                        self?._sensorsToTurnOff?.Remove(r);
                        r._preLaunchTargetObject = null;
                    }
                }
            }

            if (state.WasEmcon && self != null && !self.Emcon.Value)
            {
                self.Emcon.Value = true;
            }
        }

        private static void EnsureRemotePreLaunchTargetAssigned(
            WeaponSystem instance,
            ObjectBase targetObject,
            AmmunitionParameters ap)
        {
            if (instance?._vwp?._associatedSensors == null || targetObject == null)
                return;

            if (ap != null)
                CecCapability.SyncConsortIlluminatorsToAmmo(instance, ap);

            // Check if any currently ON associated sensor already has _preLaunchTargetObject == targetObject
            foreach (SensorSystem sensor in instance._vwp._associatedSensors)
            {
                if (sensor != null
                    && sensor.IsOn.Value
                    && !sensor.IsDestroyed
                    && !sensor.Inoperable.Value
                    && sensor._preLaunchTargetObject == targetObject)
                {
                    instance._sensorPreLaunchAligned = true;
                    return;
                }
            }

            // Otherwise, assign targetObject to the first active associated sensor (such as an injected remote AWACS/consort radar)
            foreach (SensorSystem sensor in instance._vwp._associatedSensors)
            {
                if (sensor == null || !sensor.IsOn.Value || sensor.IsDestroyed || sensor.Inoperable.Value)
                    continue;
                if (sensor is SensorSystemRadar assocRadar
                    && (CecCapability.IsAwacsRadar(assocRadar) || CecCapability.IsCecMultifunctionRadar(assocRadar)))
                {
                    CecCapability.EnsureMidCourseChannels(assocRadar);
                }
                if (ap != null && !sensor.CanGuideAmmo(ap))
                    continue;
                if (!sensor.HasFreeTargetChannels(targetObject) || !sensor.HasFreeWeaponChannels())
                    continue;
                if (sensor is SensorSystemRadar radar && !CecCapability.RadarCanSee(radar, targetObject))
                    continue;

                sensor.SetPrelaunchObject(targetObject);
                sensor._restPositionReached = false;
                instance._sensorPreLaunchAligned = true;
                break;
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
                {
                    if (!currentIsLocal)
                    {
                        if (CecCapability.IsAwacsRadar(current) || CecCapability.IsCecMultifunctionRadar(current))
                            CecCapability.EnsureMidCourseChannels(current);
                        EgBridge.SetActiveEgIlluminator(m, current);
                        CecPlugin.Log.LogInfo(
                            $"[CEC] Launch-on-Remote: '{m.getObjectIniName()}' (#{m.UniqueID}) from '{launcher?.getName() ?? "?"}' " +
                            $"guided by remote '{current._baseObject?.getName() ?? "?"}' ({current._systemName}) " +
                            $"[{CecCapability.GetPlatformProfile(launcher).CecTier} -> {CecCapability.GetPlatformProfile(current._baseObject).CecTier}].");
                    }
                    return;
                }

                SensorSystemRadar bestRemote = null;
                float bestDistSq = float.MaxValue;

                foreach (SensorSystem s in m._weaponSystem._vwp._associatedSensors)
                {
                    if (!(s is SensorSystemRadar candidate) || candidate == current)
                        continue;
                    if (!CecCapability.CanRadarGuideWeapon(candidate))
                        continue;
                    if (CecCapability.IsAwacsRadar(candidate) || CecCapability.IsCecMultifunctionRadar(candidate))
                        CecCapability.EnsureMidCourseChannels(candidate);
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
