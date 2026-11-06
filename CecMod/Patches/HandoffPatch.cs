using System;
using System.Collections.Generic;
using HarmonyLib;
using SeaPower;

namespace CecMod.Patches
{
    /// <summary>
    /// Mid-flight CEC handoff and consort radio-link fix.
    /// <para>
    /// 1. Fixes two vanilla bugs in <see cref="Missile.CheckRadioConnection"/> when a consort
    ///    or AWACS is guiding a missile (<c>_guidingRadarSystem._baseObject != _launchPlatform</c>):
    ///    - Vanilla raycasts from <c>_guidingRadarSystem.SensorPosition</c> and only ignores colliders
    ///      belonging to <c>_launchPlatform</c>, so a guiding consort's own hull/superstructure blocks
    ///      the raycast and causes immediate <c>ConnectionLost</c>.
    ///    - Vanilla evaluates <c>_launchPlatform.GetSensorRange</c> and <c>_launchPlatform._alignmentTasks</c>
    ///      instead of the guiding platform's sensor range and alignment state.
    ///    By temporarily aliasing <c>_launchPlatform</c> to <c>_guidingRadarSystem._baseObject</c> during
    ///    <see cref="Missile.OnUpdateEveryFrame"/>, both checks accurately evaluate the guiding unit.
    /// </para>
    /// <para>
    /// 2. Polls in-flight missiles that have lost connection to their guiding radar (or whose guiding
    ///    radar was destroyed/jammed/horizon-blocked) and hands them off to the nearest compatible
    ///    CEC consort or AWACS radar in the same doctrinal network.
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class HandoffPatch
    {
        private const double RetryCooldownSeconds = 1.0;

        /// <summary>
        /// True while <see cref="Missile.OnUpdateEveryFrame"/> is executing, so
        /// <see cref="RemoteEngagementPatch"/> evaluates only the guiding platform's own sensors
        /// rather than recursing into remote network sensors.
        /// </summary>
        internal static bool IsCheckingRadioConnection { get; private set; }

        private static readonly Dictionary<int, double> _lastAttemptByMissile = new Dictionary<int, double>();
        private static double _lastPruneTime;

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

                SensorSystemRadar guiderRadar = __instance._guidingRadarSystem;
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
                if (target == null || __instance._ap == null)
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

                if (!__instance.ConnectionLost.Value && !__instance.ConnectionLostForever.Value && !currentRadarDead)
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

                SensorSystemRadar newRadar = CecCapability.FindHandoffRadar(__instance);
                if (newRadar == null)
                    return;

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

                CecPlugin.Log.LogInfo(
                    $"[CEC] Handoff: missile '{__instance.getObjectIniName()}' (#{id}) re-linked from " +
                    $"'{oldOwner}' ({oldRadarName}) -> '{newRadar._baseObject.getName()}' ({newRadar._systemName}).");
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] OnUpdateEveryFrame_Postfix exception: {ex}");
            }
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

            SensorSystemRadar guider = __instance._guidingRadarSystem;
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
