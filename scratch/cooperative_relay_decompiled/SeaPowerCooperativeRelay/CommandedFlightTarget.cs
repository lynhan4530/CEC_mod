using HarmonyLib;
using SeaPower;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch(typeof(WeaponBase), "get_CurrentTarget")]
internal static class CommandedFlightTarget
{
	private static void Postfix(WeaponBase __instance, ref ObjectBase __result)
	{
		__result = TargetGuard.FlightTarget(__instance, __result);
	}
}
