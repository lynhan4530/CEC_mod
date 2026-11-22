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
            public ObjectBase PreviousProtectedUnit;
            public bool PreviousAllowSyntheticDatalink;
        }

        private struct EmconScopeState
        {
            public ObjectBase PrevUnit;
            public bool PrevAllowSynth;
        }

        [ThreadStatic]
        private static ObjectBase _emconProtectedUnit;

        [ThreadStatic]
        private static bool _allowEmconSyntheticDatalink;

        internal static bool AllowEmconSyntheticDatalink => _allowEmconSyntheticDatalink;

        private static readonly AccessTools.FieldRef<WeaponSystem, float> HorizonRangeRef =
            AccessTools.FieldRefAccess<WeaponSystem, float>("_horizonRange");

        internal static ObjectBase PushEmconProtection(ObjectBase unit)
        {
            ObjectBase previous = _emconProtectedUnit;
            if (unit != null)
                _emconProtectedUnit = unit;
            return previous;
        }

        internal static void PopEmconProtection(ObjectBase previous)
        {
            _emconProtectedUnit = previous;
        }

        internal static void PopEmconProtection(ObjectBase previousUnit, bool previousAllowSyntheticDatalink)
        {
            _emconProtectedUnit = previousUnit;
            _allowEmconSyntheticDatalink = previousAllowSyntheticDatalink;
        }

        private static bool IsOwnDatalinkLor(ObjectBase self, AmmunitionParameters ap, ObjectBase target)
        {
            return EgBridge.IsActive
                && self != null
                && ap != null
                && target != null
                && CecCapability.IsSarhMissile(ap)
                && ap._midCourseCorrection != null
                && ap._midCourseCorrection.IsDatalink
                && !CecCapability.HasForwardPassIlluminatorCoverage(self, ap, target)
                && CecCapability.HasOwnShipTerminalIlluminationForLor(self, ap, target);
        }

        /// <summary>
        /// Blocks asynchronous local radar auto-enable requests (<c>turnOffAfterEngage == true</c>)
        /// while evaluating or launching a qualified CEC Launch-on-Remote / Forward Pass engagement
        /// from an EMCON platform, while permitting own-ship synthetic mid-course datalink carriers
        /// during Own-Ship Terminal LOR.
        /// </summary>
        [HarmonyPatch(typeof(SensorSystem), nameof(SensorSystem.Enable))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool SensorSystemEnable_Prefix(SensorSystem __instance, bool turnOffAfterEngage)
        {
            if (turnOffAfterEngage
                && _emconProtectedUnit != null
                && __instance is SensorSystemRadar radar
                && radar._baseObject == _emconProtectedUnit
                && !radar.IsOn.Value)
            {
                if (_allowEmconSyntheticDatalink
                    && CecCapability.IsSyntheticDatalinkSensor(radar)
                    && EgBridge.IsDatalinkCarrier(radar))
                {
                    return true;
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// Protects EMCON during <see cref="ObjectBase.HandleEngageTasks"/> when all radar-triggering
        /// air engagement tasks qualify for Launch-on-Remote.
        /// </summary>
        [HarmonyPatch(typeof(ObjectBase), nameof(ObjectBase.HandleEngageTasks))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void HandleEngageTasks_Prefix(ObjectBase __instance, out EmconScopeState __state)
        {
            __state = new EmconScopeState
            {
                PrevUnit = _emconProtectedUnit,
                PrevAllowSynth = _allowEmconSyntheticDatalink
            };
            try
            {
                if (__instance == null
                    || __instance._currentEngageTasks == null
                    || __instance._currentEngageTasks.Count == 0
                    || !CecCapability.IsPlatformInAirRadarEmcon(__instance))
                {
                    return;
                }

                var profile = CecCapability.GetPlatformProfile(__instance);
                bool hasSatcom = (profile.Datalinks & DatalinkStandard.SatcomUplink) != 0;
                if (profile.CecTier != CecTier.Tier1 && !hasSatcom)
                    return;

                bool anyQualified = false;
                bool allowOwnSynth = false;
                var tasks = __instance._currentEngageTasks;
                for (int i = 0; i < tasks.Count; i++)
                {
                    EngageTask task = tasks[i];
                    ObjectBase target = task?._targetObject;
                    AmmunitionParameters ap = task?._ammunition?._ap;
                    if (target == null || ap == null)
                        continue;

                    bool isSarhOrCmd = ap.GuidanceTypeForTarget(target) == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming
                        || ap._midCourseCorrection == AmmunitionParameters.MidCourseCorrection.RadioCommand;
                    bool isAirMissileOrProj = target.isAirTarget()
                        && (ap._type == Ammunition.Type.Missile || ap._type == Ammunition.Type.Projectile);
                    if (!isSarhOrCmd && !isAirMissileOrProj)
                        continue;

                    if (!target.isAirTarget() || !CecCapability.CanPerformLaunchOnRemote(__instance, ap, target))
                        return;

                    anyQualified = true;
                    if (IsOwnDatalinkLor(__instance, ap, target))
                        allowOwnSynth = true;
                }

                if (anyQualified)
                {
                    _emconProtectedUnit = __instance;
                    _allowEmconSyntheticDatalink = allowOwnSynth;
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] HandleEngageTasks_Prefix exception: {ex}");
            }
        }

        [HarmonyPatch(typeof(ObjectBase), nameof(ObjectBase.HandleEngageTasks))]
        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        private static Exception HandleEngageTasks_Finalizer(EmconScopeState __state, Exception __exception)
        {
            _emconProtectedUnit = __state.PrevUnit;
            _allowEmconSyntheticDatalink = __state.PrevAllowSynth;
            return __exception;
        }

        /// <summary>
        /// Protects EMCON during <see cref="WeaponSystemLauncher.OnUpdate"/> (which directly calls
        /// <c>associatedSearchSensor.Enable(true, true)</c> for SARH and RadioCommand launches).
        /// </summary>
        [HarmonyPatch(typeof(WeaponSystemLauncher), nameof(WeaponSystemLauncher.OnUpdate))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void WeaponSystemLauncherOnUpdate_Prefix(WeaponSystemLauncher __instance, out EmconScopeState __state)
        {
            __state = new EmconScopeState
            {
                PrevUnit = _emconProtectedUnit,
                PrevAllowSynth = _allowEmconSyntheticDatalink
            };
            try
            {
                if (__instance == null
                    || !__instance._executingEngageTask
                    || __instance._baseObject == null
                    || __instance._targetObject == null
                    || !__instance._targetObject.isAirTarget()
                    || !CecCapability.IsPlatformInAirRadarEmcon(__instance._baseObject))
                {
                    return;
                }

                AmmunitionParameters ap = CecCapability.ResolveWeaponAmmunitionParameters(__instance);
                if (ap != null && CecCapability.CanPerformLaunchOnRemote(__instance._baseObject, ap, __instance._targetObject))
                {
                    _emconProtectedUnit = __instance._baseObject;
                    _allowEmconSyntheticDatalink = IsOwnDatalinkLor(__instance._baseObject, ap, __instance._targetObject);
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] WeaponSystemLauncherOnUpdate_Prefix exception: {ex}");
            }
        }

        [HarmonyPatch(typeof(WeaponSystemLauncher), nameof(WeaponSystemLauncher.OnUpdate))]
        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        private static Exception WeaponSystemLauncherOnUpdate_Finalizer(EmconScopeState __state, Exception __exception)
        {
            _emconProtectedUnit = __state.PrevUnit;
            _allowEmconSyntheticDatalink = __state.PrevAllowSynth;
            return __exception;
        }

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
        [HarmonyPriority(Priority.First)]
        private static void ActivatePreLaunchGuidance_Prefix(
            WeaponSystemLauncher __instance,
            ObjectBase targetObject,
            out PreLaunchEmconState __state)
        {
            __state = default;
            try
            {
                if (__instance == null || targetObject == null || __instance._vwp == null)
                {
                    __state.PreviousProtectedUnit = _emconProtectedUnit;
                    __state.PreviousAllowSyntheticDatalink = _allowEmconSyntheticDatalink;
                    return;
                }

                ObjectBase self = __instance._baseObject;
                if (self == null)
                {
                    __state.PreviousProtectedUnit = _emconProtectedUnit;
                    __state.PreviousAllowSyntheticDatalink = _allowEmconSyntheticDatalink;
                    return;
                }

                AmmunitionParameters ap = CecCapability.ResolveWeaponAmmunitionParameters(__instance);
                CecCapability.EnsureConsortRadarsFresh(__instance, ap);
                __state = CapturePreLaunchEmconState(__instance, self, ap, targetObject);
            }
            catch (Exception ex)
            {
                __state.PreviousProtectedUnit = _emconProtectedUnit;
                __state.PreviousAllowSyntheticDatalink = _allowEmconSyntheticDatalink;
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

        [HarmonyPatch(typeof(WeaponSystemLauncher), nameof(WeaponSystemLauncher.activatePreLaunchGuidance))]
        [HarmonyFinalizer]
        private static Exception ActivatePreLaunchGuidance_Finalizer(PreLaunchEmconState __state, Exception __exception)
        {
            PopEmconProtection(__state.PreviousProtectedUnit, __state.PreviousAllowSyntheticDatalink);
            return __exception;
        }

        [HarmonyPatch(typeof(WeaponSystemHardpoint), nameof(WeaponSystemHardpoint.activatePreLaunchGuidance))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void ActivatePreLaunchGuidanceHardpoint_Prefix(
            WeaponSystemHardpoint __instance,
            ObjectBase targetObject,
            out PreLaunchEmconState __state)
        {
            __state = default;
            try
            {
                if (__instance == null || targetObject == null || __instance._vwp == null)
                {
                    __state.PreviousProtectedUnit = _emconProtectedUnit;
                    __state.PreviousAllowSyntheticDatalink = _allowEmconSyntheticDatalink;
                    return;
                }

                ObjectBase self = __instance._baseObject;
                if (self == null)
                {
                    __state.PreviousProtectedUnit = _emconProtectedUnit;
                    __state.PreviousAllowSyntheticDatalink = _allowEmconSyntheticDatalink;
                    return;
                }

                AmmunitionParameters ap = CecCapability.ResolveWeaponAmmunitionParameters(__instance);
                CecCapability.EnsureConsortRadarsFresh(__instance, ap);
                __state = CapturePreLaunchEmconState(__instance, self, ap, targetObject);
            }
            catch (Exception ex)
            {
                __state.PreviousProtectedUnit = _emconProtectedUnit;
                __state.PreviousAllowSyntheticDatalink = _allowEmconSyntheticDatalink;
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

        [HarmonyPatch(typeof(WeaponSystemHardpoint), nameof(WeaponSystemHardpoint.activatePreLaunchGuidance))]
        [HarmonyFinalizer]
        private static Exception ActivatePreLaunchGuidanceHardpoint_Finalizer(PreLaunchEmconState __state, Exception __exception)
        {
            PopEmconProtection(__state.PreviousProtectedUnit, __state.PreviousAllowSyntheticDatalink);
            return __exception;
        }

        internal static PreLaunchEmconState CapturePreLaunchEmconState(
            WeaponSystem ws,
            ObjectBase self,
            AmmunitionParameters ap,
            ObjectBase targetObject)
        {
            var state = default(PreLaunchEmconState);
            state.PreviousProtectedUnit = _emconProtectedUnit;
            state.PreviousAllowSyntheticDatalink = _allowEmconSyntheticDatalink;

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

            bool allowOwnSynth = IsOwnDatalinkLor(self, ap, targetObject);
            _emconProtectedUnit = self;
            _allowEmconSyntheticDatalink = allowOwnSynth;

            var offLocalRadars = new List<SensorSystemRadar>();
            if (ws?._vwp?._associatedSensors != null)
            {
                foreach (SensorSystem s in ws._vwp._associatedSensors)
                {
                    if (s is SensorSystemRadar radar
                        && radar._baseObject == self
                        && !radar.IsOn.Value
                        && !radar._wasSetOn
                        && !(allowOwnSynth && CecCapability.IsSyntheticDatalinkSensor(radar) && EgBridge.IsDatalinkCarrier(radar))
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
                        && !(allowOwnSynth && CecCapability.IsSyntheticDatalinkSensor(radar) && EgBridge.IsDatalinkCarrier(radar))
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
            PopEmconProtection(state.PreviousProtectedUnit, state.PreviousAllowSyntheticDatalink);

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

            ObjectBase self = instance._baseObject;
            bool selfInEmcon = self != null && CecCapability.IsPlatformInAirRadarEmcon(self);
            bool isSarh = ap != null && CecCapability.IsSarhMissile(ap);
            bool hasMccDatalink = ap?._midCourseCorrection != null && ap._midCourseCorrection.IsDatalink;
            bool preferDatalinkCarrier = isSarh && EgBridge.IsActive && hasMccDatalink;
            bool allowOwnDatalinkLor = preferDatalinkCarrier
                && self != null
                && !CecCapability.HasForwardPassIlluminatorCoverage(self, ap, targetObject)
                && CecCapability.HasOwnShipTerminalIlluminationForLor(self, ap, targetObject);

            // Check if any currently ON associated sensor already has _preLaunchTargetObject == targetObject.
            // If the shooter is in air radar EMCON, clear local pre-launch locks so local dishes do not slew
            // (except the ship's own DatalinkCarrier during Own-Ship Terminal LOR).
            bool alreadyAligned = false;
            foreach (SensorSystem sensor in instance._vwp._associatedSensors)
            {
                if (sensor == null)
                    continue;

                bool isAllowedOwnDatalink = allowOwnDatalinkLor
                    && sensor._baseObject == self
                    && sensor is SensorSystemRadar dlCheck
                    && CecCapability.IsSyntheticDatalinkSensor(dlCheck)
                    && EgBridge.IsDatalinkCarrier(dlCheck);

                if (selfInEmcon && sensor._baseObject == self && !isAllowedOwnDatalink)
                {
                    if (sensor._preLaunchTargetObject == targetObject)
                        sensor._preLaunchTargetObject = null;
                    continue;
                }

                if (alreadyAligned
                    || !sensor.IsOn.Value
                    || sensor.IsDestroyed
                    || sensor.Inoperable.Value
                    || sensor._preLaunchTargetObject != targetObject)
                {
                    continue;
                }

                if (sensor is SensorSystemRadar ssr)
                {
                    if (!isAllowedOwnDatalink && ssr._baseObject != null && CecCapability.IsPlatformInAirRadarEmcon(ssr._baseObject))
                        continue;

                    if (isSarh)
                    {
                        bool validSarhAlign = preferDatalinkCarrier
                            ? EgBridge.IsDatalinkCarrier(ssr)
                            : CecCapability.CanRadarIlluminateSarh(ssr, ap, self);
                        if (!validSarhAlign)
                            continue;
                    }
                }

                alreadyAligned = true;
            }

            if (alreadyAligned)
            {
                instance._sensorPreLaunchAligned = true;
                return;
            }

            // Otherwise, assign targetObject to the best active associated sensor (such as an injected remote radar).
            SensorSystemRadar bestSensor = null;
            int bestPriority = -1;

            foreach (SensorSystem sensor in instance._vwp._associatedSensors)
            {
                if (!(sensor is SensorSystemRadar radar) || radar.IsDestroyed || radar.Inoperable.Value)
                    continue;
                bool isAllowedOwnDatalink = allowOwnDatalinkLor
                    && radar._baseObject == self
                    && CecCapability.IsSyntheticDatalinkSensor(radar)
                    && EgBridge.IsDatalinkCarrier(radar);

                if (!radar.IsOn.Value && !isAllowedOwnDatalink)
                    continue;

                if (!isAllowedOwnDatalink && selfInEmcon && radar._baseObject == self)
                    continue;
                if (!isAllowedOwnDatalink && radar._baseObject != null && CecCapability.IsPlatformInAirRadarEmcon(radar._baseObject))
                    continue;

                int priority = 1;
                if (isSarh)
                {
                    if (preferDatalinkCarrier)
                    {
                        if (EgBridge.IsDatalinkCarrier(radar))
                            priority = isAllowedOwnDatalink ? 2 : 3;
                        else if (CecCapability.CanRadarIlluminateSarh(radar, ap, self) && EgBridge.CanEgIlluminate(radar))
                            priority = 1;
                        else
                            continue;
                    }
                    else
                    {
                        if (!CecCapability.CanRadarIlluminateSarh(radar, ap, self))
                            continue;
                    }
                }

                if (CecCapability.IsAwacsRadar(radar) || CecCapability.IsCecMultifunctionRadar(radar))
                {
                    CecCapability.EnsureMidCourseChannels(radar);
                }
                if (ap != null && !radar.CanGuideAmmo(ap))
                    continue;
                if (!radar.HasFreeTargetChannels(targetObject) || !radar.HasFreeWeaponChannels())
                    continue;
                if (!isAllowedOwnDatalink && !CecCapability.RadarCanSee(radar, targetObject))
                    continue;

                if (bestSensor == null || priority > bestPriority)
                {
                    bestSensor = radar;
                    bestPriority = priority;
                }
            }

            if (bestSensor != null)
            {
                if (!bestSensor.IsOn.Value)
                    bestSensor.Enable(false, true);

                if (bestSensor.IsOn.Value)
                {
                    bestSensor.SetPrelaunchObject(targetObject);
                    bestSensor._restPositionReached = false;
                    instance._sensorPreLaunchAligned = true;
                }
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
                bool launcherInEmcon = launcher != null && CecCapability.IsPlatformInAirRadarEmcon(launcher);
                bool isSarh = CecCapability.IsSarhMissile(m._ap);
                bool hasMccDatalink = m._ap._midCourseCorrection != null && m._ap._midCourseCorrection.IsDatalink;
                bool allowOwnDatalinkLor = EgBridge.IsActive
                    && isSarh
                    && hasMccDatalink
                    && currentIsLocal
                    && current != null
                    && CecCapability.IsSyntheticDatalinkSensor(current)
                    && EgBridge.IsDatalinkCarrier(current)
                    && !CecCapability.HasForwardPassIlluminatorCoverage(launcher, m._ap, target)
                    && CecCapability.HasOwnShipTerminalIlluminationForLor(launcher, m._ap, target);
                bool localGuiderDisallowed = currentIsLocal && (currentWasAutoEnabledFromEmcon || (launcherInEmcon && !allowOwnDatalinkLor));
                bool currentHostInEmcon = current?._baseObject != null && CecCapability.IsPlatformInAirRadarEmcon(current._baseObject);
                bool currentIsNonAirHelper = target.isAirTarget() && CecCapability.IsNonAirSearchHelperSensor(current);

                if (EgBridge.IsActive
                    && current != null
                    && !current.IsDestroyed
                    && !current.Inoperable.Value
                    && current.IsOn.Value
                    && !localGuiderDisallowed
                    && (!currentHostInEmcon || allowOwnDatalinkLor)
                    && !currentIsNonAirHelper
                    && EgBridge.IsDatalinkCarrier(current)
                    && current.CanGuideAmmo(m._ap)
                    && CecCapability.RadarCanReachMissile(current, m))
                {
                    if (allowOwnDatalinkLor && !CecCapability.HasLocalRadarSight(launcher, target))
                    {
                        CecPlugin.Log.LogInfo(
                            $"[CEC] Launch-on-Remote (Own-Ship Terminal LOR): '{m.getObjectIniName()}' (#{m.UniqueID}) from '{launcher?.getName() ?? "?"}' " +
                            $"using mid-course '{current._systemName}' for own-ship terminal illumination.");
                    }
                    else if (!currentIsLocal)
                    {
                        if (CecCapability.IsAwacsRadar(current) || CecCapability.IsCecMultifunctionRadar(current))
                            CecCapability.EnsureMidCourseChannels(current);
                        CecPlugin.Log.LogInfo(
                            $"[CEC] Launch-on-Remote: '{m.getObjectIniName()}' (#{m.UniqueID}) from '{launcher?.getName() ?? "?"}' " +
                            $"guided by remote '{current._baseObject?.getName() ?? "?"}' ({current._systemName}) " +
                            $"[{CecCapability.GetPlatformProfile(launcher).CecTier} -> {CecCapability.GetPlatformProfile(current._baseObject).CecTier}].");
                    }
                    // Keep the mid-course DatalinkCarrier (e.g. eu_AEGIS_BL9) even if target is OTH;
                    // ExtendedGuidance.Runtime.Illuminator / HandoffPatch will reserve terminal CW illumination when due.
                    return;
                }

                bool currentCanSee = current != null
                    && !current.IsDestroyed
                    && !current.Inoperable.Value
                    && current.IsOn.Value
                    && !localGuiderDisallowed
                    && !currentHostInEmcon
                    && !currentIsNonAirHelper
                    && current.CanGuideAmmo(m._ap)
                    && CecCapability.RadarCanSee(current, target)
                    && (!CecCapability.IsPureCwDishIlluminator(current._systemName)
                        || CecCapability.CooperatorSeesTarget(current._baseObject, target))
                    && (!isSarh || EgBridge.IsDatalinkCarrier(current)
                        || CecCapability.CanRadarIlluminateSarh(current, m._ap, launcher));

                if (current != null && currentCanSee)
                {
                    if (!currentIsLocal)
                    {
                        if (CecCapability.IsAwacsRadar(current) || CecCapability.IsCecMultifunctionRadar(current))
                            CecCapability.EnsureMidCourseChannels(current);
                        if (EgBridge.CanEgIlluminate(current))
                            EgBridge.SetActiveEgIlluminator(m, current);
                        else
                            EgBridge.SetActiveEgIlluminator(m, null);
                        CecPlugin.Log.LogInfo(
                            $"[CEC] Launch-on-Remote: '{m.getObjectIniName()}' (#{m.UniqueID}) from '{launcher?.getName() ?? "?"}' " +
                            $"guided by remote '{current._baseObject?.getName() ?? "?"}' ({current._systemName}) " +
                            $"[{CecCapability.GetPlatformProfile(launcher).CecTier} -> {CecCapability.GetPlatformProfile(current._baseObject).CecTier}].");
                    }
                    return;
                }

                SensorSystemRadar bestRemote = null;
                float bestDistSq = float.MaxValue;
                int bestPriority = -1;

                foreach (SensorSystem s in m._weaponSystem._vwp._associatedSensors)
                {
                    if (!(s is SensorSystemRadar candidate) || candidate == current)
                        continue;
                    if (target.isAirTarget() && CecCapability.IsNonAirSearchHelperSensor(candidate))
                        continue;
                    if ((launcherInEmcon || localGuiderDisallowed) && candidate._baseObject == launcher)
                        continue;
                    if (!CecCapability.CanRadarGuideWeapon(candidate))
                        continue;

                    int priority = 1;
                    if (isSarh)
                    {
                        if (EgBridge.IsActive && hasMccDatalink)
                        {
                            if (EgBridge.IsDatalinkCarrier(candidate))
                                priority = 2;
                            else if (CecCapability.CanRadarIlluminateSarh(candidate, m._ap, launcher)
                                     && EgBridge.CanEgIlluminate(candidate))
                                priority = 1;
                            else
                                continue;
                        }
                        else
                        {
                            if (!CecCapability.CanRadarIlluminateSarh(candidate, m._ap, launcher))
                                continue;
                        }
                    }

                    if (CecCapability.IsAwacsRadar(candidate) || CecCapability.IsCecMultifunctionRadar(candidate))
                        CecCapability.EnsureMidCourseChannels(candidate);
                    if (!candidate.CanGuideAmmo(m._ap))
                        continue;
                    if (!candidate.HasFreeTargetChannels(target) || !candidate.HasFreeWeaponChannels())
                        continue;
                    if (!CecCapability.RadarCanSee(candidate, target))
                        continue;

                    float distSq = (candidate._baseObject.transform.position - target.transform.position).sqrMagnitude;
                    if (priority > bestPriority || (priority == bestPriority && distSq < bestDistSq))
                    {
                        bestPriority = priority;
                        bestDistSq = distSq;
                        bestRemote = candidate;
                    }
                }

                if (bestRemote == null)
                    bestRemote = CecCapability.FindHandoffRadar(m, requireTerminalIlluminator: isSarh && (!EgBridge.IsActive || !hasMccDatalink));

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
                    if (localGuiderDisallowed)
                    {
                        current._preLaunchTargetObject = null;
                        if (!current.isBusy() && current._associatedWeapons.Count == 0)
                        {
                            current._isGuiding = false;
                            current._restPositionReached = true;
                        }
                    }
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
                bestRemote.setTargetObject(bestRemote.getMode() == SensorSystemRadar.Mode.Illuminate ? target : (ObjectBase)m);
                m._seeker?.setGuidingSystem(bestRemote);
                m._seeker?.setCurrentTarget(target);
                if (EgBridge.CanEgIlluminate(bestRemote))
                    EgBridge.SetActiveEgIlluminator(m, bestRemote);
                else
                    EgBridge.SetActiveEgIlluminator(m, null);
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
