using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SeaPower;
using SeapowerUI;
using UnityEngine;

namespace CecMod.Patches
{
    [HarmonyPatch]
    internal static class AttackingStateUpdateDisplayedRangePatch
    {
        private static PropertyInfo _sourceProp;
        private static PropertyInfo _ammunitionProp;
        private static int _lastLoggedTargetId = -1;

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type t = AccessTools.TypeByName("SeapowerUI.ViewModels.AttackingState");
            if (t != null)
            {
                _sourceProp = AccessTools.Property(t, "Source");
                _ammunitionProp = AccessTools.Property(t, "Ammunition");

                MethodInfo m = AccessTools.Method(t, "UpdateDisplayedRange", new Type[] { typeof(object), typeof(Ammunition) });
                if (m != null) yield return m;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(object __instance, object v, Ammunition ammo)
        {
            try
            {
                if (__instance == null || !(v is MapUnitViewModel muvm) || muvm.Unit == null)
                    return;

                ObjectBase shooter = _sourceProp?.GetValue(__instance) as ObjectBase;
                ObjectBase target = muvm.Unit.BaseObject;
                if (shooter == null || target == null || shooter == target)
                    return;

                Ammunition effectiveAmmo = ammo;
                if (effectiveAmmo?._ap == null && _ammunitionProp != null)
                {
                    string ammoName = _ammunitionProp.GetValue(__instance) as string;
                    if (!string.IsNullOrEmpty(ammoName))
                        effectiveAmmo = shooter.getAmmunitionByName(ammoName);
                }

                if (effectiveAmmo?._ap == null)
                    return;

                bool localSight = CecCapability.HasLocalRadarSight(shooter, target);
                if (localSight)
                    return;

                string tag = CecCapability.GetHoverTelemetryTag(shooter, effectiveAmmo._ap, target);
                if (string.IsNullOrEmpty(tag))
                    return;

                if (target.UniqueID != _lastLoggedTargetId)
                {
                    _lastLoggedTargetId = target.UniqueID;
                    string cleanTag = tag.Replace("<LineBreak/>", " ")
                        .Replace("<Run Foreground=\"#FF00FF00\">", "[")
                        .Replace("<Run Foreground=\"#FFFFFF00\">", "[")
                        .Replace("<Run Foreground=\"#FF00FFFF\">", "[")
                        .Replace("</Run>", "]");
                    CecDebugOverlay.LogToDm($"[CEC Target Hover] Shooter: {shooter.FullIDData()} -> Target: {target.FullIDData()} | LocalSight: {localSight} | Telemetry: {cleanTag}", "lime");
                }

                string current = muvm.ContactMouseoverInfo;
                if (string.IsNullOrEmpty(current))
                {
                    muvm.ContactMouseoverInfo = tag.EndsWith("<LineBreak/>", StringComparison.Ordinal)
                        ? tag.Substring(0, tag.Length - "<LineBreak/>".Length)
                        : tag;
                }
                else if (!current.Contains(tag))
                {
                    muvm.ContactMouseoverInfo = tag + current;
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] AttackingStateUpdateDisplayedRangePatch exception: {ex}");
            }
        }
    }

    /// <summary>
    /// Enables real-world CEC / NIFC-CA / APAR / Russian / PLAN cooperative engagement capabilities:
    /// <list type="bullet">
    ///   <item>
    ///     <b>Synthetic Datalink Compatibility (<see cref="SensorSystem.CanGuideAmmo"/>):</b>
    ///     Allows compatible AWACS / ship search radars to provide mid-course datalink updates to
    ///     ARH + MidCourseCorrection missiles and enforces doctrinal SARH illuminator matching.
    ///   </item>
    ///   <item>
    ///     <b>Engage-on-Remote / Launch-on-Remote (OTH &amp; EMCON):</b>
    ///     Gates pre-launch vision and FCR range gates behind <see cref="CecCapability.CanPerformLaunchOnRemote"/>
    ///     so Tier 1 ARH missiles and Forward Pass SARH engagements succeed while Tier 2 Cue Only contacts
    ///     require local radar lock to leave the rail.
    ///   </item>
    /// </list>
    /// </summary>
    [HarmonyPatch]
    internal static class RemoteEngagementPatch
    {
        /// <summary>
        /// Filters <see cref="SensorSystem.CanGuideAmmo"/> through <see cref="CecCapability.CanSensorGuideAmmo"/>.
        /// </summary>
        [HarmonyPatch(typeof(SensorSystem), nameof(SensorSystem.CanGuideAmmo))]
        [HarmonyPostfix]
        private static void CanGuideAmmo_Postfix(SensorSystem __instance, AmmunitionParameters ap, ref bool __result)
        {
            try
            {
                if (!__result)
                    return;

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
        /// (due to OTH radar horizon or EMCON), allow a cooperative radar track to satisfy visibility
        /// if the platform qualifies for Launch-on-Remote.
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

                if (CecCapability.CanPerformLaunchOnRemote(__instance, null, targetObject))
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
        ///     When a consort or AWACS is guiding a missile it does not carry in its own magazine,
        ///     evaluate the platform's own sensors directly.
        ///   </item>
        ///   <item>
        ///     When a firing platform is checking pre-launch FCR range and its local FCRs are horizon-limited
        ///     or in EMCON, check <see cref="CecCapability.CanPerformLaunchOnRemote"/>.
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
                // in its own weapon systems (e.g. an E-2D Hawkeye or consort ship guiding another unit's missile).
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
                        && CecCapability.CanPerformLaunchOnRemote(__instance, ap, targetObject))
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
        /// Lifts local mast horizon clamps in <see cref="ObjectBase.GetSensorRange"/> when
        /// <see cref="CecCapability.CanPerformLaunchOnRemote"/> succeeds.
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

                if (CecCapability.CanPerformLaunchOnRemote(__instance, ap, targetObject))
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
        /// Allows <see cref="WeaponSystem.IsVisionRequirementMet"/> to succeed for air targets
        /// during Launch-on-Remote cooperative engagements.
        /// </summary>
        [HarmonyPatch(typeof(WeaponSystem), nameof(WeaponSystem.IsVisionRequirementMet))]
        [HarmonyPostfix]
        private static void IsVisionRequirementMet_Postfix(
            WeaponSystem __instance,
            ObjectBase targetObject,
            ref float guidanceSystemPenalty,
            ObjectBase.SensorRangeOptions options,
            ref bool __result)
        {
            try
            {
                if (__result || __instance == null || __instance._baseObject == null || targetObject == null)
                    return;

                if (!targetObject.isAirTarget())
                    return;

                AmmunitionParameters ap = __instance._ammoForEngage?._ap;
                if (CecCapability.CanPerformLaunchOnRemote(__instance._baseObject, ap, targetObject))
                {
                    __result = true;
                    guidanceSystemPenalty = 0f;
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] IsVisionRequirementMet_Postfix exception: {ex}");
            }
        }

        /// <summary>
        /// Allows <see cref="WeaponSystem.CanSearchSensorsSeeTarget"/> to succeed for air targets
        /// during Launch-on-Remote cooperative engagements.
        /// </summary>
        [HarmonyPatch(typeof(WeaponSystem), nameof(WeaponSystem.CanSearchSensorsSeeTarget))]
        [HarmonyPostfix]
        private static void CanSearchSensorsSeeTarget_Postfix(
            WeaponSystem __instance,
            ObjectBase targetObject,
            ref float guidanceSystemPenalty,
            ObjectBase.SensorRangeOptions options,
            ref bool __result)
        {
            try
            {
                if (__result || __instance == null || __instance._baseObject == null || targetObject == null)
                    return;

                if (!targetObject.isAirTarget())
                    return;

                AmmunitionParameters ap = __instance._ammoForEngage?._ap;
                if (CecCapability.CanPerformLaunchOnRemote(__instance._baseObject, ap, targetObject))
                {
                    __result = true;
                    guidanceSystemPenalty = 0f;
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] CanSearchSensorsSeeTarget_Postfix exception: {ex}");
            }
        }

        /// <summary>
        /// Lifts <see cref="WeaponSystem.FCRDetectionRange"/> during pre-launch OTH / EMCON checks
        /// when <see cref="CecCapability.CanPerformLaunchOnRemote"/> succeeds.
        /// </summary>
        [HarmonyPatch(typeof(WeaponSystem), nameof(WeaponSystem.FCRDetectionRange))]
        [HarmonyPostfix]
        private static void FCRDetectionRange_Postfix(
            WeaponSystem __instance,
            ObjectBase targetObject,
            Ammunition ammo,
            ref ObjectBase.EngagementIssue? issue,
            ref float __result)
        {
            try
            {
                if (HandoffPatch.IsCheckingRadioConnection
                    || __instance == null
                    || __instance._baseObject == null
                    || targetObject == null
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
                    __instance._baseObject.transform.position,
                    targetObject.transform.position);

                if (__result < distToTarget
                    && CecCapability.CanPerformLaunchOnRemote(__instance._baseObject, ap, targetObject))
                {
                    __result = Mathf.Min(ap._launchRangesInUnity.y, distToTarget + 50f);
                    issue = null;
                }
            }
            catch (Exception ex)
            {
                CecPlugin.Log.LogError($"[CEC] FCRDetectionRange_Postfix exception: {ex}");
            }
        }
    }
}
