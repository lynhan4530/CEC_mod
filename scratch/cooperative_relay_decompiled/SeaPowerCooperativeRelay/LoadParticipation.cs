using HarmonyLib;
using SeaPower;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch(typeof(ObjectBase), "LoadStateFromFile")]
internal static class LoadParticipation
{
	private static void Postfix(ObjectBase __instance, IniHandler ini, string sectionName)
	{
		if (Network.Platform(__instance))
		{
			Relay.RestoreParticipation(__instance, ini.readValue(sectionName, "SPCR_Participates", true, true));
		}
	}
}
