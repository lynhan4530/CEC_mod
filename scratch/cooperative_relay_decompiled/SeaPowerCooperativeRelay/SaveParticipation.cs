using HarmonyLib;
using SeaPower;

namespace SeaPowerCooperativeRelay;

[HarmonyPatch(typeof(ObjectBase), "SaveStateToFile")]
internal static class SaveParticipation
{
	private static void Postfix(ObjectBase __instance, IniHandler ini, string sectionName)
	{
		if (Network.Platform(__instance))
		{
			ini.writeValue(sectionName, "SPCR_Participates", Relay.Participates(__instance));
		}
	}
}
