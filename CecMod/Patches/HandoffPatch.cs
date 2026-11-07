using System;
using System.Collections.Generic;
using HarmonyLib;
using SeaPower;

namespace CecMod.Patches
{
    /// <summary>
    /// Mid-flight CEC handoff, <c>ExtendedGuidance</c> physics-tick aliasing, and terminal illuminator promotion.
    /// <para>
    /// 1. Temporarily aliases <c>_launchPlatform</c> to the active guiding platform
    ///    (<c>EgBridge.GetActiveEgIlluminator(m) ?? m._guidingRadarSystem</c>) during both
    ///    <see cref="Missile.OnUpdateEveryFrame"/> and <see cref="Missile.OnFixedUpdate"/> so vanilla
    ///    <see cref="Missile.CheckRadioConnection"/> and <c>ExtendedGuidance.Runtime.RefreshExternal</c>
    ///    evaluate the guiding consort/AWACS rather than an OTH, EMCON, or sunken launcher.
    /// </para>
    /// <para>
    /// 2. Polls in-flight missiles that have lost connection to their guiding radar — or SARH+Datalink
    ///    missiles under <c>ExtendedGuidance</c> entering terminal illumination when own-ship illuminators
    ///    are horizon-masked, saturated, in EMCON, or sunk — and hands them off to the nearest compatible
    ///    CEC consort or AWACS radar in the same doctrinal network.
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class HandoffPatch
    {
        private const double RetryCooldownSeconds = 1.0;

        /// <summary>
        /// True while <see cref="Missile.OnUpdateEveryFrame"/> or <see cref="Missile.OnFixedUpdate"/>
        /// is executing, so <see cref="RemoteEngagementPatch"/> evaluates only the guiding platform's
        /// own sensors rather than recursing into remote network sensors.
        /// </summary>
        internal static bool IsCheckingRadioConnection { get; private set; }

        private static readonly Dictionary<int, double> _lastAttemptByMissile = new Dictionary<int, double>();
        private static double _lastPruneTime;

        private static double _lastBatchFixedTime = double.NaN;
        private static readonly List<(Missile Missile, ObjectBase OriginalLauncher)> _fixedUpdateAliases =
            new List<(Missile, ObjectBase)>(32);

        private static SensorSystemRadar GetActiveGuiderRadar(Missile m)
        {
            if (m == null)
                return null;
            return EgBridge.GetActiveEgIlluminator(m) ?? m._guidingRadarSystem;
        }

        /// <summary>
        /// Runs before <c>ExtendedGuidance.Hooks.Fixed</c> (<c>Priority.High = 800</c>).
        /// Because <c>ExtendedGuidance.Runtime.Prepare(now)</c> batch-processes all airborne missiles
        /// on the first <see cref="Missile.OnFixedUpdate"/> call of each physics tick, we alias
        /// <c>_launchPlatform</c> across all CEC-guided airborne missiles for that tick and restore
        /// them immediately in <see cref="OnFixedUpdate_Postfix"/> / <see cref="OnFixedUpdate_Finalizer"/>.
        /// </summary>
        [HarmonyPatch(typeof(Missile), nameof(Missile.OnFixedUpdate))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void OnFixedUpdate_Prefix(Missile __instance)
        {
            IsCheckingRadioConnection = true;
            _fixedUpdateAliases.Clear();

            try
            {
                if (EgBridge.IsActive)
                {
                    double now = GameTime.missionElapsedTime;
                    if (now != _lastBatchFixedTime)
                    {
                        _lastBatchFixedTime = now;
                        var allObjects = Singleton<ObjectsManager>.Instance?._listOfAllObjects;
                        if (allObjects != null)
                        {
                            for (int i = 0; i < allObjects.Count; i++)
                            {
                                if (!(allObjects[i] is Missile m) || m == null || m.IsDestroyed)
                                    continue;

                                if (m._weaponSystem != null && m._ap != null)
                                    CecCapability.SyncConsortIlluminatorsToAmmo(m._weaponSystem, m._ap);

                                ObjectBase guiderPlatform = GetActiveGuiderRadar(m)?._baseObject;
                                if (guiderPlatform != null
                                    && !guiderPlatform.IsDestroyed
                                    && guiderPlatform != m._launchPlatform)
                                {
                                    _fixedUpdateAliases.Add((m, m._launchPlatform));
                                    m._launchPlatform = guiderPlatform;
                                }
                            }
                        }
                    }
                }

                if (__instance != null && !IsAlreadyAliased(__instance))
                {
                    ObjectBase guiderPlatform = GetActiveGuiderRadar(__instance)?._baseObject;
                    if (guiderPlatform != null
                        && !guiderPlatform.IsDestroyed
                        && guiderPlatform != __instance._launchPlatform)
                    {
                        _fixedUpdateAliases.Add((__instance, __instance._launchPlatform));
                        __instance._launchPlatform = guiderPlatform;
                    }
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] OnFixedUpdate_Prefix exception: {ex}");
            }
        }

        [HarmonyPatch(typeof(Missile), nameof(Missile.OnFixedUpdate))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void OnFixedUpdate_Postfix()
        {
            RestoreFixedUpdateAliases();
        }

        [HarmonyPatch(typeof(Missile), nameof(Missile.OnFixedUpdate))]
        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        private static Exception OnFixedUpdate_Finalizer(Exception __exception)
        {
            RestoreFixedUpdateAliases();
            return __exception;
        }

        private static bool IsAlreadyAliased(Missile m)
        {
            for (int i = 0; i < _fixedUpdateAliases.Count; i++)
            {
                if (ReferenceEquals(_fixedUpdateAliases[i].Missile, m))
                    return true;
            }
            return false;
        }

        private static void RestoreFixedUpdateAliases()
        {
            for (int i = _fixedUpdateAliases.Count - 1; i >= 0; i--)
            {
                var entry = _fixedUpdateAliases[i];
                if (entry.Missile != null)
                    entry.Missile._launchPlatform = entry.OriginalLauncher;
            }
            _fixedUpdateAliases.Clear();
            IsCheckingRadioConnection = false;
        }

        [HarmonyPatch(typeof(Missile), nameof(Missile.OnUpdateEveryFrame))]
        [HarmonyPrefix]
        private static void OnUpdateEveryFrame_Prefix(Missile __instance, out ObjectBase __state)
        {
            __state = __instance != null ? __instance._launchPlatform : null;
            IsCheckingRadioConnection = true;

            try
            {
                if (__instance == null)
                    return;

                SensorSystemRadar guiderRadar = GetActiveGuiderRadar(__instance);
                ObjectBase guiderPlatform = guiderRadar != null ? guiderRadar._baseObject : null;
                if (guiderPlatform != null && !guiderPlatform.IsDestroyed && guiderPlatform != __instance._launchPlatform)
                {
                    __instance._launchPlatform = guiderPlatform;
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] OnUpdateEveryFrame_Prefix exception: {ex}");
            }
        }

        [HarmonyPatch(typeof(Missile), nameof(Missile.OnUpdateEveryFrame))]
        [HarmonyPostfix]
        private static void OnUpdateEveryFrame_Postfix(Missile __instance, ObjectBase __state)
        {
            if (__instance != null)
                __instance._launchPlatform = __state;
            IsCheckingRadioConnection = false;

            try
            {
                if (__instance == null || __instance.IsDestroyed)
                    return;

                ObjectBase target = __instance.CurrentIntendedTargetObject;
                if (target == null || target.IsDestroyed || __instance._ap == null)
                    return;

                // Terminal seeker already took over (e.g. active radar terminal phase) -> no mid-course handoff needed.
                if (!__instance._requiresExternalGuidance)
                    return;

                bool isSarh = __instance._ap._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming;
                bool hasMidCourseDatalink = __instance._ap._midCourseCorrection != null
                                            && __instance._ap._midCourseCorrection.AllowsLauncherGuidance;
                if (!isSarh && !hasMidCourseDatalink)
                    return;

                SensorSystemRadar currentRadar = __instance._guidingRadarSystem;
                bool currentRadarDead = currentRadar == null
                                        || currentRadar.IsDestroyed
                                        || currentRadar.Inoperable.Value
                                        || !currentRadar.IsOn.Value
                                        || currentRadar._baseObject == null
                                        || currentRadar._baseObject.IsDestroyed;

                // Under ExtendedGuidance, a SARH+Datalink missile flying mid-course on an operational
                // DatalinkCarrier (eu_AEGIS_BL9, SYR-1, etc.) before terminal illumination is due is healthy.
                if (!currentRadarDead && EgBridge.IsWaitingForTerminalIllumination(__instance))
                    return;

                // Under ExtendedGuidance, when terminal SARH illumination IS due on a DatalinkCarrier,
                // check if ExtendedGuidance already reserved an illuminator or own-ship can illuminate;
                // if not (OTH, horizon-masked, saturated, EMCON, or sunk), promote to a forward consort illuminator.
                bool needsTerminalPromotion = EgBridge.IsActive
                                              && isSarh
                                              && !currentRadarDead
                                              && !__instance.ConnectionLost.Value
                                              && EgBridge.IsDatalinkCarrier(currentRadar)
                                              && EgBridge.IsTerminalIlluminationDue(__instance)
                                              && EgBridge.GetActiveEgIlluminator(__instance) == null
                                              && !HasOwnShipIlluminatorForTarget(__instance, target);

                bool linkBroken = __instance.ConnectionLost.Value
                                  || __instance.ConnectionLostForever.Value
                                  || currentRadarDead;

                if (!linkBroken && !needsTerminalPromotion)
                    return;

                double now = GameTime.missionElapsedTime;
                int id = __instance.UniqueID;

                if (_lastAttemptByMissile.TryGetValue(id, out double last) && now - last < RetryCooldownSeconds)
                    return;
                _lastAttemptByMissile[id] = now;

                if (now - _lastPruneTime > 30.0)
                {
                    _lastPruneTime = now;
                    _lastAttemptByMissile.Clear();
                }

                SensorSystemRadar newRadar = CecCapability.FindHandoffRadar(
                    __instance,
                    requireTerminalIlluminator: needsTerminalPromotion);
                if (newRadar == null)
                    return;

                if (!newRadar.IsOn.Value)
                    newRadar.Enable(allowMessage: false, turnOffAfterEngage: true);

                if (!newRadar.IsAimedAtTarget(target) && !newRadar.TryToAddTarget(target))
                    return;
                if (!newRadar.IsGuidingWeapon(__instance) && !newRadar.TryToAddWeapon(__instance))
                    return;

                string oldOwner = currentRadar?._baseObject?.getName() ?? "<none>";
                string oldRadarName = currentRadar?._systemName ?? "<none>";

                if (currentRadar != null && currentRadar != newRadar)
                {
                    currentRadar.FreeChannels(target, __instance);
                }

                __instance._guidingRadarSystem = newRadar;
                newRadar._isGuiding = true;
                newRadar._restPositionReached = false;
                newRadar._preLaunchTargetObject = null;
                __instance._seeker?.setGuidingSystem(newRadar);

                __instance.ConnectionLost.Value = false;
                __instance.ConnectionLostForever.Value = false;
                __instance._lostGuidanceTime = now;

                string reasonTag = needsTerminalPromotion ? "Terminal-Illum Handoff" : "Handoff";
                CecPlugin.Log.LogInfo(
                    $"[CEC] {reasonTag}: missile '{__instance.getObjectIniName()}' (#{id}) re-linked from " +
                    $"'{oldOwner}' ({oldRadarName}) -> '{newRadar._baseObject.getName()}' ({newRadar._systemName}).");
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] OnUpdateEveryFrame_Postfix exception: {ex}");
            }
        }

        private static bool HasOwnShipIlluminatorForTarget(Missile m, ObjectBase target)
        {
            ObjectBase host = m._guidingRadarSystem?._baseObject ?? m._launchPlatform;
            if (host == null || host.IsDestroyed || CecCapability.IsPlatformInAirRadarEmcon(host))
                return false;

            var systems = host._obp?._sensorSystems;
            if (systems == null)
                return false;

            for (int i = 0; i < systems.Count; i++)
            {
                if (!(systems[i] is SensorSystemRadar r))
                    continue;
                if (r.IsDestroyed || r.Inoperable.Value || r._radar?._rp == null)
                    continue;
                if (!EgBridge.CanEgIlluminate(r) || !r.CanGuideAmmo(m._ap))
                    continue;
                if (!r.IsGuidingWeapon(m) && (!r.HasFreeTargetChannels(target) || !r.HasFreeWeaponChannels()))
                    continue;
                if (CecCapability.RadarCanSee(r, target))
                    return true;
            }

            return false;
        }

        [HarmonyPatch(typeof(Missile), nameof(Missile.OnUpdateEveryFrame))]
        [HarmonyFinalizer]
        private static Exception OnUpdateEveryFrame_Finalizer(Missile __instance, ObjectBase __state, Exception __exception)
        {
            if (__instance != null)
                __instance._launchPlatform = __state;
            IsCheckingRadioConnection = false;
            return __exception;
        }

        /// <summary>
        /// Prevents a missile actively guided by a live consort/AWACS from being classified as an
        /// unguidable orphan if its original <see cref="WeaponBase._launchPlatform"/> is sunk.
        /// </summary>
        [HarmonyPatch(typeof(WeaponBase), nameof(WeaponBase.IsUnguidableOrphan), MethodType.Getter)]
        [HarmonyPostfix]
        private static void IsUnguidableOrphan_Postfix(WeaponBase __instance, ref bool __result)
        {
            if (!__result || __instance == null)
                return;

            SensorSystemRadar guider = (__instance is Missile m ? GetActiveGuiderRadar(m) : null)
                                       ?? __instance._guidingRadarSystem;
            if (guider != null
                && !guider.IsDestroyed
                && !guider.Inoperable.Value
                && guider.IsOn.Value
                && guider._baseObject != null
                && !guider._baseObject.IsDestroyed)
            {
                __result = false;
            }
        }
    }
}
