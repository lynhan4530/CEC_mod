using HarmonyLib;
using SeaPower;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch(typeof(Missile), "LoadStateFromFile")]
internal static class LoadRelay
{
	private static void Postfix(Missile __instance, IniHandler ini, string sectionName)
	{
		Relay.Restore(__instance, ini, sectionName);
	}
}
