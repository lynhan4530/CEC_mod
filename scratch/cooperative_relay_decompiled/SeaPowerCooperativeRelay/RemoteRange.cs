using System;
using HarmonyLib;
using SeaPower;
using UnityEngine;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch(typeof(ObjectBase), "GetSensorRange")]
internal static class RemoteRange
{
	private static void Postfix(ObjectBase __instance, ObjectBase targetObject, Ammunition ammo, ref EstimatedSensorRangeData __result)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Expected O, but got Unknown
		if (!RadioCheck.InProgress && Network.FindRoute(__instance, ammo?._ap, targetObject, null, out var route))
		{
			float num = Math.Max(ammo._ap._launchRangesInUnity.y, Vector3.Distance(((Component)__instance).transform.position, ((GeoPosition)(ref route.Report.Position)).ToUnity()) + 1f);
			__result.Sensor = (SensorSystem)(object)route.Guide;
			__result.SensorData = new SensorDistanceData
			{
				Distance = num
			};
			__result.WeaponSensorLimit = (__result.UnscreenedWeaponSensorLimit = (__result.UnscreenedSensorDistance = num));
			__result.EngagementIssues?.Remove((EngagementIssue)9);
		}
	}
}
