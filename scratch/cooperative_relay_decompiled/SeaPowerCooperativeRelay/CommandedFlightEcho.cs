using HarmonyLib;
using SeaPower;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch(typeof(WeaponBase), "get_CurrentStrongestTargetEcho")]
internal static class CommandedFlightEcho
{
	private static void Postfix(WeaponBase __instance, ref SensorEcho __result)
	{
		__result = TargetGuard.FlightEcho(__instance, __result);
	}
}
