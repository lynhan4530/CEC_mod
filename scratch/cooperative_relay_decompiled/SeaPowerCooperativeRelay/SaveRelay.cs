using HarmonyLib;
using SeaPower;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch(typeof(Missile), "SaveStateToFile")]
internal static class SaveRelay
{
	private static void Postfix(Missile __instance, IniHandler ini, string sectionName)
	{
		Relay.Save(__instance, ini, sectionName);
	}
}
