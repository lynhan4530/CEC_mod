using System;
using System.Reflection;
using CecMod.Patches;
using HarmonyLib;
using SeaPower;
using UnityEngine;

namespace CecMod
{
    /// <summary>
    /// Zero-hard-dependency runtime reflection bridge to Anchor Chain's <c>ExtendedGuidance.dll</c>
    /// (<c>euromod.extended-guidance</c>, Workshop ID 3784474738).
    /// <para>
    /// Because Anchor Chain loads submods dynamically after BepInEx <c>Awake()</c> and allows toggling
    /// <c>euromod.extended-guidance</c> via <c>ACSubmods.PreferenceStore</c>, all detection and delegate
    /// binding in this class is lazy and live rather than cached once at startup.
    /// </para>
    /// </summary>
    internal static class EgBridge
    {
        public const string EgHarmonyId = "euromod.extended-guidance";
        private const string EgAssemblyName = "ExtendedGuidance";
        private const int ProbeIntervalTicks = 2000;

        private static Harmony _harmony;
        private static volatile bool _probePending = true;
        private static int _lastProbeTick = System.Environment.TickCount - ProbeIntervalTicks;

        private static bool _bound;
        private static bool _runtimeHooksApplied;
        private static Type _runtimeType;

        private static AccessTools.FieldRef<bool> _activeFieldRef;
        private static Func<SensorSystemRadar, bool> _datalinkCarrierFn;
        private static Func<SensorSystemRadar, bool> _canIlluminateFn;
        private static FastInvokeHandler _getStateInvoker;
        private static FastInvokeHandler _terminalDueInvoker;
        private static AccessTools.FieldRef<object, SensorSystemRadar> _stateIlluminatorRef;

        /// <summary>
        /// Initializes assembly-load watching so <c>ExtendedGuidance</c> is detected immediately if
        /// Anchor Chain loads it after <see cref="CecPlugin.Awake"/>.
        /// </summary>
        public static void Initialize(Harmony harmony)
        {
            _harmony = harmony;
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            try
            {
                string loadedName = args?.LoadedAssembly?.GetName()?.Name;
                if (string.Equals(loadedName, EgAssemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    _probePending = true;
                }
            }
            catch
            {
                // Never throw inside AppDomain.AssemblyLoad.
            }
        }

        /// <summary>
        /// True when <c>ExtendedGuidance.dll</c> is loaded and actively patching Sea Power.
        /// </summary>
        public static bool IsActive
        {
            get
            {
                EnsureBound();

                bool active = false;
                if (_bound)
                {
                    try
                    {
                        active = _activeFieldRef != null
                            ? _activeFieldRef()
                            : Harmony.HasAnyPatches(EgHarmonyId);
                    }
                    catch
                    {
                        active = false;
                    }
                }

                if (active && !_runtimeHooksApplied)
                {
                    ApplyRuntimeHooks();
                }

                return active;
            }
        }

        /// <summary>
        /// True when <paramref name="radar"/> is treated by <c>ExtendedGuidance</c> as a mid-course
        /// datalink carrier (either native <c>_isDatalinkGuidance</c> or an EG link-role transceiver
        /// such as <c>eu_AEGIS_BL9</c>).
        /// </summary>
        public static bool IsDatalinkCarrier(SensorSystemRadar radar)
        {
            if (radar == null)
                return false;

            if (IsActive && _datalinkCarrierFn != null)
            {
                try
                {
                    return _datalinkCarrierFn(radar);
                }
                catch
                {
                    // Fall through to structural check.
                }
            }

            return radar._isDatalinkGuidance || CecCapability.IsSyntheticDatalinkSensor(radar);
        }

        /// <summary>
        /// True when <paramref name="radar"/> is capable of terminal SARH CW/IWI illumination.
        /// </summary>
        public static bool CanEgIlluminate(SensorSystemRadar radar)
        {
            if (radar == null)
                return false;

            if (IsActive && _canIlluminateFn != null)
            {
                try
                {
                    return _canIlluminateFn(radar);
                }
                catch
                {
                    // Fall through to doctrinal check.
                }
            }

            return !CecCapability.IsSyntheticDatalinkSensor(radar) && !CecCapability.IsAwacsRadar(radar);
        }

        /// <summary>
        /// Returns the terminal SARH illuminator currently reserved by <c>ExtendedGuidance.Runtime.Illuminator</c>
        /// for <paramref name="m"/> (<c>State.Illuminator</c>), or <c>null</c> if none is reserved or operational.
        /// </summary>
        public static SensorSystemRadar GetActiveEgIlluminator(Missile m)
        {
            if (!IsActive || m == null || _getStateInvoker == null || _stateIlluminatorRef == null)
                return null;

            try
            {
                object state = _getStateInvoker(null, m, false);
                if (state == null)
                    return null;

                SensorSystemRadar illum = _stateIlluminatorRef(state);
                if (illum != null
                    && !illum.IsDestroyed
                    && !illum.Inoperable.Value
                    && illum.IsOn.Value
                    && illum._baseObject != null
                    && !illum._baseObject.IsDestroyed)
                {
                    return illum;
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogDebug($"[CEC] EgBridge.GetActiveEgIlluminator reflection fallback: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// True when <c>ExtendedGuidance</c> determines that <paramref name="m"/> has reached the
        /// terminal SARH illumination window (<c>Runtime.TerminalIlluminationDue</c>).
        /// </summary>
        public static bool IsTerminalIlluminationDue(Missile m)
        {
            if (!IsActive || m == null)
                return true;

            if (_getStateInvoker != null && _terminalDueInvoker != null)
            {
                try
                {
                    object state = _getStateInvoker(null, m, false);
                    if (state != null)
                    {
                        return (bool)_terminalDueInvoker(null, state, GameTime.missionElapsedTime);
                    }
                }
                catch (Exception ex)
                {
                    CecPlugin.Log.LogDebug($"[CEC] EgBridge.IsTerminalIlluminationDue reflection fallback: {ex.Message}");
                }
            }

            return m.getFlightStage() == WeaponBase.FlightStage.TerminalApproach;
        }

        /// <summary>
        /// True when <c>ExtendedGuidance</c> is flying a SARH+Datalink missile mid-course on a live
        /// <c>DatalinkCarrier</c> and terminal CW/IWI illumination is not yet due.
        /// </summary>
        public static bool IsWaitingForTerminalIllumination(Missile m)
        {
            if (!IsActive || m == null || m._ap == null)
                return false;

            if (m._ap._guidanceType != AmmunitionParameters.GuidanceType.SemiActiveRadarHoming)
                return false;

            if (m._ap._midCourseCorrection == null || !m._ap._midCourseCorrection.IsDatalink)
                return false;

            if (!IsDatalinkCarrier(m._guidingRadarSystem))
                return false;

            if (m.ConnectionLost.Value || m.ConnectionLostForever.Value)
                return false;

            return !IsTerminalIlluminationDue(m);
        }

        private static void EnsureBound()
        {
            if (_bound)
                return;

            int nowTicks = System.Environment.TickCount;
            if (!_probePending && unchecked(nowTicks - _lastProbeTick) < ProbeIntervalTicks)
                return;

            _probePending = false;
            _lastProbeTick = nowTicks;

            try
            {
                Assembly egAsm = null;
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    Assembly asm = assemblies[i];
                    if (asm != null && string.Equals(asm.GetName().Name, EgAssemblyName, StringComparison.OrdinalIgnoreCase))
                    {
                        egAsm = asm;
                        break;
                    }
                }

                if (egAsm == null)
                    return;

                const BindingFlags AllStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                Type statusApiType = egAsm.GetType("ExtendedGuidance.MissileStatusApi", throwOnError: false);
                if (statusApiType != null)
                {
                    FieldInfo activeField = statusApiType.GetField("Active", AllStatic);
                    if (activeField != null && activeField.FieldType == typeof(bool))
                    {
                        _activeFieldRef = AccessTools.StaticFieldRefAccess<bool>(activeField);
                    }
                }

                _runtimeType = egAsm.GetType("ExtendedGuidance.Runtime", throwOnError: false);
                Type stateType = egAsm.GetType("ExtendedGuidance.State", throwOnError: false);

                if (_runtimeType != null)
                {
                    MethodInfo dlCarrier = _runtimeType.GetMethod(
                        "DatalinkCarrier",
                        AllStatic,
                        binder: null,
                        types: new[] { typeof(SensorSystemRadar) },
                        modifiers: null);
                    if (dlCarrier != null && dlCarrier.ReturnType == typeof(bool))
                    {
                        _datalinkCarrierFn = AccessTools.MethodDelegate<Func<SensorSystemRadar, bool>>(dlCarrier);
                    }

                    MethodInfo canIllum = _runtimeType.GetMethod(
                        "CanIlluminate",
                        AllStatic,
                        binder: null,
                        types: new[] { typeof(SensorSystemRadar) },
                        modifiers: null);
                    if (canIllum != null && canIllum.ReturnType == typeof(bool))
                    {
                        _canIlluminateFn = AccessTools.MethodDelegate<Func<SensorSystemRadar, bool>>(canIllum);
                    }

                    if (stateType != null)
                    {
                        MethodInfo getState = _runtimeType.GetMethod(
                            "Get",
                            AllStatic,
                            binder: null,
                            types: new[] { typeof(Missile), typeof(bool) },
                            modifiers: null);
                        if (getState != null)
                        {
                            _getStateInvoker = MethodInvoker.GetHandler(getState);
                        }

                        MethodInfo termDue = _runtimeType.GetMethod(
                            "TerminalIlluminationDue",
                            AllStatic,
                            binder: null,
                            types: new[] { stateType, typeof(double) },
                            modifiers: null);
                        if (termDue != null && termDue.ReturnType == typeof(bool))
                        {
                            _terminalDueInvoker = MethodInvoker.GetHandler(termDue);
                        }

                        FieldInfo illumField = stateType.GetField("Illuminator", AllInstance);
                        if (illumField != null && illumField.FieldType == typeof(SensorSystemRadar))
                        {
                            _stateIlluminatorRef = AccessTools.FieldRefAccess<object, SensorSystemRadar>(illumField);
                        }
                    }
                }

                _bound = true;
                CecPlugin.Log.LogInfo("[CEC] Detected ExtendedGuidance assembly — reflection bridge bound.");
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogWarning($"[CEC] EgBridge binding failed (continuing in standalone mode): {ex}");
            }
        }

        private static void ApplyRuntimeHooks()
        {
            _runtimeHooksApplied = true;
            if (_harmony == null || _runtimeType == null)
                return;

            const BindingFlags AllStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                // 1. Bridge FcrAcquisitionAdapter's transpiled pre-launch visibility checks
                //    (DoesAmmoMatchTarget, HandleEngageTasks, ApplyLocalAirVisibilityLimit) so OTH & EMCON
                //    cooperative engagements pass ExtendedGuidance's PlanningLaunchVisibility / PlanningLaunchCantSee.
                MethodInfo planningVis = _runtimeType.GetMethod("PlanningLaunchVisibility", AllStatic);
                if (planningVis != null)
                {
                    MethodInfo postfix = typeof(EgBridge).GetMethod(
                        nameof(PlanningLaunchVisibility_Postfix),
                        AllStatic);
                    _harmony.Patch(planningVis, postfix: new HarmonyMethod(postfix));
                }

                // 2. Bridge ReadNativeContact so CEC missiles launched in EMCON/OTH or guided by a
                //    remote AWACS/consort can consume shared taskforce PlottingTable radar measurements
                //    instead of being rejected by the firing launcher's local ContactSensorAssignment.
                MethodInfo readContact = _runtimeType.GetMethod("ReadNativeContact", AllStatic);
                if (readContact != null)
                {
                    MethodInfo prefix = typeof(EgBridge).GetMethod(
                        nameof(ReadNativeContact_Prefix),
                        AllStatic);
                    _harmony.Patch(readContact, prefix: new HarmonyMethod(prefix));
                }

                CecPlugin.Log.LogInfo("[CEC] ExtendedGuidance active — hybrid CEC + ExtendedGuidance realism bridge enabled.");
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogWarning($"[CEC] EgBridge runtime hook installation warning: {ex}");
            }
        }

        private static void PlanningLaunchVisibility_Postfix(
            ObjectBase host,
            ObjectBase target,
            ObjectBase.SensorRangeOptions options,
            bool excludeRadars,
            Ammunition ammo,
            ref float __result)
        {
            try
            {
                if (!IsActive
                    || HandoffPatch.IsCheckingRadioConnection
                    || excludeRadars
                    || host == null
                    || target == null
                    || !target.isAirTarget()
                    || ammo == null
                    || ammo._ap == null)
                {
                    return;
                }

                AmmunitionParameters ap = ammo._ap;
                bool isSarh = ap._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming;
                bool hasMidCourseDatalink = ap._midCourseCorrection != null
                                            && ap._midCourseCorrection.AllowsLauncherGuidance;
                if (!isSarh && !hasMidCourseDatalink)
                    return;

                float distToTarget = CecCapability.HorizontalDistance(
                    host.transform.position,
                    target.transform.position);

                // PlanningLaunchCantSee checks PlanningLaunchVisibility < 20.669622f.
                float requiredVisibility = Mathf.Max(distToTarget, 21f);
                if (__result >= requiredVisibility)
                    return;

                if (CecCapability.HasCooperativeAirTrack(host, target, options)
                    && CecCapability.HasRemoteFcrCoverage(host, ap, target))
                {
                    __result = Mathf.Max(distToTarget + 50f, 25f);
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] PlanningLaunchVisibility_Postfix exception: {ex}");
            }
        }

        private static void ReadNativeContact_Prefix(
            ObjectBase launcher,
            ObjectBase target,
            ref SensorSystemRadar guide,
            ref WeaponBase weapon)
        {
            try
            {
                if (!IsActive || !(weapon is Missile m) || m._ap == null || launcher == null || target == null)
                    return;

                AmmunitionParameters ap = m._ap;
                bool isSarh = ap._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming;
                bool hasMidCourseDatalink = ap._midCourseCorrection != null
                                            && ap._midCourseCorrection.AllowsLauncherGuidance;
                if (!isSarh && !hasMidCourseDatalink)
                    return;

                if (CecCapability.GetPlatformNetwork(launcher) == CecCapability.CecNetwork.None)
                    return;

                ObjectBase trueLauncher = m._weaponSystem?._baseObject ?? m._launchPlatform;
                SensorSystemRadar activeGuider = GetActiveEgIlluminator(m) ?? m._guidingRadarSystem;
                bool guidedRemotely = (trueLauncher != null && launcher != trueLauncher)
                                      || (activeGuider?._baseObject != null && activeGuider._baseObject != trueLauncher);

                if (guidedRemotely)
                {
                    // Missile is guided by a consort/AWACS whose sensors are not in trueLauncher's WeaponParameters.
                    guide = null;
                    weapon = null;
                    return;
                }

                // Launcher is guiding on its own DatalinkCarrier (e.g. eu_AEGIS_BL9), but own-ship air radars
                // are in EMCON or horizon-blocked (OTH) while a cooperative CEC/AWACS unit holds the track.
                if (!CecCapability.HasLocalRadarSight(launcher, target)
                    && CecCapability.HasCooperativeTrack(launcher, target))
                {
                    guide = null;
                    weapon = null;
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] ReadNativeContact_Prefix exception: {ex}");
            }
        }
    }
}
