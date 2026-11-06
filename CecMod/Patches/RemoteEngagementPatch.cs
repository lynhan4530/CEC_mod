using System;
using HarmonyLib;
using SeaPower;
using UnityEngine;

namespace CecMod.Patches
{
    /// <summary>
    /// Enables real-world CEC / NIFC-CA / APAR / Russian / PLAN cooperative engagement capabilities:
    /// <list type="bullet">
    ///   <item>
    ///     <b>Synthetic Datalink Compatibility (<see cref="SensorSystem.CanGuideAmmo"/>):</b>
    ///     Allows compatible AWACS / ship search radars to provide mid-course datalink updates to
    ///     ARH + MidCourseCorrection missiles (SM-6, SM-2 Blk IIIC, ESSM Blk 2, Aster 15/30, CAMM,
    ///     40N6, 9M96, HHQ-9B, AIM-120D/AIM-260/AIM-174B, Meteor, R-37M, PL-15) and enforces
    ///     doctrinal SARH illuminator matching when Euromod's <c>ExtendedGuidance.dll</c> is disabled.
    ///   </item>
    ///   <item>
    ///     <b>Engage-on-Remote / Launch-on-Remote (OTH &amp; EMCON):</b>
    ///     Allows a CEC-capable ship or aircraft whose local radar is horizon-blocked or in EMCON
    ///     to pass pre-launch vision and FCR range gates when a friendly CEC consort or AWACS in the
    ///     same doctrinal network is actively tracking the target on radar and has a compatible FCR/datalink.
    ///   </item>
    /// </list>
    /// </summary>
    [HarmonyPatch]
    internal static class RemoteEngagementPatch
    {
        /// <summary>
        /// Filters <see cref="SensorSystem.CanGuideAmmo"/> through <see cref="CecCapability.CanSensorGuideAmmo"/>.
        /// Note: <see cref="SensorSystemRadar"/> inherits <c>CanGuideAmmo</c> from <see cref="SensorSystem"/>
        /// without overriding it.
        /// </summary>
        [HarmonyPatch(typeof(SensorSystem), nameof(SensorSystem.CanGuideAmmo))]
        [HarmonyPostfix]
        private static void CanGuideAmmo_Postfix(SensorSystem __instance, AmmunitionParameters ap, ref bool __result)
        {
            try
            {
                __result = CecCapability.CanSensorGuideAmmo(__instance, ap);
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] CanGuideAmmo_Postfix exception: {ex}");
            }
        }

        /// <summary>
        /// When a CEC-capable platform checks <c>ObjectBase.GetLocalAirTargetVisibilityRange</c>
        /// (with <c>excludeRadars == false</c>) and its own local sensors cannot reach the air target
        /// (due to OTH radar horizon or EMCON), allow a cooperative radar track from an allied
        /// CEC ship or AWACS in the same network to satisfy the air-target visibility gate.
        /// </summary>
        [HarmonyPatch(typeof(ObjectBase), "GetLocalAirTargetVisibilityRange")]
        [HarmonyPostfix]
        private static void GetLocalAirTargetVisibilityRange_Postfix(
            ObjectBase __instance,
            ObjectBase targetObject,
            ObjectBase.SensorRangeOptions options,
            bool excludeRadars,
            ref float __result)
        {
            try
            {
                if (excludeRadars || __instance == null || targetObject == null || !targetObject.isAirTarget())
                    return;

                float distToTarget = CecCapability.HorizontalDistance(
                    __instance.transform.position,
                    targetObject.transform.position);

                if (__result >= distToTarget)
                    return;

                if (CecCapability.HasCooperativeAirTrack(__instance, targetObject, options))
                {
                    __result = distToTarget + 50f;
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] GetLocalAirTargetVisibilityRange_Postfix exception: {ex}");
            }
        }

        /// <summary>
        /// Extends <see cref="ObjectBase.GetMaxRangeForFCR"/> for two critical CEC cases:
        /// <list type="number">
        ///   <item>
        ///     When a consort or AWACS is guiding a missile it does not carry in its own magazine
        ///     (so <c>GetWeaponSystemsForAmmunition(ammo)</c> is empty on that platform), evaluate
        ///     the platform's own <c>_obp._sensorSystems</c> directly so <see cref="Missile.CheckRadioConnection"/>
        ///     accurately checks the guiding unit's FCR range and radar horizon.
        ///   </item>
        ///   <item>
        ///     When a firing platform is checking pre-launch FCR range (<c>!HandoffPatch.IsCheckingRadioConnection</c>)
        ///     and its local FCRs are horizon-limited (<c>GetAmmunitionHorizons</c>) or in EMCON, check
        ///     <see cref="CecCapability.HasRemoteFcrCoverage"/> for a remote CEC ship or AWACS that can
        ///     guide the ammo and has radar LOS to the target.
        ///   </item>
        /// </list>
        /// </summary>
        [HarmonyPatch(typeof(ObjectBase), nameof(ObjectBase.GetMaxRangeForFCR))]
        [HarmonyPostfix]
        private static void GetMaxRangeForFCR_Postfix(
            ObjectBase __instance,
            Ammunition ammo,
            ObjectBase targetObject,
            float maxRangeOverride,
            ObjectBase.SensorRangeOptions options,
            ref ObjectBase.FcrRangeData __result)
        {
            try
            {
                if (__instance == null || ammo == null || ammo._ap == null || targetObject == null)
                    return;

                AmmunitionParameters ap = ammo._ap;
                bool isSarh = ap._guidanceType == AmmunitionParameters.GuidanceType.SemiActiveRadarHoming;
                bool hasMidCourseDatalink = ap._midCourseCorrection != null
                                            && ap._midCourseCorrection.AllowsLauncherGuidance;
                if (!isSarh && !hasMidCourseDatalink)
                    return;

                // Case 1: Direct sensor evaluation on __instance when __instance doesn't carry 'ammo'
                // in its own weapon systems (e.g., an E-2D Hawkeye or consort ship guiding another unit's missile).
                if (__result.Range <= 0f && __instance._obp?._sensorSystems != null)
                {
                    float bestDirectRange = 0f;
                    foreach (SensorSystem s in __instance._obp._sensorSystems)
                    {
                        if (!(s is SensorSystemRadar radar))
                            continue;
                        if (radar.IsDestroyed || radar.Inoperable.Value || !radar.IsOn.Value)
                            continue;
                        if (!radar.CanGuideAmmo(ap))
                            continue;

                        float r = radar.GetRange(targetObject, options).Distance;
                        if (isSarh)
                        {
                            float horizon = RadarCalculator.getRadarHorizon(
                                radar.SensorPosition.y,
                                targetObject.VisibleAltitude,
                                visual: false,
                                Utils.TerrainAltForHorizon(__instance, targetObject));
                            r = Mathf.Min(r, horizon);
                        }

                        if (r > bestDirectRange)
                            bestDirectRange = r;
                    }

                    if (bestDirectRange > 0f)
                    {
                        float maxAllowed = maxRangeOverride > 0f ? maxRangeOverride : ap._launchRangesInUnity.y;
                        __result.Range = Mathf.Min(maxAllowed, bestDirectRange);
                        if (__result.Range > 0f)
                            __result.Issue = null;
                    }
                }

                // Case 2: Pre-launch OTH / EMCON remote FCR coverage across the CEC network.
                if (!HandoffPatch.IsCheckingRadioConnection)
                {
                    float distToTarget = CecCapability.HorizontalDistance(
                        __instance.transform.position,
                        targetObject.transform.position);

                    if (__result.Range < distToTarget
                        && CecCapability.HasRemoteFcrCoverage(__instance, ap, targetObject))
                    {
                        float maxAllowed = maxRangeOverride > 0f ? maxRangeOverride : ap._launchRangesInUnity.y;
                        __result.Range = Mathf.Min(maxAllowed, distToTarget + 50f);
                        __result.Issue = null;
                    }
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] GetMaxRangeForFCR_Postfix exception: {ex}");
            }
        }

        /// <summary>
        /// When <c>OptionsManager.Instance.RequireVisionToEngage == VisionToEngageMode.AssociatedSensors</c>,
        /// <c>EvaluateLimitingSensor</c> inside <see cref="ObjectBase.GetSensorRange"/> clamps
        /// <c>SensorData.Distance</c> to the firing platform's local <c>_associatedSearchSensors</c>,
        /// which is 0 in EMCON or mast-horizon-limited for OTH targets. When a remote CEC/AWACS unit
        /// provides FCR/datalink coverage, lift that clamp so OTH/EMCON launches succeed.
        /// </summary>
        [HarmonyPatch(typeof(ObjectBase), nameof(ObjectBase.GetSensorRange))]
        [HarmonyPostfix]
        private static void GetSensorRange_Postfix(
            ObjectBase __instance,
            ObjectBase targetObject,
            Ammunition ammo,
            ObjectBase.SensorRangeOptions options,
            ref ObjectBase.EstimatedSensorRangeData __result)
        {
            try
            {
                if (HandoffPatch.IsCheckingRadioConnection
                    || __instance == null
                    || targetObject == null
                    || !targetObject.isAirTarget()
                    || ammo == null
                    || ammo._ap == null
                    || __result.SensorData == null)
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
                    __instance.transform.position,
                    targetObject.transform.position);

                if (__result.SensorData.Distance >= distToTarget)
                    return;

                if (CecCapability.HasRemoteFcrCoverage(__instance, ap, targetObject))
                {
                    float cooperativeRange = Mathf.Min(ap._launchRangesInUnity.y, distToTarget + 50f);
                    if (cooperativeRange > __result.SensorData.Distance)
                    {
                        __result.SensorData.Distance = cooperativeRange;
                        if (__result.UnscreenedSensorDistance < cooperativeRange)
                            __result.UnscreenedSensorDistance = cooperativeRange;

                        if (__result.EngagementIssues != null)
                        {
                            __result.EngagementIssues.Remove(ObjectBase.EngagementIssue.CantSeeTarget);
                            __result.EngagementIssues.Remove(ObjectBase.EngagementIssue.NotEmitting);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] GetSensorRange_Postfix exception: {ex}");
            }
        }

        /// <summary>
        /// Allows <c>WeaponSystem.IsVisionRequirementMet</c> to succeed for air targets
        /// when the firing platform is in EMCON or OTH and a cooperative CEC/AWACS radar in the
        /// same network has a live radar track on the target.
        /// </summary>
        [HarmonyPatch(typeof(WeaponSystem), "IsVisionRequirementMet")]
        [HarmonyPostfix]
        private static void IsVisionRequirementMet_Postfix(
            WeaponSystem __instance,
            ObjectBase targetObject,
            ObjectBase.SensorRangeOptions options,
            ref bool __result)
        {
            try
            {
                if (__result || __instance == null || __instance._baseObject == null || targetObject == null)
                    return;

                if (!targetObject.isAirTarget())
                    return;

                if (CecCapability.HasCooperativeAirTrack(__instance._baseObject, targetObject, options))
                {
                    __result = true;
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] IsVisionRequirementMet_Postfix exception: {ex}");
            }
        }
    }
}
