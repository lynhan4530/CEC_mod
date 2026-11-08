using HarmonyLib;
using SeaPower;
using UnityEngine;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch(typeof(ActiveRadarSeeker), "searchForTarget")]
internal static class CorrelatedSeeker
{
	private static void Prefix(ActiveRadarSeeker __instance, ref ObjectBase ____lastTrackedObject)
	{
		ObjectBase host = __instance.Host;
		Missile i = (Missile)(object)((host is Missile) ? host : null);
		if (i != null && TargetGuard.Managed(i))
		{
			TargetGuard.Clean(i, (SeekerBase)(object)__instance);
			if ((Object)(object)____lastTrackedObject != (Object)null && !TargetGuard.Accepts(i, ____lastTrackedObject))
			{
				____lastTrackedObject = null;
			}
			((SeekerBase)__instance)._potentialContacts.RemoveAll((ObjectBase o) => !TargetGuard.Accepts(i, o));
			((SeekerBase)__instance)._sensorEchoesInRange.RemoveAll((SensorEcho e) => e == null || !TargetGuard.Accepts(i, e._objectBase));
		}
	}

	private static void Postfix(ActiveRadarSeeker __instance, double ____targetLastSeenTime)
	{
		ObjectBase host = __instance.Host;
		Missile val = (Missile)(object)((host is Missile) ? host : null);
		if (val != null && TargetGuard.Managed(val))
		{
			TargetGuard.Clean(val, (SeekerBase)(object)__instance);
			RemoteGuidance.RememberSeeker(val, ____targetLastSeenTime);
			TargetGuard.Observe(val);
		}
	}
}
