using HarmonyLib;
using SeaPower;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch(typeof(WeaponSystemHardpoint), "activatePreLaunchGuidance")]
internal static class LocalPrelaunch
{
	private static bool Prefix(WeaponSystemHardpoint __instance, ObjectBase targetObject)
	{
		if (!Network.Designated(((BaseSystem)__instance)._baseObject, ((WeaponSystem)__instance)._ammoForEngage?._ap, targetObject))
		{
			return true;
		}
		((WeaponSystem)__instance)._sensorPreLaunchAligned = Network.Route(((BaseSystem)__instance)._baseObject, ((WeaponSystem)__instance)._ammoForEngage._ap, targetObject, out var _, out var _);
		return false;
	}
}
